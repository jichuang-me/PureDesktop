using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using PureDesktop.Helpers;

namespace PureDesktop.Helpers;

/// <summary>
/// A low-level mouse hook to detect double-clicks on the Desktop.
/// </summary>
public class MouseHook : IDisposable
{
    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData; public uint flags; public uint time; public IntPtr dwExtraInfo; }

    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_LBUTTONDOWN = 0x0201;

    private readonly LowLevelMouseProc _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private DateTime _lastClick = DateTime.MinValue;
    
    public event Action? OnDesktopDoubleClick;

    public MouseHook()
    {
        _proc = HookCallback;
    }

    public void Start()
    {
        _hookId = SetHook(_proc);
    }

    private IntPtr SetHook(LowLevelMouseProc proc)
    {
        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule? curModule = curProcess.MainModule)
        {
            if (curModule == null) return IntPtr.Zero;
            return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN)
        {
            var ms = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            var now = DateTime.Now;

            // Detect double click manually since LL hook only gives Down/Up
            if ((now - _lastClick).TotalMilliseconds < 500)
            {
                IntPtr hWnd = WindowFromPoint(ms.pt);
                if (IsDesktopWindow(hWnd))
                {
                    OnDesktopDoubleClick?.Invoke();
                }
                _lastClick = DateTime.MinValue;
            }
            else
            {
                _lastClick = now;
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private bool IsDesktopWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return false;
        var sb = new System.Text.StringBuilder(256);
        GetClassName(hWnd, sb, sb.Capacity);
        string name = sb.ToString();

        // Standard desktop classes
        if (name == "SysListView32" || name == "SHELLDLL_DefView" || name == "WorkerW" || name == "Progman")
            return true;

        return false;
    }

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
            UnhookWindowsHookEx(_hookId);
    }
}
