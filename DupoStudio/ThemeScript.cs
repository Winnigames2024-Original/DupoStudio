using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DupoLang;

namespace DupoStudio
{
    /// <summary>
    /// Кусочек оформления, которому можно задать цвет и картинку.
    /// Именно с ним работает тема, написанная на C#:
    /// <code>
    /// background.color = "#101216";
    /// background.image = "C:\\Фото\\стена.jpg";
    /// accent.color = "#3B82F6";
    /// </code>
    /// </summary>
    public class Paint
    {
        /// <summary>Цвет: «#RRGGBB», «255,120,0» или обычное имя вроде "SteelBlue".</summary>
        public string color = "";

        /// <summary>Картинка: путь к файлу. Пусто — картинки нет.</summary>
        public string image = "";

        /// <summary>Как растянуть картинку: cover, tile, center, stretch.</summary>
        public string fit = "cover";

        /// <summary>Прозрачность картинки от 0 до 100.</summary>
        public int opacity = 100;

        /// <summary>Разобранный цвет или пусто.</summary>
        public Color? Color2
        {
            get { return ThemeScript.ToColor(color); }
        }

        public override string ToString()
        {
            return (color.Length > 0 ? color : "—") + (image.Length > 0 ? " + картинка" : "");
        }
    }

    /// <summary>
    /// Тема, написанная на C#. Студия даёт файл theme.cs, человек пишет
    /// в нём простые присваивания, а студия собирает его настоящим
    /// компилятором и применяет, что получилось.
    /// </summary>
    public static class ThemeScript
    {
        public const string FileName = "theme.cs";

        /// <summary>Имена кусочков, которые видны из темы.</summary>
        public static readonly string[] Parts =
        {
            "background", "panel", "chrome", "border", "text", "dim",
            "accent", "accent2", "gutter", "lineNumber", "status",
            "keyword", "typeName", "str", "number", "comment",
            "ok", "warn", "error", "info", "editor"
        };

        /// <summary>Где лежит файл темы.</summary>
        public static string FilePath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DupoStudio");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, FileName);
            }
        }

        public static bool Exists { get { return File.Exists(FilePath); } }

        /// <summary>Картинка фона, если тема её задала.</summary>
        public static Image Background { get; private set; }

        /// <summary>Как растягивать картинку.</summary>
        public static string BackgroundFit { get; private set; }

        // ------------------------------------------------------------------
        // образец
        // ------------------------------------------------------------------

        /// <summary>Завести файл темы с примером внутри.</summary>
        public static string Create()
        {
            string path = FilePath;
            if (File.Exists(path)) return path;

            var sb = new StringBuilder();
            sb.AppendLine("// Тема Dupo Studio. Обычный C#: пишите присваивания,");
            sb.AppendLine("// студия соберёт этот файл настоящим компилятором и применит.");
            sb.AppendLine("//");
            sb.AppendLine("// Цвет: \"#RRGGBB\", \"255,120,0\" или имя вроде \"SteelBlue\".");
            sb.AppendLine("// Картинка: путь к файлу; fit — cover, stretch, tile или center.");
            sb.AppendLine();
            sb.AppendLine("background.color = \"#14161C\";");
            sb.AppendLine("// background.image = @\"C:\\Фото\\стена.jpg\";");
            sb.AppendLine("// background.fit = \"cover\";");
            sb.AppendLine("// background.opacity = 60;");
            sb.AppendLine();
            sb.AppendLine("panel.color  = \"#1B1E26\";");
            sb.AppendLine("chrome.color = \"#222631\";");
            sb.AppendLine("border.color = \"#333A49\";");
            sb.AppendLine();
            sb.AppendLine("text.color = \"#E6EAF2\";");
            sb.AppendLine("dim.color  = \"#8D97AA\";");
            sb.AppendLine();
            sb.AppendLine("accent.color  = \"#3B82F6\";");
            sb.AppendLine("accent2.color = \"#22C7A9\";");
            sb.AppendLine("status.color  = \"#1E3A6E\";");
            sb.AppendLine();
            sb.AppendLine("// Подсветка кода");
            sb.AppendLine("keyword.color  = \"#7AA2F7\";");
            sb.AppendLine("typeName.color = \"#56D4C0\";");
            sb.AppendLine("str.color      = \"#E0A072\";");
            sb.AppendLine("number.color   = \"#BBD98B\";");
            sb.AppendLine("comment.color  = \"#6A7F6A\";");
            sb.AppendLine();
            sb.AppendLine("// Поля редактора и подсказок");
            sb.AppendLine("// gutter.color     = \"#171A21\";   // полоса с номерами строк");
            sb.AppendLine("// lineNumber.color = \"#5A6478\";   // сами номера");
            sb.AppendLine("// editor.color     = \"#14161C\";   // поле кода");
            sb.AppendLine();
            sb.AppendLine("// Сообщения в консоли");
            sb.AppendLine("// ok.color    = \"#78C878\";");
            sb.AppendLine("// warn.color  = \"#D8B860\";");
            sb.AppendLine("// error.color = \"#E06C75\";");
            sb.AppendLine("// info.color  = \"#6FA8DC\";");
            sb.AppendLine();
            sb.AppendLine("// Можно и считать что-нибудь — это полноценный C#.");
            sb.AppendLine("// for (int i = 0; i < 1; i++) { ok.color = \"#78C878\"; }");

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return path;
        }

        // ------------------------------------------------------------------
        // сборка и применение
        // ------------------------------------------------------------------

        /// <summary>
        /// Собрать файл темы и применить его. Возвращает рассказ о том,
        /// что вышло — его показывают человеку.
        /// </summary>
        public static string Apply(Action<string> say)
        {
            Action<string> tell = say ?? delegate { };

            string path = FilePath;
            if (!File.Exists(path)) return "Файла темы нет — создайте его в «Вид → Тема → Своя тема».";

            string body = File.ReadAllText(path, Encoding.UTF8);

            // Файл не менялся с прошлого раза — берём собранную тему из кэша,
            // иначе студия звала бы компилятор на каждом запуске.
            string cached = Cached(body);
            if (cached != null)
            {
                var ready = Load(cached);
                if (ready != null) return Use(ready, tell);
            }

            string dir = Path.Combine(Path.GetTempPath(), "dupo-theme-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);

            try
            {
                string source = Path.Combine(dir, "Theme.cs");
                File.WriteAllText(source, Wrap(body), new UTF8Encoding(false));

                string dll = Path.Combine(dir, "DupoUserTheme.dll");

                CsTools.Log = tell;
                CsTools.Instance.Compile(source, dll, "library", "System.Drawing.dll");

                var parts = Load(dll);
                if (parts == null) return "Тема собралась, но применить её не вышло.";

                Keep(body, dll);
                return Use(parts, tell);
            }
            finally
            {
                try { Directory.Delete(dir, true); }
                catch { }
            }
        }

        // ------------------------------------------------------------------
        // кэш собранной темы
        // ------------------------------------------------------------------

        /// <summary>Папка кэша рядом с самим файлом темы.</summary>
        static string CacheDir
        {
            get
            {
                string dir = Path.Combine(Path.GetDirectoryName(FilePath) ?? "", "theme-cache");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        /// <summary>Готовая сборка для такого текста темы, если она уже есть.</summary>
        static string Cached(string body)
        {
            try
            {
                string dll = Path.Combine(CacheDir, Hash(body) + ".dll");
                return File.Exists(dll) ? dll : null;
            }
            catch { return null; }
        }

        /// <summary>Сохранить сборку и убрать сборки от прежних версий файла.</summary>
        static void Keep(string body, string dll)
        {
            try
            {
                string dir = CacheDir;
                string keep = Hash(body) + ".dll";

                File.Copy(dll, Path.Combine(dir, keep), true);

                foreach (string old in Directory.GetFiles(dir, "*.dll"))
                    if (!string.Equals(Path.GetFileName(old), keep, StringComparison.OrdinalIgnoreCase))
                        try { File.Delete(old); } catch { }
            }
            catch { }
        }

        static string Hash(string text)
        {
            using (var sha = System.Security.Cryptography.SHA1.Create())
            {
                byte[] sum = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? ""));
                var sb = new StringBuilder(sum.Length * 2);
                foreach (byte b in sum) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>Оборачиваем тело файла в настоящий класс.</summary>
        static string Wrap(string body)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("public class DupoPaint");
            sb.AppendLine("{");
            sb.AppendLine("    public string color = \"\";");
            sb.AppendLine("    public string image = \"\";");
            sb.AppendLine("    public string fit = \"cover\";");
            sb.AppendLine("    public int opacity = 100;");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("public static class DupoUserTheme");
            sb.AppendLine("{");

            foreach (string part in Parts)
                sb.AppendLine("    public static DupoPaint " + part + " = new DupoPaint();");

            sb.AppendLine();
            sb.AppendLine("    public static void Build()");
            sb.AppendLine("    {");
            sb.AppendLine(body);
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        /// <summary>Выполнить собранную тему и забрать её значения.</summary>
        static Dictionary<string, Paint> Load(string dll)
        {
            try
            {
                // Читаем байтами: иначе файл останется занятым и папку не убрать.
                Assembly built = Assembly.Load(File.ReadAllBytes(dll));

                Type type = built.GetType("DupoUserTheme");
                if (type == null) return null;

                MethodInfo build = type.GetMethod("Build", BindingFlags.Public | BindingFlags.Static);
                if (build == null) return null;

                build.Invoke(null, null);

                var res = new Dictionary<string, Paint>(StringComparer.OrdinalIgnoreCase);

                foreach (string part in Parts)
                {
                    FieldInfo field = type.GetField(part, BindingFlags.Public | BindingFlags.Static);
                    if (field == null) continue;

                    object value = field.GetValue(null);
                    if (value == null) continue;

                    Type pt = value.GetType();
                    var paint = new Paint();
                    paint.color = Str(pt, value, "color");
                    paint.image = Str(pt, value, "image");
                    paint.fit = Str(pt, value, "fit");

                    FieldInfo opacity = pt.GetField("opacity");
                    if (opacity != null) paint.opacity = Convert.ToInt32(opacity.GetValue(value));

                    res[part] = paint;
                }

                return res;
            }
            catch (TargetInvocationException tie)
            {
                throw new InvalidOperationException("тема споткнулась: " +
                    (tie.InnerException == null ? tie.Message : tie.InnerException.Message));
            }
        }

        static string Str(Type type, object obj, string name)
        {
            FieldInfo f = type.GetField(name);
            if (f == null) return "";

            object v = f.GetValue(obj);
            return v == null ? "" : v.ToString();
        }

        /// <summary>Применить значения к палитре студии.</summary>
        static string Use(Dictionary<string, Paint> parts, Action<string> tell)
        {
            var map = new Dictionary<string, Action<Color>>(StringComparer.OrdinalIgnoreCase)
            {
                { "background", c => Theme.SetBack(c) },
                { "panel", c => Theme.SetPanel(c) },
                { "chrome", c => Theme.SetChrome(c) },
                { "border", c => Theme.SetBorder(c) },
                { "text", c => Theme.SetText(c) },
                { "dim", c => Theme.SetDim(c) },
                { "accent", c => Theme.SetAccent(c) },
                { "accent2", c => Theme.SetAccent2(c) },
                { "gutter", c => Theme.SetGutter(c) },
                { "lineNumber", c => Theme.SetLineNumber(c) },
                { "status", c => Theme.SetStatus(c) },
                { "keyword", c => Theme.SetKeyword(c) },
                { "typeName", c => Theme.SetTypeName(c) },
                { "str", c => Theme.SetStr(c) },
                { "number", c => Theme.SetNumber(c) },
                { "comment", c => Theme.SetComment(c) },
                { "ok", c => Theme.SetOk(c) },
                { "warn", c => Theme.SetWarn(c) },
                { "error", c => Theme.SetErr(c) },
                { "info", c => Theme.SetInfo(c) },
                { "editor", c => Theme.SetBack(c) },
            };

            int painted = 0;

            foreach (var pair in parts)
            {
                Action<Color> set;
                if (!map.TryGetValue(pair.Key, out set)) continue;

                Color? c = ToColor(pair.Value.color);
                if (c == null) continue;

                set(c.Value);
                painted++;
            }

            // Картинка фона
            Background = null;
            BackgroundFit = "cover";

            Paint back;
            if (parts.TryGetValue("background", out back) && back.image.Trim().Length > 0)
            {
                string file = back.image.Trim();
                try
                {
                    if (File.Exists(file))
                    {
                        using (var raw = Image.FromFile(file))
                            Background = Fade(new Bitmap(raw), back.opacity);

                        BackgroundFit = (back.fit ?? "cover").Trim().ToLowerInvariant();
                        tell("Фон: " + Path.GetFileName(file) + " (" + BackgroundFit + ")");
                    }
                    else tell("Картинка фона не найдена: " + file);
                }
                catch (Exception ex) { tell("Картинка фона не открылась: " + ex.Message); }
            }

            Theme.MarkCustom();

            return painted == 0
                ? "Тема собралась, но ни одного цвета в ней не задано."
                : "Тема применена: цветов " + painted + (Background != null ? ", с картинкой" : "") + ".";
        }

        /// <summary>Притушить картинку, если просили прозрачность.</summary>
        static Image Fade(Bitmap source, int opacity)
        {
            int percent = Math.Max(0, Math.Min(100, opacity));
            if (percent >= 100) return source;

            var faded = new Bitmap(source.Width, source.Height);

            using (var g = Graphics.FromImage(faded))
            using (var attrs = new System.Drawing.Imaging.ImageAttributes())
            {
                var matrix = new System.Drawing.Imaging.ColorMatrix();
                matrix.Matrix33 = percent / 100f;
                attrs.SetColorMatrix(matrix);

                g.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height),
                            0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attrs);
            }

            source.Dispose();
            return faded;
        }

        // ------------------------------------------------------------------
        // разбор цвета
        // ------------------------------------------------------------------

        /// <summary>«#1E1E1E», «30,30,30» или имя цвета. Пусто — ничего.</summary>
        public static Color? ToColor(string text)
        {
            string s = (text ?? "").Trim();
            if (s.Length == 0) return null;

            if (s[0] == '#')
            {
                string hex = s.Substring(1);
                if (hex.Length == 3)
                    hex = "" + hex[0] + hex[0] + hex[1] + hex[1] + hex[2] + hex[2];

                if (hex.Length != 6 && hex.Length != 8) return null;

                try
                {
                    int at = hex.Length == 8 ? 2 : 0;
                    int r = Convert.ToInt32(hex.Substring(at, 2), 16);
                    int g = Convert.ToInt32(hex.Substring(at + 2, 2), 16);
                    int b = Convert.ToInt32(hex.Substring(at + 4, 2), 16);
                    int a = hex.Length == 8 ? Convert.ToInt32(hex.Substring(0, 2), 16) : 255;
                    return Color.FromArgb(a, r, g, b);
                }
                catch { return null; }
            }

            if (s.IndexOf(',') > 0)
            {
                var parts = s.Split(',').Select(x => x.Trim()).ToArray();
                if (parts.Length < 3) return null;

                try
                {
                    return Color.FromArgb(
                        Clamp(int.Parse(parts[0])),
                        Clamp(int.Parse(parts[1])),
                        Clamp(int.Parse(parts[2])));
                }
                catch { return null; }
            }

            try
            {
                Color named = Color.FromName(s);
                return named.A == 0 && named.R == 0 && named.G == 0 && named.B == 0 ? (Color?)null : named;
            }
            catch { return null; }
        }

        static int Clamp(int v) { return v < 0 ? 0 : (v > 255 ? 255 : v); }
    }
}
