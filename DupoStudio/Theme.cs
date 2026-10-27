using System.Drawing;
using System.Windows.Forms;

namespace DupoStudio
{
    /// <summary>Тёмная палитра Dupo Studio.</summary>
    public static class Theme
    {
        public static readonly Color Back = Color.FromArgb(30, 30, 30);
        public static readonly Color Panel = Color.FromArgb(37, 37, 38);
        public static readonly Color Chrome = Color.FromArgb(45, 45, 48);
        public static readonly Color Border = Color.FromArgb(62, 62, 66);
        public static readonly Color Text = Color.FromArgb(220, 220, 220);
        public static readonly Color Dim = Color.FromArgb(150, 150, 150);
        public static readonly Color Accent = Color.FromArgb(43, 127, 255);
        public static readonly Color Accent2 = Color.FromArgb(0, 194, 168);
        public static readonly Color Gutter = Color.FromArgb(24, 24, 25);
        public static readonly Color LineNumber = Color.FromArgb(92, 99, 112);
        public static readonly Color Status = Color.FromArgb(24, 58, 110);

        public static readonly Color Keyword = Color.FromArgb(86, 156, 214);
        public static readonly Color TypeName = Color.FromArgb(78, 201, 176);
        public static readonly Color Str = Color.FromArgb(206, 145, 120);
        public static readonly Color Number = Color.FromArgb(181, 206, 168);
        public static readonly Color Comment = Color.FromArgb(106, 153, 85);

        /// <summary>
        /// Задаёт светлый текст всем пунктам меню, включая выпадающие.
        /// Верхние пункты наследуют цвет от строки меню, а выпадающие — нет:
        /// у них свой владелец с системным чёрным текстом, и на тёмном фоне
        /// их попросту не видно.
        /// </summary>
        public static void PaintMenu(ToolStripItemCollection items)
        {
            foreach (ToolStripItem item in items)
            {
                if (item is ToolStripSeparator) continue;

                item.ForeColor = Text;

                var menu = item as ToolStripMenuItem;
                if (menu == null) continue;

                menu.DropDown.BackColor = Chrome;
                menu.DropDown.ForeColor = Text;
                menu.DropDown.Renderer = new ToolStripProfessionalRenderer(new DarkColors());

                PaintMenu(menu.DropDownItems);
            }
        }

        public static readonly Color Ok = Color.FromArgb(106, 190, 120);
        public static readonly Color Warn = Color.FromArgb(220, 180, 100);
        public static readonly Color Err = Color.FromArgb(230, 110, 110);
        public static readonly Color Info = Color.FromArgb(120, 170, 220);
    }

    /// <summary>Тёмная раскраска для меню и панели инструментов.</summary>
    public class DarkColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected { get { return Theme.Accent; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.Accent; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.Accent; } }
        public override Color MenuItemBorder { get { return Theme.Accent; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color ToolStripDropDownBackground { get { return Theme.Chrome; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Chrome; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Chrome; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Chrome; } }
        public override Color ToolStripGradientBegin { get { return Theme.Chrome; } }
        public override Color ToolStripGradientMiddle { get { return Theme.Chrome; } }
        public override Color ToolStripGradientEnd { get { return Theme.Chrome; } }
        public override Color ButtonSelectedHighlight { get { return Theme.Border; } }
        public override Color ButtonPressedHighlight { get { return Theme.Accent; } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Theme.Border; } }
    }
}
