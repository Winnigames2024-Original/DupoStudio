using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DupoLang;

namespace DupoBuild
{
    /// <summary>
    /// Сборка приложения из папки в один .exe — то же, что делает студия,
    /// только из командной строки.
    ///
    ///   DupoBuild.exe <папка> [итог.exe] [ключ=значение ...]
    ///   DupoBuild.exe <папка> --dapp <итог.dapp>
    ///   DupoBuild.exe --info <файл.dapp|файл.exe>
    /// </summary>
    static class Program
    {
        static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }

            if (args.Length == 0 || IsHelp(args[0])) { Help(); return args.Length == 0 ? 1 : 0; }

            try
            {
                if (args[0] == "--info") return Info(args);
                return Build(args);
            }
            catch (DupoError e)
            {
                Console.WriteLine("Ошибка: " + e);
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
            Console.WriteLine("DupoBuild " + Dupo.Version + " — сборка приложений DupoLang");
            Console.WriteLine();
            Console.WriteLine("  DupoBuild.exe <папка> [итог.exe] [ключ=значение ...]");
            Console.WriteLine("      собрать папку в один самостоятельный .exe");
            Console.WriteLine();
            Console.WriteLine("  DupoBuild.exe <папка> --dapp [итог.dapp]");
            Console.WriteLine("      упаковать папку в архив .dapp, без exe");
            Console.WriteLine();
            Console.WriteLine("  DupoBuild.exe --info <файл.dapp или .exe>");
            Console.WriteLine("      показать, что внутри");
            Console.WriteLine();
            Console.WriteLine("Ключи:");
            Console.WriteLine("  name=       название приложения");
            Console.WriteLine("  version=    версия");
            Console.WriteLine("  author=     автор");
            Console.WriteLine("  main=       главный скрипт внутри папки (по умолчанию main.dupolang)");
            Console.WriteLine("  console=off спрятать консольное окно у собранного приложения");
            Console.WriteLine();
            Console.WriteLine("Примеры:");
            Console.WriteLine("  DupoBuild.exe Projects\\HelloWeb");
            Console.WriteLine("  DupoBuild.exe Projects\\HelloWeb out\\Hello.exe name=Привет version=1.0 console=off");
            Console.WriteLine("  DupoBuild.exe Projects\\HelloWeb --dapp");
        }

        // ---------------- сборка ----------------

        static int Build(string[] args)
        {
            string source = Path.GetFullPath(args[0]);
            if (!Directory.Exists(source))
            {
                Console.WriteLine("Нет такой папки: " + source);
                return 4;
            }

            bool dappOnly = false;
            string output = null;
            var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 1; i < args.Length; i++)
            {
                string a = args[i];

                if (a == "--dapp") { dappOnly = true; continue; }

                int eq = a.IndexOf('=');
                if (eq > 0)
                {
                    keys[a.Substring(0, eq).Trim()] = a.Substring(eq + 1).Trim();
                    continue;
                }

                if (output == null) output = a;
            }

            string name = Get(keys, "name", Path.GetFileName(source));

            var manifest = new DappManifest();
            manifest.Name = name;
            manifest.Version = Get(keys, "version", "1.0");
            manifest.Author = Get(keys, "author", "");
            manifest.Main = Get(keys, "main", "main.dupolang");
            manifest.Created = DateTime.Now.ToString("dd.MM.yyyy HH:mm");

            string console = Get(keys, "console", "");
            if (console.Length > 0) manifest.Extra["console"] = console;

            DappPackage.Log = Console.WriteLine;
            DappBundle.Log = Console.WriteLine;

            if (dappOnly)
            {
                string target = output ?? Path.Combine(Path.GetDirectoryName(source),
                                                       DappPackage.SafeName(name) + DappPackage.Extension);
                DappPackage.Create(source, target, manifest);
                return 0;
            }

            string host = FindHost();
            if (host == null)
            {
                Console.WriteLine("Не нашёл DupoHost.exe — он должен лежать рядом с DupoBuild.exe.");
                return 5;
            }

            string exe = output ?? Path.Combine(source, "build", DappPackage.SafeName(name) + ".exe");
            string temp = Path.Combine(Path.GetTempPath(),
                "dupo-build-" + Guid.NewGuid().ToString("N") + DappPackage.Extension);

            try
            {
                DappPackage.Create(source, temp, manifest);
                DappBundle.Build(host, exe, File.ReadAllBytes(temp));
                Console.WriteLine("Готово — это один самостоятельный файл.");
                return 0;
            }
            finally { try { File.Delete(temp); } catch { } }
        }

        static string Get(Dictionary<string, string> keys, string name, string fallback)
        {
            string v;
            return keys.TryGetValue(name, out v) && v.Length > 0 ? v : fallback;
        }

        /// <summary>Рантайм ищем рядом с собой и на этаж выше — как при обычной сборке решения.</summary>
        static string FindHost()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;

            string beside = Path.Combine(dir, "DupoHost.exe");
            if (File.Exists(beside)) return beside;

            try
            {
                var up = new DirectoryInfo(dir);
                for (int i = 0; i < 5 && up != null; i++)
                {
                    string guess = Path.Combine(up.FullName, "DupoHost", "bin", "Release", "net48", "DupoHost.exe");
                    if (File.Exists(guess)) return guess;
                    up = up.Parent;
                }
            }
            catch { }

            return null;
        }

        // ---------------- что внутри ----------------

        static int Info(string[] args)
        {
            if (args.Length < 2) { Help(); return 1; }

            string file = Path.GetFullPath(args[1]);
            if (!File.Exists(file)) { Console.WriteLine("Нет такого файла: " + file); return 4; }

            string dapp = file;
            string temp = null;

            if (!file.EndsWith(DappPackage.Extension, StringComparison.OrdinalIgnoreCase))
            {
                byte[] payload = DappBundle.ReadPayload(file);
                if (payload == null)
                {
                    Console.WriteLine("В этом файле нет вшитого приложения.");
                    return 6;
                }

                temp = Path.Combine(Path.GetTempPath(), "dupo-info-" + Guid.NewGuid().ToString("N") + DappPackage.Extension);
                File.WriteAllBytes(temp, payload);
                dapp = temp;
            }

            try
            {
                DappInfo info = DappPackage.Read(dapp);
                Console.WriteLine(info.Summary());
                Console.WriteLine();

                foreach (DappEntry entry in info.Entries)
                    Console.WriteLine("  " + entry);

                return 0;
            }
            finally { if (temp != null) try { File.Delete(temp); } catch { } }
        }
    }
}
