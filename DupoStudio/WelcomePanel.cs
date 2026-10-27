using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DupoStudio
{
    /// <summary>Стартовый экран — виден, пока не открыт ни один проект.</summary>
    public class WelcomePanel : Panel
    {
        public event EventHandler NewProject;
        public event EventHandler NewJavaProject;
        public event EventHandler OpenProject;
        public event EventHandler<string> OpenRecent;

        const int CardWidth = 560;
        const int RowHeight = 60;
        const int RowGap = 12;

        readonly Panel card = new Panel();
        readonly Panel recent = new Panel();
        readonly Label hint = new Label();

        int rowsBottom;

        public WelcomePanel()
        {
            Dock = DockStyle.Fill;
            AutoScroll = true;
            BackColor = Theme.Back;

            card.Width = CardWidth;
            card.BackColor = Theme.Back;
            Controls.Add(card);

            int y = 0;

            var title = new Label
            {
                Text = "Dupo Studio",
                Font = new Font("Segoe UI", 28f, FontStyle.Bold),
                ForeColor = Theme.Text,
                AutoSize = true,
                Location = new Point(0, y)
            };
            card.Controls.Add(title);
            y += 52;

            var sub = new Label
            {
                Text = "Конструктор приложений на DupoLang",
                Font = new Font("Segoe UI", 11f),
                ForeColor = Theme.Dim,
                AutoSize = true,
                Location = new Point(3, y)
            };
            card.Controls.Add(sub);
            y += 44;

            card.Controls.Add(Row("Новый проект",
                "любой шаблон: DupoLang, C#, C++, Java, установщик",
                y, true, delegate { Fire(NewProject); }));
            y += RowHeight + RowGap;

            card.Controls.Add(Row("Новый Java-проект",
                "исходники на Java, сборка в .jar одной кнопкой",
                y, false, delegate { Fire(NewJavaProject); }));
            y += RowHeight + RowGap;

            card.Controls.Add(Row("Открыть проект",
                "выбрать папку на диске",
                y, false, delegate { Fire(OpenProject); }));
            y += RowHeight + 18;

            // Подпись идёт строго под кнопками: раньше она попадала на третью
            // кнопку и пряталась за ней.
            hint.Text = "F5 — запустить   ·   Ctrl+B — собрать в один .exe   ·   F7 — конструктор форм";
            hint.Font = new Font("Segoe UI", 8.5f);
            hint.ForeColor = Theme.Dim;
            hint.AutoSize = true;
            hint.Location = new Point(3, y);
            card.Controls.Add(hint);
            y += 28;

            recent.Location = new Point(2, y);
            recent.Size = new Size(CardWidth - 4, 0);
            recent.BackColor = Theme.Back;
            card.Controls.Add(recent);

            rowsBottom = y;
            card.Height = y;

            // Текст должен быть поверх плиток, а не под ними: в WinForms
            // то, что добавлено позже, уходит назад.
            title.BringToFront();
            sub.BringToFront();
            hint.BringToFront();
            recent.BringToFront();
        }

        void Fire(EventHandler h) { if (h != null) h(this, EventArgs.Empty); }

        Control Row(string text, string note, int top, bool accent, EventHandler click)
        {
            var host = new Panel
            {
                Location = new Point(0, top),
                Size = new Size(CardWidth - 4, RowHeight),
                BackColor = accent ? Theme.Accent : Theme.Panel,
                Cursor = Cursors.Hand
            };

            var caption = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = accent ? Color.White : Theme.Text,
                AutoSize = true,
                Location = new Point(18, 9),
                Cursor = Cursors.Hand
            };

            var note2 = new Label
            {
                Text = note,
                Font = new Font("Segoe UI", 9f),
                ForeColor = accent ? Color.FromArgb(228, 240, 255) : Theme.Dim,
                AutoSize = true,
                Location = new Point(20, 34),
                Cursor = Cursors.Hand
            };

            host.Controls.Add(caption);
            host.Controls.Add(note2);

            Color rest = host.BackColor;
            Color hot = Theme.Hover(rest);

            EventHandler enter = delegate { host.BackColor = hot; };
            EventHandler leave = delegate { host.BackColor = rest; };

            foreach (Control c in new Control[] { host, caption, note2 })
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
            recent.Height = 0;

            string[] dirs = Recent(projectsDir);
            if (dirs.Length > 0)
            {
                var caption = new Label
                {
                    Text = "НЕДАВНИЕ",
                    Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                    ForeColor = Theme.Dim,
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
                    y += 22;
                }

                recent.Height = y + 4;
            }

            // Высота карточки зависит от списка, иначе пустое место внизу
            // съезжает и экран перестаёт быть по центру.
            card.Height = rowsBottom + recent.Height;
            Center();
        }

        static string[] Recent(string projectsDir)
        {
            if (string.IsNullOrEmpty(projectsDir) || !Directory.Exists(projectsDir))
                return new string[0];

            try
            {
                return Directory.GetDirectories(projectsDir)
                                .OrderByDescending(Directory.GetLastWriteTime)
                                .Take(5)
                                .ToArray();
            }
            catch { return new string[0]; }
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
                Math.Max(20, (ClientSize.Width - card.Width) / 2),
                Math.Max(16, (ClientSize.Height - card.Height) / 2));
        }
    }
}
