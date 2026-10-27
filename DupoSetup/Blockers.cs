using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace DupoSetup
{
    /// <summary>
    /// Процессы, из-за которых файлы в папке установки заняты. Перед
    /// копированием установщик показывает их список и предлагает закрыть —
    /// иначе получится «файл используется другим процессом» на середине.
    /// </summary>
    public static class Blockers
    {
        /// <summary>Имена без .exe, которые всегда мешают нашей установке.</summary>
        static readonly string[] Ours =
        {
            "DupoStudio", "DupoBuild", "DupoHost", "DupoCs",
            "DupoService", "DupoUpdate", "DupoUninstall"
        };

        /// <summary>Найти процессы, запущенные из папки установки или наши по имени.</summary>
        public static List<Process> Find(string targetDir)
        {
            var found = new List<Process>();
            string dir = Normalize(targetDir);
            int me = Process.GetCurrentProcess().Id;

            foreach (Process p in Process.GetProcesses())
            {
                bool keep = false;
                try
                {
                    if (p.Id == me) { p.Dispose(); continue; }

                    string name = p.ProcessName ?? "";

                    // Запущен из той самой папки — неважно, как называется.
                    string exe = PathOf(p);
                    if (exe != null && dir.Length > 0 && Inside(Normalize(Path.GetDirectoryName(exe)), dir))
                        keep = true;

                    // Или это наша программа, запущенная откуда угодно.
                    if (!keep)
                        foreach (string our in Ours)
                            if (string.Equals(name, our, StringComparison.OrdinalIgnoreCase)) { keep = true; break; }
                }
                catch { keep = false; }

                if (keep) found.Add(p);
                else p.Dispose();
            }
            return found;
        }

        /// <summary>
        /// Спросить и закрыть. true — путь свободен (или мешающих не было),
        /// false — пользователь отказался либо что-то не закрылось.
        /// </summary>
        public static bool Clear(IWin32Window owner, string targetDir, Action<string> log)
        {
            Action<string> say = log ?? delegate { };

            var busy = Find(targetDir);
            if (busy.Count == 0) return true;

            var text = new StringBuilder();
            text.AppendLine("Эти программы мешают установке — файлы заняты:");
            text.AppendLine();
            foreach (Process p in busy)
            {
                string title = "";
                try { title = p.MainWindowTitle ?? ""; } catch { }
                text.Append("    • ").Append(Name(p));
                if (title.Length > 0) text.Append("  —  ").Append(title);
                text.AppendLine();
            }
            text.AppendLine();
            text.AppendLine("Закрыть их и продолжить установку?");

            var answer = MessageBox.Show(owner, text.ToString(), "Dupo Studio — установка",
                                         MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                                         MessageBoxDefaultButton.Button1);

            if (answer != DialogResult.Yes)
            {
                say("Установка остановлена: мешающие программы не закрыты.");
                Release(busy);
                return false;
            }

            bool all = Kill(busy, say);
            Release(busy);

            if (!all)
            {
                // Могли остаться процессы от другого пользователя или служба.
                var left = Find(targetDir);
                int n = left.Count;
                Release(left);

                if (n > 0)
                {
                    MessageBox.Show(owner,
                        "Не получилось закрыть " + n + " программ(ы). Закройте их вручную " +
                        "и запустите установку снова.",
                        "Dupo Studio — установка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
            }

            say("Мешающие программы закрыты.");
            return true;
        }

        /// <summary>Закрыть по-хорошему, а через полторы секунды — насовсем.</summary>
        static bool Kill(List<Process> list, Action<string> say)
        {
            bool all = true;

            foreach (Process p in list)
            {
                string name = Name(p);
                try
                {
                    if (p.HasExited) continue;

                    try { if (p.MainWindowHandle != IntPtr.Zero) p.CloseMainWindow(); }
                    catch { }

                    if (!p.WaitForExit(1500))
                    {
                        p.Kill();
                        p.WaitForExit(3000);
                    }

                    if (p.HasExited) say("Закрыто: " + name);
                    else { say("Не закрылось: " + name); all = false; }
                }
                catch (Exception ex)
                {
                    say("Не закрылось: " + name + " — " + ex.Message);
                    all = false;
                }
            }
            return all;
        }

        static void Release(List<Process> list)
        {
            foreach (Process p in list) { try { p.Dispose(); } catch { } }
        }

        static string Name(Process p)
        {
            try { return (p.ProcessName ?? "программа") + ".exe"; }
            catch { return "программа"; }
        }

        /// <summary>Путь к .exe процесса — без прав может не получиться.</summary>
        static string PathOf(Process p)
        {
            try { return p.MainModule.FileName; }
            catch { return null; }
        }

        /// <summary>Лежит ли папка внутри другой — по частям пути, не по буквам.</summary>
        static bool Inside(string what, string root)
        {
            if (what.Length == 0 || root.Length == 0) return false;
            if (string.Equals(what, root, StringComparison.OrdinalIgnoreCase)) return true;

            return what.StartsWith(root + Path.DirectorySeparatorChar.ToString(),
                                   StringComparison.OrdinalIgnoreCase);
        }

        static string Normalize(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return "";
            try
            {
                string full = Path.GetFullPath(dir);
                return full.TrimEnd('\\', '/');
            }
            catch { return dir.TrimEnd('\\', '/'); }
        }
    }
}
