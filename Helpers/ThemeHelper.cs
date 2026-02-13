using Microsoft.Win32;

namespace PureDesktop.Helpers;

/// <summary>
/// Detects and monitors the Windows system theme (Light/Dark).
/// Supports three-way theme mode: System / Light / Dark.
/// </summary>
public static class ThemeHelper
{
    private const string ThemeRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string ThemeValueName = "AppsUseLightTheme";

    private static string _currentMode = "system";
    private static System.Windows.Threading.DispatcherTimer? _timer;

    /// <summary>
    /// Returns true if Windows is currently in dark mode.
    /// </summary>
    public static bool IsDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ThemeRegistryKey);
            var value = key?.GetValue(ThemeValueName);
            if (value is int intVal)
                return intVal == 0; // 0 = dark, 1 = light
        }
        catch { }
        return false;
    }

    /// <summary>
    /// Apply theme based on mode: "system", "light", or "dark".
    /// </summary>
    public static void ApplyThemeMode(string mode)
    {
        _currentMode = mode;
        bool dark = mode switch
        {
            "light" => false,
            "dark" => true,
            _ => IsDarkMode() // "system" follows OS
        };
        ApplyTheme(dark);
    }

    /// <summary>
    /// Apply the appropriate theme ResourceDictionary to the application.
    /// </summary>
    public static void ApplyTheme(bool dark)
    {
        var app = System.Windows.Application.Current;
        if (app == null) return;

        string themeUri = dark
            ? "pack://application:,,,/Resources/ThemeDark.xaml"
            : "pack://application:,,,/Resources/ThemeLight.xaml";

        // Remove existing theme dictionaries
        var toRemove = app.Resources.MergedDictionaries
            .Where(d => d.Source != null && d.Source.OriginalString.Contains("Theme"))
            .ToList();
        foreach (var d in toRemove)
            app.Resources.MergedDictionaries.Remove(d);

        // Add new theme
        app.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary
        {
            Source = new Uri(themeUri)
        });

        // Set DWM dark mode attribute on all windows
        foreach (System.Windows.Window win in app.Windows)
        {
            SetDwmDarkMode(win, dark);
        }
    }

    /// <summary>
    /// Set the DWM immersive dark mode attribute on a window.
    /// </summary>
    public static void SetDwmDarkMode(System.Windows.Window window, bool dark)
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            int value = dark ? 1 : 0;
            Win32Api.DwmSetWindowAttribute(hwnd, Win32Api.DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref value, sizeof(int));
        }
        catch { }
    }

    /// <summary>
    /// Start monitoring for theme changes via registry watcher.
    /// Only triggers re-apply when mode is "system".
    /// </summary>
    public static void StartMonitoring(Action<bool> onThemeChanged)
    {
        _timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };

        bool lastDark = IsDarkMode();
        _timer.Tick += (s, e) =>
        {
            if (_currentMode != "system") return; // Only monitor in system mode

            bool currentDark = IsDarkMode();
            if (currentDark != lastDark)
            {
                lastDark = currentDark;
                onThemeChanged(currentDark);
            }
        };
        _timer.Start();
    }
}
