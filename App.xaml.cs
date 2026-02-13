using System.Drawing;
using System.Diagnostics;
using System.Windows;
using PureDesktop.Helpers;
using PureDesktop.Views;
using WinForms = System.Windows.Forms;

namespace PureDesktop;

public partial class App : Application
{
    private WinForms.NotifyIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private Window? _hiddenOwner;
    private string _currentThemeMode = "system";

    private System.Threading.Mutex? _mutex;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        const string appName = "PureDesktop_SingleInstance_Mutex";
        bool createdNew;
        _mutex = new System.Threading.Mutex(true, appName, out createdNew);

        if (!createdNew)
        {
            // App is already running - Perform silent restart
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var existingProcesses = Process.GetProcessesByName(currentProcess.ProcessName);
                foreach (var p in existingProcesses)
                {
                    if (p.Id != currentProcess.Id)
                    {
                        p.Kill();
                        p.WaitForExit(3000);
                    }
                }
            }
            catch { }

            // Re-acquire mutex for the new instance
            _mutex = new System.Threading.Mutex(true, appName, out createdNew);
        }

        // Create hidden owner for taskbar hiding
        _hiddenOwner = new Window
        {
            Width = 0, Height = 0, WindowStyle = WindowStyle.None,
            ShowInTaskbar = false, AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent
        };
        _hiddenOwner.Show();

        // Global exception handling
        this.DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;

        EnsureAssets();

        try
        {
            // Load settings to get theme mode
            var fm = new Core.FenceManager();
            fm.Load();
            _currentThemeMode = fm.Settings.ThemeMode;

            // Apply theme based on mode
            ThemeHelper.ApplyThemeMode(_currentThemeMode);

            // Monitor for system theme changes
            ThemeHelper.StartMonitoring(dark => ThemeHelper.ApplyTheme(dark));

            // Apply saved language
            if (fm.Settings.Language != "zh-CN")
            {
                SwitchLanguage(fm.Settings.Language);
            }

            // Create tray icon
            SetupTrayIcon();
        }
        catch (Exception ex)
        {
            LogException(ex);
            Shutdown(1);
        }
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception);
        e.Handled = true;
        Shutdown(1);
    }

    private void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogException(ex);
        }
    }

    private void LogException(Exception ex)
    {
        try
        {
            string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_error.log");
            System.IO.File.AppendAllText(logPath, $"{DateTime.Now}: {ex}\n\n");
            MessageBox.Show($"Startup Error: {ex.Message}", "PureDesktop Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch { }
    }

    public void SetTrayIconVisible(bool visible)
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visible = visible;
        }
    }

    private void SetupTrayIcon()
    {
        var fm = new Core.FenceManager();
        fm.Load();

        _trayIcon = new WinForms.NotifyIcon
        {
            Visible = fm.Settings.ShowTrayIcon,
            Text = "PureDesktop",
            Icon = LoadTrayIcon()
        };

        RebuildTrayMenu();

        _trayIcon.DoubleClick += (s, e) =>
        {
            if (_mainWindow != null)
            {
                _mainWindow.Activate();
            }
        };

        _trayIcon.MouseClick += (s, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left)
            {
                _mainWindow = Application.Current.MainWindow as PureDesktop.Views.MainWindow
                             ?? Application.Current.Windows.OfType<PureDesktop.Views.MainWindow>().FirstOrDefault();
                _mainWindow?.ToggleFences();
            }
        };
    }

    private void RebuildTrayMenu()
    {
        if (_trayIcon == null) return;

        string Res(string key) => TryFindResource(key) as string ?? key;
        var menu = new WinForms.ContextMenuStrip();

        // 1. Show/Hide Tray Icon (Toggle) - Actually user said "Show/Hide Tray Icon"
        // But if they hide it, they can't see this menu.
        // It's likely they want "Show/Hide Fences" or just "Hide Tray Icon".
        // Given the context of "System Settings -> Show Tray Icon", let's make this item 
        // toggle the setting. If they click it to Hide, the icon vanishes.
        var toggleTrayItem = new WinForms.ToolStripMenuItem(Res("Tray_ShowTrayIcon"));
        toggleTrayItem.Checked = true; // It's visible right now
        toggleTrayItem.Click += (s, e) =>
        {
            // Set setting to false
            var fm = new Core.FenceManager();
            fm.Load();
            fm.Settings.ShowTrayIcon = false;
            fm.Save();
            
            // Apply (hide icon)
            _trayIcon.Visible = false;
            
            // Sync main window if needed (optional, but good for consistency)
            _mainWindow = Application.Current.MainWindow as PureDesktop.Views.MainWindow 
                         ?? Application.Current.Windows.OfType<PureDesktop.Views.MainWindow>().FirstOrDefault();
            // We can't easily sync the main window menu checkmark from here without exposing a method, 
            // but the main window usually re-reads settings on menu open or we can force a sync.
            // For now, just hiding the icon is what "Hide" does.
        };
        menu.Items.Add(toggleTrayItem);

        // 2. Restart
        var restartItem = new WinForms.ToolStripMenuItem(Res("Menu_Restart"));
        restartItem.Click += (s, e) =>
        {
            string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (exePath != null)
            {
                Process.Start(exePath);
                Shutdown();
            }
        };
        menu.Items.Add(restartItem);

        // 3. Exit
        var exitItem = new WinForms.ToolStripMenuItem(Res("Tray_Exit"));
        exitItem.Click += (s, e) =>
        {
            _trayIcon!.Visible = false;
            _trayIcon.Dispose();
            Shutdown();
        };
        menu.Items.Add(exitItem);

        menu.Items.Add(new WinForms.ToolStripSeparator());

        // 4. About
        var aboutItem = new WinForms.ToolStripMenuItem(Res("Tray_About"));
        aboutItem.Click += (s, e) => ShowAbout();
        menu.Items.Add(aboutItem);

        _trayIcon.ContextMenuStrip = menu;
    }


    public void ApplyOpacity(double opacity)
    {
        var fm = new Core.FenceManager();
        fm.Load();
        fm.Settings.FenceOpacity = opacity;
        fm.Save();

        _mainWindow = Application.Current.MainWindow as PureDesktop.Views.MainWindow 
                     ?? Application.Current.Windows.OfType<PureDesktop.Views.MainWindow>().FirstOrDefault();
        if (_mainWindow?.DataContext is ViewModels.MainViewModel vm)
        {
            vm.FenceOpacity = opacity;
        }
        RebuildTrayMenu();
    }

    public void ApplyAccent(string hex, bool previewOnly = false)
    {
        if (!previewOnly)
        {
            var fm = new Core.FenceManager();
            fm.Load();
            fm.Settings.AccentColor = hex;
            fm.Save();
        }

        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            Resources["Accent"] = new System.Windows.Media.SolidColorBrush(color);
            Resources["AccentColor"] = color;

            // Update derived brushes to provide real-time feedback on grid/hover
            // Hover: 15% opacity accent
            var hoverColor = System.Windows.Media.Color.FromArgb((byte)(255 * 0.15), color.R, color.G, color.B);
            Resources["HoverBrush"] = new System.Windows.Media.SolidColorBrush(hoverColor);

            // FenceBorder: 30% accent for visibility
            var borderColor = System.Windows.Media.Color.FromArgb((byte)(255 * 0.3), color.R, color.G, color.B);
            Resources["FenceBorder"] = new System.Windows.Media.SolidColorBrush(borderColor);
            
            // HighlightBrush: 100% accent (used for selection/borders if needed)
            Resources["HighlightBrush"] = new System.Windows.Media.SolidColorBrush(color);
            
            // Grid Lines (Group Headers) use AccentBrush
            Resources["AccentBrush"] = new System.Windows.Media.SolidColorBrush(color);
        }
        catch { }

        if (!previewOnly)
        {
            RebuildTrayMenu();
        }
    }

    private void OnCustomAccentClick()
    {
        var dialog = new InputDialog("Accent Color", "Enter Hex code (e.g. #FF0000):", "#60CDFF");
        dialog.Owner = _hiddenOwner;
        if (dialog.ShowDialog() == true)
        {
            ApplyAccent(dialog.InputValue);
        }
    }

    private bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
            return key?.GetValue("PureDesktop") != null;
        }
        catch { return false; }
    }

    private void SetAutoStart(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;
            if (enable)
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                key.SetValue("PureDesktop", $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue("PureDesktop", false);
            }
        }
        catch { }
        RebuildTrayMenu();
    }

    private void ManageExclusions()
    {
        var win = new ExclusionsWindow(new Core.FenceManager());
        win.Owner = _hiddenOwner;
        win.ShowDialog();
    }

    private void ShowAbout()
    {
        string aboutMsg = "PureDesktop v1.2.0\nProfessional Edition\n\nA modern, high-performance desktop organizer.\nCopyright © 2026 PureDesktop Team.";
        MessageBox.Show(aboutMsg, "About PureDesktop", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void SwitchTheme(string mode)
    {
        _currentThemeMode = mode;
        ThemeHelper.ApplyThemeMode(mode);

        // Save to settings
        var fm = new Core.FenceManager();
        fm.Load();
        fm.Settings.ThemeMode = mode;
        fm.Save();

        // Rebuild tray menu to update check marks
        RebuildTrayMenu();
    }

    public void SwitchLanguage(string lang)
    {
        // Remove existing string dictionaries
        var toRemove = Resources.MergedDictionaries
            .Where(d => d.Source != null && d.Source.OriginalString.Contains("Strings."))
            .ToList();
        foreach (var d in toRemove)
            Resources.MergedDictionaries.Remove(d);

        // Add new language dictionary
        string uri = $"pack://application:,,,/Resources/Strings.{lang}.xaml";
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(uri)
        });

        // Save to settings
        var fm = new Core.FenceManager();
        fm.Load();
        fm.Settings.Language = lang;
        fm.Save();

        // Rebuild tray menu with new strings
        RebuildTrayMenu();

        // Refresh fence titles
        _mainWindow = Application.Current.MainWindow as PureDesktop.Views.MainWindow 
                     ?? Application.Current.Windows.OfType<PureDesktop.Views.MainWindow>().FirstOrDefault();
        if (_mainWindow?.DataContext is ViewModels.MainViewModel vm)
        {
            foreach (var fence in vm.Fences)
            {
                fence.RefreshItems();
            }
        }
    }

    private void OnAddMappedFence()
    {
        var dialog = new WinForms.FolderBrowserDialog
        {
            Description = TryFindResource("Dlg_SelectFolder") as string ?? "Select Folder",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            _mainWindow = Application.Current.MainWindow as PureDesktop.Views.MainWindow 
                         ?? Application.Current.Windows.OfType<PureDesktop.Views.MainWindow>().FirstOrDefault();
            _mainWindow?.AddMappedFence(dialog.SelectedPath);
        }
    }

    private void EnsureAssets()
    {
        try
        {
            string assetsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
            if (!System.IO.Directory.Exists(assetsDir)) System.IO.Directory.CreateDirectory(assetsDir);

            string iconPath = System.IO.Path.Combine(assetsDir, "app_icon.ico");
            if (!System.IO.File.Exists(iconPath))
            {
                using var icon = CreateFallbackIcon();
                using var stream = new System.IO.FileStream(iconPath, System.IO.FileMode.Create);
                icon.Save(stream);
            }
            
            // Also try to save to source directory if in dev
            try {
                string srcAssets = @"c:\Users\jichu\OneDrive - onoffices\AI产品\Tidy-desktop\PureDesktop\Assets";
                if (System.IO.Directory.Exists(srcAssets)) {
                    string srcIcon = System.IO.Path.Combine(srcAssets, "app_icon.ico");
                    if (!System.IO.File.Exists(srcIcon)) {
                        using var icon = CreateFallbackIcon();
                        using var stream = new System.IO.FileStream(srcIcon, System.IO.FileMode.Create);
                        icon.Save(stream);
                    }
                }
            } catch {}
        }
        catch { }
    }

    private static Icon LoadTrayIcon()
    {
        try
        {
            string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app_icon.ico");
            if (System.IO.File.Exists(iconPath))
                return new Icon(iconPath);
        }
        catch { }

        return CreateFallbackIcon();
    }

    private static Icon CreateFallbackIcon()
    {
        // 32x32 icon representing organized "Fences" (Grid)
        var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Use a Fluent Blue accent color
            using var brush = new SolidBrush(Color.FromArgb(0, 120, 212));
            
            // Draw 4 rounded boxes representing organized desktop areas (Fences)
            float padding = 2;
            float size = 12;
            float corner = 3;

            // Top Left
            g.FillRoundedRectangle(brush, new RectangleF(padding + 1, padding + 1, size, size), corner);
            // Top Right
            g.FillRoundedRectangle(brush, new RectangleF(padding + size + 3, padding + 1, size, size), corner);
            // Bottom Left
            g.FillRoundedRectangle(brush, new RectangleF(padding + 1, padding + size + 3, size, size), corner);
            
            // Bottom Right (Highlight/Accent)
            using var accentBrush = new SolidBrush(Color.FromArgb(96, 205, 255)); // Light Blue
            g.FillRoundedRectangle(accentBrush, new RectangleF(padding + size + 3, padding + size + 3, size, size), corner);
        }
        var hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        
        // Ensure settings are saved on exit
        if (_mainWindow != null)
        {
            (_mainWindow as PureDesktop.Views.MainWindow)?.SaveLayout();
        }
        
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
    }
}

// Extension for rounded rectangle drawing
public static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics g, Brush brush, RectangleF rect, float radius)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
        path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
        path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
        path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }
}
