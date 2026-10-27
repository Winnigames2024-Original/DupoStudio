using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;

namespace DupoLang
{
    /// <summary>
    /// BuildTools — DupoBuild.exe и DupoLang.exe. В поставку не входят:
    /// качаются с GitHub по ссылке на последний выпуск, поэтому один
    /// и тот же архив подходит любой версии студии.
    /// </summary>
    public class BuildTools
    {
        public static readonly BuildTools Instance = new BuildTools();
        public static Action<string> Log = delegate (string s) { Console.WriteLine(s); };

        /// <summary>Ссылка всегда ведёт на свежий выпуск — отдельной под каждую версию не нужно.</summary>
        public const string DownloadUrl =
            "https://github.com/Winnigames2024-Original/DupoStudio/releases/latest/download/BuildTools.zip";

        BuildTools() { }

        public static string Dir
        {
            get { return Path.Combine(DupoRuntime.RuntimeDir, "runtime", "tools"); }
        }

        public static string BuildExe { get { return Path.Combine(Dir, "DupoBuild.exe"); } }
        public static string LangExe { get { return Path.Combine(Dir, "DupoLang.exe"); } }
        public static string HostExe { get { return Path.Combine(Dir, "DupoHost.exe"); } }

        public bool IsInstalled { get { return File.Exists(BuildExe) && File.Exists(LangExe); } }
        public string Folder { get { return Dir; } }
        public string Url { get { return DownloadUrl; } }

        /// <summary>Что именно скачалось — из version.txt внутри архива.</summary>
        public string Version
        {
            get
            {
                string file = Path.Combine(Dir, "version.txt");
                if (!File.Exists(file)) return IsInstalled ? "неизвестно" : "не установлены";
                try { return File.ReadAllText(file, Encoding.UTF8).Trim(); }
                catch { return "неизвестно"; }
            }
        }

        // ---------------- установка ----------------

        public string Install()
        {
            if (IsInstalled)
            {
                Log("BuildTools уже на месте: " + Dir);
                return Dir;
            }

            return Reinstall();
        }

        /// <summary>Скачивает заново, даже если уже стоят.</summary>
        public string Reinstall()
        {
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            string tmp = Path.Combine(Path.GetTempPath(), "dupo-tools-" + Guid.NewGuid().ToString("N") + ".zip");

            Log("Скачиваю BuildTools с GitHub...");
            Log(DownloadUrl);

            using (var wc = new WebClient())
            {
                wc.Headers.Add("User-Agent", "DupoStudio/" + Dupo.Version);
                wc.DownloadFile(DownloadUrl, tmp);
            }

            var info = new FileInfo(tmp);
            Log("Загружено " + (info.Length / 1024) + " КБ, распаковываю...");

            try { if (Directory.Exists(Dir)) Directory.Delete(Dir, true); } catch { }
            Directory.CreateDirectory(Dir);
            ZipFile.ExtractToDirectory(tmp, Dir);
            try { File.Delete(tmp); } catch { }

            if (IsInstalled) Log("BuildTools готовы: " + Dir + "   (" + Version + ")");
            else Log("Распаковал, но DupoBuild.exe не нашёлся — проверьте " + Dir);

            return Dir;
        }

        public void Uninstall()
        {
            if (!Directory.Exists(Dir)) { Log("BuildTools не установлены."); return; }
            Directory.Delete(Dir, true);
            Log("BuildTools удалены.");
        }

        void Need()
        {
            if (!IsInstalled)
                throw new DupoError("BuildTools не скачаны. Выполните Tools.Install() " +
                                    "или «Инструменты → Скачать BuildTools».", 0);
        }

        // ---------------- запуск ----------------

        /// <summary>Собирает папку в один exe через DupoBuild.exe.</summary>
        public string Build(string folder, string outExe)
        {
            Need();

            string source = DupoRuntime.Resolve(folder);
            string target = DupoRuntime.Resolve(outExe);

            string outp = Run(BuildExe, "\"" + source + "\" \"" + target + "\"");
            if (!string.IsNullOrEmpty(outp)) Log(outp.TrimEnd());

            return target;
        }

        /// <summary>Переводит скрипт в C# через DupoLang.exe.</summary>
        public string ToCSharp(string file, string outFile)
        {
            Need();

            string source = DupoRuntime.Resolve(file);
            string target = DupoRuntime.Resolve(outFile);

            string outp = Run(LangExe, "\"" + source + "\" \"" + target + "\"");
            if (!string.IsNullOrEmpty(outp)) Log(outp.TrimEnd());

            return target;
        }

        /// <summary>Показывает, что внутри .dapp или собранного exe.</summary>
        public string Info(string file)
        {
            Need();
            string outp = Run(BuildExe, "--info \"" + DupoRuntime.Resolve(file) + "\"");
            if (!string.IsNullOrEmpty(outp)) Log(outp.TrimEnd());
            return outp;
        }

        static string Run(string exe, string args)
        {
            var psi = new ProcessStartInfo(exe, args);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
            psi.WorkingDirectory = Path.GetDirectoryName(exe);

            using (var proc = Process.Start(psi))
            {
                string outp = proc.StandardOutput.ReadToEnd();
                string err = proc.StandardError.ReadToEnd();
                proc.WaitForExit(300000);
                return (outp ?? "") + (string.IsNullOrEmpty(err) ? "" : err);
            }
        }
    }
}
