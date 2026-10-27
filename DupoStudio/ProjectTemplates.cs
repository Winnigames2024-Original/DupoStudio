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
        /// <summary>Кавычка внутри генерируемого кода.</summary>
        const string Q = "\"";

        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        public static string ProjectsDir
        {
            get { return Path.Combine(DupoRuntime.RuntimeDir, "Projects"); }
        }

        // ------------------------------------------------------------------
        // диалог выбора шаблона
        // ------------------------------------------------------------------

        /// <summary>Показать окно «Новый проект». Вернёт папку проекта или null.</summary>
        public static string New(IWin32Window owner)
        {
            return NewProjectForm.Ask(owner, ProjectsDir);
        }

        /// <summary>Создать проект по шаблону. Вернёт папку проекта или null.</summary>
        public static string Create(IWin32Window owner, ProjectTemplate template, string name, string location)
        {
            if (template == null || string.IsNullOrEmpty(name)) return null;

            string dir = Path.Combine(location, name);

            try
            {
                Directory.CreateDirectory(dir);

                switch (template.Kind)
                {
                    case "dupolang-web": Web(dir, name); break;
                    case "dupolang-console": ConsoleApp(dir, name); break;
                    case "dupolang-service": Service(dir, name); break;
                    case "csharp-console": CsConsole(dir, name); break;
                    case "csharp-forms": CsForms(dir, name); break;
                    case "csharp-library": CsLibrary(dir, name); break;
                    case "csharp-dupolang": CsWithDupo(dir, name); break;
                    case "winforms-dupolang": FormsWithDupo(dir, name); break;
                    case "cpp-console": CppConsole(dir, name); break;
                    case "cpp-window": CppWindow(dir, name); break;
                    case "java": Java(dir, name); break;
                    case "setup": Setup(dir, name); break;
                    default: ConsoleApp(dir, name); break;
                }

                WriteSettings(dir, name, template);
                return dir;
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, "Проект не создался:" + Environment.NewLine + ex.Message,
                                "Dupo Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
        }

        /// <summary>Записать project.dupo с данными шаблона.</summary>
        static void WriteSettings(string dir, string name, ProjectTemplate t)
        {
            var s = new ProjectSettings();
            s.Name = name;
            s.Product = "Dupo Studio";
            s.Author = "Winnigames2024";
            s.Version = "1.0";
            s.Description = t.Title;
            s.Namespace = Ident(name);
            s.JavaPackage = "com.winnigames2024." + Ident(name).ToLowerInvariant();
            s.SingleExe = true;
            s.Dir = dir;
            s.Save();
        }

        /// <summary>Имя, годное для пространства имён: буквы, цифры, подчёркивание.</summary>
        public static string Ident(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name ?? "")
                if (char.IsLetterOrDigit(c) || c == '_') sb.Append(c);

            if (sb.Length == 0) return "App";
            if (char.IsDigit(sb[0])) sb.Insert(0, '_');
            return sb.ToString();
        }

        static void Save(string dir, string rel, string text)
        {
            string path = Path.Combine(dir, rel);
            string sub = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(sub)) Directory.CreateDirectory(sub);
            File.WriteAllText(path, text, Utf8);
        }

        // ------------------------------------------------------------------
        // DupoLang
        // ------------------------------------------------------------------

        static void Web(string dir, string name)
        {
            var code = new StringBuilder();
            code.AppendLine("// " + name + " — веб-приложение Dupo Studio");
            code.AppendLine("import Dupo.Web");
            code.AppendLine();
            code.AppendLine("print(" + Q + "Запускаю " + name + "..." + Q + ")");
            code.AppendLine();
            code.AppendLine("Web.Server site = new Web.Server(8080, " + Q + "www" + Q + ", " + Q + "index" + Q + ")");
            code.AppendLine();
            code.AppendLine("if (Web.Server.Php.IsInstalled == false) {");
            code.AppendLine("    Web.Server.Php.Install()");
            code.AppendLine("}");
            code.AppendLine();
            code.AppendLine("site.Start()");
            code.AppendLine("Web.Open(site.Url)");
            Save(dir, "main.dupolang", code.ToString());

            Save(dir, Path.Combine("www", "index.html"),
                "<!DOCTYPE html>\r\n<html lang=\"ru\">\r\n<head>\r\n<meta charset=\"utf-8\">\r\n" +
                "<title>" + name + "</title>\r\n</head>\r\n<body style=\"font-family:Segoe UI,sans-serif;" +
                "background:#141821;color:#e6e9ef;padding:48px\">\r\n<h1>" + name + "</h1>\r\n" +
                "<p>Страница раздаётся сервером DupoLang.</p>\r\n</body>\r\n</html>");
        }

        static void ConsoleApp(string dir, string name)
        {
            var code = new StringBuilder();
            code.AppendLine("// " + name + " — консольное приложение DupoLang");
            code.AppendLine("using Dupo.System.Files");
            code.AppendLine();
            code.AppendLine("print(" + Q + "Привет из " + name + "!" + Q + ")");
            code.AppendLine();
            code.AppendLine("for (int i = 1; i <= 5; i = i + 1) {");
            code.AppendLine("    print(" + Q + "  строка " + Q + " + i)");
            code.AppendLine("}");
            code.AppendLine();
            code.AppendLine("print(" + Q + Q + ")");
            code.AppendLine("print(" + Q + "Папка проекта: " + Q + " + Files.ProjectDir())");
            Save(dir, "main.dupolang", code.ToString());
        }

        static void Service(string dir, string name)
        {
            string svc = Ident(name);

            var code = new StringBuilder();
            code.AppendLine("// " + name + " — фоновый сервис");
            code.AppendLine("using Dupo.Services");
            code.AppendLine("using Dupo.System.Files");
            code.AppendLine();
            code.AppendLine("// Тип выбирается через Services.ServiceLevel:");
            code.AppendLine("//   Background     — фоновый процесс пользователя, прав админа не нужно");
            code.AppendLine("//   UserProcess    — то же, но с окном");
            code.AppendLine("//   SystemProcess  — служба системы (нужен администратор)");
            code.AppendLine("//   LocalService, NetworkService, Delayed, Manual, Interactive, Disabled");
            code.AppendLine();
            code.AppendLine("Services.Service " + svc + " = new Services.Service(");
            code.AppendLine("    " + Q + name + Q + ",");
            code.AppendLine("    " + Q + "сервис " + name + Q + ",");
            code.AppendLine("    Services.ServiceLevel.Background)");
            code.AppendLine();
            code.AppendLine(svc + ".Program(Files.Join(Files.ProjectDir(), " + Q + "worker.bat" + Q + "))");
            code.AppendLine();
            code.AppendLine("print(" + svc + ".Info())");
            code.AppendLine();
            code.AppendLine(svc + ".Install()");
            code.AppendLine(svc + ".Start()");
            code.AppendLine("print(" + Q + "Состояние: " + Q + " + " + svc + ".State)");
            code.AppendLine();
            code.AppendLine("// Снять сервис: " + svc + ".Stop() и " + svc + ".Uninstall()");
            Save(dir, "main.dupolang", code.ToString());

            Save(dir, "worker.bat",
                "@echo off\r\nrem Тело сервиса " + name + ": здесь делается полезная работа.\r\n" +
                ":loop\r\nping -n 31 127.0.0.1 >nul\r\ngoto loop\r\n");
        }

        // ------------------------------------------------------------------
        // C#
        // ------------------------------------------------------------------

        static void CsConsole(string dir, string name)
        {
            string ns = Ident(name);

            var cs = new StringBuilder();
            cs.AppendLine("using System;");
            cs.AppendLine();
            cs.AppendLine("namespace " + ns);
            cs.AppendLine("{");
            cs.AppendLine("    class Program");
            cs.AppendLine("    {");
            cs.AppendLine("        static void Main(string[] args)");
            cs.AppendLine("        {");
            cs.AppendLine("            Console.OutputEncoding = System.Text.Encoding.UTF8;");
            cs.AppendLine("            Console.WriteLine(\"Привет из " + name + "!\");");
            cs.AppendLine();
            cs.AppendLine("            for (int i = 1; i <= 5; i++)");
            cs.AppendLine("                Console.WriteLine(\"  строка \" + i);");
            cs.AppendLine("        }");
            cs.AppendLine("    }");
            cs.AppendLine("}");
            Save(dir, Path.Combine("src", "Program.cs"), cs.ToString());

            Save(dir, "main.dupolang", CsScript(name, "Build", "консольное приложение"));
        }

        static void CsForms(string dir, string name)
        {
            string ns = Ident(name);
            string src = Path.Combine(dir, "src");
            Directory.CreateDirectory(src);

            // Форма создаётся парой Form1.cs + Form1.Designer.cs, как в Visual Studio,
            // поэтому её сразу можно открыть в конструкторе.
            var form = new WinFormsForm();
            form.DesignerPath = Path.Combine(src, "Form1" + WinFormsCode.DesignerSuffix);
            form.Namespace = ns;
            form.ClassName = "Form1";
            form.Title = name;
            form.ClientSize = new System.Drawing.Size(460, 240);

            var label = new DesignItem();
            label.Kind = "Label";
            label.Name = "label1";
            label.Caption = "Привет из " + name + "!";
            label.Bounds = new System.Drawing.Rectangle(24, 24, 380, 24);
            form.Items.Add(label);

            var button = new DesignItem();
            button.Kind = "Button";
            button.Name = "button1";
            button.Caption = "Нажми меня";
            button.Bounds = new System.Drawing.Rectangle(24, 68, 140, 32);
            button.Click = true;
            form.Items.Add(button);

            WinFormsCode.Write(form, false);
            Save(dir, Path.Combine("src", "Program.cs"), WinFormsCode.Program(form));

            Save(dir, "main.dupolang", CsScript(name, "Forms", "приложение WinForms"));
        }

        /// <summary>Логика на DupoLang, которую зовёт C#.</summary>
        static void Logic(string dir, string name)
        {
            var code = new StringBuilder();
            code.AppendLine("// logic.dupolang — блоки def для " + name);
            code.AppendLine("using Dupo.Text");
            code.AppendLine("using Dupo.Data");
            code.AppendLine();
            code.AppendLine("def Привет(имя) {");
            code.AppendLine("    return " + Q + "Привет, " + Q + " + имя + " + Q + "!" + Q + "");
            code.AppendLine("}");
            code.AppendLine();
            code.AppendLine("def Сумма(a, b) {");
            code.AppendLine("    return a + b");
            code.AppendLine("}");
            code.AppendLine();
            code.AppendLine("def Громко(текст) {");
            code.AppendLine("    return Text.Upper(Text.Trim(текст))");
            code.AppendLine("}");
            Save(dir, "logic.dupolang", code.ToString());
        }

        /// <summary>Скрипт сборки, который ещё и кладёт logic.dupolang рядом с exe.</summary>
        static string DupoScript(string name, string method, string what)
        {
            var code = new StringBuilder();
            code.AppendLine("// " + name + " — " + what + ": C# зовёт блоки def из logic.dupolang");
            code.AppendLine("using Dupo.CSharp");
            code.AppendLine("using Dupo.Lang");
            code.AppendLine("using Dupo.System.Files");
            code.AppendLine();
            code.AppendLine("print(" + Q + "Блоки: " + Q + " + Lang.Functions(" + Q + "logic.dupolang" + Q + "))");
            code.AppendLine();
            code.AppendLine("string exe = CSharp." + method + "(" + Q + "src" + Q + ", " +
                            Q + "build/" + name + ".exe" + Q + ")");
            code.AppendLine();
            code.AppendLine("// Файл с блоками должен лежать рядом с exe");
            code.AppendLine("Files.Copy(" + Q + "logic.dupolang" + Q + ", " + Q + "build/logic.dupolang" + Q + ")");
            code.AppendLine();
            code.AppendLine("print(" + Q + Q + ")");
            code.AppendLine("CSharp.Launch(exe, " + Q + Q + ")");
            return code.ToString();
        }

        static void CsWithDupo(string dir, string name)
        {
            string ns = Ident(name);

            var cs = new StringBuilder();
            cs.AppendLine("using System;");
            cs.AppendLine("using DupoLang;");
            cs.AppendLine();
            cs.AppendLine("namespace " + ns);
            cs.AppendLine("{");
            cs.AppendLine("    class Program");
            cs.AppendLine("    {");
            cs.AppendLine("        const string Logic = \"logic.dupolang\";");
            cs.AppendLine();
            cs.AppendLine("        static void Main(string[] args)");
            cs.AppendLine("        {");
            cs.AppendLine("            Console.OutputEncoding = System.Text.Encoding.UTF8;");
            cs.AppendLine("            Dupo.Lang.Output = Console.WriteLine;");
            cs.AppendLine();
            cs.AppendLine("            Console.WriteLine(Dupo.Lang.TextFrom(Logic, \"Привет\", \"" + name + "\"));");
            cs.AppendLine("            Console.WriteLine(\"2 + 3 = \" + Dupo.Lang.NumberFrom(Logic, \"Сумма\", 2, 3));");
            cs.AppendLine("            Console.WriteLine(Dupo.Lang.TextFrom(Logic, \"Громко\", \"  тише едешь  \"));");
            cs.AppendLine("        }");
            cs.AppendLine("    }");
            cs.AppendLine("}");
            Save(dir, Path.Combine("src", "Program.cs"), cs.ToString());

            Logic(dir, name);
            Save(dir, "main.dupolang", DupoScript(name, "Build", "консольное приложение"));
        }

        /// <summary>
        /// WinForms на DupoLang: ни папки src, ни C#. Форма лежит в .dupoform,
        /// а запускает её program.dupolang — он же держит обработчики.
        /// </summary>
        static void FormsWithDupo(string dir, string name)
        {
            string cls = Ident(name);

            // Форму пишем прямо файлом .dupoform — тем же, что правит конструктор по F7.
            var layout = new StringBuilder();
            layout.AppendLine("# Форма Dupo Studio");
            layout.AppendLine("form " + cls + " " + Q + name + Q + " 460 260");
            layout.AppendLine("Label label1 " + Q + "Нажмите кнопку — ответит DupoLang" + Q + " 24 24 400 24");
            layout.AppendLine("TextBox textBox1 " + Q + "мир" + Q + " 24 60 220 24");
            layout.AppendLine("Button button1 " + Q + "Спросить DupoLang" + Q + " 24 104 200 32");
            Save(dir, cls + FormDesignerPanel.Extension, layout.ToString());

            // Логика — отдельным файлом, как «другой класс».
            Logic(dir, name);

            var code = new StringBuilder();
            code.AppendLine("// " + name + " — окно на DupoLang, без C# и без папки src");
            code.AppendLine("using Dupo.Forms");
            code.AppendLine("using Dupo.Lang");
            code.AppendLine();
            code.AppendLine("// Форма нарисована конструктором: " + cls + FormDesignerPanel.Extension);
            code.AppendLine("Forms.Form main = Forms.Open(" + Q + cls + FormDesignerPanel.Extension + Q + ")");
            code.AppendLine();
            code.AppendLine("// Нажатие кнопки идёт в блок def ниже");
            code.AppendLine("main.On(" + Q + "button1" + Q + ", " + Q + "Нажали" + Q + ")");
            code.AppendLine();
            code.AppendLine("main.Show()");
            code.AppendLine();
            code.AppendLine("def Нажали(кто) {");
            code.AppendLine("    string имя = main.Get(" + Q + "textBox1" + Q + ")");
            code.AppendLine();
            code.AppendLine("    // Методы из другого файла DupoLang — через Lang.ExecuteFrom");
            code.AppendLine("    string ответ = Lang.ExecuteFrom(" + Q + "logic.dupolang" + Q + ", " +
                            Q + "Привет" + Q + ", имя)");
            code.AppendLine();
            code.AppendLine("    main.Set(" + Q + "label1" + Q + ", ответ)");
            code.AppendLine("}");
            Save(dir, "program.dupolang", code.ToString());

            // main.dupolang просто запускает program.dupolang — так F5 работает как везде.
            var starter = new StringBuilder();
            starter.AppendLine("// " + name + " — запуск окна");
            starter.AppendLine("using Dupo.Lang");
            starter.AppendLine();
            starter.AppendLine("Lang.Run(" + Q + "program.dupolang" + Q + ")");
            Save(dir, "main.dupolang", starter.ToString());

            // Ресурсы лежат в assets
            Save(dir, Path.Combine("assets", "readme.txt"),
                 "Сюда кладутся картинки и прочие файлы приложения.\r\n" +
                 "Из кода они видны как Resources.имя — подсказка их знает.\r\n");
        }

        static void CsLibrary(string dir, string name)        static void CsLibrary(string dir, string name)
        {
            string ns = Ident(name);

            var cs = new StringBuilder();
            cs.AppendLine("using System;");
            cs.AppendLine();
            cs.AppendLine("namespace " + ns);
            cs.AppendLine("{");
            cs.AppendLine("    public static class Greeter");
            cs.AppendLine("    {");
            cs.AppendLine("        public static string Hello(string who)");
            cs.AppendLine("        {");
            cs.AppendLine("            return \"Привет, \" + who + \"!\";");
            cs.AppendLine("        }");
            cs.AppendLine("    }");
            cs.AppendLine("}");
            Save(dir, Path.Combine("src", "Greeter.cs"), cs.ToString());

            var code = new StringBuilder();
            code.AppendLine("// " + name + " — библиотека классов C#");
            code.AppendLine("using Dupo.CSharp");
            code.AppendLine("using Dupo.System.Files");
            code.AppendLine();
            code.AppendLine("print(" + Q + "Компилятор: " + Q + " + CSharp.Version)");
            code.AppendLine();
            code.AppendLine("string dll = CSharp.Library(" + Q + "src" + Q + ", " + Q + "build/" + name + ".dll" + Q + ")");
            code.AppendLine();
            code.AppendLine("print(" + Q + Q + ")");
            code.AppendLine("print(" + Q + "Готово: " + Q + " + dll)");
            code.AppendLine("print(" + Q + "Размер: " + Q + " + Files.Size(dll) + " + Q + " байт" + Q + ")");
            Save(dir, "main.dupolang", code.ToString());
        }

        /// <summary>Скрипт сборки C#: Build для консоли, Forms для окна.</summary>
        static string CsScript(string name, string method, string what)
        {
            var code = new StringBuilder();
            code.AppendLine("// " + name + " — " + what + " на C# (.NET Framework 4.8)");
            code.AppendLine("using Dupo.CSharp");
            code.AppendLine();
            code.AppendLine("print(" + Q + "Компилятор: " + Q + " + CSharp.Version)");
            code.AppendLine();
            code.AppendLine("if (CSharp.IsInstalled == false) {");
            code.AppendLine("    print(" + Q + "csc.exe не найден — поставьте .NET Framework 4.8" + Q + ")");
            code.AppendLine("} else {");
            code.AppendLine("    string exe = CSharp." + method + "(" + Q + "src" + Q + ", " +
                            Q + "build/" + name + ".exe" + Q + ")");
            code.AppendLine("    print(" + Q + Q + ")");
            code.AppendLine("    CSharp.Launch(exe, " + Q + Q + ")");
            code.AppendLine("}");
            return code.ToString();
        }

        // ------------------------------------------------------------------
        // C++
        // ------------------------------------------------------------------

        static void CppConsole(string dir, string name)
        {
            var cpp = new StringBuilder();
            cpp.AppendLine("#include <iostream>");
            cpp.AppendLine("#include <string>");
            cpp.AppendLine("#include <windows.h>");
            cpp.AppendLine();
            cpp.AppendLine("int main()");
            cpp.AppendLine("{");
            cpp.AppendLine("    SetConsoleOutputCP(CP_UTF8);");
            cpp.AppendLine();
            cpp.AppendLine("    std::cout << \"Привет из " + name + "!\" << std::endl;");
            cpp.AppendLine();
            cpp.AppendLine("    for (int i = 1; i <= 5; ++i)");
            cpp.AppendLine("        std::cout << \"  строка \" << i << std::endl;");
            cpp.AppendLine();
            cpp.AppendLine("    return 0;");
            cpp.AppendLine("}");
            Save(dir, Path.Combine("src", "main.cpp"), cpp.ToString());

            Save(dir, "main.dupolang", CppScript(name, "Build", "консольное приложение"));
        }

        static void CppWindow(string dir, string name)
        {
            var cpp = new StringBuilder();
            cpp.AppendLine("#include <windows.h>");
            cpp.AppendLine();
            cpp.AppendLine("LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)");
            cpp.AppendLine("{");
            cpp.AppendLine("    switch (msg)");
            cpp.AppendLine("    {");
            cpp.AppendLine("    case WM_PAINT:");
            cpp.AppendLine("    {");
            cpp.AppendLine("        PAINTSTRUCT ps;");
            cpp.AppendLine("        HDC dc = BeginPaint(hwnd, &ps);");
            cpp.AppendLine("        RECT r; GetClientRect(hwnd, &r);");
            cpp.AppendLine("        SetBkMode(dc, TRANSPARENT);");
            cpp.AppendLine("        DrawTextW(dc, L\"Привет из " + name + "!\", -1, &r,");
            cpp.AppendLine("                  DT_CENTER | DT_VCENTER | DT_SINGLELINE);");
            cpp.AppendLine("        EndPaint(hwnd, &ps);");
            cpp.AppendLine("        return 0;");
            cpp.AppendLine("    }");
            cpp.AppendLine("    case WM_DESTROY:");
            cpp.AppendLine("        PostQuitMessage(0);");
            cpp.AppendLine("        return 0;");
            cpp.AppendLine("    }");
            cpp.AppendLine("    return DefWindowProcW(hwnd, msg, wp, lp);");
            cpp.AppendLine("}");
            cpp.AppendLine();
            cpp.AppendLine("int WINAPI wWinMain(HINSTANCE inst, HINSTANCE, LPWSTR, int show)");
            cpp.AppendLine("{");
            cpp.AppendLine("    const wchar_t* cls = L\"" + Ident(name) + "Window\";");
            cpp.AppendLine();
            cpp.AppendLine("    WNDCLASSW wc = {};");
            cpp.AppendLine("    wc.lpfnWndProc = WndProc;");
            cpp.AppendLine("    wc.hInstance = inst;");
            cpp.AppendLine("    wc.lpszClassName = cls;");
            cpp.AppendLine("    wc.hCursor = LoadCursor(nullptr, IDC_ARROW);");
            cpp.AppendLine("    wc.hbrBackground = (HBRUSH)(COLOR_WINDOW + 1);");
            cpp.AppendLine("    RegisterClassW(&wc);");
            cpp.AppendLine();
            cpp.AppendLine("    HWND hwnd = CreateWindowExW(0, cls, L\"" + name + "\",");
            cpp.AppendLine("        WS_OVERLAPPEDWINDOW, CW_USEDEFAULT, CW_USEDEFAULT, 460, 260,");
            cpp.AppendLine("        nullptr, nullptr, inst, nullptr);");
            cpp.AppendLine();
            cpp.AppendLine("    ShowWindow(hwnd, show);");
            cpp.AppendLine();
            cpp.AppendLine("    MSG msg;");
            cpp.AppendLine("    while (GetMessageW(&msg, nullptr, 0, 0))");
            cpp.AppendLine("    {");
            cpp.AppendLine("        TranslateMessage(&msg);");
            cpp.AppendLine("        DispatchMessageW(&msg);");
            cpp.AppendLine("    }");
            cpp.AppendLine("    return 0;");
            cpp.AppendLine("}");
            Save(dir, Path.Combine("src", "main.cpp"), cpp.ToString());

            Save(dir, "main.dupolang", CppScript(name, "Window", "приложение с окном"));
        }

        static string CppScript(string name, string method, string what)
        {
            var code = new StringBuilder();
            code.AppendLine("// " + name + " — " + what + " на C++");
            code.AppendLine("using Dupo.Cpp");
            code.AppendLine();
            code.AppendLine("if (Cpp.IsInstalled == false) {");
            code.AppendLine("    print(" + Q + "Компилятора нет — скачиваю MinGW-w64..." + Q + ")");
            code.AppendLine("    Cpp.Install()");
            code.AppendLine("}");
            code.AppendLine();
            code.AppendLine("print(" + Q + "Компилятор: " + Q + " + Cpp.Version)");
            code.AppendLine();
            code.AppendLine("string exe = Cpp." + method + "(" + Q + "src" + Q + ", " +
                            Q + "build/" + name + ".exe" + Q + ")");
            code.AppendLine();
            if (method == "Build")
            {
                code.AppendLine("print(" + Q + Q + ")");
                code.AppendLine("Cpp.Launch(exe, " + Q + Q + ")");
            }
            else
            {
                code.AppendLine("print(" + Q + "Готово: " + Q + " + exe)");
            }
            return code.ToString();
        }

        // ------------------------------------------------------------------
        // Java
        // ------------------------------------------------------------------

        static void Java(string dir, string name)
        {
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
            Save(dir, "main.dupolang", code.ToString());

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
            Save(dir, Path.Combine("src", "Main.java"), java.ToString());
        }

        // ------------------------------------------------------------------
        // Установщик
        // ------------------------------------------------------------------

        static void Setup(string dir, string name)
        {
            string app = name.EndsWith("Setup", StringComparison.OrdinalIgnoreCase)
                ? name.Substring(0, name.Length - 5) : name;
            if (app.Length == 0) app = name;

            var code = new StringBuilder();
            code.AppendLine("// " + name + " — установщик приложения " + app + ".");
            code.AppendLine("// Положите файлы приложения в папку payload и соберите проект");
            code.AppendLine("// в один .exe (Запуск → Собрать в один .exe).");
            code.AppendLine("// Полученный exe и есть установщик.");
            code.AppendLine("using Dupo.System.Files");
            code.AppendLine();
            code.AppendLine("string app = " + Q + app + Q);
            code.AppendLine("string exe = " + Q + app + ".exe" + Q + "   // что запускать после установки");
            code.AppendLine();
            code.AppendLine("string target = Files.Join(Files.AppData(), app)");
            code.AppendLine();
            code.AppendLine("print(" + Q + "=== Установка " + Q + " + app + " + Q + " ===" + Q + ")");
            code.AppendLine("print(" + Q + "Куда: " + Q + " + target)");
            code.AppendLine("print(" + Q + Q + ")");
            code.AppendLine();
            code.AppendLine("if (Files.DirExists(" + Q + "payload" + Q + ") == false) {");
            code.AppendLine("    print(" + Q + "Папки payload нет — класть нечего." + Q + ")");
            code.AppendLine("} else {");
            code.AppendLine("    Files.CopyDir(" + Q + "payload" + Q + ", target)");
            code.AppendLine("    print(" + Q + "Файлы скопированы: " + Q + " + Files.Count(target))");
            code.AppendLine();
            code.AppendLine("    string main = Files.Join(target, exe)");
            code.AppendLine("    if (Files.Exists(main)) {");
            code.AppendLine("        Files.Shortcut(main, Files.Join(Files.Desktop(), app))");
            code.AppendLine("        Files.Shortcut(main, Files.Join(Files.StartMenu(), app))");
            code.AppendLine("    } else {");
            code.AppendLine("        print(" + Q + "Главный файл не найден: " + Q + " + main)");
            code.AppendLine("    }");
            code.AppendLine();
            code.AppendLine("    print(" + Q + Q + ")");
            code.AppendLine("    print(" + Q + "Готово. Ярлыки на рабочем столе и в меню «Пуск»." + Q + ")");
            code.AppendLine("}");
            Save(dir, "main.dupolang", code.ToString());

            Save(dir, Path.Combine("payload", "README.txt"),
                "Сюда кладутся файлы приложения " + app + ".\r\n\r\n" +
                "Всё из этой папки попадёт в установщик и будет скопировано\r\n" +
                "пользователю в %LOCALAPPDATA%\\" + app + ".\r\n\r\n" +
                "Главный файл по умолчанию — " + app + ".exe; имя меняется\r\n" +
                "в первых строках main.dupolang.\r\n");
        }

        // ------------------------------------------------------------------
        // старые точки входа
        // ------------------------------------------------------------------

        /// <summary>Сайт на DupoLang без диалога шаблонов.</summary>
        public static string CreateWeb(IWin32Window owner)
        {
            return Quick(owner, "dupolang-web", "Новый проект");
        }

        /// <summary>Проект на Java без диалога шаблонов.</summary>
        public static string CreateJava(IWin32Window owner)
        {
            return Quick(owner, "java", "Новый Java-проект");
        }

        static string Quick(IWin32Window owner, string kind, string title)
        {
            ProjectTemplate t = null;
            foreach (var x in NewProjectForm.All)
                if (x.Kind == kind) { t = x; break; }
            if (t == null) return null;

            string name = PromptDialog.Show(owner, title, "Название проекта:", t.DefaultName);
            if (string.IsNullOrEmpty(name)) return null;

            if (Directory.Exists(Path.Combine(ProjectsDir, name)))
            {
                MessageBox.Show(owner, "Такой проект уже есть.", "Dupo Studio");
                return null;
            }

            return Create(owner, t, name, ProjectsDir);
        }

        /// <summary>Выбор существующей папки проекта.</summary>
        public static string Choose(IWin32Window owner, string startDir)
        {
            return Folders.Ask(owner, "Выберите папку проекта",
                               Directory.Exists(startDir) ? startDir : ProjectsDir);
        }
    }
}
