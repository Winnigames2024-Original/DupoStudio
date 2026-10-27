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
    /// Dupo.Cpp — разработка на C++. Компилятор ищется по порядку:
    /// собственный MinGW в runtime\mingw, затем g++/clang++ из PATH.
    /// Cpp.Install() скачивает сборку MinGW-w64 (winlibs) и распаковывает её.
    /// <code>
    /// using Dupo.Cpp
    /// Cpp.Build("src", "build/App.exe")
    /// Cpp.Run("src", "build/App.exe")
    /// </code>
    /// </summary>
    public class CppTools
    {
        public static readonly CppTools Instance = new CppTools();
        public static Action<string> Log = delegate (string s) { Console.WriteLine(s); };

        const string ReleasesApi =
            "https://api.github.com/repos/brechtsanders/winlibs_mingw/releases?per_page=10";

        CppTools() { }

        /// <summary>Куда ставится свой MinGW.</summary>
        public static string Dir
        {
            get { return Path.Combine(DupoRuntime.RuntimeDir, "runtime", "mingw"); }
        }

        /// <summary>Папка mingw64 внутри Dir — архив разворачивается с подпапкой.</summary>
        public static string Home
        {
            get
            {
                if (!Directory.Exists(Dir)) return Dir;
                if (File.Exists(Path.Combine(Dir, "bin", "g++.exe"))) return Dir;

                foreach (string sub in Directory.GetDirectories(Dir))
                    if (File.Exists(Path.Combine(sub, "bin", "g++.exe"))) return sub;

                return Dir;
            }
        }

        /// <summary>Путь к компилятору: свой MinGW, потом PATH.</summary>
        public static string GppPath
        {
            get
            {
                string own = Path.Combine(Home, "bin", "g++.exe");
                if (File.Exists(own)) return own;

                foreach (string name in new[] { "g++.exe", "clang++.exe" })
                {
                    string found = InPath(name);
                    if (found != null) return found;
                }
                return "";
            }
        }

        public bool IsInstalled { get { return File.Exists(GppPath); } }
        public string Folder { get { return Path.GetDirectoryName(GppPath) ?? Home; } }

        public string Version
        {
            get
            {
                string gpp = GppPath;
                if (!File.Exists(gpp)) return "не установлен";
                string outp = CsTools.Run(gpp, "--version", null);
                string first = (outp ?? "").Split('\n').FirstOrDefault(x => x.Trim().Length > 0);
                return string.IsNullOrEmpty(first) ? "неизвестно" : first.Trim();
            }
        }

        // ---------------- установка ----------------

        /// <summary>Скачивает MinGW-w64 и распаковывает в runtime\mingw.</summary>
        public string Install()
        {
            if (IsInstalled)
            {
                Log("Компилятор C++ уже есть: " + GppPath);
                return GppPath;
            }

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            string url = FindDownload();
            if (url == null)
                throw new InvalidOperationException(
                    "не нашёл, что скачать. Поставьте MinGW-w64 вручную или добавьте g++ в PATH.");

            Directory.CreateDirectory(Dir);
            string zip = Path.Combine(Path.GetTempPath(), "mingw-" + Guid.NewGuid().ToString("N") + ".zip");

            Log("Качаю MinGW-w64 — это сотни мегабайт, наберитесь терпения...");
            using (var wc = new WebClient())
            {
                wc.Headers.Add("User-Agent", "DupoStudio");
                wc.DownloadFile(url, zip);
            }

            Log("Распаковываю...");
            try
            {
                ZipFile.ExtractToDirectory(zip, Dir);
            }
            finally
            {
                try { File.Delete(zip); } catch { }
            }

            if (!IsInstalled)
                throw new InvalidOperationException("после распаковки g++.exe не нашёлся в " + Dir);

            Log("Готово: " + GppPath);
            return GppPath;
        }

        /// <summary>Удалить свой MinGW.</summary>
        public bool Uninstall()
        {
            if (!Directory.Exists(Dir)) { Log("MinGW и не стоял."); return false; }
            try
            {
                Directory.Delete(Dir, true);
                Log("MinGW удалён.");
                return true;
            }
            catch (Exception ex) { Log("Не удалилось: " + ex.Message); return false; }
        }

        /// <summary>Ссылка на свежий 64-битный архив winlibs.</summary>
        static string FindDownload()
        {
            string json;
            try
            {
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "DupoStudio");
                    wc.Encoding = Encoding.UTF8;
                    json = wc.DownloadString(ReleasesApi);
                }
            }
            catch (Exception ex) { Log("GitHub не ответил: " + ex.Message); return null; }

            // Из всех ссылок берём 64-битный zip с ucrt и posix-потоками.
            var urls = Regex.Matches(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+)\"")
                            .Cast<Match>().Select(m => m.Groups[1].Value).ToList();

            foreach (var want in new[] { "ucrt", "msvcrt" })
                foreach (string u in urls)
                {
                    string name = u.Substring(u.LastIndexOf('/') + 1).ToLowerInvariant();
                    if (!name.EndsWith(".zip")) continue;
                    if (name.IndexOf("x86_64", StringComparison.Ordinal) < 0) continue;
                    if (name.IndexOf(want, StringComparison.Ordinal) < 0) continue;
                    if (name.IndexOf("posix", StringComparison.Ordinal) < 0) continue;
                    return u;
                }

            return urls.FirstOrDefault(u => u.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                                            u.IndexOf("x86_64", StringComparison.Ordinal) >= 0);
        }

        // ---------------- сборка ----------------

        /// <summary>Собрать .exe из папки или файла.</summary>
        public string Build(string src, string outFile)
        {
            return Compile(src, outFile, "");
        }

        /// <summary>Собрать приложение без окна консоли (Windows GUI).</summary>
        public string Window(string src, string outFile)
        {
            return Compile(src, outFile, "-mwindows");
        }

        /// <summary>Собрать с дополнительными ключами компилятора.</summary>
        public string Compile(string src, string outFile, string flags)
        {
            string gpp = GppPath;
            if (!File.Exists(gpp))
                throw new InvalidOperationException(
                    "компилятор C++ не найден — вызовите Cpp.Install() или поставьте MinGW-w64");

            string from = DupoRuntime.Resolve(src);
            var sources = Sources(from);
            if (sources.Count == 0)
                throw new FileNotFoundException("нет файлов .cpp в " + from);

            string target = DupoRuntime.Resolve(outFile);
            string dir = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var args = new StringBuilder();
            args.Append("-std=c++17 -O2 -static -static-libgcc -static-libstdc++");
            if (!string.IsNullOrEmpty(flags)) args.Append(' ').Append(flags);
            foreach (string s in sources) args.Append(" \"").Append(s).Append("\"");
            args.Append(" -o \"").Append(target).Append("\"");

            Log("Компилирую C++: " + sources.Count + " файл(ов) → " + Path.GetFileName(target));

            string outp = CsTools.Run(gpp, args.ToString(), Path.GetDirectoryName(target));
            foreach (string line in (outp ?? "").Split('\r', '\n'))
                if (line.Trim().Length > 0) Log("  " + line.Trim());

            if (!File.Exists(target))
                throw new InvalidOperationException("сборка не удалась — " + Path.GetFileName(target) + " не появился");

            Log("Готово: " + target + "  (" + new FileInfo(target).Length / 1024 + " КБ)");
            return target;
        }

        /// <summary>Собрать и сразу запустить.</summary>
        public string Run(string src, string outFile)
        {
            string exe = Build(src, outFile);
            Log("Запускаю " + Path.GetFileName(exe));
            return Launch(exe, "");
        }

        /// <summary>Запустить собранный .exe и вернуть вывод.</summary>
        public string Launch(string exe, string args)
        {
            string path = DupoRuntime.Resolve(exe);
            string outp = CsTools.Run(path, args ?? "", Path.GetDirectoryName(path));
            foreach (string line in (outp ?? "").Split('\r', '\n'))
                if (line.Trim().Length > 0) Log(line.Trim());
            return outp;
        }

        /// <summary>Файлы .cpp / .cc / .cxx из папки, без build и bin.</summary>
        public static List<string> Sources(string from)
        {
            var res = new List<string>();
            if (File.Exists(from)) { res.Add(from); return res; }
            if (!Directory.Exists(from)) return res;

            foreach (string f in Directory.GetFiles(from, "*.*", SearchOption.AllDirectories))
            {
                string ext = (Path.GetExtension(f) ?? "").ToLowerInvariant();
                if (ext != ".cpp" && ext != ".cc" && ext != ".cxx" && ext != ".c") continue;

                string rel = f.Substring(from.Length).Replace('\\', '/');
                if (rel.IndexOf("/build/", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (rel.IndexOf("/bin/", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                res.Add(f);
            }
            res.Sort(StringComparer.OrdinalIgnoreCase);
            return res;
        }

        static string InPath(string exe)
        {
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string part in path.Split(';'))
            {
                if (part.Trim().Length == 0) continue;
                try
                {
                    string full = Path.Combine(part.Trim(), exe);
                    if (File.Exists(full)) return full;
                }
                catch { }
            }
            return null;
        }
    }
}
