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
    /// Dupo.Java — разработка на Java: установка JDK, компиляция, сборка .jar и запуск.
    /// Java.Install() скачивает Temurin JDK с adoptium.net в runtime\jdk.
    /// </summary>
    public class JavaTools
    {
        public static readonly JavaTools Instance = new JavaTools();
        public static Action<string> Log = delegate (string s) { Console.WriteLine(s); };

        const string AssetsApi = "https://api.adoptium.net/v3/assets/latest/{0}/hotspot" +
                                 "?architecture=x64&image_type=jdk&os=windows&vendor=eclipse";
        const string BinaryApi = "https://api.adoptium.net/v3/binary/latest/{0}/ga/windows/x64/jdk/hotspot/normal/eclipse";

        JavaTools() { }

        /// <summary>Папка, куда ставится JDK.</summary>
        public static string Dir
        {
            get { return Path.Combine(DupoRuntime.RuntimeDir, "runtime", "jdk"); }
        }

        /// <summary>Корень JDK — внутри Dir лежит папка вида jdk-21.0.5+11.</summary>
        public static string Home
        {
            get
            {
                if (!Directory.Exists(Dir)) return Dir;
                if (File.Exists(Path.Combine(Dir, "bin", "javac.exe"))) return Dir;

                foreach (string sub in Directory.GetDirectories(Dir))
                    if (File.Exists(Path.Combine(sub, "bin", "javac.exe"))) return sub;

                return Dir;
            }
        }

        public static string JavacPath { get { return Path.Combine(Home, "bin", "javac.exe"); } }
        public static string JavaPath { get { return Path.Combine(Home, "bin", "java.exe"); } }
        public static string JarPath { get { return Path.Combine(Home, "bin", "jar.exe"); } }

        public bool IsInstalled { get { return File.Exists(JavacPath); } }
        public string Folder { get { return Home; } }

        public string Version
        {
            get
            {
                if (!File.Exists(JavaPath)) return "не установлен";
                string outp = Run(JavaPath, "-version", null);
                string first = (outp ?? "").Split('\n').FirstOrDefault(x => x.Trim().Length > 0);
                return string.IsNullOrEmpty(first) ? "неизвестно" : first.Trim();
            }
        }

        // ---------------- установка ----------------

        public string Install() { return Install("21"); }

        /// <summary>Скачивает и распаковывает Temurin JDK. Возвращает папку установки.</summary>
        public string Install(string feature)
        {
            if (IsInstalled)
            {
                Log("JDK уже установлен: " + Home);
                return Home;
            }

            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            if (string.IsNullOrEmpty(feature)) feature = "21";

            string url = FindUrl(feature);
            string tmp = Path.Combine(Path.GetTempPath(), "dupo-jdk-" + Guid.NewGuid().ToString("N") + ".zip");

            Log("Скачиваю JDK " + feature + " (это примерно 190 МБ, потерпите)...");
            Log(url);

            using (var wc = new WebClient())
            {
                wc.Headers.Add("User-Agent", "DupoStudio/1.3");
                wc.DownloadFile(url, tmp);
            }

            var info = new FileInfo(tmp);
            Log("Загружено " + (info.Length / 1024 / 1024) + " МБ, распаковываю...");

            try { if (Directory.Exists(Dir)) Directory.Delete(Dir, true); } catch { }
            Directory.CreateDirectory(Dir);
            ZipFile.ExtractToDirectory(tmp, Dir);
            try { File.Delete(tmp); } catch { }

            if (IsInstalled)
            {
                Log("JDK установлен: " + Home);
                Log(Version);
            }
            else
            {
                Log("Распаковал, но javac.exe не нашёлся — проверьте " + Dir);
            }

            return Home;
        }

        public void Uninstall()
        {
            if (!Directory.Exists(Dir)) { Log("JDK не установлен."); return; }
            Directory.Delete(Dir, true);
            Log("JDK удалён.");
        }

        static string FindUrl(string feature)
        {
            try
            {
                string json;
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "DupoStudio/1.3");
                    wc.Encoding = Encoding.UTF8;
                    json = wc.DownloadString(string.Format(AssetsApi, feature));
                }

                var m = Regex.Match(json, "\"link\"\\s*:\\s*\"(?<u>[^\"]+?\\.zip)\"");
                if (m.Success) return m.Groups["u"].Value.Replace("\\/", "/");
            }
            catch (Exception ex)
            {
                Log("Список сборок не прочитался (" + ex.Message + "), беру прямую ссылку.");
            }
            return string.Format(BinaryApi, feature);
        }

        // ---------------- сборка ----------------

        void NeedJdk()
        {
            if (!IsInstalled)
                throw new DupoError("JDK не установлен. Выполните Java.Install() или «Инструменты → Установить JDK».", 0);
        }

        /// <summary>Компилирует все .java из папки (или один файл) в папку классов.</summary>
        public string Compile(string source, string outDir)
        {
            NeedJdk();

            string src = DupoRuntime.Resolve(source);
            string target = DupoRuntime.Resolve(outDir);
            Directory.CreateDirectory(target);

            string[] files;
            if (Directory.Exists(src)) files = Directory.GetFiles(src, "*.java", SearchOption.AllDirectories);
            else if (File.Exists(src)) files = new[] { src };
            else throw new DupoError("Нет исходников Java: " + src, 0);

            if (files.Length == 0) throw new DupoError("В папке нет файлов .java: " + src, 0);

            string list = Path.Combine(Path.GetTempPath(), "dupo-javac-" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllLines(list, files.Select(f => "\"" + f.Replace("\\", "/") + "\""), new UTF8Encoding(false));

            Log("Компилирую " + files.Length + " файл(ов) Java...");
            string args = "-encoding UTF-8 -nowarn -d \"" + target + "\" \"@" + list + "\"";
            string outp = Run(JavacPath, args, Directory.Exists(src) ? src : Path.GetDirectoryName(src));

            try { File.Delete(list); } catch { }

            if (!string.IsNullOrEmpty(outp) && outp.Trim().Length > 0) Log(outp.Trim());
            Log("Классы готовы: " + target);
            return target;
        }

        /// <summary>Запускает класс с главным методом.</summary>
        public string Run(string classesDir, string mainClass)
        {
            NeedJdk();
            string cp = DupoRuntime.Resolve(classesDir);
            string outp = Run(JavaPath, "-cp \"" + cp + "\" " + mainClass, cp);
            if (!string.IsNullOrEmpty(outp)) Log(outp.TrimEnd());
            return outp;
        }

        /// <summary>Собирает .jar с манифестом, который знает главный класс.</summary>
        public string Jar(string classesDir, string jarFile, string mainClass)
        {
            NeedJdk();

            string classes = DupoRuntime.Resolve(classesDir);
            string jar = DupoRuntime.Resolve(jarFile);
            if (!jar.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)) jar += ".jar";

            string dir = Path.GetDirectoryName(jar);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string args = "cfe \"" + jar + "\" " + mainClass + " -C \"" + classes + "\" .";
            string outp = Run(JarPath, args, classes);
            if (!string.IsNullOrEmpty(outp) && outp.Trim().Length > 0) Log(outp.Trim());

            if (File.Exists(jar))
                Log("Собран " + jar + "  (" + (new FileInfo(jar).Length / 1024) + " КБ)");

            return jar;
        }

        public string RunJar(string jarFile)
        {
            NeedJdk();
            string jar = DupoRuntime.Resolve(jarFile);
            string outp = Run(JavaPath, "-jar \"" + jar + "\"", Path.GetDirectoryName(jar));
            if (!string.IsNullOrEmpty(outp)) Log(outp.TrimEnd());
            return outp;
        }

        /// <summary>Компиляция и упаковка одним шагом.</summary>
        public string Build(string sourceDir, string jarFile, string mainClass)
        {
            string classes = Path.Combine(Path.GetDirectoryName(DupoRuntime.Resolve(jarFile)), "classes");
            Compile(sourceDir, classes);
            return Jar(classes, jarFile, mainClass);
        }

        /// <summary>Создаёт заготовку Java-программы.</summary>
        public string New(string sourceDir, string className)
        {
            string dir = DupoRuntime.Resolve(sourceDir);
            Directory.CreateDirectory(dir);

            if (string.IsNullOrEmpty(className)) className = "Main";
            string file = Path.Combine(dir, className + ".java");

            if (!File.Exists(file))
            {
                var sb = new StringBuilder();
                sb.AppendLine("public class " + className + " {");
                sb.AppendLine("    public static void main(String[] args) {");
                sb.AppendLine("        System.out.println(\"Привет из Java!\");");
                sb.AppendLine();
                sb.AppendLine("        for (int i = 1; i <= 5; i++) {");
                sb.AppendLine("            System.out.println(\"  строка \" + i);");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine("}");
                File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
                Log("Создан " + file);
            }

            return file;
        }

        // ---------------- запуск процессов ----------------

        static string Run(string exe, string args, string workDir)
        {
            var psi = new ProcessStartInfo(exe, args);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
            if (!string.IsNullOrEmpty(workDir) && Directory.Exists(workDir)) psi.WorkingDirectory = workDir;
            psi.EnvironmentVariables["JAVA_HOME"] = Home;

            using (var proc = Process.Start(psi))
            {
                string outp = proc.StandardOutput.ReadToEnd();
                string err = proc.StandardError.ReadToEnd();
                proc.WaitForExit(600000);
                return (outp ?? "") + (string.IsNullOrEmpty(err) ? "" : err);
            }
        }
    }
}
