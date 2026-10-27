using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DupoLang;

namespace DupoCs
{
    /// <summary>
    /// Перевод .dupolang в C#.
    ///
    ///   DupoLang.exe <файл.dupolang> [итог.cs] [ключ=значение ...]
    ///   DupoLang.exe <папка> --out <папка>
    ///   DupoLang.exe --check <файл.dupolang>
    /// </summary>
    static class Program
    {
        static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }

            if (args.Length == 0 || IsHelp(args[0])) { Help(); return args.Length == 0 ? 1 : 0; }

            try
            {
                if (args[0] == "--check") return Check(args);
                return Convert(args);
            }
            catch (DupoError e)
            {
                Console.WriteLine("Ошибка DupoLang → " + e);
                return 2;
            }
            catch (Exception e)
            {
                Console.WriteLine("Ошибка: " + e.Message);
                return 3;
            }
        }

        static bool IsHelp(string a)
        {
            return a == "-h" || a == "--help" || a == "/?" || a == "help";
        }

        static void Help()
        {
            Console.WriteLine("DupoLang " + Dupo.Version + " — перевод .dupolang в C#");
            Console.WriteLine();
            Console.WriteLine("  DupoLang.exe <файл.dupolang> [итог.cs] [ключ=значение ...]");
            Console.WriteLine("      перевести один файл");
            Console.WriteLine();
            Console.WriteLine("  DupoLang.exe <папка> --out <папка>");
            Console.WriteLine("      перевести все .dupolang из папки");
            Console.WriteLine();
            Console.WriteLine("  DupoLang.exe --check <файл.dupolang>");
            Console.WriteLine("      только проверить синтаксис");
            Console.WriteLine();
            Console.WriteLine("Ключи:");
            Console.WriteLine("  namespace=  пространство имён (по умолчанию Generated)");
            Console.WriteLine("  class=      имя класса (по умолчанию Program)");
            Console.WriteLine();
            Console.WriteLine("Примеры:");
            Console.WriteLine("  DupoLang.exe main.dupolang");
            Console.WriteLine("  DupoLang.exe main.dupolang out\\Program.cs namespace=MyApp class=App");
            Console.WriteLine("  DupoLang.exe Projects\\HelloWeb --out cs");
            Console.WriteLine();
            Console.WriteLine("Готовому коду нужна ссылка на DupoLang.dll — оттуда берутся модули.");
        }

        // ---------------- проверка ----------------

        static int Check(string[] args)
        {
            if (args.Length < 2) { Help(); return 1; }

            string file = Path.GetFullPath(args[1]);
            if (!File.Exists(file)) { Console.WriteLine("Нет такого файла: " + file); return 4; }

            string problem = Dupo.Check(File.ReadAllText(file, Encoding.UTF8));
            if (problem == null)
            {
                Console.WriteLine("Синтаксис в порядке: " + Path.GetFileName(file));
                return 0;
            }

            Console.WriteLine(Path.GetFileName(file) + " → " + problem);
            return 2;
        }

        // ---------------- перевод ----------------

        static int Convert(string[] args)
        {
            string source = Path.GetFullPath(args[0]);

            string outPath = null;
            var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--out" && i + 1 < args.Length) { outPath = args[++i]; continue; }

                int eq = args[i].IndexOf('=');
                if (eq > 0) { keys[args[i].Substring(0, eq).Trim()] = args[i].Substring(eq + 1).Trim(); continue; }

                if (outPath == null) outPath = args[i];
            }

            if (Directory.Exists(source))
            {
                string target = Path.GetFullPath(outPath ?? Path.Combine(source, "cs"));
                Directory.CreateDirectory(target);

                string[] files = Directory.GetFiles(source, "*.dupolang", SearchOption.AllDirectories);
                if (files.Length == 0) { Console.WriteLine("В папке нет файлов .dupolang"); return 4; }

                foreach (string file in files)
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    One(file, Path.Combine(target, Safe(name) + ".cs"), keys, Safe(name));
                }

                Console.WriteLine();
                Console.WriteLine("Переведено файлов: " + files.Length);
                return 0;
            }

            if (!File.Exists(source)) { Console.WriteLine("Нет такого файла: " + source); return 4; }

            string outFile = Path.GetFullPath(outPath ??
                Path.Combine(Path.GetDirectoryName(source),
                             Safe(Path.GetFileNameWithoutExtension(source)) + ".cs"));

            One(source, outFile, keys, Safe(Path.GetFileNameWithoutExtension(source)));
            return 0;
        }

        static void One(string source, string outFile, Dictionary<string, string> keys, string fallbackClass)
        {
            string code = File.ReadAllText(source, Encoding.UTF8);
            List<Stmt> program = new Parser(new Lexer(code).Tokenize()).ParseProgram();

            var writer = new CSharpWriter();
            writer.SourceName = Path.GetFileName(source);
            writer.Namespace = Get(keys, "namespace", "Generated");
            writer.ClassName = Get(keys, "class", fallbackClass);

            string result = writer.Build(program);

            string dir = Path.GetDirectoryName(outFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(outFile, result, new UTF8Encoding(true));

            Console.WriteLine(Path.GetFileName(source) + "  →  " + outFile);

            foreach (string note in writer.Notes)
                Console.WriteLine("    внимание: " + note);
        }

        static string Get(Dictionary<string, string> keys, string name, string fallback)
        {
            string v;
            return keys.TryGetValue(name, out v) && v.Length > 0 ? v : fallback;
        }

        /// <summary>Имя файла превращаем в пригодное имя класса.</summary>
        static string Safe(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name ?? "")
                sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');

            string result = sb.ToString();
            if (result.Length == 0) return "Program";
            if (char.IsDigit(result[0])) result = "_" + result;
            return char.ToUpperInvariant(result[0]) + result.Substring(1);
        }
    }
}
