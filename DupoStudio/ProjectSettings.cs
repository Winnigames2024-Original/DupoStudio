using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DupoLang;

namespace DupoStudio
{
    /// <summary>
    /// Свойства проекта — файл project.dupo в его корне.
    /// Простые строки «ключ=значение», чтобы их можно было править руками.
    /// </summary>
    public class ProjectSettings
    {
        public const string FileName = "project.dupo";

        public string Dir = "";
        public string Name = "";
        public string Product = "Dupo Studio";
        public string Author = "Winnigames2024";
        public string Version = "1.0";
        public string Description = "";
        public string Namespace = "";
        public string JavaPackage = "";
        public bool SingleExe = true;

        /// <summary>Библиотеки, которые подключаются при сборке C#.</summary>
        public readonly List<string> References = new List<string>();

        public string FilePath { get { return Path.Combine(Dir, FileName); } }

        public string ResourcesDir { get { return Path.Combine(Dir, ResourceSet.FolderName); } }

        public static ProjectSettings Load(string projectDir)
        {
            var s = new ProjectSettings();
            s.Dir = projectDir ?? "";
            s.Name = Path.GetFileName(s.Dir);
            s.Namespace = CleanNamespace(s.Name);
            s.JavaPackage = "com.example." + s.Name.ToLowerInvariant().Replace(" ", "");

            try
            {
                if (!File.Exists(s.FilePath)) return s;

                foreach (string raw in File.ReadAllLines(s.FilePath, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;

                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string value = line.Substring(eq + 1).Trim();

                    if (key == "name") s.Name = value;
                    else if (key == "product") s.Product = value;
                    else if (key == "author") s.Author = value;
                    else if (key == "version") s.Version = value;
                    else if (key == "description") s.Description = value;
                    else if (key == "namespace") s.Namespace = value;
                    else if (key == "javapackage") s.JavaPackage = value;
                    else if (key == "references")
                    {
                        foreach (string r in value.Split(';', ','))
                            if (r.Trim().Length > 0 && !s.References.Contains(r.Trim()))
                                s.References.Add(r.Trim());
                    }
                    else if (key == "singleexe") s.SingleExe = value == "1" || value == "true";
                }
            }
            catch { }

            return s;
        }

        public void Save()
        {
            try
            {
                var lines = new List<string>();
                lines.Add("# Свойства проекта Dupo Studio");
                lines.Add("name=" + Name);
                lines.Add("product=" + Product);
                lines.Add("author=" + Author);
                lines.Add("version=" + Version);
                lines.Add("description=" + Description);
                lines.Add("namespace=" + Namespace);
                lines.Add("javapackage=" + JavaPackage);
                lines.Add("references=" + string.Join(";", References.ToArray()));
                lines.Add("singleexe=" + (SingleExe ? "1" : "0"));

                File.WriteAllLines(FilePath, lines.ToArray(), new UTF8Encoding(false));
            }
            catch { }
        }

        /// <summary>Переносит данные проекта в паспорт собираемого приложения.</summary>
        public void IntoManifest(DappManifest m)
        {
            m.Name = Name;
            m.Version = Version;
            m.Author = Author;
            m.Description = Description;

            if (Product.Length > 0) m.Extra["product"] = Product;
            if (Namespace.Length > 0) m.Extra["namespace"] = Namespace;
            if (JavaPackage.Length > 0) m.Extra["javapackage"] = JavaPackage;
        }

        static string CleanNamespace(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name ?? "")
                sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');

            string result = sb.ToString();
            if (result.Length == 0) return "App";
            if (char.IsDigit(result[0])) result = "_" + result;
            return char.ToUpperInvariant(result[0]) + result.Substring(1);
        }
    }
}
