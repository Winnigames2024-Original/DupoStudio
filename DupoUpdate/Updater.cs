using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DupoLang;

namespace DupoUpdate
{
    /// <summary>Что нашлось на GitHub.</summary>
    public class Found
    {
        public string Tag = "";
        public string Version = "";
        public string Notes = "";

        /// <summary>Отметка сборки из version.json выпуска.</summary>
        public Stamp Build = new Stamp();
        public readonly Dictionary<string, string> Assets =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public bool Has(string name) { return Assets.ContainsKey(name); }
    }

    /// <summary>
    /// Обновление приложения с GitHub. Порядок такой: сначала само
    /// приложение, затем новый update.exe откладывается рядом, потом
    /// инструменты, и уже на выходе обновлятор меняет себя — работающий
    /// файл перезаписать нельзя.
    /// </summary>
    public class Updater
    {
        public const string Repo = "Winnigames2024-Original/DupoStudio";

        public Action<string> Log = delegate { };
        public Action<int> Progress = delegate { };

        readonly UninstallData data;
        readonly string dir;

        /// <summary>Файл, который заменит update.exe после выхода.</summary>
        public string Staged { get; private set; }

        /// <summary>Папка, куда Plan сложил скачанное.</summary>
        public string Staging { get; private set; }

        public Updater(UninstallData info, string folder)
        {
            data = info;
            dir = folder;
        }

        // ------------------------------------------------------------------
        // что доступно
        // ------------------------------------------------------------------

        /// <summary>Последний выпуск с GitHub.</summary>
        public static Found Latest()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            string json;
            using (var wc = new WebClient())
            {
                wc.Headers.Add("User-Agent", "DupoStudio-update");
                wc.Encoding = Encoding.UTF8;
                json = wc.DownloadString("https://api.github.com/repos/" + Repo + "/releases/latest");
            }

            var found = new Found();

            var tag = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
            if (tag.Success) found.Tag = tag.Groups[1].Value;
            found.Version = Regex.Replace(found.Tag, "^v", "", RegexOptions.IgnoreCase);

            var body = Regex.Match(json, "\"body\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            if (body.Success) found.Notes = Unescape(body.Groups[1].Value);

            foreach (Match m in Regex.Matches(json,
                "\"name\"\\s*:\\s*\"([^\"]+)\"[^{}]*?\"browser_download_url\"\\s*:\\s*\"([^\"]+)\""))
            {
                found.Assets[m.Groups[1].Value] = m.Groups[2].Value;
            }

            // Запасной разбор: если порядок полей другой, берём по ссылке.
            if (found.Assets.Count == 0)
            {
                foreach (Match m in Regex.Matches(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+)\""))
                {
                    string url = m.Groups[1].Value;
                    found.Assets[url.Substring(url.LastIndexOf('/') + 1)] = url;
                }
            }

            found.Build = Mark(found);
            return found;
        }

        /// <summary>
        /// Отметка сборки выпуска. Файл version.json лежит среди файлов
        /// выпуска; если его нет — значит, выпуск собран до того, как
        /// появился счётчик, и номером сборки считаем первый.
        /// </summary>
        static Stamp Mark(Found found)
        {
            string url;
            if (found.Assets.TryGetValue(Stamp.FileName, out url))
            {
                try
                {
                    using (var wc = new WebClient())
                    {
                        wc.Headers.Add("User-Agent", "DupoStudio-update");
                        wc.Encoding = Encoding.UTF8;

                        var mark = Stamp.Parse(wc.DownloadString(url));
                        if (mark.Version.Length == 0) mark.Version = found.Version;
                        if (mark.Updated < 1) mark.Updated = 1;
                        return mark;
                    }
                }
                catch { }
            }

            return new Stamp(found.Version, 1);
        }

        static string Unescape(string s)
        {
            return (s ?? "").Replace("\\r\\n", "\n").Replace("\\n", "\n")
                            .Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        /// <summary>Сравнение версий: 1 — слева новее, -1 — справа, 0 — одинаково.</summary>
        public static int Compare(string a, string b)
        {
            int[] x = Parts(a), y = Parts(b);
            for (int i = 0; i < Math.Max(x.Length, y.Length); i++)
            {
                int l = i < x.Length ? x[i] : 0;
                int r = i < y.Length ? y[i] : 0;
                if (l != r) return l > r ? 1 : -1;
            }
            return 0;
        }

        static int[] Parts(string v)
        {
            return Regex.Matches(v ?? "", @"\d+").Cast<Match>()
                        .Select(m => int.Parse(m.Value)).ToArray();
        }

        /// <summary>Версия, которая стоит сейчас.</summary>
        public string Installed
        {
            get { return data != null && data.AppVersion.Length > 0 ? data.AppVersion : "0.0.0"; }
        }

        // ------------------------------------------------------------------
        // обновление
        // ------------------------------------------------------------------

        /// <summary>Обновить всё по порядку. Возвращает true, если что-то поменялось.</summary>
        public bool Run(Found found)
        {
            if (found == null) throw new InvalidOperationException("нечего ставить");

            bool changed = false;

            // 1. Само приложение.
            changed |= App(found);
            Progress(45);

            // 2. Новый обновлятор откладываем рядом — подменим себя на выходе.
            changed |= Self(found);
            Progress(60);

            // 3. Инструменты сборки.
            changed |= Tools(found);
            Progress(85);

            // 4. Заодно свежий uninstall.exe, если он есть в выпуске.
            changed |= Companion(found, "uninstall.exe");
            Progress(95);

            if (changed) Record(found);
            Progress(100);

            return changed;
        }

        /// <summary>
        /// Скачать всё новое в отдельную папку, не трогая работающие файлы.
        /// Список замен потом отдаётся Batch — он и поставит их на место.
        /// </summary>
        public List<Swap> Plan(Found found)
        {
            var swaps = new List<Swap>();
            if (found == null) return swaps;

            string staging = Path.Combine(Path.GetTempPath(),
                                          "dupo-staged-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            Staging = staging;

            // 1. Само приложение.
            string app = string.IsNullOrEmpty(data.MainExe)
                ? "DupoStudio.exe"
                : Path.GetFileName(data.MainExe);

            Take(found, swaps, staging, app, Path.Combine(dir, app));
            Progress(35);

            // 2. Сам обновлятор — его меняет тот же пакетный файл, после нашего выхода.
            Take(found, swaps, staging, "update.exe", Path.Combine(dir, "update.exe"));
            Progress(50);

            // 3. Удалятор, если он рядом лежит.
            if (File.Exists(Path.Combine(dir, "uninstall.exe")))
                Take(found, swaps, staging, "uninstall.exe", Path.Combine(dir, "uninstall.exe"));

            // Отметка сборки едет вместе со всеми: по ней в следующий раз
            // станет видно, что именно установлено.
            Take(found, swaps, staging, Stamp.FileName, Path.Combine(dir, Stamp.FileName));
            Progress(60);

            // 4. Инструменты сборки — архив разбирается на отдельные файлы.
            TakeTools(found, swaps, staging);
            Progress(90);

            if (swaps.Count == 0)
            {
                try { Directory.Delete(staging, true); } catch { }
                Staging = null;
            }

            Progress(100);
            return swaps;
        }

        /// <summary>Скачать один файл выпуска в папку ожидания.</summary>
        void Take(Found found, List<Swap> swaps, string staging, string asset, string target)
        {
            string url;
            if (!found.Assets.TryGetValue(asset, out url))
            {
                Log("В выпуске нет " + asset + " — пропускаю.");
                return;
            }

            try
            {
                Log("Качаю " + asset + "...");
                string temp = Download(url);

                string to = Path.Combine(staging, asset);
                File.Copy(temp, to, true);
                Clean(temp);

                swaps.Add(new Swap(to, target));
            }
            catch (Exception ex) { Log(asset + " не скачался: " + ex.Message); }
        }

        /// <summary>Разобрать BuildTools.zip в папку ожидания, файл за файлом.</summary>
        void TakeTools(Found found, List<Swap> swaps, string staging)
        {
            string url;
            if (!found.Assets.TryGetValue("BuildTools.zip", out url))
            {
                Log("В выпуске нет BuildTools.zip — инструменты не трогаю.");
                return;
            }

            string temp = null;
            try
            {
                Log("Качаю BuildTools.zip...");
                temp = Download(url);

                string into = Path.Combine(staging, "tools");
                Directory.CreateDirectory(into);

                string tools = Path.Combine(dir, "runtime", "tools");

                using (var zip = ZipFile.OpenRead(temp))
                {
                    foreach (var entry in zip.Entries)
                    {
                        if (entry.Length == 0 && entry.Name.Length == 0) continue;

                        string rel = entry.FullName.Replace('/', Path.DirectorySeparatorChar);

                        string to = Path.Combine(into, rel);
                        Directory.CreateDirectory(Path.GetDirectoryName(to));
                        entry.ExtractToFile(to, true);

                        swaps.Add(new Swap(to, Path.Combine(tools, rel)));
                    }
                }
            }
            catch (Exception ex) { Log("Инструменты не скачались: " + ex.Message); }
            finally { Clean(temp); }
        }

        bool App(Found found)
        {
            string name = string.IsNullOrEmpty(data.MainExe) ? "DupoStudio.exe" : Path.GetFileName(data.MainExe);

            string url;
            if (!found.Assets.TryGetValue(name, out url))
            {
                Log("В выпуске нет " + name + " — приложение не трогаю.");
                return false;
            }

            string target = Path.Combine(dir, name);
            Log("Качаю " + name + "...");

            string temp = Download(url);
            try
            {
                Wait(target);
                Backup(target);
                File.Copy(temp, target, true);
                Log("Приложение обновлено: " + name);
                return true;
            }
            finally { Clean(temp); }
        }

        bool Self(Found found)
        {
            string url;
            if (!found.Assets.TryGetValue("update.exe", out url))
            {
                Log("В выпуске нет update.exe — обновлятор остаётся прежним.");
                return false;
            }

            Log("Качаю update.exe...");
            string temp = Download(url);

            string staged = Path.Combine(dir, "update.new");
            try
            {
                File.Copy(temp, staged, true);
                Staged = staged;
                Log("Новый update.exe отложен — встанет на место при выходе.");
                return true;
            }
            finally { Clean(temp); }
        }

        bool Tools(Found found)
        {
            string url;
            if (!found.Assets.TryGetValue("BuildTools.zip", out url))
            {
                Log("В выпуске нет BuildTools.zip — инструменты не трогаю.");
                return false;
            }

            Log("Качаю BuildTools.zip...");
            string temp = Download(url);

            string tools = Path.Combine(dir, "runtime", "tools");

            try
            {
                Directory.CreateDirectory(tools);

                using (var zip = ZipFile.OpenRead(temp))
                {
                    foreach (var entry in zip.Entries)
                    {
                        if (entry.Length == 0 && entry.Name.Length == 0) continue;

                        string to = Path.Combine(tools, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(to));

                        Wait(to);
                        entry.ExtractToFile(to, true);
                    }
                }

                Log("Инструменты обновлены: " + tools);
                return true;
            }
            catch (Exception ex)
            {
                Log("Инструменты не обновились: " + ex.Message);
                return false;
            }
            finally { Clean(temp); }
        }

        bool Companion(Found found, string name)
        {
            string url;
            if (!found.Assets.TryGetValue(name, out url)) return false;

            string target = Path.Combine(dir, name);
            if (!File.Exists(target)) return false;

            Log("Качаю " + name + "...");
            string temp = Download(url);

            try
            {
                Wait(target);
                File.Copy(temp, target, true);
                Log(name + " обновлён");
                return true;
            }
            catch (Exception ex)
            {
                Log(name + " не обновился: " + ex.Message);
                return false;
            }
            finally { Clean(temp); }
        }

        /// <summary>
        /// Записать, что теперь установлено: версию в uninstall.dll и отметку
        /// сборки в version.json рядом с программой.
        /// Имя Stamp занято типом, поэтому метод называется иначе.
        /// </summary>
        void Record(Found found)
        {
            try
            {
                if (data == null || found.Version.Length == 0) return;

                data.AppVersion = found.Version;
                data.Extra["updated"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                data.Extra["updatedversion"] = found.Build.Updated.ToString();
                data.Save(dir);

                found.Build.Write(dir);

                Log("Записано: " + found.Build);
            }
            catch (Exception ex) { Log("Версию записать не вышло: " + ex.Message); }
        }

        // ------------------------------------------------------------------
        // мелочи
        // ------------------------------------------------------------------

        string Download(string url)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            string temp = Path.Combine(Path.GetTempPath(), "dupo-up-" + Guid.NewGuid().ToString("N"));
            using (var wc = new WebClient())
            {
                wc.Headers.Add("User-Agent", "DupoStudio-update");
                wc.DownloadFile(url, temp);
            }
            return temp;
        }

        static void Clean(string path)
        {
            try { if (path != null && File.Exists(path)) File.Delete(path); }
            catch { }
        }

        /// <summary>Отодвинуть старый файл: вдруг он ещё занят.</summary>
        static void Backup(string path)
        {
            try
            {
                if (!File.Exists(path)) return;

                string old = path + ".old";
                if (File.Exists(old)) File.Delete(old);
                File.Move(path, old);
            }
            catch { }
        }

        /// <summary>Подождать, пока файл освободится — вдруг приложение ещё закрывается.</summary>
        static void Wait(string path)
        {
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    if (!File.Exists(path)) return;
                    using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                    return;
                }
                catch { System.Threading.Thread.Sleep(250); }
            }
        }

        /// <summary>
        /// Подменить себя новым файлом после выхода: сами себя переписать
        /// не можем, поэтому поручаем короткой команде.
        /// </summary>
        public void ApplySelf()
        {
            if (string.IsNullOrEmpty(Staged) || !File.Exists(Staged)) return;

            try
            {
                string self = Process.GetCurrentProcess().MainModule.FileName;

                string bat = Path.Combine(Path.GetTempPath(), "dupo-self-" + Guid.NewGuid().ToString("N") + ".cmd");
                var sb = new StringBuilder();
                sb.AppendLine("@echo off");
                sb.AppendLine("ping -n 3 127.0.0.1 >nul");
                sb.AppendLine("move /y \"" + Staged + "\" \"" + self + "\" >nul");
                sb.AppendLine("del \"%~f0\"");

                File.WriteAllText(bat, sb.ToString(), Encoding.GetEncoding(866));

                var psi = new ProcessStartInfo("cmd.exe", "/c call \"" + bat + "\"");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                Process.Start(psi);
            }
            catch { }
        }

        /// <summary>Закрыть приложение, если оно запущено, — иначе файл занят.</summary>
        public void StopApp()
        {
            string name = string.IsNullOrEmpty(data.MainExe)
                ? "DupoStudio"
                : Path.GetFileNameWithoutExtension(data.MainExe);

            try
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    using (p)
                    {
                        Log("Закрываю " + name + "...");
                        try { p.CloseMainWindow(); } catch { }
                        if (!p.WaitForExit(4000))
                        {
                            try { p.Kill(); } catch { }
                            p.WaitForExit(2000);
                        }
                    }
                }
            }
            catch { }
        }
    }
}
