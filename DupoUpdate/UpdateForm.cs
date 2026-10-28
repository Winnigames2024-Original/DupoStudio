using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using DupoLang;

namespace DupoUpdate
{
    /// <summary>
    /// Окно обновления: что стоит, что доступно, и ход работы.
    /// Порядок виден по строчкам: приложение, обновлятор, инструменты.
    /// </summary>
    public class UpdateForm : Form
    {
        readonly UninstallData data;
        readonly string dir;

        readonly Label title = new Label();
        readonly Label state = new Label();
        readonly TextBox notes = new TextBox();
        readonly ProgressBar bar = new ProgressBar();
        readonly Label step = new Label();
        readonly Button go = new Button();
        readonly Button close = new Button();
        readonly CheckBox auto = new CheckBox();

        Found found;
        Updater updater;
        bool working;
        bool done;

        public UpdateForm(UninstallData info, string folder)
        {
            data = info;
            dir = folder;

            Text = "Обновление — " + (data != null ? data.Name : "Dupo Studio");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(520, 388);
            BackColor = SystemColors.Control;
            Font = SystemFonts.MessageBoxFont;

            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Build();
            Load += delegate { Check(); };
        }

        void Build()
        {
            title.Text = data != null ? data.Name : "Dupo Studio";
            title.Font = new Font(Font.FontFamily, 12f, FontStyle.Bold);
            title.Bounds = new Rectangle(20, 18, 480, 24);

            state.Text = "Проверяю обновления...";
            state.ForeColor = SystemColors.GrayText;
            state.Bounds = new Rectangle(20, 46, 480, 36);

            var caption = new Label
            {
                Text = "Что нового:",
                Bounds = new Rectangle(20, 90, 200, 18)
            };

            notes.Multiline = true;
            notes.ReadOnly = true;
            notes.ScrollBars = ScrollBars.Vertical;
            notes.BackColor = SystemColors.Window;
            notes.SetBounds(20, 110, 480, 160);

            auto.Text = "Проверять и ставить обновления самостоятельно";
            auto.SetBounds(19, 276, 480, 22);
            auto.Checked = AutoUpdate.Wanted(data) && AutoUpdate.Registered;
            auto.CheckedChanged += delegate { Autostart(); };

            step.Bounds = new Rectangle(20, 304, 480, 18);
            step.ForeColor = SystemColors.GrayText;

            bar.SetBounds(20, 324, 480, 16);
            bar.Style = ProgressBarStyle.Continuous;

            go.Text = "Обновить";
            go.SetBounds(314, 350, 93, 26);
            go.UseVisualStyleBackColor = true;
            go.Enabled = false;
            go.Click += delegate { Go(); };

            close.Text = "Закрыть";
            close.SetBounds(411, 350, 93, 26);
            close.UseVisualStyleBackColor = true;
            close.Click += delegate { Close(); };

            AcceptButton = go;
            CancelButton = close;

            Controls.AddRange(new Control[] { title, state, caption, notes,
                                              auto, step, bar, go, close });
        }

        /// <summary>
        /// Галочка заведует записью в автозапуске: с ней update.exe поднимается
        /// вместе с системой и ставит новые версии сам, без окон.
        /// </summary>
        void Autostart()
        {
            AutoUpdate.Want(data, dir, auto.Checked);

            if (!auto.Checked) return;

            if (!AutoUpdate.Registered)
            {
                MessageBox.Show(this,
                    "Запись в автозапуск не получилась — возможно, её не разрешает система.",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Сразу и запускаем, чтобы не ждать перезагрузки.
            Watcher();
        }

        /// <summary>Поднять тихий update.exe -autoupdate, если он ещё не работает.</summary>
        void Watcher()
        {
            try
            {
                string exe = Path.Combine(dir, "update.exe");
                if (!File.Exists(exe)) return;

                var psi = new System.Diagnostics.ProcessStartInfo(exe, "-autoupdate");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                System.Diagnostics.Process.Start(psi);
            }
            catch { }
        }

        // ------------------------------------------------------------------

        void Check()
        {
            var worker = new Thread(delegate ()
            {
                Found result = null;
                string error = null;

                try { result = Updater.Latest(); }
                catch (Exception ex) { error = ex.Message; }

                try
                {
                    BeginInvoke(new Action(delegate { Checked2(result, error); }));
                }
                catch { }
            });

            worker.IsBackground = true;
            worker.Start();
        }

        void Checked2(Found result, string error)
        {
            if (error != null)
            {
                state.Text = "Не получилось проверить: " + error;
                return;
            }

            found = result;
            string installed = data != null && data.AppVersion.Length > 0 ? data.AppVersion : "0.0.0";

            notes.Text = (found.Notes ?? "").Replace("\n", Environment.NewLine);

            if (Updater.Compare(found.Version, installed) <= 0)
            {
                state.Text = "Установлена версия " + installed + " — это уже последняя.";
                go.Text = "Переустановить";
                go.Enabled = true;
                return;
            }

            state.Text = "Установлено " + installed + ", доступно " + found.Version + "." +
                         Environment.NewLine +
                         "Сначала обновится приложение, затем обновлятор, потом инструменты сборки.";
            go.Enabled = true;
        }

        void Go()
        {
            if (working) return;

            if (done) { Launch(); Close(); return; }
            if (found == null) return;

            working = true;
            go.Enabled = false;
            close.Enabled = false;

            updater = new Updater(data, dir);
            updater.Log = delegate (string s) { Say(s); };
            updater.Progress = delegate (int v) { Move(v); };

            var worker = new Thread(delegate ()
            {
                string error = null;
                bool changed = false;

                try
                {
                    updater.StopApp();
                    changed = updater.Run(found);
                }
                catch (Exception ex) { error = ex.Message; }

                bool ok = changed;
                string fail = error;

                try
                {
                    BeginInvoke(new Action(delegate { Finished(ok, fail); }));
                }
                catch { }
            });

            worker.IsBackground = true;
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
        }

        void Say(string text)
        {
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<string>(Say), text); }
                catch { }
                return;
            }
            step.Text = text;
        }

        void Move(int value)
        {
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<int>(Move), value); }
                catch { }
                return;
            }
            bar.Value = Math.Max(0, Math.Min(100, value));
        }

        void Finished(bool changed, string error)
        {
            working = false;
            done = true;
            close.Enabled = true;

            if (error != null)
            {
                state.Text = "Обновление не удалось: " + error;
                go.Text = "Повторить";
                go.Enabled = true;
                done = false;
                return;
            }

            bar.Value = 100;
            step.Text = "";
            state.Text = changed
                ? "Готово. Обновлено до версии " + found.Version + "."
                : "Менять было нечего.";

            go.Text = "Запустить";
            go.Enabled = data != null && !string.IsNullOrEmpty(data.MainExe);
        }

        void Launch()
        {
            if (data == null || string.IsNullOrEmpty(data.MainExe)) return;

            string exe = Path.Combine(dir, data.MainExe);
            try { if (File.Exists(exe)) System.Diagnostics.Process.Start(exe); }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (working)
            {
                e.Cancel = true;
                return;
            }

            base.OnFormClosing(e);

            // Себя подменяем последним — работающий файл перезаписать нельзя.
            if (updater != null) updater.ApplySelf();
        }
    }
}
