using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PureDesktop.Core;

/// <summary>
/// Embeds the window into the Windows Desktop (WorkerW) to persist during Win+D (Show Desktop).
/// Fallbacks to bottom-most Z-order if WorkerW injection fails.
/// </summary>
public static class DesktopEmbedder
{
    // P/Invoke definitions
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string? className, string? windowTitle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SendMessageTimeout(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private const uint WM_SPAWN_WORKER = 0x052C;
    private static readonly IntPtr HWND_BOTTOM = new(1);

    /// <summary>
    /// Embeds the window into the desktop.
    /// Returns true if successfully attached to WorkerW (Win+D compatible).
    /// Returns false if fell back to generic bottom-most positioning.
    /// </summary>
    public static bool Embed(Window window)
    {
        try
        {
            var hwndSource = (HwndSource)PresentationSource.FromVisual(window);
            if (hwndSource == null) return false;
            IntPtr hwnd = hwndSource.Handle;

            // 1. Force ToolWindow style to hide from Taskbar and Alt+Tab
            // We set it BEFORE parent change to be effective
            int exStyle = Helpers.Win32Api.GetWindowLong(hwnd, Helpers.Win32Api.GWL_EXSTYLE);
            exStyle |= Helpers.Win32Api.WS_EX_TOOLWINDOW;
            exStyle |= Helpers.Win32Api.WS_EX_NOACTIVATE;
            Helpers.Win32Api.SetWindowLong(hwnd, Helpers.Win32Api.GWL_EXSTYLE, exStyle);

            // 2. Try to find WorkerW to attach to (Win+D persistence)
            IntPtr workerw = GetWorkerW();
            if (workerw != IntPtr.Zero)
            {
                // Set as WS_CHILD to become a truly integrated part of the desktop
                int style = Helpers.Win32Api.GetWindowLong(hwnd, Helpers.Win32Api.GWL_STYLE);
                style |= 0x40000000; // WS_CHILD
                style &= unchecked((int)~0x80000000); // Remove WS_POPUP (unchecked for overflow)
                Helpers.Win32Api.SetWindowLong(hwnd, Helpers.Win32Api.GWL_STYLE, style);

                SetParent(hwnd, workerw);
                
                // Calculate full virtual screen bounds
                double vWidth = SystemParameters.VirtualScreenWidth;
                double vHeight = SystemParameters.VirtualScreenHeight;

                // Important: In a WorkerW, 0,0 is the top-left of the virtual screen area.
                // We map 0,0 to the parent's origin.
                // We also ensure it's at the TOP of the Z-order within the WorkerW to be above the wallpaper.
                Helpers.Win32Api.SetWindowPos(hwnd, Helpers.Win32Api.HWND_TOP, 
                    0, 0, (int)vWidth, (int)vHeight, 
                    Helpers.Win32Api.SWP_SHOWWINDOW);
                
                return true;
            }

            // 2. Fallback: Just place at bottom of Z-order
            var workArea = SystemParameters.WorkArea;
            Helpers.Win32Api.SetWindowPos(hwnd, HWND_BOTTOM,
                (int)workArea.Left, (int)workArea.Top,
                (int)workArea.Width, (int)workArea.Height,
                Helpers.Win32Api.SWP_SHOWWINDOW);

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static IntPtr GetWorkerW()
    {
        // 1. Spawn a WorkerW behind the desktop icons if one doesn't exist
        IntPtr progman = FindWindow("Progman", null);
        IntPtr result = IntPtr.Zero;
        
        // Send 0x052C to Progman. This message instructs the OS to spawn a WorkerW behind the desktop icons.
        SendMessageTimeout(progman, WM_SPAWN_WORKER, IntPtr.Zero, IntPtr.Zero, 0, 1000, out result);

        IntPtr workerw = IntPtr.Zero;

        // 2. Find the WorkerW that is the BEHIND the desktop icons (SHELLDLL_DefView).
        EnumWindows((hwnd, lParam) =>
        {
            IntPtr shell = FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shell != IntPtr.Zero)
            {
                // Found the WorkerW that holds the icons.
                // The WorkerW BEHIND (sibling after) this one is the wallpaper container we want.
                workerw = FindWindowEx(IntPtr.Zero, hwnd, "WorkerW", null);
            }
            return true;
        }, IntPtr.Zero);

        // 3. Fallback: If the above failed (can happen on some versions/states), 
        // try to find ANY WorkerW that doesn't have SHELLDLL_DefView.
        if (workerw == IntPtr.Zero)
        {
            EnumWindows((hwnd, lParam) =>
            {
                if (GetWindowClassName(hwnd) == "WorkerW")
                {
                    IntPtr shell = FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (shell == IntPtr.Zero)
                    {
                        workerw = hwnd;
                        return false; // Found one!
                    }
                }
                return true;
            }, IntPtr.Zero);
        }

        return workerw;
    }

    private static string GetWindowClassName(IntPtr hwnd)
    {
        var sb = new System.Text.StringBuilder(256);
        GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    public static void Detach(Window window)
    {
        // No explicit detach needed as process termination handles cleanup,
        // but resetting parent could be done if we wanted to support "Un-embed" at runtime.
    }
}
