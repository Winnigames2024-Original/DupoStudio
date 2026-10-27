using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;

namespace DupoLang
{
    /// <summary>
    /// Тип (уровень) сервиса. Определяет, от чьего имени и как запускается служба.
    /// </summary>
    public enum ServiceLevel
    {
        /// <summary>Служба системы: LocalSystem, автозапуск. Нужны права администратора.</summary>
        SystemProcess,
        /// <summary>Служба с правами LocalService — меньше прав, чем у системы.</summary>
        LocalService,
        /// <summary>Служба с правами NetworkService — выходит в сеть под учётной записью машины.</summary>
        NetworkService,
        /// <summary>Служба системы с отложенным автозапуском (стартует после загрузки).</summary>
        Delayed,
        /// <summary>Служба системы, запуск только вручную.</summary>
        Manual,
        /// <summary>Служба системы с доступом к рабочему столу.</summary>
        Interactive,
        /// <summary>Служба установлена, но отключена.</summary>
        Disabled,
        /// <summary>Процесс пользователя: стартует при входе в систему. Права админа не нужны.</summary>
        UserProcess,
        /// <summary>Фоновый процесс пользователя: при входе, без окна. Права админа не нужны.</summary>
        Background,
        /// <summary>Просто процесс: ничего не прописывается, только Start/Stop вручную.</summary>
        Process
    }

    /// <summary>
    /// Сервис DupoLang.
    /// <code>
    /// using Dupo.Services
    /// Services.Service agent = new Services.Service("DupoAgent", "фоновый агент", Services.ServiceLevel.Background)
    /// agent.Program("agent.exe")
    /// agent.Install()
    /// agent.Start()
    /// </code>
    /// </summary>
    public class DupoService
    {
        public string Name;
        public string Description;
        public ServiceLevel Level;

        /// <summary>Что запускать. По умолчанию — текущий процесс.</summary>
        public string ExePath;

        /// <summary>Аргументы командной строки.</summary>
        public string Args = "";

        /// <summary>Рабочая папка (для процессов пользователя).</summary>
        public string WorkDir = "";

        /// <summary>Номер последнего запущенного процесса (для типов пользователя).</summary>
        int pid;

        public DupoService(string name)
            : this(name, "", ServiceLevel.Background) { }

        public DupoService(string name, string description)
            : this(name, description, ServiceLevel.Background) { }

        public DupoService(string name, string description, ServiceLevel level)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("у сервиса должно быть имя");
            Name = name.Trim();
            Description = description ?? "";
            Level = level;
            ExePath = ServiceTools.SelfPath();
        }

        // --------------------------------------------------------------
        // настройка
        // --------------------------------------------------------------

        /// <summary>Задать исполняемый файл.</summary>
        public DupoService Program(string path)
        {
            ExePath = DupoRuntime.Resolve(path);
            return this;
        }

        /// <summary>Задать исполняемый файл и аргументы.</summary>
        public DupoService Program(string path, string args)
        {
            ExePath = DupoRuntime.Resolve(path);
            Args = args ?? "";
            return this;
        }

        /// <summary>Задать аргументы.</summary>
        public DupoService Arguments(string args)
        {
            Args = args ?? "";
            return this;
        }

        /// <summary>Задать рабочую папку.</summary>
        public DupoService Folder(string dir)
        {
            WorkDir = DupoRuntime.Resolve(dir);
            return this;
        }

        /// <summary>Сменить тип сервиса.</summary>
        public DupoService SetLevel(ServiceLevel level)
        {
            Level = level;
            return this;
        }

        // --------------------------------------------------------------
        // состояние
        // --------------------------------------------------------------

        /// <summary>true, если сервис зарегистрирован в системе.</summary>
        public bool Installed
        {
            get
            {
                return UserMode
                    ? ServiceTools.RunKeyHas(Name)
                    : ServiceTools.ScQuery(Name) != null;
            }
        }

        /// <summary>true, если сервис сейчас работает.</summary>
        public bool Running
        {
            get
            {
                if (UserMode)
                    return ServiceTools.Alive(pid) || ServiceTools.ProcessAlive(ExePath);
                string s = ServiceTools.ScQuery(Name);
                return s != null && s.IndexOf("RUNNING", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        /// <summary>Человеческое описание состояния.</summary>
        public string State
        {
            get
            {
                if (!Installed) return "не установлен";
                return Running ? "работает" : "остановлен";
            }
        }

        /// <summary>Тип сервиса словами.</summary>
        public string LevelName { get { return ServiceTools.LevelName(Level); } }

        /// <summary>Нужны ли права администратора для этого типа.</summary>
        public bool NeedsAdmin { get { return !UserMode; } }

        bool UserMode
        {
            get
            {
                return Level == ServiceLevel.UserProcess
                    || Level == ServiceLevel.Background
                    || Level == ServiceLevel.Process;
            }
        }

        // --------------------------------------------------------------
        // действия
        // --------------------------------------------------------------

        /// <summary>Зарегистрировать сервис.</summary>
        public bool Install()
        {
            if (!File.Exists(ExePath))
            {
                ServiceTools.Say("Сервис " + Name + ": файл не найден — " + ExePath);
                return false;
            }

            if (Level == ServiceLevel.Process)
            {
                ServiceTools.Say("Сервис " + Name + ": тип Process, регистрация не нужна");
                return true;
            }

            if (UserMode)
            {
                ServiceTools.RunKeySet(Name, ServiceTools.Quote(ExePath) + (Args.Length > 0 ? " " + Args : ""));
                ServiceTools.Say("Сервис " + Name + " (" + LevelName + ") прописан на вход в систему");
                return true;
            }

            if (!ServiceTools.IsAdmin)
            {
                ServiceTools.Say("Сервис " + Name + ": для типа " + LevelName + " нужны права администратора");
                return false;
            }

            string bin = ServiceTools.Quote(ExePath) + (Args.Length > 0 ? " " + Args : "");
            var sb = new StringBuilder();
            sb.Append("create ").Append(ServiceTools.Quote(Name));
            sb.Append(" binPath= ").Append(ServiceTools.Quote(bin));
            sb.Append(" start= ").Append(StartMode());
            sb.Append(" obj= ").Append(ServiceTools.Quote(Account()));
            if (Level == ServiceLevel.Interactive) sb.Append(" type= interact type= own");

            string outp = ServiceTools.Sc(sb.ToString());
            bool ok = Installed;

            if (ok && Description.Length > 0)
                ServiceTools.Sc("description " + ServiceTools.Quote(Name) + " " + ServiceTools.Quote(Description));

            if (ok && Level == ServiceLevel.Delayed)
                ServiceTools.Sc("config " + ServiceTools.Quote(Name) + " start= delayed-auto");

            ServiceTools.Say(ok
                ? "Сервис " + Name + " (" + LevelName + ") установлен"
                : "Сервис " + Name + " не установился: " + ServiceTools.OneLine(outp));
            return ok;
        }

        /// <summary>Удалить регистрацию сервиса.</summary>
        public bool Uninstall()
        {
            if (UserMode || Level == ServiceLevel.Process)
            {
                Stop();
                bool had = ServiceTools.RunKeyHas(Name);
                ServiceTools.RunKeyRemove(Name);
                ServiceTools.Say(had ? "Сервис " + Name + " снят с автозапуска" : "Сервис " + Name + " не был установлен");
                return true;
            }

            if (!ServiceTools.IsAdmin)
            {
                ServiceTools.Say("Сервис " + Name + ": удаление требует прав администратора");
                return false;
            }

            Stop();
            string outp = ServiceTools.Sc("delete " + ServiceTools.Quote(Name));
            bool ok = !Installed;
            ServiceTools.Say(ok ? "Сервис " + Name + " удалён" : "Не удалось удалить: " + ServiceTools.OneLine(outp));
            return ok;
        }

        /// <summary>Запустить сервис.</summary>
        public bool Start()
        {
            if (UserMode || Level == ServiceLevel.Process)
            {
                if (Running)
                {
                    ServiceTools.Say("Сервис " + Name + " уже работает");
                    return true;
                }
                if (!File.Exists(ExePath))
                {
                    ServiceTools.Say("Сервис " + Name + ": файл не найден — " + ExePath);
                    return false;
                }

                var psi = ServiceTools.Launcher(ExePath, Args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = Level != ServiceLevel.UserProcess;
                psi.WindowStyle = Level == ServiceLevel.UserProcess
                    ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden;
                psi.WorkingDirectory = WorkDir.Length > 0
                    ? WorkDir : (Path.GetDirectoryName(ExePath) ?? Environment.CurrentDirectory);

                try
                {
                    var started = Process.Start(psi);
                    pid = started != null ? started.Id : 0;
                    ServiceTools.Say("Сервис " + Name + " запущен");
                    return true;
                }
                catch (Exception ex)
                {
                    ServiceTools.Say("Сервис " + Name + " не запустился: " + ex.Message);
                    return false;
                }
            }

            string outp = ServiceTools.Sc("start " + ServiceTools.Quote(Name));
            bool ok = Running;
            ServiceTools.Say(ok ? "Сервис " + Name + " запущен" : "Не запустился: " + ServiceTools.OneLine(outp));
            return ok;
        }

        /// <summary>Остановить сервис.</summary>
        public bool Stop()
        {
            if (UserMode || Level == ServiceLevel.Process)
            {
                int killed = ServiceTools.Kill(pid) + ServiceTools.KillByPath(ExePath);
                pid = 0;
                ServiceTools.Say(killed > 0
                    ? "Сервис " + Name + " остановлен (" + killed + ")"
                    : "Сервис " + Name + " не был запущен");
                return true;
            }

            string outp = ServiceTools.Sc("stop " + ServiceTools.Quote(Name));
            ServiceTools.Say("Сервис " + Name + ": " + ServiceTools.OneLine(outp));
            return !Running;
        }

        /// <summary>Перезапустить сервис.</summary>
        public bool Restart()
        {
            Stop();
            System.Threading.Thread.Sleep(400);
            return Start();
        }

        /// <summary>Сводка по сервису.</summary>
        public string Info()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Сервис: " + Name);
            if (Description.Length > 0) sb.AppendLine("Описание: " + Description);
            sb.AppendLine("Тип: " + LevelName + (NeedsAdmin ? "  (нужен администратор)" : ""));
            sb.AppendLine("Файл: " + ExePath + (Args.Length > 0 ? " " + Args : ""));
            sb.AppendLine("Состояние: " + State);
            return sb.ToString();
        }

        public override string ToString() { return "<сервис " + Name + ": " + State + ">"; }

        string StartMode()
        {
            switch (Level)
            {
                case ServiceLevel.Manual: return "demand";
                case ServiceLevel.Disabled: return "disabled";
                case ServiceLevel.Delayed: return "auto";
                default: return "auto";
            }
        }

        string Account()
        {
            switch (Level)
            {
                case ServiceLevel.LocalService: return "NT AUTHORITY\\LocalService";
                case ServiceLevel.NetworkService: return "NT AUTHORITY\\NetworkService";
                default: return "LocalSystem";
            }
        }
    }

    /// <summary>Общие функции модуля Dupo.Services.</summary>
    public static class ServiceTools
    {
        public static Action<string> Log;

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        internal static void Say(string s)
        {
            var l = Log;
            if (l != null) l(s);
        }

        /// <summary>Запущены ли мы с правами администратора.</summary>
        public static bool IsAdmin
        {
            get
            {
                try
                {
                    using (var id = WindowsIdentity.GetCurrent())
                        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
                }
                catch { return false; }
            }
        }

        /// <summary>Все доступные типы сервиса.</summary>
        public static List<object> Levels()
        {
            return Enum.GetNames(typeof(ServiceLevel)).Select(x => (object)x).ToList();
        }

        /// <summary>Тип сервиса словами.</summary>
        public static string LevelName(ServiceLevel level)
        {
            switch (level)
            {
                case ServiceLevel.SystemProcess: return "служба системы";
                case ServiceLevel.LocalService: return "локальная служба";
                case ServiceLevel.NetworkService: return "сетевая служба";
                case ServiceLevel.Delayed: return "служба с отложенным запуском";
                case ServiceLevel.Manual: return "служба, запуск вручную";
                case ServiceLevel.Interactive: return "служба с доступом к рабочему столу";
                case ServiceLevel.Disabled: return "служба отключена";
                case ServiceLevel.UserProcess: return "процесс пользователя";
                case ServiceLevel.Background: return "фоновый процесс";
                default: return "процесс";
            }
        }

        /// <summary>Есть ли такой сервис (служба или автозапуск).</summary>
        public static bool Exists(string name)
        {
            return RunKeyHas(name) || ScQuery(name) != null;
        }

        /// <summary>Состояние сервиса словами.</summary>
        public static string State(string name)
        {
            if (RunKeyHas(name)) return "в автозапуске пользователя";
            string s = ScQuery(name);
            if (s == null) return "не установлен";
            return s.IndexOf("RUNNING", StringComparison.OrdinalIgnoreCase) >= 0 ? "работает" : "остановлен";
        }

        /// <summary>Сервисы, прописанные на вход в систему.</summary>
        public static List<object> List()
        {
            var res = new List<object>();
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey))
                    if (k != null) foreach (string n in k.GetValueNames()) res.Add(n);
            }
            catch { }
            return res;
        }

        // ---------------- внутреннее ----------------

        internal static string SelfPath()
        {
            try { return Process.GetCurrentProcess().MainModule.FileName; }
            catch { return System.Reflection.Assembly.GetEntryAssembly() != null
                ? System.Reflection.Assembly.GetEntryAssembly().Location : ""; }
        }

        internal static string Quote(string s)
        {
            return "\"" + (s ?? "").Replace("\"", "\\\"") + "\"";
        }

        internal static string OneLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return "(без ответа)";
            var parts = s.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
            return parts.Length == 0 ? "(без ответа)" : string.Join("; ", parts.Take(3));
        }

        internal static string Sc(string args)
        {
            try
            {
                var psi = new ProcessStartInfo("sc.exe", args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.GetEncoding(866);
                psi.StandardErrorEncoding = Encoding.GetEncoding(866);
                using (var p = Process.Start(psi))
                {
                    string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                    p.WaitForExit(15000);
                    return o;
                }
            }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>Ответ sc query или null, если службы нет.</summary>
        internal static string ScQuery(string name)
        {
            string o = Sc("query " + Quote(name));
            if (string.IsNullOrEmpty(o)) return null;
            if (o.IndexOf("1060", StringComparison.Ordinal) >= 0) return null;   // не существует
            if (o.IndexOf("SERVICE_NAME", StringComparison.OrdinalIgnoreCase) < 0 &&
                o.IndexOf("STATE", StringComparison.OrdinalIgnoreCase) < 0) return null;
            return o;
        }

        internal static bool RunKeyHas(string name)
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue(name) != null;
            }
            catch { return false; }
        }

        internal static void RunKeySet(string name, string cmd)
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey))
                    if (k != null) k.SetValue(name, cmd);
            }
            catch (Exception ex) { Say("Автозапуск не записался: " + ex.Message); }
        }

        internal static void RunKeyRemove(string name)
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, true))
                    if (k != null && k.GetValue(name) != null) k.DeleteValue(name, false);
            }
            catch { }
        }

        /// <summary>
        /// Как запускать файл: .bat и .cmd сами не запускаются,
        /// им нужен cmd.exe, скриптам PowerShell — powershell.
        /// </summary>
        internal static ProcessStartInfo Launcher(string exePath, string args)
        {
            string ext = (Path.GetExtension(exePath) ?? "").ToLowerInvariant();
            string tail = string.IsNullOrEmpty(args) ? "" : " " + args;

            if (ext == ".bat" || ext == ".cmd")
                return new ProcessStartInfo("cmd.exe", "/c " + Quote(exePath) + tail);

            if (ext == ".ps1")
                return new ProcessStartInfo("powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -File " + Quote(exePath) + tail);

            return new ProcessStartInfo(exePath, args ?? "");
        }

        internal static bool Alive(int pid)
        {
            if (pid <= 0) return false;
            try { using (var p = Process.GetProcessById(pid)) return !p.HasExited; }
            catch { return false; }
        }

        internal static int Kill(int pid)
        {
            if (pid <= 0) return 0;
            try
            {
                using (var p = Process.GetProcessById(pid))
                {
                    if (p.HasExited) return 0;
                    p.Kill();
                    p.WaitForExit(3000);
                    return 1;
                }
            }
            catch { return 0; }
        }

        internal static bool ProcessAlive(string exePath)
        {
            return Find(exePath).Count > 0;
        }

        internal static int KillByPath(string exePath)
        {
            int n = 0;
            foreach (var p in Find(exePath))
            {
                try { p.Kill(); p.WaitForExit(3000); n++; }
                catch { }
                finally { p.Dispose(); }
            }
            return n;
        }

        static List<Process> Find(string exePath)
        {
            var res = new List<Process>();
            if (string.IsNullOrEmpty(exePath)) return res;

            string name = Path.GetFileNameWithoutExtension(exePath);
            int self = 0;
            try { self = Process.GetCurrentProcess().Id; } catch { }

            Process[] all;
            try { all = Process.GetProcessesByName(name); } catch { return res; }

            foreach (var p in all)
            {
                if (p.Id == self) { p.Dispose(); continue; }
                res.Add(p);
            }
            return res;
        }
    }
}
