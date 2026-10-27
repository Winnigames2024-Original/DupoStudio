using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DupoLang
{
    /// <summary>
    /// Окно на DupoLang. Форма рисуется конструктором студии в файл
    /// .dupoform, а запускается прямо из языка — без C# и без папки src.
    ///
    /// <code>
    /// using Dupo.Forms
    ///
    /// Forms.Form main = Forms.Open("Form1.dupoform")
    /// main.On("button1", "Нажали")
    /// main.Show()
    ///
    /// def Нажали(кто) {
    ///     main.Set("label1", "Привет из DupoLang!")
    /// }
    /// </code>
    /// </summary>
    public class DupoForm
    {
        public static Action<string> Log = delegate (string s) { Console.WriteLine(s); };

        /// <summary>Кто вызывает блоки обработчиков — ставится при подключении модуля.</summary>
        public static Func<string, object[], object> Handler;

        readonly Form form = new Form();
        readonly Dictionary<string, Control> items =
            new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Откуда форма прочитана.</summary>
        public string FilePath = "";

        public DupoForm() : this("Форма", 480, 320) { }

        public DupoForm(string title) : this(title, 480, 320) { }

        public DupoForm(string title, double width, double height)
        {
            form.Text = title ?? "Форма";
            form.ClientSize = new Size((int)Math.Max(160, width), (int)Math.Max(120, height));
            form.StartPosition = FormStartPosition.CenterScreen;
            form.Font = new Font("Segoe UI", 9f);
        }

        // ------------------------------------------------------------------
        // свойства формы
        // ------------------------------------------------------------------

        public string Title
        {
            get { return form.Text; }
            set { form.Text = value ?? ""; }
        }

        public double Width
        {
            get { return form.ClientSize.Width; }
            set { form.ClientSize = new Size((int)value, form.ClientSize.Height); }
        }

        public double Height
        {
            get { return form.ClientSize.Height; }
            set { form.ClientSize = new Size(form.ClientSize.Width, (int)value); }
        }

        /// <summary>Имена всех элементов.</summary>
        public List<object> Names
        {
            get { return items.Keys.Select(x => (object)x).ToList(); }
        }

        /// <summary>Настоящее окно — для тех, кто пишет на C#.</summary>
        public Form Window { get { return form; } }

        // ------------------------------------------------------------------
        // элементы
        // ------------------------------------------------------------------

        /// <summary>Добавить элемент: тип, имя, надпись и место.</summary>
        public DupoForm Add(string kind, string name, string caption,
                            double x, double y, double w, double h)
        {
            Control c = Make(kind);
            c.Name = name;
            c.Bounds = new Rectangle((int)x, (int)y, (int)w, (int)h);

            if (HasText(kind)) c.Text = caption ?? "";

            items[name] = c;
            form.Controls.Add(c);
            return this;
        }

        public DupoForm Button(string name, string caption, double x, double y, double w, double h)
        {
            return Add("Button", name, caption, x, y, w, h);
        }

        public DupoForm Label(string name, string caption, double x, double y, double w, double h)
        {
            return Add("Label", name, caption, x, y, w, h);
        }

        public DupoForm TextBox(string name, string text, double x, double y, double w, double h)
        {
            return Add("TextBox", name, text, x, y, w, h);
        }

        /// <summary>Надпись или текст элемента.</summary>
        public string Get(string name)
        {
            Control c = Find(name);
            if (c == null) return "";

            var box = c as CheckBox;
            if (box != null) return box.Checked ? "true" : "false";

            var list = c as ListBox;
            if (list != null) return list.SelectedItem == null ? "" : list.SelectedItem.ToString();

            return c.Text;
        }

        /// <summary>Поменять надпись или текст элемента.</summary>
        public DupoForm Set(string name, object value)
        {
            Control c = Find(name);
            if (c == null) return this;

            var box = c as CheckBox;
            if (box != null) { box.Checked = Val.Truth(value); return this; }

            var bar = c as ProgressBar;
            if (bar != null)
            {
                bar.Value = Math.Max(bar.Minimum, Math.Min(bar.Maximum, (int)Val.Num(value)));
                return this;
            }

            c.Text = Val.Str(value);
            return this;
        }

        /// <summary>Отмечен ли флажок.</summary>
        public bool Checked(string name)
        {
            var box = Find(name) as CheckBox;
            return box != null && box.Checked;
        }

        /// <summary>Добавить строку в список.</summary>
        public DupoForm Push(string name, object value)
        {
            var list = Find(name) as ListBox;
            if (list != null) { list.Items.Add(Val.Str(value)); return this; }

            var combo = Find(name) as ComboBox;
            if (combo != null) combo.Items.Add(Val.Str(value));
            return this;
        }

        /// <summary>Очистить список.</summary>
        public DupoForm Clear(string name)
        {
            var list = Find(name) as ListBox;
            if (list != null) { list.Items.Clear(); return this; }

            var combo = Find(name) as ComboBox;
            if (combo != null) combo.Items.Clear();
            return this;
        }

        /// <summary>Показать картинку из папки assets.</summary>
        public DupoForm SetImage(string name, object resource)
        {
            var box = Find(name) as PictureBox;
            if (box == null) return this;

            string path = Val.Str(resource);
            try
            {
                if (File.Exists(path))
                {
                    box.Image = Image.FromFile(path);
                    box.SizeMode = PictureBoxSizeMode.Zoom;
                }
                else Log("Картинка не найдена: " + path);
            }
            catch (Exception ex) { Log("Картинка не открылась: " + ex.Message); }

            return this;
        }

        /// <summary>Включить или выключить элемент.</summary>
        public DupoForm Enable(string name, bool on)
        {
            Control c = Find(name);
            if (c != null) c.Enabled = on;
            return this;
        }

        /// <summary>Показать или спрятать элемент.</summary>
        public DupoForm ShowItem(string name, bool on)
        {
            Control c = Find(name);
            if (c != null) c.Visible = on;
            return this;
        }

        Control Find(string name)
        {
            Control c;
            if (items.TryGetValue((name ?? "").Trim(), out c)) return c;

            Log("На форме нет элемента '" + name + "'");
            return null;
        }

        // ------------------------------------------------------------------
        // обработчики
        // ------------------------------------------------------------------

        /// <summary>Привязать блок def к нажатию на элемент.</summary>
        public DupoForm On(string name, string def)
        {
            return On(name, "click", def);
        }

        /// <summary>Привязать блок def к событию: click, change, close.</summary>
        public DupoForm On(string name, string what, string def)
        {
            string block = (def ?? "").Trim();
            if (block.Length == 0) return this;

            string kind = (what ?? "click").Trim().ToLowerInvariant();

            if (kind == "close")
            {
                form.FormClosing += delegate { Call(block, name); };
                return this;
            }

            Control c = Find(name);
            if (c == null) return this;

            switch (kind)
            {
                case "change":
                    var box = c as CheckBox;
                    if (box != null) box.CheckedChanged += delegate { Call(block, name); };
                    else c.TextChanged += delegate { Call(block, name); };
                    break;

                case "select":
                    var list = c as ListBox;
                    if (list != null) list.SelectedIndexChanged += delegate { Call(block, name); };
                    else
                    {
                        var combo = c as ComboBox;
                        if (combo != null) combo.SelectedIndexChanged += delegate { Call(block, name); };
                    }
                    break;

                default:
                    c.Click += delegate { Call(block, name); };
                    break;
            }

            return this;
        }

        void Call(string def, string sender)
        {
            var h = Handler;
            if (h == null)
            {
                Log("Обработчик '" + def + "' некому вызвать");
                return;
            }

            try { h(def, new object[] { sender }); }
            catch (Exception ex) { Log("В блоке '" + def + "': " + ex.Message); }
        }

        // ------------------------------------------------------------------
        // показ
        // ------------------------------------------------------------------

        /// <summary>Показать форму и ждать, пока её закроют.</summary>
        public DupoForm Show()
        {
            Application.EnableVisualStyles();
            Application.Run(form);
            return this;
        }

        /// <summary>Закрыть форму.</summary>
        public DupoForm Close()
        {
            try { form.Close(); }
            catch { }
            return this;
        }

        /// <summary>Сообщение в окошке.</summary>
        public static string Message(string text)
        {
            MessageBox.Show(Val.Str(text), "Dupo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return text;
        }

        /// <summary>Вопрос «да или нет».</summary>
        public static bool Ask(string text)
        {
            return MessageBox.Show(Val.Str(text), "Dupo",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        // ------------------------------------------------------------------
        // чтение .dupoform
        // ------------------------------------------------------------------

        /// <summary>Открыть форму, нарисованную конструктором студии.</summary>
        public static DupoForm Open(string path)
        {
            string file = DupoRuntime.Resolve(path);
            if (!File.Exists(file)) throw new FileNotFoundException("форма не найдена: " + file);

            var result = new DupoForm();
            result.FilePath = file;

            foreach (string raw in File.ReadAllLines(file, Encoding.UTF8))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                var parts = Split(line);
                if (parts.Count == 0) continue;

                if (string.Equals(parts[0], "form", StringComparison.OrdinalIgnoreCase))
                {
                    if (parts.Count > 2) result.Title = parts[2];
                    if (parts.Count > 4)
                    {
                        result.Width = Int(parts[3], 480);
                        result.Height = Int(parts[4], 320);
                    }
                    continue;
                }

                if (parts.Count < 7) continue;

                result.Add(parts[0], parts[1], parts[2],
                           Int(parts[3], 0), Int(parts[4], 0),
                           Int(parts[5], 100), Int(parts[6], 24));
            }

            Log("Форма открыта: " + Path.GetFileName(file) + ", элементов: " + result.items.Count);
            return result;
        }

        static int Int(string s, int fallback)
        {
            int v;
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

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
                    if (sb.Length > 0) { res.Add(sb.ToString()); sb.Length = 0; }
                    continue;
                }
                sb.Append(c);
            }
            if (sb.Length > 0) res.Add(sb.ToString());
            return res;
        }

        static bool HasText(string kind)
        {
            return kind != "PictureBox" && kind != "Panel" && kind != "ListBox" &&
                   kind != "ComboBox" && kind != "ProgressBar";
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
                case "ProgressBar": return new ProgressBar();
                case "PictureBox": return new PictureBox { BorderStyle = BorderStyle.FixedSingle };
                case "Panel": return new Panel { BorderStyle = BorderStyle.FixedSingle };
                case "GroupBox": return new GroupBox();
                default: return new Button();
            }
        }

        public override string ToString() { return "<форма " + Title + ">"; }
    }
}
