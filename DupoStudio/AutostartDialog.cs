using System;
using System.Drawing;
using System.Windows.Forms;

namespace DupoStudio
{
    /// <summary>Спрашивает при закрытии проекта, открывать ли его сразу при запуске студии.</summary>
    public class AutostartDialog : Form
    {
        readonly CheckBox dontAsk = new CheckBox();

        public bool DontAskAgain { get { return dontAsk.Checked; } }

        public AutostartDialog(string projectName)
        {
            Text = "Автозапуск проекта";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(440, 190);
            BackColor = Theme.Chrome;
            ForeColor = Theme.Text;

            var title = new Label
            {
                Text = "Открывать «" + projectName + "» при запуске?",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Theme.Text,
                AutoSize = false,
                Bounds = new Rectangle(20, 20, 400, 26)
            };

            var note = new Label
            {
                Text = "Тогда студия будет сразу открывать этот проект, минуя стартовое окно.\n" +
                       "Поменять решение можно в меню «Файл».",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Theme.Dim,
                AutoSize = false,
                Bounds = new Rectangle(20, 52, 400, 42)
            };

            dontAsk.Text = "Больше не спрашивать";
            dontAsk.Font = new Font("Segoe UI", 9f);
            dontAsk.ForeColor = Theme.Dim;
            dontAsk.FlatStyle = FlatStyle.Flat;
            dontAsk.Bounds = new Rectangle(22, 102, 260, 22);

            var yes = new Button
            {
                Text = "Да, открывать",
                DialogResult = DialogResult.Yes,
                Bounds = new Rectangle(190, 140, 120, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Accent,
                ForeColor = Color.White
            };
            yes.FlatAppearance.BorderSize = 0;

            var no = new Button
            {
                Text = "Не надо",
                DialogResult = DialogResult.No,
                Bounds = new Rectangle(318, 140, 102, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Panel,
                ForeColor = Theme.Text
            };
            no.FlatAppearance.BorderColor = Theme.Border;

            Controls.AddRange(new Control[] { title, note, dontAsk, yes, no });
            AcceptButton = yes;
            CancelButton = no;
        }
    }
}
