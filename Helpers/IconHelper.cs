using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace PureDesktop.Helpers;

/// <summary>
/// Extracts file/folder icons from the Windows shell.
/// </summary>
public static class IconHelper
{
    /// <summary>
    /// Get the large icon for a file or folder path as a WPF ImageSource.
    /// </summary>
    public static BitmapSource? GetIcon(string path, bool large = true)
    {
        try
        {
            var shfi = new Win32Api.SHFILEINFO();
            uint flags = Win32Api.SHGFI_ICON | (large ? Win32Api.SHGFI_LARGEICON : Win32Api.SHGFI_SMALLICON);

            // For files that don't exist (e.g., .lnk targets), use USEFILEATTRIBUTES
            if (!File.Exists(path) && !Directory.Exists(path))
                flags |= Win32Api.SHGFI_USEFILEATTRIBUTES;

            IntPtr result = Win32Api.SHGetFileInfo(path, 0, ref shfi,
                (uint)Marshal.SizeOf<Win32Api.SHFILEINFO>(), flags);

            if (result == IntPtr.Zero || shfi.hIcon == IntPtr.Zero)
                return null;

            var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                shfi.hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            Win32Api.DestroyIcon(shfi.hIcon);

            bitmapSource.Freeze(); // Make it thread-safe
            return bitmapSource;
        }
        catch
        {
            return null;
        }
    }
}
