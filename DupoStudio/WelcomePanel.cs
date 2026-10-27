using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DupoStudio
{
    /// <summary>Стартовый экран — виден, пока не открыт ни один файл.</summary>
    public class WelcomePanel : Panel
    {
        public event EventHandler NewProject;
        public event EventHandler NewJavaProject;
        public event EventHandler OpenProject;
        public event EventHandler<string> OpenRecent;

        readonly Panel card = new Panel();
        readonly Panel recent = new Panel();

        public WelcomePanel()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Back;

            card.Size = new Size(560, 430);
            card.BackColor = Theme.Back;
            Controls.Add(card);

            var title = new Label
            {
                Text = "Dupo Studio",
                Font = new Font("Segoe UI", 28f, FontStyle.Bold),
                ForeColor = Theme.Text,
                AutoSize = true,
                Location = new Point(0, 0)
            };

            var sub = new Label
            {
                Text = "Конструктор приложений на DupoLang",
                Font = new Font("Segoe UI", 11f),
                ForeColor = Theme.Dim,
                AutoSize = true,
                Location = new Point(3, 52)
            };

            card.Controls.Add(title);
            card.Controls.Add(sub);

            card.Controls.Add(Button("Новый проект",
                "сайт на DupoLang, сервер и PHP ставятся сами",
                100, true, delegate { Fire(NewProject); }));

            card.Controls.Add(Button("Новый Java-проект",
                "исходники на Java, сборка в .jar одной кнопкой",
                172, false, delegate { Fire(NewJavaProject); }));

            card.Controls.Add(Button("Открыть проект",
                "выбрать папку на диске",
                244, false, delegate { Fire(OpenProject); }));

            recent.Location = new Point(2, 320);
            recent.Size = new Size(556, 110);
            recent.BackColor = Theme.Back;
            card.Controls.Add(recent);

            var hint = new Label
            {
                Text = "F5 — запустить   ·   Ctrl+B — собрать в один .exe   ·   всё это есть в меню «Файл»",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(110, 118, 132),
                AutoSize = true,
                Location = new Point(3, 296)
            };
            card.Controls.Add(hint);
        }

        void Fire(EventHandler h) { if (h != null) h(this, EventArgs.Empty); }

        Control Button(string text, string note, int top, bool accent, EventHandler click)
        {
            var host = new Panel
            {
                Location = new Point(0, top),
                Size = new Size(556, 60),
                BackColor = accent ? Theme.Accent : Theme.Panel,
                Cursor = Cursors.Hand
            };

            var caption = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = accent ? Color.White : Theme.Text,
                AutoSize = true,
                Location = new Point(18, 10),
                Cursor = Cursors.Hand
            };

            var hint = new Label
            {
                Text = note,
                Font = new Font("Segoe UI", 9f),
                ForeColor = accent ? Color.FromArgb(225, 238, 255) : Theme.Dim,
                AutoSize = true,
                Location = new Point(20, 34),
                Cursor = Cursors.Hand
            };

            host.Controls.Add(caption);
            host.Controls.Add(hint);

            Color rest = host.BackColor;
            Color hot = accent
                ? Color.FromArgb(70, 148, 255)
                : Color.FromArgb(52, 58, 70);

            EventHandler enter = delegate { host.BackColor = hot; };
            EventHandler leave = delegate { host.BackColor = rest; };

            foreach (Control c in new Control[] { host, caption, hint })
            {
                c.Click += click;
                c.MouseEnter += enter;
                c.MouseLeave += leave;
            }

            return host;
        }

        /// <summary>Показывает последние проекты из папки Projects.</summary>
        public void LoadRecent(string projectsDir)
        {
            recent.Controls.Clear();

            if (string.IsNullOrEmpty(projectsDir) || !Directory.Exists(projectsDir)) return;

            string[] dirs;
            try
            {
                dirs = Directory.GetDirectories(projectsDir)
                                .OrderByDescending(d => Directory.GetLastWriteTime(d))
                                .Take(4).ToArray();
            }
            catch { return; }

            if (dirs.Length == 0) return;

            var caption = new Label
            {
                Text = "НЕДАВНИЕ",
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(110, 118, 132),
                AutoSize = true,
                Location = new Point(1, 0)
            };
            recent.Controls.Add(caption);

            int y = 22;
            foreach (string dir in dirs)
            {
                string path = dir;

                var link = new LinkLabel
                {
                    Text = Path.GetFileName(dir),
                    Font = new Font("Segoe UI", 10f),
                    LinkColor = Theme.Accent2,
                    ActiveLinkColor = Theme.Text,
                    VisitedLinkColor = Theme.Accent2,
                    LinkBehavior = LinkBehavior.HoverUnderline,
                    AutoSize = true,
                    Location = new Point(1, y)
                };

                link.Click += delegate
                {
                    var h = OpenRecent;
                    if (h != null) h(this, path);
                };

                recent.Controls.Add(link);
                y += 21;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Center();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) Center();
        }

        void Center()
        {
            card.Location = new Point(
                Math.Max(24, (ClientSize.Width - card.Width) / 2),
                Math.Max(16, (ClientSize.Height - card.Height) / 2));
        }
    }
}
