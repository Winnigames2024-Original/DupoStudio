using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;
using DupoLang;

namespace DupoSetup
{
    /// <summary>
    /// Сама установка: распаковка, ярлыки, запись в список программ
    /// и связывание файлов. Всё для текущего пользователя — права
    /// администратора не нужны.
    /// </summary>
    public class Installer
    {
        public Action<string> Log = delegate { };
        public Action<int> Progress = delegate { };

        readonly SetupInfo info;

        public Installer(SetupInfo setup) { info = setup; }

        const string UninstallRoot = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\";
        const string ClassesRoot = @"Software\Classes\";

        /// <summary>Список установленного — по нему же потом удаляем.</summary>
        static string ListFile(string dir) { return Path.Combine(dir, "installed.txt"); }

        // ---------------- установка ----------------

        public void Install(string targetDir, byte[] payload, bool desktopShortcut, bool associate)
        {
            Directory.CreateDirectory(targetDir);

            string temp = Path.Combine(Path.GetTempPath(), "dupo-setup-" + Guid.NewGuid().ToString("N") + ".dapp");
            File.WriteAllBytes(temp, payload);

            try
            {
                DappInfo pack = DappPackage.Read(temp);
                Log("Файлов внутри: " + pack.Entries.Count);

                DappPackage.Log = delegate { };
                DappPackage.Extract(temp, targetDir);
                Progress(60);

                var written = new List<string>();
                foreach (DappEntry entry in pack.Entries) written.Add(entry.Path);

                // установщик кладём рядом — им же потом удалять
                string uninstaller = Path.Combine(targetDir, "uninstall.exe");
                File.Copy(DappBundle.SelfPath(), uninstaller, true);
                written.Add("uninstall.exe");
                Progress(70);

                string exe = string.IsNullOrEmpty(info.MainExe)
                    ? null
                    : Path.Combine(targetDir, info.MainExe.Replace('/', Path.DirectorySeparatorChar));

                if (exe != null && File.Exists(exe))
                {
                    Shortcut(StartMenuPath(), exe, targetDir);
                    Log("Ярлык в меню «Пуск» создан");

                    if (desktopShortcut)
                    {
                        Shortcut(DesktopPath(), exe, targetDir);
                        Log("Ярлык на рабочем столе создан");
                    }
                }
                Progress(85);

                if (associate && exe != null) Associate(exe);

                File.WriteAllLines(ListFile(targetDir), written.ToArray(), new UTF8Encoding(false));
                Register(targetDir, uninstaller);

                Progress(100);
                Log("Готово: " + targetDir);
            }
            finally { try { File.Delete(temp); } catch { } }
        }

        string StartMenuPath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Programs));
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, info.Shortcut + ".lnk");
        }

        string DesktopPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                info.Shortcut + ".lnk");
        }

        /// <summary>Ярлык через WScript.Shell — без ссылки на COM-библиотеку.</summary>
        static void Shortcut(string linkPath, string target, string workDir)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;

                object shell = Activator.CreateInstance(shellType);
                object link = shellType.InvokeMember("CreateShortcut",
                    System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { linkPath });

                Type linkType = link.GetType();
                Set(linkType, link, "TargetPath", target);
                Set(linkType, link, "WorkingDirectory", workDir);
                Set(linkType, link, "IconLocation", target + ",0");

                linkType.InvokeMember("Save",
                    System.Reflection.BindingFlags.InvokeMethod, null, link, new object[0]);
            }
            catch { }
        }

        static void Set(Type t, object obj, string name, object value)
        {
            t.InvokeMember(name, System.Reflection.BindingFlags.SetProperty, null, obj, new[] { value });
        }

        void Associate(string exe)
        {
            string progId = "DupoStudio" + DappPackage.SafeName(info.Folder);

            foreach (string ext in info.Associations)
            {
                try
                {
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(ClassesRoot + ext))
                        if (k != null) k.SetValue("", progId);

                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(
                        ClassesRoot + progId + @"\shell\open\command"))
                        if (k != null) k.SetValue("", "\"" + exe + "\" \"%1\"");

                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(
                        ClassesRoot + progId + @"\DefaultIcon"))
                        if (k != null) k.SetValue("", exe + ",0");

                    Log("Файлы " + ext + " связаны с программой");
                }
                catch { }
            }
        }

        void Register(string targetDir, string uninstaller)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(UninstallRoot + info.AppId))
                {
                    if (k == null) return;

                    k.SetValue("DisplayName", info.Name);
                    k.SetValue("DisplayVersion", info.Version);
                    k.SetValue("Publisher", info.Publisher);
                    k.SetValue("InstallLocation", targetDir);
                    k.SetValue("UninstallString", "\"" + uninstaller + "\" /uninstall");
                    k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    k.SetValue("NoRepair", 1, RegistryValueKind.DWord);

                    if (!string.IsNullOrEmpty(info.MainExe))
                        k.SetValue("DisplayIcon", Path.Combine(targetDir, info.MainExe));
                }
            }
            catch { }
        }

        // ---------------- удаление ----------------

        public void Uninstall(string targetDir)
        {
            Log("Удаляю " + info.Name + "...");

            try
            {
                string list = ListFile(targetDir);
                if (File.Exists(list))
                {
                    foreach (string rel in File.ReadAllLines(list, Encoding.UTF8))
                    {
                        if (rel.Trim().Length == 0) continue;
                        string file = Path.Combine(targetDir, rel.Replace('/', Path.DirectorySeparatorChar));
                        try { if (File.Exists(file)) File.Delete(file); } catch { }
                    }
                    try { File.Delete(list); } catch { }
                }
            }
            catch { }

            try { if (File.Exists(StartMenuPath())) File.Delete(StartMenuPath()); } catch { }
            try { if (File.Exists(DesktopPath())) File.Delete(DesktopPath()); } catch { }

            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallRoot + info.AppId, false); } catch { }

            string progId = "DupoStudio" + DappPackage.SafeName(info.Folder);
            foreach (string ext in info.Associations)
            {
                try { Registry.CurrentUser.DeleteSubKeyTree(ClassesRoot + ext, false); } catch { }
            }
            try { Registry.CurrentUser.DeleteSubKeyTree(ClassesRoot + progId, false); } catch { }

            // Сам установщик сейчас запущен и удалить себя не может —
            // поручаем это короткому пакетному файлу после выхода.
            ScheduleSelfDelete(targetDir);

            Log("Удалено.");
        }

        static void ScheduleSelfDelete(string targetDir)
        {
            try
            {
                string bat = Path.Combine(Path.GetTempPath(), "dupo-clean-" + Guid.NewGuid().ToString("N") + ".bat");

                var sb = new StringBuilder();
                sb.AppendLine("@echo off");
                sb.AppendLine("ping 127.0.0.1 -n 3 >nul");
                sb.AppendLine("rd /s /q \"" + targetDir + "\"");
                sb.AppendLine("del \"%~f0\"");

                File.WriteAllText(bat, sb.ToString(), Encoding.Default);

                var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c \"" + bat + "\"");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                System.Diagnostics.Process.Start(psi);
            }
            catch { }
        }

        /// <summary>Пустые папки убираем, чужие файлы не трогаем.</summary>
        static void CleanEmpty(string dir)
        {
            try
            {
                foreach (string sub in Directory.GetDirectories(dir)) CleanEmpty(sub);
                if (Directory.GetFileSystemEntries(dir).Length == 0) Directory.Delete(dir);
            }
            catch { }
        }
    }
}
