using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DupoLang
{
    /// <summary>
    /// Dupo.Text — работа со строками.
    /// <code>
    /// using Dupo.Text
    /// print(Text.Join(", ", Text.Words("привет большой мир")))
    /// </code>
    /// </summary>
    public static class TextTools
    {
        public static Action<string> Log = delegate (string s) { Console.WriteLine(s); };

        // ---------------- простое ----------------

        public static string Trim(string s) { return (s ?? "").Trim(); }
        public static string TrimStart(string s) { return (s ?? "").TrimStart(); }
        public static string TrimEnd(string s) { return (s ?? "").TrimEnd(); }
        public static string Upper(string s) { return (s ?? "").ToUpperInvariant(); }
        public static string Lower(string s) { return (s ?? "").ToLowerInvariant(); }
        public static double Length(string s) { return (s ?? "").Length; }
        public static bool IsEmpty(string s) { return string.IsNullOrWhiteSpace(s); }

        public static string Replace(string s, string from, string to)
        {
            if (string.IsNullOrEmpty(from)) return s ?? "";
            return (s ?? "").Replace(from, to ?? "");
        }

        public static bool Contains(string s, string what)
        {
            return (s ?? "").IndexOf(what ?? "", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool StartsWith(string s, string what)
        {
            return (s ?? "").StartsWith(what ?? "", StringComparison.OrdinalIgnoreCase);
        }

        public static bool EndsWith(string s, string what)
        {
            return (s ?? "").EndsWith(what ?? "", StringComparison.OrdinalIgnoreCase);
        }

        public static double IndexOf(string s, string what)
        {
            return (s ?? "").IndexOf(what ?? "", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Часть строки с позиции и до конца.</summary>
        public static string Cut(string s, double from)
        {
            string t = s ?? "";
            int i = Clamp((int)from, 0, t.Length);
            return t.Substring(i);
        }

        /// <summary>Часть строки с позиции заданной длины.</summary>
        public static string Cut(string s, double from, double length)
        {
            string t = s ?? "";
            int i = Clamp((int)from, 0, t.Length);
            int n = Clamp((int)length, 0, t.Length - i);
            return t.Substring(i, n);
        }

        /// <summary>Первые n символов.</summary>
        public static string Left(string s, double n) { return Cut(s, 0, n); }

        /// <summary>Последние n символов.</summary>
        public static string Right(string s, double n)
        {
            string t = s ?? "";
            int k = Clamp((int)n, 0, t.Length);
            return t.Substring(t.Length - k);
        }

        // ---------------- разбор и сборка ----------------

        /// <summary>Разбить по разделителю.</summary>
        public static List<object> Split(string s, string sep)
        {
            if (string.IsNullOrEmpty(sep))
                return (s ?? "").Select(c => (object)c.ToString()).ToList();

            return (s ?? "").Split(new[] { sep }, StringSplitOptions.None)
                            .Select(x => (object)x).ToList();
        }

        /// <summary>Разбить на строки.</summary>
        public static List<object> Lines(string s)
        {
            return (s ?? "").Replace("\r\n", "\n").Split('\n').Select(x => (object)x).ToList();
        }

        /// <summary>Разбить на слова, пропуская пустое.</summary>
        public static List<object> Words(string s)
        {
            return (s ?? "").Split(new[] { ' ', '\t', '\r', '\n', ',', ';', '.', '!', '?' },
                                   StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => (object)x).ToList();
        }

        /// <summary>Склеить список через разделитель.</summary>
        public static string Join(string sep, List<object> items)
        {
            if (items == null) return "";
            return string.Join(sep ?? "", items.Select(Val.Str).ToArray());
        }

        /// <summary>Повторить строку n раз.</summary>
        public static string Repeat(string s, double n)
        {
            int k = Math.Max(0, (int)n);
            var sb = new StringBuilder();
            for (int i = 0; i < k; i++) sb.Append(s ?? "");
            return sb.ToString();
        }

        /// <summary>Перевернуть строку.</summary>
        public static string Reverse(string s)
        {
            var a = (s ?? "").ToCharArray();
            Array.Reverse(a);
            return new string(a);
        }

        /// <summary>Добить пробелами слева до нужной длины.</summary>
        public static string PadLeft(string s, double width) { return (s ?? "").PadLeft(Math.Max(0, (int)width)); }

        /// <summary>Добить пробелами справа.</summary>
        public static string PadRight(string s, double width) { return (s ?? "").PadRight(Math.Max(0, (int)width)); }

        /// <summary>Первая буква заглавная.</summary>
        public static string Capital(string s)
        {
            string t = (s ?? "").Trim();
            if (t.Length == 0) return "";
            return char.ToUpperInvariant(t[0]) + t.Substring(1);
        }

        /// <summary>Сколько раз встречается подстрока.</summary>
        public static double CountOf(string s, string what)
        {
            if (string.IsNullOrEmpty(what)) return 0;
            string t = s ?? "";
            int n = 0, i = 0;
            while ((i = t.IndexOf(what, i, StringComparison.OrdinalIgnoreCase)) >= 0) { n++; i += what.Length; }
            return n;
        }

        // ---------------- шаблоны ----------------

        /// <summary>Подставить значения вместо {0}, {1} и так далее.</summary>
        public static string Format(string pattern, List<object> values)
        {
            string t = pattern ?? "";
            if (values == null) return t;
            for (int i = 0; i < values.Count; i++)
                t = t.Replace("{" + i + "}", Val.Str(values[i]));
            return t;
        }

        /// <summary>Подставить значения по именам из словаря: {имя}.</summary>
        public static string Fill(string pattern, DupoMap values)
        {
            string t = pattern ?? "";
            if (values == null) return t;
            foreach (var pair in values.Pairs)
                t = t.Replace("{" + pair.Key + "}", Val.Str(pair.Value));
            return t;
        }

        /// <summary>Проверить строку регулярным выражением.</summary>
        public static bool Match(string s, string pattern)
        {
            try { return Regex.IsMatch(s ?? "", pattern ?? "", RegexOptions.IgnoreCase); }
            catch (Exception ex) { Log("Выражение не годится: " + ex.Message); return false; }
        }

        /// <summary>Все совпадения регулярного выражения.</summary>
        public static List<object> Find(string s, string pattern)
        {
            var res = new List<object>();
            try
            {
                foreach (Match m in Regex.Matches(s ?? "", pattern ?? "", RegexOptions.IgnoreCase))
                    res.Add(m.Value);
            }
            catch (Exception ex) { Log("Выражение не годится: " + ex.Message); }
            return res;
        }

        /// <summary>Замена по регулярному выражению.</summary>
        public static string ReplaceAll(string s, string pattern, string to)
        {
            try { return Regex.Replace(s ?? "", pattern ?? "", to ?? "", RegexOptions.IgnoreCase); }
            catch (Exception ex) { Log("Выражение не годится: " + ex.Message); return s ?? ""; }
        }

        // ---------------- числа и кодировки ----------------

        /// <summary>Строка в число; если не вышло — ноль.</summary>
        public static double ToNumber(string s)
        {
            double d;
            if (double.TryParse((s ?? "").Replace(',', '.'), NumberStyles.Any,
                                CultureInfo.InvariantCulture, out d)) return d;
            return 0;
        }

        /// <summary>Число строкой с заданным числом знаков после запятой.</summary>
        public static string Round(double value, double digits)
        {
            int d = Clamp((int)digits, 0, 15);
            return value.ToString("F" + d, CultureInfo.InvariantCulture);
        }

        /// <summary>Закодировать в Base64.</summary>
        public static string ToBase64(string s)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(s ?? ""));
        }

        /// <summary>Раскодировать из Base64.</summary>
        public static string FromBase64(string s)
        {
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(s ?? "")); }
            catch { return ""; }
        }

        /// <summary>Экранировать строку для адреса.</summary>
        public static string UrlEncode(string s) { return Uri.EscapeDataString(s ?? ""); }

        /// <summary>Вернуть строку из адреса.</summary>
        public static string UrlDecode(string s)
        {
            try { return Uri.UnescapeDataString(s ?? ""); }
            catch { return s ?? ""; }
        }

        static int Clamp(int v, int min, int max)
        {
            return v < min ? min : (v > max ? max : v);
        }
    }
}
