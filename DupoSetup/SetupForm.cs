using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace DupoSetup
{
    /// <summary>Окно установщика: приветствие, ход установки, завершение.</summary>
    public class SetupForm : Form
    {
        static readonly Color Back = Color.FromArgb(22, 26, 33);
        static readonly Color Card = Color.FromArgb(30, 36, 46);
        static readonly Color Line = Color.FromArgb(52, 60, 74);
        static readonly Color Fore = Color.FromArgb(230, 235, 243);   // не Text: это имя занято Form.Text
        static readonly Color Dim = Color.FromArgb(139, 151, 171);
        static readonly Color Accent = Color.FromArgb(43, 127, 255);
        static readonly Color Ok = Color.FromArgb(106, 190, 120);

        readonly SetupInfo info;
        readonly byte[] payload;

        readonly Label title = new Label();
        readonly Label subtitle = new Label();
        readonly TextBox pathBox = new TextBox();
        readonly Button browse = new Button();
        readonly CheckBox desktop = new CheckBox();
        readonly CheckBox assoc = new CheckBox();
        readonly CheckBox tools = new CheckBox();
        readonly ProgressBar bar = new ProgressBar();
        readonly RichTextBox log = new RichTextBox();
        readonly Button action = new Button();
        readonly Button cancel = new Button();
        readonly CheckBox runAfter = new CheckBox();

        bool uninstallMode;
        bool finished;
        string installedDir;

        public SetupForm(SetupInfo setup, byte[] data, bool uninstall)
        {
            info = setup;
            payload = data;
            uninstallMode = uninstall;

            Text = (uninstall ? "Удаление — " : "Установка — ") + info.Name;
            ClientSize = new Size(560, 430);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Back;
            ForeColor = Fore;

            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Build();
        }

        void Build()
        {
            title.Text = info.Name;
            title.Font = new Font("Segoe UI", 20f, FontStyle.Bold);
            title.ForeColor = Fore;
            title.SetBounds(26, 22, 500, 34);

            subtitle.Text = "Версия " + info.Version +
                            (string.IsNullOrEmpty(info.Publisher) ? "" : "   ·   " + info.Publisher);
            subtitle.Font = new Font("Segoe UI", 9.5f);
            subtitle.ForeColor = Dim;
            subtitle.SetBounds(28, 60, 500, 20);

            var pathLabel = new Label
            {
                Text = uninstallMode ? "Программа установлена в:" : "Папка установки:",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Dim,
                Bounds = new Rectangle(28, 100, 300, 18)
            };

            pathBox.Text = info.DefaultDir;
            pathBox.SetBounds(28, 122, 408, 26);
            pathBox.BackColor = Card;
            pathBox.ForeColor = Fore;
            pathBox.BorderStyle = BorderStyle.FixedSingle;
            pathBox.ReadOnly = uninstallMode;

            browse.Text = "Обзор";
            browse.SetBounds(444, 121, 88, 28);
            browse.FlatStyle = FlatStyle.Flat;
            browse.BackColor = Card;
            browse.ForeColor = Fore;
            browse.FlatAppearance.BorderColor = Line;
            browse.Visible = !uninstallMode;
            browse.Click += delegate
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.Description = "Куда установить " + info.Name;
                    dlg.SelectedPath = pathBox.Text;
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                        pathBox.Text = Path.Combine(dlg.SelectedPath, info.Folder);
                }
            };

            desktop.Text = "Ярлык на рабочем столе";
            desktop.Checked = info.Desktop;
            desktop.SetBounds(30, 162, 260, 22);
            desktop.ForeColor = Dim;
            desktop.FlatStyle = FlatStyle.Flat;
            desktop.Visible = !uninstallMode;

            assoc.Text = "Открывать файлы " + string.Join(", ", info.Associations.ToArray()) + " этой программой";
            assoc.Checked = true;
            assoc.SetBounds(30, 188, 480, 22);
            assoc.ForeColor = Dim;
            assoc.FlatStyle = FlatStyle.Flat;
            assoc.Visible = !uninstallMode && info.Associations.Count > 0;

            tools.Text = "Скачать сборщик командной строки (BuildTools) с GitHub";
            tools.Checked = true;
            tools.SetBounds(30, 212, 480, 22);
            tools.ForeColor = Dim;
            tools.FlatStyle = FlatStyle.Flat;
            tools.Visible = !uninstallMode;

            bar.SetBounds(28, 240, 504, 8);
            bar.Style = ProgressBarStyle.Continuous;
            bar.Visible = false;

            log.SetBounds(28, 256, 504, 108);
            log.BackColor = Card;
            log.ForeColor = Dim;
            log.BorderStyle = BorderStyle.None;
            log.Font = new Font("Consolas", 8.5f);
            log.ReadOnly = true;
            log.Visible = false;

            runAfter.Text = "Запустить " + info.Name;
            runAfter.Checked = true;
            runAfter.SetBounds(30, 380, 260, 22);
            runAfter.ForeColor = Dim;
            runAfter.FlatStyle = FlatStyle.Flat;
            runAfter.Visible = false;

            action.Text = uninstallMode ? "Удалить" : "Установить";
            action.SetBounds(card(0), 376, 140, 34);
            action.FlatStyle = FlatStyle.Flat;
            action.BackColor = Accent;
            action.ForeColor = Color.White;
            action.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            action.FlatAppearance.BorderSize = 0;
            action.Click += delegate { Go(); };

            cancel.Text = "Отмена";
            cancel.SetBounds(card(1), 376, 96, 34);
            cancel.FlatStyle = FlatStyle.Flat;
            cancel.BackColor = Card;
            cancel.ForeColor = Fore;
            cancel.FlatAppearance.BorderColor = Line;
            cancel.Click += delegate { Close(); };

            Controls.AddRange(new Control[] {
                title, subtitle, pathLabel, pathBox, browse,
                desktop, assoc, tools, bar, log, runAfter, action, cancel });
        }

        static int card(int which) { return which == 0 ? 296 : 442; }

        void Write(string text, Color color)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string, Color>(Write), text, color); return; }

            log.SelectionStart = log.TextLength;
            log.SelectionColor = color;
            log.AppendText(text + Environment.NewLine);
            log.ScrollToCaret();
        }

        void Go()
        {
            if (finished)
            {
                if (runAfter.Visible && runAfter.Checked && installedDir != null)
                {
                    string exe = Path.Combine(installedDir, info.MainExe);
                    try { if (File.Exists(exe)) Process.Start(exe); } catch { }
                }
                Close();
                return;
            }

            string dir = pathBox.Text.Trim();
            if (dir.Length == 0) return;

            action.Enabled = false;
            browse.Enabled = false;
            pathBox.Enabled = false;
            desktop.Enabled = assoc.Enabled = tools.Enabled = false;

            bar.Visible = true;
            log.Visible = true;

            bool wantDesktop = desktop.Checked;
            bool wantAssoc = assoc.Checked;
            bool wantTools = tools.Checked;

            var worker = new Thread(delegate ()
            {
                var installer = new Installer(info);
                installer.Log = delegate (string s) { Write(s, Dim); };
                installer.Progress = delegate (int v)
                {
                    BeginInvoke(new Action(delegate { bar.Value = Math.Min(100, Math.Max(0, v)); }));
                };

                try
                {
                    if (uninstallMode) installer.Uninstall(dir);
                    else installer.Install(dir, payload, wantDesktop, wantAssoc, wantTools);

                    installedDir = dir;
                    BeginInvoke(new Action(Done));
                }
                catch (Exception ex)
                {
                    Write("Не получилось: " + ex.Message, Color.FromArgb(230, 110, 110));
                    BeginInvoke(new Action(delegate
                    {
                        action.Enabled = true;
                        action.Text = "Повторить";
                    }));
                }
            });
            worker.IsBackground = true;
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
        }

        void Done()
        {
            finished = true;
            bar.Value = 100;

            subtitle.Text = uninstallMode ? "Программа удалена" : "Установка завершена";
            subtitle.ForeColor = Ok;

            runAfter.Visible = !uninstallMode && !string.IsNullOrEmpty(info.MainExe);
            action.Text = "Готово";
            action.Enabled = true;
            cancel.Visible = false;
        }
    }
}
