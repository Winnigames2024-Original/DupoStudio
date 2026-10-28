using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;
using DupoLang;

namespace DupoUninstall
{
    /// <summary>Само удаление: файлы, ярлыки, записи в реестре, папка.</summary>
    public class Remover
    {
        const string UninstallRoot = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\";
        const string RunRoot = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValue = "DupoStudioUpdate";
        const string ClassesRoot = @"Software\Classes\";

        readonly UninstallData data;

        public Action<string> Log = delegate { };
        public Action<int> Progress = delegate { };

        public Remover(UninstallData info) { data = info; }

        public void Run()
        {
            Log("Удаление " + data.Name + "...");

            Files();
            Progress(55);

            Shortcuts();
            Progress(70);

            Registry2();
            Progress(85);

            Folder();
            Progress(100);

            Log("Готово.");
        }

        // ------------------------------------------------------------------

        void Files()
        {
            string dir = data.Dir;
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

            int done = 0;
            foreach (string rel in data.Files)
            {
                if (string.IsNullOrEmpty(rel)) continue;

                string file = Path.Combine(dir, rel.Replace('/', Path.DirectorySeparatorChar));
                try { if (File.Exists(file)) File.Delete(file); }
                catch { }

                done++;
                if (data.Files.Count > 0)
                    Progress(10 + (int)(40.0 * done / data.Files.Count));
            }

            // Файлы, которые приложение завело уже после установки,
            // тоже убираем — иначе папка останется с мусором.
            foreach (string name in new[] { "installed.txt", UninstallData.FileName })
            {
                try
                {
                    string extra = Path.Combine(dir, name);
                    if (File.Exists(extra)) File.Delete(extra);
                }
                catch { }
            }

            Log("Файлы удалены");
        }

        void Shortcuts()
        {
            foreach (string key in new[] { "startmenu", "desktop" })
            {
                string path;
                if (!data.Extra.TryGetValue(key, out path) || string.IsNullOrEmpty(path)) continue;

                try { if (File.Exists(path)) File.Delete(path); }
                catch { }
            }

            // На случай, если путей в данных нет — ищем по названию ярлыка.
            string name = string.IsNullOrEmpty(data.Shortcut) ? data.Name : data.Shortcut;
            foreach (Environment.SpecialFolder where in
                     new[] { Environment.SpecialFolder.Programs, Environment.SpecialFolder.DesktopDirectory })
            {
                try
                {
                    string lnk = Path.Combine(Environment.GetFolderPath(where), name + ".lnk");
                    if (File.Exists(lnk)) File.Delete(lnk);
                }
                catch { }
            }

            Log("Ярлыки удалены");
        }

        void Registry2()
        {
            if (!string.IsNullOrEmpty(data.AppId))
            {
                try { Registry.CurrentUser.DeleteSubKeyTree(UninstallRoot + data.AppId, false); }
                catch { }
            }

            foreach (string ext in data.Associations)
            {
                try { Registry.CurrentUser.DeleteSubKeyTree(ClassesRoot + ext, false); }
                catch { }
            }

            string progId;
            if (data.Extra.TryGetValue("progid", out progId) && !string.IsNullOrEmpty(progId))
            {
                try { Registry.CurrentUser.DeleteSubKeyTree(ClassesRoot + progId, false); }
                catch { }
            }

            // Автообновление поднимается вместе с системой — без программы
            // оно будет каждый раз стучаться в пустоту.
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunRoot, true))
                    if (k != null && k.GetValue(RunValue) != null) k.DeleteValue(RunValue, false);
            }
            catch { }

            Log("Записи в реестре удалены");
        }

        void Folder()
        {
            string dir = data.Dir;
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

            // Пустые подпапки убираем снизу вверх, затем и саму папку.
            try
            {
                foreach (string sub in Directory.GetDirectories(dir, "*", SearchOption.AllDirectories))
                    TryDeleteEmpty(sub);

                TryDeleteEmpty(dir);
            }
            catch { }

            if (Directory.Exists(dir))
                Log("Папка осталась — в ней есть ваши файлы: " + dir);
        }

        static void TryDeleteEmpty(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                if (Directory.GetFileSystemEntries(dir).Length > 0) return;
                Directory.Delete(dir);
            }
            catch { }
        }

        /// <summary>Что останется после удаления — для окна подтверждения.</summary>
        public static string Leftovers(UninstallData data)
        {
            var sb = new StringBuilder();
            try
            {
                if (!Directory.Exists(data.Dir)) return "";

                var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string rel in data.Files)
                    known.Add(Path.GetFullPath(Path.Combine(data.Dir, rel.Replace('/', Path.DirectorySeparatorChar))));

                int extra = 0;
                foreach (string f in Directory.GetFiles(data.Dir, "*.*", SearchOption.AllDirectories))
                {
                    string full = Path.GetFullPath(f);
                    if (known.Contains(full)) continue;
                    if (full.EndsWith(UninstallData.FileName, StringComparison.OrdinalIgnoreCase)) continue;
                    if (full.EndsWith("uninstall.exe", StringComparison.OrdinalIgnoreCase)) continue;
                    if (full.EndsWith("installed.txt", StringComparison.OrdinalIgnoreCase)) continue;
                    extra++;
                }

                if (extra > 0)
                    sb.Append("В папке есть ещё " + extra + " файл(ов), появившихся после установки — они останутся.");
            }
            catch { }

            return sb.ToString();
        }
    }
}
