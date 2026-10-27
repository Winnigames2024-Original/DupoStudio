using System;
using System.IO;
using System.Runtime.InteropServices;

namespace DupoLang
{
    /// <summary>
    /// Выбор папки обычным проводником Windows, а не крошечным деревом
    /// из FolderBrowserDialog: то же окно, что в «Открыть файл», —
    /// с адресной строкой, поиском и закладками.
    /// </summary>
    public static class FolderPicker
    {
        /// <summary>
        /// Показать окно выбора папки. Возвращает путь или null, если отменили.
        /// Если системное окно недоступно, вернёт null — тогда вызывающий
        /// показывает своё.
        /// </summary>
        public static string Pick(IntPtr owner, string title, string startDir)
        {
            IFileDialog dialog = null;

            try
            {
                dialog = (IFileDialog)new FileOpenDialogRCW();
            }
            catch { return null; }

            try
            {
                uint options;
                dialog.GetOptions(out options);
                dialog.SetOptions(options | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST);

                if (!string.IsNullOrEmpty(title)) dialog.SetTitle(title);

                SetStart(dialog, startDir);

                const uint CANCELLED = 0x800704C7;
                uint hr = (uint)dialog.Show(owner);
                if (hr == CANCELLED || hr != 0) return null;

                IShellItem item;
                dialog.GetResult(out item);
                if (item == null) return null;

                try
                {
                    string path;
                    item.GetDisplayName(SIGDN_FILESYSPATH, out path);
                    return path;
                }
                finally { Release(item); }
            }
            catch { return null; }
            finally { Release(dialog); }
        }

        /// <summary>Доступно ли системное окно (Windows Vista и новее).</summary>
        public static bool Available
        {
            get
            {
                try
                {
                    var probe = new FileOpenDialogRCW();
                    Release(probe);
                    return true;
                }
                catch { return false; }
            }
        }

        static void SetStart(IFileDialog dialog, string startDir)
        {
            string dir = startDir ?? "";

            // Если указали файл или несуществующую папку — поднимаемся вверх,
            // пока не найдём то, что есть на диске.
            try
            {
                while (dir.Length > 0 && !Directory.Exists(dir))
                    dir = Path.GetDirectoryName(dir) ?? "";
            }
            catch { dir = ""; }

            if (dir.Length == 0) return;

            try
            {
                object item;
                int hr = SHCreateItemFromParsingName(dir, IntPtr.Zero, typeof(IShellItem).GUID, out item);
                if (hr != 0 || item == null) return;

                try { dialog.SetFolder((IShellItem)item); }
                finally { Release(item); }
            }
            catch { }
        }

        static void Release(object com)
        {
            if (com != null && Marshal.IsComObject(com))
            {
                try { Marshal.ReleaseComObject(com); } catch { }
            }
        }

        // ------------------------------------------------------------------
        // COM
        // ------------------------------------------------------------------

        const uint FOS_PICKFOLDERS = 0x00000020;
        const uint FOS_FORCEFILESYSTEM = 0x00000040;
        const uint FOS_PATHMUSTEXIST = 0x00000800;
        const uint SIGDN_FILESYSPATH = 0x80058000;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string path,
            IntPtr bindingContext,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out object item);

        [ComImport, ClassInterface(ClassInterfaceType.None)]
        [Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
        class FileOpenDialogRCW { }

        [ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IFileDialog
        {
            // IModalWindow
            [PreserveSig] int Show(IntPtr parent);

            // IFileDialog — порядок менять нельзя: это таблица вызовов.
            void SetFileTypes(uint count, IntPtr filters);
            void SetFileTypeIndex(uint index);
            void GetFileTypeIndex(out uint index);
            void Advise(IntPtr sink, out uint cookie);
            void Unadvise(uint cookie);
            void SetOptions(uint options);
            void GetOptions(out uint options);
            void SetDefaultFolder(IShellItem folder);
            void SetFolder(IShellItem folder);
            void GetFolder(out IShellItem folder);
            void GetCurrentSelection(out IShellItem item);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            void GetResult(out IShellItem item);
            void AddPlace(IShellItem place, int order);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
            void Close([MarshalAs(UnmanagedType.Error)] int result);
            void SetClientGuid([MarshalAs(UnmanagedType.LPStruct)] Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr filter);
        }

        [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellItem
        {
            void BindToHandler(IntPtr bindingContext,
                               [MarshalAs(UnmanagedType.LPStruct)] Guid handler,
                               [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
                               out IntPtr result);
            void GetParent(out IShellItem parent);
            void GetDisplayName(uint kind, [MarshalAs(UnmanagedType.LPWStr)] out string name);
            void GetAttributes(uint mask, out uint attributes);
            void Compare(IShellItem other, uint hint, out int order);
        }
    }
}
