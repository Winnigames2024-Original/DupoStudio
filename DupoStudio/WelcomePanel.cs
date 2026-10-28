using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DupoLang;

namespace DupoStudio
{
    /// <summary>
    /// Стартовый экран — виден, пока не открыт ни один проект.
    /// Слева действия, справа недавние проекты; всё растягивается
    /// по окну, без мёртвого места на большом мониторе.
    /// </summary>
    public class WelcomePanel : Panel
    {
        public event EventHandler NewProject;
        public event EventHandler NewJavaProject;
        public event EventHandler OpenProject;
        public event EventHandler<string> OpenRecent;

        const int Pad = 40;          // отступ от краёв
        const int Gap = 28;          // между колонками
        const int LeftWidth = 420;   // колонка действий
        const int RightMax = 760;    // шире список недавних не растягиваем
        const int TileHeight = 72;

        Panel left = new Panel();
        Panel right = new Panel();
        Soft recentBox = new Soft();
        Panel recentList = new Panel();
        Label recentEmpty = new Label();

        string projectsDir;

        public WelcomePanel()
        {
            Dock = DockStyle.Fill;
            AutoScroll = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);

            // Фон рисуем сами, значит и перерисовывать его при растягивании
            // окна тоже нам: иначе уклон и пятно остаются полосами.
            SetStyle(ControlStyles.ResizeRedraw, true);

            Paper();
            Build();
        }

        void Build()
        {
            BuildLeft();
            BuildRight();

            Controls.Add(left);
            Controls.Add(right);
        }

        /// <summary>
        /// Пересобрать экран новыми цветами. Шрифты и кисти разобраны по
        /// кусочкам, перекрасить их на месте нельзя — проще собрать заново.
        /// </summary>
        public void Restyle()
        {
            SuspendLayout();

            foreach (Control c in Controls.Cast<Control>().ToArray()) c.Dispose();
            Controls.Clear();

            left = new Panel();
            right = new Panel();
            recentBox = new Soft();
            recentList = new Panel();
            recentEmpty = new Label();

            Paper();
            Build();

            ResumeLayout(true);

            LoadRecent(projectsDir);
        }

        // ------------------------------------------------------------------
        // левая колонка: марка и действия
        // ------------------------------------------------------------------

        void BuildLeft()
        {
            left.BackColor = Color.Transparent;

            int y = 0;

            var mark = new Badge
            {
                Location = new Point(0, y),
                Size = new Size(56, 56)
            };
            left.Controls.Add(mark);

            var title = new Label
            {
                Text = "Dupo Studio",
                Font = new Font("Segoe UI Semibold", 23f),
                ForeColor = Theme.Text,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(70, y)
            };
            left.Controls.Add(title);

            // Подпись ставим под заголовок по его настоящей высоте: на другом
            // размере шрифта системы она иначе наезжает на название.
            var sub = new Label
            {
                Text = "Конструктор приложений · версия " + Dupo.Version,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Theme.Dim,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(72, y + title.Height - 2)
            };
            left.Controls.Add(sub);

            y += Math.Max(56, sub.Bottom - y) + 34;

            left.Controls.Add(Tile("Новый проект", "DupoLang, C#, C++, Python, Java, установщик",
                                   Glyph.Plus, y, true, delegate { Fire(NewProject); }));
            y += TileHeight + 10;

            left.Controls.Add(Tile("Новый Java-проект", "исходники на Java, сборка в .jar",
                                   Glyph.Java, y, false, delegate { Fire(NewJavaProject); }));
            y += TileHeight + 10;

            left.Controls.Add(Tile("Открыть проект", "выбрать папку на диске",
                                   Glyph.Folder, y, false, delegate { Fire(OpenProject); }));
            y += TileHeight + 26;

            foreach (var line in new[]
            {
                new[] { "F5", "запустить проект" },
                new[] { "Ctrl+B", "собрать в один .exe" },
                new[] { "F7", "конструктор форм" },
                new[] { "Ctrl+Space", "подсказки в коде" }
            })
            {
                left.Controls.Add(new Key
                {
                    Text = line[0],
                    Note = line[1],
                    Location = new Point(2, y),
                    Size = new Size(LeftWidth - 4, 24)
                });
                y += 26;
            }

            left.Height = y;
        }

        // ------------------------------------------------------------------
        // правая колонка: недавние
        // ------------------------------------------------------------------

        void BuildRight()
        {
            right.BackColor = Color.Transparent;

            recentBox.Location = new Point(0, 0);
            right.Controls.Add(recentBox);

            var caption = new Label
            {
                Text = "Недавние проекты",
                Font = new Font("Segoe UI Semibold", 11f),
                ForeColor = Theme.Text,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(20, 16)
            };
            recentBox.Controls.Add(caption);

            recentEmpty.Text = "Здесь появятся проекты, которые вы откроете.";
            recentEmpty.Font = new Font("Segoe UI", 9.5f);
            recentEmpty.ForeColor = Theme.Dim;
            recentEmpty.AutoSize = true;
            recentEmpty.BackColor = Color.Transparent;
            recentEmpty.Location = new Point(21, 48);
            recentBox.Controls.Add(recentEmpty);

            recentList.Location = new Point(10, 44);
            recentList.BackColor = Color.Transparent;
            recentBox.Controls.Add(recentList);
        }

        void Fire(EventHandler h) { if (h != null) h(this, EventArgs.Empty); }

        /// <summary>
        /// Если тема поставила картинку фона — пропускаем её сквозь себя,
        /// иначе рисуем свой фон сами.
        /// </summary>
        void Paper()
        {
            BackColor = Theme.Background != null ? Color.Transparent : Theme.Back;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);

            // Поверх картинки темы ничего не рисуем — она и так на месте.
            if (Theme.Background != null) return;

            var g = e.Graphics;
            var box = new Rectangle(0, 0, Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height));

            // Очень мягкий уклон: сверху чуть светлее, снизу — основной цвет.
            using (var slope = new LinearGradientBrush(box, Lift(Theme.Back, 7), Theme.Back, 90f))
                g.FillRectangle(slope, box);

            // И пятно акцентом в левом верхнем углу — оно и даёт глубину.
            int size = Math.Min(760, Math.Max(360, box.Width / 2));
            var spot = new Rectangle(-size / 3, -size / 2, size, size);

            using (var path = new GraphicsPath())
            {
                path.AddEllipse(spot);
                using (var glow = new PathGradientBrush(path))
                {
                    glow.CenterColor = Color.FromArgb(Theme.Dark ? 34 : 24, Theme.Accent);
                    glow.SurroundColors = new[] { Color.FromArgb(0, Theme.Accent) };

                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.FillPath(glow, path);
                }
            }
        }

        /// <summary>Поднять яркость цвета на несколько ступеней.</summary>
        static Color Lift(Color c, int by)
        {
            int d = Theme.Dark ? by : -by;
            return Color.FromArgb(
                Math.Max(0, Math.Min(255, c.R + d)),
                Math.Max(0, Math.Min(255, c.G + d)),
                Math.Max(0, Math.Min(255, c.B + d)));
        }

        // ------------------------------------------------------------------
        // недавние проекты
        // ------------------------------------------------------------------

        /// <summary>Показывает последние проекты из папки Projects.</summary>
        public void LoadRecent(string projectsDir)
        {
            this.projectsDir = projectsDir;

            foreach (Control c in recentList.Controls.Cast<Control>().ToArray()) c.Dispose();
            recentList.Controls.Clear();

            string[] dirs = Recent(projectsDir);
            recentEmpty.Visible = dirs.Length == 0;

            int y = 0;
            foreach (string dir in dirs)
            {
                string path = dir;

                var row = new Entry
                {
                    Caption = Path.GetFileName(dir),
                    Kind = KindOf(dir),
                    When = Ago(SafeTime(dir)),
                    Where = dir,
                    Location = new Point(0, y),
                    Size = new Size(Math.Max(240, recentList.Width), 56)
                };
                row.Click += delegate
                {
                    var h = OpenRecent;
                    if (h != null) h(this, path);
                };

                recentList.Controls.Add(row);
                y += 58;
            }

            recentList.Height = Math.Max(0, y - 2);
            Relayout();
        }

        static DateTime SafeTime(string dir)
        {
            try { return Directory.GetLastWriteTime(dir); }
            catch { return DateTime.Now; }
        }

        /// <summary>Что за проект — читаем описание из project.dupo.</summary>
        static string KindOf(string dir)
        {
            try
            {
                var s = ProjectSettings.Load(dir);
                if (s != null && !string.IsNullOrEmpty(s.Description)) return s.Description;
            }
            catch { }

            // Описания нет — смотрим, что лежит в папке.
            try
            {
                if (File.Exists(Path.Combine(dir, "program.dupolang"))) return "WinForms + DupoLang";
                if (Directory.Exists(Path.Combine(dir, "www"))) return "Веб-приложение";
                if (Directory.GetFiles(dir, "*.dupolang").Length > 0) return "DupoLang";
            }
            catch { }

            return "Проект";
        }

        /// <summary>«4 минуты назад», «вчера» — как в обычных редакторах.</summary>
        static string Ago(DateTime when)
        {
            TimeSpan d = DateTime.Now - when;

            if (d.TotalSeconds < 90) return "только что";
            if (d.TotalMinutes < 60) return Plural((int)d.TotalMinutes, "минуту", "минуты", "минут") + " назад";
            if (d.TotalHours < 24) return Plural((int)d.TotalHours, "час", "часа", "часов") + " назад";
            if (d.TotalDays < 2) return "вчера";
            if (d.TotalDays < 31) return Plural((int)d.TotalDays, "день", "дня", "дней") + " назад";

            return when.ToString("d MMMM yyyy");
        }

        static string Plural(int n, string one, string few, string many)
        {
            int h = n % 100, t = n % 10;
            string word = h >= 11 && h <= 14 ? many
                        : t == 1 ? one
                        : t >= 2 && t <= 4 ? few
                        : many;
            return n + " " + word;
        }

        static string[] Recent(string projectsDir)
        {
            if (string.IsNullOrEmpty(projectsDir) || !Directory.Exists(projectsDir))
                return new string[0];

            try
            {
                return Directory.GetDirectories(projectsDir)
                                .OrderByDescending(Directory.GetLastWriteTime)
                                .Take(6)
                                .ToArray();
            }
            catch { return new string[0]; }
        }

        // ------------------------------------------------------------------
        // раскладка
        // ------------------------------------------------------------------

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Relayout();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) Relayout();
        }

        void Relayout()
        {
            int w = ClientSize.Width;
            int h = ClientSize.Height;

            // Узкое окно — одна колонка, недавние уходят под действия.
            bool narrow = w < LeftWidth + Gap + 320 + Pad * 2;

            // Ширины считаем первыми: от них зависит высота правой колонки,
            // а от неё — куда ставить обе.
            left.Width = narrow ? Math.Max(260, w - Pad * 2) : LeftWidth;
            right.Width = narrow
                ? left.Width
                : Math.Min(RightMax, Math.Max(300, w - Pad * 2 - LeftWidth - Gap));

            recentBox.Width = right.Width;
            recentList.Width = recentBox.Width - 20;

            foreach (Control c in recentList.Controls)
                c.Width = recentList.Width;

            int boxHeight = recentList.Controls.Count > 0
                ? 44 + recentList.Height + 14
                : 44 + 34;

            recentBox.Height = boxHeight;
            right.Height = boxHeight;

            // Весь блок — по центру окна, а не прижат к верху: на большом
            // мониторе иначе остаётся полэкрана пустоты.
            int block = narrow
                ? left.Height + Gap + right.Height
                : Math.Max(left.Height, right.Height);

            int top = Math.Max(Pad, (h - block) / 2);

            // И по горизонтали тоже: на широком мониторе две колонки
            // расползались бы по краям, а читать их потом через всю ширину.
            int wide = narrow ? left.Width : left.Width + Gap + right.Width;
            int x = Math.Max(Pad, (w - wide) / 2);

            left.Location = new Point(x, top);
            right.Location = narrow
                ? new Point(x, top + left.Height + Gap)
                : new Point(x + left.Width + Gap, top);
        }

        // ------------------------------------------------------------------
        // плитки действий
        // ------------------------------------------------------------------

        Control Tile(string text, string note, Glyph glyph, int top, bool accent, EventHandler click)
        {
            var host = new Soft
            {
                Location = new Point(0, top),
                Size = new Size(LeftWidth - 4, TileHeight),
                Fill = accent ? Theme.Accent : Theme.Panel,
                Edge = accent ? Theme.Accent : Theme.Border,
                Cursor = Cursors.Hand,
                Mark = glyph,
                MarkColor = accent ? Color.White : Theme.Accent2
            };

            var caption = new Label
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 11.5f),
                ForeColor = accent ? Color.White : Theme.Text,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(66, 15),
                Cursor = Cursors.Hand
            };

            var note2 = new Label
            {
                Text = note,
                Font = new Font("Segoe UI", 8.75f),
                ForeColor = accent ? Color.FromArgb(226, 238, 255) : Theme.Dim,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(67, 39),
                Cursor = Cursors.Hand
            };

            host.Controls.Add(caption);
            host.Controls.Add(note2);

            Color rest = host.Fill;
            Color hot = Theme.Hover(rest);

            EventHandler enter = delegate { host.Fill = hot; host.Invalidate(); };
            EventHandler leave = delegate { host.Fill = rest; host.Invalidate(); };

            foreach (Control c in new Control[] { host, caption, note2 })
            {
                c.Click += click;
                c.MouseEnter += enter;
                c.MouseLeave += leave;
            }

            return host;
        }

        // ------------------------------------------------------------------
        // мелкие рисованные части
        // ------------------------------------------------------------------

        /// <summary>Значок, который рисуется на плитке.</summary>
        public enum Glyph { None, Plus, Folder, Java, Project }

        /// <summary>Панель со скруглёнными углами, рамкой и значком.</summary>
        public class Soft : Panel
        {
            public Color Fill = Theme.Panel;
            public Color Edge = Theme.Border;
            public int Radius = 8;
            public Glyph Mark = Glyph.None;
            public Color MarkColor = Theme.Accent2;

            public Soft()
            {
                BackColor = Color.Transparent;
                DoubleBuffered = true;
                SetStyle(ControlStyles.SupportsTransparentBackColor, true);
                SetStyle(ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var box = new Rectangle(0, 0, Width - 1, Height - 1);
                using (GraphicsPath path = Round(box, Radius))
                {
                    using (var brush = new SolidBrush(Fill)) g.FillPath(brush, path);
                    using (var pen = new Pen(Edge)) g.DrawPath(pen, path);
                }

                if (Mark != Glyph.None) Draw(g, Mark, MarkColor, new Rectangle(22, Height / 2 - 11, 22, 22));

                base.OnPaint(e);
            }
        }

        /// <summary>Квадратный значок студии — буква на градиенте.</summary>
        public class Badge : Panel
        {
            public Badge()
            {
                BackColor = Color.Transparent;
                DoubleBuffered = true;
                SetStyle(ControlStyles.SupportsTransparentBackColor, true);
                SetStyle(ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                var box = new Rectangle(0, 0, Width - 1, Height - 1);
                using (GraphicsPath path = Round(box, 12))
                using (var brush = new LinearGradientBrush(box, Theme.Accent, Theme.Accent2, 55f))
                    g.FillPath(brush, path);

                using (var font = new Font("Segoe UI Semibold", Height * 0.46f))
                using (var text = new SolidBrush(Color.White))
                {
                    var format = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    g.DrawString("D", font, text, box, format);
                }
            }
        }

        /// <summary>Строка «горячая клавиша — что делает».</summary>
        public class Key : Panel
        {
            public string Note = "";

            public Key()
            {
                BackColor = Color.Transparent;
                DoubleBuffered = true;
                SetStyle(ControlStyles.SupportsTransparentBackColor, true);
                SetStyle(ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using (var font = new Font("Segoe UI", 8.25f, FontStyle.Bold))
                {
                    SizeF size = g.MeasureString(Text, font);
                    int w = (int)Math.Ceiling(size.Width) + 14;
                    var cap = new Rectangle(0, 2, w, 19);

                    using (GraphicsPath path = Round(cap, 4))
                    using (var brush = new SolidBrush(Theme.Panel))
                    using (var pen = new Pen(Theme.Border))
                    {
                        g.FillPath(brush, path);
                        g.DrawPath(pen, path);
                    }

                    using (var ink = new SolidBrush(Theme.Dim))
                        g.DrawString(Text, font, ink, cap.X + 7, cap.Y + 3);

                    using (var small = new Font("Segoe UI", 9f))
                    using (var ink = new SolidBrush(Theme.Dim))
                        g.DrawString(Note, small, ink, cap.Right + 10, 3);
                }
            }
        }

        /// <summary>Строка недавнего проекта: имя, тип, когда открывали, путь.</summary>
        public class Entry : Panel
        {
            public string Caption = "";
            public string Kind = "";
            public string When = "";
            public string Where = "";

            bool hot;

            public Entry()
            {
                BackColor = Color.Transparent;
                DoubleBuffered = true;
                Cursor = Cursors.Hand;
                SetStyle(ControlStyles.SupportsTransparentBackColor, true);
                SetStyle(ControlStyles.ResizeRedraw, true);
            }

            protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                var box = new Rectangle(0, 0, Width - 1, Height - 1);
                if (hot)
                    using (GraphicsPath path = Round(box, 6))
                    using (var brush = new SolidBrush(Theme.Hover(Theme.Panel)))
                        g.FillPath(brush, path);

                Draw(g, Glyph.Project, Theme.Accent2, new Rectangle(12, Height / 2 - 9, 18, 18));

                using (var bold = new Font("Segoe UI Semibold", 10f))
                using (var ink = new SolidBrush(hot ? Theme.Text : Theme.Text))
                    g.DrawString(Caption, bold, ink, 42, 8);

                using (var small = new Font("Segoe UI", 8.25f))
                using (var dim = new SolidBrush(Theme.Dim))
                {
                    string line = Kind;
                    if (!string.IsNullOrEmpty(When)) line += "   ·   " + When;
                    g.DrawString(line, small, dim, 43, 30);

                    // Путь — справа, и только если он туда влезает.
                    if (!string.IsNullOrEmpty(Where))
                    {
                        SizeF size = g.MeasureString(Where, small);
                        float x = Width - 14 - size.Width;
                        if (x > 240) g.DrawString(Where, small, dim, x, 19);
                    }
                }
            }
        }

        /// <summary>Скруглённый прямоугольник.</summary>
        static GraphicsPath Round(Rectangle box, int radius)
        {
            int d = Math.Max(2, radius * 2);
            var path = new GraphicsPath();

            path.AddArc(box.X, box.Y, d, d, 180, 90);
            path.AddArc(box.Right - d, box.Y, d, d, 270, 90);
            path.AddArc(box.Right - d, box.Bottom - d, d, d, 0, 90);
            path.AddArc(box.X, box.Bottom - d, d, d, 90, 90);
            path.CloseFigure();

            return path;
        }

        /// <summary>Значки рисуются линиями — никаких файлов картинок.</summary>
        static void Draw(Graphics g, Glyph glyph, Color color, Rectangle box)
        {
            using (var pen = new Pen(color, 2f))
            using (var brush = new SolidBrush(color))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                switch (glyph)
                {
                    case Glyph.Plus:
                        g.DrawLine(pen, box.Left + box.Width / 2, box.Top + 2,
                                        box.Left + box.Width / 2, box.Bottom - 2);
                        g.DrawLine(pen, box.Left + 2, box.Top + box.Height / 2,
                                        box.Right - 2, box.Top + box.Height / 2);
                        break;

                    case Glyph.Folder:
                    {
                        var body = new Rectangle(box.Left + 1, box.Top + 5, box.Width - 3, box.Height - 8);
                        g.DrawLine(pen, box.Left + 1, box.Top + 5, box.Left + 7, box.Top + 5);
                        g.DrawLine(pen, box.Left + 7, box.Top + 5, box.Left + 9, box.Top + 2);
                        g.DrawLine(pen, box.Left + 9, box.Top + 2, box.Left + 14, box.Top + 2);
                        g.DrawRectangle(pen, body);
                        break;
                    }

                    case Glyph.Java:
                    {
                        // Чашка: овал сверху, бок и ручка.
                        var cup = new Rectangle(box.Left + 2, box.Top + 4, box.Width - 8, box.Height - 7);
                        g.DrawArc(pen, cup.X, cup.Y - 3, cup.Width, 7, 0, 360);
                        g.DrawLine(pen, cup.Left, cup.Top, cup.Left + 2, cup.Bottom);
                        g.DrawLine(pen, cup.Right, cup.Top, cup.Right - 2, cup.Bottom);
                        g.DrawLine(pen, cup.Left + 2, cup.Bottom, cup.Right - 2, cup.Bottom);
                        g.DrawArc(pen, cup.Right - 2, cup.Top + 3, 7, 9, -80, 170);
                        break;
                    }

                    case Glyph.Project:
                    {
                        var body = new Rectangle(box.Left + 1, box.Top + 1, box.Width - 3, box.Height - 3);
                        g.DrawRectangle(pen, body);
                        g.FillRectangle(brush, body.Left + 4, body.Top + 5, body.Width - 8, 2);
                        g.FillRectangle(brush, body.Left + 4, body.Top + 10, body.Width - 11, 2);
                        break;
                    }
                }
            }
        }
    }
}
