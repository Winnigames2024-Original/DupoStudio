using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace DupoLang
{
    /// <summary>
    /// Свой файл имён — как HOSTS в Windows, только для DupoWebServer
    /// и внутренних соединений студии и собранных в ней приложений.
    /// Лежит в папке dupo-Dupo во временных файлах, а если её нет —
    /// в данных приложений.
    ///
    /// Формат тот же, что у системного:
    /// <code>
    /// 127.0.0.1    myapp.local
    /// 127.0.0.1    api.dupo      # комментарий
    /// </code>
    /// </summary>
    public static class DupoHosts
    {
        public const string FolderName = "dupo-Dupo";
        public const string FileName = "hosts.txt";

        static readonly Dictionary<string, string> map =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static DateTime stamp = DateTime.MinValue;
        static string loadedFrom = "";

        /// <summary>Куда пишет сообщения — обычно консоль скрипта.</summary>
        public static Action<string> Log = delegate { };

        // ------------------------------------------------------------------
        // где лежит
        // ------------------------------------------------------------------

        /// <summary>Папка dupo-Dupo во временных файлах.</summary>
        public static string TempDir
        {
            get { return Path.Combine(Path.GetTempPath(), FolderName); }
        }

        /// <summary>Папка dupo-Dupo в данных приложений.</summary>
        public static string DataDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    FolderName);
            }
        }

        /// <summary>
        /// Рабочий файл. Если его нет ни там, ни там — заводим
        /// во временных файлах вместе с подсказкой внутри.
        /// </summary>
        public static string FilePath
        {
            get
            {
                string temp = System.IO.Path.Combine(TempDir, FileName);
                if (File.Exists(temp)) return temp;

                string data = System.IO.Path.Combine(DataDir, FileName);
                if (File.Exists(data)) return data;

                return temp;
            }
        }

        /// <summary>Есть ли файл на диске.</summary>
        public static bool Exists { get { return File.Exists(FilePath); } }

        /// <summary>Сколько имён прописано.</summary>
        public static double Count { get { Refresh(); return map.Count; } }

        /// <summary>Откуда прочитан последний раз.</summary>
        public static string Source { get { Refresh(); return loadedFrom; } }

        /// <summary>Завести файл с подсказкой внутри, если его ещё нет.</summary>
        public static string Create()
        {
            string path = FilePath;
            if (File.Exists(path)) return path;

            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));

                var sb = new StringBuilder();
                sb.AppendLine("# Имена для DupoWebServer и внутренних соединений Dupo Studio.");
                sb.AppendLine("# Формат как у системного HOSTS: адрес, пробел, имя.");
                sb.AppendLine("# Системный файл Windows не трогается — это отдельный, только для Dupo.");
                sb.AppendLine("#");
                sb.AppendLine("# 127.0.0.1    myapp.local");
                sb.AppendLine("# 127.0.0.1    api.dupo");
                sb.AppendLine();

                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                Log("Создан файл имён: " + path);
            }
            catch (Exception ex) { Log("Файл имён не создался: " + ex.Message); }

            return path;
        }

        // ------------------------------------------------------------------
        // чтение
        // ------------------------------------------------------------------

        /// <summary>Перечитать файл, если он поменялся.</summary>
        public static void Refresh()
        {
            string path = FilePath;

            if (!File.Exists(path))
            {
                if (map.Count > 0) map.Clear();
                loadedFrom = "";
                stamp = DateTime.MinValue;
                return;
            }

            DateTime last;
            try { last = File.GetLastWriteTimeUtc(path); }
            catch { return; }

            if (last == stamp && string.Equals(loadedFrom, path, StringComparison.OrdinalIgnoreCase)) return;

            map.Clear();
            stamp = last;
            loadedFrom = path;

            try
            {
                foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string line = raw;

                    int hash = line.IndexOf('#');
                    if (hash >= 0) line = line.Substring(0, hash);

                    line = line.Trim();
                    if (line.Length == 0) continue;

                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;

                    string address = parts[0];
                    for (int i = 1; i < parts.Length; i++)
                    {
                        string name = parts[i].Trim();
                        if (name.Length == 0) continue;
                        map[name] = address;
                    }
                }
            }
            catch (Exception ex) { Log("Файл имён не прочитался: " + ex.Message); }
        }

        /// <summary>Принудительно перечитать.</summary>
        public static double Reload()
        {
            stamp = DateTime.MinValue;
            Refresh();
            return map.Count;
        }

        // ------------------------------------------------------------------
        // использование
        // ------------------------------------------------------------------

        /// <summary>Адрес для имени или пусто, если имени нет.</summary>
        public static string Resolve(string name)
        {
            Refresh();

            string value;
            return map.TryGetValue((name ?? "").Trim(), out value) ? value : "";
        }

        /// <summary>Знаем ли такое имя.</summary>
        public static bool Knows(string name) { return Resolve(name).Length > 0; }

        /// <summary>Все имена.</summary>
        public static List<object> List()
        {
            Refresh();
            return map.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                      .Select(p => (object)(p.Value + "  " + p.Key)).ToList();
        }

        /// <summary>
        /// Подменить имя в адресе на тот, что прописан у нас.
        /// Возвращает адрес как есть, если имя незнакомо.
        /// </summary>
        public static string Rewrite(string url)
        {
            string host = HostOf(url);
            if (host.Length == 0) return url;

            string address = Resolve(host);
            if (address.Length == 0) return url;

            try
            {
                var builder = new UriBuilder(url);
                builder.Host = address;
                return builder.Uri.ToString();
            }
            catch { return url; }
        }

        /// <summary>Имя узла из адреса.</summary>
        public static string HostOf(string url)
        {
            try
            {
                var uri = new Uri(url, UriKind.Absolute);
                return uri.Host;
            }
            catch { return ""; }
        }

        /// <summary>
        /// Готовит запрос: адрес уже с подменённым именем, а заголовок Host
        /// остаётся прежним — иначе сервер не поймёт, какой сайт просят.
        /// </summary>
        public static string Prepare(WebClient client, string url)
        {
            string host = HostOf(url);
            if (host.Length == 0) return url;

            string address = Resolve(host);
            if (address.Length == 0) return url;

            try { client.Headers["Host"] = host; }
            catch { }

            Log("Имя " + host + " взято из " + FileName + ": " + address);
            return Rewrite(url);
        }

        // ------------------------------------------------------------------
        // правка
        // ------------------------------------------------------------------

        /// <summary>Добавить или поменять имя.</summary>
        public static string Add(string name, string address)
        {
            string key = (name ?? "").Trim();
            string value = (address ?? "").Trim();

            if (key.Length == 0) return "";
            if (value.Length == 0) value = "127.0.0.1";

            Create();
            Refresh();

            var lines = new List<string>();
            try { lines.AddRange(File.ReadAllLines(FilePath, Encoding.UTF8)); }
            catch { }

            bool replaced = false;
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                string body = line;

                int hash = body.IndexOf('#');
                if (hash >= 0) body = body.Substring(0, hash);

                var parts = body.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                for (int k = 1; k < parts.Length; k++)
                {
                    if (!string.Equals(parts[k], key, StringComparison.OrdinalIgnoreCase)) continue;
                    lines[i] = value + "    " + key;
                    replaced = true;
                    break;
                }

                if (replaced) break;
            }

            if (!replaced) lines.Add(value + "    " + key);

            try
            {
                File.WriteAllLines(FilePath, lines.ToArray(), new UTF8Encoding(false));
                Reload();
                Log((replaced ? "Имя обновлено: " : "Имя добавлено: ") + key + " → " + value);
            }
            catch (Exception ex) { Log("Не записалось: " + ex.Message); }

            return value;
        }

        /// <summary>Убрать имя.</summary>
        public static bool Remove(string name)
        {
            string key = (name ?? "").Trim();
            if (key.Length == 0 || !File.Exists(FilePath)) return false;

            var keep = new List<string>();
            bool removed = false;

            try
            {
                foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    string body = line;

                    int hash = body.IndexOf('#');
                    if (hash >= 0) body = body.Substring(0, hash);

                    var parts = body.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                    bool hit = parts.Length >= 2 &&
                               parts.Skip(1).Any(p => string.Equals(p, key, StringComparison.OrdinalIgnoreCase));

                    if (hit) { removed = true; continue; }
                    keep.Add(line);
                }

                if (removed)
                {
                    File.WriteAllLines(FilePath, keep.ToArray(), new UTF8Encoding(false));
                    Reload();
                    Log("Имя убрано: " + key);
                }
            }
            catch (Exception ex) { Log("Не записалось: " + ex.Message); }

            return removed;
        }

        /// <summary>Открыть файл в блокноте.</summary>
        public static string Open()
        {
            string path = Create();
            try { System.Diagnostics.Process.Start(path); }
            catch (Exception ex) { Log("Не открылось: " + ex.Message); }
            return path;
        }

        /// <summary>Содержимое файла.</summary>
        public static string Text()
        {
            try { return File.Exists(FilePath) ? File.ReadAllText(FilePath, Encoding.UTF8) : ""; }
            catch { return ""; }
        }
    }
}
