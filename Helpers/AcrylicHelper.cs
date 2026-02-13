using System.Windows.Interop;

namespace PureDesktop.Helpers;

/// <summary>
/// Applies Acrylic/Mica backdrop effects to WPF windows via DWM API.
/// </summary>
public static class AcrylicHelper
{
    /// <summary>
    /// Apply Mica backdrop to a window (Windows 11 22H2+).
    /// Falls back to Acrylic on older builds.
    /// </summary>
    public static void ApplyMica(System.Windows.Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            // Try Mica first (DWMWA_SYSTEMBACKDROP_TYPE = 38)
            int backdropType = Win32Api.DWMSBT_MAINWINDOW; // Mica
            int result = Win32Api.DwmSetWindowAttribute(hwnd,
                Win32Api.DWMWA_SYSTEMBACKDROP_TYPE,
                ref backdropType, sizeof(int));

            if (result != 0)
            {
                // Fallback: try the older Mica attribute (Win11 21H2)
                int micaValue = 1;
                Win32Api.DwmSetWindowAttribute(hwnd,
                    Win32Api.DWMWA_MICA_EFFECT,
                    ref micaValue, sizeof(int));
            }

            // Extend frame into client area to enable the effect
            var margins = new Win32Api.MARGINS(-1, -1, -1, -1);
            Win32Api.DwmExtendFrameIntoClientArea(hwnd, ref margins);
        }
        catch { }
    }

    /// <summary>
    /// Apply Acrylic (transient) backdrop to a window.
    /// </summary>
    public static void ApplyAcrylic(System.Windows.Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            int backdropType = Win32Api.DWMSBT_TRANSIENTWINDOW; // Acrylic
            Win32Api.DwmSetWindowAttribute(hwnd,
                Win32Api.DWMWA_SYSTEMBACKDROP_TYPE,
                ref backdropType, sizeof(int));

            var margins = new Win32Api.MARGINS(-1, -1, -1, -1);
            Win32Api.DwmExtendFrameIntoClientArea(hwnd, ref margins);
        }
        catch { }
    }
}
