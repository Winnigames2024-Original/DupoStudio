using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Win32;
using DupoLang;

namespace DupoUpdate
{
    /// <summary>
    /// Режим -autoupdate: update.exe поднимается вместе с системой, тихо сидит
    /// в стороне и сам ставит новые версии. Окон не показывает вовсе — всё,
    /// что он делает, пишется в update.log рядом с программой.
    ///
    /// Файлы меняет не сам: скачивает в сторону и отдаёт список пакетному
    /// файлу (см. Batch) — тот дожидается выхода программы и обновлятора,
    /// переименовывает старое, ставит новое и удаляет остатки.
    /// </summary>
    public static class AutoUpdate
    {
        /// <summary>Ключ автозапуска — туда прописывается команда с -autoupdate.</summary>
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>Имя значения в автозапуске.</summary>
        public const string RunName = "DupoStudioUpdate";

        /// <summary>Сколько ждать после входа в систему: сеть поднимается не сразу.</summary>
        static readonly TimeSpan FirstWait = TimeSpan.FromMinutes(2);

        /// <summary>Как часто проверять дальше.</summary>
        static readonly TimeSpan Every = TimeSpan.FromHours(6);

        static string logFile;

        // ------------------------------------------------------------------
        // запуск
        // ------------------------------------------------------------------

        /// <summary>Главный цикл. Возвращается, только когда пора выходить.</summary>
        public static int Run(UninstallData data, string dir)
        {
            logFile = Path.Combine(dir, "update.log");

            // Вторая копия ни к чему: следит пусть одна.
            bool first;
            using (var only = new Mutex(true, MutexName(dir), out first))
            {
                if (!first)
                {
                    Say("Другой -autoupdate уже работает — выхожу.");
                    return 0;
                }

                // Пользователь мог снять галочку в окне обновления — тогда
                // нас могли запустить и студия, и старая запись автозапуска.
                if (!Wanted(data))
                {
                    Say("Автообновление выключено — выхожу.");
                    return 0;
                }

                Say("Автообновление запущено. Папка: " + dir);

                // Запись могла потеряться: программу распаковали руками или
                // обновили с версии, где автозапуска ещё не было.
                if (!Registered) Register(dir);

                TimeSpan wait = FirstWait;

                while (true)
                {
                    Thread.Sleep(wait);
                    wait = Every;

                    try
                    {
                        if (Once(data, dir)) return 0;   // обновление пошло — выходим
                    }
                    catch (Exception ex) { Say("Проверка сорвалась: " + ex.Message); }
                }
            }
        }

        /// <summary>
        /// Одна проверка. true — обновление запущено и нам пора выйти,
        /// чтобы пакетный файл смог заменить сам update.exe.
        /// </summary>
        static bool Once(UninstallData data, string dir)
        {
            Sync(data, dir);

            Found found;
            try { found = Updater.Latest(); }
            catch (Exception ex)
            {
                // Сети может не быть — это не беда, проверим в следующий раз.
                Say("Не дозвонился до GitHub: " + ex.Message);
                return false;
            }

            string installed = data.AppVersion.Length > 0 ? data.AppVersion : "0.0.0";

            if (Updater.Compare(found.Version, installed) <= 0)
            {
                Say("Обновлений нет. Установлено " + installed + ", доступно " +
                    (found.Version.Length > 0 ? found.Version : "неизвестно") + ".");
                return false;
            }

            Say("Есть версия " + found.Version + " (установлена " + installed + "). Качаю...");

            var updater = new Updater(data, dir);
            updater.Log = Say;

            List<Swap> swaps = updater.Plan(found);
            if (swaps.Count == 0)
            {
                Say("Скачивать оказалось нечего.");
                return false;
            }

            // Приложение могло работать — тогда вернём его после замены.
            string appExe = AppPath(data, dir);
            bool wasRunning = Running(appExe);

            string[] closeFirst = Busy(swaps, appExe);

            Stamp(data, dir, found);

            string bat = Batch.Apply(swaps, updater.Staging, closeFirst,
                                     wasRunning ? appExe : null,
                                     Path.Combine(dir, "update.exe"));

            if (bat == null)
            {
                Say("Пакетный файл не собрался — обновление отложено.");
                return false;
            }

            Say("Файлов к замене: " + swaps.Count + ". Выхожу, дальше работает " +
                Path.GetFileName(bat) + ".");
            return true;
        }

        // ------------------------------------------------------------------
        // мелочи
        // ------------------------------------------------------------------

        /// <summary>Путь к главному файлу приложения.</summary>
        static string AppPath(UninstallData data, string dir)
        {
            string name = string.IsNullOrEmpty(data.MainExe)
                ? "DupoStudio.exe"
                : Path.GetFileName(data.MainExe);

            return Path.Combine(dir, name);
        }

        /// <summary>Запущено ли приложение прямо сейчас.</summary>
        static bool Running(string exe)
        {
            try
            {
                string name = Path.GetFileNameWithoutExtension(exe);
                foreach (var p in Process.GetProcessesByName(name)) { p.Dispose(); return true; }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Имена .exe, которые надо дождаться: всё, что мы собираемся заменить,
        /// плюс само приложение. update.exe в список не входит — его пакетный
        /// файл ждёт по номеру процесса.
        /// </summary>
        static string[] Busy(List<Swap> swaps, string appExe)
        {
            var names = new List<string>();
            names.Add(Path.GetFileName(appExe));

            foreach (Swap s in swaps)
            {
                string name = Path.GetFileName(s.To);
                if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(name, "update.exe", StringComparison.OrdinalIgnoreCase)) continue;
                if (names.Contains(name)) continue;

                names.Add(name);
            }

            return names.ToArray();
        }

        /// <summary>
        /// Привести запись о версии в соответствие с тем, что лежит на диске:
        /// после замены файлов это единственный надёжный источник.
        /// </summary>
        static void Sync(UninstallData data, string dir)
        {
            try
            {
                string exe = AppPath(data, dir);
                if (!File.Exists(exe)) return;

                var info = FileVersionInfo.GetVersionInfo(exe);
                string real = (info.FileVersion ?? info.ProductVersion ?? "").Trim();

                if (real.Length == 0) return;
                if (Updater.Compare(real, data.AppVersion) == 0) return;

                data.AppVersion = real;
                data.Save(dir);
                Say("Версия на диске: " + real + " — запись поправлена.");
            }
            catch { }
        }

        /// <summary>Записать будущую версию заранее: после выхода писать будет некому.</summary>
        static void Stamp(UninstallData data, string dir, Found found)
        {
            try
            {
                if (found.Version.Length == 0) return;

                data.AppVersion = found.Version;
                data.Extra["updated"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                data.Save(dir);
            }
            catch { }
        }

        /// <summary>Своё имя для mutex — у каждой установки оно своё.</summary>
        static string MutexName(string dir)
        {
            var sb = new System.Text.StringBuilder("Local\\DupoStudioAutoUpdate-");
            foreach (char c in (dir ?? "").ToLowerInvariant())
                if (char.IsLetterOrDigit(c)) sb.Append(c);

            return sb.ToString();
        }

        // ------------------------------------------------------------------
        // автозапуск
        // ------------------------------------------------------------------

        /// <summary>
        /// Хочет ли пользователь автообновление. Отказ запоминается в
        /// uninstall.dll: запись в реестре можно потерять при переустановке,
        /// а это — осознанный выбор, и переспрашивать его не нужно.
        /// </summary>
        public static bool Wanted(UninstallData data)
        {
            try
            {
                string v;
                if (data != null && data.Extra.TryGetValue("autoupdate", out v))
                    return !string.Equals(v, "off", StringComparison.OrdinalIgnoreCase);
            }
            catch { }

            return true;
        }

        /// <summary>Включить или выключить автообновление целиком.</summary>
        public static void Want(UninstallData data, string dir, bool on)
        {
            try
            {
                if (data != null)
                {
                    data.Extra["autoupdate"] = on ? "on" : "off";
                    data.Save(dir);
                }
            }
            catch { }

            if (on) Register(dir);
            else Unregister();
        }

        /// <summary>Прописать себя в автозапуск, если записи ещё нет.</summary>
        public static void Register(string dir)
        {
            try
            {
                string exe = Path.Combine(dir, "update.exe");
                if (!File.Exists(exe)) return;

                string want = "\"" + exe + "\" -autoupdate";

                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (k == null) return;

                    string has = k.GetValue(RunName) as string;
                    if (string.Equals(has, want, StringComparison.OrdinalIgnoreCase)) return;

                    k.SetValue(RunName, want);
                    Say("Автозапуск прописан: " + want);
                }
            }
            catch (Exception ex) { Say("Автозапуск не прописался: " + ex.Message); }
        }

        /// <summary>Убрать себя из автозапуска — зовётся при удалении программы.</summary>
        public static void Unregister()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (k.GetValue(RunName) == null) return;

                    k.DeleteValue(RunName, false);
                }
            }
            catch { }
        }

        /// <summary>Стоит ли уже запись автозапуска.</summary>
        public static bool Registered
        {
            get
            {
                try
                {
                    using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                        return k != null && k.GetValue(RunName) != null;
                }
                catch { return false; }
            }
        }

        // ------------------------------------------------------------------
        // журнал
        // ------------------------------------------------------------------

        /// <summary>Окон у нас нет, поэтому всё уходит в update.log рядом с программой.</summary>
        static void Say(string text)
        {
            try
            {
                if (string.IsNullOrEmpty(logFile)) return;

                // Журнал не должен расти бесконечно.
                try
                {
                    var f = new FileInfo(logFile);
                    if (f.Exists && f.Length > 128 * 1024) f.Delete();
                }
                catch { }

                File.AppendAllText(logFile,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text + Environment.NewLine,
                    new System.Text.UTF8Encoding(false));
            }
            catch { }
        }
    }
}
