using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using DupoLang;

namespace DupoHost
{
    /// <summary>
    /// Рантайм собранного приложения. Сам по себе пустой — приложение дописывается
    /// в хвост exe студией (Запуск → Собрать в один .exe).
    /// </summary>
    static class Program
    {
        [DllImport("kernel32.dll")] static extern IntPtr GetConsoleWindow();
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        const int SW_HIDE = 0;

        static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }

            string script = null;

            try
            {
                DappBundle.Log = Console.WriteLine;
                DappPackage.Log = delegate (string s) { };
                script = DappBundle.RunSelf();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Вшитое приложение не читается → " + ex.Message);
                return 4;
            }

            if (script != null)
            {
                DappInfo info = DappBundle.LastInfo;
                if (info != null)
                {
                    try { Console.Title = info.Manifest.Name + " " + info.Manifest.Version; } catch { }

                    string console;
                    if (info.Manifest.Extra.TryGetValue("console", out console) &&
                        (console == "off" || console == "hidden" || console == "false" || console == "no"))
                    {
                        ShowWindow(GetConsoleWindow(), SW_HIDE);
                    }
                }
            }
            else
            {
                script = args.Length > 0 ? args[0] : null;

                if (script != null && script.EndsWith(DappPackage.Extension, StringComparison.OrdinalIgnoreCase))
                {
                    DappPackage.Log = Console.WriteLine;
                    script = DappPackage.ExtractToTemp(script);
                }

                if (script == null) script = FindDefaultScript();

                if (script == null || !File.Exists(script))
                {
                    Console.WriteLine("DupoHost " + Dupo.Version + " — рантайм собранных приложений.");
                    Console.WriteLine("Этот exe пустой: соберите приложение в Dupo Studio");
                    Console.WriteLine("(Запуск → Собрать в один .exe) или передайте файл .dupolang / .dapp.");
                    return 1;
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
                Console.WriteLine("Enter — выход.");
                Console.ReadLine();
                return 2;
            }
            catch (Exception e)
            {
                Console.WriteLine();
                Console.WriteLine("Ошибка → " + e.Message);
                Console.WriteLine("Enter — выход.");
                Console.ReadLine();
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

        /// <summary>Скрипт рядом с exe — для сборки «папкой с файлами».</summary>
        static string FindDefaultScript()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;

            string main = Path.Combine(dir, "main.dupolang");
            if (File.Exists(main)) return main;

            string[] scripts = Directory.GetFiles(dir, "*.dupolang");
            if (scripts.Length > 0) return scripts[0];

            string[] packs = Directory.GetFiles(dir, "*" + DappPackage.Extension);
            return packs.Length > 0 ? packs[0] : null;
        }
    }
}
