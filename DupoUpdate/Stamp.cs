using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DupoUpdate
{
    /// <summary>
    /// Отметка сборки — содержимое version.json.
    ///
    /// Номер версии меняется не при каждом выпуске: бывает, что ту же версию
    /// пересобирают с поправками. Тогда version остаётся прежним, а
    /// updatedversion растёт на единицу — по нему обновление и различает
    /// такие сборки. При смене version счётчик начинается заново с единицы.
    ///
    /// <code>
    /// {
    ///   "version": "0.0.7",
    ///   "updatedversion": 2
    /// }
    /// </code>
    /// </summary>
    public class Stamp
    {
        /// <summary>Имя файла — и в папке программы, и среди файлов выпуска.</summary>
        public const string FileName = "version.json";

        public string Version = "";

        /// <summary>Номер сборки этой версии: 1, 2, 3 и так далее.</summary>
        public int Updated;

        public Stamp() { }
        public Stamp(string version, int updated) { Version = version ?? ""; Updated = updated; }

        public override string ToString()
        {
            string v = Version.Length > 0 ? Version : "неизвестно";
            return Updated > 1 ? v + " (сборка " + Updated + ")" : v;
        }

        // ------------------------------------------------------------------
        // чтение
        // ------------------------------------------------------------------

        /// <summary>Разобрать содержимое version.json. Никогда не бросает.</summary>
        public static Stamp Parse(string json)
        {
            var s = new Stamp();
            if (string.IsNullOrEmpty(json)) return s;

            var v = Regex.Match(json, "\"version\"\\s*:\\s*\"([^\"]*)\"");
            if (v.Success) s.Version = v.Groups[1].Value.Trim();

            var u = Regex.Match(json, "\"updatedversion\"\\s*:\\s*\"?(\\d+)\"?");
            if (u.Success)
            {
                int n;
                if (int.TryParse(u.Groups[1].Value, out n)) s.Updated = n;
            }

            return s;
        }

        /// <summary>Отметка установленной программы — файл рядом с ней.</summary>
        public static Stamp Read(string dir)
        {
            try
            {
                string path = Path.Combine(dir ?? "", FileName);
                if (File.Exists(path)) return Parse(File.ReadAllText(path, Encoding.UTF8));
            }
            catch { }

            return new Stamp();
        }

        /// <summary>Записать отметку рядом с программой.</summary>
        public bool Write(string dir)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine("  \"version\": \"" + Version + "\",");
                sb.AppendLine("  \"updatedversion\": " + Math.Max(1, Updated));
                sb.AppendLine("}");

                File.WriteAllText(Path.Combine(dir, FileName), sb.ToString(), new UTF8Encoding(false));
                return true;
            }
            catch { return false; }
        }

        // ------------------------------------------------------------------
        // сравнение
        // ------------------------------------------------------------------

        /// <summary>
        /// Новее ли сборка <paramref name="there"/>, чем <paramref name="here"/>.
        /// Сначала смотрим версию, и только при равной — номер сборки.
        /// </summary>
        public static bool Newer(Stamp here, Stamp there)
        {
            if (there == null || there.Version.Length == 0) return false;
            if (here == null) return true;

            int byVersion = Updater.Compare(there.Version, here.Version);
            if (byVersion != 0) return byVersion > 0;

            return there.Updated > here.Updated;
        }
    }
}
