using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace DupoStudio
{
    /// <summary>Пункт меню «Тема» — одинаковый в стартовом окне и в окне проекта.</summary>
    public static class ThemeMenu
    {
        /// <summary>
        /// Собирает подменю со списком тем. Выбор сразу перекрашивает окно
        /// и запоминается в настройках.
        /// </summary>
        public static ToolStripMenuItem Build(Form owner, Action afterChange)
        {
            var root = new ToolStripMenuItem("Тема");
            var items = new List<ToolStripMenuItem>();

            foreach (string name in Theme.Names)
            {
                string pick = name;

                var item = new ToolStripMenuItem(name);
                item.Checked = string.Equals(name, Theme.Name, StringComparison.OrdinalIgnoreCase);
                item.Click += delegate
                {
                    Switch(owner, pick);
                    foreach (var other in items)
                        other.Checked = string.Equals(other.Text, Theme.Name, StringComparison.OrdinalIgnoreCase);

                    var h = afterChange;
                    if (h != null) h();
                };

                items.Add(item);
                root.DropDownItems.Add(item);
            }

            root.DropDownItems.Add(new ToolStripSeparator());

            var mine = new ToolStripMenuItem("Своя тема (C#)");
            mine.Checked = Theme.Custom;
            mine.Click += delegate
            {
                Custom(owner);
                foreach (var other in items) other.Checked = false;
                mine.Checked = Theme.Custom;

                var h = afterChange;
                if (h != null) h();
            };
            root.DropDownItems.Add(mine);

            var edit = new ToolStripMenuItem("Открыть файл темы...");
            edit.Click += delegate { Edit(owner); };
            root.DropDownItems.Add(edit);

            return root;
        }

        /// <summary>Собрать theme.cs и применить его.</summary>
        public static void Custom(Form owner)
        {
            // Берём встроенную палитру за основу: то, что в theme.cs не указали,
            // останется нормальным, а не чёрным по чёрному.
            string baseName = Theme.Custom ? Settings.ThemeBase : Theme.Name;
            Theme.Apply(baseName);

            string report;
            try
            {
                ThemeScript.Create();

                var said = new System.Text.StringBuilder();
                report = ThemeScript.Apply(delegate (string s2) { said.AppendLine(s2); });

                if (said.Length > 0) report = report + Environment.NewLine + said.ToString().Trim();
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner,
                    "Тема не собралась:" + Environment.NewLine + Environment.NewLine + ex.Message,
                    "Своя тема", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Settings.ThemeBase = baseName;
            Settings.ThemeName = "Своя";
            Settings.Save();

            // Перекрашиваем по-честному: цвета уже новые, карту строить не из чего,
            // поэтому просто проходим по окнам и ставим палитру заново.
            foreach (Form f in Application.OpenForms)
            {
                Repaint(f);
                Theme.PaintBackground(f);
            }

            MessageBox.Show(owner, report, "Своя тема", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>Открыть theme.cs в блокноте — править его можно и там.</summary>
        public static void Edit(Form owner)
        {
            try
            {
                string path = ThemeScript.Create();
                System.Diagnostics.Process.Start(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, "Не открылось: " + ex.Message, "Своя тема",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>Пройтись по окну и поставить текущие цвета.</summary>
        static void Repaint(Control root)
        {
            if (root == null) return;

            var strip = root as ToolStrip;
            if (strip != null)
            {
                strip.BackColor = Theme.Chrome;
                strip.ForeColor = Theme.Text;
                strip.Renderer = new ToolStripProfessionalRenderer(new DarkColors());
                Theme.PaintMenu(strip.Items);
            }
            else if (root is TextBoxBase || root is ListBox || root is CheckedListBox || root is TreeView)
            {
                root.BackColor = Theme.Panel;
                root.ForeColor = Theme.Text;
            }
            else if (root is Button)
            {
                root.BackColor = Theme.Chrome;
                root.ForeColor = Theme.Text;
            }
            else if (root is SplitContainer)
            {
                root.BackColor = Theme.Border;
            }
            else if (root is Form || root is Panel || root is TabPage || root is TabControl)
            {
                root.BackColor = Theme.Back;
                root.ForeColor = Theme.Text;
            }
            else
            {
                root.ForeColor = Theme.Text;
            }

            foreach (Control child in root.Controls) Repaint(child);
            root.Invalidate(true);
        }

        /// <summary>Переключить тему и перекрасить все открытые окна.</summary>
        public static void Switch(Form owner, string name)
        {
            var map = Theme.Apply(name);

            Settings.ThemeName = Theme.Name;
            Settings.Save();

            foreach (Form f in Application.OpenForms) Theme.Recolor(f, map);
            if (owner != null) Theme.PaintMenu(Menu(owner));
        }

        static ToolStripItemCollection Menu(Form f)
        {
            return f.MainMenuStrip != null ? f.MainMenuStrip.Items : new MenuStrip().Items;
        }
    }
}
