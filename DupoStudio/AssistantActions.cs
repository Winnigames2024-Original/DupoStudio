using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DupoStudio
{
    /// <summary>Что помощник предлагает сделать с файлом.</summary>
    public class FileAction
    {
        public string Kind = "write";      // write, append, replace, delete
        public string Path = "";
        public string Text = "";
        public string From = "";           // для replace
        public string To = "";

        /// <summary>Полный путь в папке проекта.</summary>
        public string Full(string projectDir)
        {
            return System.IO.Path.Combine(projectDir ?? "",
                (Path ?? "").Replace('/', System.IO.Path.DirectorySeparatorChar).TrimStart('\\', '/'));
        }

        public string Title
        {
            get
            {
                switch (Kind)
                {
                    case "append": return "дописать в " + Path;
                    case "replace": return "заменить в " + Path;
                    case "delete": return "удалить " + Path;
                    default: return "записать " + Path;
                }
            }
        }
    }

    /// <summary>
    /// Помощник умеет не только отвечать, но и править файлы проекта.
    /// Для этого в ответе он пишет блоки действий, а студия их разбирает,
    /// показывает и применяет — но только когда человек согласится.
    /// </summary>
    public static class AssistantActions
    {
        /// <summary>Что рассказать внешней модели про работу с файлами.</summary>
        public const string Rules =
            "Ты можешь менять файлы проекта. Для этого в ответе пиши блоки:\r\n" +
            "\r\n" +
            "[файл: путь/имя.dupolang]\r\n" +
            "содержимое целиком\r\n" +
            "[/файл]\r\n" +
            "\r\n" +
            "[дописать: путь/имя.txt]\r\n" +
            "что добавить в конец\r\n" +
            "[/дописать]\r\n" +
            "\r\n" +
            "[заменить: путь/имя.cs]\r\n" +
            "<<<было\r\n" +
            "старый кусок точь-в-точь\r\n" +
            ">>>стало\r\n" +
            "новый кусок\r\n" +
            "[/заменить]\r\n" +
            "\r\n" +
            "[удалить: путь/имя.txt]\r\n" +
            "\r\n" +
            "Пути только внутри проекта. Перед блоками коротко скажи, что меняешь.\r\n" +
            "Человек увидит список правок и подтвердит их сам.";

        // ------------------------------------------------------------------
        // разбор ответа
        // ------------------------------------------------------------------

        /// <summary>Вытащить из ответа предложенные правки.</summary>
        public static List<FileAction> Parse(string answer)
        {
            var res = new List<FileAction>();
            string text = (answer ?? "").Replace("\r\n", "\n");

            foreach (Match m in Regex.Matches(text,
                @"\[(файл|дописать|заменить)\s*:\s*([^\]\n]+)\]\n(.*?)\[/\1\]",
                RegexOptions.Singleline))
            {
                string kind = m.Groups[1].Value;
                string path = m.Groups[2].Value.Trim();
                string body = m.Groups[3].Value;

                if (path.Length == 0) continue;

                // Внутри может лежать ограждённый код — снимаем рамку.
                body = Unfence(body);

                if (kind == "заменить")
                {
                    var pair = Regex.Match(body, @"<<<было\n(.*?)\n>>>стало\n(.*)", RegexOptions.Singleline);
                    if (!pair.Success) continue;

                    res.Add(new FileAction
                    {
                        Kind = "replace",
                        Path = path,
                        From = pair.Groups[1].Value.TrimEnd('\n'),
                        To = pair.Groups[2].Value.TrimEnd('\n')
                    });
                    continue;
                }

                res.Add(new FileAction
                {
                    Kind = kind == "дописать" ? "append" : "write",
                    Path = path,
                    Text = body.TrimEnd('\n')
                });
            }

            foreach (Match m in Regex.Matches(text, @"\[удалить\s*:\s*([^\]\n]+)\]"))
            {
                string path = m.Groups[1].Value.Trim();
                if (path.Length > 0) res.Add(new FileAction { Kind = "delete", Path = path });
            }

            return res;
        }

        static string Unfence(string body)
        {
            var m = Regex.Match(body.Trim(), @"^```[a-zA-Z#+]*\n(.*)\n```$", RegexOptions.Singleline);
            return m.Success ? m.Groups[1].Value : body;
        }

        /// <summary>Убрать блоки действий из текста — в чате они ни к чему.</summary>
        public static string Strip(string answer)
        {
            string text = (answer ?? "").Replace("\r\n", "\n");

            text = Regex.Replace(text, @"\[(файл|дописать|заменить)\s*:\s*[^\]\n]+\]\n.*?\[/\1\]",
                                 "", RegexOptions.Singleline);
            text = Regex.Replace(text, @"\[удалить\s*:\s*[^\]\n]+\]", "");
            text = Regex.Replace(text, @"\n{3,}", "\n\n");

            return text.Trim();
        }

        // ------------------------------------------------------------------
        // применение
        // ------------------------------------------------------------------

        /// <summary>Можно ли трогать этот путь.</summary>
        public static string Check(FileAction action, string projectDir)
        {
            if (string.IsNullOrEmpty(projectDir)) return "проект не открыт";

            string path = (action.Path ?? "").Trim();
            if (path.Length == 0) return "пустой путь";
            if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0) return "недопустимые символы в пути";

            string full, root;
            try
            {
                full = Path.GetFullPath(action.Full(projectDir));
                root = Path.GetFullPath(projectDir);
            }
            catch (Exception ex) { return ex.Message; }

            // Строго внутри проекта: «..» и абсолютные пути не пропускаем.
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return "путь ведёт за пределы проекта";

            if (action.Kind == "replace" && (action.From ?? "").Length == 0)
                return "нечего заменять";

            if ((action.Kind == "replace" || action.Kind == "append" || action.Kind == "delete") &&
                !File.Exists(full))
                return "файла нет: " + action.Path;

            return null;
        }

        /// <summary>Применить правку. Возвращает описание или бросает исключение.</summary>
        public static string Apply(FileAction action, string projectDir)
        {
            string problem = Check(action, projectDir);
            if (problem != null) throw new InvalidOperationException(problem);

            string full = action.Full(projectDir);

            switch (action.Kind)
            {
                case "delete":
                    Backup(full);
                    File.Delete(full);
                    return "удалён " + action.Path;

                case "append":
                    Backup(full);
                    File.AppendAllText(full, Environment.NewLine + Fix(action.Text), Utf8);
                    return "дописан " + action.Path;

                case "replace":
                {
                    string was = File.ReadAllText(full, Encoding.UTF8);
                    string from = Fix(action.From);

                    if (was.IndexOf(from, StringComparison.Ordinal) < 0)
                        throw new InvalidOperationException(
                            "в " + action.Path + " нет куска, который просили заменить");

                    Backup(full);
                    File.WriteAllText(full, ReplaceFirst(was, from, Fix(action.To)), Utf8);
                    return "изменён " + action.Path;
                }

                default:
                {
                    string dir = Path.GetDirectoryName(full);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                    bool existed = File.Exists(full);
                    if (existed) Backup(full);

                    File.WriteAllText(full, Fix(action.Text), Utf8);
                    return (existed ? "переписан " : "создан ") + action.Path;
                }
            }
        }

        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        static string Fix(string text)
        {
            return (text ?? "").Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        }

        static string ReplaceFirst(string text, string from, string to)
        {
            int at = text.IndexOf(from, StringComparison.Ordinal);
            if (at < 0) return text;
            return text.Substring(0, at) + to + text.Substring(at + from.Length);
        }

        /// <summary>Старое содержимое кладём рядом — чтобы было куда вернуться.</summary>
        static void Backup(string full)
        {
            try
            {
                if (!File.Exists(full)) return;

                string dir = Path.Combine(Path.GetDirectoryName(full), ".dupo", "backup");
                Directory.CreateDirectory(dir);

                string copy = Path.Combine(dir,
                    Path.GetFileName(full) + "." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak");

                File.Copy(full, copy, true);
            }
            catch { }
        }

        // ------------------------------------------------------------------
        // чтение проекта для разговора
        // ------------------------------------------------------------------

        /// <summary>Файлы проекта, которые можно показать помощнику.</summary>
        public static List<string> Files(string projectDir)
        {
            var res = new List<string>();
            if (string.IsNullOrEmpty(projectDir) || !Directory.Exists(projectDir)) return res;

            try
            {
                foreach (string f in Directory.GetFiles(projectDir, "*.*", SearchOption.AllDirectories))
                {
                    string rel = f.Substring(projectDir.Length).TrimStart('\\', '/').Replace('\\', '/');

                    if (rel.StartsWith("build/", StringComparison.OrdinalIgnoreCase)) continue;
                    if (rel.StartsWith("bin/", StringComparison.OrdinalIgnoreCase)) continue;
                    if (rel.StartsWith("obj/", StringComparison.OrdinalIgnoreCase)) continue;
                    if (rel.StartsWith(".dupo/", StringComparison.OrdinalIgnoreCase)) continue;
                    if (rel.StartsWith("runtime/", StringComparison.OrdinalIgnoreCase)) continue;

                    res.Add(rel);
                    if (res.Count >= 120) break;
                }
            }
            catch { }

            res.Sort(StringComparer.OrdinalIgnoreCase);
            return res;
        }

        /// <summary>Содержимое текстовых файлов проекта — то, с чем помощник работает.</summary>
        public static string Contents(string projectDir, int budget)
        {
            var sb = new StringBuilder();
            int left = Math.Max(2000, budget);

            foreach (string rel in Files(projectDir))
            {
                if (!Textual(rel)) continue;

                string full = Path.Combine(projectDir, rel.Replace('/', Path.DirectorySeparatorChar));

                string text;
                try
                {
                    var info = new FileInfo(full);
                    if (info.Length > 40000) continue;
                    text = File.ReadAllText(full, Encoding.UTF8);
                }
                catch { continue; }

                if (text.Length + 40 > left) continue;

                sb.AppendLine("--- " + rel + " ---");
                sb.AppendLine(text.TrimEnd());
                sb.AppendLine();

                left -= text.Length + 40;
                if (left <= 0) break;
            }

            return sb.ToString();
        }

        static bool Textual(string rel)
        {
            string ext = (Path.GetExtension(rel) ?? "").ToLowerInvariant();
            return ext == ".dupolang" || ext == ".dupoform" || ext == ".cs" || ext == ".java" ||
                   ext == ".cpp" || ext == ".h" || ext == ".html" || ext == ".css" ||
                   ext == ".js" || ext == ".json" || ext == ".txt" || ext == ".md" ||
                   ext == ".php" || ext == ".dupo" || ext == ".xml" || ext == ".bat";
        }
    }
}
