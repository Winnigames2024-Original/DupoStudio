using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using DupoLang;

namespace DupoStudio
{
    /// <summary>
    /// Фоновый сервис студии: <c>DupoStudio.exe --agent</c>.
    /// Окна нет — агент следит за очередью заданий и выполняет их,
    /// чтобы сборка шла, пока студия закрыта.
    /// </summary>
    public static class AgentService
    {
        public const string ServiceName = "DupoStudioAgent";
        public const string ServiceText = "Фоновый сервис Dupo Studio: сборка и запуск проектов";
        public const string Switch = "--agent";

        const string MutexName = @"Local\DupoStudioAgent";
        const string StopEvent = @"Local\DupoStudioAgentStop";

        /// <summary>Папка данных агента.</summary>
        public static string DataDir
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DupoStudio", "agent");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        /// <summary>Очередь заданий: по файлу на задание.</summary>
        public static string QueueDir
        {
            get
            {
                string dir = Path.Combine(DataDir, "queue");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string LogPath { get { return Path.Combine(DataDir, "agent.log"); } }

        // --------------------------------------------------------------
        // описание сервиса для Dupo.Services
        // --------------------------------------------------------------

        /// <summary>Сервис студии как объект Dupo.Services.</summary>
        public static DupoService Describe()
        {
            var svc = new DupoService(ServiceName, ServiceText, ServiceLevel.Background);
            svc.ExePath = Application0();
            svc.Args = Switch;
            return svc;
        }

        static string Application0()
        {
            try { return Process.GetCurrentProcess().MainModule.FileName; }
            catch { return System.Reflection.Assembly.GetEntryAssembly().Location; }
        }

        // --------------------------------------------------------------
        // очередь
        // --------------------------------------------------------------

        /// <summary>Поставить задание в очередь. Команда — строка вида «build C:\Projects\My».</summary>
        public static string Enqueue(string command)
        {
            string file = Path.Combine(QueueDir,
                DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".task");
            File.WriteAllText(file, command, new UTF8Encoding(false));
            return file;
        }

        /// <summary>Сколько заданий ждёт.</summary>
        public static int Pending()
        {
            try { return Directory.GetFiles(QueueDir, "*.task").Length; }
            catch { return 0; }
        }

        /// <summary>Последние строки журнала.</summary>
        public static string Tail(int lines)
        {
            try
            {
                if (!File.Exists(LogPath)) return "журнал пуст";
                var all = File.ReadAllLines(LogPath);
                int from = Math.Max(0, all.Length - lines);
                var sb = new StringBuilder();
                for (int i = from; i < all.Length; i++) sb.AppendLine(all[i]);
                return sb.ToString();
            }
            catch (Exception ex) { return "журнал не прочитан: " + ex.Message; }
        }

        // --------------------------------------------------------------
        // работа агента
        // --------------------------------------------------------------

        /// <summary>Точка входа фонового режима. Возвращает код выхода.</summary>
        public static int Run(string[] args)
        {
            bool once = Has(args, "--once");

            if (Has(args, "--stop")) return Signal();
            if (Has(args, "--status")) return Status();

            bool fresh;
            using (var mutex = new Mutex(true, MutexName, out fresh))
            {
                if (!fresh)
                {
                    Write("Агент уже работает — выхожу");
                    return 0;
                }

                using (var stop = new EventWaitHandle(false, EventResetMode.ManualReset, StopEvent))
                {
                    Write("Агент запущен" + (once ? " (один проход)" : ""));

                    do
                    {
                        try { Pass(); }
                        catch (Exception ex) { Write("Сбой прохода: " + ex.Message); }

                        if (once) break;
                    }
                    while (!stop.WaitOne(TimeSpan.FromSeconds(20)));

                    Write("Агент остановлен");
                }
            }
            return 0;
        }

        static int Signal()
        {
            try
            {
                EventWaitHandle stop;
                if (EventWaitHandle.TryOpenExisting(StopEvent, out stop))
                {
                    using (stop) stop.Set();
                    Write("Получен сигнал остановки");
                    return 0;
                }
            }
            catch { }
            Write("Останавливать нечего — агент не запущен");
            return 1;
        }

        static int Status()
        {
            bool running = false;
            try
            {
                Mutex m;
                running = Mutex.TryOpenExisting(MutexName, out m);
                if (running) m.Dispose();
            }
            catch { }
            Console.WriteLine((running ? "работает" : "остановлен") + ", заданий в очереди: " + Pending());
            return running ? 0 : 1;
        }

        /// <summary>Один проход по очереди.</summary>
        static void Pass()
        {
            string[] tasks;
            try { tasks = Directory.GetFiles(QueueDir, "*.task"); }
            catch { return; }

            Array.Sort(tasks, StringComparer.OrdinalIgnoreCase);

            foreach (string task in tasks)
            {
                string cmd;
                try { cmd = File.ReadAllText(task, Encoding.UTF8).Trim(); }
                catch { continue; }

                try { File.Delete(task); } catch { }

                if (cmd.Length == 0) continue;
                Write("Задание: " + cmd);
                Execute(cmd);
            }
        }

        static void Execute(string cmd)
        {
            string verb = cmd, rest = "";
            int space = cmd.IndexOf(' ');
            if (space > 0) { verb = cmd.Substring(0, space); rest = cmd.Substring(space + 1).Trim(); }

            switch (verb.ToLowerInvariant())
            {
                case "build":
                    RunTool(BuildTools.BuildExe, "\"" + rest.Trim('"') + "\"");
                    break;

                case "cs":
                case "tocsharp":
                    RunTool(BuildTools.LangExe, "\"" + rest.Trim('"') + "\"");
                    break;

                case "run":
                    RunHost(rest.Trim('"'));
                    break;

                case "log":
                    Write(rest);
                    break;

                default:
                    Write("Неизвестное задание: " + verb);
                    break;
            }
        }

        static void RunTool(string exe, string args)
        {
            if (!File.Exists(exe))
            {
                Write("Нет инструмента: " + exe + " — поставьте BuildTools");
                return;
            }
            Shell(exe, args);
        }

        static void RunHost(string dir)
        {
            if (!Directory.Exists(dir)) { Write("Нет папки: " + dir); return; }

            string host;
            try { host = EmbeddedRuntime.HostPath(); }
            catch (Exception ex) { Write("Ядро не распаковалось: " + ex.Message); return; }

            string main = Path.Combine(dir, "main.dupolang");
            if (!File.Exists(main))
            {
                var found = Directory.GetFiles(dir, "*.dupolang");
                if (found.Length == 0) { Write("В папке нет файлов .dupolang: " + dir); return; }
                main = found[0];
            }
            Shell(host, "\"" + main + "\"");
        }

        static void Shell(string exe, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;
                psi.WorkingDirectory = Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory;

                using (var p = Process.Start(psi))
                {
                    string outp = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                    p.WaitForExit(10 * 60 * 1000);
                    foreach (var line in outp.Split('\r', '\n'))
                        if (line.Trim().Length > 0) Write("  " + line.Trim());
                    Write("Готово, код " + p.ExitCode);
                }
            }
            catch (Exception ex) { Write("Не запустилось: " + ex.Message); }
        }

        static bool Has(string[] args, string flag)
        {
            if (args == null) return false;
            foreach (string a in args)
                if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static void Write(string text)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text;
            try { File.AppendAllText(LogPath, line + Environment.NewLine, new UTF8Encoding(false)); }
            catch { }
            try { Console.WriteLine(line); } catch { }
        }
    }
}
