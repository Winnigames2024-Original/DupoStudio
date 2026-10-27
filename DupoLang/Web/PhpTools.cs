using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace DupoLang
{
    /// <summary>
    /// Web.Server.Php — скачивание и установка PHP для Windows.
    /// Web.Server.Php.Install()
    /// </summary>
    public class PhpTools
    {
        public static readonly PhpTools Instance = new PhpTools();
        public static Action<string> Log = delegate (string s) { Console.WriteLine(s); };

        const string ReleasesJson = "https://windows.php.net/downloads/releases/releases.json";
        const string BaseUrl = "https://windows.php.net/downloads/releases/";
        const string Fallback = "https://windows.php.net/downloads/releases/php-8.3.13-nts-Win32-vs16-x64.zip";

        PhpTools() { }

        /// <summary>Папка, куда ставится PHP.</summary>
        public static string Dir
        {
            get { return Path.Combine(DupoRuntime.RuntimeDir, "runtime", "php"); }
        }

        public static string CgiPath { get { return Path.Combine(Dir, "php-cgi.exe"); } }
        public static string ExePath { get { return Path.Combine(Dir, "php.exe"); } }

        public bool IsInstalled { get { return File.Exists(CgiPath); } }
        public string Folder { get { return Dir; } }

        public string Version
        {
            get
            {
                if (!File.Exists(ExePath)) return "не установлен";
                try
                {
                    var psi = new ProcessStartInfo(ExePath, "-n -v");
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    using (var p = Process.Start(psi))
                    {
                        string all = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                        p.WaitForExit(5000);

                        // Предупреждения печатаются раньше версии, поэтому
                        // ищем нужную строку, а не берём первую попавшуюся.
                        var m = Regex.Match(all, @"PHP\s+\d+\.\d+\.\d+[^\r\n]*");
                        if (m.Success) return m.Value.Trim();

                        string first = all.Split('\r', '\n').FirstOrDefault(x => x.Trim().Length > 0);
                        return string.IsNullOrEmpty(first) ? "неизвестно" : first.Trim();
                    }
                }
                catch (Exception ex) { return "ошибка: " + ex.Message; }
            }
        }

        public string Install() { return Install("8.3"); }

        /// <summary>Скачивает и распаковывает PHP. Возвращает папку установки.</summary>
        public string Install(string branch)
        {
            if (IsInstalled)
            {
                Log("PHP уже установлен: " + Dir);
                return Dir;
            }

            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            string url = FindUrl(branch);
            string tmp = Path.Combine(Path.GetTempPath(), "dupo-php-" + Guid.NewGuid().ToString("N") + ".zip");

            Log("Скачиваю PHP: " + url);
            using (var wc = new WebClient())
            {
                wc.Headers.Add("User-Agent", "DupoStudio/1.0");
                wc.DownloadFile(url, tmp);
            }

            var info = new FileInfo(tmp);
            Log("Загружено " + (info.Length / 1024 / 1024) + " МБ, распаковываю...");

            try { if (Directory.Exists(Dir)) Directory.Delete(Dir, true); } catch { }
            Directory.CreateDirectory(Dir);
            ZipFile.ExtractToDirectory(tmp, Dir);
            try { File.Delete(tmp); } catch { }

            WriteIni();

            if (IsInstalled) Log("PHP установлен: " + Dir);
            else Log("Распаковал, но php-cgi.exe не нашёлся — проверьте " + Dir);

            return Dir;
        }

        public void Uninstall()
        {
            if (!Directory.Exists(Dir)) { Log("PHP не установлен."); return; }
            Directory.Delete(Dir, true);
            Log("PHP удалён.");
        }

        static string FindUrl(string branch)
        {
            try
            {
                string json;
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "DupoStudio/1.0");
                    wc.Encoding = Encoding.UTF8;
                    json = wc.DownloadString(ReleasesJson);
                }

                // В списке лежат сразу несколько веток, и первая попавшаяся
                // обычно самая старая — поэтому выбираем наибольшую версию.
                string best = Newest(json, "php-" + Regex.Escape(branch) + @"\.(\d+)-nts-Win32-vs\d+-x64\.zip");
                if (best != null) return BaseUrl + best;

                best = Newest(json, @"php-(\d+)\.(\d+)\.(\d+)-nts-Win32-vs\d+-x64\.zip");
                if (best != null) return BaseUrl + best;
            }
            catch (Exception ex)
            {
                Log("Список релизов не прочитался (" + ex.Message + "), беру запасную ссылку.");
            }
            return Fallback;
        }

        /// <summary>Оставляет только первое включение каждого расширения.</summary>
        static string NoDuplicateExtensions(string text)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lines = text.Replace("\r\n", "\n").Split('\n');
            var sb = new StringBuilder();

            foreach (string line in lines)
            {
                var m = Regex.Match(line, @"^\s*extension\s*=\s*([A-Za-z0-9_\.]+)\s*$");
                if (m.Success)
                {
                    string name = m.Groups[1].Value;
                    if (name.StartsWith("php_", StringComparison.OrdinalIgnoreCase))
                        name = name.Substring(4);
                    if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                        name = name.Substring(0, name.Length - 4);

                    if (!seen.Add(name))
                    {
                        sb.AppendLine("; " + line.Trim() + "   ; убрано Dupo Studio — дубликат");
                        continue;
                    }
                }
                sb.AppendLine(line);
            }
            return sb.ToString();
        }

        /// <summary>Имя файла с наибольшей версией среди подходящих под шаблон.</summary>
        static string Newest(string json, string pattern)
        {
            string best = null;
            long bestKey = -1;

            foreach (Match m in Regex.Matches(json, pattern))
            {
                long key = 0;
                foreach (var digits in Regex.Matches(m.Value, @"\d+").Cast<Match>().Take(3))
                    key = key * 10000 + long.Parse(digits.Value);

                if (key <= bestKey) continue;
                bestKey = key;
                best = m.Value;
            }
            return best;
        }

        static void WriteIni()
        {
            string src = Path.Combine(Dir, "php.ini-development");
            string dst = Path.Combine(Dir, "php.ini");
            if (File.Exists(dst)) return;

            string text = File.Exists(src) ? File.ReadAllText(src) : "";

            text = Regex.Replace(text, "^;\\s*extension_dir\\s*=\\s*\"ext\"",
                                 "extension_dir = \"ext\"", RegexOptions.Multiline);

            string[] exts = { "curl", "gd", "mbstring", "openssl", "pdo_sqlite", "sqlite3",
                              "mysqli", "pdo_mysql", "fileinfo", "zip", "exif" };
            foreach (string e in exts)
                text = Regex.Replace(text, "^;\\s*extension\\s*=\\s*" + e + "\\s*$",
                                     "extension=" + e, RegexOptions.Multiline);

            // В php.ini-development расширения перечислены дважды, и после
            // раскомментирования PHP ругается «Module already loaded».
            text = NoDuplicateExtensions(text);

            var sb = new StringBuilder(text);
            sb.AppendLine();
            sb.AppendLine("; --- добавлено Dupo Studio ---");
            sb.AppendLine("extension_dir = \"ext\"");
            sb.AppendLine("cgi.force_redirect = 0");
            sb.AppendLine("cgi.fix_pathinfo = 1");
            sb.AppendLine("display_errors = On");
            sb.AppendLine("date.timezone = \"Europe/Moscow\"");

            File.WriteAllText(dst, sb.ToString(), new UTF8Encoding(false));
            Log("Создан php.ini");
        }
    }
}
