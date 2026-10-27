using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace DupoStudio
{
    /// <summary>
    /// Окно свойств, как в Visual Studio: сетка «свойство — значение».
    /// Показывает либо свойства проекта, либо выделенный элемент формы.
    /// </summary>
    public class PropertiesPanel : Panel
    {
        readonly PropertyGrid grid = new PropertyGrid();
        readonly Label header = new Label();

        /// <summary>Свойства изменили — пора сохранить проект.</summary>
        public event EventHandler Changed;

        public PropertiesPanel()
        {
            BackColor = Theme.Panel;

            header.Dock = DockStyle.Top;
            header.Height = 22;
            header.ForeColor = Theme.Dim;
            header.Padding = new Padding(8, 3, 0, 0);
            header.Text = "ничего не выбрано";
            Controls.Add(header);

            grid.Dock = DockStyle.Fill;
            grid.ToolbarVisible = true;
            grid.HelpVisible = true;
            grid.PropertySort = PropertySort.Categorized;
            grid.BackColor = Theme.Panel;
            grid.ViewBackColor = Theme.Panel;
            grid.ViewForeColor = Theme.Text;
            grid.LineColor = Theme.Border;
            grid.CategoryForeColor = Theme.Accent2;
            grid.HelpBackColor = Theme.Chrome;
            grid.HelpForeColor = Theme.Dim;
            grid.CommandsBackColor = Theme.Chrome;
            grid.PropertyValueChanged += delegate
            {
                var h = Changed;
                if (h != null) h(this, EventArgs.Empty);
            };
            Controls.Add(grid);
            grid.BringToFront();
        }

        /// <summary>Показать свойства объекта.</summary>
        public void Bind(object target, string title)
        {
            grid.SelectedObject = target;
            header.Text = string.IsNullOrEmpty(title) ? "ничего не выбрано" : title;
        }

        /// <summary>Перечитать значения, не меняя объект.</summary>
        public void Reload() { grid.Refresh(); }
    }

    /// <summary>Свойства проекта для окна свойств.</summary>
    public class ProjectProxy
    {
        readonly ProjectSettings s;
        public ProjectProxy(ProjectSettings settings) { s = settings; }

        [Category("Приложение"), DisplayName("Название")]
        public string Name { get { return s.Name; } set { s.Name = value ?? ""; } }

        [Category("Приложение"), DisplayName("Продукт")]
        public string Product { get { return s.Product; } set { s.Product = value ?? ""; } }

        [Category("Приложение"), DisplayName("Автор")]
        public string Author { get { return s.Author; } set { s.Author = value ?? ""; } }

        [Category("Приложение"), DisplayName("Версия")]
        public string Version { get { return s.Version; } set { s.Version = value ?? ""; } }

        [Category("Приложение"), DisplayName("Описание")]
        public string Description { get { return s.Description; } set { s.Description = value ?? ""; } }

        [Category("Код"), DisplayName("Пространство имён"),
         Description("namespace для сгенерированного C#")]
        public string Namespace { get { return s.Namespace; } set { s.Namespace = value ?? ""; } }

        [Category("Код"), DisplayName("Пакет Java"),
         Description("package для классов Java")]
        public string JavaPackage { get { return s.JavaPackage; } set { s.JavaPackage = value ?? ""; } }

        [Category("Сборка"), DisplayName("Собирать в один exe")]
        public bool SingleExe { get { return s.SingleExe; } set { s.SingleExe = value; } }

        [Category("Сборка"), DisplayName("Папка проекта"), ReadOnly(true)]
        public string Folder { get { return s.Dir; } }
    }
}
