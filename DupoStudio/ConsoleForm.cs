using System;
using System.Drawing;
using System.Windows.Forms;

namespace DupoStudio
{
    /// <summary>Окно вывода — встраивается в нижнюю часть окна проекта.</summary>
    public class ConsoleForm : Form
    {
        public readonly RichTextBox Box = new RichTextBox();

        public ConsoleForm()
        {
            Text = "Вывод";
            FormBorderStyle = FormBorderStyle.None;
            TopLevel = false;
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(24, 24, 24);

            Box.Dock = DockStyle.Fill;
            Box.BackColor = Color.FromArgb(24, 24, 24);
            Box.ForeColor = Theme.Text;
            Box.BorderStyle = BorderStyle.None;
            Box.Font = new Font("Consolas", 10f);
            Box.ReadOnly = true;
            Box.DetectUrls = false;

            Controls.Add(Box);
        }

        public void Write(string text, Color color)
        {
            if (text == null) text = "";

            Box.SelectionStart = Box.TextLength;
            Box.SelectionLength = 0;
            Box.SelectionColor = color;
            Box.AppendText(text + Environment.NewLine);
            Box.SelectionColor = Box.ForeColor;
            Box.ScrollToCaret();
        }

        public void Clear() { Box.Clear(); }
    }
}
