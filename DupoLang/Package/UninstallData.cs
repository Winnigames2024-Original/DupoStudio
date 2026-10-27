using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DupoLang
{
    /// <summary>
    /// Данные об установленном приложении. Лежат рядом с uninstall.exe
    /// в файле uninstall.dll: что за программа, куда поставлена, что
    /// за собой убирать. Формат свой — подпись, пары «ключ=значение»
    /// и список файлов.
    /// </summary>
    public class UninstallData
    {
        public const string FileName = "uninstall.dll";
        const string Signature = "DUNI";
        const ushort Version = 1;

        public string Name = "Приложение";
        public string AppVersion = "1.0";
        public string Publisher = "";
        public string Folder = "";
        public string AppId = "";
        public string MainExe = "";
        public string Shortcut = "";
        public string InstalledAt = "";
        public string Dir = "";

        /// <summary>Расширения, связанные с приложением.</summary>
        public readonly List<string> Associations = new List<string>();

        /// <summary>Что было распаковано — по этому списку и удаляем.</summary>
        public readonly List<string> Files = new List<string>();

        /// <summary>Прочие поля, которые кладёт установщик.</summary>
        public readonly Dictionary<string, string> Extra =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // ------------------------------------------------------------------
        // запись
        // ------------------------------------------------------------------

        /// <summary>Записать данные рядом с uninstall.exe.</summary>
        public string Save(string dir)
        {
            string path = Path.Combine(dir, FileName);

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(fs, new UTF8Encoding(false)))
            {
                w.Write(Encoding.ASCII.GetBytes(Signature));
                w.Write(Version);

                var head = new List<string>
                {
                    "name=" + Name,
                    "version=" + AppVersion,
                    "publisher=" + Publisher,
                    "folder=" + Folder,
                    "appid=" + AppId,
                    "exe=" + MainExe,
                    "shortcut=" + Shortcut,
                    "installed=" + (InstalledAt.Length > 0 ? InstalledAt : DateTime.Now.ToString("yyyy-MM-dd HH:mm")),
                    "dir=" + Dir,
                    "assoc=" + string.Join(",", Associations.ToArray())
                };

                foreach (var pair in Extra)
                    head.Add(pair.Key + "=" + pair.Value);

                w.Write(string.Join("\n", head.ToArray()));

                w.Write(Files.Count);
                foreach (string f in Files) w.Write(f ?? "");

                w.Write(Encoding.ASCII.GetBytes("DEND"));
            }

            return path;
        }

        // ------------------------------------------------------------------
        // чтение
        // ------------------------------------------------------------------

        /// <summary>Прочитать uninstall.dll из папки. null — файла нет или он не наш.</summary>
        public static UninstallData Load(string dir)
        {
            string path = Path.Combine(dir ?? "", FileName);
            if (!File.Exists(path)) return null;

            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (var r = new BinaryReader(fs, new UTF8Encoding(false)))
                {
                    string sig = Encoding.ASCII.GetString(r.ReadBytes(4));
                    if (sig != Signature) return null;

                    r.ReadUInt16();

                    var data = new UninstallData();
                    data.Dir = dir;

                    foreach (string line in r.ReadString().Split('\n'))
                    {
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;

                        string key = line.Substring(0, eq).Trim();
                        string value = line.Substring(eq + 1);

                        switch (key.ToLowerInvariant())
                        {
                            case "name": data.Name = value; break;
                            case "version": data.AppVersion = value; break;
                            case "publisher": data.Publisher = value; break;
                            case "folder": data.Folder = value; break;
                            case "appid": data.AppId = value; break;
                            case "exe": data.MainExe = value; break;
                            case "shortcut": data.Shortcut = value; break;
                            case "installed": data.InstalledAt = value; break;
                            case "dir": if (value.Length > 0) data.Dir = value; break;
                            case "assoc":
                                foreach (string ext in value.Split(',', ';', ' '))
                                    if (ext.Trim().Length > 0) data.Associations.Add(ext.Trim());
                                break;
                            default: data.Extra[key] = value; break;
                        }
                    }

                    int count = r.ReadInt32();
                    for (int i = 0; i < count; i++) data.Files.Add(r.ReadString());

                    return data;
                }
            }
            catch { return null; }
        }

        /// <summary>Сколько места занимает установленное — в килобайтах.</summary>
        public long SizeKb
        {
            get
            {
                long total = 0;
                try
                {
                    if (Directory.Exists(Dir))
                        total = Directory.GetFiles(Dir, "*.*", SearchOption.AllDirectories)
                                         .Select(f => new FileInfo(f).Length).Sum();
                }
                catch { }
                return total / 1024;
            }
        }

        /// <summary>Человеческое описание для окна удаления.</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Name + (AppVersion.Length > 0 ? "  " + AppVersion : ""));
            if (Publisher.Length > 0) sb.AppendLine("Издатель: " + Publisher);
            sb.AppendLine("Папка: " + Dir);
            if (InstalledAt.Length > 0) sb.AppendLine("Установлено: " + InstalledAt);
            sb.AppendLine("Файлов: " + Files.Count);
            return sb.ToString();
        }

        public override string ToString() { return Name + " " + AppVersion; }
    }
}
