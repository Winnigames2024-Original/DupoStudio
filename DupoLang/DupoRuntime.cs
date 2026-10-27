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

        /// <summary>
        /// Путь к DupoLang.dll на диске. Студия держит библиотеку ресурсом
        /// внутри exe и подставляет сюда распакованную копию, чтобы проекты
        /// на C# могли на неё ссылаться.
        /// </summary>
        public static string LibraryPath;

        /// <summary>DupoLang.dll, на которую можно сослаться при сборке C#. Пусто — нет такой.</summary>
        public static string Library
        {
            get
            {
                if (!string.IsNullOrEmpty(LibraryPath) && File.Exists(LibraryPath)) return LibraryPath;

                try
                {
                    string own = typeof(DupoRuntime).Assembly.Location;
                    if (!string.IsNullOrEmpty(own) && File.Exists(own)) return own;
                }
                catch { }

                string beside = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DupoLang.dll");
                return File.Exists(beside) ? beside : "";
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
