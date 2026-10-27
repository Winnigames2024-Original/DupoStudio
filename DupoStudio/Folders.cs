using System;
using System.Windows.Forms;
using DupoLang;

namespace DupoStudio
{
    /// <summary>
    /// Выбор папки. Берём обычное окно проводника; если система его
    /// не даёт, остаётся старое дерево — чтобы выбор был всегда.
    /// </summary>
    public static class Folders
    {
        public static string Ask(IWin32Window owner, string title, string startDir)
        {
            IntPtr handle = owner == null ? IntPtr.Zero : owner.Handle;

            string picked = FolderPicker.Pick(handle, title, startDir);
            if (picked != null) return picked;

            if (FolderPicker.Available) return null;   // окно показали, но отменили

            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = title ?? "Выберите папку";
                if (!string.IsNullOrEmpty(startDir)) dlg.SelectedPath = startDir;
                return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.SelectedPath : null;
            }
        }
    }
}
