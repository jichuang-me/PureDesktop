using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Drawing;
using System.Windows.Forms; // Needed for ContextMenu behavior (TrackPopupMenu) interaction

namespace PureDesktop.Core
{
    /// <summary>
    /// Helper to show the native Windows Shell Context Menu (IContextMenu).
    /// </summary>
    public static class ShellContextMenu
    {
        // ─── P/Invoke Definitions ────────────────────────────────────────

        [ComImport, Guid("000214E6-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellFolder
        {
            void ParseDisplayName(IntPtr hwnd, IntPtr pbc, [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName, out uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);
            void EnumObjects(IntPtr hwnd, int grfFlags, out IntPtr ppenumIDList);
            void BindToObject(IntPtr pidl, IntPtr pbc, [In] ref Guid riid, out IntPtr ppv);
            void BindToStorage(IntPtr pidl, IntPtr pbc, [In] ref Guid riid, out IntPtr ppv);
            void CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);
            void CreateViewObject(IntPtr hwndOwner, [In] ref Guid riid, out IntPtr ppv);
            void GetAttributesOf(uint cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, ref uint rgfInOut);
            void GetUIObjectOf(IntPtr hwndOwner, uint cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, [In] ref Guid riid, ref uint rgfReserved, out IntPtr ppv);
            void GetDisplayNameOf(IntPtr pidl, uint uFlags, IntPtr pName);
            void SetNameOf(IntPtr hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string pszName, uint uFlags, out IntPtr ppidlOut);
        }

        [ComImport, Guid("000214e4-0000-0000-c000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IContextMenu
        {
            [PreserveSig]
            int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
            [PreserveSig]
            int InvokeCommand(ref CMINVOKECOMMANDINFO pici);
            [PreserveSig]
            int GetCommandString(uint idcmd, uint uflags, ref uint pwReserved, [MarshalAs(UnmanagedType.LPStr)] StringBuilder pszName, uint cchMax);
        }

        // IContextMenu2 and 3 are needed for submenus (Open With, etc), 
        // but handling the messages (WM_INITMENUPOPUP, WM_DRAWITEM) requires a message loop hook.
        // For a WPF app, we might need a hidden window or hook the main window proc.
        // For simplicity, we implement basic IContextMenu first. 
        // If "Open With" fails to render, we'll need a wrapper window.

        [StructLayout(LayoutKind.Sequential)]
        private struct CMINVOKECOMMANDINFO
        {
            public int cbSize;
            public int fMask;
            public IntPtr hwnd;
            public IntPtr lpVerb;
            public string lpParameters;
            public string lpDirectory;
            public int nShow;
            public int dwHotKey;
            public IntPtr hIcon;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHGetDesktopFolder(out IShellFolder ppshf);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetSpecialFolderLocation(IntPtr hwndOwner, int nFolder, out IntPtr ppidl);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern void ILFree(IntPtr pidl);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern int TrackPopupMenuEx(IntPtr hmenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

        private const uint CMF_NORMAL = 0x00000000;
        private const uint CMF_EXPLORE = 0x00000004;
        
        private const uint TPM_RETURNCMD = 0x0100;
        private const uint TPM_LEFTBUTTON = 0x0000;
        private const uint TPM_RIGHTBUTTON = 0x0002;

        private static Guid IID_IContextMenu = new Guid("000214e4-0000-0000-c000-000000000046");

        // ─── Public API ──────────────────────────────────────────────────

        public static void Show(System.Windows.Window owner, string filePath, System.Windows.Point screenPos)
        {
            IntPtr pidlMain = IntPtr.Zero;
            IntPtr pidlItem = IntPtr.Zero;
            IntPtr contextMenuPtr = IntPtr.Zero;
            IntPtr hMenu = IntPtr.Zero;

            IShellFolder? desktopFolder = null;
            IShellFolder? parentFolder = null;
            IContextMenu? contextMenu = null;

            try
            {
                // 1. Get Desktop Folder
                if (SHGetDesktopFolder(out desktopFolder) != 0) return;

                // 2. Parse Path to PIDL
                uint pchEaten = 0;
                uint pdwAttributes = 0;
                // We need the parent folder and the child item ID
                string? folderPath = System.IO.Path.GetDirectoryName(filePath);
                string? fileName = System.IO.Path.GetFileName(filePath);

                if (string.IsNullOrEmpty(folderPath) || string.IsNullOrEmpty(fileName)) return;

                // Get PIDL of the parent folder
                desktopFolder.ParseDisplayName(IntPtr.Zero, IntPtr.Zero, folderPath, out pchEaten, out pidlMain, ref pdwAttributes);

                // Bind to Parent Folder
                // We need the GUID of IShellFolder
                Guid iidShellFolder = typeof(IShellFolder).GUID; 
                desktopFolder.BindToObject(pidlMain, IntPtr.Zero, ref iidShellFolder, out IntPtr ppvParent);
                parentFolder = (IShellFolder)Marshal.GetObjectForIUnknown(ppvParent);

                // Get PIDL of the child file relative to parent
                parentFolder.ParseDisplayName(IntPtr.Zero, IntPtr.Zero, fileName, out pchEaten, out pidlItem, ref pdwAttributes);

                // 3. Get IContextMenu
                IntPtr[] apidl = new IntPtr[] { pidlItem };
                parentFolder.GetUIObjectOf(new System.Windows.Interop.WindowInteropHelper(owner).Handle, 1, apidl, ref IID_IContextMenu, 0, out contextMenuPtr);
                contextMenu = (IContextMenu)Marshal.GetObjectForIUnknown(contextMenuPtr);

                // 4. Create Popup Menu
                hMenu = CreatePopupMenu();
                if (contextMenu.QueryContextMenu(hMenu, 0, 1, 0x7FFF, CMF_NORMAL | CMF_EXPLORE) >= 0) // HRESULT check logic simplified
                {
                    // 5. Track Popup Menu
                    // We use TPM_RETURNCMD to get the command ID back, ensuring we just execute it.
                    // Note: Handle complex submenus (like Open With) might require window hooking which is complex.
                    // For now, let's try standard invocation.
                    
                    int command = TrackPopupMenuEx(hMenu, TPM_RETURNCMD | TPM_LEFTBUTTON, (int)screenPos.X, (int)screenPos.Y, new System.Windows.Interop.WindowInteropHelper(owner).Handle, IntPtr.Zero);

                    // 6. Invoke Command
                    if (command > 0)
                    {
                        var ici = new CMINVOKECOMMANDINFO();
                        ici.cbSize = Marshal.SizeOf(ici);
                        ici.hwnd = new System.Windows.Interop.WindowInteropHelper(owner).Handle;
                        ici.lpVerb = (IntPtr)(command - 1); // idCmdFirst was 1, so offset back
                        ici.nShow = 1; // SW_SHOWNORMAL

                        contextMenu.InvokeCommand(ref ici);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            finally
            {
                if (hMenu != IntPtr.Zero) DestroyMenu(hMenu);
                if (pidlItem != IntPtr.Zero) ILFree(pidlItem);
                if (pidlMain != IntPtr.Zero) ILFree(pidlMain);
                if (contextMenu != null) Marshal.ReleaseComObject(contextMenu);
                if (parentFolder != null) Marshal.ReleaseComObject(parentFolder);
                if (desktopFolder != null) Marshal.ReleaseComObject(desktopFolder);
            }
        }
    }
}
