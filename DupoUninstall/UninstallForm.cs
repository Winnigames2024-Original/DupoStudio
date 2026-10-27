using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using DupoLang;

namespace DupoUninstall
{
    /// <summary>
    /// Окно удаления в привычном виде: сначала вопрос, затем узкая
    /// полоска хода работы — как у обычных установщиков Windows.
    /// </summary>
    public class UninstallForm : Form
    {
        readonly UninstallData data;

        readonly Label question = new Label();
        readonly Label details = new Label();
        readonly Label leftovers = new Label();
        readonly ProgressBar bar = new ProgressBar();
        readonly Label step = new Label();
        readonly Button yes = new Button();
        readonly Button no = new Button();

        bool running;

        public UninstallForm(UninstallData info)
        {
            data = info;

            Text = "Удаление — " + data.Name;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(470, 214);
            BackColor = SystemColors.Control;
            Font = SystemFonts.MessageBoxFont;

            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Build();
        }

        void Build()
        {
            var icon = new PictureBox
            {
                Image = SystemIcons.Question.ToBitmap(),
                SizeMode = PictureBoxSizeMode.CenterImage,
                Bounds = new Rectangle(18, 20, 40, 40)
            };

            question.Text = "Вы действительно хотите удалить " + data.Name +
                            " и все его компоненты?";
            question.Bounds = new Rectangle(70, 20, 380, 36);

            details.Text = Details();
            details.ForeColor = SystemColors.GrayText;
            details.Bounds = new Rectangle(70, 60, 380, 56);

            leftovers.Text = Remover.Leftovers(data);
            leftovers.ForeColor = SystemColors.GrayText;
            leftovers.Bounds = new Rectangle(70, 116, 380, 32);

            step.Bounds = new Rectangle(20, 118, 430, 20);
            step.ForeColor = SystemColors.GrayText;
            step.Visible = false;

            bar.SetBounds(20, 142, 430, 18);
            bar.Style = ProgressBarStyle.Continuous;
            bar.Visible = false;

            yes.Text = "Да";
            yes.SetBounds(254, 172, 93, 26);
            yes.UseVisualStyleBackColor = true;
            yes.Click += delegate { Go(); };

            no.Text = "Нет";
            no.SetBounds(357, 172, 93, 26);
            no.UseVisualStyleBackColor = true;
            no.Click += delegate { Close(); };

            AcceptButton = yes;
            CancelButton = no;

            Controls.AddRange(new Control[] {
                icon, question, details, leftovers, step, bar, yes, no });
        }

        string Details()
        {
            string s = "";
            if (data.AppVersion.Length > 0) s += "Версия " + data.AppVersion;
            if (data.Publisher.Length > 0) s += (s.Length > 0 ? "   ·   " : "") + data.Publisher;
            if (s.Length > 0) s += Environment.NewLine;

            s += data.Dir;
            if (data.Files.Count > 0) s += Environment.NewLine + "Файлов: " + data.Files.Count;
            return s;
        }

        void Go()
        {
            if (running) return;
            running = true;

            yes.Enabled = false;
            no.Enabled = false;
            question.Text = "Удаление " + data.Name + "...";
            details.Visible = false;
            leftovers.Visible = false;
            step.Visible = true;
            bar.Visible = true;

            var worker = new Thread(delegate ()
            {
                var remover = new Remover(data);
                remover.Log = delegate (string s) { Say(s); };
                remover.Progress = delegate (int v) { Move(v); };

                try { remover.Run(); }
                catch (Exception ex) { Say("Не всё удалилось: " + ex.Message); }

                try { BeginInvoke(new Action(Done)); }
                catch { }
            });

            worker.IsBackground = true;
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
        }

        void Say(string text)
        {
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<string>(Say), text); }
                catch { }
                return;
            }
            step.Text = text;
        }

        void Move(int value)
        {
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<int>(Move), value); }
                catch { }
                return;
            }
            bar.Value = Math.Max(0, Math.Min(100, value));
        }

        void Done()
        {
            bar.Value = 100;
            step.Text = "";
            question.Text = data.Name + " удалён с вашего компьютера.";

            no.Text = "Закрыть";
            no.Enabled = true;
            yes.Visible = false;

            AcceptButton = no;
        }
    }
}
