using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DupoSetup
{
    /// <summary>
    /// Каркас мастера в привычном виде: слева на первой и последней странице
    /// широкая полоса, на остальных — шапка с заголовком, снизу — полоса
    /// с кнопками «Назад», «Далее» и «Отмена».
    /// </summary>
    public class WizardForm : Form
    {
        protected readonly Panel Header = new Panel();
        protected readonly Label HeaderTitle = new Label();
        protected readonly Label HeaderNote = new Label();
        protected readonly Panel Body = new Panel();
        protected readonly Panel Footer = new Panel();

        protected readonly Button Back = new Button();
        protected readonly Button Next = new Button();
        protected readonly Button Cancel = new Button();

        readonly List<WizardPage> pages = new List<WizardPage>();
        int current = -1;

        public WizardForm()
        {
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(634, 456);
            BackColor = SystemColors.Control;
            ForeColor = SystemColors.ControlText;
            Font = SystemFonts.MessageBoxFont;

            BuildChrome();
        }

        void BuildChrome()
        {
            Footer.Dock = DockStyle.Bottom;
            Footer.Height = 50;
            Footer.BackColor = SystemColors.Control;
            Footer.Paint += delegate (object s, PaintEventArgs e)
            {
                using (var pen = new Pen(SystemColors.ControlDark))
                    e.Graphics.DrawLine(pen, 0, 0, Footer.Width, 0);
                using (var pen = new Pen(SystemColors.ControlLightLight))
                    e.Graphics.DrawLine(pen, 0, 1, Footer.Width, 1);
            };
            Controls.Add(Footer);

            Button(Cancel, "Отмена", 634 - 12 - 93);
            Button(Next, "Далее >", 634 - 12 - 93 - 10 - 93);
            Button(Back, "< Назад", 634 - 12 - 93 - 10 - 93 - 93);

            Back.Click += delegate { Step(-1); };
            Next.Click += delegate { Advance(); };
            Cancel.Click += delegate { AskCancel(); };

            Header.Dock = DockStyle.Top;
            Header.Height = 59;
            Header.BackColor = SystemColors.Window;
            Header.Paint += delegate (object s, PaintEventArgs e)
            {
                using (var pen = new Pen(SystemColors.ControlDark))
                    e.Graphics.DrawLine(pen, 0, Header.Height - 1, Header.Width, Header.Height - 1);
            };

            HeaderTitle.AutoSize = false;
            HeaderTitle.Bounds = new Rectangle(20, 11, 520, 19);
            HeaderTitle.Font = new Font(Font, FontStyle.Bold);
            Header.Controls.Add(HeaderTitle);

            HeaderNote.AutoSize = false;
            HeaderNote.Bounds = new Rectangle(34, 31, 506, 20);
            Header.Controls.Add(HeaderNote);

            Controls.Add(Header);

            Body.Dock = DockStyle.Fill;
            Body.BackColor = SystemColors.Control;
            Controls.Add(Body);
            Body.BringToFront();

            CancelButton = Cancel;
        }

        static void Button(Button b, string text, int left)
        {
            b.Text = text;
            b.SetBounds(left, 14, 93, 26);
            b.UseVisualStyleBackColor = true;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Footer.Controls.AddRange(new Control[] { Back, Next, Cancel });
            if (pages.Count > 0 && current < 0) Show(0);
        }

        // ------------------------------------------------------------------
        // страницы
        // ------------------------------------------------------------------

        protected void AddPage(WizardPage page)
        {
            page.Owner = this;
            page.Panel.Dock = DockStyle.Fill;
            page.Panel.Visible = false;
            page.Panel.BackColor = page.Wide ? SystemColors.Window : SystemColors.Control;

            pages.Add(page);
            Body.Controls.Add(page.Panel);
        }

        protected WizardPage Current { get { return current >= 0 && current < pages.Count ? pages[current] : null; } }

        protected void Show(int index)
        {
            if (index < 0 || index >= pages.Count) return;

            var was = Current;
            if (was != null) was.Panel.Visible = false;

            current = index;
            var page = pages[current];

            Header.Visible = !page.Wide;
            HeaderTitle.Text = page.Title;
            HeaderNote.Text = page.Note;

            Back.Enabled = page.CanBack && index > 0;
            Back.Visible = page.ShowButtons;
            Next.Visible = page.ShowButtons;
            Cancel.Visible = page.ShowCancel;
            Next.Text = page.NextText;
            Next.Enabled = true;

            page.Panel.Visible = true;
            page.Panel.BringToFront();

            page.Entered();
        }

        /// <summary>Перейти на страницу по названию.</summary>
        protected void Show(string title)
        {
            for (int i = 0; i < pages.Count; i++)
                if (pages[i].Key == title) { Show(i); return; }
        }

        void Step(int delta)
        {
            int next = current + delta;
            if (next < 0 || next >= pages.Count) return;
            Show(next);
        }

        void Advance()
        {
            var page = Current;
            if (page == null) return;
            if (!page.Leaving()) return;
            Step(1);
        }

        /// <summary>Текст кнопки «Далее» на текущей странице.</summary>
        protected void NextLabel(string text) { Next.Text = text; }

        /// <summary>Разрешить или запретить переход вперёд.</summary>
        protected void AllowNext(bool on) { Next.Enabled = on; }

        /// <summary>Что делать по «Отмена». По умолчанию спрашиваем.</summary>
        protected virtual void AskCancel()
        {
            var r = MessageBox.Show(this,
                "Прервать установку?", Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);

            if (r == DialogResult.Yes) Close();
        }

        // ------------------------------------------------------------------
        // широкая полоса слева — как на первой странице Inno Setup
        // ------------------------------------------------------------------

        /// <summary>Нарисованная полоса: градиент и крупная буква.</summary>
        public static Panel Banner(string letter, Color from, Color to)
        {
            var band = new Panel();
            band.Dock = DockStyle.Left;
            band.Width = 164;

            band.Paint += delegate (object s, PaintEventArgs e)
            {
                var rect = band.ClientRectangle;
                if (rect.Width <= 0 || rect.Height <= 0) return;

                using (var brush = new LinearGradientBrush(rect, from, to, LinearGradientMode.Vertical))
                    e.Graphics.FillRectangle(brush, rect);

                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                using (var font = new Font("Segoe UI", 68f, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
                {
                    var size = e.Graphics.MeasureString(letter, font);
                    e.Graphics.DrawString(letter, font, brush,
                        (rect.Width - size.Width) / 2f, rect.Height - size.Height - 24f);
                }
            };

            return band;
        }
    }

    /// <summary>Одна страница мастера.</summary>
    public class WizardPage
    {
        public string Key = "";
        public string Title = "";
        public string Note = "";
        public string NextText = "Далее >";

        /// <summary>Страница без шапки, во всю ширину — приветствие и завершение.</summary>
        public bool Wide;

        public bool CanBack = true;
        public bool ShowButtons = true;
        public bool ShowCancel = true;

        public readonly Panel Panel = new Panel();
        public WizardForm Owner;

        /// <summary>Вызывается при показе страницы.</summary>
        public Action OnEnter;

        /// <summary>Вызывается перед уходом вперёд; false — остаёмся.</summary>
        public Func<bool> OnLeave;

        public void Entered() { var h = OnEnter; if (h != null) h(); }

        public bool Leaving()
        {
            var h = OnLeave;
            return h == null || h();
        }
    }
}
