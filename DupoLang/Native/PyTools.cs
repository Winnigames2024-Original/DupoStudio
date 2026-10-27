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
    /// Dupo.Python — разработка на Python. Если питона в системе нет,
    /// Python.Install() скачивает переносимую сборку с python.org
    /// и кладёт её в runtime\python — ничего не ставя в систему.
    ///
    /// <code>
    /// using Dupo.Python
    ///
    /// if (Python.IsInstalled == false) { Python.Install() }
    /// Python.Run("main.py")
    /// </code>
    /// </summary>
    public class PyTools
    {
        public static readonly PyTools Instance = new PyTools();
        public static Action<string> Log = delegate (string s) { Console.WriteLine(s); };

        const string Index = "https://www.python.org/ftp/python/";
        const string Fallback = "https://www.python.org/ftp/python/3.12.7/python-3.12.7-embed-amd64.zip";

        PyTools() { }

        /// <summary>Куда ставится переносимый питон.</summary>
        public static string Dir
        {
            get { return Path.Combine(DupoRuntime.RuntimeDir, "runtime", "python"); }
        }

        /// <summary>Свой python.exe, если он распакован.</summary>
        public static string OwnExe { get { return Path.Combine(Dir, "python.exe"); } }

        /// <summary>
        /// python.exe: сначала свой, потом из PATH, потом из обычных мест установки.
        /// </summary>
        public static string ExePath
        {
            get
            {
                if (File.Exists(OwnExe)) return OwnExe;

                string found = InPath("python.exe");
                if (found != null) return found;

                foreach (string guess in Guesses())
                    if (File.Exists(guess)) return guess;

                return "";
            }
        }

        public bool IsInstalled { get { return File.Exists(ExePath); } }
        public string Folder { get { return Path.GetDirectoryName(ExePath) ?? Dir; } }

        /// <summary>Свой питон или системный — иногда это важно знать.</summary>
        public bool Portable
        {
            get
            {
                string exe = ExePath;
                return exe.Length > 0 && exe.StartsWith(Dir, StringComparison.OrdinalIgnoreCase);
            }
        }

        public string Version
        {
            get
            {
                string exe = ExePath;
                if (!File.Exists(exe)) return "не установлен";

                string outp = CsTools.Run(exe, "-V", null);
                string first = (outp ?? "").Split('\n').FirstOrDefault(x => x.Trim().Length > 0);
                return string.IsNullOrEmpty(first) ? "неизвестно" : first.Trim();
            }
        }

        // ---------------- установка ----------------

        /// <summary>Скачать переносимый питон и распаковать его в runtime\python.</summary>
        public string Install()
        {
            if (IsInstalled)
            {
                Log("Python уже есть: " + ExePath);
                return ExePath;
            }

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            string url = Newest() ?? Fallback;
            string zip = Path.Combine(Path.GetTempPath(), "dupo-python-" + Guid.NewGuid().ToString("N") + ".zip");

            Log("Качаю Python: " + url);
            using (var wc = new WebClient())
            {
                wc.Headers.Add("User-Agent", "DupoStudio");
                wc.DownloadFile(url, zip);
            }

            Log("Распаковываю...");
            try
            {
                Directory.CreateDirectory(Dir);
                ZipFile.ExtractToDirectory(zip, Dir);
            }
            finally { try { File.Delete(zip); } catch { } }

            OpenImports();

            if (!IsInstalled)
                throw new InvalidOperationException("после распаковки python.exe не нашёлся в " + Dir);

            Log("Готово: " + ExePath + "  (" + Version + ")");
            return ExePath;
        }

        /// <summary>
        /// В переносимой сборке импорт из папки выключен: правим ._pth,
        /// иначе не заработают ни свои модули, ни pip.
        /// </summary>
        static void OpenImports()
        {
            try
            {
                foreach (string file in Directory.GetFiles(Dir, "python*._pth"))
                {
                    var lines = File.ReadAllLines(file).ToList();

                    for (int i = 0; i < lines.Count; i++)
                        if (lines[i].Trim() == "#import site") lines[i] = "import site";

                    if (!lines.Any(x => x.Trim() == "Lib\\site-packages")) lines.Add("Lib\\site-packages");
                    if (!lines.Any(x => x.Trim() == ".")) lines.Add(".");

                    File.WriteAllLines(file, lines.ToArray(), new UTF8Encoding(false));
                }
            }
            catch (Exception ex) { Log("Настройка путей не удалась: " + ex.Message); }
        }

        /// <summary>Свежая переносимая сборка со страницы загрузок python.org.</summary>
        static string Newest()
        {
            try
            {
                string html;
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "DupoStudio");
                    wc.Encoding = Encoding.UTF8;
                    html = wc.DownloadString(Index);
                }

                var versions = Regex.Matches(html, @"href=""(3\.(\d+)\.(\d+))/""")
                    .Cast<Match>()
                    .Select(m => new
                    {
                        Name = m.Groups[1].Value,
                        Minor = int.Parse(m.Groups[2].Value),
                        Patch = int.Parse(m.Groups[3].Value)
                    })
                    .Where(v => v.Minor >= 9 && v.Minor <= 13)
                    .OrderByDescending(v => v.Minor).ThenByDescending(v => v.Patch)
                    .Take(4)
                    .ToList();

                foreach (var v in versions)
                {
                    string url = Index + v.Name + "/python-" + v.Name + "-embed-amd64.zip";
                    if (Head(url)) return url;
                }
            }
            catch (Exception ex) { Log("Список версий не прочитался (" + ex.Message + "), беру запасную."); }

            return null;
        }

        static bool Head(string url)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "HEAD";
                req.Timeout = 15000;
                req.UserAgent = "DupoStudio";

                using (var resp = (HttpWebResponse)req.GetResponse())
                    return resp.StatusCode == HttpStatusCode.OK;
            }
            catch { return false; }
        }

        /// <summary>Удалить свой питон.</summary>
        public bool Uninstall()
        {
            if (!Directory.Exists(Dir)) { Log("Свой Python и не стоял."); return false; }

            try
            {
                Directory.Delete(Dir, true);
                Log("Свой Python удалён.");
                return true;
            }
            catch (Exception ex) { Log("Не удалилось: " + ex.Message); return false; }
        }

        // ---------------- запуск ----------------

        /// <summary>Выполнить файл .py и вернуть его вывод.</summary>
        public string Run(string file)
        {
            return Run(file, "");
        }

        /// <summary>Выполнить файл .py с аргументами.</summary>
        public string Run(string file, string args)
        {
            string exe = Need();
            string path = DupoRuntime.Resolve(file);

            if (!File.Exists(path)) throw new FileNotFoundException("нет файла: " + path);

            Log("Запускаю " + Path.GetFileName(path));

            string outp = CsTools.Run(exe, "\"" + path + "\"" + (args.Length > 0 ? " " + args : ""),
                                      Path.GetDirectoryName(path));

            foreach (string line in (outp ?? "").Split('\r', '\n'))
                if (line.Trim().Length > 0) Log(line.Trim());

            return outp ?? "";
        }

        /// <summary>Выполнить строку кода.</summary>
        public string Code(string code)
        {
            string exe = Need();
            string outp = CsTools.Run(exe, "-c \"" + (code ?? "").Replace("\"", "\\\"") + "\"", null);

            foreach (string line in (outp ?? "").Split('\r', '\n'))
                if (line.Trim().Length > 0) Log(line.Trim());

            return outp ?? "";
        }

        /// <summary>Проверить файл на синтаксис, не выполняя его.</summary>
        public string Check(string file)
        {
            string exe = Need();
            string path = DupoRuntime.Resolve(file);

            string outp = CsTools.Run(exe, "-m py_compile \"" + path + "\"", Path.GetDirectoryName(path));
            string text = (outp ?? "").Trim();

            if (text.Length == 0) { Log("Ошибок нет."); return ""; }

            Log(text);
            return text;
        }

        // ---------------- пакеты ----------------

        /// <summary>Поставить пакет через pip.</summary>
        public string Pip(string package)
        {
            string exe = Need();
            EnsurePip(exe);

            Log("Ставлю " + package + "...");
            string outp = CsTools.Run(exe, "-m pip install --disable-pip-version-check " + package, null);

            foreach (string line in (outp ?? "").Split('\r', '\n'))
                if (line.Trim().Length > 0) Log("  " + line.Trim());

            return outp ?? "";
        }

        /// <summary>Поставленные пакеты.</summary>
        public List<object> Packages()
        {
            string exe = Need();
            string outp = CsTools.Run(exe, "-m pip list --disable-pip-version-check", null);

            return (outp ?? "").Split('\r', '\n')
                .Select(x => x.Trim())
                .Where(x => x.Length > 0 && !x.StartsWith("---") && !x.StartsWith("Package"))
                .Select(x => (object)x)
                .ToList();
        }

        /// <summary>В переносимой сборке pip сначала нужно завести.</summary>
        static void EnsurePip(string exe)
        {
            string check = CsTools.Run(exe, "-m pip --version", null);
            if ((check ?? "").IndexOf("pip", StringComparison.OrdinalIgnoreCase) >= 0) return;

            Log("Завожу pip...");

            string script = Path.Combine(Path.GetTempPath(), "get-pip-" + Guid.NewGuid().ToString("N") + ".py");

            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "DupoStudio");
                    wc.DownloadFile("https://bootstrap.pypa.io/get-pip.py", script);
                }

                CsTools.Run(exe, "\"" + script + "\" --no-warn-script-location", null);
            }
            catch (Exception ex) { Log("pip не завёлся: " + ex.Message); }
            finally { try { File.Delete(script); } catch { } }
        }

        // ---------------- сборка ----------------

        /// <summary>
        /// Собрать .py в один .exe через PyInstaller. Сам PyInstaller
        /// ставится при первом вызове.
        /// </summary>
        public string Build(string file, string outDir)
        {
            string exe = Need();
            string path = DupoRuntime.Resolve(file);
            string target = DupoRuntime.Resolve(string.IsNullOrEmpty(outDir) ? "build" : outDir);

            if (!File.Exists(path)) throw new FileNotFoundException("нет файла: " + path);

            EnsurePip(exe);

            string have = CsTools.Run(exe, "-m PyInstaller --version", null);
            if ((have ?? "").Trim().Length == 0 || have.IndexOf("No module", StringComparison.OrdinalIgnoreCase) >= 0)
                Pip("pyinstaller");

            Directory.CreateDirectory(target);
            Log("Собираю " + Path.GetFileName(path) + " в один exe — это не быстро...");

            string args = "-m PyInstaller --onefile --noconfirm" +
                          " --distpath \"" + target + "\"" +
                          " --workpath \"" + Path.Combine(target, "work") + "\"" +
                          " --specpath \"" + Path.Combine(target, "spec") + "\"" +
                          " \"" + path + "\"";

            string outp = CsTools.Run(exe, args, Path.GetDirectoryName(path));

            foreach (string line in (outp ?? "").Split('\r', '\n'))
                if (line.Trim().Length > 0) Log("  " + line.Trim());

            string made = Path.Combine(target, Path.GetFileNameWithoutExtension(path) + ".exe");
            if (!File.Exists(made))
                throw new InvalidOperationException("exe не появился — смотрите вывод выше");

            Log("Готово: " + made + "  (" + new FileInfo(made).Length / 1024 + " КБ)");
            return made;
        }

        // ---------------- мелочи ----------------

        string Need()
        {
            string exe = ExePath;
            if (exe.Length == 0)
                throw new InvalidOperationException(
                    "Python не найден — вызовите Python.Install(), он скачает переносимую сборку");
            return exe;
        }

        static IEnumerable<string> Guesses()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string files = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            for (int minor = 13; minor >= 8; minor--)
            {
                yield return Path.Combine(local, "Programs", "Python", "Python3" + minor, "python.exe");
                yield return Path.Combine(files, "Python3" + minor, "python.exe");
                yield return @"C:\Python3" + minor + @"\python.exe";
            }
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

                    // В Windows есть заглушка из магазина — она только открывает магазин.
                    if (File.Exists(full) &&
                        full.IndexOf("WindowsApps", StringComparison.OrdinalIgnoreCase) < 0) return full;
                }
                catch { }
            }
            return null;
        }
    }
}
