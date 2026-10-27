using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DupoStudio
{
    /// <summary>
    /// Палитра студии. Цвета читаются при создании окон, поэтому смена темы
    /// применяется к окну, когда оно открывается заново — студия делает это сама.
    /// </summary>
    public static class Theme
    {
        /// <summary>Имя текущей темы.</summary>
        public static string Name { get; private set; }

        /// <summary>Тёмная ли тема сейчас — от этого зависят мелкие оттенки.</summary>
        public static bool Dark { get; private set; }

        public static Color Back { get; private set; }
        public static Color Panel { get; private set; }
        public static Color Chrome { get; private set; }
        public static Color Border { get; private set; }
        public static Color Text { get; private set; }
        public static Color Dim { get; private set; }
        public static Color Accent { get; private set; }
        public static Color Accent2 { get; private set; }
        public static Color Gutter { get; private set; }
        public static Color LineNumber { get; private set; }
        public static Color Status { get; private set; }

        public static Color Keyword { get; private set; }
        public static Color TypeName { get; private set; }
        public static Color Str { get; private set; }
        public static Color Number { get; private set; }
        public static Color Comment { get; private set; }

        public static Color Ok { get; private set; }
        public static Color Warn { get; private set; }
        public static Color Err { get; private set; }
        public static Color Info { get; private set; }

        /// <summary>Названия тем по порядку, как они идут в меню.</summary>
        public static string[] Names
        {
            get { return palettes.Select(p => p.Name).ToArray(); }
        }

        static Theme() { Apply("Тёмная"); }

        /// <summary>Цвета текущей палитры в постоянном порядке — для пересчёта.</summary>
        static Color[] Snapshot()
        {
            return new[]
            {
                Back, Panel, Chrome, Border, Text, Dim, Accent, Accent2,
                Gutter, LineNumber, Status,
                Keyword, TypeName, Str, Number, Comment,
                Ok, Warn, Err, Info
            };
        }

        /// <summary>
        /// Включить тему по названию. Возвращает соответствие «старый цвет →
        /// новый», по которому перекрашиваются уже открытые окна.
        /// </summary>
        public static Dictionary<int, Color> Apply(string name)
        {
            Color[] before = Name == null ? null : Snapshot();

            Set(name);

            var map = new Dictionary<int, Color>();
            if (before == null) return map;

            Color[] after = Snapshot();
            for (int i = 0; i < before.Length && i < after.Length; i++)
            {
                int key = before[i].ToArgb();
                if (key == after[i].ToArgb()) continue;
                if (!map.ContainsKey(key)) map[key] = after[i];
            }
            return map;
        }

        /// <summary>
        /// Перекрасить уже созданное окно: у каждого элемента цвета заменяются
        /// по карте. Так тема применяется сразу, без переоткрытия окон.
        /// </summary>
        public static void Recolor(Control root, Dictionary<int, Color> map)
        {
            if (root == null || map == null || map.Count == 0) return;

            Color swap;
            if (map.TryGetValue(root.BackColor.ToArgb(), out swap)) root.BackColor = swap;
            if (map.TryGetValue(root.ForeColor.ToArgb(), out swap)) root.ForeColor = swap;

            var strip = root as ToolStrip;
            if (strip != null)
            {
                strip.Renderer = new ToolStripProfessionalRenderer(new DarkColors());
                RecolorItems(strip.Items, map);
            }

            var link = root as LinkLabel;
            if (link != null)
            {
                if (map.TryGetValue(link.LinkColor.ToArgb(), out swap)) link.LinkColor = swap;
                if (map.TryGetValue(link.ActiveLinkColor.ToArgb(), out swap)) link.ActiveLinkColor = swap;
                if (map.TryGetValue(link.VisitedLinkColor.ToArgb(), out swap)) link.VisitedLinkColor = swap;
            }

            var grid = root as PropertyGrid;
            if (grid != null)
            {
                grid.ViewBackColor = Panel;
                grid.ViewForeColor = Text;
                grid.LineColor = Border;
                grid.CategoryForeColor = Accent2;
                grid.HelpBackColor = Chrome;
                grid.HelpForeColor = Dim;
                grid.CommandsBackColor = Chrome;
            }

            var split = root as SplitContainer;
            if (split != null) split.BackColor = Border;

            foreach (Control child in root.Controls) Recolor(child, map);

            root.Invalidate(true);
        }

        static void RecolorItems(ToolStripItemCollection items, Dictionary<int, Color> map)
        {
            Color swap;
            foreach (ToolStripItem item in items)
            {
                if (map.TryGetValue(item.ForeColor.ToArgb(), out swap)) item.ForeColor = swap;
                if (map.TryGetValue(item.BackColor.ToArgb(), out swap)) item.BackColor = swap;

                var menu = item as ToolStripMenuItem;
                if (menu == null) continue;

                menu.DropDown.BackColor = Chrome;
                menu.DropDown.ForeColor = Text;
                menu.DropDown.Renderer = new ToolStripProfessionalRenderer(new DarkColors());
                RecolorItems(menu.DropDownItems, map);
            }
        }

        static void Set(string name)
        {
            var p = palettes.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
                    ?? palettes[0];

            Name = p.Name;
            Dark = p.Dark;

            Back = p.Back;
            Panel = p.Panel;
            Chrome = p.Chrome;
            Border = p.Border;
            Text = p.Text;
            Dim = p.Dim;
            Accent = p.Accent;
            Accent2 = p.Accent2;
            Gutter = p.Gutter;
            LineNumber = p.LineNumber;
            Status = p.Status;

            Keyword = p.Keyword;
            TypeName = p.TypeName;
            Str = p.Str;
            Number = p.Number;
            Comment = p.Comment;

            Ok = p.Ok;
            Warn = p.Warn;
            Err = p.Err;
            Info = p.Info;
        }

        /// <summary>Оттенок для наведения — чуть светлее или темнее фона.</summary>
        public static Color Hover(Color c)
        {
            int d = Dark ? 18 : -16;
            return Color.FromArgb(
                Math.Max(0, Math.Min(255, c.R + d)),
                Math.Max(0, Math.Min(255, c.G + d)),
                Math.Max(0, Math.Min(255, c.B + d)));
        }

        /// <summary>
        /// Задаёт цвет текста всем пунктам меню, включая выпадающие.
        /// Верхние пункты наследуют цвет от строки меню, а выпадающие — нет:
        /// у них свой владелец с системным цветом, и на чужом фоне
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

        // ------------------------------------------------------------------
        // палитры
        // ------------------------------------------------------------------

        class Palette
        {
            public string Name;
            public bool Dark = true;

            public Color Back, Panel, Chrome, Border, Text, Dim, Accent, Accent2,
                         Gutter, LineNumber, Status,
                         Keyword, TypeName, Str, Number, Comment,
                         Ok, Warn, Err, Info;
        }

        static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }

        static readonly Palette[] palettes =
        {
            new Palette
            {
                Name = "Тёмная",
                Dark = true,
                Back = C(30, 30, 30),
                Panel = C(37, 37, 38),
                Chrome = C(45, 45, 48),
                Border = C(62, 62, 66),
                Text = C(220, 220, 220),
                Dim = C(150, 150, 150),
                Accent = C(43, 127, 255),
                Accent2 = C(0, 194, 168),
                Gutter = C(24, 24, 25),
                LineNumber = C(92, 99, 112),
                Status = C(24, 58, 110),
                Keyword = C(86, 156, 214),
                TypeName = C(78, 201, 176),
                Str = C(206, 145, 120),
                Number = C(181, 206, 168),
                Comment = C(106, 153, 85),
                Ok = C(106, 190, 120),
                Warn = C(220, 180, 100),
                Err = C(230, 110, 110),
                Info = C(120, 170, 220),
            },

            new Palette
            {
                Name = "Светлая",
                Dark = false,
                Back = C(255, 255, 255),
                Panel = C(243, 243, 243),
                Chrome = C(234, 234, 236),
                Border = C(205, 205, 210),
                Text = C(30, 30, 32),
                Dim = C(108, 112, 120),
                Accent = C(0, 102, 204),
                Accent2 = C(0, 138, 122),
                Gutter = C(246, 246, 248),
                LineNumber = C(150, 155, 165),
                Status = C(0, 122, 204),
                Keyword = C(0, 0, 255),
                TypeName = C(43, 145, 175),
                Str = C(163, 21, 21),
                Number = C(9, 134, 88),
                Comment = C(0, 128, 0),
                Ok = C(22, 134, 60),
                Warn = C(176, 118, 0),
                Err = C(190, 40, 40),
                Info = C(0, 102, 204),
            },

            new Palette
            {
                Name = "Синяя ночь",
                Dark = true,
                Back = C(26, 32, 48),
                Panel = C(32, 40, 60),
                Chrome = C(38, 48, 72),
                Border = C(56, 70, 100),
                Text = C(220, 228, 244),
                Dim = C(138, 152, 180),
                Accent = C(80, 140, 255),
                Accent2 = C(90, 215, 200),
                Gutter = C(22, 27, 41),
                LineNumber = C(88, 102, 136),
                Status = C(28, 70, 140),
                Keyword = C(130, 170, 255),
                TypeName = C(120, 220, 205),
                Str = C(236, 170, 140),
                Number = C(190, 215, 170),
                Comment = C(110, 140, 120),
                Ok = C(120, 205, 140),
                Warn = C(230, 190, 110),
                Err = C(240, 125, 125),
                Info = C(140, 185, 235),
            },

            new Palette
            {
                Name = "Контрастная",
                Dark = true,
                Back = C(0, 0, 0),
                Panel = C(12, 12, 12),
                Chrome = C(20, 20, 20),
                Border = C(110, 110, 110),
                Text = C(255, 255, 255),
                Dim = C(200, 200, 200),
                Accent = C(255, 190, 0),
                Accent2 = C(0, 255, 200),
                Gutter = C(0, 0, 0),
                LineNumber = C(170, 170, 170),
                Status = C(60, 60, 60),
                Keyword = C(120, 200, 255),
                TypeName = C(120, 255, 220),
                Str = C(255, 190, 150),
                Number = C(200, 255, 170),
                Comment = C(150, 220, 150),
                Ok = C(120, 255, 150),
                Warn = C(255, 220, 120),
                Err = C(255, 130, 130),
                Info = C(160, 210, 255),
            },

            new Palette
            {
                Name = "Тёплая",
                Dark = true,
                Back = C(40, 35, 32),
                Panel = C(48, 42, 38),
                Chrome = C(56, 49, 44),
                Border = C(80, 70, 62),
                Text = C(236, 226, 214),
                Dim = C(168, 154, 140),
                Accent = C(224, 122, 60),
                Accent2 = C(190, 180, 90),
                Gutter = C(34, 30, 27),
                LineNumber = C(120, 108, 96),
                Status = C(120, 64, 30),
                Keyword = C(230, 160, 90),
                TypeName = C(200, 190, 120),
                Str = C(200, 160, 130),
                Number = C(190, 200, 140),
                Comment = C(135, 140, 110),
                Ok = C(150, 195, 120),
                Warn = C(230, 190, 110),
                Err = C(232, 120, 100),
                Info = C(220, 170, 120),
            },
        };
    }

    /// <summary>Раскраска меню и панели инструментов по текущей теме.</summary>
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
