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
                
                // Calculate full virtual screen bounds in pixels
                int vLeft = Helpers.Win32Api.GetSystemMetrics(76); // SM_XVIRTUALSCREEN
                int vTop = Helpers.Win32Api.GetSystemMetrics(77); // SM_YVIRTUALSCREEN
                int vWidth = Helpers.Win32Api.GetSystemMetrics(78); // SM_CXVIRTUALSCREEN
                int vHeight = Helpers.Win32Api.GetSystemMetrics(79); // SM_CYVIRTUALSCREEN

                // Important: Coordinates after SetParent are relative to the parent's client area.
                // Since Progman/WorkerW already spans the entire virtual screen,
                // (0,0) relative to the parent is exactly the top-left of the virtual desktop.
                Helpers.Win32Api.SetWindowPos(hwnd, Helpers.Win32Api.HWND_TOP, 
                    0, 0, vWidth, vHeight, 
                    Helpers.Win32Api.SWP_SHOWWINDOW | Helpers.Win32Api.SWP_NOACTIVATE | Helpers.Win32Api.SWP_NOOWNERZORDER);
                
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
        IntPtr progman = FindWindow("Progman", null);
        IntPtr result = IntPtr.Zero;
        
        // Spawn the WorkerW/Wallpaper layer split
        SendMessageTimeout(progman, WM_SPAWN_WORKER, IntPtr.Zero, IntPtr.Zero, 0, 1000, out result);

        IntPtr workerw = IntPtr.Zero;

        // 1. Find the WorkerW that is the container for icons.
        EnumWindows((hwnd, lParam) =>
        {
            IntPtr shell = FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shell != IntPtr.Zero)
            {
                // Found the top-level window holding icons.
                // In some systems, this is a WorkerW. In others, it's Progman.
                workerw = hwnd;
            }
            return true;
        }, IntPtr.Zero);

        // 2. If we found an icon container, we want to be parented to its parent (Progman or Desktop)
        // so we are a sibling behind it.
        if (workerw != IntPtr.Zero)
        {
            // On Windows 10/11, if WorkerW was spawned, it's usually at the bottom of Z-order.
            // Returning Progman is the most reliable way to cover all screens.
            return progman;
        }

        // Special check for systems where SHELLDLL_DefView is directly under Progman
        if (FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
        {
            return progman;
        }

        return progman;
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
