using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using DupoLang;

namespace DupoUpdate
{
    /// <summary>
    /// update.exe — обновление приложения с GitHub.
    ///
    ///   update.exe             — окно с проверкой и кнопкой «Обновить»
    ///   update.exe -autoupdate — тихо следит за новыми версиями и ставит их сам;
    ///                            в этом виде он и прописан в автозапуск
    ///   update.exe /check      — только проверить; код 10 — есть новая версия
    ///   update.exe /silent     — обновить без окон
    ///
    /// Что за приложение обновляем, написано в uninstall.dll рядом.
    /// Порядок: приложение, затем сам update.exe, затем инструменты сборки.
    /// </summary>
    static class Program
    {
        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int processId);

        [STAThread]
        static int Main(string[] args)
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            UninstallData data = UninstallData.Load(dir) ?? Guess(dir);

            // Главный режим: запуск вместе с системой, без окон.
            if (Has(args, "-autoupdate") || Has(args, "/autoupdate"))
                return AutoUpdate.Run(data, dir);

            bool check = Has(args, "/check");
            bool silent = Has(args, "/silent") || Has(args, "/s");

            if (check || silent)
            {
                try { AttachConsole(-1); } catch { }
                return Console2(data, dir, check);
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new UpdateForm(data, dir));
            return 0;
        }

        /// <summary>Если uninstall.dll нет — всё равно пробуем обновить студию рядом.</summary>
        static UninstallData Guess(string dir)
        {
            var data = new UninstallData();
            data.Name = "Dupo Studio";
            data.Publisher = "Winnigames2024";
            data.Dir = dir;
            data.MainExe = File.Exists(Path.Combine(dir, "DupoStudio.exe")) ? "DupoStudio.exe" : "";
            data.AppVersion = Version(Path.Combine(dir, data.MainExe));
            return data;
        }

        static string Version(string exe)
        {
            try
            {
                if (!File.Exists(exe)) return "0.0.0";
                var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(exe);
                string v = info.FileVersion ?? info.ProductVersion ?? "0.0.0";
                return v.Trim();
            }
            catch { return "0.0.0"; }
        }

        static bool Has(string[] args, string flag)
        {
            foreach (string a in args ?? new string[0])
                if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // ---------------- без окон ----------------

        static int Console2(UninstallData data, string dir, bool checkOnly)
        {
            try { System.Console.OutputEncoding = Encoding.UTF8; } catch { }

            Found found;
            try { found = Updater.Latest(); }
            catch (Exception ex)
            {
                System.Console.WriteLine("Не получилось проверить: " + ex.Message);
                return 3;
            }

            Stamp here = Stamp.Read(dir);
            if (here.Version.Length == 0)
                here = new Stamp(data.AppVersion.Length > 0 ? data.AppVersion : "0.0.0", 1);

            bool newer = Stamp.Newer(here, found.Build);

            System.Console.WriteLine("Установлено: " + here);
            System.Console.WriteLine("Доступно:    " + found.Build);

            if (checkOnly)
            {
                System.Console.WriteLine(newer ? "Есть обновление." : "Обновлений нет.");
                return newer ? 10 : 0;
            }

            if (!newer)
            {
                System.Console.WriteLine("Обновлять нечего.");
                return 0;
            }

            var updater = new Updater(data, dir);
            updater.Log = System.Console.WriteLine;

            try
            {
                updater.StopApp();
                updater.Run(found);
                updater.ApplySelf();
                System.Console.WriteLine("Готово.");
                return 0;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("Не получилось: " + ex.Message);
                return 4;
            }
        }
    }
}
