using System;
using System.Drawing;
using System.Windows.Forms;

namespace DupoStudio
{
    /// <summary>Маленькое окно ввода строки.</summary>
    public static class PromptDialog
    {
        public static string Show(IWin32Window owner, string title, string caption, string value)
        {
            using (var form = new Form())
            using (var label = new Label())
            using (var box = new TextBox())
            using (var ok = new Button())
            using (var cancel = new Button())
            {
                form.Text = title;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ClientSize = new Size(420, 130);
                form.BackColor = Theme.Chrome;
                form.ForeColor = Theme.Text;

                label.Text = caption;
                label.SetBounds(16, 16, 390, 20);
                label.ForeColor = Theme.Text;

                box.Text = value;
                box.SetBounds(16, 44, 388, 26);
                box.BackColor = Theme.Back;
                box.ForeColor = Theme.Text;
                box.BorderStyle = BorderStyle.FixedSingle;

                ok.Text = "OK";
                ok.DialogResult = DialogResult.OK;
                ok.SetBounds(228, 86, 84, 28);
                ok.FlatStyle = FlatStyle.Flat;
                ok.BackColor = Theme.Accent;

                cancel.Text = "Отмена";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.SetBounds(320, 86, 84, 28);
                cancel.FlatStyle = FlatStyle.Flat;
                cancel.BackColor = Theme.Panel;

                form.Controls.AddRange(new Control[] { label, box, ok, cancel });
                form.AcceptButton = ok;
                form.CancelButton = cancel;

                return form.ShowDialog(owner) == DialogResult.OK ? box.Text.Trim() : null;
            }
        }
    }
}
