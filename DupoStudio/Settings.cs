using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DupoStudio
{
    /// <summary>
    /// Настройки студии — простые строки «ключ=значение» в папке пользователя.
    /// Рядом с exe не пишем: студия может лежать там, куда нет доступа на запись.
    /// </summary>
    public static class Settings
    {
        /// <summary>Проект, который открывается сразу при запуске. Пусто — показываем стартовое окно.</summary>
        public static string Autostart = "";

        /// <summary>Не спрашивать про автозапуск при закрытии проекта.</summary>
        public static bool DontAsk;

        public static string FilePath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DupoStudio");
                return Path.Combine(dir, "settings.txt");
            }
        }

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;

                foreach (string raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string value = line.Substring(eq + 1).Trim();

                    if (key == "autostart") Autostart = value;
                    else if (key == "dontask") DontAsk = value == "1" || value == "true";
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(FilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var lines = new List<string>();
                lines.Add("autostart=" + (Autostart ?? ""));
                lines.Add("dontask=" + (DontAsk ? "1" : "0"));

                File.WriteAllLines(FilePath, lines.ToArray(), new UTF8Encoding(false));
            }
            catch { }
        }

        /// <summary>Папка автозапуска, если она ещё существует.</summary>
        public static string AutostartProject
        {
            get
            {
                if (string.IsNullOrEmpty(Autostart)) return null;
                return Directory.Exists(Autostart) ? Autostart : null;
            }
        }
    }
}
