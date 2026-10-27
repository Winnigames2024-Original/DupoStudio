using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using DupoLang;

namespace DupoSetup
{
    /// <summary>
    /// Установщик. Файлы приложения дописаны в хвост этого exe тем же приёмом,
    /// что и в собранных приложениях DupoLang.
    ///
    ///   DupoSetup.exe                     — установить
    ///   DupoSetup.exe /uninstall          — удалить
    ///   DupoSetup.exe /silent [папка]     — установить без окон
    ///   DupoSetup.exe /pack папка out.exe ключ=значение ...   — собрать установщик
    /// </summary>
    static class Program
    {
        // Приложение оконное, поэтому для /pack и /silent цепляемся
        // к консоли того, кто нас запустил, — иначе вывод уходит в никуда.
        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int processId);

        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length > 0 && args[0].Equals("/pack", StringComparison.OrdinalIgnoreCase))
            {
                try { AttachConsole(-1); } catch { }
                return Pack(args);
            }

            byte[] payload;
            DappInfo pack;

            try
            {
                payload = DappBundle.ReadPayload(DappBundle.SelfPath());
                if (payload == null)
                {
                    Message("Этот установщик пустой.\r\n\r\n" +
                            "Соберите его так:\r\nDupoSetup.exe /pack <папка> <итог.exe> name=... exe=...");
                    return 1;
                }

                string temp = Path.Combine(Path.GetTempPath(), "dupo-read-" + Guid.NewGuid().ToString("N") + ".dapp");
                File.WriteAllBytes(temp, payload);
                try { pack = DappPackage.Read(temp); }
                finally { try { File.Delete(temp); } catch { } }
            }
            catch (Exception ex)
            {
                Message("Установщик повреждён:\r\n" + ex.Message);
                return 2;
            }

            SetupInfo info = SetupInfo.FromManifest(pack.Manifest);

            bool uninstall = Has(args, "/uninstall") || Has(args, "/u");
            bool silent = Has(args, "/silent") || Has(args, "/s");

            if (silent)
            {
                try { AttachConsole(-1); } catch { }
                return Silent(info, payload, args, uninstall);
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (uninstall)
            {
                // Обычно удаляет отдельный uninstall.exe; сюда попадают
                // старые записи в реестре, поэтому оставляем простой путь.
                return Remove(info);
            }

            Application.Run(new SetupForm(info, payload));
            return 0;
        }

        /// <summary>Запасное удаление: спросить и убрать.</summary>
        static int Remove(SetupInfo info)
        {
            string dir = info.DefaultDir;

            var r = MessageBox.Show(
                "Вы действительно хотите удалить " + info.Name + " и все его компоненты?" +
                Environment.NewLine + Environment.NewLine + dir,
                "Удаление — " + info.Name,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (r != DialogResult.Yes) return 0;

            var installer = new Installer(info);
            try
            {
                installer.Uninstall(dir);
                MessageBox.Show(info.Name + " удалён с вашего компьютера.",
                                "Удаление", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не всё удалилось: " + ex.Message,
                                "Удаление", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 3;
            }
        }

        static bool Has(string[] args, string flag)
        {
            foreach (string a in args)
                if (a.Equals(flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static void Message(string text)
        {
            MessageBox.Show(text, "Dupo Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ---------------- без окон ----------------

        static int Silent(SetupInfo info, byte[] payload, string[] args, bool uninstall)
        {
            string dir = info.DefaultDir;
            for (int i = 0; i < args.Length; i++)
                if (!args[i].StartsWith("/") && Path.IsPathRooted(args[i])) { dir = args[i]; break; }

            var installer = new Installer(info);
            installer.Log = Console.WriteLine;

            try
            {
                if (uninstall) installer.Uninstall(dir);
                else installer.Install(dir, payload, info.Desktop,
                                      info.Associations.Count > 0, !Has(args, "/notools"));
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Не получилось: " + ex.Message);
                return 3;
            }
        }

        // ---------------- сборка установщика ----------------

        static int Pack(string[] args)
        {
            if (args.Length < 3)
            {
                Message("DupoSetup.exe /pack <папка> <итог.exe> name=... exe=... version=...");
                return 1;
            }

            string source = Path.GetFullPath(args[1]);
            string outExe = Path.GetFullPath(args[2]);

            var info = new SetupInfo();
            info.Name = Path.GetFileName(source);
            info.Folder = DappPackage.SafeName(info.Name);

            var assoc = new List<string>();

            for (int i = 3; i < args.Length; i++)
            {
                int eq = args[i].IndexOf('=');
                if (eq <= 0) continue;

                string key = args[i].Substring(0, eq).Trim().ToLowerInvariant();
                string value = args[i].Substring(eq + 1).Trim();

                if (key == "name") info.Name = value;
                else if (key == "version") info.Version = value;
                else if (key == "publisher") info.Publisher = value;
                else if (key == "folder") info.Folder = value;
                else if (key == "exe") info.MainExe = value;
                else if (key == "shortcut") info.Shortcut = value;
                else if (key == "desktop") info.Desktop = value == "1";
                else if (key == "assoc") assoc.AddRange(value.Split(',', ';'));
            }

            foreach (string a in assoc)
            {
                string ext = a.Trim();
                if (ext.Length == 0) continue;
                if (ext[0] != '.') ext = "." + ext;
                info.Associations.Add(ext.ToLowerInvariant());
            }

            if (string.IsNullOrEmpty(info.Shortcut)) info.Shortcut = info.Name;

            var manifest = new DappManifest();
            manifest.Main = string.IsNullOrEmpty(info.MainExe) ? "setup" : info.MainExe;
            manifest.Created = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            info.IntoManifest(manifest);

            string temp = Path.Combine(Path.GetTempPath(), "dupo-pack-" + Guid.NewGuid().ToString("N") + ".dapp");

            try
            {
                DappPackage.Log = Console.WriteLine;
                DappBundle.Log = Console.WriteLine;

                DappPackage.Create(source, temp, manifest);
                byte[] payload = File.ReadAllBytes(temp);

                DappBundle.Build(DappBundle.SelfPath(), outExe, payload);
                Console.WriteLine("Установщик собран: " + outExe);
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Не получилось: " + ex.Message);
                return 4;
            }
            finally { try { File.Delete(temp); } catch { } }
        }
    }
}
