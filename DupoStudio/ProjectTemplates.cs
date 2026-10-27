using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using DupoLang;

namespace DupoStudio
{
    /// <summary>Создание и выбор проектов — одинаково нужно и лаунчеру, и редактору.</summary>
    public static class ProjectTemplates
    {
        public static string ProjectsDir
        {
            get { return Path.Combine(DupoRuntime.RuntimeDir, "Projects"); }
        }

        static string AskName(IWin32Window owner, string title, string preset, out string dir)
        {
            dir = null;

            string name = PromptDialog.Show(owner, title, "Название проекта:", preset);
            if (string.IsNullOrEmpty(name)) return null;

            string target = Path.Combine(ProjectsDir, name);
            if (Directory.Exists(target))
            {
                MessageBox.Show(owner as IWin32Window, "Такой проект уже есть.", "Dupo Studio");
                return null;
            }

            dir = target;
            return name;
        }

        /// <summary>Сайт на DupoLang. Возвращает папку проекта или null.</summary>
        public static string CreateWeb(IWin32Window owner)
        {
            string dir;
            string name = AskName(owner, "Новый проект", "MyApp", out dir);
            if (name == null) return null;

            Directory.CreateDirectory(Path.Combine(dir, "www"));

            var code = new StringBuilder();
            code.AppendLine("// " + name + " — проект Dupo Studio");
            code.AppendLine("import Dupo.Web");
            code.AppendLine();
            code.AppendLine("print(\"Запускаю " + name + "...\")");
            code.AppendLine();
            code.AppendLine("Web.Server site = new Web.Server(8080, \"www\", \"index\")");
            code.AppendLine();
            code.AppendLine("if (Web.Server.Php.IsInstalled == false) {");
            code.AppendLine("    Web.Server.Php.Install()");
            code.AppendLine("}");
            code.AppendLine();
            code.AppendLine("site.Start()");
            code.AppendLine("Web.Open(site.Url)");

            File.WriteAllText(Path.Combine(dir, "main.dupolang"), code.ToString(), new UTF8Encoding(false));

            string html =
                "<!DOCTYPE html>\r\n<html lang=\"ru\">\r\n<head>\r\n<meta charset=\"utf-8\">\r\n" +
                "<title>" + name + "</title>\r\n</head>\r\n<body style=\"font-family:Segoe UI,sans-serif;" +
                "background:#141821;color:#e6e9ef;padding:48px\">\r\n<h1>" + name + "</h1>\r\n" +
                "<p>Страница раздаётся сервером DupoLang.</p>\r\n</body>\r\n</html>";
            File.WriteAllText(Path.Combine(dir, "www", "index.html"), html, new UTF8Encoding(false));

            return dir;
        }

        /// <summary>Проект на Java: исходники в src, сборка в .jar скриптом DupoLang.</summary>
        public static string CreateJava(IWin32Window owner)
        {
            const string Q = "\"";   // кавычка внутри генерируемого кода

            string dir;
            string name = AskName(owner, "Новый Java-проект", "MyJavaApp", out dir);
            if (name == null) return null;

            Directory.CreateDirectory(Path.Combine(dir, "src"));

            var code = new StringBuilder();
            code.AppendLine("// " + name + " — проект на Java");
            code.AppendLine("using Dupo.Java");
            code.AppendLine("using Dupo.System.Files");
            code.AppendLine();
            code.AppendLine("print(" + Q + "Собираю " + name + "..." + Q + ")");
            code.AppendLine();
            code.AppendLine("if (Java.IsInstalled == false) {");
            code.AppendLine("    Java.Install()");
            code.AppendLine("}");
            code.AppendLine();
            code.AppendLine("Java.Compile(" + Q + "src" + Q + ", " + Q + "build/classes" + Q + ")");
            code.AppendLine("string jar = Java.Jar(" + Q + "build/classes" + Q + ", " + Q +
                            "build/" + name + ".jar" + Q + ", " + Q + "Main" + Q + ")");
            code.AppendLine();
            code.AppendLine("print(" + Q + Q + ")");
            code.AppendLine("print(" + Q + "Запускаю:" + Q + ")");
            code.AppendLine("Java.RunJar(jar)");
            code.AppendLine();
            code.AppendLine("print(" + Q + Q + ")");
            code.AppendLine("print(" + Q + "Размер: " + Q + " + Files.Size(jar) + " + Q + " байт" + Q + ")");

            File.WriteAllText(Path.Combine(dir, "main.dupolang"), code.ToString(), new UTF8Encoding(false));

            var java = new StringBuilder();
            java.AppendLine("public class Main {");
            java.AppendLine("    public static void main(String[] args) {");
            java.AppendLine("        System.out.println(" + Q + "Привет из " + name + "!" + Q + ");");
            java.AppendLine();
            java.AppendLine("        for (int i = 1; i <= 5; i++) {");
            java.AppendLine("            System.out.println(" + Q + "  строка " + Q + " + i);");
            java.AppendLine("        }");
            java.AppendLine("    }");
            java.AppendLine("}");

            File.WriteAllText(Path.Combine(dir, "src", "Main.java"), java.ToString(), new UTF8Encoding(false));

            return dir;
        }

        /// <summary>Выбор существующей папки проекта.</summary>
        public static string Choose(IWin32Window owner, string startDir)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Выберите папку проекта";
                dlg.SelectedPath = Directory.Exists(startDir) ? startDir : ProjectsDir;
                return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.SelectedPath : null;
            }
        }
    }
}
