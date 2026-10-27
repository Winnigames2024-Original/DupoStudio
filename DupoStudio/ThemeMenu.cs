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

            return root;
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
