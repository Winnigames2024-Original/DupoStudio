using System;
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
            Start(args);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Start(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(args));
        }
    }
}
