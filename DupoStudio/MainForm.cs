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
    public partial class MainForm : Form
    {
        string projectDir;
        Thread runThread;
        Interp interp;
        bool running;

        static readonly Font tabFont = new Font("Segoe UI", 9f);
        static readonly string[] SkipDirs = { "build", "bin", "obj", ".git", ".vs", "runtime", "node_modules" };

        public MainForm(string[] args)
        {
            InitializeComponent();
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            WireUp();

            string start = args != null && args.Length > 0 && Directory.Exists(args[0])
                ? args[0]
                : Path.Combine(DupoRuntime.RuntimeDir, "Projects", "HelloWeb");

            Load += delegate
            {
                try { splitMain.SplitterDistance = 260; } catch { }
                try { splitRight.SplitterDistance = Math.Max(120, splitRight.Height - 230); } catch { }
                Banner();
                if (Directory.Exists(start)) OpenProject(start);
                else Status("Создайте проект: Файл → Новый проект");
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
            miBrowser.Click += delegate { OpenBrowser(); };
            miFolder.Click += delegate { if (projectDir != null) Process.Start(projectDir); };
            miAbout.Click += delegate { About(); };

            tbNew.Click += delegate { NewProject(); };
            tbOpen.Click += delegate { OpenProjectDialog(); };
            tbSave.Click += delegate { SaveActive(); };
            tbRun.Click += delegate { RunScript(); };
            tbStop.Click += delegate { StopScript(); };
            tbPhp.Click += delegate { InstallPhp(); };
            tbBrowser.Click += delegate { OpenBrowser(); };
            tbExport.Click += delegate { ExportDapp(); };

            treeProject.NodeMouseDoubleClick += delegate (object s, TreeNodeMouseClickEventArgs e)
            {
                string path = e.Node.Tag as string;
                if (path != null && File.Exists(path)) OpenFile(path);
            };

            tabsEditor.DrawItem += DrawTab;
            tabsEditor.SelectedIndexChanged += delegate { UpdateCaret(); };

            FormClosing += OnClosing;
            UpdateRunState();
        }

        // ---------------- проект ----------------

        void NewProject()
        {
            string name = PromptDialog.Show(this, "Новый проект", "Название проекта:", "MyApp");
            if (string.IsNullOrEmpty(name)) return;

            string dir = Path.Combine(DupoRuntime.RuntimeDir, "Projects", name);
            if (Directory.Exists(dir))
            {
                MessageBox.Show(this, "Такой проект уже есть.", "Dupo Studio");
                return;
            }

            Directory.CreateDirectory(Path.Combine(dir, "www"));

            var code = new StringBuilder();
            code.AppendLine("// " + name + " — проект Dupo Studio");
            code.AppendLine("import Dupo.Web");
            code.AppendLine();
            code.AppendLine("print(\"Запускаю " + name + "...\")");
            code.AppendLine();
            code.AppendLine("Web.Server site = new Web.Server(8080, \"www\", \"index.html\")");
            code.AppendLine();
            code.AppendLine("if (Web.Server.Php.IsInstalled == false) {");
            code.AppendLine("    Web.Server.Php.Install()");
            code.AppendLine("}");
            code.AppendLine();
            code.AppendLine("site.Start()");
            code.AppendLine("Web.Open(site.Url)");

            File.WriteAllText(Path.Combine(dir, "main.dupolang"), code.ToString(), new UTF8Encoding(false));

            string html =
                "<!DOCTYPE html>\r\n<html lang=\"ru\">\r\n<head>\r\n<meta charset=\"utf-8\">\r\n" +
                "<title>" + name + "</title>\r\n</head>\r\n<body style=\"font-family:Segoe UI,sans-serif;" +
                "background:#141821;color:#e6e9ef;padding:48px\">\r\n<h1>" + name + "</h1>\r\n" +
                "<p>Страница раздаётся сервером DupoLang.</p>\r\n</body>\r\n</html>";
            File.WriteAllText(Path.Combine(dir, "www", "index.html"), html, new UTF8Encoding(false));

            OpenProject(dir);
            OpenFile(Path.Combine(dir, "main.dupolang"));
            Log("Проект создан: " + dir, Theme.Ok);
        }

        /// <summary>Проект на Java: исходники в src, сборка в .jar скриптом DupoLang.</summary>
        void NewJavaProject()
        {
            string name = PromptDialog.Show(this, "Новый Java-проект", "Название проекта:", "MyJavaApp");
            if (string.IsNullOrEmpty(name)) return;

            string dir = Path.Combine(DupoRuntime.RuntimeDir, "Projects", name);
            if (Directory.Exists(dir))
            {
                MessageBox.Show(this, "Такой проект уже есть.", "Dupo Studio");
                return;
            }

            Directory.CreateDirectory(Path.Combine(dir, "src"));

            var code = new StringBuilder();
            code.AppendLine("// " + name + " — проект на Java");
            code.AppendLine("using Dupo.Java");
            code.AppendLine("using Dupo.System.Files");
            code.AppendLine();
            code.AppendLine("print(" + Q + "Собираю " + name + "..." + Q + ")");
            code.AppendLine();
            code.AppendLine("if (Java.IsInstalled == false) {");
            code.AppendLine("    Java.Install()");
            code.AppendLine("}");
            code.AppendLine();
            code.AppendLine("Java.Compile(" + Q + "src" + Q + ", " + Q + "build/classes" + Q + ")");
            code.AppendLine("string jar = Java.Jar(" + Q + "build/classes" + Q + ", " + Q +
                            "build/" + name + ".jar" + Q + ", " + Q + "Main" + Q + ")");
            code.AppendLine();
            code.AppendLine("print(" + Q + Q + ")");
            code.AppendLine("print(" + Q + "Запускаю:" + Q + ")");
            code.AppendLine("Java.RunJar(jar)");
            code.AppendLine();
            code.AppendLine("print(" + Q + Q + ")");
            code.AppendLine("print(" + Q + "Размер: " + Q + " + Files.Size(jar) + " + Q + " байт" + Q + ")");

            File.WriteAllText(Path.Combine(dir, "main.dupolang"), code.ToString(), new UTF8Encoding(false));

            var java = new StringBuilder();
            java.AppendLine("public class Main {");
            java.AppendLine("    public static void main(String[] args) {");
            java.AppendLine("        System.out.println(" + Q + "Привет из " + name + "!" + Q + ");");
            java.AppendLine();
            java.AppendLine("        for (int i = 1; i <= 5; i++) {");
            java.AppendLine("            System.out.println(" + Q + "  строка " + Q + " + i);");
            java.AppendLine("        }");
            java.AppendLine("    }");
            java.AppendLine("}");

            File.WriteAllText(Path.Combine(dir, "src", "Main.java"), java.ToString(), new UTF8Encoding(false));

            OpenProject(dir);
            OpenFile(Path.Combine(dir, "main.dupolang"));
            Log("Java-проект создан: " + dir, Theme.Ok);
            if (!JavaTools.Instance.IsInstalled)
                Log("JDK ещё не установлен — F5 поставит его сам, либо Инструменты → Установить JDK.", Theme.Dim);
        }

        void OpenProjectDialog()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Выберите папку проекта";
                dlg.SelectedPath = projectDir ?? Path.Combine(DupoRuntime.RuntimeDir, "Projects");
                if (dlg.ShowDialog(this) == DialogResult.OK) OpenProject(dlg.SelectedPath);
            }
        }

        void OpenProject(string dir)
        {
            projectDir = dir;
            DupoRuntime.ProjectDir = dir;
            Text = "Dupo Studio — " + Path.GetFileName(dir);
            lblProject.Text = "  " + Path.GetFileName(dir) + "  ";
            RefreshTree();
            Status(dir);

            string main = Path.Combine(dir, "main.dupolang");
            if (File.Exists(main)) OpenFile(main);
        }

        void RefreshTree()
        {
            treeProject.Nodes.Clear();
            if (projectDir == null || !Directory.Exists(projectDir)) return;

            var root = new TreeNode(Path.GetFileName(projectDir));
            root.Tag = projectDir;
            FillNode(root, projectDir);
            treeProject.Nodes.Add(root);
            root.Expand();
        }

        static void FillNode(TreeNode node, string dir)
        {
            foreach (string sub in Directory.GetDirectories(dir).OrderBy(x => x))
            {
                string name = Path.GetFileName(sub);
                if (SkipDirs.Contains(name.ToLowerInvariant())) continue;
                var child = new TreeNode(name);
                child.Tag = sub;
                FillNode(child, sub);
                node.Nodes.Add(child);
                child.Expand();
            }
            foreach (string file in Directory.GetFiles(dir).OrderBy(x => x))
            {
                var child = new TreeNode(Path.GetFileName(file));
                child.Tag = file;
                child.ForeColor = ColorFor(Path.GetExtension(file));
                node.Nodes.Add(child);
            }
        }

        static Color ColorFor(string ext)
        {
            switch ((ext ?? "").ToLowerInvariant())
            {
                case ".dupolang": return Theme.Accent2;
                case ".dapp": return Theme.Warn;
                case ".java": return Color.FromArgb(235, 160, 90);
                case ".jar": return Color.FromArgb(200, 170, 110);
                case ".class": return Theme.Dim;
                case ".html": case ".htm": return Color.FromArgb(226, 142, 90);
                case ".php": return Color.FromArgb(165, 150, 230);
                case ".css": return Color.FromArgb(120, 170, 220);
                case ".js": return Color.FromArgb(222, 205, 120);
                case ".exe": return Theme.Ok;
                case ".md": return Theme.Dim;
                default: return Theme.Text;
            }
        }

        // ---------------- файлы ----------------

        CodeEditor ActiveEditor { get { return EditorOf(tabsEditor.SelectedTab); } }

        static CodeEditor EditorOf(TabPage page)
        {
            if (page == null || page.Controls.Count == 0) return null;
            var host = page.Controls[0] as EditorHost;
            return host == null ? null : host.Editor;
        }

        void OpenFile(string path)
        {
            foreach (TabPage page in tabsEditor.TabPages)
            {
                var ed = EditorOf(page);
                if (ed != null && string.Equals(ed.FilePath, path, StringComparison.OrdinalIgnoreCase))
                {
                    tabsEditor.SelectedTab = page;
                    return;
                }
            }

            var host = new EditorHost();
            host.Dock = DockStyle.Fill;
            CodeEditor editor = host.Editor;
            editor.FilePath = path;
            editor.JavaMode = path.EndsWith(".java", StringComparison.OrdinalIgnoreCase);

            try { editor.Text = File.ReadAllText(path, Encoding.UTF8); }
            catch (Exception ex) { Log("Не открылось: " + ex.Message, Theme.Err); return; }

            editor.Dirty = false;
            editor.SelectionChanged += delegate { UpdateCaret(); };

            var tab = new TabPage(Path.GetFileName(path));
            tab.BackColor = Theme.Back;
            tab.Controls.Add(host);
            tabsEditor.TabPages.Add(tab);
            tabsEditor.SelectedTab = tab;

            editor.HighlightNow();
            Status(path);
        }

        void SaveActive()
        {
            var ed = ActiveEditor;
            if (ed == null || ed.FilePath == null) return;
            File.WriteAllText(ed.FilePath, ed.Text, new UTF8Encoding(false));
            ed.Dirty = false;
            Status("Сохранено: " + ed.FilePath);
        }

        void SaveAll()
        {
            foreach (TabPage page in tabsEditor.TabPages)
            {
                var ed = EditorOf(page);
                if (ed != null && ed.Dirty && ed.FilePath != null)
                {
                    File.WriteAllText(ed.FilePath, ed.Text, new UTF8Encoding(false));
                    ed.Dirty = false;
                }
            }
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

            txtOutput.Clear();
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

            string host = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DupoHost.exe");
            if (!File.Exists(host))
            {
                Log("Не нашёл DupoHost.exe рядом со студией — соберите проект DupoHost.", Theme.Err);
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
                manifest.Name = name;
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

            SaveAll();
            string name = Path.GetFileName(projectDir);
            string outDir = Path.Combine(projectDir, "build");
            string appDir = AppDomain.CurrentDomain.BaseDirectory;

            try
            {
                if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
                Directory.CreateDirectory(outDir);

                CopyTree(projectDir, outDir);

                string runner = Path.Combine(appDir, "DupoRun.exe");
                if (!File.Exists(runner))
                {
                    Log("Не нашёл DupoRun.exe рядом со студией — соберите проект DupoRun.", Theme.Err);
                    return;
                }

                File.Copy(runner, Path.Combine(outDir, name + ".exe"), true);
                File.Copy(Path.Combine(appDir, "DupoLang.dll"), Path.Combine(outDir, "DupoLang.dll"), true);

                string cfg = Path.Combine(appDir, "DupoRun.exe.config");
                if (File.Exists(cfg)) File.Copy(cfg, Path.Combine(outDir, name + ".exe.config"), true);

                Log("Собрано: " + Path.Combine(outDir, name + ".exe"), Theme.Ok);
                Log("Папка build готова к раздаче.", Theme.Dim);
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
                manifest.Name = name;
                manifest.Version = PromptDialog.Show(this, "Экспорт", "Версия приложения:", "1.0") ?? "1.0";
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
            Log("  DupoLang " + Dupo.Version + "   ·   Dupo.Web · Dupo.Java · Dupo.System.Files · Dupo.App", Theme.Dim);
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
                "Web.Server s = new Web.Server(8080, \"www\", \"index.html\")\r\n" +
                "Web.Server.Php.Install()\r\n" +
                "s.Start()",
                "О программе", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void Log(string text, Color color)
        {
            if (text == null) text = "";
            if (InvokeRequired) { BeginInvoke(new Action<string, Color>(Log), text, color); return; }

            txtOutput.SelectionStart = txtOutput.TextLength;
            txtOutput.SelectionLength = 0;
            txtOutput.SelectionColor = color;
            txtOutput.AppendText(text + Environment.NewLine);
            txtOutput.SelectionColor = txtOutput.ForeColor;
            txtOutput.ScrollToCaret();
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

        void DrawTab(object sender, DrawItemEventArgs e)
        {
            var page = tabsEditor.TabPages[e.Index];
            bool active = tabsEditor.SelectedIndex == e.Index;

            using (var back = new SolidBrush(active ? Theme.Back : Theme.Chrome))
                e.Graphics.FillRectangle(back, e.Bounds);

            if (active)
                using (var line = new SolidBrush(Theme.Accent))
                    e.Graphics.FillRectangle(line, e.Bounds.X, e.Bounds.Y, e.Bounds.Width, 2);

            var ed = EditorOf(page);
            string title = page.Text + (ed != null && ed.Dirty ? " •" : "");

            TextRenderer.DrawText(e.Graphics, title, tabFont, e.Bounds,
                active ? Theme.Text : Theme.Dim,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
        }

        void OnClosing(object sender, FormClosingEventArgs e)
        {
            bool dirty = tabsEditor.TabPages.Cast<TabPage>()
                .Select(EditorOf)
                .Any(ed => ed != null && ed.Dirty);

            if (dirty)
            {
                var r = MessageBox.Show(this, "Есть несохранённые изменения. Сохранить?",
                    "Dupo Studio", MessageBoxButtons.YesNoCancel);
                if (r == DialogResult.Cancel) { e.Cancel = true; return; }
                if (r == DialogResult.Yes) SaveAll();
            }

            if (interp != null) { try { interp.Shutdown(); } catch { } }
            if (runThread != null && runThread.IsAlive) { try { runThread.Abort(); } catch { } }
        }
    }
}
