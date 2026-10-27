using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DupoLang
{
    /// <summary>
    /// Один файл из папки Resources проекта. Отдаёт себя путём, байтами
    /// или текстом — смотря что нужно тому, кому его передают.
    /// </summary>
    public class DupoResource
    {
        public string Name { get; private set; }
        public string Path { get; private set; }

        public DupoResource(string name, string path)
        {
            Name = name;
            Path = path;
        }

        public bool Exists { get { return File.Exists(Path); } }
        public double Size { get { return Exists ? new FileInfo(Path).Length : 0; } }
        public string Extension { get { return System.IO.Path.GetExtension(Path); } }
        public string FileName { get { return System.IO.Path.GetFileName(Path); } }

        public string Text
        {
            get { return Exists ? File.ReadAllText(Path, Encoding.UTF8) : ""; }
        }

        public List<object> Lines
        {
            get
            {
                var list = new List<object>();
                if (!Exists) return list;
                foreach (string line in File.ReadAllLines(Path, Encoding.UTF8)) list.Add(line);
                return list;
            }
        }

        /// <summary>Чтобы ресурс можно было просто склеить со строкой пути.</summary>
        public override string ToString() { return Path; }
    }

    /// <summary>Папка Resources проекта: читается при подключении модуля.</summary>
    public static class ResourceSet
    {
        public const string FolderName = "Resources";

        public static string Folder
        {
            get { return System.IO.Path.Combine(DupoRuntime.ProjectDir, FolderName); }
        }

        /// <summary>Имя ресурса — имя файла без расширения, пригодное для кода.</summary>
        public static string NameOf(string file)
        {
            string raw = System.IO.Path.GetFileNameWithoutExtension(file);

            var sb = new StringBuilder();
            foreach (char c in raw)
                sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');

            string name = sb.ToString();
            if (name.Length == 0) return "res";
            if (char.IsDigit(name[0])) name = "_" + name;
            return name;
        }

        /// <summary>Все ресурсы проекта: имя → файл.</summary>
        public static Dictionary<string, DupoResource> Load()
        {
            var map = new Dictionary<string, DupoResource>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(Folder)) return map;

            foreach (string file in Directory.GetFiles(Folder, "*", SearchOption.AllDirectories))
            {
                string name = NameOf(file);

                // Два файла с одинаковым именем — добавляем расширение, чтобы не потерять.
                if (map.ContainsKey(name))
                    name = name + "_" + System.IO.Path.GetExtension(file).TrimStart('.');

                if (!map.ContainsKey(name)) map[name] = new DupoResource(name, file);
            }

            return map;
        }

        /// <summary>Имена для подсказок в редакторе.</summary>
        public static List<string> Names(string projectDir)
        {
            var names = new List<string>();
            string dir = System.IO.Path.Combine(projectDir ?? "", FolderName);
            if (!Directory.Exists(dir)) return names;

            foreach (string file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            {
                string name = NameOf(file);
                if (!names.Contains(name)) names.Add(name);
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }
    }
}
