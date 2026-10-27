using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DupoStudio
{
    /// <summary>Редактор с вкладками — встраивается в центр окна проекта.</summary>
    public class CodeEditorForm : Form
    {
        public readonly TabControl Tabs = new TabControl();

        /// <summary>Папка открытого проекта — нужна подсказкам, чтобы знать ресурсы.</summary>
        public Func<string> ProjectDir = delegate { return null; };

        /// <summary>Курсор переехал — окну проекта пора обновить строку состояния.</summary>
        public event EventHandler CaretMoved;

        static readonly Font TabFont = new Font("Segoe UI", 9f);

        public CodeEditorForm()
        {
            Text = "Редактор";
            FormBorderStyle = FormBorderStyle.None;
            TopLevel = false;
            Dock = DockStyle.Fill;
            BackColor = Theme.Back;

            Tabs.Dock = DockStyle.Fill;
            Tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            Tabs.SizeMode = TabSizeMode.Normal;
            Tabs.ItemSize = new Size(170, 26);
            Tabs.Padding = new Point(10, 4);
            Tabs.BackColor = Theme.Back;

            Tabs.DrawItem += DrawTab;
            Tabs.SelectedIndexChanged += delegate { Raise(); };

            Controls.Add(Tabs);
        }

        void Raise()
        {
            var h = CaretMoved;
            if (h != null) h(this, EventArgs.Empty);
        }

        // ---------------- доступ к редакторам ----------------

        public CodeEditor Active { get { return EditorOf(Tabs.SelectedTab); } }

        public static CodeEditor EditorOf(TabPage page)
        {
            if (page == null || page.Controls.Count == 0) return null;
            var host = page.Controls[0] as EditorHost;
            return host == null ? null : host.Editor;
        }

        public int Count { get { return Tabs.TabPages.Count; } }

        public bool HasDirty
        {
            get
            {
                return Tabs.TabPages.Cast<TabPage>()
                    .Select(EditorOf)
                    .Any(ed => ed != null && ed.Dirty);
            }
        }

        // ---------------- файлы ----------------

        /// <summary>Открывает файл или переключается на уже открытую вкладку.</summary>
        public CodeEditor Open(string path)
        {
            foreach (TabPage page in Tabs.TabPages)
            {
                var existing = EditorOf(page);
                if (existing != null &&
                    string.Equals(existing.FilePath, path, StringComparison.OrdinalIgnoreCase))
                {
                    Tabs.SelectedTab = page;
                    return existing;
                }
            }

            var host = new EditorHost();
            host.Dock = DockStyle.Fill;
            host.Completion.ProjectDir = ProjectDir;

            CodeEditor editor = host.Editor;
            editor.FilePath = path;
            editor.JavaMode = path.EndsWith(".java", StringComparison.OrdinalIgnoreCase);
            editor.Text = File.ReadAllText(path, Encoding.UTF8);
            editor.Dirty = false;
            editor.SelectionChanged += delegate { Raise(); };

            var tab = new TabPage(Path.GetFileName(path));
            tab.BackColor = Theme.Back;
            tab.Controls.Add(host);

            Tabs.TabPages.Add(tab);
            Tabs.SelectedTab = tab;

            editor.HighlightNow();
            return editor;
        }

        /// <summary>Перекрасить открытые вкладки — после смены темы.</summary>
        public void Repaint()
        {
            BackColor = Theme.Back;
            Tabs.BackColor = Theme.Panel;

            foreach (TabPage page in Tabs.TabPages)
            {
                page.BackColor = Theme.Back;

                foreach (Control c in page.Controls)
                {
                    var host = c as EditorHost;
                    if (host == null) continue;

                    host.BackColor = Theme.Back;
                    host.Editor.BackColor = Theme.Back;
                    host.Editor.ForeColor = Theme.Text;
                    host.Editor.HighlightNow();
                    host.Invalidate(true);
                }
            }

            Invalidate(true);
        }

        /// <summary>Перечитать с диска всё, что открыто и не правилось.</summary>
        public void ReloadAll()
        {
            foreach (TabPage page in Tabs.TabPages)
                foreach (Control c in page.Controls)
                {
                    var host = c as EditorHost;
                    if (host == null) continue;

                    var ed = host.Editor;
                    if (ed.FilePath == null || !File.Exists(ed.FilePath)) continue;
                    if (ed.Dirty) continue;

                    try
                    {
                        ed.Text = File.ReadAllText(ed.FilePath, Encoding.UTF8);
                        ed.HighlightNow();
                    }
                    catch { }
                }
        }

        public void SaveActive()
        {
            Save(Active);
        }

        public void SaveAll()
        {
            foreach (TabPage page in Tabs.TabPages) Save(EditorOf(page));
        }

        static void Save(CodeEditor editor)
        {
            if (editor == null || editor.FilePath == null || !editor.Dirty) return;
            File.WriteAllText(editor.FilePath, editor.Text, new UTF8Encoding(false));
            editor.Dirty = false;
        }

        // ---------------- отрисовка вкладок ----------------

        void DrawTab(object sender, DrawItemEventArgs e)
        {
            var page = Tabs.TabPages[e.Index];
            bool active = Tabs.SelectedIndex == e.Index;

            using (var back = new SolidBrush(active ? Theme.Back : Theme.Chrome))
                e.Graphics.FillRectangle(back, e.Bounds);

            if (active)
                using (var line = new SolidBrush(Theme.Accent))
                    e.Graphics.FillRectangle(line, e.Bounds.X, e.Bounds.Y, e.Bounds.Width, 2);

            var ed = EditorOf(page);
            string title = page.Text + (ed != null && ed.Dirty ? " •" : "");

            TextRenderer.DrawText(e.Graphics, title, TabFont, e.Bounds,
                active ? Theme.Text : Theme.Dim,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
        }
    }
}
