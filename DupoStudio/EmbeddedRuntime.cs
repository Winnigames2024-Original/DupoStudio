using System;
using System.IO;
using System.Reflection;

namespace DupoStudio
{
    /// <summary>
    /// Студия — один файл: DupoLang.dll и DupoHost.exe лежат ресурсами внутри exe.
    /// Библиотека подхватывается из ресурса при загрузке, рантайм распаковывается
    /// на диск только когда нужно собрать приложение.
    ///
    /// Если рядом со студией лежат настоящие файлы (так бывает при сборке
    /// в Visual Studio), берутся они — ресурсы остаются запасным вариантом.
    /// </summary>
    public static class EmbeddedRuntime
    {
        const string LangResource = "DupoStudio.Embedded.DupoLang.dll";
        const string HostResource = "DupoStudio.Embedded.DupoHost.exe";

        static Assembly cached;

        /// <summary>Вызывается первой строкой Main — до того, как понадобится DupoLang.</summary>
        public static void Install()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
        }

        static Assembly ResolveAssembly(object sender, ResolveEventArgs e)
        {
            string wanted;
            try { wanted = new AssemblyName(e.Name).Name; }
            catch { return null; }

            if (!string.Equals(wanted, "DupoLang", StringComparison.OrdinalIgnoreCase)) return null;
            if (cached != null) return cached;

            byte[] data = Read(LangResource);
            if (data == null) return null;

            cached = Assembly.Load(data);
            return cached;
        }

        static byte[] Read(string name)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (s == null) return null;

                var data = new byte[s.Length];
                int done = 0;
                while (done < data.Length)
                {
                    int n = s.Read(data, done, data.Length - done);
                    if (n <= 0) break;
                    done += n;
                }
                return data;
            }
        }

        /// <summary>
        /// Путь к DupoHost.exe: файл рядом со студией либо распакованный из ресурса.
        /// null — рантайма нет нигде.
        /// </summary>
        public static string HostPath()
        {
            string beside = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DupoHost.exe");
            if (File.Exists(beside)) return beside;

            byte[] data = Read(HostResource);
            if (data == null) return null;

            string dir = Path.Combine(Path.GetTempPath(), "dupo-runtime");
            Directory.CreateDirectory(dir);
            string target = Path.Combine(dir, "DupoHost.exe");

            if (!File.Exists(target) || new FileInfo(target).Length != data.Length)
                File.WriteAllBytes(target, data);

            return target;
        }

        /// <summary>
        /// Путь к DupoLang.dll на диске. Нужен, когда проект на C# ссылается
        /// на библиотеку, чтобы звать Dupo.Lang.ExecuteFrom — из ресурса
        /// компилятору её не подсунуть, поэтому распаковываем.
        /// </summary>
        public static string LibraryPath()
        {
            string beside = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DupoLang.dll");
            if (File.Exists(beside)) return beside;

            byte[] data = Read(LangResource);
            if (data == null) return null;

            string dir = Path.Combine(Path.GetTempPath(), "dupo-runtime");
            Directory.CreateDirectory(dir);
            string target = Path.Combine(dir, "DupoLang.dll");

            if (!File.Exists(target) || new FileInfo(target).Length != data.Length)
                File.WriteAllBytes(target, data);

            return target;
        }

        /// <summary>Есть ли рантайм внутри exe (для надписи «о программе»).</summary>
        public static bool Bundled
        {
            get
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(HostResource))
                    return s != null;
            }
        }
    }
}
