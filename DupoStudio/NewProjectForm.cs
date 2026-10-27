using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DupoStudio
{
    /// <summary>Один шаблон проекта.</summary>
    public class ProjectTemplate
    {
        public string Kind;
        public string Title;
        public string Category;
        public string Language;
        public string Description;
        public string DefaultName;

        public ProjectTemplate(string kind, string category, string language,
                               string title, string defaultName, string description)
        {
            Kind = kind;
            Category = category;
            Language = language;
            Title = title;
            DefaultName = defaultName;
            Description = description;
        }

        public override string ToString() { return Title; }
    }

    /// <summary>
    /// Выбор шаблона нового проекта — слева разделы, в середине шаблоны,
    /// справа описание, снизу имя и расположение.
    /// </summary>
    public class NewProjectForm : Form
    {
        public static readonly ProjectTemplate[] All =
        {
            new ProjectTemplate("dupolang-web", "DupoLang", "DupoLang",
                "Веб-приложение", "MyApp",
                "Сайт на DupoLang: Web.Server раздаёт папку www, главный файл ищется " +
                "как в nginx — index.html, index.php или index.js. PHP ставится сам."),

            new ProjectTemplate("dupolang-console", "DupoLang", "DupoLang",
                "Консольное приложение", "MyConsole",
                "Пустой проект на DupoLang: один main.dupolang с выводом в консоль. " +
                "Отсюда удобно начинать что угодно."),

            new ProjectTemplate("dupolang-service", "DupoLang", "DupoLang",
                "Фоновый сервис", "MyService",
                "Сервис на Dupo.Services: регистрация, запуск и остановка. " +
                "Тип сервиса выбирается через Services.ServiceLevel."),

            new ProjectTemplate("csharp-console", "C#", "C#",
                "Консольное приложение (.NET 4.8)", "MyCsApp",
                "Классический C#: исходники в src, сборка через csc.exe из .NET Framework 4.8. " +
                "Ставить ничего не нужно — компилятор уже в системе."),

            new ProjectTemplate("csharp-forms", "C#", "C#",
                "Windows Forms (.NET 4.8)", "MyFormsApp",
                "Приложение с окном на WinForms: Form1 с кнопкой, сборка в winexe, " +
                "то есть без чёрного окна консоли."),

            new ProjectTemplate("csharp-library", "C#", "C#",
                "Библиотека классов (.dll)", "MyLibrary",
                "Библиотека на C#: собирается в .dll, которую потом подключают " +
                "к другим проектам через ссылки."),

            new ProjectTemplate("cpp-console", "C++", "C++",
                "Консольное приложение", "MyCppApp",
                "C++17, сборка g++ со статической линковкой — получается один .exe " +
                "без лишних dll. Компилятор ставится командой Cpp.Install()."),

            new ProjectTemplate("cpp-window", "C++", "C++",
                "Приложение с окном (WinAPI)", "MyCppWindow",
                "Окно на чистом WinAPI: CreateWindowEx и цикл сообщений, " +
                "сборка с -mwindows, без консоли."),

            new ProjectTemplate("java", "Java", "Java",
                "Приложение Java", "MyJavaApp",
                "Java: исходники в src, компиляция и сборка .jar. " +
                "JDK (Temurin) скачивается сам при первом запуске."),

            new ProjectTemplate("setup", "Установщик", "DupoLang",
                "Установщик приложения", "MySetup",
                "Проект-установщик: кладёте файлы в папку payload, а собранный .exe " +
                "ставит их пользователю, делает ярлыки и запись в «Программы и компоненты»."),
        };

        readonly ListBox categories = new ListBox();
        readonly ListBox templates = new ListBox();
        readonly Label title = new Label();
        readonly Label about = new Label();
        readonly TextBox nameBox = new TextBox();
        readonly TextBox locationBox = new TextBox();
        readonly Button browse = new Button();
        readonly Button ok = new Button();
        readonly Button cancel = new Button();

        /// <summary>Выбранный шаблон.</summary>
        public ProjectTemplate Template { get; private set; }

        /// <summary>Имя проекта.</summary>
        public string ProjectName { get { return nameBox.Text.Trim(); } }

        /// <summary>Папка, где появится проект.</summary>
        public string Location { get { return locationBox.Text.Trim(); } }

        /// <summary>Итоговая папка проекта.</summary>
        public string TargetDir { get { return Path.Combine(Location, ProjectName); } }

        public NewProjectForm(string location)
        {
            Text = "Новый проект";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(880, 540);
            MinimumSize = new Size(760, 480);
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9f);

            BuildLayout(location);
            FillCategories();

            categories.SelectedIndexChanged += delegate { FillTemplates(); };
            templates.SelectedIndexChanged += delegate { ShowTemplate(); };
            templates.DoubleClick += delegate { if (ok.Enabled) Accept(); };
            nameBox.TextChanged += delegate { UpdateOk(); };
            browse.Click += delegate { Browse(); };
            ok.Click += delegate { Accept(); };

            categories.SelectedIndex = 0;
        }

        // ------------------------------------------------------------------
        // внешний вид
        // ------------------------------------------------------------------

        void BuildLayout(string location)
        {
            var head = new Label();
            head.Text = "Выберите шаблон";
            head.Font = new Font("Segoe UI Semibold", 13f);
            head.ForeColor = Theme.Text;
            head.Bounds = new Rectangle(18, 14, 400, 28);
            Controls.Add(head);

            Style(categories);
            categories.Bounds = new Rectangle(18, 50, 190, 360);
            categories.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom;
            Controls.Add(categories);

            Style(templates);
            templates.Bounds = new Rectangle(220, 50, 330, 360);
            templates.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom;
            templates.ItemHeight = 22;
            Controls.Add(templates);

            var side = new Panel();
            side.Bounds = new Rectangle(562, 50, 298, 360);
            side.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            side.BackColor = Theme.Panel;
            side.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(side);

            title.Bounds = new Rectangle(14, 14, 270, 44);
            title.Font = new Font("Segoe UI Semibold", 11f);
            title.ForeColor = Theme.Text;
            title.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            side.Controls.Add(title);

            about.Bounds = new Rectangle(14, 64, 270, 280);
            about.ForeColor = Theme.Dim;
            about.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            side.Controls.Add(about);

            var lblName = Caption("Имя проекта:", 18, 428);
            Controls.Add(lblName);

            nameBox.Bounds = new Rectangle(130, 425, 300, 24);
            nameBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            Style(nameBox);
            Controls.Add(nameBox);

            var lblWhere = Caption("Расположение:", 18, 464);
            Controls.Add(lblWhere);

            locationBox.Bounds = new Rectangle(130, 461, 620, 24);
            locationBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            locationBox.Text = location;
            Style(locationBox);
            Controls.Add(locationBox);

            browse.Text = "Обзор...";
            browse.Bounds = new Rectangle(760, 460, 100, 26);
            browse.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            Style(browse);
            Controls.Add(browse);

            ok.Text = "Создать";
            ok.Bounds = new Rectangle(650, 500, 100, 28);
            ok.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            Style(ok);
            ok.BackColor = Theme.Accent;
            ok.ForeColor = Color.White;
            Controls.Add(ok);

            cancel.Text = "Отмена";
            cancel.Bounds = new Rectangle(760, 500, 100, 28);
            cancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            cancel.DialogResult = DialogResult.Cancel;
            Style(cancel);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        Label Caption(string text, int x, int y)
        {
            var l = new Label();
            l.Text = text;
            l.ForeColor = Theme.Dim;
            l.Bounds = new Rectangle(x, y + 3, 110, 20);
            l.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            return l;
        }

        static void Style(Control c)
        {
            c.BackColor = Theme.Panel;
            c.ForeColor = Theme.Text;

            var lb = c as ListBox;
            if (lb != null) { lb.BorderStyle = BorderStyle.FixedSingle; return; }

            var tb = c as TextBox;
            if (tb != null) { tb.BorderStyle = BorderStyle.FixedSingle; return; }

            var b = c as Button;
            if (b != null)
            {
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderColor = Theme.Border;
                b.BackColor = Theme.Chrome;
            }
        }

        // ------------------------------------------------------------------
        // данные
        // ------------------------------------------------------------------

        void FillCategories()
        {
            categories.Items.Add("Все шаблоны");
            foreach (string c in All.Select(t => t.Category).Distinct())
                categories.Items.Add(c);
        }

        void FillTemplates()
        {
            string cat = categories.SelectedItem as string;
            bool all = categories.SelectedIndex <= 0;

            templates.BeginUpdate();
            templates.Items.Clear();
            foreach (var t in All)
                if (all || string.Equals(t.Category, cat, StringComparison.OrdinalIgnoreCase))
                    templates.Items.Add(t);
            templates.EndUpdate();

            if (templates.Items.Count > 0) templates.SelectedIndex = 0;
            else ShowTemplate();
        }

        void ShowTemplate()
        {
            Template = templates.SelectedItem as ProjectTemplate;

            if (Template == null)
            {
                title.Text = "";
                about.Text = "";
                UpdateOk();
                return;
            }

            title.Text = Template.Title;
            about.Text = Template.Description +
                         Environment.NewLine + Environment.NewLine +
                         "Язык: " + Template.Language +
                         Environment.NewLine +
                         "Раздел: " + Template.Category;

            if (nameBox.Text.Trim().Length == 0 || IsDefaultName(nameBox.Text.Trim()))
                nameBox.Text = Unique(Template.DefaultName);

            UpdateOk();
        }

        static bool IsDefaultName(string name)
        {
            return All.Any(t => name.StartsWith(t.DefaultName, StringComparison.OrdinalIgnoreCase));
        }

        string Unique(string baseName)
        {
            string where = Location;
            if (!Directory.Exists(where)) return baseName;

            if (!Directory.Exists(Path.Combine(where, baseName))) return baseName;
            for (int i = 1; i < 500; i++)
            {
                string n = baseName + i;
                if (!Directory.Exists(Path.Combine(where, n))) return n;
            }
            return baseName;
        }

        void UpdateOk()
        {
            string n = ProjectName;
            bool good = Template != null && n.Length > 0 &&
                        n.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
            ok.Enabled = good;
        }

        void Browse()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Где создать проект";
                if (Directory.Exists(Location)) dlg.SelectedPath = Location;
                if (dlg.ShowDialog(this) == DialogResult.OK) locationBox.Text = dlg.SelectedPath;
            }
        }

        void Accept()
        {
            if (Template == null) return;

            string n = ProjectName;
            if (n.Length == 0 || n.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageBox.Show(this, "Имя проекта не годится.", "Dupo Studio");
                return;
            }

            if (Location.Length == 0)
            {
                MessageBox.Show(this, "Укажите, где создать проект.", "Dupo Studio");
                return;
            }

            if (Directory.Exists(TargetDir))
            {
                MessageBox.Show(this, "Такая папка уже есть:" + Environment.NewLine + TargetDir,
                                "Dupo Studio");
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>Показать диалог. Возвращает папку созданного проекта или null.</summary>
        public static string Ask(IWin32Window owner, string location)
        {
            using (var dlg = new NewProjectForm(location))
            {
                if (dlg.ShowDialog(owner) != DialogResult.OK) return null;
                return ProjectTemplates.Create(owner, dlg.Template, dlg.ProjectName, dlg.Location);
            }
        }
    }
}
