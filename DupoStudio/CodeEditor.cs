using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace DupoStudio
{
    /// <summary>Редактор кода DupoLang с подсветкой синтаксиса.</summary>
    public class CodeEditor : RichTextBox
    {
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref Point lParam);

        const int WM_SETREDRAW = 0x000B;
        const int EM_GETSCROLLPOS = 0x04DD;
        const int EM_SETSCROLLPOS = 0x04DE;

        static readonly Regex RxComment = new Regex(@"//[^\n]*|#[^\n]*|/\*[\s\S]*?\*/", RegexOptions.Compiled);
        static readonly Regex RxString = new Regex("\"[^\"\\n]*\"|'[^'\\n]*'", RegexOptions.Compiled);
        static readonly Regex RxNumber = new Regex(@"\b\d+(\.\d+)?\b", RegexOptions.Compiled);
        static readonly Regex RxKeyword = new Regex(
            @"\b(import|using|var|new|func|return|if|else|while|for|true|false|null|break|continue)\b",
            RegexOptions.Compiled);
        static readonly Regex RxType = new Regex(
            @"\b(Web|Server|Php|IO|Sys|App|Java|Files|System|Dupo|print|println|str|num|int|len|wait|now|random|list|push|upper|lower|split|round|abs|type)\b",
            RegexOptions.Compiled);

        static readonly Regex RxJavaKeyword = new Regex(
            @"\b(abstract|assert|boolean|break|byte|case|catch|char|class|const|continue|default|do|double|" +
            @"else|enum|extends|final|finally|float|for|goto|if|implements|import|instanceof|int|interface|" +
            @"long|native|new|package|private|protected|public|return|short|static|strictfp|super|switch|" +
            @"synchronized|this|throw|throws|transient|try|var|void|volatile|while|true|false|null|record|sealed)\b",
            RegexOptions.Compiled);
        static readonly Regex RxJavaType = new Regex(
            @"\b(String|System|Object|Integer|Long|Double|Float|Boolean|Character|Byte|Math|List|ArrayList|" +
            @"Map|HashMap|Set|HashSet|Arrays|Collections|Exception|RuntimeException|Thread|Runnable|" +
            @"Scanner|File|StringBuilder|Optional|Stream|out|err|println|printf|print)\b",
            RegexOptions.Compiled);

        readonly Timer timer = new Timer();
        bool busy;

        /// <summary>Подсвечивать как Java, а не как DupoLang.</summary>
        public bool JavaMode;

        public string FilePath;
        public bool Dirty;

        public CodeEditor()
        {
            BorderStyle = BorderStyle.None;
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            Font = new Font("Consolas", 11f);
            AcceptsTab = true;
            WordWrap = false;
            ScrollBars = RichTextBoxScrollBars.Both;
            DetectUrls = false;

            timer.Interval = 300;
            timer.Tick += delegate { timer.Stop(); Highlight(); };
            TextChanged += delegate { Dirty = true; timer.Stop(); timer.Start(); };
            KeyDown += OnKeyDown;
        }

        void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Tab)
            {
                e.SuppressKeyPress = true;
                SelectedText = "    ";
            }
            else if (e.KeyCode == Keys.Enter)
            {
                int lineIndex = GetLineFromCharIndex(SelectionStart);
                if (lineIndex >= 0 && lineIndex < Lines.Length)
                {
                    string cur = Lines[lineIndex];
                    int spaces = 0;
                    while (spaces < cur.Length && cur[spaces] == ' ') spaces++;
                    if (cur.TrimEnd().EndsWith("{")) spaces += 4;
                    if (spaces > 0)
                    {
                        e.SuppressKeyPress = true;
                        SelectedText = "\n" + new string(' ', spaces);
                    }
                }
            }
        }

        public void HighlightNow() { Highlight(); }

        void Highlight()
        {
            if (busy || !IsHandleCreated) return;
            busy = true;

            string text = Text;
            int selStart = SelectionStart;
            int selLen = SelectionLength;
            Point scroll = Point.Empty;

            SendMessage(Handle, EM_GETSCROLLPOS, IntPtr.Zero, ref scroll);
            SendMessage(Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);

            try
            {
                Select(0, text.Length);
                SelectionColor = Theme.Text;

                PaintRx(RxNumber, text, Theme.Number);
                PaintRx(JavaMode ? RxJavaKeyword : RxKeyword, text, Theme.Keyword);
                PaintRx(JavaMode ? RxJavaType : RxType, text, Theme.TypeName);
                PaintRx(RxString, text, Theme.Str);
                PaintRx(RxComment, text, Theme.Comment);
            }
            finally
            {
                Select(selStart, selLen);
                SelectionColor = Theme.Text;
                SendMessage(Handle, EM_SETSCROLLPOS, IntPtr.Zero, ref scroll);
                SendMessage(Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                Invalidate();
                busy = false;
            }
        }

        void PaintRx(Regex rx, string text, Color color)
        {
            foreach (Match m in rx.Matches(text))
            {
                Select(m.Index, m.Length);
                SelectionColor = color;
            }
        }

        public int CurrentLine { get { return GetLineFromCharIndex(SelectionStart) + 1; } }
        public int CurrentColumn { get { return SelectionStart - GetFirstCharIndexOfCurrentLine() + 1; } }
    }
}
