using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;
using DupoLang;

namespace DupoUninstall
{
    /// <summary>
    /// uninstall.exe — удаление приложения. Что именно удалять, написано
    /// в uninstall.dll рядом: название, папка, список файлов, ярлыки
    /// и записи в реестре.
    ///
    ///   uninstall.exe            — спросить и удалить
    ///   uninstall.exe /silent    — удалить без вопросов
    ///   uninstall.exe /info      — показать, что это за приложение
    /// </summary>
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            UninstallData data = UninstallData.Load(dir);

            if (data == null)
            {
                MessageBox.Show(
                    "Рядом нет файла " + UninstallData.FileName + " — непонятно, что удалять." +
                    Environment.NewLine + Environment.NewLine + dir,
                    "Удаление", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 2;
            }

            if (Has(args, "/info"))
            {
                MessageBox.Show(data.Describe(), "Об установке",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }

            // Копируем себя во временную папку: из папки, которую сносим,
            // удалить самого себя нельзя.
            if (!Has(args, "/relocated"))
            {
                string moved = Relocate(dir, args);
                if (moved != null) return 0;
            }

            if (Has(args, "/silent"))
            {
                var remover = new Remover(data);
                remover.Run();
                return 0;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new UninstallForm(data));
            return 0;
        }

        static bool Has(string[] args, string flag)
        {
            foreach (string a in args ?? new string[0])
                if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>
        /// Перезапускает себя из временной папки, прихватив uninstall.dll.
        /// Возвращает путь копии или null, если не получилось.
        /// </summary>
        static string Relocate(string dir, string[] args)
        {
            try
            {
                string temp = Path.Combine(Path.GetTempPath(),
                                           "dupo-uninstall-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temp);

                string self = Process.GetCurrentProcess().MainModule.FileName;
                string copy = Path.Combine(temp, "uninstall.exe");

                File.Copy(self, copy, true);
                File.Copy(Path.Combine(dir, UninstallData.FileName),
                          Path.Combine(temp, UninstallData.FileName), true);

                // В копию кладём путь установки: сама она лежит уже не там.
                var data = UninstallData.Load(temp);
                if (data != null)
                {
                    data.Dir = dir;
                    data.Save(temp);
                }

                var sb = new StringBuilder("/relocated");
                foreach (string a in args ?? new string[0]) sb.Append(' ').Append(a);

                var psi = new ProcessStartInfo(copy, sb.ToString());
                psi.UseShellExecute = false;
                psi.WorkingDirectory = temp;
                Process.Start(psi);

                return copy;
            }
            catch { return null; }
        }
    }
}
