using System;
using System.IO;

namespace DupoLang
{
    /// <summary>Пути, с которыми работает запущенный скрипт.</summary>
    public static class DupoRuntime
    {
        /// <summary>Папка текущего проекта — относительные пути считаются от неё.</summary>
        public static string ProjectDir = AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>Корень установки DupoApp — сюда ставится PHP и прочие рантаймы.</summary>
        public static string RuntimeDir
        {
            get
            {
                var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                while (dir != null)
                {
                    if (string.Equals(dir.Name, "DupoApp", StringComparison.OrdinalIgnoreCase))
                        return dir.FullName;
                    dir = dir.Parent;
                }
                if (Directory.Exists(@"C:\DupoApp")) return @"C:\DupoApp";
                return AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        public static string Resolve(string path)
        {
            if (string.IsNullOrEmpty(path)) return ProjectDir;
            if (Path.IsPathRooted(path)) return Path.GetFullPath(path);
            return Path.GetFullPath(Path.Combine(ProjectDir, path));
        }
    }
}
