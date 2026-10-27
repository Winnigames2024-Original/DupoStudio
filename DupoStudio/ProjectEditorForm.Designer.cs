using System.Drawing;
using System.Windows.Forms;

namespace DupoStudio
{
    partial class ProjectEditorForm
    {
        System.ComponentModel.IContainer components = null;

        MenuStrip menuMain;
        ToolStripMenuItem miFile, miNewProject, miNewJava, miOpenProject, miSave, miSaveAll;
        ToolStripMenuItem miExport, miImport, miAutostart, miClose, miExit;
        ToolStripMenuItem miRun, miStart, miStop, miBuild, miBuildExe;
        ToolStripMenuItem miTools, miPhpInstall, miPhpInfo, miPhpRemove;
        ToolStripMenuItem miJdkInstall, miJdkInfo, miJdkRemove, miBrowser, miFolder;
        ToolStripMenuItem miToolsGet, miToolsFolder, miToolsRemove;
        ToolStripMenuItem miService, miSvcInstall, miSvcStart, miSvcStop, miSvcRemove, miSvcStatus, miSvcQueue;
        ToolStripMenuItem miProject, miProperties, miResources;
        ToolStripMenuItem miView, miViewExplorer, miViewProps, miViewToolbox, miViewConsole, miViewDockAll;
        ToolStripMenuItem miNewForm, miOpenForm, miFormCode;
        ToolStripMenuItem miHelp, miAbout;

        ToolStrip toolMain;
        ToolStripButton tbNew, tbOpen, tbSave, tbRun, tbStop, tbPhp, tbBrowser, tbExport;

        DockHost dock;
        PropertiesPanel properties;
        ToolboxPanel toolbox;
        FormDesignerPanel designer;
        StatusStrip statusMain;
        ToolStripStatusLabel lblStatus, lblCaret, lblProject, lblPhp, lblJava;

        // Три встроенных окна: дерево слева, редактор в центре, вывод снизу.
        ExplorerForm explorer;
        CodeEditorForm editors;
        ConsoleForm console;

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
            miAutostart = new ToolStripMenuItem("Открывать этот проект при запуске");
            miClose = new ToolStripMenuItem("Закрыть проект");
            miExit = new ToolStripMenuItem("Выход");

            miRun = new ToolStripMenuItem("Запуск");
            miStart = new ToolStripMenuItem("Запустить");
            miStop = new ToolStripMenuItem("Остановить");
            miBuildExe = new ToolStripMenuItem("Собрать в один .exe");
            miBuild = new ToolStripMenuItem("Собрать папку с файлами");

            miTools = new ToolStripMenuItem("Инструменты");
            miPhpInstall = new ToolStripMenuItem("Установить PHP");
            miPhpInfo = new ToolStripMenuItem("Версия PHP");
            miPhpRemove = new ToolStripMenuItem("Удалить PHP");
            miJdkInstall = new ToolStripMenuItem("Установить JDK (Java)");
            miJdkInfo = new ToolStripMenuItem("Версия Java");
            miJdkRemove = new ToolStripMenuItem("Удалить JDK");
            miToolsGet = new ToolStripMenuItem("Скачать BuildTools с GitHub");
            miToolsFolder = new ToolStripMenuItem("Папка BuildTools");
            miToolsRemove = new ToolStripMenuItem("Удалить BuildTools");

            miService = new ToolStripMenuItem("Фоновый сервис");
            miSvcInstall = new ToolStripMenuItem("Установить сервис");
            miSvcStart = new ToolStripMenuItem("Запустить");
            miSvcStop = new ToolStripMenuItem("Остановить");
            miSvcRemove = new ToolStripMenuItem("Удалить сервис");
            miSvcStatus = new ToolStripMenuItem("Состояние и журнал");
            miSvcQueue = new ToolStripMenuItem("Собрать проект в фоне");
            miService.DropDownItems.AddRange(new ToolStripItem[] {
                miSvcInstall, miSvcRemove, new ToolStripSeparator(),
                miSvcStart, miSvcStop, new ToolStripSeparator(),
                miSvcQueue, miSvcStatus });

            miBrowser = new ToolStripMenuItem("Открыть в браузере");
            miFolder = new ToolStripMenuItem("Открыть папку проекта");

            miProject = new ToolStripMenuItem("Проект");
            miProperties = new ToolStripMenuItem("Свойства проекта...");
            miResources = new ToolStripMenuItem("Папка ресурсов");

            miNewForm = new ToolStripMenuItem("Новая форма...");
            miOpenForm = new ToolStripMenuItem("Открыть форму...");
            miFormCode = new ToolStripMenuItem("Сгенерировать код формы");

            miView = new ToolStripMenuItem("Вид");
            miViewExplorer = new ToolStripMenuItem("Обозреватель решений");
            miViewProps = new ToolStripMenuItem("Окно свойств");
            miViewToolbox = new ToolStripMenuItem("Панель элементов");
            miViewConsole = new ToolStripMenuItem("Вывод");
            miViewDockAll = new ToolStripMenuItem("Вернуть все окна на место");
            miViewExplorer.CheckOnClick = true;
            miViewProps.CheckOnClick = true;
            miViewToolbox.CheckOnClick = true;
            miViewConsole.CheckOnClick = true;
            miView.DropDownItems.AddRange(new ToolStripItem[] {
                miViewExplorer, miViewProps, miViewToolbox, miViewConsole,
                new ToolStripSeparator(), miViewDockAll });

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

            dock = new DockHost();
            properties = new PropertiesPanel();
            toolbox = new ToolboxPanel();
            designer = new FormDesignerPanel();

            explorer = new ExplorerForm();
            editors = new CodeEditorForm();
            console = new ConsoleForm();

            statusMain = new StatusStrip();
            lblProject = new ToolStripStatusLabel("  нет проекта  ");
            lblStatus = new ToolStripStatusLabel("Готово");
            lblPhp = new ToolStripStatusLabel("PHP не установлен");
            lblJava = new ToolStripStatusLabel("JDK не установлен");
            lblCaret = new ToolStripStatusLabel("1 : 1");

            SuspendLayout();

            // --- меню ---
            miFile.DropDownItems.AddRange(new ToolStripItem[] {
                miNewProject, miNewJava, miOpenProject, new ToolStripSeparator(),
                miSave, miSaveAll, new ToolStripSeparator(),
                miExport, miImport, new ToolStripSeparator(),
                miAutostart, miClose, new ToolStripSeparator(), miExit });

            miRun.DropDownItems.AddRange(new ToolStripItem[] {
                miStart, miStop, new ToolStripSeparator(), miBuildExe, miBuild });

            miTools.DropDownItems.AddRange(new ToolStripItem[] {
                miPhpInstall, miPhpInfo, miPhpRemove, new ToolStripSeparator(),
                miJdkInstall, miJdkInfo, miJdkRemove, new ToolStripSeparator(),
                miToolsGet, miToolsFolder, miToolsRemove, new ToolStripSeparator(),
                miService, new ToolStripSeparator(),
                miBrowser, miFolder });

            miProject.DropDownItems.AddRange(new ToolStripItem[] {
                miProperties, miResources, new ToolStripSeparator(),
                miNewForm, miOpenForm, miFormCode });
            miHelp.DropDownItems.Add(miAbout);

            miNewProject.ShortcutKeys = Keys.Control | Keys.N;
            miNewJava.ShortcutKeys = Keys.Control | Keys.Shift | Keys.N;
            miOpenProject.ShortcutKeys = Keys.Control | Keys.O;
            miSave.ShortcutKeys = Keys.Control | Keys.S;
            miSaveAll.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;
            miExport.ShortcutKeys = Keys.Control | Keys.E;
            miStart.ShortcutKeys = Keys.F5;
            miStop.ShortcutKeys = Keys.Shift | Keys.F5;
            miBuildExe.ShortcutKeys = Keys.Control | Keys.B;
            miClose.ShortcutKeys = Keys.Control | Keys.W;
            miProperties.ShortcutKeys = Keys.Alt | Keys.Enter;

            miAutostart.CheckOnClick = true;

            menuMain.Items.AddRange(new ToolStripItem[] { miFile, miProject, miView, miRun, miTools, miHelp });
            menuMain.BackColor = Theme.Chrome;
            menuMain.ForeColor = Theme.Text;
            menuMain.Padding = new Padding(6, 2, 0, 2);
            menuMain.Font = new Font("Segoe UI", 9f);
            menuMain.Renderer = new ToolStripProfessionalRenderer(new DarkColors());
            Theme.PaintMenu(menuMain.Items);

            // --- панель инструментов ---
            toolMain.Items.AddRange(new ToolStripItem[] {
                tbNew, tbOpen, tbSave, new ToolStripSeparator(),
                tbRun, tbStop, new ToolStripSeparator(), tbPhp, tbBrowser, tbExport });
            toolMain.BackColor = Theme.Chrome;
            toolMain.ForeColor = Theme.Text;
            toolMain.GripStyle = ToolStripGripStyle.Hidden;
            toolMain.Padding = new Padding(8, 5, 8, 5);
            toolMain.Font = new Font("Segoe UI", 9f);
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

            // --- встроенные окна ---
            dock.Dock = DockStyle.Fill;

            editors.Dock = DockStyle.Fill;
            designer.Dock = DockStyle.Fill;
            designer.Visible = false;
            dock.Center.Controls.Add(designer);
            dock.Center.Controls.Add(editors);

            dock.Add("Обозреватель решений", explorer, DockZone.Left);
            dock.Add("Свойства", properties, DockZone.Right);
            dock.Add("Панель элементов", toolbox, DockZone.Right);
            dock.Add("Вывод", console, DockZone.Bottom);

            explorer.Show();
            editors.Show();
            console.Show();

            // --- строка состояния ---
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
            lblJava.Margin = new Padding(0, 0, 18, 0);
            lblCaret.ForeColor = Color.FromArgb(205, 220, 245);

            statusMain.Items.AddRange(new ToolStripItem[] {
                lblProject, lblStatus, lblPhp, lblJava, lblCaret });

            // --- окно ---
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1240, 780);
            MinimumSize = new Size(900, 560);
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            Text = "Dupo Studio";
            StartPosition = FormStartPosition.CenterScreen;

            Controls.Add(dock);
            Controls.Add(statusMain);
            Controls.Add(toolMain);
            Controls.Add(menuMain);
            MainMenuStrip = menuMain;

            ResumeLayout(false);
            PerformLayout();
        }
    }
}
