using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace DupoStudio
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Ставим перехватчик до того, как среда выполнения полезет за DupoLang:
            // сам запуск вынесен в отдельный метод, иначе библиотека понадобится
            // ещё при входе в Main.
            EmbeddedRuntime.Install();

            // Фоновый режим: окон нет, работаем как сервис.
            if (args != null)
                foreach (string a in args)
                    if (string.Equals(a, "--agent", StringComparison.OrdinalIgnoreCase))
                    {
                        Environment.ExitCode = Agent(args);
                        return;
                    }

            Start(args);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static int Agent(string[] args) { return AgentService.Run(args); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Start(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Settings.Load();

            // Папка в командной строке важнее настроек.
            string project = args != null && args.Length > 0 && Directory.Exists(args[0])
                ? args[0]
                : Settings.AutostartProject;

            if (project == null)
            {
                // Автозапуска нет — показываем стартовое окно.
                Application.Run(new MainForm());
                return;
            }

            var editor = new ProjectEditorForm(project);
            Application.Run(editor);

            // «Закрыть проект» из окна редактора возвращает в стартовое окно.
            if (editor.BackToLauncher) Application.Run(new MainForm());
        }
    }
}
