using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using DupoLang;

namespace DupoStudio
{
    /// <summary>
    /// Окно проекта: меню, панель инструментов и три встроенных окна —
    /// ExplorerForm слева, CodeEditorForm в центре, ConsoleForm снизу.
    /// </summary>
    public partial class ProjectEditorForm : Form
    {
        string projectDir;
        ProjectSettings project = new ProjectSettings();
        Thread runThread;
        Interp interp;
        bool running;

        /// <summary>Пользователь закрыл проект — надо вернуться в стартовое окно.</summary>
        public bool BackToLauncher { get; private set; }

        static readonly string[] SkipDirs = { "build", "bin", "obj", ".git", ".vs", "runtime", "node_modules" };

        public ProjectEditorForm(string projectPath)
        {
            InitializeComponent();
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            WireUp();

            Load += delegate
            {
                dock.Collapse();
                RefreshView();

                Banner();

                if (!string.IsNullOrEmpty(projectPath) && Directory.Exists(projectPath))
                    OpenProject(projectPath);
                else
                    Status("Проект не найден: " + projectPath);
            };
        }

        void WireUp()
        {
            miNewProject.Click += delegate { NewProject(); };
            miNewJava.Click += delegate { NewJavaProject(); };
            miOpenProject.Click += delegate { OpenProjectDialog(); };
            miSave.Click += delegate { SaveActive(); };
            miSaveAll.Click += delegate { SaveAll(); };
            miExport.Click += delegate { ExportDapp(); };
            miImport.Click += delegate { ImportDapp(); };
            miAutostart.Click += delegate { ToggleAutostart(); };
            miClose.Click += delegate { CloseProject(); };
            miExit.Click += delegate { Close(); };

            miStart.Click += delegate { RunScript(); };
            miStop.Click += delegate { StopScript(); };
            miBuild.Click += delegate { BuildApp(); };
            miBuildExe.Click += delegate { BuildSingleExe(); };

            miPhpInstall.Click += delegate { InstallPhp(); };
            miPhpInfo.Click += delegate { Log("PHP: " + PhpTools.Instance.Version, Theme.Info); };
            miPhpRemove.Click += delegate { RemovePhp(); };
            miJdkInstall.Click += delegate { InstallJdk(); };
            miJdkInfo.Click += delegate { Log("Java: " + JavaTools.Instance.Version, Theme.Info); };
            miJdkRemove.Click += delegate { RemoveJdk(); };
            miToolsGet.Click += delegate { GetBuildTools(); };
            miToolsFolder.Click += delegate { OpenToolsFolder(); };
            miToolsRemove.Click += delegate { RemoveBuildTools(); };
            miSvcInstall.Click += delegate { ServiceAction("install"); };
            miSvcRemove.Click += delegate { ServiceAction("remove"); };
            miSvcStart.Click += delegate { ServiceAction("start"); };
            miSvcStop.Click += delegate { ServiceAction("stop"); };
            miSvcStatus.Click += delegate { ServiceStatus(); };
            miSvcQueue.Click += delegate { QueueBuild(); };

            miViewExplorer.Click += delegate { TogglePane("Обозреватель решений"); };
            miViewProps.Click += delegate { TogglePane("Свойства"); };
            miViewToolbox.Click += delegate { TogglePane("Панель элементов"); };
            miViewConsole.Click += delegate { TogglePane("Вывод"); };
            miViewAssistant.Click += delegate { TogglePane("Помощник"); };
            miViewDockAll.Click += delegate { dock.DockAll(); RefreshView(); };

            miNewForm.Click += delegate { NewForm(); };
            miNewWinForm.Click += delegate { NewWinForm(); };
            miToggleDesigner.Click += delegate { ToggleDesigner(); };
            miOpenForm.Click += delegate { OpenFormFile(); };
            miFormCode.Click += delegate { FormToCode(); };

            dock.Changed += delegate { RefreshView(); };

            toolbox.SelectionChanged += delegate { };
            designer.PickKind = delegate { return toolbox.Selected; };
            designer.Placed += delegate { toolbox.Reset(); ShowDesignerProps(); };
            designer.SelectionChanged += delegate { ShowDesignerProps(); };
            properties.Changed += delegate { PropertiesEdited(); };
            designer.OpenCode += delegate (object s2, string path) { ShowDesigner(false); OpenFile(path); };

            assistant.InsertCode = delegate (string code) { InsertIntoEditor(code); };
            assistant.Context = delegate { return ProjectSummary(); };
            miBrowser.Click += delegate { OpenBrowser(); };
            miFolder.Click += delegate { if (projectDir != null) Process.Start(projectDir); };
            miProperties.Click += delegate { ShowProperties(); };
            miResources.Click += delegate { OpenResources(); };
            miAbout.Click += delegate { About(); };

            tbNew.Click += delegate { NewProject(); };
            tbOpen.Click += delegate { OpenProjectDialog(); };
            tbSave.Click += delegate { SaveActive(); };
            tbRun.Click += delegate { RunScript(); };
            tbStop.Click += delegate { StopScript(); };
            tbPhp.Click += delegate { InstallPhp(); };
            tbBrowser.Click += delegate { OpenBrowser(); };
            tbExport.Click += delegate { ExportDapp(); };

            editors.ProjectDir = delegate { return projectDir; };
            explorer.FileActivated += delegate (object s, string path) { OpenFile(path); };
            editors.CaretMoved += delegate { UpdateCaret(); };

            FormClosing += OnClosing;
            UpdateRunState();
        }

        // ---------------- свойства проекта ----------------

        void ShowProperties()
        {
            if (projectDir == null) { Log("Сначала откройте проект.", Theme.Err); return; }

            using (var dlg = new PropertiesForm(project))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
            }

            Log("Свойства сохранены: " + project.FilePath, Theme.Ok);
            Log("Приложение: " + project.Name + " " + project.Version +
                "   ·   " + project.Author + "   ·   " + project.Product, Theme.Dim);

            RefreshTree();
        }

        void OpenResources()
        {
            if (projectDir == null) { Log("Сначала откройте проект.", Theme.Err); return; }

            string dir = project.ResourcesDir;
            Directory.CreateDirectory(dir);

            try { Process.Start(dir); }
            catch (Exception ex) { Log(ex.Message, Theme.Err); }
        }

        // ---------------- автозапуск ----------------

        bool IsAutostart
        {
            get
            {
                return projectDir != null &&
                       string.Equals(Settings.Autostart, projectDir, StringComparison.OrdinalIgnoreCase);
            }
        }

        void ToggleAutostart()
        {
            if (projectDir == null) return;

            Settings.Autostart = miAutostart.Checked ? projectDir : "";
            Settings.Save();

            Log(miAutostart.Checked
                ? "Этот проект будет открываться сразу при запуске."
                : "Автозапуск выключен — при запуске откроется стартовое окно.", Theme.Dim);
        }

        /// <summary>При закрытии проекта спрашиваем, открывать ли его сразу при следующем запуске.</summary>
        void AskAutostart()
        {
            if (projectDir == null || Settings.DontAsk || IsAutostart) return;

            using (var dlg = new AutostartDialog(Path.GetFileName(projectDir)))
            {
                DialogResult r = dlg.ShowDialog(this);

                if (dlg.DontAskAgain) Settings.DontAsk = true;
                if (r == DialogResult.Yes) Settings.Autostart = projectDir;

                Settings.Save();
            }
        }

        void CloseProject()
        {
            BackToLauncher = true;
            Close();
        }

        // ---------------- проект ----------------

        void NewProject()
        {
            string dir = ProjectTemplates.New(this);
            if (dir == null) return;

            OpenProject(dir);
            OpenFile(Path.Combine(dir, "main.dupolang"));
            Log("Проект создан: " + dir, Theme.Ok);
        }

        void NewJavaProject()
        {
            string dir = ProjectTemplates.CreateJava(this);
            if (dir == null) return;

            OpenProject(dir);
            OpenFile(Path.Combine(dir, "main.dupolang"));
            Log("Java-проект создан: " + dir, Theme.Ok);

            if (!JavaTools.Instance.IsInstalled)
                Log("JDK ещё не установлен — F5 поставит его сам, либо Инструменты → Установить JDK.", Theme.Dim);
        }

        void OpenProjectDialog()
        {
            string dir = ProjectTemplates.Choose(this, projectDir);
            if (dir != null) OpenProject(dir);
        }

        void OpenProject(string dir)
        {
            projectDir = dir;
            DupoRuntime.ProjectDir = dir;
            project = ProjectSettings.Load(dir);
            Text = "Dupo Studio — " + Path.GetFileName(dir);
            lblProject.Text = "  " + Path.GetFileName(dir) + "  ";
            RefreshTree();
            miAutostart.Checked = IsAutostart;
            Status(dir);
            ShowDesigner(false);
            ShowProjectProps();
            assistant.OpenProject(dir);

            string main = Path.Combine(dir, "main.dupolang");
            if (File.Exists(main)) OpenFile(main);
        }

        void RefreshTree()
        {
            explorer.LoadProject(projectDir);
        }

        /// <summary>Перечитать дерево после правок на диске.</summary>
        void ReloadTree() { RefreshTree(); }

        // ---------------- файлы ----------------

        CodeEditor ActiveEditor { get { return editors.Active; } }

        void OpenFile(string path)
        {
            // Формы открываются в конструкторе, остальное — в редакторе.
            if (path != null && path.EndsWith(FormDesignerPanel.Extension, StringComparison.OrdinalIgnoreCase))
            {
                OpenForm(path);
                return;
            }

            if (WinFormsCode.IsDesigner(path))
            {
                OpenWinForm(path);
                return;
            }

            try
            {
                if (designer.Visible) ShowDesigner(false);
                editors.Open(path);
                Status(path);
            }
            catch (Exception ex) { Log("Не открылось: " + ex.Message, Theme.Err); }
        }

        void SaveActive()
        {
            if (designer.Visible)
            {
                try
                {
                    string saved = designer.SaveAny();
                    Log("Форма сохранена: " + Path.GetFileName(saved), Theme.Ok);
                    ReloadTree();
                }
                catch (Exception ex) { Log("Форма не сохранилась: " + ex.Message, Theme.Err); }
                return;
            }

            var ed = ActiveEditor;
            if (ed == null || ed.FilePath == null) return;

            editors.SaveActive();
            Status("Сохранено: " + ed.FilePath);
        }

        void SaveAll()
        {
            editors.SaveAll();
            Status("Все файлы сохранены");
        }

        // ---------------- запуск ----------------

        void RunScript()
        {
            if (running) { Log("Уже запущено — сначала «Стоп».", Theme.Err); return; }

            SaveAll();

            string script = null;
            var ed = ActiveEditor;
            if (ed != null && ed.FilePath != null && ed.FilePath.EndsWith(".dupolang", StringComparison.OrdinalIgnoreCase))
                script = ed.FilePath;

            if (script == null && projectDir != null)
            {
                string main = Path.Combine(projectDir, "main.dupolang");
                if (File.Exists(main)) script = main;
            }

            if (script == null) { Log("Не нашёл файл .dupolang для запуска.", Theme.Err); return; }

            console.Clear();
            Log("▶  " + Path.GetFileName(script), Theme.Info);
            Log(new string('-', 60), Theme.Dim);

            running = true;
            UpdateRunState();

            string path = script;
            runThread = new Thread(delegate ()
            {
                try
                {
                    interp = Dupo.RunFile(path, delegate (string s) { Log(s, Theme.Text); });
                    if (interp.HasLiveResources)
                        Log("Скрипт отработал. Сервер продолжает работать — «Стоп» остановит.", Theme.Ok);
                    else
                    {
                        Log("Готово.", Theme.Ok);
                        BeginInvoke(new Action(FinishRun));
                    }
                }
                catch (DupoError de)
                {
                    Log("✖  " + de, Theme.Err);
                    BeginInvoke(new Action(FinishRun));
                }
                catch (ThreadAbortException) { }
                catch (Exception ex)
                {
                    Log("✖  " + ex.Message, Theme.Err);
                    BeginInvoke(new Action(FinishRun));
                }
            });
            runThread.IsBackground = true;
            runThread.SetApartmentState(ApartmentState.STA);
            runThread.Start();
        }

        void StopScript()
        {
            if (interp != null) { interp.Shutdown(); interp = null; }
            if (runThread != null && runThread.IsAlive)
            {
                try { runThread.Abort(); } catch { }
            }
            runThread = null;
            FinishRun();
            Log("■  Остановлено.", Theme.Info);
        }

        void FinishRun()
        {
            running = false;
            UpdateRunState();
        }

        void UpdateRunState()
        {
            tbRun.Enabled = miStart.Enabled = !running;
            tbStop.Enabled = miStop.Enabled = running;
            Status(running ? "Выполняется..." : "Готово");
        }

        // ---------------- PHP ----------------

        void InstallPhp()
        {
            PhpTools.Log = delegate (string s) { Log(s, Theme.Text); };

            if (PhpTools.Instance.IsInstalled)
            {
                Log("PHP уже установлен: " + PhpTools.Dir, Theme.Ok);
                Log(PhpTools.Instance.Version, Theme.Dim);
                return;
            }

            Log("Устанавливаю PHP...", Theme.Info);
            tbPhp.Enabled = false;

            var t = new Thread(delegate ()
            {
                try
                {
                    PhpTools.Instance.Install();
                    Log("PHP: " + PhpTools.Instance.Version, Theme.Ok);
                    BeginInvoke(new Action(UpdatePhpStatus));
                }
                catch (Exception ex) { Log("✖  PHP не поставился: " + ex.Message, Theme.Err); }
                finally { BeginInvoke(new Action(delegate { tbPhp.Enabled = true; })); }
            });
            t.IsBackground = true;
            t.Start();
        }

        void InstallJdk()
        {
            JavaTools.Log = delegate (string s) { Log(s, Theme.Text); };

            if (JavaTools.Instance.IsInstalled)
            {
                Log("JDK уже установлен: " + JavaTools.Home, Theme.Ok);
                Log(JavaTools.Instance.Version, Theme.Dim);
                return;
            }

            Log("Ставлю JDK — скачивание около 190 МБ, это займёт несколько минут.", Theme.Info);
            miJdkInstall.Enabled = false;

            var t = new Thread(delegate ()
            {
                try
                {
                    JavaTools.Instance.Install();
                    BeginInvoke(new Action(UpdateJavaStatus));
                }
                catch (Exception ex) { Log("✖  JDK не поставился: " + ex.Message, Theme.Err); }
                finally { BeginInvoke(new Action(delegate { miJdkInstall.Enabled = true; })); }
            });
            t.IsBackground = true;
            t.Start();
        }

        void RemoveJdk()
        {
            JavaTools.Log = delegate (string s) { Log(s, Theme.Text); };
            if (MessageBox.Show(this, "Удалить установленный JDK?", "Dupo Studio",
                                MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            try { JavaTools.Instance.Uninstall(); UpdateJavaStatus(); }
            catch (Exception ex) { Log(ex.Message, Theme.Err); }
        }

        /// <summary>BuildTools качаются с GitHub: ссылка ведёт на свежий выпуск,
        /// поэтому один архив подходит любой версии студии.</summary>
        void GetBuildTools()
        {
            BuildTools.Log = delegate (string s) { Log(s, Theme.Text); };

            if (BuildTools.Instance.IsInstalled)
            {
                Log("BuildTools уже на месте: " + BuildTools.Dir, Theme.Ok);
                Log(BuildTools.Instance.Version, Theme.Dim);

                if (MessageBox.Show(this, "Скачать заново, взяв свежий выпуск?", "Dupo Studio",
                                    MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            }

            Log("Качаю BuildTools...", Theme.Info);
            miToolsGet.Enabled = false;

            var t = new Thread(delegate ()
            {
                try { BuildTools.Instance.Reinstall(); }
                catch (Exception ex) { Log("✖  Не скачалось: " + ex.Message, Theme.Err); }
                finally { BeginInvoke(new Action(delegate { miToolsGet.Enabled = true; })); }
            });
            t.IsBackground = true;
            t.Start();
        }

        // ------------------------------------------------------------------
        // панели: показать, спрятать, вернуть из окна
        // ------------------------------------------------------------------

        /// <summary>Delete убирает выделенный элемент, пока открыт дизайнер.</summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Delete && designer != null && designer.Visible && designer.Selected != null)
            {
                designer.RemoveSelected();
                ShowDesignerProps();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ------------------------------------------------------------------
        // помощник
        // ------------------------------------------------------------------

        /// <summary>Вставить код из ответа помощника в открытый файл.</summary>
        void InsertIntoEditor(string code)
        {
            if (string.IsNullOrEmpty(code)) return;

            var ed = ActiveEditor;
            if (ed == null)
            {
                Log("Откройте файл — вставлять некуда.", Theme.Dim);
                return;
            }

            if (designer.Visible) ShowDesigner(false);

            ed.SelectedText = code.EndsWith(Environment.NewLine) ? code : code + Environment.NewLine;
            ed.Focus();
            Log("Код вставлен из ответа помощника.", Theme.Ok);
        }

        /// <summary>Чем описать открытый проект помощнику.</summary>
        string ProjectSummary()
        {
            if (projectDir == null) return "";

            var sb = new StringBuilder();
            sb.AppendLine("Папка: " + projectDir);
            sb.AppendLine("Название: " + (project.Name.Length > 0 ? project.Name : Path.GetFileName(projectDir)));
            if (project.Description.Length > 0) sb.AppendLine("Описание: " + project.Description);
            if (project.Namespace.Length > 0) sb.AppendLine("Пространство имён: " + project.Namespace);
            if (project.JavaPackage.Length > 0) sb.AppendLine("Пакет Java: " + project.JavaPackage);

            try
            {
                var files = Directory.GetFiles(projectDir, "*.*", SearchOption.AllDirectories)
                    .Where(f => !f.Contains("\\build\\") && !f.Contains("\\bin\\") &&
                                !f.Contains("\\obj\\") && !f.Contains("\\.dupo\\"))
                    .Select(f => f.Substring(projectDir.Length).TrimStart('\\'))
                    .Take(40)
                    .ToArray();

                if (files.Length > 0)
                {
                    sb.AppendLine("Файлы:");
                    foreach (string f in files) sb.AppendLine("  " + f);
                }
            }
            catch { }

            var ed = ActiveEditor;
            if (ed != null && ed.FilePath != null)
                sb.AppendLine("Открыт файл: " + Path.GetFileName(ed.FilePath));

            return sb.ToString();
        }

        void TogglePane(string title)
        {
            var pane = dock.Find(title);
            if (pane == null) return;

            if (dock.IsShown(pane)) dock.HidePane(pane);
            else dock.Show(pane);

            RefreshView();
        }

        void RefreshView()
        {
            miViewExplorer.Checked = dock.IsShown(dock.Find("Обозреватель решений"));
            miViewProps.Checked = dock.IsShown(dock.Find("Свойства"));
            miViewToolbox.Checked = dock.IsShown(dock.Find("Панель элементов"));
            miViewConsole.Checked = dock.IsShown(dock.Find("Вывод"));
            miViewAssistant.Checked = dock.IsShown(dock.Find("Помощник"));
        }

        /// <summary>Окно свойств показывает проект, когда дизайнер закрыт.</summary>
        void ShowProjectProps()
        {
            properties.Bind(new ProjectProxy(project),
                            "Проект: " + (project.Name.Length > 0 ? project.Name : "без имени"));
        }

        void ShowDesignerProps()
        {
            if (!designer.Visible) { ShowProjectProps(); return; }

            var sel = designer.Selected;
            properties.Bind(designer.PropertyTarget,
                            sel == null
                                ? "Форма: " + designer.FormName
                                : sel.Name + " — " + ToolboxPanel.NameOf(sel.Kind));
        }

        /// <summary>В окне свойств что-то поправили.</summary>
        void PropertiesEdited()
        {
            if (designer.Visible) { designer.Touch(); ShowDesignerProps(); return; }

            try { project.Save(); }
            catch (Exception ex) { Log("Свойства не сохранились: " + ex.Message, Theme.Err); }
            RefreshTitle();
        }

        void RefreshTitle()
        {
            string label = project.Name.Length > 0 ? project.Name : Path.GetFileName(projectDir ?? "");
            lblProject.Text = string.IsNullOrEmpty(label) ? "нет проекта" : label;
        }

        // ------------------------------------------------------------------
        // дизайнер форм
        // ------------------------------------------------------------------

        void ShowDesigner(bool on)
        {
            designer.Visible = on;
            editors.Visible = !on;
            if (on) designer.BringToFront(); else editors.BringToFront();

            var toolboxPane = dock.Find("Панель элементов");
            if (on && !dock.IsShown(toolboxPane)) dock.Show(toolboxPane);

            var propsPane = dock.Find("Свойства");
            if (on && !dock.IsShown(propsPane)) dock.Show(propsPane);

            ShowDesignerProps();
            RefreshView();
        }

        void NewForm()
        {
            if (projectDir == null) { Log("Сначала откройте проект.", Theme.Dim); return; }

            string name = PromptDialog.Show(this, "Новая форма", "Имя формы:", "Form1");
            if (string.IsNullOrEmpty(name)) return;

            string file = Path.Combine(projectDir, ProjectTemplates.Ident(name) + FormDesignerPanel.Extension);
            if (File.Exists(file))
            {
                Log("Такая форма уже есть: " + Path.GetFileName(file), Theme.Dim);
                return;
            }

            designer.Reset(file, name);
            try
            {
                designer.Save(file);
                Log("Форма создана: " + Path.GetFileName(file), Theme.Ok);
            }
            catch (Exception ex) { Log("Форма не сохранилась: " + ex.Message, Theme.Err); }

            ShowDesigner(true);
            ReloadTree();
        }

        /// <summary>Создать пару Form1.cs + Form1.Designer.cs.</summary>
        void NewWinForm()
        {
            if (projectDir == null) { Log("Сначала откройте проект.", Theme.Dim); return; }

            string name = PromptDialog.Show(this, "Новая форма Windows Forms", "Класс формы:", "Form1");
            if (string.IsNullOrEmpty(name)) return;

            string cls = ProjectTemplates.Ident(name);
            string dir = Path.Combine(projectDir, "src");
            Directory.CreateDirectory(dir);

            string designerPath = Path.Combine(dir, cls + WinFormsCode.DesignerSuffix);
            if (File.Exists(designerPath))
            {
                Log("Такая форма уже есть: " + cls, Theme.Dim);
                return;
            }

            var form = new WinFormsForm();
            form.DesignerPath = designerPath;
            form.Namespace = project.Namespace.Length > 0
                ? project.Namespace : ProjectTemplates.Ident(project.Name);
            form.ClassName = cls;
            form.Title = name;

            var button = new DesignItem();
            button.Kind = "Button";
            button.Name = "button1";
            button.Caption = "Нажми меня";
            button.Bounds = new Rectangle(24, 24, 140, 32);
            button.Click = true;
            form.Items.Add(button);

            try
            {
                WinFormsCode.Write(form, false);

                // Точка входа, если её ещё нет в проекте.
                string program = Path.Combine(dir, "Program.cs");
                bool haveMain = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
                    .Any(f => File.ReadAllText(f).Contains("static void Main"));

                if (!haveMain && !File.Exists(program))
                    File.WriteAllText(program, WinFormsCode.Program(form), new UTF8Encoding(false));

                Log("Форма создана: " + Path.GetFileName(designerPath), Theme.Ok);
            }
            catch (Exception ex) { Log("Форма не создалась: " + ex.Message, Theme.Err); return; }

            OpenWinForm(designerPath);
            ReloadTree();
        }

        /// <summary>Открыть Form1.Designer.cs в конструкторе.</summary>
        public void OpenWinForm(string designerPath)
        {
            try
            {
                designer.LoadWinForms(designerPath);
                ShowDesigner(true);
                Log("Конструктор: " + Path.GetFileName(designerPath), Theme.Info);
            }
            catch (Exception ex) { Log("Форма не разобралась: " + ex.Message, Theme.Err); }
        }

        /// <summary>F7 — переключение между конструктором и кодом, как в Visual Studio.</summary>
        void ToggleDesigner()
        {
            if (designer.Visible)
            {
                string code = designer.WinForms
                    ? WinFormsCode.CodeFileOf(designer.FilePath)
                    : null;

                ShowDesigner(false);
                if (code != null && File.Exists(code)) OpenFile(code);
                return;
            }

            var ed = ActiveEditor;
            string path = ed == null ? null : ed.FilePath;

            if (path != null && WinFormsCode.IsDesigner(path)) { OpenWinForm(path); return; }

            string sibling = WinFormsCode.DesignerFileOf(path);
            if (sibling != null) { OpenWinForm(sibling); return; }

            if (path != null && path.EndsWith(FormDesignerPanel.Extension, StringComparison.OrdinalIgnoreCase))
            {
                OpenForm(path);
                return;
            }

            Log("Для этого файла конструктора нет — откройте .Designer.cs или .dupoform.", Theme.Dim);
        }

        void OpenFormFile()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "Формы Dupo (*" + FormDesignerPanel.Extension + ")|*" +
                             FormDesignerPanel.Extension + "|Все файлы (*.*)|*.*";
                if (Directory.Exists(projectDir)) dlg.InitialDirectory = projectDir;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                OpenForm(dlg.FileName);
            }
        }

        /// <summary>Открыть .dupoform в дизайнере.</summary>
        public void OpenForm(string path)
        {
            try
            {
                designer.Load(path);
                ShowDesigner(true);
                Log("Форма открыта: " + Path.GetFileName(path), Theme.Info);
            }
            catch (Exception ex) { Log("Форма не открылась: " + ex.Message, Theme.Err); }
        }

        void FormToCode()
        {
            if (!designer.Visible) { Log("Сначала откройте форму в дизайнере.", Theme.Dim); return; }

            try
            {
                designer.Save(null);

                string ns = project.Namespace.Length > 0
                    ? project.Namespace
                    : ProjectTemplates.Ident(project.Name);

                string dir = Path.Combine(projectDir, "src");
                Directory.CreateDirectory(dir);

                string file = Path.Combine(dir, designer.FormName + ".cs");
                File.WriteAllText(file, designer.ToCSharp(ns), new UTF8Encoding(false));

                Log("Код формы: " + file, Theme.Ok);
                ShowDesigner(false);
                OpenFile(file);
                ReloadTree();
            }
            catch (Exception ex) { Log("Код не сгенерировался: " + ex.Message, Theme.Err); }
        }

        // ------------------------------------------------------------------
        // фоновый сервис студии
        // ------------------------------------------------------------------

        void ServiceAction(string what)
        {
            var svc = AgentService.Describe();
            ServiceTools.Log = delegate (string s) { Log(s, Theme.Info); };

            try
            {
                switch (what)
                {
                    case "install":
                        if (svc.Install())
                        {
                            svc.Start();
                            Log("Сервис будет подниматься сам при входе в систему.", Theme.Dim);
                        }
                        break;
                    case "remove": svc.Uninstall(); break;
                    case "start": svc.Start(); break;
                    case "stop": StopAgent(svc); break;
                }
            }
            catch (Exception ex) { Log("Сервис: " + ex.Message, Theme.Err); }

            Log(svc.Name + " — " + svc.State, Theme.Dim);
        }

        /// <summary>Сначала просим агента выйти по-хорошему, потом — силой.</summary>
        void StopAgent(DupoService svc)
        {
            try
            {
                var psi = new ProcessStartInfo(svc.ExePath, AgentService.Switch + " --stop");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                using (var p = Process.Start(psi)) p.WaitForExit(5000);
            }
            catch { }

            Thread.Sleep(300);
            if (svc.Running) svc.Stop();
            else Log("Сервис " + svc.Name + " остановлен", Theme.Info);
        }

        void ServiceStatus()
        {
            var svc = AgentService.Describe();
            Log("── Фоновый сервис ──", Theme.Info);
            foreach (var line in svc.Info().Split('\r', '\n'))
                if (line.Trim().Length > 0) Log(line.Trim(), Theme.Dim);
            Log("Заданий в очереди: " + AgentService.Pending(), Theme.Dim);
            Log("Журнал: " + AgentService.LogPath, Theme.Dim);
            foreach (var line in AgentService.Tail(12).Split('\r', '\n'))
                if (line.Trim().Length > 0) Log("  " + line.Trim(), Theme.Dim);
        }

        void QueueBuild()
        {
            if (projectDir == null) { Log("Проект не открыт.", Theme.Dim); return; }

            var svc = AgentService.Describe();
            AgentService.Enqueue("build " + projectDir);
            Log("Задание на сборку поставлено в очередь агенту.", Theme.Ok);

            if (!svc.Running)
            {
                ServiceTools.Log = delegate (string s) { Log(s, Theme.Info); };
                svc.Start();
            }
        }

        void OpenToolsFolder()
        {
            if (!BuildTools.Instance.IsInstalled)
            {
                Log("BuildTools ещё не скачаны — «Инструменты → Скачать BuildTools».", Theme.Dim);
                return;
            }
            try { Process.Start(BuildTools.Dir); }
            catch (Exception ex) { Log(ex.Message, Theme.Err); }
        }

        void RemoveBuildTools()
        {
            BuildTools.Log = delegate (string s) { Log(s, Theme.Text); };
            if (MessageBox.Show(this, "Удалить скачанные BuildTools?", "Dupo Studio",
                                MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            try { BuildTools.Instance.Uninstall(); }
            catch (Exception ex) { Log(ex.Message, Theme.Err); }
        }

        void RemovePhp()
        {
            PhpTools.Log = delegate (string s) { Log(s, Theme.Text); };
            if (MessageBox.Show(this, "Удалить установленный PHP?", "Dupo Studio",
                                MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            try { PhpTools.Instance.Uninstall(); UpdatePhpStatus(); }
            catch (Exception ex) { Log(ex.Message, Theme.Err); }
        }

        void OpenBrowser()
        {
            string url = "http://localhost:" + DupoWebServer.LastPort + "/";
            try { Process.Start(url); Log("Открываю " + url, Theme.Dim); }
            catch (Exception ex) { Log(ex.Message, Theme.Err); }
        }

        // ---------------- сборка ----------------

        /// <summary>Один exe: хост-рантайм с приложением в хвосте.</summary>
        void BuildSingleExe()
        {
            if (projectDir == null) { Log("Сначала откройте проект.", Theme.Err); return; }

            SaveAll();
            DappPackage.Log = delegate (string s) { Log(s, Theme.Text); };
            DappBundle.Log = delegate (string s) { Log(s, Theme.Text); };

            string host = EmbeddedRuntime.HostPath();
            if (host == null)
            {
                Log("Рантайм DupoHost не найден ни рядом со студией, ни внутри неё.", Theme.Err);
                return;
            }

            string name = Path.GetFileName(projectDir);
            string outDir = Path.Combine(projectDir, "build");
            string temp = Path.Combine(Path.GetTempPath(),
                DappPackage.SafeName(name) + "-" + Guid.NewGuid().ToString("N") + DappPackage.Extension);

            try
            {
                Directory.CreateDirectory(outDir);

                var manifest = new DappManifest();
                project.IntoManifest(manifest);
                manifest.Created = DateTime.Now.ToString("dd.MM.yyyy HH:mm");

                var ed = ActiveEditor;
                if (ed != null && ed.FilePath != null &&
                    ed.FilePath.EndsWith(".dupolang", StringComparison.OrdinalIgnoreCase))
                {
                    manifest.Main = ed.FilePath.Substring(projectDir.Length)
                        .TrimStart('\\', '/').Replace('\\', '/');
                }

                DappPackage.Create(projectDir, temp, manifest);
                byte[] payload = File.ReadAllBytes(temp);

                string outExe = Path.Combine(outDir, DappPackage.SafeName(name) + ".exe");
                DappBundle.Build(host, outExe, payload);

                Log("Готово — это один самостоятельный файл, DLL рядом не нужны.", Theme.Ok);
                RefreshTree();
            }
            catch (Exception ex) { Log("✖  Сборка не удалась: " + ex.Message, Theme.Err); }
            finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
        }


        void BuildApp()
        {
            if (projectDir == null) { Log("Сначала откройте проект.", Theme.Err); return; }

            if (project.SingleExe)
            {
                Log("В свойствах проекта включено «Собирать в один exe» — собираю так.", Theme.Dim);
                BuildSingleExe();
                return;
            }

            SaveAll();
            string name = Path.GetFileName(projectDir);
            string outDir = Path.Combine(projectDir, "build");

            try
            {
                if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
                Directory.CreateDirectory(outDir);

                CopyTree(projectDir, outDir);

                string runner = EmbeddedRuntime.HostPath();
                if (runner == null)
                {
                    Log("Рантайм DupoHost не найден ни рядом со студией, ни внутри неё.", Theme.Err);
                    return;
                }

                // В рантайме язык уже внутри, отдельная DupoLang.dll не нужна.
                File.Copy(runner, Path.Combine(outDir, name + ".exe"), true);

                Log("Собрано: " + Path.Combine(outDir, name + ".exe"), Theme.Ok);
                Log("Рядом лежат исходники — код видно и можно править.", Theme.Dim);
                RefreshTree();
            }
            catch (Exception ex) { Log("✖  Сборка не удалась: " + ex.Message, Theme.Err); }
        }

        static void CopyTree(string from, string to)
        {
            foreach (string file in Directory.GetFiles(from))
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)), true);

            foreach (string dir in Directory.GetDirectories(from))
            {
                string name = Path.GetFileName(dir);
                if (SkipDirs.Contains(name.ToLowerInvariant())) continue;
                string target = Path.Combine(to, name);
                Directory.CreateDirectory(target);
                CopyTree(dir, target);
            }
        }

        // ---------------- формат .dapp ----------------

        void ExportDapp()
        {
            if (projectDir == null) { Log("Сначала откройте проект.", Theme.Err); return; }

            SaveAll();
            DappPackage.Log = delegate (string s) { Log(s, Theme.Text); };

            string name = Path.GetFileName(projectDir);

            using (var dlg = new SaveFileDialog())
            {
                dlg.Title = "Экспорт приложения";
                dlg.Filter = "Приложение DupoLang (*.dapp)|*.dapp";
                dlg.FileName = DappPackage.SafeName(name) + DappPackage.Extension;
                dlg.InitialDirectory = Path.Combine(DupoRuntime.RuntimeDir, "Projects");
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                var manifest = new DappManifest();
                project.IntoManifest(manifest);
                manifest.Version = PromptDialog.Show(this, "Экспорт", "Версия приложения:",
                                                     project.Version) ?? project.Version;
                manifest.Created = DateTime.Now.ToString("dd.MM.yyyy HH:mm");

                var ed = ActiveEditor;
                if (ed != null && ed.FilePath != null &&
                    ed.FilePath.EndsWith(".dupolang", StringComparison.OrdinalIgnoreCase))
                {
                    manifest.Main = ed.FilePath.Substring(projectDir.Length)
                        .TrimStart('\\', '/').Replace('\\', '/');
                }

                try
                {
                    string file = DappPackage.Create(projectDir, dlg.FileName, manifest);
                    Log("Экспорт готов: " + file, Theme.Ok);
                    Log("Запуск: DupoRun.exe \"" + Path.GetFileName(file) + "\"", Theme.Dim);
                }
                catch (Exception ex) { Log("✖  Экспорт не удался: " + ex.Message, Theme.Err); }
            }
        }

        void ImportDapp()
        {
            DappPackage.Log = delegate (string s) { Log(s, Theme.Text); };

            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Импорт приложения";
                dlg.Filter = "Приложение DupoLang (*.dapp)|*.dapp";
                dlg.InitialDirectory = Path.Combine(DupoRuntime.RuntimeDir, "Projects");
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    DappInfo info = DappPackage.Read(dlg.FileName);
                    Log(info.Summary(), Theme.Info);

                    string baseDir = Path.Combine(DupoRuntime.RuntimeDir, "Projects",
                                                  DappPackage.SafeName(info.Manifest.Name));
                    string target = baseDir;
                    int n = 2;
                    while (Directory.Exists(target)) { target = baseDir + "_" + n; n++; }

                    string main = DappPackage.Extract(dlg.FileName, target);
                    OpenProject(target);
                    if (File.Exists(main)) OpenFile(main);
                    Log("Импортировано: " + target, Theme.Ok);
                }
                catch (Exception ex) { Log("✖  Импорт не удался: " + ex.Message, Theme.Err); }
            }
        }

        // ---------------- мелочи ----------------

        void Banner()
        {
            Log("  ╭───────────────────────────────────────────────╮", Theme.Accent);
            Log("  │   D U P O   S T U D I O   ·   App Maker        │", Theme.Accent);
            Log("  ╰───────────────────────────────────────────────╯", Theme.Accent);
            Log("  DupoLang " + Dupo.Version + "   ·   Dupo.Web · Dupo.Java · Dupo.System.Files · Dupo.App · Dupo.Tools", Theme.Dim);
            Log("  F5 запуск   ·   Shift+F5 стоп   ·   Ctrl+B один .exe   ·   Ctrl+E .dapp", Theme.Dim);
            Log("", Theme.Text);
            UpdatePhpStatus();
        }

        void UpdatePhpStatus()
        {
            bool ok = PhpTools.Instance.IsInstalled;
            lblPhp.Text = ok ? "PHP готов" : "PHP не установлен";
            lblPhp.ForeColor = ok ? Color.FromArgb(180, 240, 200) : Color.FromArgb(255, 220, 170);
            UpdateJavaStatus();
        }

        void UpdateJavaStatus()
        {
            bool ok = JavaTools.Instance.IsInstalled;
            lblJava.Text = ok ? "JDK готов" : "JDK не установлен";
            lblJava.ForeColor = ok ? Color.FromArgb(180, 240, 200) : Color.FromArgb(255, 220, 170);
        }

        void About()
        {
            MessageBox.Show(this,
                "Dupo Studio — App Maker\r\n" +
                "Язык DupoLang " + Dupo.Version + "\r\n\r\n" +
                "import Dupo.Web\r\n" +
                "Web.Server s = new Web.Server(8080, \"www\", \"index\")\r\n" +
                "Web.Server.Php.Install()\r\n" +
                "s.Start()",
                "О программе", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void Log(string text, Color color)
        {
            if (text == null) text = "";
            if (InvokeRequired) { BeginInvoke(new Action<string, Color>(Log), text, color); return; }

            console.Write(text, color);
        }

        void Status(string text)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(Status), text); return; }
            lblStatus.Text = text;
        }

        void UpdateCaret()
        {
            var ed = ActiveEditor;
            lblCaret.Text = ed == null ? "" : ed.CurrentLine + " : " + ed.CurrentColumn;
        }

        void OnClosing(object sender, FormClosingEventArgs e)
        {
            try { assistant.Store(); } catch { }

            if (designer.Visible && designer.Dirty)
            {
                var d = MessageBox.Show(this, "Форма изменена. Сохранить?",
                    "Dupo Studio", MessageBoxButtons.YesNoCancel);
                if (d == DialogResult.Cancel) { e.Cancel = true; return; }
                if (d == DialogResult.Yes)
                {
                    try { designer.SaveAny(); }
                    catch (Exception ex) { Log("Форма не сохранилась: " + ex.Message, Theme.Err); }
                }
            }

            if (editors.HasDirty)
            {
                var r = MessageBox.Show(this, "Есть несохранённые изменения. Сохранить?",
                    "Dupo Studio", MessageBoxButtons.YesNoCancel);
                if (r == DialogResult.Cancel) { e.Cancel = true; return; }
                if (r == DialogResult.Yes) SaveAll();
            }

            AskAutostart();

            if (interp != null) { try { interp.Shutdown(); } catch { } }
            if (runThread != null && runThread.IsAlive) { try { runThread.Abort(); } catch { } }
        }
    }
}
