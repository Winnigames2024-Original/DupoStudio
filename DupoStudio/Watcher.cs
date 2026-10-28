using System;
using System.Diagnostics;
using System.IO;

namespace DupoStudio
{
    /// <summary>
    /// Тихий наблюдатель за обновлениями — это update.exe с ключом -autoupdate.
    /// Обычно он поднимается вместе с системой, но если его почему-то нет
    /// (студию распаковали руками, запись автозапуска стёрли, пользователь
    /// только что поставил программу и ещё не перезагружался), студия поднимает
    /// его сама при своём запуске.
    /// </summary>
    static class Watcher
    {
        /// <summary>Запустить, если рядом есть update.exe и он ещё не работает.</summary>
        public static void Ensure()
        {
            try
            {
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                string exe = Path.Combine(dir, "update.exe");

                // Студию могли запустить из папки сборки, где обновлятора нет.
                if (!File.Exists(exe)) return;
                if (Working(exe)) return;

                var psi = new ProcessStartInfo(exe, "-autoupdate");
                psi.WorkingDirectory = dir;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;

                Process.Start(psi);
            }
            catch { }
        }

        /// <summary>
        /// Работает ли уже наш update.exe. Смотрим не только на имя, но и на то,
        /// откуда процесс запущен: рядом может крутиться обновлятор чужой
        /// программы с таким же названием.
        /// </summary>
        static bool Working(string exe)
        {
            string want;
            try { want = Path.GetFullPath(exe); }
            catch { return false; }

            foreach (Process p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe)))
            {
                using (p)
                {
                    try
                    {
                        string path = p.MainModule.FileName;
                        if (string.Equals(Path.GetFullPath(path), want, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    catch
                    {
                        // Путь не читается — процесс чужой или прав не хватает.
                        // Считаем, что это не наш, и пробуем запустить свой:
                        // вторую копию он отсечёт сам.
                    }
                }
            }

            return false;
        }
    }
}
