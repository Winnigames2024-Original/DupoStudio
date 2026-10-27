using System;
using System.Collections.Generic;
using System.IO;
using DupoLang;

namespace DupoSetup
{
    /// <summary>
    /// Что и куда ставить. Лежит в манифесте .dapp, который дописан
    /// в хвост установщика — тем же приёмом, что и собранные приложения.
    /// </summary>
    public class SetupInfo
    {
        public string Name = "Приложение";
        public string Version = "1.0";
        public string Publisher = "";
        public string Folder = "App";
        public string MainExe = "";
        public string Shortcut = "";
        public bool Desktop = true;
        public List<string> Associations = new List<string>();

        /// <summary>Папка по умолчанию: ставим для пользователя, без прав администратора.</summary>
        public string DefaultDir
        {
            get
            {
                string root = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs");
                return Path.Combine(root, Folder);
            }
        }

        /// <summary>Ключ в списке установленных программ.</summary>
        public string AppId
        {
            get { return "DupoSetup." + DappPackage.SafeName(Folder); }
        }

        public static SetupInfo FromManifest(DappManifest m)
        {
            var info = new SetupInfo();
            if (m == null) return info;

            info.Name = Pick(m, "setup.name", m.Name);
            info.Version = Pick(m, "setup.version", m.Version);
            info.Publisher = Pick(m, "setup.publisher", m.Author);
            info.Folder = Pick(m, "setup.folder", DappPackage.SafeName(info.Name));
            info.MainExe = Pick(m, "setup.exe", "");
            info.Shortcut = Pick(m, "setup.shortcut", info.Name);

            string desktop = Pick(m, "setup.desktop", "1");
            info.Desktop = desktop == "1" || desktop.Equals("true", StringComparison.OrdinalIgnoreCase);

            string assoc = Pick(m, "setup.assoc", "");
            foreach (string part in assoc.Split(',', ';', ' '))
            {
                string ext = part.Trim();
                if (ext.Length == 0) continue;
                if (ext[0] != '.') ext = "." + ext;
                info.Associations.Add(ext.ToLowerInvariant());
            }

            return info;
        }

        public void IntoManifest(DappManifest m)
        {
            m.Name = Name;
            m.Version = Version;
            m.Author = Publisher;

            m.Extra["setup.name"] = Name;
            m.Extra["setup.version"] = Version;
            m.Extra["setup.publisher"] = Publisher;
            m.Extra["setup.folder"] = Folder;
            m.Extra["setup.exe"] = MainExe;
            m.Extra["setup.shortcut"] = Shortcut;
            m.Extra["setup.desktop"] = Desktop ? "1" : "0";
            m.Extra["setup.assoc"] = string.Join(",", Associations.ToArray());
        }

        static string Pick(DappManifest m, string key, string fallback)
        {
            string v;
            if (m.Extra.TryGetValue(key, out v) && !string.IsNullOrEmpty(v)) return v;
            return fallback ?? "";
        }
    }
}
