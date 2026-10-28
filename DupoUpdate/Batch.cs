using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace DupoUpdate
{
    /// <summary>Один файл, который надо поставить на место.</summary>
    public class Swap
    {
        /// <summary>Скачанный файл во временной папке.</summary>
        public string From = "";

        /// <summary>Куда он должен лечь.</summary>
        public string To = "";

        public Swap() { }
        public Swap(string from, string to) { From = from; To = to; }
    }

    /// <summary>
    /// Подмена файлов пакетным файлом. Сам update.exe переписать себя не может,
    /// да и приложение в это время ещё закрывается, поэтому список замен
    /// отдаётся cmd.exe: он ждёт, пока все выйдут, переименовывает старые файлы,
    /// ставит новые и удаляет остатки.
    /// </summary>
    public static class Batch
    {
        /// <summary>Сколько ждать, пока процессы закроются, прежде чем махнуть рукой.</summary>
        const int WaitTries = 60;   // примерно минута

        /// <summary>
        /// Собрать .cmd и запустить его. Возвращает путь к файлу — он удалит
        /// себя сам. Управление возвращается сразу: дальше нужно выйти,
        /// иначе пакетный файл так и будет нас ждать.
        /// </summary>
        public static string Apply(List<Swap> swaps, string staging, string[] closeFirst,
                                   string restartApp, string restartWatch)
        {
            if (swaps == null || swaps.Count == 0) return null;

            var sb = new StringBuilder();

            sb.AppendLine("@echo off");
            sb.AppendLine("chcp 866 >nul");
            sb.AppendLine("title Обновление Dupo Studio");
            sb.AppendLine();
            sb.AppendLine("rem Этот файл создан update.exe и удаляет себя в конце.");
            sb.AppendLine();

            // 1. Ждём, пока закроется всё, что держит файлы.
            int w = 0;
            foreach (string exe in closeFirst ?? new string[0])
            {
                if (string.IsNullOrEmpty(exe)) continue;
                WaitFor(sb, exe, ++w);
            }

            // 2. Себя ждём по номеру процесса: имя может совпасть с чужим.
            WaitSelf(sb);

            sb.AppendLine();
            sb.AppendLine("rem --- подмена файлов ---");

            int n = 0;
            foreach (Swap s in swaps)
            {
                if (s == null || s.From.Length == 0 || s.To.Length == 0) continue;
                n++;
                One(sb, s, n);
            }

            if (n == 0) return null;

            // 3. Папку со скачанным убираем целиком.
            if (!string.IsNullOrEmpty(staging))
            {
                sb.AppendLine();
                sb.AppendLine("rem --- прибираемся ---");
                sb.AppendLine("rd /s /q " + Q(staging) + " 2>nul");
            }

            // 4. Возвращаем на место то, что закрыли.
            sb.AppendLine();
            if (!string.IsNullOrEmpty(restartApp))
                sb.AppendLine("start \"\" " + Q(restartApp));

            if (!string.IsNullOrEmpty(restartWatch))
                sb.AppendLine("start \"\" " + Q(restartWatch) + " -autoupdate");

            sb.AppendLine();
            sb.AppendLine("del /f /q \"%~f0\" >nul 2>&1");

            string bat = Path.Combine(Path.GetTempPath(),
                                      "dupo-update-" + Guid.NewGuid().ToString("N") + ".cmd");

            // В cmd.exe по-прежнему кодовая страница 866 — иначе комментарии
            // и заголовок превратятся в кашу.
            File.WriteAllText(bat, sb.ToString(), Encoding.GetEncoding(866));

            // call с кавычками — единственная форма, которая не спотыкается
            // о пробелы в пути к временной папке.
            var psi = new ProcessStartInfo("cmd.exe", "/c call \"" + bat + "\"");
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            Process.Start(psi);

            return bat;
        }

        /// <summary>Замена одного файла: старый в .old, новый на место, .old удалить.</summary>
        static void One(StringBuilder sb, Swap s, int n)
        {
            string name = Path.GetFileName(s.To);
            string folder = Path.GetDirectoryName(s.To) ?? "";
            string old = s.To + ".old";

            sb.AppendLine();
            sb.AppendLine("rem (" + n + ") " + name);

            // Папки может не быть — например, runtime\tools при первом обновлении.
            // md на существующей просто ругнётся, и это ничему не мешает.
            if (folder.Length > 0)
                sb.AppendLine("md " + Q(folder) + " >nul 2>&1");

            sb.AppendLine("if not exist " + Q(s.From) + " goto skip" + n);

            // Старый файл сначала отодвигаем: занятый файл переименовать можно,
            // а перезаписать — нет.
            sb.AppendLine("if exist " + Q(old) + " del /f /q " + Q(old) + " >nul 2>&1");
            sb.AppendLine("if exist " + Q(s.To) + " ren " + Q(s.To) + " \"" + name + ".old\" >nul 2>&1");

            sb.AppendLine("move /y " + Q(s.From) + " " + Q(s.To) + " >nul 2>&1");

            // Не переехало — возвращаем как было, лучше старая версия, чем никакой.
            sb.AppendLine("if not exist " + Q(s.To) + " (");
            sb.AppendLine("  if exist " + Q(old) + " ren " + Q(old) + " \"" + name + "\" >nul 2>&1");
            sb.AppendLine(") else (");
            sb.AppendLine("  del /f /q " + Q(old) + " >nul 2>&1");
            sb.AppendLine(")");

            sb.AppendLine(":skip" + n);
        }

        /// <summary>
        /// Ждать, пока процесс с таким именем исчезнет. Метки нумеруются,
        /// а не берутся из имени: два разных файла могли бы дать одну метку.
        /// </summary>
        static void WaitFor(StringBuilder sb, string exe, int n)
        {
            sb.AppendLine();
            sb.AppendLine("rem ждём, пока закроется " + exe);
            sb.AppendLine("set tries=0");
            sb.AppendLine(":wait" + n);
            sb.AppendLine("tasklist /fi \"imagename eq " + exe + "\" 2>nul | find /i \"" + exe + "\" >nul || goto free" + n);
            sb.AppendLine("set /a tries+=1");
            sb.AppendLine("if %tries% geq " + WaitTries + " goto free" + n);
            sb.AppendLine("ping -n 2 127.0.0.1 >nul");
            sb.AppendLine("goto wait" + n);
            sb.AppendLine(":free" + n);
        }

        /// <summary>Ждать выхода самого update.exe — по номеру процесса.</summary>
        static void WaitSelf(StringBuilder sb)
        {
            int pid = Process.GetCurrentProcess().Id;

            sb.AppendLine("set tries=0");
            sb.AppendLine(":waitself");
            sb.AppendLine("tasklist /fi \"pid eq " + pid + "\" 2>nul | find \"" + pid + "\" >nul || goto doneself");
            sb.AppendLine("set /a tries+=1");
            sb.AppendLine("if %tries% geq " + WaitTries + " goto doneself");
            sb.AppendLine("ping -n 2 127.0.0.1 >nul");
            sb.AppendLine("goto waitself");
            sb.AppendLine(":doneself");
        }

        /// <summary>Путь в кавычках. Кавычек внутри путей не бывает, но хвостовой слеш мешает.</summary>
        static string Q(string path)
        {
            return "\"" + (path ?? "").Replace("\"", "") + "\"";
        }
    }
}
