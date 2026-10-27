using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace DupoStudio
{
    /// <summary>
    /// Панель помощника: свой чат на каждый проект. Без интернета отвечает
    /// по базе знаний студии, а если задан адрес и ключ сервиса — через него.
    /// </summary>
    public class AssistantPanel : Panel
    {
        readonly RichTextBox view = new RichTextBox();
        readonly TextBox input = new TextBox();
        readonly Button send = new Button();
        readonly ToolStrip bar = new ToolStrip();
        readonly ToolStripButton btnNew = new ToolStripButton("Новый чат");
        readonly ToolStripButton btnInsert = new ToolStripButton("Вставить код");
        readonly ToolStripButton btnOnline = new ToolStripButton("Настроить сервис");
        readonly ToolStripLabel lblMode = new ToolStripLabel("база знаний");

        readonly List<ChatLine> lines = new List<ChatLine>();

        readonly Panel actionBar = new Panel();
        readonly Label actionText = new Label();
        readonly Button apply = new Button();
        readonly Button skip = new Button();
        List<FileAction> pending = new List<FileAction>();

        string projectDir;
        bool busy;

        /// <summary>Папка открытого проекта — помощник правит файлы только в ней.</summary>
        public Func<string> ProjectDir = delegate { return null; };

        /// <summary>Файлы изменились — студии пора обновить дерево и вкладки.</summary>
        public event EventHandler<string> FilesChanged;

        /// <summary>Куда вставлять код из ответа.</summary>
        public Action<string> InsertCode;

        /// <summary>Чем помощник описывает открытый проект.</summary>
        public Func<string> Context = delegate { return ""; };

        public AssistantPanel()
        {
            BackColor = Theme.Panel;
            BuildBar();
            BuildView();
            BuildActions();
            BuildInput();
        }

        // ------------------------------------------------------------------
        // внешний вид
        // ------------------------------------------------------------------

        void BuildBar()
        {
            bar.Dock = DockStyle.Top;
            bar.BackColor = Theme.Chrome;
            bar.ForeColor = Theme.Text;
            bar.GripStyle = ToolStripGripStyle.Hidden;
            bar.Renderer = new ToolStripProfessionalRenderer(new DarkColors());
            bar.Font = new Font("Segoe UI", 8.5f);

            foreach (var b in new[] { btnNew, btnInsert, btnOnline })
            {
                b.DisplayStyle = ToolStripItemDisplayStyle.Text;
                b.ForeColor = Theme.Text;
            }

            lblMode.ForeColor = Theme.Dim;
            lblMode.Alignment = ToolStripItemAlignment.Right;

            bar.Items.AddRange(new ToolStripItem[] {
                btnNew, new ToolStripSeparator(), btnInsert, btnOnline, lblMode });

            btnNew.Click += delegate { Clear(); };
            btnInsert.Click += delegate { InsertLast(); };
            btnOnline.Click += delegate { Configure(); };

            Controls.Add(bar);
        }

        void BuildView()
        {
            view.Dock = DockStyle.Fill;
            view.ReadOnly = true;
            view.BorderStyle = BorderStyle.None;
            view.BackColor = Theme.Back;
            view.ForeColor = Theme.Text;
            view.Font = new Font("Segoe UI", 9.5f);
            view.DetectUrls = true;
            view.HideSelection = false;
            Controls.Add(view);
        }

        void BuildActions()
        {
            actionBar.Dock = DockStyle.Bottom;
            actionBar.Height = 64;
            actionBar.BackColor = Theme.Chrome;
            actionBar.Visible = false;
            actionBar.Padding = new Padding(8, 6, 8, 6);

            actionText.Dock = DockStyle.Top;
            actionText.Height = 30;
            actionText.ForeColor = Theme.Text;
            actionBar.Controls.Add(actionText);

            apply.Text = "Применить";
            apply.Dock = DockStyle.Left;
            apply.Width = 110;
            apply.FlatStyle = FlatStyle.Flat;
            apply.BackColor = Theme.Accent;
            apply.ForeColor = Color.White;
            apply.FlatAppearance.BorderColor = Theme.Accent;
            apply.Click += delegate { ApplyPending(); };

            skip.Text = "Не надо";
            skip.Dock = DockStyle.Left;
            skip.Width = 100;
            skip.FlatStyle = FlatStyle.Flat;
            skip.BackColor = Theme.Chrome;
            skip.ForeColor = Theme.Text;
            skip.FlatAppearance.BorderColor = Theme.Border;
            skip.Click += delegate { Forget(); };

            var row = new Panel();
            row.Dock = DockStyle.Fill;
            row.BackColor = Theme.Chrome;
            row.Controls.Add(skip);
            row.Controls.Add(apply);
            actionBar.Controls.Add(row);
            row.BringToFront();

            Controls.Add(actionBar);
        }

        /// <summary>Показать предложенные правки.</summary>
        void Offer(List<FileAction> actions)
        {
            pending = actions ?? new List<FileAction>();

            if (pending.Count == 0) { actionBar.Visible = false; return; }

            var names = pending.Select(a => a.Title).Take(4).ToList();
            if (pending.Count > names.Count) names.Add("и ещё " + (pending.Count - names.Count));

            actionText.Text = "Предлагаю " + pending.Count + " правк(и): " + string.Join(", ", names.ToArray());
            actionBar.Visible = true;
            actionBar.BringToFront();
        }

        void Forget()
        {
            pending = new List<FileAction>();
            actionBar.Visible = false;
            Add("помощник", "Хорошо, ничего не трогаю.");
            Store();
        }

        void ApplyPending()
        {
            string dir = Safe(ProjectDir);
            if (string.IsNullOrEmpty(dir))
            {
                Add("помощник", "Проект не открыт — править нечего.");
                return;
            }

            var done = new List<string>();
            var failed = new List<string>();
            var touched = new List<string>();

            foreach (var action in pending)
            {
                try
                {
                    done.Add(AssistantActions.Apply(action, dir));
                    touched.Add(action.Full(dir));
                }
                catch (Exception ex) { failed.Add(action.Path + ": " + ex.Message); }
            }

            pending = new List<FileAction>();
            actionBar.Visible = false;

            var sb = new StringBuilder();
            if (done.Count > 0)
            {
                sb.AppendLine("**Сделано**");
                foreach (string d in done) sb.AppendLine("• " + d);
            }
            if (failed.Count > 0)
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.AppendLine("**Не вышло**");
                foreach (string f in failed) sb.AppendLine("• " + f);
            }
            if (done.Count > 0) sb.AppendLine(Environment.NewLine + "Прежние файлы сохранены в .dupo/backup.");

            Add("помощник", sb.ToString());
            Store();

            var h = FilesChanged;
            if (h != null) h(this, touched.Count > 0 ? touched[0] : "");
        }

        void BuildInput()
        {
            var box = new Panel();
            box.Dock = DockStyle.Bottom;
            box.Height = 76;
            box.BackColor = Theme.Panel;
            box.Padding = new Padding(6);

            input.Multiline = true;
            input.Dock = DockStyle.Fill;
            input.BackColor = Theme.Chrome;
            input.ForeColor = Theme.Text;
            input.BorderStyle = BorderStyle.FixedSingle;
            input.Font = new Font("Segoe UI", 9.5f);
            input.ScrollBars = ScrollBars.Vertical;
            input.KeyDown += InputKey;

            send.Dock = DockStyle.Right;
            send.Width = 96;
            send.Text = "Спросить";
            send.FlatStyle = FlatStyle.Flat;
            send.BackColor = Theme.Accent;
            send.ForeColor = Color.White;
            send.FlatAppearance.BorderColor = Theme.Accent;
            send.Click += delegate { Ask(); };

            box.Controls.Add(input);
            box.Controls.Add(send);
            Controls.Add(box);

            view.BringToFront();
        }

        void InputKey(object sender, KeyEventArgs e)
        {
            // Enter отправляет, Shift+Enter переводит строку.
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                Ask();
            }
        }

        // ------------------------------------------------------------------
        // проект
        // ------------------------------------------------------------------

        /// <summary>Переключиться на чат этого проекта.</summary>
        public void OpenProject(string dir)
        {
            Store();

            projectDir = dir;
            lines.Clear();
            lines.AddRange(Assistant.Load(dir));

            Redraw();

            if (lines.Count == 0) Greet();
            RefreshMode();
        }

        /// <summary>Сохранить историю на диск.</summary>
        public void Store()
        {
            if (!string.IsNullOrEmpty(projectDir) && lines.Count > 0)
                Assistant.Save(projectDir, lines);
        }

        void Greet()
        {
            string name = string.IsNullOrEmpty(projectDir) ? "" : Path.GetFileName(projectDir);

            Add("помощник",
                "Здравствуйте! Я знаю DupoLang, C# вместе с `Designer.cs`, Java и связку между ними." +
                (name.Length > 0 ? "\r\nЧат ведётся отдельно для проекта **" + name + "**." : "") +
                "\r\n\r\nСпросите, например: «как вызвать def из C#», «как устроен Designer.cs», " +
                "«как собрать C++», «список команд».");
        }

        void RefreshMode()
        {
            var online = Assistant.LoadOnline();

            if (online.Ollama) lblMode.Text = "Ollama на этом компьютере";
            else if (online.Ready) lblMode.Text = "сервис подключён";
            else lblMode.Text = "база знаний";

            lblMode.ForeColor = online.Ready ? Theme.Ok : Theme.Dim;
        }

        // ------------------------------------------------------------------
        // переписка
        // ------------------------------------------------------------------

        void Ask()
        {
            if (busy) return;

            string question = input.Text.Trim();
            if (question.Length == 0) return;

            input.Clear();
            Add("вы", question);

            var online = Assistant.LoadOnline();

            if (!online.Ready)
            {
                Reply(Local(question));
                return;
            }

            busy = true;
            send.Enabled = false;
            Add("помощник", "_думаю..._");

            string context = Safe(Context);

            string dir = Safe(ProjectDir);
            if (!string.IsNullOrEmpty(dir))
                context += Environment.NewLine + "Содержимое файлов:" + Environment.NewLine +
                           AssistantActions.Contents(dir, 24000);
            var history = lines.Take(Math.Max(0, lines.Count - 2)).ToList();

            var t = new Thread(delegate ()
            {
                string answer;
                try { answer = Assistant.AskOnline(online, context, history, question); }
                catch (Exception ex)
                {
                    answer = "Сервис не ответил: " + ex.Message +
                             "\r\n\r\nОтвечаю по своей базе знаний.\r\n\r\n" + Local(question);
                }

                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        // Убираем «думаю...» и кладём настоящий ответ.
                        if (lines.Count > 0 && lines[lines.Count - 1].Text == "_думаю..._")
                            lines.RemoveAt(lines.Count - 1);

                        busy = false;
                        send.Enabled = true;
                        Reply(answer);
                    }));
                }
                catch { }
            });
            t.IsBackground = true;
            t.Start();
        }

        string Local(string question)
        {
            string q = question.ToLowerInvariant();

            if (q.Contains("команд") && (q.Contains("спис") || q.Contains("все") || q.Contains("всё")))
                return Assistant.Commands();

            return Assistant.Answer(question, Safe(Context));
        }

        void Reply(string text)
        {
            var actions = AssistantActions.Parse(text);
            string clean = actions.Count > 0 ? AssistantActions.Strip(text) : text;

            if (clean.Trim().Length == 0)
                clean = "Подготовил правки — посмотрите список ниже.";

            Add("помощник", clean);
            Store();

            Offer(actions);
        }

        void Add(string who, string text)
        {
            lines.Add(new ChatLine { Who = who, Text = text ?? "" });
            Draw(lines[lines.Count - 1]);
        }

        void Clear()
        {
            lines.Clear();
            view.Clear();

            string path = Assistant.HistoryPath(projectDir);
            try { if (path != null && File.Exists(path)) File.Delete(path); }
            catch { }

            Greet();
        }

        void Redraw()
        {
            view.Clear();
            foreach (var l in lines) Draw(l);
        }

        // ------------------------------------------------------------------
        // рисование реплик
        // ------------------------------------------------------------------

        void Draw(ChatLine line)
        {
            Head(line);
            Body(line.Text);
            view.AppendText(Environment.NewLine);
            view.SelectionStart = view.TextLength;
            view.ScrollToCaret();
        }

        void Head(ChatLine line)
        {
            view.SelectionStart = view.TextLength;
            view.SelectionFont = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            view.SelectionColor = line.Mine ? Theme.Accent2 : Theme.Accent;
            view.AppendText((line.Mine ? "Вы" : "Помощник") +
                            (string.IsNullOrEmpty(line.Stamp) ? "" : "   " + line.Stamp) +
                            Environment.NewLine);
        }

        /// <summary>Разметка простая: ``` — код, ** — выделение, • — список.</summary>
        void Body(string text)
        {
            bool code = false;

            foreach (string raw in (text ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                string row = raw;

                if (row.TrimStart().StartsWith("```"))
                {
                    code = !code;
                    continue;
                }

                view.SelectionStart = view.TextLength;

                if (code)
                {
                    view.SelectionFont = new Font("Consolas", 9.5f);
                    view.SelectionColor = Theme.Keyword;
                    view.SelectionBackColor = Theme.Gutter;
                    view.AppendText("  " + row + Environment.NewLine);
                    view.SelectionBackColor = Theme.Back;
                    continue;
                }

                view.SelectionBackColor = Theme.Back;

                bool bold = row.StartsWith("**") && row.TrimEnd().EndsWith("**");
                if (bold) row = row.Trim().Trim('*');

                view.SelectionFont = new Font("Segoe UI", 9.5f, bold ? FontStyle.Bold : FontStyle.Regular);
                view.SelectionColor = bold ? Theme.Text : Theme.Text;
                view.AppendText(row.Replace("`", "") + Environment.NewLine);
            }
        }

        // ------------------------------------------------------------------
        // вставка кода и настройки
        // ------------------------------------------------------------------

        /// <summary>Код из последнего ответа — в редактор.</summary>
        void InsertLast()
        {
            var last = lines.LastOrDefault(l => !l.Mine);
            if (last == null) { Add("помощник", "Пока нечего вставлять."); return; }

            string code = CodeOf(last.Text);
            if (code.Length == 0)
            {
                Add("помощник", "В последнем ответе нет блока кода.");
                return;
            }

            var h = InsertCode;
            if (h == null) { Add("помощник", "Вставлять некуда — откройте файл в редакторе."); return; }

            h(code);
        }

        /// <summary>Вытащить содержимое блоков ``` из ответа.</summary>
        public static string CodeOf(string text)
        {
            var sb = new StringBuilder();
            bool code = false;

            foreach (string row in (text ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                if (row.TrimStart().StartsWith("```")) { code = !code; continue; }
                if (code) sb.AppendLine(row);
            }
            return sb.ToString().TrimEnd();
        }

        void Configure()
        {
            using (var dlg = new AssistantSetupForm())
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                RefreshMode();

                var now = Assistant.LoadOnline();
                Add("помощник", now.Ready
                    ? "Подключено: " + now.Where + ". Если не отзовётся — вернусь к своей базе знаний."
                    : "Внешний сервис отключён — отвечаю по своей базе знаний.");
                Store();
            }
        }

        static string Safe(Func<string> fn)
        {
            try { return fn == null ? "" : (fn() ?? ""); }
            catch { return ""; }
        }
    }

    /// <summary>Адрес, ключ и модель внешнего сервиса.</summary>
    public class AssistantSetupForm : Form
    {
        readonly TextBox endpoint = new TextBox();
        readonly TextBox key = new TextBox();
        readonly TextBox model = new TextBox();
        readonly CheckBox ollama = new CheckBox();
        readonly Label ollamaState = new Label();

        public AssistantSetupForm()
        {
            Text = "Помощник — внешний сервис";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(560, 312);
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9f);

            var hint = new Label();
            hint.Bounds = new Rectangle(16, 12, 528, 56);
            hint.ForeColor = Theme.Dim;
            hint.Text = "Помощник работает и без этого — по своей базе знаний о DupoLang, C# и Java.\r\n" +
                        "Ollama крутится прямо на этом компьютере: ключ и регистрация не нужны.\r\n" +
                        "Либо укажите адрес сервиса, совместимого с OpenAI, и ключ к нему.";
            Controls.Add(hint);

            Row("Адрес:", endpoint, 126, "https://api.openai.com/v1/chat/completions");
            Row("Ключ:", key, 158, "");
            Row("Модель:", model, 190, "qwen2.5-coder или gpt-4o-mini");
            key.UseSystemPasswordChar = true;

            ollama.Text = "Спрашивать Ollama на этом компьютере (бесплатно, без ключа)";
            ollama.Bounds = new Rectangle(18, 74, 526, 22);
            ollama.ForeColor = Theme.Text;
            ollama.CheckedChanged += delegate { Mode(); };
            Controls.Add(ollama);

            ollamaState.Bounds = new Rectangle(38, 98, 506, 20);
            ollamaState.ForeColor = Theme.Dim;
            Controls.Add(ollamaState);

            var saved = Assistant.LoadOnline();
            endpoint.Text = saved.Endpoint;
            key.Text = saved.Key;
            model.Text = saved.Model;
            ollama.Checked = saved.Ollama;
            Mode();

            Load += delegate { Probe(); };

            var ok = new Button();
            ok.Text = "Сохранить";
            ok.Bounds = new Rectangle(330, 258, 104, 30);
            Flat(ok);
            ok.BackColor = Theme.Accent;
            ok.ForeColor = Color.White;
            ok.Click += delegate
            {
                Assistant.SaveOnline(new Assistant.Online
                {
                    Endpoint = endpoint.Text.Trim(),
                    Key = key.Text.Trim(),
                    Model = model.Text.Trim(),
                    Ollama = ollama.Checked
                });
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(ok);

            var install = new Button();
            install.Text = "Поставить Ollama";
            install.Bounds = new Rectangle(18, 258, 150, 30);
            Flat(install);
            install.Click += delegate { Install(install); };
            Controls.Add(install);

            var off = new Button();
            off.Text = "Отключить";
            off.Bounds = new Rectangle(214, 258, 104, 30);
            Flat(off);
            off.Click += delegate
            {
                Assistant.SaveOnline(new Assistant.Online());
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(off);

            var cancel = new Button();
            cancel.Text = "Отмена";
            cancel.Bounds = new Rectangle(444, 258, 100, 30);
            cancel.DialogResult = DialogResult.Cancel;
            Flat(cancel);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        /// <summary>Поля сервиса нужны только без Ollama.</summary>
        void Mode()
        {
            bool manual = !ollama.Checked;
            endpoint.Enabled = manual;
            key.Enabled = manual;
            Probe();
        }

        /// <summary>Поставить Ollama и скачать модель — одной кнопкой.</summary>
        void Install(Button button)
        {
            button.Enabled = false;
            ollama.Checked = true;
            ollamaState.ForeColor = Theme.Dim;
            ollamaState.Text = "Начинаю...";

            string wanted = model.Text.Trim();

            var t = new Thread(delegate ()
            {
                string problem = null;

                try
                {
                    Say(Assistant.InstallOllama(Say));
                    Assistant.StartOllama();

                    if (Assistant.OllamaModels().Count == 0)
                        Say(Assistant.PullModel(wanted, Say));
                }
                catch (Exception ex) { problem = ex.Message; }

                string fail = problem;
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        button.Enabled = true;

                        if (fail != null)
                        {
                            ollamaState.ForeColor = Theme.Warn;
                            ollamaState.Text = "Не вышло: " + fail;
                            return;
                        }

                        Probe();
                    }));
                }
                catch { }
            });

            t.IsBackground = true;
            t.Start();
        }

        void Say(string text)
        {
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<string>(Say), text); }
                catch { }
                return;
            }
            ollamaState.Text = text;
        }

        void Probe()
        {
            if (!ollama.Checked)
            {
                ollamaState.Text = "";
                return;
            }

            ollamaState.Text = "Проверяю Ollama...";

            var t = new Thread(delegate ()
            {
                bool alive = Assistant.OllamaAlive;
                var models = alive ? Assistant.OllamaModels() : new List<string>();

                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        if (!alive)
                        {
                            ollamaState.Text = "Ollama не отвечает на " + Assistant.OllamaUrl +
                                               " — поставьте её с ollama.com и запустите.";
                            ollamaState.ForeColor = Theme.Warn;
                            return;
                        }

                        ollamaState.ForeColor = Theme.Ok;
                        ollamaState.Text = models.Count > 0
                            ? "Ollama работает. Модели: " + string.Join(", ", models.Take(4).ToArray())
                            : "Ollama работает, но моделей нет — выполните: ollama pull qwen2.5-coder";

                        if (model.Text.Trim().Length == 0 && models.Count > 0) model.Text = models[0];
                    }));
                }
                catch { }
            });

            t.IsBackground = true;
            t.Start();
        }

        void Row(string caption, TextBox box, int y, string placeholder)
        {
            var l = new Label();
            l.Text = caption;
            l.Bounds = new Rectangle(16, y + 4, 80, 20);
            l.ForeColor = Theme.Dim;
            Controls.Add(l);

            box.Bounds = new Rectangle(100, y, 444, 24);
            box.BackColor = Theme.Panel;
            box.ForeColor = Theme.Text;
            box.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(box);

            if (placeholder.Length > 0)
            {
                var tip = new ToolTip();
                tip.SetToolTip(box, "например: " + placeholder);
            }
        }

        static void Flat(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = Theme.Chrome;
            b.ForeColor = Theme.Text;
            b.FlatAppearance.BorderColor = Theme.Border;
        }
    }
}
