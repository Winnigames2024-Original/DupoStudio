using System;
using System.IO;
using System.Text;
using System.Threading;
using DupoLang;

namespace DupoRun
{
    /// <summary>Консольный запускатель .dupolang — им же собираются готовые приложения.</summary>
    static class Program
    {
        static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }

            string script = args.Length > 0 ? args[0] : FindDefaultScript();

            if (script == null || !File.Exists(script))
            {
                Console.WriteLine("DupoRun " + Dupo.Version);
                Console.WriteLine("Использование: DupoRun.exe <файл.dupolang | архив.dapp>");
                return 1;
            }

            if (script.EndsWith(DappPackage.Extension, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    DappPackage.Log = Console.WriteLine;
                    script = DappPackage.ExtractToTemp(script);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Не удалось открыть .dapp → " + ex.Message);
                    return 4;
                }
            }

            Interp interp = null;
            try
            {
                interp = Dupo.RunFile(script, Console.WriteLine);
            }
            catch (DupoError e)
            {
                Console.WriteLine();
                Console.WriteLine("Ошибка DupoLang → " + e);
                return 2;
            }
            catch (Exception e)
            {
                Console.WriteLine();
                Console.WriteLine("Ошибка → " + e.Message);
                return 3;
            }

            if (interp != null && interp.HasLiveResources)
            {
                Console.WriteLine();
                Console.WriteLine("Приложение работает. Ctrl+C — выход.");
                Console.CancelKeyPress += delegate (object s, ConsoleCancelEventArgs e)
                {
                    e.Cancel = false;
                    interp.Shutdown();
                };
                while (true) Thread.Sleep(1000);
            }

            return 0;
        }

        static string FindDefaultScript()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            string main = Path.Combine(dir, "main.dupolang");
            if (File.Exists(main)) return main;
            string[] found = Directory.GetFiles(dir, "*.dupolang");
            if (found.Length > 0) return found[0];
            string[] packs = Directory.GetFiles(dir, "*" + DappPackage.Extension);
            return packs.Length > 0 ? packs[0] : null;
        }
    }
}
