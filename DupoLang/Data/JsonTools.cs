using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace DupoLang
{
    /// <summary>
    /// Dupo.Json — чтение и запись JSON без сторонних библиотек.
    /// Объект превращается в <see cref="DupoMap"/>, массив — в список.
    /// <code>
    /// using Dupo.Json
    /// Map answer = Json.Parse(Web.Get("https://api.example.com/x"))
    /// print(answer["name"])
    /// </code>
    /// </summary>
    public static class JsonTools
    {
        public static Action<string> Log = delegate (string s) { Console.WriteLine(s); };

        // ---------------- чтение ----------------

        /// <summary>Разобрать строку JSON.</summary>
        public static object Parse(string text)
        {
            int pos = 0;
            object v = ReadValue(text ?? "", ref pos);
            SkipSpace(text ?? "", ref pos);
            return v;
        }

        /// <summary>Прочитать файл JSON.</summary>
        public static object Read(string path)
        {
            return Parse(File.ReadAllText(DupoRuntime.Resolve(path), Encoding.UTF8));
        }

        static void SkipSpace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static object ReadValue(string s, ref int i)
        {
            SkipSpace(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON кончился раньше времени");

            char c = s[i];
            if (c == '{') return ReadObject(s, ref i);
            if (c == '[') return ReadArray(s, ref i);
            if (c == '"') return ReadString(s, ref i);

            if (Match(s, ref i, "true")) return true;
            if (Match(s, ref i, "false")) return false;
            if (Match(s, ref i, "null")) return null;

            return ReadNumber(s, ref i);
        }

        static bool Match(string s, ref int i, string word)
        {
            if (i + word.Length > s.Length) return false;
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) return false;
            i += word.Length;
            return true;
        }

        static DupoMap ReadObject(string s, ref int i)
        {
            var map = new DupoMap();
            i++;   // {
            SkipSpace(s, ref i);

            if (i < s.Length && s[i] == '}') { i++; return map; }

            while (true)
            {
                SkipSpace(s, ref i);
                if (i >= s.Length || s[i] != '"') throw new FormatException("ожидалось имя поля в позиции " + i);

                string key = ReadString(s, ref i);
                SkipSpace(s, ref i);

                if (i >= s.Length || s[i] != ':') throw new FormatException("ожидалось ':' в позиции " + i);
                i++;

                map.Set(key, ReadValue(s, ref i));
                SkipSpace(s, ref i);

                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; return map; }
                throw new FormatException("ожидалось ',' или '}' в позиции " + i);
            }
        }

        static List<object> ReadArray(string s, ref int i)
        {
            var list = new List<object>();
            i++;   // [
            SkipSpace(s, ref i);

            if (i < s.Length && s[i] == ']') { i++; return list; }

            while (true)
            {
                list.Add(ReadValue(s, ref i));
                SkipSpace(s, ref i);

                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; return list; }
                throw new FormatException("ожидалось ',' или ']' в позиции " + i);
            }
        }

        static string ReadString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++;   // "

            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();

                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;

                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 <= s.Length)
                        {
                            sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                            i += 4;
                        }
                        break;
                    default: sb.Append(e); break;
                }
            }
            throw new FormatException("строка не закрыта кавычкой");
        }

        static object ReadNumber(string s, ref int i)
        {
            int from = i;
            while (i < s.Length && ("+-0123456789.eE".IndexOf(s[i]) >= 0)) i++;

            string part = s.Substring(from, i - from);
            double d;
            if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                throw new FormatException("это не число: '" + part + "'");
            return d;
        }

        // ---------------- запись ----------------

        /// <summary>Превратить значение в JSON одной строкой.</summary>
        public static string Text(object value) { return Write(value, false, 0); }

        /// <summary>Превратить значение в JSON с отступами.</summary>
        public static string Pretty(object value) { return Write(value, true, 0); }

        /// <summary>Записать значение в файл.</summary>
        public static string Save(string path, object value)
        {
            string target = DupoRuntime.Resolve(path);
            string dir = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(target, Pretty(value), new UTF8Encoding(false));
            Log("Записан JSON: " + target);
            return target;
        }

        static string Write(object v, bool pretty, int depth)
        {
            if (v == null) return "null";
            if (v is bool) return (bool)v ? "true" : "false";
            if (v is double) return Num((double)v);
            if (v is string) return Quote((string)v);

            string pad = pretty ? new string(' ', (depth + 1) * 2) : "";
            string close = pretty ? new string(' ', depth * 2) : "";
            string nl = pretty ? Environment.NewLine : "";
            string gap = pretty ? ": " : ":";

            var map = v as DupoMap;
            if (map != null)
            {
                if (map.Count == 0) return "{}";
                var parts = new List<string>();
                foreach (var pair in map.Pairs)
                    parts.Add(pad + Quote(pair.Key) + gap + Write(pair.Value, pretty, depth + 1));
                return "{" + nl + string.Join("," + nl, parts.ToArray()) + nl + close + "}";
            }

            var list = v as List<object>;
            if (list != null)
            {
                if (list.Count == 0) return "[]";
                var parts = new List<string>();
                foreach (object x in list) parts.Add(pad + Write(x, pretty, depth + 1));
                return "[" + nl + string.Join("," + nl, parts.ToArray()) + nl + close + "]";
            }

            return Quote(Val.Str(v));
        }

        static string Num(double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) return "null";
            if (Math.Abs(d - Math.Round(d)) < 1e-10 && Math.Abs(d) < 1e15)
                return ((long)Math.Round(d)).ToString(CultureInfo.InvariantCulture);
            return d.ToString("R", CultureInfo.InvariantCulture);
        }

        static string Quote(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
