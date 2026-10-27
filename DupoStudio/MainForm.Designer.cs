using System.Drawing;
using System.Windows.Forms;

namespace DupoStudio
{
    partial class MainForm
    {
        System.ComponentModel.IContainer components = null;

        MenuStrip menuMain;
        ToolStripMenuItem miFile, miNewProject, miOpenProject, miSave, miSaveAll, miExit;
        ToolStripMenuItem miExport, miImport;
        ToolStripMenuItem miRun, miStart, miStop, miBuild, miBuildExe;
        ToolStripMenuItem miTools, miPhpInstall, miPhpInfo, miPhpRemove, miBrowser, miFolder;
        ToolStripMenuItem miJdkInstall, miJdkInfo, miJdkRemove, miNewJava;
        ToolStripMenuItem miHelp, miAbout;

        ToolStrip toolMain;
        ToolStripButton tbNew, tbOpen, tbSave, tbRun, tbStop, tbPhp, tbBrowser, tbExport;

        SplitContainer splitMain, splitRight;
        TreeView treeProject;
        TabControl tabsEditor;
        RichTextBox txtOutput;
        StatusStrip statusMain;
        ToolStripStatusLabel lblStatus, lblCaret, lblProject, lblPhp, lblJava;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        void InitializeComponent()
        {
            components = new System.ComponentModel.Container();

            menuMain = new MenuStrip();
            miFile = new ToolStripMenuItem("Файл");
            miNewProject = new ToolStripMenuItem("Новый проект...");
            miNewJava = new ToolStripMenuItem("Новый Java-проект...");
            miOpenProject = new ToolStripMenuItem("Открыть проект...");
            miSave = new ToolStripMenuItem("Сохранить");
            miSaveAll = new ToolStripMenuItem("Сохранить всё");
            miExport = new ToolStripMenuItem("Экспорт в .dapp...");
            miImport = new ToolStripMenuItem("Импорт .dapp...");
            miExit = new ToolStripMenuItem("Выход");

            miRun = new ToolStripMenuItem("Запуск");
            miStart = new ToolStripMenuItem("Запустить");
            miStop = new ToolStripMenuItem("Остановить");
            miBuild = new ToolStripMenuItem("Собрать папку с файлами");
            miBuildExe = new ToolStripMenuItem("Собрать в один .exe");

            miTools = new ToolStripMenuItem("Инструменты");
            miPhpInstall = new ToolStripMenuItem("Установить PHP");
            miPhpInfo = new ToolStripMenuItem("Версия PHP");
            miPhpRemove = new ToolStripMenuItem("Удалить PHP");
            miJdkInstall = new ToolStripMenuItem("Установить JDK (Java)");
            miJdkInfo = new ToolStripMenuItem("Версия Java");
            miJdkRemove = new ToolStripMenuItem("Удалить JDK");
            miBrowser = new ToolStripMenuItem("Открыть в браузере");
            miFolder = new ToolStripMenuItem("Открыть папку проекта");

            miHelp = new ToolStripMenuItem("Справка");
            miAbout = new ToolStripMenuItem("О программе");

            toolMain = new ToolStrip();
            tbNew = new ToolStripButton("Новый");
            tbOpen = new ToolStripButton("Открыть");
            tbSave = new ToolStripButton("Сохранить");
            tbRun = new ToolStripButton("▶  Запустить");
            tbStop = new ToolStripButton("■  Стоп");
            tbPhp = new ToolStripButton("Установить PHP");
            tbBrowser = new ToolStripButton("Браузер");
            tbExport = new ToolStripButton("Экспорт .dapp");

            splitMain = new SplitContainer();
            splitRight = new SplitContainer();
            treeProject = new TreeView();
            tabsEditor = new TabControl();
            txtOutput = new RichTextBox();
            statusMain = new StatusStrip();
            lblStatus = new ToolStripStatusLabel("Готово");
            lblCaret = new ToolStripStatusLabel("1 : 1");
            lblProject = new ToolStripStatusLabel("  нет проекта  ");
            lblPhp = new ToolStripStatusLabel("PHP не установлен");
            lblJava = new ToolStripStatusLabel("JDK не установлен");

            ((System.ComponentModel.ISupportInitialize)splitMain).BeginInit();
            ((System.ComponentModel.ISupportInitialize)splitRight).BeginInit();
            splitMain.Panel1.SuspendLayout();
            splitMain.Panel2.SuspendLayout();
            splitRight.Panel1.SuspendLayout();
            splitRight.Panel2.SuspendLayout();
            SuspendLayout();

            // --- меню ---
            miFile.DropDownItems.AddRange(new ToolStripItem[] {
                miNewProject, miNewJava, miOpenProject, new ToolStripSeparator(), miSave, miSaveAll,
                new ToolStripSeparator(), miExport, miImport,
                new ToolStripSeparator(), miExit });
            miRun.DropDownItems.AddRange(new ToolStripItem[] {
                miStart, miStop, new ToolStripSeparator(), miBuildExe, miBuild });
            miTools.DropDownItems.AddRange(new ToolStripItem[] {
                miPhpInstall, miPhpInfo, miPhpRemove, new ToolStripSeparator(),
                miJdkInstall, miJdkInfo, miJdkRemove, new ToolStripSeparator(),
                miBrowser, miFolder });
            miHelp.DropDownItems.Add(miAbout);

            miSave.ShortcutKeys = Keys.Control | Keys.S;
            miSaveAll.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;
            miStart.ShortcutKeys = Keys.F5;
            miStop.ShortcutKeys = Keys.Shift | Keys.F5;
            miExport.ShortcutKeys = Keys.Control | Keys.E;
            miBuildExe.ShortcutKeys = Keys.Control | Keys.B;

            menuMain.Items.AddRange(new ToolStripItem[] { miFile, miRun, miTools, miHelp });
            menuMain.BackColor = Theme.Chrome;
            menuMain.ForeColor = Theme.Text;
            menuMain.Padding = new Padding(6, 2, 0, 2);
            menuMain.Font = new Font("Segoe UI", 9f);
            menuMain.Renderer = new ToolStripProfessionalRenderer(new DarkColors());

            // --- панель инструментов ---
            toolMain.Items.AddRange(new ToolStripItem[] {
                tbNew, tbOpen, tbSave, new ToolStripSeparator(),
                tbRun, tbStop, new ToolStripSeparator(), tbPhp, tbBrowser, tbExport });
            toolMain.BackColor = Theme.Chrome;
            toolMain.ForeColor = Theme.Text;
            toolMain.GripStyle = ToolStripGripStyle.Hidden;
            toolMain.Padding = new Padding(8, 5, 8, 5);
            toolMain.Font = new Font("Segoe UI", 9f);
            toolMain.ImageScalingSize = new Size(16, 16);
            toolMain.Renderer = new ToolStripProfessionalRenderer(new DarkColors());
            foreach (ToolStripItem it in toolMain.Items)
            {
                it.ForeColor = Theme.Text;
                it.Margin = new Padding(3, 1, 3, 1);
                it.Padding = new Padding(6, 2, 6, 2);
            }
            tbRun.ForeColor = Theme.Ok;
            tbRun.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            tbStop.ForeColor = Theme.Err;
            tbExport.ForeColor = Theme.Accent2;

            // --- дерево проекта ---
            treeProject.Dock = DockStyle.Fill;
            treeProject.BackColor = Theme.Panel;
            treeProject.ForeColor = Theme.Text;
            treeProject.BorderStyle = BorderStyle.None;
            treeProject.HideSelection = false;
            treeProject.Font = new Font("Segoe UI", 9.5f);
            treeProject.ItemHeight = 24;
            treeProject.Indent = 18;
            treeProject.ShowLines = false;
            treeProject.ShowPlusMinus = true;
            treeProject.FullRowSelect = true;

            // --- редактор ---
            tabsEditor.Dock = DockStyle.Fill;
            tabsEditor.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabsEditor.SizeMode = TabSizeMode.Normal;
            tabsEditor.ItemSize = new Size(170, 26);
            tabsEditor.Padding = new Point(10, 4);
            tabsEditor.BackColor = Theme.Back;

            // --- вывод ---
            txtOutput.Dock = DockStyle.Fill;
            txtOutput.BackColor = Color.FromArgb(24, 24, 24);
            txtOutput.ForeColor = Theme.Text;
            txtOutput.BorderStyle = BorderStyle.None;
            txtOutput.Font = new Font("Consolas", 10f);
            txtOutput.Padding = new Padding(8);
            txtOutput.ReadOnly = true;
            txtOutput.DetectUrls = false;

            // --- разделители ---
            splitRight.Dock = DockStyle.Fill;
            splitRight.Orientation = Orientation.Horizontal;
            splitRight.BackColor = Theme.Chrome;
            splitRight.SplitterWidth = 5;
            splitRight.Panel1.Controls.Add(tabsEditor);
            splitRight.Panel2.Controls.Add(txtOutput);

            splitMain.Dock = DockStyle.Fill;
            splitMain.Orientation = Orientation.Vertical;
            splitMain.BackColor = Theme.Chrome;
            splitMain.SplitterWidth = 5;
            splitMain.Panel1.Controls.Add(treeProject);
            splitMain.Panel2.Controls.Add(splitRight);

            // --- статус ---
            statusMain.BackColor = Theme.Status;
            statusMain.ForeColor = Color.White;
            statusMain.SizingGrip = false;
            statusMain.Padding = new Padding(4, 2, 10, 2);
            statusMain.Font = new Font("Segoe UI", 8.5f);

            lblProject.BackColor = Theme.Accent;
            lblProject.ForeColor = Color.White;
            lblProject.Margin = new Padding(0, 0, 10, 0);

            lblStatus.Spring = true;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            lblStatus.ForeColor = Color.FromArgb(205, 220, 245);

            lblPhp.Margin = new Padding(0, 0, 18, 0);
            lblCaret.ForeColor = Color.FromArgb(205, 220, 245);

            lblJava.Margin = new Padding(0, 0, 18, 0);
            statusMain.Items.AddRange(new ToolStripItem[] { lblProject, lblStatus, lblPhp, lblJava, lblCaret });

            // --- форма ---
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1240, 780);
            MinimumSize = new Size(900, 560);
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            Text = "Dupo Studio — App Maker";
            StartPosition = FormStartPosition.CenterScreen;

            Controls.Add(splitMain);
            Controls.Add(statusMain);
            Controls.Add(toolMain);
            Controls.Add(menuMain);
            MainMenuStrip = menuMain;

            splitMain.Panel1.ResumeLayout(false);
            splitMain.Panel2.ResumeLayout(false);
            splitRight.Panel1.ResumeLayout(false);
            splitRight.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitMain).EndInit();
            ((System.ComponentModel.ISupportInitialize)splitRight).EndInit();
            splitMain.ResumeLayout(false);
            splitRight.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }
    }

    /// <summary>Тёмная раскраска для меню и тулбара.</summary>
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
