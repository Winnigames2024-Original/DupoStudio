using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DupoStudio
{
    /// <summary>Где живёт панель.</summary>
    public enum DockZone { Left, Right, Bottom, Center }

    /// <summary>
    /// Одна панель инструментов: заголовок, содержимое и место, куда
    /// она возвращается, если её отцепили в отдельное окно.
    /// </summary>
    public class DockPane
    {
        public string Title;
        public Control Content;
        public DockZone Home;
        public TabPage Page;
        public FloatWindow Window;

        public bool Floating { get { return Window != null; } }

        public DockPane(string title, Control content, DockZone home)
        {
            Title = title;
            Content = content;
            Home = home;
        }

        public override string ToString() { return Title; }
    }

    /// <summary>Отдельное окно для отцепленной панели.</summary>
    public class FloatWindow : Form
    {
        public readonly DockPane Pane;

        public event EventHandler DockBack;

        public FloatWindow(DockPane pane, Size size)
        {
            Pane = pane;

            Text = pane.Title;
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            ClientSize = size;
            BackColor = Theme.Panel;
            ForeColor = Theme.Text;
            KeyPreview = true;

            var hint = new Label();
            hint.Dock = DockStyle.Bottom;
            hint.Height = 20;
            hint.TextAlign = ContentAlignment.MiddleCenter;
            hint.ForeColor = Theme.Dim;
            hint.BackColor = Theme.Chrome;
            hint.Text = "Закройте окно, чтобы вернуть панель на место";
            Controls.Add(hint);

            pane.Content.Dock = DockStyle.Fill;
            Controls.Add(pane.Content);
            pane.Content.BringToFront();

            KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) Close();
            };
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Окно не закрывают насовсем — панель просто возвращается в студию.
            if (e.CloseReason == CloseReason.UserClosing || e.CloseReason == CloseReason.None)
            {
                var h = DockBack;
                if (h != null) h(this, EventArgs.Empty);
            }
            base.OnFormClosing(e);
        }
    }

    /// <summary>
    /// Область студии, в которой лежат панели: заголовок с кнопками
    /// и вкладки снизу, как у окон инструментов Visual Studio.
    /// </summary>
    public class DockZonePanel : Panel
    {
        readonly Panel caption = new Panel();
        readonly Label title = new Label();
        readonly Label floatBtn = new Label();
        readonly Label closeBtn = new Label();
        readonly TabControl tabs = new TabControl();

        readonly List<DockPane> panes = new List<DockPane>();

        Point dragFrom;
        bool dragging;

        /// <summary>Панель попросили отцепить.</summary>
        public event EventHandler<DockPaneEventArgs> FloatRequested;

        /// <summary>Панель попросили спрятать.</summary>
        public event EventHandler<DockPaneEventArgs> HideRequested;

        /// <summary>Зона, за которую отвечает эта область.</summary>
        public DockZone Zone { get; private set; }

        /// <summary>Сколько панелей сейчас видно.</summary>
        public int Count { get { return tabs.TabPages.Count; } }

        public DockZonePanel(DockZone zone)
        {
            Zone = zone;
            BackColor = Theme.Panel;
            Padding = new Padding(0);

            BuildCaption();
            BuildTabs();

            Controls.Add(tabs);
            Controls.Add(caption);
        }

        void BuildCaption()
        {
            caption.Dock = DockStyle.Top;
            caption.Height = 26;
            caption.BackColor = Theme.Chrome;

            title.Dock = DockStyle.Fill;
            title.TextAlign = ContentAlignment.MiddleLeft;
            title.ForeColor = Theme.Text;
            title.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            title.Padding = new Padding(8, 0, 0, 0);
            title.MouseDown += CaptionDown;
            title.MouseMove += CaptionMove;
            title.MouseUp += CaptionUp;
            title.DoubleClick += delegate { Float(); };

            SmallButton(floatBtn, "⤢", "Отцепить панель в отдельное окно");
            floatBtn.Click += delegate { Float(); };

            SmallButton(closeBtn, "✕", "Спрятать панель");
            closeBtn.Click += delegate { Hide2(); };

            caption.Controls.Add(title);
            caption.Controls.Add(floatBtn);
            caption.Controls.Add(closeBtn);
        }

        void SmallButton(Label l, string text, string hint)
        {
            l.Dock = DockStyle.Right;
            l.Width = 26;
            l.Text = text;
            l.TextAlign = ContentAlignment.MiddleCenter;
            l.ForeColor = Theme.Dim;
            l.Font = new Font("Segoe UI", 9f);
            l.Cursor = Cursors.Hand;
            l.MouseEnter += delegate { l.ForeColor = Theme.Text; l.BackColor = Theme.Border; };
            l.MouseLeave += delegate { l.ForeColor = Theme.Dim; l.BackColor = Color.Transparent; };

            var tip = new ToolTip();
            tip.SetToolTip(l, hint);
        }

        void BuildTabs()
        {
            tabs.Dock = DockStyle.Fill;
            tabs.Alignment = TabAlignment.Bottom;
            tabs.SizeMode = TabSizeMode.Normal;
            tabs.ItemSize = new Size(110, 22);
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.Padding = new Point(10, 3);
            tabs.BackColor = Theme.Panel;
            tabs.DrawItem += DrawTab;
            tabs.SelectedIndexChanged += delegate { RefreshCaption(); };
        }

        void DrawTab(object sender, DrawItemEventArgs e)
        {
            bool active = e.Index == tabs.SelectedIndex;
            var rect = e.Bounds;

            using (var back = new SolidBrush(active ? Theme.Panel : Theme.Chrome))
                e.Graphics.FillRectangle(back, rect);

            if (active)
                using (var line = new SolidBrush(Theme.Accent))
                    e.Graphics.FillRectangle(line, rect.Left, rect.Bottom - 2, rect.Width, 2);

            TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, Font, rect,
                                  active ? Theme.Text : Theme.Dim,
                                  TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        // ------------------------------------------------------------------
        // панели
        // ------------------------------------------------------------------

        /// <summary>Поставить панель в эту область.</summary>
        public void Add(DockPane pane)
        {
            if (panes.Contains(pane)) { Select(pane); return; }

            var page = new TabPage(pane.Title);
            page.BackColor = Theme.Panel;
            page.Padding = new Padding(0);

            pane.Content.Dock = DockStyle.Fill;
            page.Controls.Add(pane.Content);

            pane.Page = page;
            panes.Add(pane);
            tabs.TabPages.Add(page);

            Select(pane);
            Visible = true;
        }

        /// <summary>Снять панель с этой области, не трогая её содержимое.</summary>
        public void Remove(DockPane pane)
        {
            if (!panes.Contains(pane)) return;

            if (pane.Page != null)
            {
                pane.Page.Controls.Remove(pane.Content);
                tabs.TabPages.Remove(pane.Page);
                pane.Page.Dispose();
                pane.Page = null;
            }

            panes.Remove(pane);
            RefreshCaption();
        }

        /// <summary>Показать панель поверх остальных вкладок.</summary>
        public void Select(DockPane pane)
        {
            if (pane.Page != null && tabs.TabPages.Contains(pane.Page))
                tabs.SelectedTab = pane.Page;
            RefreshCaption();
        }

        /// <summary>Панель, видимая сейчас.</summary>
        public DockPane Active
        {
            get
            {
                var page = tabs.SelectedTab;
                return page == null ? null : panes.FirstOrDefault(p => p.Page == page);
            }
        }

        void RefreshCaption()
        {
            var a = Active;
            title.Text = a == null ? "" : a.Title.ToUpperInvariant();
            caption.Visible = a != null;
            floatBtn.Visible = a != null;
            closeBtn.Visible = a != null;
        }

        void Float()
        {
            var a = Active;
            if (a == null) return;
            var h = FloatRequested;
            if (h != null) h(this, new DockPaneEventArgs(a));
        }

        void Hide2()
        {
            var a = Active;
            if (a == null) return;
            var h = HideRequested;
            if (h != null) h(this, new DockPaneEventArgs(a));
        }

        // ------------------------------------------------------------------
        // перетаскивание заголовка
        // ------------------------------------------------------------------

        void CaptionDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            dragFrom = e.Location;
            dragging = true;
        }

        void CaptionMove(object sender, MouseEventArgs e)
        {
            if (!dragging) return;
            if (Math.Abs(e.X - dragFrom.X) < 40 && Math.Abs(e.Y - dragFrom.Y) < 40) return;

            dragging = false;
            Float();
        }

        void CaptionUp(object sender, MouseEventArgs e) { dragging = false; }
    }

    public class DockPaneEventArgs : EventArgs
    {
        public readonly DockPane Pane;
        public DockPaneEventArgs(DockPane pane) { Pane = pane; }
    }

    /// <summary>
    /// Раскладка студии: слева, справа и снизу — области панелей,
    /// в середине — редактор. Панели отцепляются в окна и возвращаются назад.
    /// </summary>
    public class DockHost : Panel
    {
        readonly SplitContainer outer = new SplitContainer();     // лево | остальное
        readonly SplitContainer inner = new SplitContainer();     // верх / низ
        readonly SplitContainer middle = new SplitContainer();    // центр | право

        public readonly DockZonePanel Left = new DockZonePanel(DockZone.Left);
        public readonly DockZonePanel Right = new DockZonePanel(DockZone.Right);
        public readonly DockZonePanel Bottom = new DockZonePanel(DockZone.Bottom);
        public readonly Panel Center = new Panel();

        readonly List<DockPane> all = new List<DockPane>();

        /// <summary>Панель спрятали — пригодится, чтобы обновить меню «Вид».</summary>
        public event EventHandler Changed;

        public DockHost()
        {
            BackColor = Theme.Back;

            Setup(outer, Orientation.Vertical, 230);
            Setup(inner, Orientation.Horizontal, 420);
            Setup(middle, Orientation.Vertical, 640);

            Center.Dock = DockStyle.Fill;
            Center.BackColor = Theme.Back;

            middle.Panel1.Controls.Add(Center);
            middle.Panel2.Controls.Add(Right);
            Right.Dock = DockStyle.Fill;

            inner.Panel1.Controls.Add(middle);
            inner.Panel2.Controls.Add(Bottom);
            Bottom.Dock = DockStyle.Fill;

            outer.Panel1.Controls.Add(Left);
            Left.Dock = DockStyle.Fill;
            outer.Panel2.Controls.Add(inner);

            Controls.Add(outer);

            foreach (var zone in new[] { Left, Right, Bottom })
            {
                zone.FloatRequested += delegate (object s, DockPaneEventArgs e) { Float(e.Pane); };
                zone.HideRequested += delegate (object s, DockPaneEventArgs e) { HidePane(e.Pane); };
            }
        }

        static void Setup(SplitContainer sc, Orientation orientation, int distance)
        {
            sc.Dock = DockStyle.Fill;
            sc.Orientation = orientation;
            sc.BackColor = Theme.Border;
            sc.SplitterWidth = 4;
            sc.Panel1MinSize = 120;
            sc.Panel2MinSize = 120;
            try { sc.SplitterDistance = distance; } catch { }
        }

        /// <summary>Все панели студии.</summary>
        public IEnumerable<DockPane> Panes { get { return all; } }

        /// <summary>Добавить панель в её зону.</summary>
        public DockPane Add(string title, Control content, DockZone zone)
        {
            var pane = new DockPane(title, content, zone);
            all.Add(pane);
            ZoneOf(zone).Add(pane);
            Fire();
            return pane;
        }

        public DockZonePanel ZoneOf(DockZone zone)
        {
            switch (zone)
            {
                case DockZone.Left: return Left;
                case DockZone.Right: return Right;
                default: return Bottom;
            }
        }

        /// <summary>Найти панель по заголовку.</summary>
        public DockPane Find(string title)
        {
            return all.FirstOrDefault(p => string.Equals(p.Title, title, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Показать панель: вернуть из окна или из небытия и выбрать.</summary>
        public void Show(DockPane pane)
        {
            if (pane == null) return;

            if (pane.Floating) { pane.Window.Close(); return; }

            var zone = ZoneOf(pane.Home);
            if (pane.Page == null) zone.Add(pane);
            else zone.Select(pane);

            Collapse();
            Fire();
        }

        /// <summary>Спрятать панель.</summary>
        public void HidePane(DockPane pane)
        {
            if (pane == null || pane.Floating) return;

            ZoneOf(pane.Home).Remove(pane);
            Collapse();
            Fire();
        }

        /// <summary>Видна ли панель — в студии или в своём окне.</summary>
        public bool IsShown(DockPane pane)
        {
            return pane != null && (pane.Floating || pane.Page != null);
        }

        /// <summary>Отцепить панель в отдельное окно.</summary>
        public void Float(DockPane pane)
        {
            if (pane == null || pane.Floating) return;

            var zone = ZoneOf(pane.Home);
            var size = new Size(Math.Max(320, zone.Width), Math.Max(240, zone.Height));
            zone.Remove(pane);

            var win = new FloatWindow(pane, size);
            pane.Window = win;

            var owner = FindForm();
            if (owner != null)
            {
                win.Owner = owner;
                win.Location = new Point(owner.Left + owner.Width / 3, owner.Top + owner.Height / 4);
            }

            win.DockBack += delegate
            {
                win.Controls.Remove(pane.Content);
                pane.Window = null;
                ZoneOf(pane.Home).Add(pane);
                Collapse();
                Fire();
            };

            win.Show(owner);
            Collapse();
            Fire();
        }

        /// <summary>Вернуть все окна на место.</summary>
        public void DockAll()
        {
            foreach (var p in all.Where(x => x.Floating).ToList()) p.Window.Close();
        }

        /// <summary>Пустые зоны не занимают место.</summary>
        public void Collapse()
        {
            outer.Panel1Collapsed = Left.Count == 0;
            inner.Panel2Collapsed = Bottom.Count == 0;
            middle.Panel2Collapsed = Right.Count == 0;
        }

        void Fire()
        {
            var h = Changed;
            if (h != null) h(this, EventArgs.Empty);
        }
    }
}
