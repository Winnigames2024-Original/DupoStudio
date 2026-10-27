using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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
                    var psi = new ProcessStartInfo(ExePath, "-v");
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    psi.RedirectStandardOutput = true;
                    using (var p = Process.Start(psi))
                    {
                        string first = p.StandardOutput.ReadLine();
                        p.WaitForExit(5000);
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

                var exact = Regex.Match(json, "php-" + Regex.Escape(branch) + @"\.\d+-nts-Win32-vs\d+-x64\.zip");
                if (exact.Success) return BaseUrl + exact.Value;

                var any = Regex.Match(json, @"php-\d+\.\d+\.\d+-nts-Win32-vs\d+-x64\.zip");
                if (any.Success) return BaseUrl + any.Value;
            }
            catch (Exception ex)
            {
                Log("Список релизов не прочитался (" + ex.Message + "), беру запасную ссылку.");
            }
            return Fallback;
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
