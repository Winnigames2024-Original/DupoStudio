using System;
using System.Drawing;
using System.Windows.Forms;

namespace DupoStudio
{
    /// <summary>Редактор вместе с полосой номеров строк слева.</summary>
    public class EditorHost : Panel
    {
        public readonly CodeEditor Editor = new CodeEditor();
        public readonly CodeCompletion Completion;

        readonly Panel gutter = new Panel();

        public EditorHost()
        {
            BackColor = Theme.Back;
            DoubleBuffered = true;

            gutter.Dock = DockStyle.Left;
            gutter.Width = 56;
            gutter.BackColor = Theme.Gutter;

            Editor.Dock = DockStyle.Fill;

            Controls.Add(Editor);
            Controls.Add(gutter);

            Completion = new CodeCompletion(Editor);
            Completion.AttachTo(this);

            gutter.Paint += PaintGutter;
            Editor.VScroll += delegate { gutter.Invalidate(); };
            Editor.TextChanged += delegate { gutter.Invalidate(); };
            Editor.SelectionChanged += delegate { gutter.Invalidate(); };
            Editor.Resize += delegate { gutter.Invalidate(); };
        }

        void PaintGutter(object sender, PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Gutter);

            using (var edge = new Pen(Theme.Border))
                e.Graphics.DrawLine(edge, gutter.Width - 1, 0, gutter.Width - 1, gutter.Height);

            if (!Editor.IsHandleCreated) return;

            int first = Editor.GetLineFromCharIndex(Editor.GetCharIndexFromPosition(new Point(2, 2)));
            int last = Editor.GetLineFromCharIndex(Editor.GetCharIndexFromPosition(
                new Point(2, Math.Max(2, Editor.ClientSize.Height - 2))));
            int current = Editor.GetLineFromCharIndex(Editor.SelectionStart);

            using (var dim = new SolidBrush(Theme.LineNumber))
            using (var hot = new SolidBrush(Theme.Accent))
            using (var font = new Font(Editor.Font.FontFamily, Editor.Font.Size - 0.5f))
            {
                for (int line = first; line <= last + 1 && line < Editor.Lines.Length + 1; line++)
                {
                    int charIndex = Editor.GetFirstCharIndexFromLine(line);
                    if (charIndex < 0) break;

                    Point pos = Editor.GetPositionFromCharIndex(charIndex);
                    if (pos.Y > Editor.ClientSize.Height) break;

                    string text = (line + 1).ToString();
                    SizeF size = e.Graphics.MeasureString(text, font);
                    e.Graphics.DrawString(text, font, line == current ? hot : dim,
                        gutter.Width - 10 - size.Width, pos.Y);
                }
            }
        }
    }
}
