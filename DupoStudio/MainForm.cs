using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DupoLang;

namespace DupoStudio
{
    /// <summary>
    /// Стартовое окно. Показывается, когда автозапуск не настроен:
    /// отсюда создают проект или открывают существующий, а сама работа
    /// идёт уже в ProjectEditorForm.
    /// </summary>
    public class MainForm : Form
    {
        readonly WelcomePanel welcome = new WelcomePanel();
        readonly MenuStrip menu = new MenuStrip();

        ToolStripMenuItem miAutostartOff;

        public MainForm()
        {
            Text = "Dupo Studio";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(820, 620);
            MinimumSize = new Size(640, 520);
            BackColor = Theme.Back;
            ForeColor = Theme.Text;

            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildMenu();

            welcome.NewProject += delegate { Create(true); };
            welcome.NewJavaProject += delegate { Create(false); };
            welcome.OpenProject += delegate { Choose(); };
            welcome.OpenRecent += delegate (object s, string path) { OpenProject(path); };

            Controls.Add(welcome);
            Controls.Add(menu);
            MainMenuStrip = menu;

            Load += delegate { RefreshRecent(); };
        }

        void BuildMenu()
        {
            var file = new ToolStripMenuItem("Файл");
            var newWeb = new ToolStripMenuItem("Новый проект...");
            var newJava = new ToolStripMenuItem("Новый Java-проект...");
            var open = new ToolStripMenuItem("Открыть проект...");
            miAutostartOff = new ToolStripMenuItem("Убрать автозапуск");
            var exit = new ToolStripMenuItem("Выход");

            newWeb.ShortcutKeys = Keys.Control | Keys.N;
            newJava.ShortcutKeys = Keys.Control | Keys.Shift | Keys.N;
            open.ShortcutKeys = Keys.Control | Keys.O;

            newWeb.Click += delegate { Create(true); };
            newJava.Click += delegate { Create(false); };
            open.Click += delegate { Choose(); };
            miAutostartOff.Click += delegate
            {
                Settings.Autostart = "";
                Settings.DontAsk = false;
                Settings.Save();
                RefreshRecent();
            };
            exit.Click += delegate { Close(); };

            file.DropDownItems.AddRange(new ToolStripItem[] {
                newWeb, newJava, open, new ToolStripSeparator(),
                miAutostartOff, new ToolStripSeparator(), exit });

            var view = new ToolStripMenuItem("Вид");
            view.DropDownItems.Add(ThemeMenu.Build(this, delegate { Recolored(); }));

            var help = new ToolStripMenuItem("Справка");
            var about = new ToolStripMenuItem("О программе");
            about.Click += delegate
            {
                MessageBox.Show(this,
                    "Dupo Studio — App Maker\r\n" +
                    "Язык DupoLang " + Dupo.Version + "\r\n\r\n" +
                    "import Dupo.Web\r\n" +
                    "Web.Server s = new Web.Server(8080, \"www\", \"index\")\r\n" +
                    "s.Start()",
                    "О программе", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            help.DropDownItems.Add(about);

            menu.Items.AddRange(new ToolStripItem[] { file, view, help });
            menu.BackColor = Theme.Chrome;
            menu.ForeColor = Theme.Text;
            menu.Padding = new Padding(6, 2, 0, 2);
            menu.Font = new Font("Segoe UI", 9f);
            menu.Renderer = new ToolStripProfessionalRenderer(new DarkColors());
            Theme.PaintMenu(menu.Items);
            Theme.PaintBackground(this);
        }

        /// <summary>После смены темы подпись «недавние» строится заново.</summary>
        void Recolored()
        {
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            menu.BackColor = Theme.Chrome;
            menu.ForeColor = Theme.Text;
            welcome.BackColor = Theme.Back;
            Theme.PaintBackground(this);
            RefreshRecent();
        }

        void RefreshRecent()
        {
            welcome.LoadRecent(ProjectTemplates.ProjectsDir);
            miAutostartOff.Visible = !string.IsNullOrEmpty(Settings.Autostart);
        }

        void Create(bool web)
        {
            // «Новый проект» открывает выбор шаблона, Java-кнопка — сразу Java.
            string dir = web ? ProjectTemplates.New(this) : ProjectTemplates.CreateJava(this);
            if (dir != null) OpenProject(dir);
        }

        void Choose()
        {
            string dir = ProjectTemplates.Choose(this, ProjectTemplates.ProjectsDir);
            if (dir != null) OpenProject(dir);
        }

        /// <summary>Прячем лаунчер, работаем в окне проекта, по его закрытию возвращаемся.</summary>
        void OpenProject(string dir)
        {
            if (!Directory.Exists(dir))
            {
                MessageBox.Show(this, "Папка не найдена:\r\n" + dir, "Dupo Studio");
                RefreshRecent();
                return;
            }

            var editor = new ProjectEditorForm(dir);

            editor.FormClosed += delegate
            {
                Show();
                RefreshRecent();
            };

            Hide();
            editor.Show();
        }
    }
}
