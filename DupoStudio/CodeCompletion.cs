using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DupoLang;

namespace DupoStudio
{
    /// <summary>
    /// Подсказки в редакторе. Появляются после точки за известным модулем
    /// и по Ctrl+Пробел. Ресурсы берутся из папки Resources проекта,
    /// поэтому Resources.имя подсказывается именно то, что вы добавили.
    /// </summary>
    public class CodeCompletion
    {
        readonly CodeEditor editor;
        readonly ListBox list = new ListBox();

        int replaceFrom = -1;

        /// <summary>Откуда брать имена ресурсов — папка открытого проекта.</summary>
        public Func<string> ProjectDir = delegate { return null; };

        static readonly Dictionary<string, string[]> Members =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "Web", new[] { "Server", "Open", "Get", "Download", "Hosts" } },
            { "Files", new[] {
                "Create", "Write", "Append", "AppendLine", "Read", "ReadLines", "ReplaceIn",
                "Contains", "Exists", "Delete", "Copy", "Move", "Rename", "Size", "Modified",
                "Name", "NameOnly", "Extension", "Folder", "Full", "Join", "Temp",
                "MakeDir", "DirExists", "DeleteDir", "CopyDir",
                "List", "Dirs", "Find", "Tree", "Count", "Zip", "Unzip", "Open", "ProjectDir",
                "AppData", "RoamingData", "Desktop", "StartMenu", "ProgramFiles", "Documents",
                "Shortcut" } },
            { "Java", new[] {
                "Install", "Uninstall", "IsInstalled", "Version", "Folder",
                "Compile", "Jar", "Run", "RunJar", "Build", "New" } },
            { "Tools", new[] {
                "Install", "Reinstall", "Uninstall", "IsInstalled", "Version", "Folder",
                "Build", "ToCSharp", "Info" } },
            { "App", new[] { "Export", "Import", "Unpack", "Info", "Files" } },
            { "IO", new[] {
                "ReadFile", "WriteFile", "AppendFile", "Exists", "MakeDir",
                "Delete", "Files", "Dirs", "ProjectDir" } },
            { "Sys", new[] { "Run", "Open", "Env", "Sleep", "Time", "Date" } },
            { "System", new[] { "Files", "Java", "Sys", "Services", "Data" } },
            { "Services", new[] {
                "Service", "ServiceLevel", "Exists", "State", "List", "Levels",
                "LevelName", "IsAdmin" } },
            { "Services.Service", new[] {
                "Name", "Description", "Level", "LevelName", "ExePath", "Args", "WorkDir",
                "Program", "Arguments", "Folder", "SetLevel",
                "Install", "Uninstall", "Start", "Stop", "Restart",
                "Installed", "Running", "State", "NeedsAdmin", "Info" } },
            { "Text", new[] {
                "Trim", "TrimStart", "TrimEnd", "Upper", "Lower", "Length", "IsEmpty",
                "Replace", "Contains", "StartsWith", "EndsWith", "IndexOf",
                "Cut", "Left", "Right", "Split", "Lines", "Words", "Join",
                "Repeat", "Reverse", "PadLeft", "PadRight", "Capital", "CountOf",
                "Format", "Fill", "Match", "Find", "ReplaceAll",
                "ToNumber", "Round", "ToBase64", "FromBase64", "UrlEncode", "UrlDecode" } },
            { "Json", new[] { "Parse", "Read", "Text", "Pretty", "Save" } },
            { "Hosts", new[] {
                "FilePath", "TempDir", "DataDir", "Exists", "Count", "Source",
                "Create", "Reload", "Resolve", "Knows", "List", "Rewrite",
                "Add", "Remove", "Open", "Text" } },
            { "Lang", new[] {
                "ExecuteFrom", "Functions", "HasFunction", "Arity",
                "RunFile", "RunCode", "Check", "Reset", "Version" } },
            { "Data", new[] { "Map", "Json", "Text" } },
            { "Data.Map", new[] {
                "Get", "Set", "Has", "Remove", "Clear", "Copy",
                "Keys", "Values", "Count" } },
            { "CSharp", new[] {
                "Build", "Forms", "Library", "Compile", "Run", "Launch",
                "IsInstalled", "Version", "Folder", "Install", "CscPath", "Sources" } },
            { "Cpp", new[] {
                "Build", "Window", "Compile", "Run", "Launch", "Install", "Uninstall",
                "IsInstalled", "Version", "Folder", "GppPath", "Sources" } },
            { "Services.ServiceLevel", new[] {
                "SystemProcess", "LocalService", "NetworkService", "Delayed", "Manual",
                "Interactive", "Disabled", "UserProcess", "Background", "Process" } },
        };

        static readonly string[] Globals =
        {
            "import", "using", "var", "func", "return", "if", "else", "while", "for",
            "break", "continue", "true", "false", "null", "new",
            "print", "println", "str", "num", "int", "round", "abs", "type", "len",
            "wait", "now", "random", "list", "push", "upper", "lower", "split",
            "Web", "Files", "Java", "Tools", "App", "IO", "Sys", "Resources", "Services",
            "CSharp", "Cpp", "Text", "Json", "Data", "Lang", "Hosts", "def",
            "map", "keys", "values", "has"
        };

        public CodeCompletion(CodeEditor owner)
        {
            editor = owner;

            list.Visible = false;
            list.BorderStyle = BorderStyle.FixedSingle;
            list.BackColor = Theme.Panel;
            list.ForeColor = Theme.Text;
            list.Font = new Font("Consolas", 10f);
            list.Width = 260;
            list.Height = 160;

            list.DoubleClick += delegate { Accept(); };
            list.KeyDown += ListKey;

            editor.KeyDown += EditorKeyDown;
            editor.KeyUp += EditorKeyUp;
            editor.LostFocus += delegate { if (!list.Focused) Hide(); };
        }

        /// <summary>Подсказки живут поверх редактора, так что им нужен общий родитель.</summary>
        public void AttachTo(Control host)
        {
            if (!host.Controls.Contains(list)) host.Controls.Add(list);
            list.BringToFront();
        }

        public bool Visible { get { return list.Visible; } }

        // ---------------- клавиши ----------------

        void EditorKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Space)
            {
                e.SuppressKeyPress = true;
                Show(WordStart(), Candidates());
                return;
            }

            if (!list.Visible) return;

            if (e.KeyCode == Keys.Escape) { Hide(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Tab) { Accept(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Down) { Move(1); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Up) { Move(-1); e.SuppressKeyPress = true; }
        }

        void EditorKeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.OemPeriod || e.KeyCode == Keys.Decimal)
            {
                string owner = IdentBefore(editor.SelectionStart - 1);
                if (owner != null)
                {
                    List<string> names = MembersOf(owner);
                    if (names.Count > 0) { Show(editor.SelectionStart, names); return; }
                }
                Hide();
                return;
            }

            if (list.Visible)
            {
                if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right ||
                    e.KeyCode == Keys.Home || e.KeyCode == Keys.End) { Hide(); return; }

                Filter();
                return;
            }

            // Подсказки сами появляются по ходу набора — как в больших средах.
            if (!Auto) return;
            if (e.Control || e.Alt) return;

            bool letter = (e.KeyCode >= Keys.A && e.KeyCode <= Keys.Z) ||
                          (e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9) ||
                          e.KeyCode == Keys.Oem3 || e.KeyCode == Keys.OemQuestion ||
                          e.KeyCode == Keys.OemMinus || (int)e.KeyCode >= 186;

            if (!letter) return;

            string word = CurrentWord();
            if (word.Length < 2) return;
            if (InText()) return;

            var hints = Candidates();
            if (hints.Count == 0 || (hints.Count == 1 && hints[0] == word)) return;

            Show(WordStart(), hints);
        }

        /// <summary>Подсказывать ли по ходу набора.</summary>
        public bool Auto = true;

        /// <summary>Курсор внутри строки или комментария — там подсказки не нужны.</summary>
        bool InText()
        {
            try
            {
                int caret = editor.SelectionStart;
                int line = editor.GetLineFromCharIndex(caret);
                int from = editor.GetFirstCharIndexFromLine(line);
                if (from < 0 || caret < from) return false;

                string before = editor.Text.Substring(from, caret - from);

                if (before.Contains("//") || before.TrimStart().StartsWith("#")) return true;

                int quotes = 0;
                foreach (char c in before) if (c == '"') quotes++;
                return quotes % 2 == 1;
            }
            catch { return false; }
        }

        void ListKey(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Tab) { Accept(); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape) { Hide(); editor.Focus(); e.Handled = true; }
        }

        void Move(int delta)
        {
            if (list.Items.Count == 0) return;
            int next = list.SelectedIndex + delta;
            list.SelectedIndex = Math.Max(0, Math.Min(list.Items.Count - 1, next));
        }

        // ---------------- что показывать ----------------

        List<string> MembersOf(string owner)
        {
            if (string.Equals(owner, "Resources", StringComparison.OrdinalIgnoreCase))
                return ResourceSet.Names(ProjectDir());

            string[] found;
            if (Members.TryGetValue(owner, out found)) return new List<string>(found);

            // Web.Server и подобные цепочки
            if (owner.EndsWith("Server", StringComparison.OrdinalIgnoreCase))
                return new List<string> { "Start", "Stop", "Url", "Port", "Root", "Running",
                                          "SetIndex", "IndexName", "IndexFiles", "Fallback", "Php" };

            if (owner.EndsWith("Php", StringComparison.OrdinalIgnoreCase))
                return new List<string> { "Install", "Uninstall", "IsInstalled", "Version", "Folder" };

            if (owner.EndsWith("ServiceLevel", StringComparison.OrdinalIgnoreCase))
                return new List<string>(Members["Services.ServiceLevel"]);

            // Переменная: ищем, каким типом её объявили, и подсказываем члены типа.
            string declared = DeclaredType(owner);
            if (declared != null && !string.Equals(declared, owner, StringComparison.OrdinalIgnoreCase))
                return MembersOf(declared);

            return new List<string>();
        }

        /// <summary>Тип из объявления «Services.Service agent = ...» для имени agent.</summary>
        string DeclaredType(string name)
        {
            if (string.IsNullOrEmpty(name) || name.IndexOf('.') >= 0) return null;

            try
            {
                var m = System.Text.RegularExpressions.Regex.Match(
                    editor.Text,
                    @"(?m)^\s*([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)\s+" +
                    System.Text.RegularExpressions.Regex.Escape(name) + @"\s*=");
                if (!m.Success) return null;

                string type = m.Groups[1].Value;
                if (string.Equals(type, "var", StringComparison.OrdinalIgnoreCase)) return null;
                return type;
            }
            catch { return null; }
        }

        List<string> Candidates()
        {
            var all = new List<string>(Globals);
            all.AddRange(ResourceSet.Names(ProjectDir()));

            string prefix = CurrentWord();
            if (prefix.Length > 0)
                all = all.Where(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();

            all.Sort(StringComparer.OrdinalIgnoreCase);
            return all;
        }

        // ---------------- показ ----------------

        void Show(int from, List<string> names)
        {
            if (names == null || names.Count == 0) { Hide(); return; }

            replaceFrom = from;

            list.BeginUpdate();
            list.Items.Clear();
            foreach (string n in names) list.Items.Add(n);
            list.EndUpdate();

            list.SelectedIndex = 0;

            Point caret = editor.GetPositionFromCharIndex(editor.SelectionStart);
            int x = editor.Left + caret.X;
            int y = editor.Top + caret.Y + 18;

            Control host = list.Parent;
            if (host != null)
            {
                if (x + list.Width > host.ClientSize.Width) x = Math.Max(0, host.ClientSize.Width - list.Width);
                if (y + list.Height > host.ClientSize.Height) y = Math.Max(0, caret.Y - list.Height);
            }

            list.Location = new Point(x, y);
            list.Visible = true;
            list.BringToFront();
        }

        public void Hide()
        {
            list.Visible = false;
            replaceFrom = -1;
        }

        void Filter()
        {
            if (replaceFrom < 0 || editor.SelectionStart < replaceFrom) { Hide(); return; }

            string typed = editor.Text.Substring(replaceFrom, editor.SelectionStart - replaceFrom);
            if (typed.Any(c => !char.IsLetterOrDigit(c) && c != '_')) { Hide(); return; }

            for (int i = 0; i < list.Items.Count; i++)
            {
                if (list.Items[i].ToString().StartsWith(typed, StringComparison.OrdinalIgnoreCase))
                {
                    list.SelectedIndex = i;
                    return;
                }
            }
        }

        void Accept()
        {
            if (list.SelectedItem == null || replaceFrom < 0) { Hide(); return; }

            string chosen = list.SelectedItem.ToString();
            int from = replaceFrom;
            int length = Math.Max(0, editor.SelectionStart - from);

            editor.Select(from, length);
            editor.SelectedText = chosen;

            Hide();
            editor.Focus();
        }

        // ---------------- разбор текста вокруг курсора ----------------

        /// <summary>Имя перед точкой: для Resources. вернёт Resources.</summary>
        string IdentBefore(int dotIndex)
        {
            if (dotIndex <= 0 || dotIndex > editor.TextLength) return null;

            string text = editor.Text;
            int end = dotIndex;
            int start = end;

            while (start > 0)
            {
                char c = text[start - 1];
                if (char.IsLetterOrDigit(c) || c == '_' || c == '.') start--;
                else break;
            }

            if (start >= end) return null;

            string chain = text.Substring(start, end - start);
            int lastDot = chain.LastIndexOf('.');
            return lastDot >= 0 ? chain.Substring(lastDot + 1) : chain;
        }

        int WordStart()
        {
            string text = editor.Text;
            int i = editor.SelectionStart;

            while (i > 0)
            {
                char c = text[i - 1];
                if (char.IsLetterOrDigit(c) || c == '_') i--;
                else break;
            }
            return i;
        }

        string CurrentWord()
        {
            int from = WordStart();
            return editor.Text.Substring(from, editor.SelectionStart - from);
        }
    }
}
