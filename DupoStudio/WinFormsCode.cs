using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DupoStudio
{
    /// <summary>Форма Windows Forms, разобранная из Designer.cs.</summary>
    public class WinFormsForm
    {
        public string Namespace = "App";
        public string ClassName = "Form1";
        public string Title = "Form1";
        public Size ClientSize = new Size(480, 320);
        public readonly List<DesignItem> Items = new List<DesignItem>();

        /// <summary>Файл Form1.Designer.cs.</summary>
        public string DesignerPath;

        /// <summary>Файл Form1.cs — код, который пишет человек.</summary>
        public string CodePath
        {
            get
            {
                if (string.IsNullOrEmpty(DesignerPath)) return null;
                return WinFormsCode.CodeFileOf(DesignerPath);
            }
        }
    }

    /// <summary>
    /// Чтение и запись Designer.cs, как это делает конструктор форм
    /// Visual Studio: разбирает InitializeComponent и собирает его заново.
    /// </summary>
    public static class WinFormsCode
    {
        public const string DesignerSuffix = ".Designer.cs";

        /// <summary>Типы, которые понимает дизайнер.</summary>
        public static readonly string[] Known = ToolboxPanel.Kinds;

        // ------------------------------------------------------------------
        // пути
        // ------------------------------------------------------------------

        /// <summary>Это файл Form1.Designer.cs?</summary>
        public static bool IsDesigner(string path)
        {
            return path != null && path.EndsWith(DesignerSuffix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Form1.cs для Form1.Designer.cs.</summary>
        public static string CodeFileOf(string designerPath)
        {
            if (!IsDesigner(designerPath)) return designerPath;
            return designerPath.Substring(0, designerPath.Length - DesignerSuffix.Length) + ".cs";
        }

        /// <summary>Form1.Designer.cs для Form1.cs; null — такого нет.</summary>
        public static string DesignerFileOf(string codePath)
        {
            if (codePath == null || IsDesigner(codePath)) return codePath;
            if (!codePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return null;

            string guess = codePath.Substring(0, codePath.Length - 3) + DesignerSuffix;
            return File.Exists(guess) ? guess : null;
        }

        // ------------------------------------------------------------------
        // чтение
        // ------------------------------------------------------------------

        /// <summary>Разобрать Designer.cs.</summary>
        public static WinFormsForm Read(string designerPath)
        {
            var form = Parse(File.ReadAllText(designerPath, Encoding.UTF8));
            form.DesignerPath = designerPath;
            if (string.IsNullOrEmpty(form.ClassName))
                form.ClassName = Path.GetFileName(CodeFileOf(designerPath)).Replace(".cs", "");
            return form;
        }

        /// <summary>Разобрать текст Designer.cs.</summary>
        public static WinFormsForm Parse(string code)
        {
            var form = new WinFormsForm();
            string text = code ?? "";

            var ns = Regex.Match(text, @"namespace\s+([A-Za-z_][\w.]*)");
            if (ns.Success) form.Namespace = ns.Groups[1].Value;

            var cls = Regex.Match(text, @"partial\s+class\s+([A-Za-z_]\w*)");
            if (cls.Success) form.ClassName = cls.Groups[1].Value;

            // Порядок важен: сначала объявления элементов, потом их свойства.
            var byName = new Dictionary<string, DesignItem>(StringComparer.Ordinal);
            var order = new List<string>();

            foreach (Match m in Regex.Matches(text,
                @"this\.([A-Za-z_]\w*)\s*=\s*new\s+(?:System\.Windows\.Forms\.)?([A-Za-z_]\w*)\s*\(\s*\)"))
            {
                string name = m.Groups[1].Value;
                string kind = m.Groups[2].Value;

                if (!Known.Contains(kind, StringComparer.Ordinal)) continue;
                if (byName.ContainsKey(name)) continue;

                var item = new DesignItem();
                item.Name = name;
                item.Kind = kind;
                item.Caption = "";
                item.Bounds = new Rectangle(8, 8, 100, 24);

                byName[name] = item;
                order.Add(name);
            }

            foreach (Match m in Regex.Matches(text,
                @"this\.([A-Za-z_]\w*)\.Location\s*=\s*new\s+System\.Drawing\.Point\(\s*(-?\d+)\s*,\s*(-?\d+)\s*\)"))
            {
                DesignItem item;
                if (!byName.TryGetValue(m.Groups[1].Value, out item)) continue;
                item.Bounds = new Rectangle(Int(m.Groups[2].Value), Int(m.Groups[3].Value),
                                            item.Bounds.Width, item.Bounds.Height);
            }

            foreach (Match m in Regex.Matches(text,
                @"this\.([A-Za-z_]\w*)\.Size\s*=\s*new\s+System\.Drawing\.Size\(\s*(\d+)\s*,\s*(\d+)\s*\)"))
            {
                DesignItem item;
                if (!byName.TryGetValue(m.Groups[1].Value, out item)) continue;
                item.Bounds = new Rectangle(item.Bounds.X, item.Bounds.Y,
                                            Int(m.Groups[2].Value), Int(m.Groups[3].Value));
            }

            foreach (Match m in Regex.Matches(text,
                @"this\.([A-Za-z_]\w*)\.Text\s*=\s*""((?:[^""\\]|\\.)*)"""))
            {
                DesignItem item;
                if (!byName.TryGetValue(m.Groups[1].Value, out item)) continue;
                item.Caption = Unescape(m.Groups[2].Value);
            }

            foreach (Match m in Regex.Matches(text,
                @"this\.([A-Za-z_]\w*)\.Click\s*\+="))
            {
                DesignItem item;
                if (byName.TryGetValue(m.Groups[1].Value, out item)) item.Click = true;
            }

            // Свойства самой формы: this.Text, а не this.что-то.Text.
            var size = Regex.Match(text,
                @"this\.ClientSize\s*=\s*new\s+System\.Drawing\.Size\(\s*(\d+)\s*,\s*(\d+)\s*\)");
            if (size.Success) form.ClientSize = new Size(Int(size.Groups[1].Value), Int(size.Groups[2].Value));

            var title = Regex.Match(text, @"this\.Text\s*=\s*""((?:[^""\\]|\\.)*)""");
            form.Title = title.Success ? Unescape(title.Groups[1].Value) : form.ClassName;

            // Порядок на форме — как в Controls.Add, остальные следом.
            var added = Regex.Matches(text, @"this\.Controls\.Add\(\s*this\.([A-Za-z_]\w*)\s*\)")
                             .Cast<Match>().Select(m => m.Groups[1].Value).ToList();

            foreach (string name in added)
            {
                DesignItem item;
                if (byName.TryGetValue(name, out item) && !form.Items.Contains(item)) form.Items.Add(item);
            }
            foreach (string name in order)
            {
                DesignItem item = byName[name];
                if (!form.Items.Contains(item)) form.Items.Add(item);
            }

            return form;
        }

        // ------------------------------------------------------------------
        // запись
        // ------------------------------------------------------------------

        /// <summary>Собрать текст Designer.cs.</summary>
        public static string Designer(WinFormsForm form)
        {
            var sb = new StringBuilder();
            string cls = form.ClassName;

            sb.AppendLine("namespace " + form.Namespace);
            sb.AppendLine("{");
            sb.AppendLine("    partial class " + cls);
            sb.AppendLine("    {");
            sb.AppendLine("        /// <summary>Требуется переменная конструктора.</summary>");
            sb.AppendLine("        private System.ComponentModel.IContainer components = null;");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>Освободить занятые ресурсы.</summary>");
            sb.AppendLine("        protected override void Dispose(bool disposing)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (disposing && (components != null)) components.Dispose();");
            sb.AppendLine("            base.Dispose(disposing);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        #region Код, созданный конструктором форм — правится дизайнером Dupo Studio");
            sb.AppendLine();
            sb.AppendLine("        private void InitializeComponent()");
            sb.AppendLine("        {");

            foreach (var i in form.Items)
                sb.AppendLine("            this." + i.Name + " = new System.Windows.Forms." + i.Kind + "();");

            sb.AppendLine("            this.SuspendLayout();");

            int tab = 0;
            foreach (var i in form.Items)
            {
                sb.AppendLine("            // ");
                sb.AppendLine("            // " + i.Name);
                sb.AppendLine("            // ");
                sb.AppendLine("            this." + i.Name + ".Location = new System.Drawing.Point(" +
                              i.Bounds.X + ", " + i.Bounds.Y + ");");
                sb.AppendLine("            this." + i.Name + ".Name = \"" + i.Name + "\";");
                sb.AppendLine("            this." + i.Name + ".Size = new System.Drawing.Size(" +
                              i.Bounds.Width + ", " + i.Bounds.Height + ");");
                sb.AppendLine("            this." + i.Name + ".TabIndex = " + (tab++) + ";");

                if (HasText(i.Kind))
                    sb.AppendLine("            this." + i.Name + ".Text = \"" + Escape(i.Caption) + "\";");

                if (i.Kind == "Button" || i.Kind == "CheckBox" || i.Kind == "RadioButton")
                    sb.AppendLine("            this." + i.Name + ".UseVisualStyleBackColor = true;");

                if (i.Click && CanClick(i.Kind))
                    sb.AppendLine("            this." + i.Name + ".Click += new System.EventHandler(this." +
                                  i.Name + "_Click);");
            }

            sb.AppendLine("            // ");
            sb.AppendLine("            // " + cls);
            sb.AppendLine("            // ");
            sb.AppendLine("            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);");
            sb.AppendLine("            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;");
            sb.AppendLine("            this.ClientSize = new System.Drawing.Size(" +
                          form.ClientSize.Width + ", " + form.ClientSize.Height + ");");

            foreach (var i in form.Items)
                sb.AppendLine("            this.Controls.Add(this." + i.Name + ");");

            sb.AppendLine("            this.Name = \"" + cls + "\";");
            sb.AppendLine("            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;");
            sb.AppendLine("            this.Text = \"" + Escape(form.Title) + "\";");
            sb.AppendLine("            this.ResumeLayout(false);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        #endregion");
            sb.AppendLine();

            foreach (var i in form.Items)
                sb.AppendLine("        private System.Windows.Forms." + i.Kind + " " + i.Name + ";");

            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>Собрать текст Form1.cs — то, что пишет человек.</summary>
        public static string CodeBehind(WinFormsForm form, bool withMain)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Drawing;");
            sb.AppendLine("using System.Windows.Forms;");
            sb.AppendLine();
            sb.AppendLine("namespace " + form.Namespace);
            sb.AppendLine("{");
            sb.AppendLine("    public partial class " + form.ClassName + " : Form");
            sb.AppendLine("    {");
            sb.AppendLine("        public " + form.ClassName + "()");
            sb.AppendLine("        {");
            sb.AppendLine("            InitializeComponent();");
            sb.AppendLine("        }");

            foreach (var i in form.Items)
            {
                if (!i.Click || !CanClick(i.Kind)) continue;
                sb.AppendLine();
                sb.AppendLine(Handler(i.Name));
            }

            if (withMain)
            {
                sb.AppendLine();
                sb.AppendLine("        [STAThread]");
                sb.AppendLine("        static void Main()");
                sb.AppendLine("        {");
                sb.AppendLine("            Application.EnableVisualStyles();");
                sb.AppendLine("            Application.SetCompatibleTextRenderingDefault(false);");
                sb.AppendLine("            Application.Run(new " + form.ClassName + "());");
                sb.AppendLine("        }");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>Текст обработчика нажатия.</summary>
        public static string Handler(string name)
        {
            var sb = new StringBuilder();
            sb.AppendLine("        private void " + name + "_Click(object sender, EventArgs e)");
            sb.AppendLine("        {");
            sb.AppendLine("            // Dupo Studio: ваш код здесь.");
            sb.AppendLine("            // Можно позвать блок def из .dupolang:");
            sb.AppendLine("            // MessageBox.Show(DupoLang.Val.Str(");
            sb.AppendLine("            //     DupoLang.Dupo.Lang.ExecuteFrom(\"logic.dupolang\", \"Привет\", \"мир\")));");
            sb.Append("        }");
            return sb.ToString();
        }

        /// <summary>
        /// Дописать обработчик в Form1.cs, если его там ещё нет.
        /// Возвращает true, если файл изменился.
        /// </summary>
        public static bool AddHandler(string codePath, string name)
        {
            if (!File.Exists(codePath)) return false;

            string text = File.ReadAllText(codePath, Encoding.UTF8);
            if (Regex.IsMatch(text, @"\b" + Regex.Escape(name) + @"_Click\s*\(")) return false;

            // Вставляем перед последней закрывающей скобкой класса.
            int lastBrace = text.LastIndexOf('}');
            if (lastBrace < 0) return false;

            int classBrace = text.LastIndexOf('}', lastBrace - 1);
            if (classBrace < 0) return false;

            string insert = Environment.NewLine + Handler(name) + Environment.NewLine;
            text = text.Substring(0, classBrace) + insert + text.Substring(classBrace);

            File.WriteAllText(codePath, text, new UTF8Encoding(false));
            return true;
        }

        /// <summary>Записать обе половины формы. Form1.cs не трогаем, если он уже есть.</summary>
        public static void Write(WinFormsForm form, bool withMain)
        {
            string designer = form.DesignerPath;
            if (string.IsNullOrEmpty(designer)) throw new InvalidOperationException("не задан файл Designer.cs");

            string dir = Path.GetDirectoryName(designer);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(designer, Designer(form), new UTF8Encoding(false));

            string code = form.CodePath;
            if (!string.IsNullOrEmpty(code) && !File.Exists(code))
                File.WriteAllText(code, CodeBehind(form, withMain), new UTF8Encoding(false));

            // Обработчики, которых ещё нет в Form1.cs, дописываем.
            if (!string.IsNullOrEmpty(code) && File.Exists(code))
                foreach (var i in form.Items)
                    if (i.Click && CanClick(i.Kind)) AddHandler(code, i.Name);
        }

        /// <summary>Program.cs с точкой входа, если его нет рядом.</summary>
        public static string Program(WinFormsForm form)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Windows.Forms;");
            sb.AppendLine();
            sb.AppendLine("namespace " + form.Namespace);
            sb.AppendLine("{");
            sb.AppendLine("    static class Program");
            sb.AppendLine("    {");
            sb.AppendLine("        [STAThread]");
            sb.AppendLine("        static void Main()");
            sb.AppendLine("        {");
            sb.AppendLine("            Application.EnableVisualStyles();");
            sb.AppendLine("            Application.SetCompatibleTextRenderingDefault(false);");
            sb.AppendLine("            Application.Run(new " + form.ClassName + "());");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        // ------------------------------------------------------------------
        // мелочи
        // ------------------------------------------------------------------

        /// <summary>У этого типа есть видимая надпись.</summary>
        public static bool HasText(string kind)
        {
            return kind != "PictureBox" && kind != "Panel" && kind != "ListBox" &&
                   kind != "ComboBox" && kind != "ProgressBar";
        }

        /// <summary>У этого типа бывает нажатие.</summary>
        public static bool CanClick(string kind)
        {
            return kind == "Button" || kind == "CheckBox" || kind == "RadioButton" ||
                   kind == "Label" || kind == "PictureBox" || kind == "Panel";
        }

        static int Int(string s)
        {
            int v;
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : 0;
        }

        static string Escape(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"")
                            .Replace("\r", "").Replace("\n", "\\n");
        }

        static string Unescape(string s)
        {
            return (s ?? "").Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
        }
    }
}
