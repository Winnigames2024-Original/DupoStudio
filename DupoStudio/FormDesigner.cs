using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DupoStudio
{
    /// <summary>Один элемент на форме.</summary>
    public class DesignItem
    {
        public string Kind = "Button";
        public string Name = "control1";
        public string Caption = "";
        public Rectangle Bounds = new Rectangle(20, 20, 120, 30);

        /// <summary>Есть ли у элемента обработчик нажатия.</summary>
        public bool Click;

        /// <summary>Настоящий контрол, который виден на холсте.</summary>
        public Control Live;

        public override string ToString() { return Name + " (" + Kind + ")"; }
    }

    /// <summary>Обёртка для окна свойств: показывает поля элемента по-русски.</summary>
    public class DesignItemProxy
    {
        readonly DesignItem item;
        readonly Action changed;

        public DesignItemProxy(DesignItem i, Action onChanged) { item = i; changed = onChanged; }

        [Category("Общее"), DisplayName("Имя"), Description("Имя переменной в коде")]
        public string Name
        {
            get { return item.Name; }
            set { item.Name = ProjectTemplates.Ident(value); Fire(); }
        }

        [Category("Общее"), DisplayName("Тип"), ReadOnly(true)]
        public string Kind { get { return item.Kind; } }

        [Category("Общее"), DisplayName("Надпись")]
        public string Caption
        {
            get { return item.Caption; }
            set { item.Caption = value ?? ""; Fire(); }
        }

        [Category("Расположение"), DisplayName("X")]
        public int X
        {
            get { return item.Bounds.X; }
            set { item.Bounds = new Rectangle(value, item.Bounds.Y, item.Bounds.Width, item.Bounds.Height); Fire(); }
        }

        [Category("Расположение"), DisplayName("Y")]
        public int Y
        {
            get { return item.Bounds.Y; }
            set { item.Bounds = new Rectangle(item.Bounds.X, value, item.Bounds.Width, item.Bounds.Height); Fire(); }
        }

        [Category("Размер"), DisplayName("Ширина")]
        public int Width
        {
            get { return item.Bounds.Width; }
            set { item.Bounds = new Rectangle(item.Bounds.X, item.Bounds.Y, Math.Max(8, value), item.Bounds.Height); Fire(); }
        }

        [Category("Размер"), DisplayName("Высота")]
        public int Height
        {
            get { return item.Bounds.Height; }
            set { item.Bounds = new Rectangle(item.Bounds.X, item.Bounds.Y, item.Bounds.Width, Math.Max(8, value)); Fire(); }
        }

        [Category("События"), DisplayName("Нажатие (Click)"),
         Description("Создать обработчик <имя>_Click в коде формы")]
        public bool Click
        {
            get { return item.Click; }
            set { item.Click = value; Fire(); }
        }

        void Fire() { var h = changed; if (h != null) h(); }
    }

    /// <summary>Свойства самой формы для окна свойств.</summary>
    public class DesignFormProxy
    {
        readonly FormDesignerPanel owner;
        public DesignFormProxy(FormDesignerPanel o) { owner = o; }

        [Category("Общее"), DisplayName("Класс")]
        public string Name
        {
            get { return owner.FormName; }
            set { owner.FormName = ProjectTemplates.Ident(value); owner.Touch(); }
        }

        [Category("Общее"), DisplayName("Заголовок")]
        public string Title
        {
            get { return owner.FormTitle; }
            set { owner.FormTitle = value ?? ""; owner.Touch(); }
        }

        [Category("Размер"), DisplayName("Ширина")]
        public int Width
        {
            get { return owner.FormSize.Width; }
            set { owner.FormSize = new Size(Math.Max(160, value), owner.FormSize.Height); owner.Touch(); }
        }

        [Category("Размер"), DisplayName("Высота")]
        public int Height
        {
            get { return owner.FormSize.Height; }
            set { owner.FormSize = new Size(owner.FormSize.Width, Math.Max(120, value)); owner.Touch(); }
        }
    }

    /// <summary>Тулбокс: список элементов, которые можно положить на форму.</summary>
    public class ToolboxPanel : Panel
    {
        public static readonly string[] Kinds =
        {
            "Button", "Label", "TextBox", "CheckBox", "RadioButton",
            "ListBox", "ComboBox", "ProgressBar", "PictureBox", "Panel", "GroupBox"
        };

        static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            { "Button", "Кнопка" }, { "Label", "Надпись" }, { "TextBox", "Поле ввода" },
            { "CheckBox", "Флажок" }, { "RadioButton", "Переключатель" }, { "ListBox", "Список" },
            { "ComboBox", "Выпадающий список" }, { "ProgressBar", "Полоса хода" },
            { "PictureBox", "Картинка" }, { "Panel", "Панель" }, { "GroupBox", "Рамка" }
        };

        readonly ListBox list = new ListBox();
        readonly Label hint = new Label();

        /// <summary>Что выбрано сейчас; пусто — режим указателя.</summary>
        public string Selected
        {
            get { return list.SelectedIndex <= 0 ? "" : Kinds[list.SelectedIndex - 1]; }
        }

        public event EventHandler SelectionChanged;

        public ToolboxPanel()
        {
            BackColor = Theme.Panel;

            hint.Dock = DockStyle.Bottom;
            hint.Height = 38;
            hint.ForeColor = Theme.Dim;
            hint.Padding = new Padding(8, 4, 8, 4);
            hint.Text = "Выберите элемент и щёлкните по форме.";
            Controls.Add(hint);

            list.Dock = DockStyle.Fill;
            list.BackColor = Theme.Panel;
            list.ForeColor = Theme.Text;
            list.BorderStyle = BorderStyle.None;
            list.ItemHeight = 22;
            list.IntegralHeight = false;
            list.Items.Add("Указатель");
            foreach (string k in Kinds) list.Items.Add(Names[k]);
            list.SelectedIndex = 0;
            list.SelectedIndexChanged += delegate
            {
                var h = SelectionChanged;
                if (h != null) h(this, EventArgs.Empty);
            };
            Controls.Add(list);
            list.BringToFront();
        }

        /// <summary>Вернуться к указателю после того, как элемент положили.</summary>
        public void Reset() { list.SelectedIndex = 0; }

        /// <summary>Русское название типа.</summary>
        public static string NameOf(string kind)
        {
            string s;
            return Names.TryGetValue(kind ?? "", out s) ? s : kind;
        }
    }

    /// <summary>
    /// Дизайнер форм: холст, на который кладут элементы из тулбокса.
    /// Файл с раскладкой — .dupoform, из него же пишется код на C#.
    /// </summary>
    public class FormDesignerPanel : Panel
    {
        public const string Extension = ".dupoform";

        readonly Panel canvas = new Panel();
        readonly Label caption = new Label();
        readonly List<DesignItem> items = new List<DesignItem>();

        DesignItem selected;
        Point grabFrom;
        bool dragging;

        public string FormName = "Form1";
        public string FormTitle = "Форма";
        public Size FormSize = new Size(480, 320);

        /// <summary>Файл, с которым работаем.</summary>
        public string FilePath { get; private set; }

        /// <summary>true — правим Form1.Designer.cs, false — .dupoform.</summary>
        public bool WinForms { get; private set; }

        /// <summary>Пространство имён формы WinForms.</summary>
        public string FormNamespace = "App";

        /// <summary>Открыть файл в редакторе кода — дизайнер просит.</summary>
        public event EventHandler<string> OpenCode;

        /// <summary>Есть ли несохранённые правки.</summary>
        public bool Dirty { get; private set; }

        /// <summary>Откуда брать выбранный в тулбоксе элемент.</summary>
        public Func<string> PickKind = delegate { return ""; };

        /// <summary>Элемент положили — тулбокс пора сбросить.</summary>
        public event EventHandler Placed;

        /// <summary>Выделение сменилось — окну свойств пора обновиться.</summary>
        public event EventHandler SelectionChanged;

        public FormDesignerPanel()
        {
            BackColor = Theme.Back;
            AutoScroll = true;

            caption.Dock = DockStyle.Top;
            caption.Height = 24;
            caption.ForeColor = Theme.Dim;
            caption.Padding = new Padding(10, 4, 0, 0);
            caption.Text = "Дизайнер форм — Del удаляет элемент";
            Controls.Add(caption);

            canvas.Location = new Point(24, 44);
            canvas.Size = FormSize;
            canvas.BackColor = Color.FromArgb(240, 240, 240);
            canvas.BorderStyle = BorderStyle.FixedSingle;
            canvas.MouseDown += CanvasDown;
            canvas.Paint += CanvasPaint;
            Controls.Add(canvas);

            KeyDown += KeyPressed;
        }

        /// <summary>Элементы формы.</summary>
        public IEnumerable<DesignItem> Items { get { return items; } }

        /// <summary>Что выделено.</summary>
        public DesignItem Selected { get { return selected; } }

        /// <summary>Объект для окна свойств: элемент или сама форма.</summary>
        public object PropertyTarget
        {
            get
            {
                if (selected != null) return new DesignItemProxy(selected, Refresh2);
                return new DesignFormProxy(this);
            }
        }

        public void Touch()
        {
            Dirty = true;
            canvas.Size = FormSize;
            caption.Text = "Дизайнер форм — " + FormName + " (" + FormSize.Width + "×" + FormSize.Height + ")";
            canvas.Invalidate();
        }

        void Refresh2()
        {
            if (selected != null && selected.Live != null)
            {
                selected.Live.Bounds = selected.Bounds;
                Caption(selected.Live, selected.Kind, selected.Caption);
            }
            Dirty = true;
            canvas.Invalidate();
        }

        // ------------------------------------------------------------------
        // добавление и выделение
        // ------------------------------------------------------------------

        void CanvasDown(object sender, MouseEventArgs e)
        {
            string kind = PickKind();
            if (!string.IsNullOrEmpty(kind))
            {
                Add(kind, e.Location);
                var h = Placed;
                if (h != null) h(this, EventArgs.Empty);
                return;
            }

            Select2(null);
        }

        /// <summary>Положить новый элемент.</summary>
        public DesignItem Add(string kind, Point at)
        {
            var item = new DesignItem();
            item.Kind = kind;
            item.Name = NextName(kind);
            item.Caption = DefaultCaption(kind, item.Name);
            item.Bounds = new Rectangle(at.X, at.Y, DefaultSize(kind).Width, DefaultSize(kind).Height);

            items.Add(item);
            Materialize(item);
            Select2(item);
            Dirty = true;
            return item;
        }

        string NextName(string kind)
        {
            string stem = kind.ToLowerInvariant();
            for (int i = 1; i < 999; i++)
            {
                string n = stem + i;
                if (!items.Any(x => string.Equals(x.Name, n, StringComparison.OrdinalIgnoreCase))) return n;
            }
            return stem + Guid.NewGuid().ToString("N").Substring(0, 4);
        }

        static string DefaultCaption(string kind, string name)
        {
            switch (kind)
            {
                case "Button": return "Кнопка";
                case "Label": return "Надпись";
                case "CheckBox": return "Флажок";
                case "RadioButton": return "Выбор";
                case "GroupBox": return "Рамка";
                case "TextBox": return "";
                default: return "";
            }
        }

        static Size DefaultSize(string kind)
        {
            switch (kind)
            {
                case "Label": return new Size(120, 20);
                case "TextBox": return new Size(160, 24);
                case "CheckBox":
                case "RadioButton": return new Size(140, 22);
                case "ListBox": return new Size(160, 90);
                case "ComboBox": return new Size(160, 24);
                case "ProgressBar": return new Size(180, 18);
                case "PictureBox": return new Size(120, 100);
                case "Panel": return new Size(180, 120);
                case "GroupBox": return new Size(200, 120);
                default: return new Size(120, 30);
            }
        }

        /// <summary>Создать настоящий контрол и повесить его на холст.</summary>
        void Materialize(DesignItem item)
        {
            Control c = Make(item.Kind);
            c.Bounds = item.Bounds;
            Caption(c, item.Kind, item.Caption);

            c.MouseDown += delegate (object s, MouseEventArgs e)
            {
                Select2(item);
                grabFrom = e.Location;
                dragging = e.Button == MouseButtons.Left;
            };
            c.MouseMove += delegate (object s, MouseEventArgs e)
            {
                if (!dragging) return;
                var p = new Point(c.Left + e.X - grabFrom.X, c.Top + e.Y - grabFrom.Y);
                p.X = Math.Max(0, (p.X / 4) * 4);
                p.Y = Math.Max(0, (p.Y / 4) * 4);
                c.Location = p;
                item.Bounds = c.Bounds;
                Dirty = true;
                var h = SelectionChanged;
                if (h != null) h(this, EventArgs.Empty);
            };
            c.MouseUp += delegate { dragging = false; };
            c.DoubleClick += delegate { WireClick(item); };

            item.Live = c;
            canvas.Controls.Add(c);
        }

        /// <summary>
        /// Двойной щелчок по элементу создаёт обработчик нажатия
        /// и открывает код формы — как в Visual Studio.
        /// </summary>
        void WireClick(DesignItem item)
        {
            if (item == null || !WinFormsCode.CanClick(item.Kind)) return;

            item.Click = true;
            Dirty = true;

            var sel = SelectionChanged;
            if (sel != null) sel(this, EventArgs.Empty);

            if (!WinForms) return;

            try
            {
                SaveWinForms(false);
                string code = WinFormsCode.CodeFileOf(FilePath);
                var h = OpenCode;
                if (h != null && code != null) h(this, code);
            }
            catch { }
        }

        static Control Make(string kind)
        {
            switch (kind)
            {
                case "Label": return new Label();
                case "TextBox": return new TextBox();
                case "CheckBox": return new CheckBox();
                case "RadioButton": return new RadioButton();
                case "ListBox": return new ListBox();
                case "ComboBox": return new ComboBox();
                case "ProgressBar": return new ProgressBar { Value = 40 };
                case "PictureBox": return new PictureBox { BorderStyle = BorderStyle.FixedSingle };
                case "Panel": return new Panel { BorderStyle = BorderStyle.FixedSingle };
                case "GroupBox": return new GroupBox();
                default: return new Button();
            }
        }

        static void Caption(Control c, string kind, string text)
        {
            if (kind == "PictureBox" || kind == "Panel" || kind == "ListBox") return;
            c.Text = text ?? "";
        }

        void Select2(DesignItem item)
        {
            selected = item;
            canvas.Invalidate();
            var h = SelectionChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        void CanvasPaint(object sender, PaintEventArgs e)
        {
            // Сетка, по которой выравниваются элементы.
            using (var pen = new Pen(Color.FromArgb(225, 225, 225)))
                for (int x = 0; x < canvas.Width; x += 8)
                    for (int y = 0; y < canvas.Height; y += 8)
                        e.Graphics.DrawRectangle(pen, x, y, 1, 1);

            if (selected == null || selected.Live == null) return;

            var r = selected.Live.Bounds;
            r.Inflate(2, 2);
            using (var pen = new Pen(Theme.Accent, 2)) e.Graphics.DrawRectangle(pen, r);
        }

        void KeyPressed(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Delete || selected == null) return;
            Remove(selected);
            e.Handled = true;
        }

        /// <summary>Убрать элемент.</summary>
        public void Remove(DesignItem item)
        {
            if (item == null) return;
            if (item.Live != null) { canvas.Controls.Remove(item.Live); item.Live.Dispose(); }
            items.Remove(item);
            Dirty = true;
            Select2(null);
        }

        /// <summary>Убрать выделенный элемент.</summary>
        public void RemoveSelected() { Remove(selected); }

        // ------------------------------------------------------------------
        // файл .dupoform
        // ------------------------------------------------------------------

        /// <summary>Начать новую форму.</summary>
        public void Reset(string path, string name)
        {
            foreach (var i in items.ToList()) if (i.Live != null) { canvas.Controls.Remove(i.Live); i.Live.Dispose(); }
            items.Clear();
            selected = null;

            FilePath = path;
            WinForms = WinFormsCode.IsDesigner(path);
            FormName = ProjectTemplates.Ident(name);
            FormTitle = name;
            FormSize = new Size(480, 320);
            Dirty = false;
            Touch();
        }

        // ------------------------------------------------------------------
        // Windows Forms: Form1.Designer.cs
        // ------------------------------------------------------------------

        /// <summary>Прочитать Form1.Designer.cs в дизайнер.</summary>
        public void LoadWinForms(string designerPath)
        {
            var form = WinFormsCode.Read(designerPath);

            Reset(designerPath, form.ClassName);
            WinForms = true;
            FormNamespace = form.Namespace;
            FormName = form.ClassName;
            FormTitle = form.Title;
            FormSize = form.ClientSize;

            foreach (var i in form.Items)
            {
                items.Add(i);
                Materialize(i);
            }

            Dirty = false;
            Touch();
            Dirty = false;
        }

        /// <summary>Записать Form1.Designer.cs (и Form1.cs, если его нет).</summary>
        public string SaveWinForms(bool withMain)
        {
            if (string.IsNullOrEmpty(FilePath))
                throw new InvalidOperationException("не задан файл Designer.cs");

            var form = new WinFormsForm();
            form.DesignerPath = FilePath;
            form.Namespace = FormNamespace;
            form.ClassName = FormName;
            form.Title = FormTitle;
            form.ClientSize = FormSize;
            form.Items.AddRange(items);

            WinFormsCode.Write(form, withMain);
            Dirty = false;
            return FilePath;
        }

        /// <summary>Сохранить так, как положено текущему файлу.</summary>
        public string SaveAny()
        {
            return WinForms ? SaveWinForms(false) : Save(null);
        }

        /// <summary>Прочитать .dupoform.</summary>
        public void Load(string path)
        {
            Reset(path, Path.GetFileNameWithoutExtension(path));

            foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                var parts = Split(line);
                if (parts.Count == 0) continue;

                if (string.Equals(parts[0], "form", StringComparison.OrdinalIgnoreCase))
                {
                    if (parts.Count > 1) FormName = parts[1];
                    if (parts.Count > 2) FormTitle = parts[2];
                    if (parts.Count > 4) FormSize = new Size(Int(parts[3], 480), Int(parts[4], 320));
                    continue;
                }

                if (parts.Count < 7) continue;

                var item = new DesignItem();
                item.Kind = parts[0];
                item.Name = parts[1];
                item.Caption = parts[2];
                item.Bounds = new Rectangle(Int(parts[3], 0), Int(parts[4], 0),
                                            Int(parts[5], 100), Int(parts[6], 24));
                items.Add(item);
                Materialize(item);
            }

            Dirty = false;
            Touch();
            Dirty = false;
        }

        /// <summary>Записать .dupoform.</summary>
        public string Save(string path)
        {
            string target = string.IsNullOrEmpty(path) ? FilePath : path;
            if (string.IsNullOrEmpty(target)) throw new InvalidOperationException("не задан файл формы");

            var sb = new StringBuilder();
            sb.AppendLine("# Форма Dupo Studio");
            sb.AppendLine("form " + FormName + " " + Quote(FormTitle) + " " +
                          FormSize.Width + " " + FormSize.Height);

            foreach (var i in items)
                sb.AppendLine(i.Kind + " " + i.Name + " " + Quote(i.Caption) + " " +
                              i.Bounds.X + " " + i.Bounds.Y + " " +
                              i.Bounds.Width + " " + i.Bounds.Height);

            File.WriteAllText(target, sb.ToString(), new UTF8Encoding(false));
            FilePath = target;
            Dirty = false;
            return target;
        }

        static string Quote(string s) { return "\"" + (s ?? "").Replace("\"", "'") + "\""; }

        static int Int(string s, int fallback)
        {
            int v;
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        /// <summary>Разбор строки с учётом кавычек.</summary>
        static List<string> Split(string line)
        {
            var res = new List<string>();
            var sb = new StringBuilder();
            bool q = false;

            foreach (char c in line)
            {
                if (c == '"') { q = !q; continue; }
                if (c == ' ' && !q)
                {
                    if (sb.Length > 0) { res.Add(sb.ToString()); sb.Clear(); }
                    continue;
                }
                sb.Append(c);
            }
            if (sb.Length > 0) res.Add(sb.ToString());
            return res;
        }

        // ------------------------------------------------------------------
        // код
        // ------------------------------------------------------------------

        /// <summary>Сгенерировать класс C# по форме.</summary>
        public string ToCSharp(string ns)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Drawing;");
            sb.AppendLine("using System.Windows.Forms;");
            sb.AppendLine();
            sb.AppendLine("namespace " + (string.IsNullOrEmpty(ns) ? "App" : ns));
            sb.AppendLine("{");
            sb.AppendLine("    public class " + FormName + " : Form");
            sb.AppendLine("    {");

            foreach (var i in items)
                sb.AppendLine("        readonly " + i.Kind + " " + i.Name + " = new " + i.Kind + "();");

            sb.AppendLine();
            sb.AppendLine("        public " + FormName + "()");
            sb.AppendLine("        {");
            sb.AppendLine("            Text = \"" + Escape(FormTitle) + "\";");
            sb.AppendLine("            ClientSize = new Size(" + FormSize.Width + ", " + FormSize.Height + ");");
            sb.AppendLine("            StartPosition = FormStartPosition.CenterScreen;");
            sb.AppendLine("            Font = new Font(\"Segoe UI\", 9f);");

            foreach (var i in items)
            {
                sb.AppendLine();
                sb.AppendLine("            " + i.Name + ".Bounds = new Rectangle(" +
                              i.Bounds.X + ", " + i.Bounds.Y + ", " +
                              i.Bounds.Width + ", " + i.Bounds.Height + ");");

                if (NeedsText(i.Kind))
                    sb.AppendLine("            " + i.Name + ".Text = \"" + Escape(i.Caption) + "\";");

                if (i.Kind == "Button")
                    sb.AppendLine("            " + i.Name + ".Click += delegate { /* ваш код */ };");

                sb.AppendLine("            Controls.Add(" + i.Name + ");");
            }

            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        [STAThread]");
            sb.AppendLine("        static void Main()");
            sb.AppendLine("        {");
            sb.AppendLine("            Application.EnableVisualStyles();");
            sb.AppendLine("            Application.SetCompatibleTextRenderingDefault(false);");
            sb.AppendLine("            Application.Run(new " + FormName + "());");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        static bool NeedsText(string kind)
        {
            return kind != "PictureBox" && kind != "Panel" && kind != "ListBox" &&
                   kind != "ComboBox" && kind != "ProgressBar";
        }

        static string Escape(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
