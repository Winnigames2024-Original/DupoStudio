using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using DupoLang;

namespace DupoSetup
{
    /// <summary>
    /// Мастер установки в привычном виде: приветствие, папка, задачи,
    /// сводка, ход установки и завершение.
    /// </summary>
    public class SetupForm : WizardForm
    {
        readonly SetupInfo info;
        readonly byte[] payload;

        readonly TextBox pathBox = new TextBox();
        readonly Label freeSpace = new Label();
        readonly CheckBox desktop = new CheckBox();
        readonly CheckBox assoc = new CheckBox();
        readonly CheckBox tools = new CheckBox();
        readonly TextBox summary = new TextBox();
        readonly ProgressBar bar = new ProgressBar();
        readonly Label step = new Label();
        readonly Label doneTitle = new Label();
        readonly Label doneText = new Label();
        readonly CheckBox runAfter = new CheckBox();

        bool installing;
        bool finished;
        string installedDir;

        public SetupForm(SetupInfo setup, byte[] data)
        {
            info = setup;
            payload = data;

            Text = "Установка — " + info.Name;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Welcome();
            Destination();
            Tasks();
            Ready();
            Installing();
            Finished();
        }

        // ------------------------------------------------------------------
        // 1. приветствие
        // ------------------------------------------------------------------

        void Welcome()
        {
            var page = new WizardPage
            {
                Key = "welcome",
                Wide = true,
                CanBack = false
            };

            page.Panel.Controls.Add(Banner(Letter(info.Name),
                Color.FromArgb(44, 88, 160), Color.FromArgb(22, 38, 72)));

            var title = new Label
            {
                Text = "Вас приветствует мастер установки " + info.Name,
                Font = new Font(Font.FontFamily, 12f, FontStyle.Bold),
                Bounds = new Rectangle(188, 28, 420, 56)
            };

            var text = new Label
            {
                Text = "Программа установит " + info.Name +
                       (info.Version.Length > 0 ? " версии " + info.Version : "") +
                       " на ваш компьютер.\r\n\r\n" +
                       "Права администратора не нужны: всё ставится в папку пользователя. " +
                       "Сборщик командной строки едет внутри установщика, поэтому интернет " +
                       "тоже не обязателен.\r\n\r\n" +
                       "Нажмите «Далее», чтобы продолжить, или «Отмена», чтобы выйти.",
                Bounds = new Rectangle(190, 96, 420, 220)
            };

            page.Panel.Controls.Add(title);
            page.Panel.Controls.Add(text);

            AddPage(page);
        }

        static string Letter(string name)
        {
            string s = (name ?? "").Trim();
            return s.Length == 0 ? "D" : s.Substring(0, 1).ToUpperInvariant();
        }

        // ------------------------------------------------------------------
        // 2. папка
        // ------------------------------------------------------------------

        void Destination()
        {
            var page = new WizardPage
            {
                Key = "dir",
                Title = "Выбор папки установки",
                Note = "В какую папку установить " + info.Name + "?"
            };

            var icon = new Label
            {
                Text = "📁",
                Font = new Font("Segoe UI Emoji", 20f),
                Bounds = new Rectangle(22, 20, 40, 40)
            };

            var text = new Label
            {
                Text = "Программа установит " + info.Name + " в следующую папку.\r\n\r\n" +
                       "Нажмите «Далее», чтобы продолжить. Чтобы выбрать другую папку, " +
                       "нажмите «Обзор».",
                Bounds = new Rectangle(66, 20, 540, 56)
            };

            pathBox.Text = info.DefaultDir;
            pathBox.SetBounds(66, 92, 440, 23);

            var browse = new Button { Text = "Обзор...", UseVisualStyleBackColor = true };
            browse.SetBounds(514, 91, 94, 26);
            browse.Click += delegate { Browse(); };

            freeSpace.Bounds = new Rectangle(66, 130, 540, 20);
            freeSpace.ForeColor = SystemColors.GrayText;

            pathBox.TextChanged += delegate { ShowSpace(); };

            page.OnEnter = delegate { ShowSpace(); };
            page.OnLeave = delegate { return CheckDir(); };

            page.Panel.Controls.AddRange(new Control[] { icon, text, pathBox, browse, freeSpace });
            AddPage(page);
        }

        void Browse()
        {
            string dir = Folders.Ask(this, "Куда установить " + info.Name, pathBox.Text);
            if (dir == null) return;

            // Если выбрали родительскую папку — дописываем имя программы,
            // как это делают обычные установщики.
            string leaf = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar));
            pathBox.Text = string.Equals(leaf, info.Folder, StringComparison.OrdinalIgnoreCase)
                ? dir
                : Path.Combine(dir, info.Folder);
        }

        void ShowSpace()
        {
            string dir = pathBox.Text.Trim();
            long need = payload == null ? 0 : payload.LongLength;

            string line = "Требуется не менее " + Kb(need * 2) + " свободного места.";

            try
            {
                string root = Path.GetPathRoot(Path.GetFullPath(dir));
                if (!string.IsNullOrEmpty(root))
                {
                    var drive = new DriveInfo(root);
                    if (drive.IsReady)
                        line += "   Доступно: " + Kb(drive.AvailableFreeSpace) + ".";
                }
            }
            catch { }

            freeSpace.Text = line;
        }

        static string Kb(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024) return (bytes / 1073741824.0).ToString("0.0") + " ГБ";
            if (bytes >= 1024 * 1024) return (bytes / 1048576.0).ToString("0.0") + " МБ";
            return Math.Max(1, bytes / 1024) + " КБ";
        }

        bool CheckDir()
        {
            string dir = pathBox.Text.Trim();

            if (dir.Length == 0 || !Path.IsPathRooted(dir))
            {
                MessageBox.Show(this, "Укажите полный путь к папке.", Text,
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (dir.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                MessageBox.Show(this, "В пути есть недопустимые символы.", Text,
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length > 0)
            {
                var r = MessageBox.Show(this,
                    "Папка" + Environment.NewLine + dir + Environment.NewLine + Environment.NewLine +
                    "уже существует и не пуста. Установить в неё?",
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (r != DialogResult.Yes) return false;
            }

            return true;
        }

        // ------------------------------------------------------------------
        // 3. задачи
        // ------------------------------------------------------------------

        void Tasks()
        {
            var page = new WizardPage
            {
                Key = "tasks",
                Title = "Выберите дополнительные задачи",
                Note = "Какие задачи выполнить при установке " + info.Name + "?"
            };

            var text = new Label
            {
                Text = "Отметьте нужное и нажмите «Далее».",
                Bounds = new Rectangle(22, 20, 580, 20)
            };

            desktop.Text = "Создать ярлык на рабочем столе";
            desktop.Checked = info.Desktop;
            desktop.SetBounds(26, 56, 560, 22);

            assoc.Text = info.Associations.Count > 0
                ? "Открывать файлы " + string.Join(", ", info.Associations.ToArray()) + " этой программой"
                : "Связать файлы с программой";
            assoc.Checked = true;
            assoc.SetBounds(26, 84, 560, 22);
            assoc.Visible = info.Associations.Count > 0;

            tools.Text = "Обновить сборщик BuildTools с GitHub";
            tools.Checked = true;
            tools.SetBounds(26, 112, 560, 22);

            var note = new Label
            {
                Text = "Без сети сборщик возьмётся из самого установщика — он внутри.",
                Bounds = new Rectangle(44, 134, 560, 20),
                ForeColor = SystemColors.GrayText
            };

            page.Panel.Controls.AddRange(new Control[] { text, desktop, assoc, tools, note });
            AddPage(page);
        }

        // ------------------------------------------------------------------
        // 4. сводка
        // ------------------------------------------------------------------

        void Ready()
        {
            var page = new WizardPage
            {
                Key = "ready",
                Title = "Всё готово к установке",
                Note = "Программа готова установить " + info.Name + " на ваш компьютер.",
                NextText = "Установить"
            };

            var text = new Label
            {
                Text = "Нажмите «Установить», чтобы начать, или «Назад», чтобы изменить настройки.",
                Bounds = new Rectangle(22, 20, 580, 20)
            };

            summary.Multiline = true;
            summary.ReadOnly = true;
            summary.ScrollBars = ScrollBars.Vertical;
            summary.BackColor = SystemColors.Window;
            summary.SetBounds(22, 48, 586, 190);

            page.OnEnter = delegate { summary.Text = Summary(); };
            page.Panel.Controls.AddRange(new Control[] { text, summary });
            AddPage(page);
        }

        string Summary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Папка установки:");
            sb.AppendLine("    " + pathBox.Text.Trim());
            sb.AppendLine();
            sb.AppendLine("Дополнительные задачи:");
            if (desktop.Checked) sb.AppendLine("    Создать ярлык на рабочем столе");
            if (assoc.Visible && assoc.Checked)
                sb.AppendLine("    Открывать файлы " + string.Join(", ", info.Associations.ToArray()));
            if (tools.Checked) sb.AppendLine("    Обновить сборщик BuildTools с GitHub");
            if (!desktop.Checked && !(assoc.Visible && assoc.Checked) && !tools.Checked)
                sb.AppendLine("    нет");

            sb.AppendLine();
            sb.AppendLine("Будет создан uninstall.exe — им программа удаляется.");
            sb.AppendLine("Рядом ляжет uninstall.dll с данными об установке,");
            sb.AppendLine("и update.exe для обновлений.");
            return sb.ToString().Replace("\n", "\r\n").Replace("\r\r", "\r");
        }

        // ------------------------------------------------------------------
        // 5. установка
        // ------------------------------------------------------------------

        void Installing()
        {
            var page = new WizardPage
            {
                Key = "run",
                Title = "Установка",
                Note = "Подождите, идёт установка " + info.Name + "...",
                CanBack = false
            };

            step.Bounds = new Rectangle(22, 60, 586, 20);
            step.ForeColor = SystemColors.GrayText;

            bar.SetBounds(22, 86, 586, 18);
            bar.Style = ProgressBarStyle.Continuous;

            page.OnEnter = delegate { Go(); };
            page.OnLeave = delegate { return finished; };

            page.Panel.Controls.AddRange(new Control[] { step, bar });
            AddPage(page);
        }

        void Go()
        {
            installing = true;
            Back.Enabled = false;
            AllowNext(false);

            string dir = pathBox.Text.Trim();
            bool wantDesktop = desktop.Checked;
            bool wantAssoc = assoc.Visible && assoc.Checked;
            bool wantTools = tools.Checked;

            var worker = new Thread(delegate ()
            {
                var installer = new Installer(info);
                installer.Log = delegate (string s) { Step(s); };
                installer.Progress = delegate (int v)
                {
                    try
                    {
                        BeginInvoke(new Action(delegate
                        {
                            bar.Value = Math.Min(100, Math.Max(0, v));
                        }));
                    }
                    catch { }
                };

                try
                {
                    installer.Install(dir, payload, wantDesktop, wantAssoc, wantTools);
                    installedDir = dir;

                    try { BeginInvoke(new Action(Done)); }
                    catch { }
                }
                catch (Exception ex)
                {
                    try
                    {
                        BeginInvoke(new Action(delegate { Failed(ex.Message); }));
                    }
                    catch { }
                }
            });

            worker.IsBackground = true;
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
        }

        void Step(string text)
        {
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<string>(Step), text); }
                catch { }
                return;
            }
            step.Text = text;
        }

        void Failed(string message)
        {
            installing = false;
            MessageBox.Show(this, "Установка не удалась:" + Environment.NewLine + message,
                            Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }

        void Done()
        {
            installing = false;
            finished = true;
            bar.Value = 100;
            Show("done");
        }

        // ------------------------------------------------------------------
        // 6. завершение
        // ------------------------------------------------------------------

        void Finished()
        {
            var page = new WizardPage
            {
                Key = "done",
                Wide = true,
                CanBack = false,
                ShowCancel = false,
                NextText = "Завершить"
            };

            page.Panel.Controls.Add(Banner(Letter(info.Name),
                Color.FromArgb(44, 88, 160), Color.FromArgb(22, 38, 72)));

            doneTitle.Text = "Завершение мастера установки " + info.Name;
            doneTitle.Font = new Font(Font.FontFamily, 12f, FontStyle.Bold);
            doneTitle.Bounds = new Rectangle(188, 28, 420, 56);

            doneText.Text = info.Name + " установлен на ваш компьютер.\r\n\r\n" +
                            "Программу можно запустить из меню «Пуск». " +
                            "Для удаления есть uninstall.exe в папке установки " +
                            "и запись в списке программ Windows.\r\n\r\n" +
                            "Нажмите «Завершить», чтобы выйти.";
            doneText.Bounds = new Rectangle(190, 96, 420, 180);

            runAfter.Text = "Запустить " + info.Name;
            runAfter.Checked = true;
            runAfter.Bounds = new Rectangle(190, 280, 420, 24);

            page.OnEnter = delegate
            {
                runAfter.Visible = !string.IsNullOrEmpty(info.MainExe);
                NextLabel("Завершить");
                AllowNext(true);
            };

            page.OnLeave = delegate
            {
                Finish();
                return false;
            };

            page.Panel.Controls.AddRange(new Control[] { doneTitle, doneText, runAfter });
            AddPage(page);
        }

        void Finish()
        {
            if (runAfter.Visible && runAfter.Checked && installedDir != null)
            {
                string exe = Path.Combine(installedDir, info.MainExe);
                try { if (File.Exists(exe)) Process.Start(exe); }
                catch { }
            }
            Close();
        }

        protected override void AskCancel()
        {
            if (finished) { Close(); return; }

            if (installing)
            {
                MessageBox.Show(this, "Установка уже идёт — дождитесь окончания.", Text,
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            base.AskCancel();
        }
    }

    /// <summary>Выбор папки проводником — то же окно, что и в студии.</summary>
    static class Folders
    {
        public static string Ask(IWin32Window owner, string title, string startDir)
        {
            IntPtr handle = owner == null ? IntPtr.Zero : owner.Handle;

            string picked = FolderPicker.Pick(handle, title, startDir);
            if (picked != null) return picked;
            if (FolderPicker.Available) return null;

            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = title ?? "Выберите папку";
                if (!string.IsNullOrEmpty(startDir)) dlg.SelectedPath = startDir;
                return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.SelectedPath : null;
            }
        }
    }
}
