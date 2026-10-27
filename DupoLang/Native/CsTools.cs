using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace DupoLang
{
    /// <summary>
    /// Dupo.CSharp — сборка C# без Visual Studio: компилятор csc.exe
    /// входит в состав .NET Framework, который есть на любой Windows.
    /// <code>
    /// using Dupo.CSharp
    /// CSharp.Build("src", "build/App.exe")          // консоль
    /// CSharp.Forms("src", "build/App.exe")          // WinForms
    /// </code>
    /// </summary>
    public class CsTools
    {
        public static readonly CsTools Instance = new CsTools();
        public static Action<string> Log = delegate (string s) { Console.WriteLine(s); };

        CsTools() { }

        /// <summary>Сборки, которые подключаются всегда.</summary>
        static readonly string[] Basic = { "System.dll", "System.Core.dll", "System.Data.dll", "System.Xml.dll" };

        /// <summary>Дополнительно для WinForms.</summary>
        static readonly string[] FormsRefs = { "System.Windows.Forms.dll", "System.Drawing.dll" };

        // ---------------- где компилятор ----------------

        /// <summary>Путь к csc.exe из .NET Framework 4.x.</summary>
        public static string CscPath
        {
            get
            {
                string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                foreach (string bits in new[] { "Framework64", "Framework" })
                {
                    string root = Path.Combine(win, "Microsoft.NET", bits);
                    if (!Directory.Exists(root)) continue;

                    var vers = Directory.GetDirectories(root, "v4.*");
                    Array.Sort(vers, StringComparer.OrdinalIgnoreCase);
                    for (int i = vers.Length - 1; i >= 0; i--)
                    {
                        string csc = Path.Combine(vers[i], "csc.exe");
                        if (File.Exists(csc)) return csc;
                    }
                }
                return "";
            }
        }

        public bool IsInstalled { get { return File.Exists(CscPath); } }
        public string Folder { get { return Path.GetDirectoryName(CscPath) ?? ""; } }

        public string Version
        {
            get
            {
                string csc = CscPath;
                if (!File.Exists(csc)) return "компилятор не найден";
                string dir = Path.GetFileName(Path.GetDirectoryName(csc) ?? "");
                return ".NET Framework " + dir.TrimStart('v');
            }
        }

        /// <summary>Напоминание, что ставить ничего не нужно.</summary>
        public string Install()
        {
            if (IsInstalled)
            {
                Log("Компилятор C# уже есть: " + CscPath);
                return CscPath;
            }
            Log("csc.exe не найден. Поставьте .NET Framework 4.8 — он включает компилятор C#.");
            return "";
        }

        // ---------------- сборка ----------------

        /// <summary>Собрать консольное приложение из папки или файла.</summary>
        public string Build(string src, string outFile)
        {
            return Compile(src, outFile, "exe", "");
        }

        /// <summary>Собрать приложение WinForms (без окна консоли).</summary>
        public string Forms(string src, string outFile)
        {
            return Compile(src, outFile, "winexe", string.Join(";", FormsRefs));
        }

        /// <summary>Собрать библиотеку .dll.</summary>
        public string Library(string src, string outFile)
        {
            return Compile(src, outFile, "library", "");
        }

        /// <summary>
        /// Собрать: тип — exe, winexe или library; ссылки через ; или пробел.
        /// </summary>
        public string Compile(string src, string outFile, string kind, string refs)
        {
            string csc = CscPath;
            if (!File.Exists(csc))
                throw new InvalidOperationException("csc.exe не найден — нужен .NET Framework 4.x");

            string from = DupoRuntime.Resolve(src);
            var sources = Sources(from);
            if (sources.Count == 0)
                throw new FileNotFoundException("нет файлов .cs в " + from);

            string target = DupoRuntime.Resolve(outFile);
            string dir = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var all = new List<string>(Basic);
            if (string.Equals(kind, "winexe", StringComparison.OrdinalIgnoreCase)) all.AddRange(FormsRefs);
            foreach (string r in (refs ?? "").Split(';', ',', ' '))
                if (r.Trim().Length > 0 && !all.Contains(r.Trim())) all.Add(r.Trim());

            var args = new StringBuilder();
            args.Append("/nologo /optimize+ /warn:1 /utf8output");
            args.Append(" /target:").Append(kind.ToLowerInvariant());
            args.Append(" /out:\"").Append(target).Append("\"");
            foreach (string r in all) args.Append(" /reference:\"").Append(r).Append("\"");
            foreach (string s in sources) args.Append(" \"").Append(s).Append("\"");

            Log("Компилирую C#: " + sources.Count + " файл(ов) → " + Path.GetFileName(target));

            string outp = Run(csc, args.ToString(), Path.GetDirectoryName(target));
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

        /// <summary>Запустить уже собранный .exe и вернуть его вывод.</summary>
        public string Launch(string exe, string args)
        {
            string path = DupoRuntime.Resolve(exe);
            string outp = Run(path, args ?? "", Path.GetDirectoryName(path));
            foreach (string line in (outp ?? "").Split('\r', '\n'))
                if (line.Trim().Length > 0) Log(line.Trim());
            return outp;
        }

        /// <summary>Список файлов .cs: папка берётся со вложенными, файл — как есть.</summary>
        public static List<string> Sources(string from)
        {
            var res = new List<string>();
            if (File.Exists(from)) { res.Add(from); return res; }
            if (!Directory.Exists(from)) return res;

            foreach (string f in Directory.GetFiles(from, "*.cs", SearchOption.AllDirectories))
            {
                string rel = f.Substring(from.Length).Replace('\\', '/');
                if (rel.IndexOf("/obj/", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (rel.IndexOf("/bin/", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                res.Add(f);
            }
            res.Sort(StringComparer.OrdinalIgnoreCase);
            return res;
        }

        internal static string Run(string exe, string args, string workDir)
        {
            var psi = new ProcessStartInfo(exe, args);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
            if (!string.IsNullOrEmpty(workDir) && Directory.Exists(workDir)) psi.WorkingDirectory = workDir;

            using (var p = Process.Start(psi))
            {
                string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit(10 * 60 * 1000);
                return o;
            }
        }
    }
}
