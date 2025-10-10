//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
//Copyright Warren Harding 2025.
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MyndSproutApp
{
    public static class FolderDialog
    {
        public static string? SelectFolder(Window window, string? initialDirectory = null)
        {
            var ownerHandle = new WindowInteropHelper(window).Handle;
            var dialog = new FileOpenDialog() as IFileDialog;
            if (dialog == null)
            {
                return null;
            }
            try
            {
                IShellItem item = null!;
                if (!string.IsNullOrEmpty(initialDirectory))
                {
                    var riid = typeof(IShellItem).GUID;
                    SHCreateItemFromParsingName(initialDirectory, IntPtr.Zero, ref riid, out item);
                }

                dialog.SetOptions(FOS.FOS_PICKFOLDERS | FOS.FOS_FORCEFILESYSTEM | FOS.FOS_NOCHANGEDIR);
                if (item != null)
                {
                    dialog.SetFolder(item);
                }

                uint hr = dialog.Show(ownerHandle);
                if (hr == ERROR_CANCELLED)
                    return null;

                if (hr != 0)
                    return null;

                dialog.GetResult(out item);
                if (item == null)
                    return null;

                item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out string path);
                return path;
            }
            finally
            {
                if (dialog != null)
                    Marshal.ReleaseComObject(dialog);
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            ref Guid riid,
            [Out, MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);

        private const uint ERROR_CANCELLED = 0x800704C7;

        [ComImport]
        [Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
        private class FileOpenDialog { }

        [ComImport]
        [Guid("42f85136-db7e-439c-85f1-e4075d135fc8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileDialog
        {
            [PreserveSig] uint Show([In] IntPtr parent);
            void SetFileTypes();
            void SetFileTypeIndex([In] uint iFileType);
            void GetFileTypeIndex(out uint piFileType);
            void Advise();
            void Unadvise();
            void SetOptions([In] FOS fos);
            void GetOptions(out FOS pfos);
            void SetDefaultFolder(IShellItem psi);
            void SetFolder(IShellItem psi);
            void GetFolder(out IShellItem ppsi);
            void GetCurrentSelection(out IShellItem ppsi);
            void SetFileName([In, MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
            void SetTitle([In, MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            void SetOkButtonLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszText);
            void SetFileNameLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
            void GetResult(out IShellItem ppsi);
            void AddPlace(IShellItem psi, int alignment);
            void SetDefaultExtension([In, MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
            void Close(uint hr);
            void SetClientGuid();
            void ClearClientData();
            void SetFilter([MarshalAs(UnmanagedType.Interface)] IntPtr pFilter);
            void GetResults([MarshalAs(UnmanagedType.Interface)] out IntPtr ppenum);
            void GetSelectedItems([MarshalAs(UnmanagedType.Interface)] out IntPtr ppsai);
        }

        [ComImport]
        [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler();
            void GetParent();
            void GetDisplayName([In] SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
            void GetAttributes();
            void Compare();
        }

        private enum SIGDN : uint
        {
            SIGDN_FILESYSPATH = 0x80058000
        }

        [Flags]
        private enum FOS
        {
            FOS_PICKFOLDERS = 0x20,
            FOS_FORCEFILESYSTEM = 0x40,
            FOS_NOCHANGEDIR = 0x8
        }
    }
}

