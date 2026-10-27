using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DupoLang;

namespace DupoStudio
{
    /// <summary>Свойства проекта — как в Visual Studio: данные приложения, пространства имён, ресурсы.</summary>
    public class PropertiesForm : Form
    {
        readonly ProjectSettings settings;

        readonly TextBox name = new TextBox();
        readonly TextBox product = new TextBox();
        readonly TextBox author = new TextBox();
        readonly TextBox version = new TextBox();
        readonly TextBox description = new TextBox();
        readonly TextBox space = new TextBox();
        readonly TextBox javaPackage = new TextBox();
        readonly CheckBox singleExe = new CheckBox();

        readonly ListBox resources = new ListBox();
        readonly Label resourceHint = new Label();

        public PropertiesForm(ProjectSettings project)
        {
            settings = project;

            Text = "Свойства проекта — " + settings.Name;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(760, 520);
            BackColor = Theme.Chrome;
            ForeColor = Theme.Text;

            Build();
            Fill();
            RefreshResources();
        }

        // ---------------- разметка ----------------

        void Build()
        {
            Controls.Add(Head("Приложение", 20));

            Controls.Add(Caption("Название", 52));
            Place(name, 52);

            Controls.Add(Caption("Продукт", 86));
            Place(product, 86);

            Controls.Add(Caption("Автор", 120));
            Place(author, 120);

            Controls.Add(Caption("Версия", 154));
            Place(version, 154);

            Controls.Add(Caption("Описание", 188));
            Place(description, 188);

            Controls.Add(Head("Код", 232));

            Controls.Add(Caption("Пространство имён", 264));
            Place(space, 264);

            Controls.Add(Caption("Пакет Java", 298));
            Place(javaPackage, 298);

            singleExe.Text = "Собирать в один exe";
            singleExe.SetBounds(182, 332, 260, 22);
            singleExe.ForeColor = Theme.Text;
            singleExe.FlatStyle = FlatStyle.Flat;
            Controls.Add(singleExe);

            var hint = new Label
            {
                Text = "Иначе рядом с exe останутся исходники.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Theme.Dim,
                Bounds = new Rectangle(184, 354, 320, 18)
            };
            Controls.Add(hint);

            // ---- ресурсы ----
            Controls.Add(Head("Ресурсы", 20, 430));

            resources.SetBounds(430, 52, 304, 300);
            resources.BackColor = Theme.Back;
            resources.ForeColor = Theme.Text;
            resources.BorderStyle = BorderStyle.FixedSingle;
            resources.Font = new Font("Consolas", 9.5f);
            Controls.Add(resources);

            resourceHint.Font = new Font("Segoe UI", 8.5f);
            resourceHint.ForeColor = Theme.Dim;
            resourceHint.SetBounds(432, 356, 300, 34);
            Controls.Add(resourceHint);

            var add = Button("Добавить файлы", 430, 396, 150);
            add.Click += delegate { AddResources(); };
            Controls.Add(add);

            var drop = Button("Удалить", 588, 396, 146);
            drop.Click += delegate { RemoveResource(); };
            Controls.Add(drop);

            var openDir = Button("Открыть папку", 430, 434, 150);
            openDir.Click += delegate { OpenResourcesDir(); };
            Controls.Add(openDir);

            // ---- кнопки окна ----
            var ok = Button("Сохранить", 540, 476, 110);
            ok.BackColor = Theme.Accent;
            ok.ForeColor = Color.White;
            ok.Click += delegate { Apply(); DialogResult = DialogResult.OK; Close(); };
            Controls.Add(ok);

            var cancel = Button("Отмена", 658, 476, 90);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        static Label Head(string text, int top) { return Head(text, top, 20); }

        static Label Head(string text, int top, int left)
        {
            return new Label
            {
                Text = text.ToUpperInvariant(),
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = Theme.Accent2,
                Bounds = new Rectangle(left, top, 300, 18)
            };
        }

        static Label Caption(string text, int top)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Theme.Dim,
                TextAlign = ContentAlignment.MiddleRight,
                Bounds = new Rectangle(20, top + 3, 156, 20)
            };
        }

        void Place(TextBox box, int top)
        {
            box.SetBounds(182, top, 222, 24);
            box.BackColor = Theme.Back;
            box.ForeColor = Theme.Text;
            box.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(box);
        }

        static Button Button(string text, int left, int top, int width)
        {
            var b = new Button
            {
                Text = text,
                Bounds = new Rectangle(left, top, width, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Panel,
                ForeColor = Theme.Text
            };
            b.FlatAppearance.BorderColor = Theme.Border;
            return b;
        }

        // ---------------- данные ----------------

        void Fill()
        {
            name.Text = settings.Name;
            product.Text = settings.Product;
            author.Text = settings.Author;
            version.Text = settings.Version;
            description.Text = settings.Description;
            space.Text = settings.Namespace;
            javaPackage.Text = settings.JavaPackage;
            singleExe.Checked = settings.SingleExe;
        }

        void Apply()
        {
            settings.Name = name.Text.Trim();
            settings.Product = product.Text.Trim();
            settings.Author = author.Text.Trim();
            settings.Version = version.Text.Trim();
            settings.Description = description.Text.Trim();
            settings.Namespace = space.Text.Trim();
            settings.JavaPackage = javaPackage.Text.Trim();
            settings.SingleExe = singleExe.Checked;

            settings.Save();
        }

        // ---------------- ресурсы ----------------

        void RefreshResources()
        {
            resources.Items.Clear();

            string dir = settings.ResourcesDir;
            if (Directory.Exists(dir))
            {
                foreach (string file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories).OrderBy(x => x))
                    resources.Items.Add(ResourceSet.NameOf(file) + "   —   " + Path.GetFileName(file));
            }

            resourceHint.Text = resources.Items.Count == 0
                ? "Пока пусто. Добавьте файлы — они станут доступны как Resources.имя"
                : "В коде: using Dupo.Resources, затем Resources.имя";
        }

        void AddResources()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Добавить в ресурсы";
                dlg.Multiselect = true;
                dlg.Filter = "Все файлы (*.*)|*.*";

                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                string dir = settings.ResourcesDir;
                Directory.CreateDirectory(dir);

                foreach (string file in dlg.FileNames)
                {
                    try
                    {
                        string target = Path.Combine(dir, Path.GetFileName(file));
                        if (!string.Equals(Path.GetFullPath(file), Path.GetFullPath(target),
                                           StringComparison.OrdinalIgnoreCase))
                            File.Copy(file, target, true);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "Не добавилось: " + Path.GetFileName(file) +
                                              "\r\n" + ex.Message, "Dupo Studio");
                    }
                }
            }

            RefreshResources();
        }

        void RemoveResource()
        {
            if (resources.SelectedIndex < 0) return;

            string entry = resources.SelectedItem.ToString();
            int sep = entry.LastIndexOf("—");
            string fileName = sep > 0 ? entry.Substring(sep + 1).Trim() : entry.Trim();

            string path = Path.Combine(settings.ResourcesDir, fileName);
            if (!File.Exists(path)) { RefreshResources(); return; }

            if (MessageBox.Show(this, "Удалить " + fileName + " из ресурсов?", "Dupo Studio",
                                MessageBoxButtons.YesNo) != DialogResult.Yes) return;

            try { File.Delete(path); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Dupo Studio"); }

            RefreshResources();
        }

        void OpenResourcesDir()
        {
            string dir = settings.ResourcesDir;
            Directory.CreateDirectory(dir);
            try { System.Diagnostics.Process.Start(dir); }
            catch { }
        }
    }
}
