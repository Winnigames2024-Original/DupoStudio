using System;
using System.Collections.Generic;
using System.Text;

namespace DupoLang
{
    /// <summary>Паспорт приложения внутри .dapp.</summary>
    public class DappManifest
    {
        public string Name = "App";
        public string Version = "1.0";
        public string Main = "main.dupolang";
        public string Author = "";
        public string Description = "";
        public string Created = "";
        public string Runtime = "DupoLang " + Dupo.Version;

        public readonly Dictionary<string, string> Extra =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string ToText()
        {
            var sb = new StringBuilder();
            sb.Append("name: ").Append(Name).Append('\n');
            sb.Append("version: ").Append(Version).Append('\n');
            sb.Append("main: ").Append(Main).Append('\n');
            sb.Append("author: ").Append(Author).Append('\n');
            sb.Append("description: ").Append(Description).Append('\n');
            sb.Append("created: ").Append(Created).Append('\n');
            sb.Append("runtime: ").Append(Runtime).Append('\n');
            foreach (var kv in Extra)
                sb.Append(kv.Key).Append(": ").Append(kv.Value).Append('\n');
            return sb.ToString();
        }

        public static DappManifest Parse(string text)
        {
            var m = new DappManifest();
            if (string.IsNullOrEmpty(text)) return m;

            foreach (string raw in text.Split('\n'))
            {
                string lineText = raw.Trim('\r', ' ', '\t');
                if (lineText.Length == 0) continue;
                int colon = lineText.IndexOf(':');
                if (colon <= 0) continue;

                string key = lineText.Substring(0, colon).Trim().ToLowerInvariant();
                string value = lineText.Substring(colon + 1).Trim();

                if (key == "name") m.Name = value;
                else if (key == "version") m.Version = value;
                else if (key == "main") m.Main = value;
                else if (key == "author") m.Author = value;
                else if (key == "description") m.Description = value;
                else if (key == "created") m.Created = value;
                else if (key == "runtime") m.Runtime = value;
                else m.Extra[key] = value;
            }
            return m;
        }

        public override string ToString()
        {
            return Name + " " + Version + " (" + Main + ")";
        }
    }

    /// <summary>Одна запись внутри архива.</summary>
    public class DappEntry
    {
        public string Path;
        public int Size;
        public int Stored;
        public byte Method;
        public uint Crc;

        public string MethodName { get { return Method == 1 ? "deflate" : "store"; } }

        public override string ToString()
        {
            return Path + "  " + Size + " → " + Stored + " (" + MethodName + ")";
        }
    }

    /// <summary>Содержимое архива без распаковки.</summary>
    public class DappInfo
    {
        public string File;
        public DappManifest Manifest;
        public List<DappEntry> Entries = new List<DappEntry>();
        public long FileSize;
        public long TotalSize;

        public string Summary()
        {
            var sb = new StringBuilder();
            sb.Append(Manifest.Name).Append(' ').Append(Manifest.Version).Append('\n');
            if (Manifest.Description.Length > 0) sb.Append(Manifest.Description).Append('\n');
            if (Manifest.Author.Length > 0) sb.Append("Автор: ").Append(Manifest.Author).Append('\n');
            sb.Append("Главный файл: ").Append(Manifest.Main).Append('\n');
            sb.Append("Собрано: ").Append(Manifest.Created).Append('\n');
            sb.Append("Файлов: ").Append(Entries.Count)
              .Append(", ").Append(TotalSize / 1024).Append(" КБ → ")
              .Append(FileSize / 1024).Append(" КБ");
            return sb.ToString();
        }
    }
}
