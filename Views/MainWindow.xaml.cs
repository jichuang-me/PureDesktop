using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using PureDesktop.Core;
using PureDesktop.ViewModels;
using PureDesktop.Helpers;
using System.Windows.Media;
using System.Runtime.InteropServices;

namespace PureDesktop.Views;

public partial class MainWindow : Window
{
    private readonly FenceManager _fenceManager;
    private readonly MainViewModel _viewModel;
    private readonly List<DiskMapper> _diskMappers = new();
    private DesktopWatcher? _desktopWatcher;
    private bool _fencesVisible = true;
    private System.Windows.Threading.DispatcherTimer? _autoHideTimer;
    private DateTime _lastInteractionTime = DateTime.Now;
    private MouseHook? _mouseHook;
    private uint _wmTaskbarCreated;

    public MainWindow()
    {
        InitializeComponent();

        _fenceManager = new FenceManager();
        _viewModel = new MainViewModel();

        // Message for Explorer Restart
        _wmTaskbarCreated = PureDesktop.Helpers.Win32Api.RegisterWindowMessage("TaskbarCreated");

        DataContext = _viewModel;

        try {
            string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app_icon.ico");
            if (System.IO.File.Exists(iconPath))
                this.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri(iconPath));
        } catch {}
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Cover entire virtual screen for multi-monitor support
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;


        UpdateRecycleBinIcon();
        // Ensure taskbar hiding
        ShowInTaskbar = false;
        /* Removed managed secondary window ownership to prevent WPF Z-order interference 
           with the native WorkerW parenting. */

        // Load saved fences
        _fenceManager.Load();

        if (_fenceManager.Settings.Fences.Count == 0)
        {
            AutoOrganize();
        }
        else
        {
            _viewModel.LoadFromSettings(_fenceManager.Settings);
            StartDiskMappers();
        }

        // Start desktop watcher
        StartDesktopWatcher();

        // Hide desktop icons (keep wallpaper visible)
        HideDesktopIcons();

        // Show our own Recycle Bin icon
        InitRecycleBinIcon();

        // Apply saved accent
        ApplySavedAccent();

        // Start auto-hide timer
        StartAutoHideTimer();

        // Register shell menu
        PureDesktop.Core.ShellMenuHelper.Register();

        // Start mouse hook for desktop double-click
        // Start mouse hook for desktop double-click
        StartMouseHook();

        // Hook WndProc for Explorer Restart
        var source = System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        source.AddHook(WndProc);

        // Embed into desktop
        EmbedToDesktop();

        // Handle arguments (if launched from shell menu)
        HandleCommandLineArgs();

        // Sync Desktop Context Menu
        SyncContextMenus();
    }

    private void StartMouseHook()
    {
        _mouseHook = new PureDesktop.Helpers.MouseHook();
        _mouseHook.OnDesktopDoubleClick += () =>
        {
            Dispatcher.Invoke(() => OnToggleFencesClick(this, new RoutedEventArgs()));
        };
        _mouseHook.Start();
    }

    private void HandleCommandLineArgs()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Contains("--organize")) AutoOrganize();
        if (args.Contains("--exit")) Application.Current.Shutdown();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_WINDOWPOSCHANGING = 0x0046;
        const int WM_DISPLAYCHANGE = 0x007E;
        const int WM_SETTINGCHANGE = 0x001A;

        if (msg == _wmTaskbarCreated || msg == WM_DISPLAYCHANGE || msg == WM_SETTINGCHANGE)
        {
            // Explorer restarted or resolution/wallpaper changed, re-embed and fix Z-order
            EmbedToDesktop();
        }
        else if (msg == WM_WINDOWPOSCHANGING)
        {
            // Glue Logic: Force HWND_TOP relative to the parent (WorkerW)
            // This prevents "sliding behind wallpaper" and Win+D minimized states.
            var pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
            pos.hwndInsertAfter = Helpers.Win32Api.HWND_TOP;
            pos.flags &= ~(uint)Helpers.Win32Api.SWP_NOZORDER;
            
            // Critical: Ensure it's not being hidden/minimized by system actions (Win+D)
            pos.flags &= ~(uint)Helpers.Win32Api.SWP_HIDEWINDOW;
            pos.flags |= Helpers.Win32Api.SWP_SHOWWINDOW;
            
            Marshal.StructureToPtr(pos, lParam, fDeleteOld: false);
        }
        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPOS {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public uint flags;
    }

    /// <summary>
    /// Embed this window into the desktop layer.
    /// </summary>
    private void EmbedToDesktop()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            // Initial attempt
            DesktopEmbedder.Embed(this);

            // Robust: Recurring Check (Every 2s) to handle wallpaper/resolution changes
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (s, e) => { RefreshZOrder(); };
            timer.Start();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Forces the window to the correct Z-order within the desktop environment.
    /// Called periodically and during drags.
    /// </summary>
    public void RefreshZOrder()
    {
        if (Application.Current?.MainWindow == null) return;

        // Try to re-embed if parent was lost/changed (e.g. desktop switching)
        if (DesktopEmbedder.Embed(this)) return;

        // If failed (no WorkerW?), force Bottom Z-Order as fallback
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        PureDesktop.Helpers.Win32Api.SetWindowPos(hwnd, new IntPtr(1), 0, 0, 0, 0, 
            PureDesktop.Helpers.Win32Api.SWP_NOMOVE | PureDesktop.Helpers.Win32Api.SWP_NOSIZE | 
            PureDesktop.Helpers.Win32Api.SWP_NOACTIVATE | PureDesktop.Helpers.Win32Api.SWP_NOOWNERZORDER);
    }

    // ─── Double-click desktop to toggle fences ────────────────────

    // Removed OnDesktopDoubleClick as MouseHook handles it now.

    public void ToggleFences()
    {
        _fencesVisible = !_fencesVisible;
        TriggerVisibilityAnimation(_fencesVisible ? 1.0 : 0.0);
        if (RecycleBinIcon != null)
            RecycleBinIcon.IsHitTestVisible = _fencesVisible;
    }

    private void OnToggleFencesClick(object sender, RoutedEventArgs e)
    {
        ToggleFences();
    }

    private void TriggerVisibilityAnimation(double target)
    {
        var anim = new DoubleAnimation(target, TimeSpan.FromMilliseconds(250))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        FenceHost.BeginAnimation(UIElement.OpacityProperty, anim);
        RecycleBinIcon.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    // ─── Auto-Hide Logic ──────────────────────────────────────

    private void StartAutoHideTimer()
    {
        _autoHideTimer = new System.Windows.Threading.DispatcherTimer();
        _autoHideTimer.Interval = TimeSpan.FromMilliseconds(500);
        _autoHideTimer.Tick += (s, e) =>
        {
            if (!_fenceManager.Settings.EnableAutoHide || !_fencesVisible) return;

            var idleTime = DateTime.Now - _lastInteractionTime;
            double target = idleTime.TotalSeconds > _fenceManager.Settings.AutoHideSeconds ? 0.0 : 1.0;

            if (Math.Abs(FenceHost.Opacity - target) > 0.01)
            {
                TriggerVisibilityAnimation(target);
            }
        };
        _autoHideTimer.Start();
    }

    private void OnGlobalMouseMove(object sender, MouseEventArgs e)
    {
        _lastInteractionTime = DateTime.Now;
    }

    private void ApplySavedAccent()
    {
        string hex = _fenceManager.Settings.AccentColor;
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            Application.Current.Resources["AccentBrush"] = new System.Windows.Media.SolidColorBrush(color);
        }
        catch { }
    }

    // ─── Desktop icon hiding ─────────────────────────────────

    private bool _iconsWereVisible = true;

    private void HideDesktopIcons()
    {
        var listView = GetDesktopListView();
        if (listView == IntPtr.Zero) return;

        // Check if currently visible
        int style = Helpers.Win32Api.GetWindowLong(listView, Helpers.Win32Api.GWL_STYLE);
        _iconsWereVisible = (style & 0x10000000 /* WS_VISIBLE */) != 0;

        if (_iconsWereVisible)
        {
            Helpers.Win32Api.ShowWindow(listView, Helpers.Win32Api.SW_HIDE);
        }
    }

    public void RestoreDesktopIcons()
    {
        // Force restore regardless of previous state
        var listView = GetDesktopListView();
        if (listView != IntPtr.Zero)
        {
            Helpers.Win32Api.ShowWindow(listView, Helpers.Win32Api.SW_SHOW);
        }
    }

    // ─── Recycle Bin icon ─────────────────────────────────────────

    [System.Runtime.InteropServices.DllImport("shell32.dll")]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);
    private const uint SHERB_NOCONFIRMATION = 0x00000001;
    private const uint SHERB_NOPROGRESSUI = 0x00000002;

    private void InitRecycleBinIcon()
    {
        try
        {
            UpdateRecycleBinIcon();

            // Position at bottom-left of PRIMARY monitor
            // Canvas coordinates are relative to VirtualScreenLeft/Top
            double primaryLeft = 0 - SystemParameters.VirtualScreenLeft;
            double primaryBottom = SystemParameters.PrimaryScreenHeight - SystemParameters.VirtualScreenTop;

            Canvas.SetLeft(RecycleBinIcon, primaryLeft + 20);
            Canvas.SetTop(RecycleBinIcon, primaryBottom - 110);

            // Set localized label
            RecycleBinLabel.Text = Application.Current.TryFindResource("Recycle_Bin") as string ?? "回收站";
        }
        catch { }
    }

    private void UpdateRecycleBinIcon()
    {
        try
        {
            // Load recycle bin icon from shell
            var recyclePath = @"::{645FF040-5081-101B-9F08-00AA002F954E}";
            RecycleBinImage.Source = Helpers.IconHelper.GetIcon(recyclePath);
        }
        catch { }
    }

    private void OnRecycleBinClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            OnRecycleBinOpen(sender, e);
            e.Handled = true;
        }
    }

    private void OnRecycleBinOpen(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "shell:RecycleBinFolder",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void OnRecycleBinEmpty(object sender, RoutedEventArgs e)
    {
        string msg = Application.Current.TryFindResource("Recycle_ConfirmEmpty") as string
            ?? "确定清空回收站？";
        if (MessageBox.Show(msg, "PureDesktop", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOPROGRESSUI);
        }
        catch { }
    }

    /// <summary>
    /// Find the SysListView32 inside SHELLDLL_DefView (the desktop icon list).
    /// </summary>
    private static IntPtr GetDesktopListView()
    {
        IntPtr progman = Helpers.Win32Api.FindWindow("Progman", null);
        if (progman == IntPtr.Zero) return IntPtr.Zero;

        IntPtr defView = Helpers.Win32Api.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);

        if (defView == IntPtr.Zero)
        {
            // SHELLDLL_DefView might be inside a WorkerW
            Helpers.Win32Api.EnumWindows((hWnd, _) =>
            {
                IntPtr child = Helpers.Win32Api.FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (child != IntPtr.Zero) { defView = child; return false; }
                return true;
            }, IntPtr.Zero);
        }

        if (defView == IntPtr.Zero) return IntPtr.Zero;
        return Helpers.Win32Api.FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
    }

    // ─── Smart auto-organize (preserves manual placements) ────────

    /// <summary>
    /// Smart auto-organize: preserve files already in any fence,
    /// only classify new/unassigned files from desktop.
    /// </summary>
    public void AutoOrganize()
    {
        // Collect all file paths already assigned to any fence
        var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fence in _fenceManager.Settings.Fences)
        {
            foreach (var item in fence.Items)
            {
                if (!string.IsNullOrEmpty(item.FullPath))
                    assigned.Add(item.FullPath);
            }
        }

        // Classify only unassigned files
        var newFences = FileClassifier.ClassifyDesktopFiles(_fenceManager.Settings);

        if (_fenceManager.Settings.Fences.Count == 0)
        {
            // First run: use all classified fences
            _fenceManager.Settings.Fences.AddRange(newFences);
        }
        else
        {
            // Subsequent runs: add only new files to matching existing fences
            foreach (var newFence in newFences)
            {
                foreach (var item in newFence.Items)
                {
                    if (assigned.Contains(item.FullPath)) continue;

                    // Find existing fence with same category
                    var existing = _fenceManager.Settings.Fences
                        .FirstOrDefault(f => f.Category == newFence.Category);

                    if (existing != null)
                    {
                        existing.Items.Add(item);
                    }
                    else
                    {
                        // Create new fence for this category
                        var fence = new Models.Fence
                        {
                            Title = newFence.Title,
                            Category = newFence.Category,
                            X = newFence.X,
                            Y = newFence.Y,
                            Width = newFence.Width,
                            Height = newFence.Height,
                            Items = new List<Models.FenceItem> { item },
                            ViewMode = "list"
                        };
                        _fenceManager.Settings.Fences.Add(fence);
                    }
                }
            }
        }

        _fenceManager.Save();
        _viewModel.LoadFromSettings(_fenceManager.Settings);
        StartDiskMappers();
    }

    // ─── New fence / context menu ─────────────────────────────────

    private void OnNewFenceClick(object sender, RoutedEventArgs e)
    {
        string title = Application.Current.TryFindResource("Dlg_NewFenceTitle") as string ?? "New Fence";
        string prompt = Application.Current.TryFindResource("Dlg_InputName") as string ?? "Enter name:";
        string? result = ShowInputDialog(title, prompt, title);

        if (result != null)
        {
            double x = 100 + _fenceManager.Settings.Fences.Count * 20;
            double y = 100 + _fenceManager.Settings.Fences.Count * 20;

            var fence = _fenceManager.AddFence(result, x, y);
            _viewModel.Fences.Add(new FenceViewModel(fence));
        }
    }

    private void OnAutoOrganizeClick(object sender, RoutedEventArgs e)
    {
        AutoOrganize();
        // Force refresh all fences
        foreach (var fence in _viewModel.Fences)
        {
            fence.RefreshItems();
        }
    }

    private void OnAddMappedFenceClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog();
        dialog.Description = Application.Current.TryFindResource("Dlg_SelectFolder") as string ?? "Select Folder";
        
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            AddMappedFence(dialog.SelectedPath);
        }
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        foreach (var fence in _viewModel.Fences)
        {
            fence.RefreshItems();
        }
    }

    private void SyncContextMenus()
    {
        if (Application.Current is not App app) return;

        // Opacity
        double currentOpacity = _fenceManager.Settings.FenceOpacity;
        GlobalOpacitySlider.Value = currentOpacity;
        OpacityValueText.Text = $"{currentOpacity:P0}";

        AccentMenuMain.Header = Application.Current.TryFindResource("Tray_AccentColor") as string ?? "Accent Color";
        
        // If we have a custom interactive item (StackPanel), don't clear it.
        // We only clear if we are using the simple MenuItem list.
        // Actually, let's just clear the dynamic list IF it exists.
        // Better: Let's remove any MenuItem that isn't our custom UI.
        for (int i = AccentMenuMain.Items.Count - 1; i >= 0; i--)
        {
            if (AccentMenuMain.Items[i] is MenuItem) AccentMenuMain.Items.RemoveAt(i);
        }

        string currentAccent = _fenceManager.Settings.AccentColor;
        // Optionally add back individual colors if desired, 
        // but the user wants the "gradient track", so let's keep it minimal.
        // If the user wants specific clickable presets too, we can add them here.
        // For now, let's just ensure the gradient preview restoration works.

        // Behavior
        BehaviorMenuMain.Items.Clear();
        bool isAutoStart = IsAutoStartEnabled();
        var autoStartMi = new MenuItem { Header = Application.Current.TryFindResource("Tray_AutoStart") as string, IsCheckable = true, IsChecked = isAutoStart };
        autoStartMi.Click += (s, e) => SetAutoStartFromMain(!isAutoStart);
        
        var autoHideMi = new MenuItem { Header = Application.Current.TryFindResource("Tray_AutoHide") as string, IsCheckable = true, IsChecked = _fenceManager.Settings.EnableAutoHide };
        autoHideMi.Click += (s, e) => {
            _fenceManager.Load();
            _fenceManager.Settings.EnableAutoHide = !_fenceManager.Settings.EnableAutoHide;
            _fenceManager.Save();
            SyncContextMenus();
        };
        BehaviorMenuMain.Items.Add(autoStartMi);
        BehaviorMenuMain.Items.Add(autoHideMi);

        // Theme
        ThemeMenuMain.Items.Clear();
        string currentTheme = _fenceManager.Settings.ThemeMode;
        var themes = new[] { "system", "light", "dark" };
        foreach (var t in themes)
        {
            var resKey = t == "system" ? "Tray_ThemeSystem" : (t == "light" ? "Tray_ThemeLight" : "Tray_ThemeDark");
            var mi = new MenuItem { Header = Application.Current.TryFindResource(resKey) as string, IsCheckable = true, IsChecked = currentTheme == t };
            mi.Click += (s, e) => app.SwitchTheme(t);
            ThemeMenuMain.Items.Add(mi);
        }

        // Language
        LangMenuMain.Items.Clear();
        var langs = new[] { ("en-US", "English"), ("zh-CN", "简体中文") };
        string currentLang = _fenceManager.Settings.Language;
        if (string.IsNullOrEmpty(currentLang)) currentLang = "zh-CN";

        foreach (var (code, label) in langs)
        {
             var mi = new MenuItem { Header = label, IsCheckable = true, IsChecked = currentLang == code };
             mi.Click += (s, e) => app.SwitchLanguage(code);
             LangMenuMain.Items.Add(mi);
        }

        // Settings Group
        SettingsMenuMain.Header = Application.Current.TryFindResource("Tray_GroupSettings") as string ?? "Settings";
        ShowTrayIconMenu.IsChecked = _fenceManager.Settings.ShowTrayIcon;
    }

    private void OnToggleTrayIconClick(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app)
        {
            _fenceManager.Settings.ShowTrayIcon = ShowTrayIconMenu.IsChecked;
            _fenceManager.Save();
            app.SetTrayIconVisible(_fenceManager.Settings.ShowTrayIcon);
        }
    }

    private bool IsAutoStartEnabled()
    {
        try {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
            return key?.GetValue("PureDesktop") != null;
        } catch { return false; }
    }

    private void SetAutoStartFromMain(bool enable)
    {
        try {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;
            if (enable) {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                key.SetValue("PureDesktop", $"\"{exePath}\"");
            } else {
                key.DeleteValue("PureDesktop", false);
            }
        } catch { }
        SyncContextMenus();
    }

    private void OnRestartClick(object sender, RoutedEventArgs e)
    {
        string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (exePath != null)
        {
            Process.Start(exePath);
            Application.Current.Shutdown();
        }
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void OnExclusionsClick(object sender, RoutedEventArgs e)
    {
        var win = new ExclusionsWindow(_fenceManager);
        win.ShowDialog();
    }

    private void OnManualClick(object sender, RoutedEventArgs e)
    {
        string manual = Application.Current.TryFindResource("Tray_Manual") as string ?? "Manual";
        string manualMsg = "PureDesktop 使用指南\n" +
                           "1. 整理桌面：右键点击空白处 -> 栅格 -> 自动整理\n" +
                           "2. 创建栅格：右键点击空白处 -> 栅格 -> 添加拖拽栅格\n" +
                           "3. 外观设计：右键点击空白处 -> 个性化 -> 悬停滑动调节透明度/色彩\n" +
                           "4. 系统设置：右键点击空白处 -> 系统设置 -> 托盘图标开关\n" +
                           "5. 退出程序：右键点击空白处 -> 退出";
        MessageBox.Show(manualMsg, manual, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        string copyright = Application.Current.TryFindResource("Copyright") as string ?? "Copyright © 2026 jichuang";
        string aboutMsg = $"PureDesktop v1.2.0\nProfessional Edition\n\nA modern, high-performance desktop organizer.\n{copyright}";
        MessageBox.Show(aboutMsg, "About PureDesktop", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnGlobalOpacitySliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityValueText == null || _viewModel == null || _fenceManager == null) return;
        
        double val = e.NewValue;
        OpacityValueText.Text = $"{val:P0}";
        
        // Preview only (Real-time update)
        _viewModel.FenceOpacity = val;
    }

    private void OnGlobalOpacitySliderMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel == null || _fenceManager == null) return;
        
        // Commit Setting
        _fenceManager.Settings.FenceOpacity = _viewModel.FenceOpacity;
        _fenceManager.Save();
    }

    private void OnOpacityMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
            var pos = e.GetPosition(fe);
            double percent = Math.Clamp(pos.X / fe.ActualWidth, 0.1, 1.0);
            GlobalOpacitySlider.Value = percent;
        }
    }

    private void OnPaletteColorPreview(object sender, MouseEventArgs e)
    {
        if (Application.Current is not App app) return;
        if (sender is FrameworkElement fe && fe.Tag is string hex)
        {
            app.ApplyAccent(hex, true); // Preview
        }
    }

    private void OnPaletteColorCommit(object sender, MouseButtonEventArgs e)
    {
        if (Application.Current is not App app) return;
        if (sender is FrameworkElement fe && fe.Tag is string hex)
        {
            app.ApplyAccent(hex, false); // Commit
            _fenceManager.Settings.AccentColor = hex;
            _fenceManager.Save();
        }
    }

    private void OnPaletteMouseLeave(object sender, MouseEventArgs e)
    {
        if (Application.Current is App app)
        {
            app.ApplyAccent(_fenceManager.Settings.AccentColor, true); // Restore actual setting
        }
    }



    // ─── Fence management ─────────────────────────────────────────

    public void RemoveFence(FenceViewModel fenceVm)
    {
        _viewModel.Fences.Remove(fenceVm);
        _fenceManager.RemoveFence(fenceVm.Model.Id);
    }

    public void AddMappedFence(string folderPath)
    {
        string title = System.IO.Path.GetFileName(folderPath);
        if (string.IsNullOrEmpty(title)) title = folderPath;

        double x = 20 + _fenceManager.Settings.Fences.Count * 20;
        double y = 20 + _fenceManager.Settings.Fences.Count * 20;

        var fence = _fenceManager.AddMappedFence(title, folderPath, x, y);
        var fenceVm = new FenceViewModel(fence);
        _viewModel.Fences.Add(fenceVm);

        var mapper = new DiskMapper(fence, _fenceManager, () => fenceVm.RefreshItems());
        mapper.Start();
        _diskMappers.Add(mapper);
    }

    public void SaveLayout()
    {
        _fenceManager.Save();
    }

    // ─── Disk mappers / Desktop watcher ───────────────────────────

    private void StartDiskMappers()
    {
        foreach (var mapper in _diskMappers) mapper.Dispose();
        _diskMappers.Clear();

        foreach (var fenceVm in _viewModel.Fences)
        {
            if (fenceVm.IsMapped)
            {
                var mapper = new DiskMapper(fenceVm.Model, _fenceManager, () => fenceVm.RefreshItems());
                mapper.Start();
                _diskMappers.Add(mapper);
            }
        }
    }


    private void StartDesktopWatcher()
    {
        _desktopWatcher?.Dispose();
        _desktopWatcher = new DesktopWatcher(_fenceManager);
        
        _desktopWatcher.FileAdded += (category, item) =>
        {
            Dispatcher.Invoke(() =>
            {
                var targetVm = _viewModel.Fences.FirstOrDefault(f => f.Model.Category == category);
                if (targetVm != null)
                {
                    if (!targetVm.Model.Items.Any(i => i.FullPath == item.FullPath))
                    {
                        targetVm.AddItem(item);
                        _fenceManager.Save();
                    }
                }
            });
        };

        _desktopWatcher.FileRemoved += (_, fullPath) =>
        {
            Dispatcher.Invoke(() =>
            {
                foreach (var fence in _viewModel.Fences)
                {
                    var item = fence.Items.FirstOrDefault(i => i.FullPath.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
                    if (item != null)
                    {
                        fence.Items.Remove(item);
                        fence.Model.Items.Remove(item.Model);
                        _fenceManager.Save();
                        // Update status bar if this fence is active? 
                        // Actually FenceControl needs to know. 
                        // FenceViewModel.Items collection changed event should handle UI trigger if bound.
                        // But FenceControl might listen to CollectionChanged to update status bar?
                        // Currently FenceControl.UpdateStatusBar is manual. 
                        // I should add CollectionChanged listener in FenceControl later or just accept it updates on next refresh.
                        // Or better: trigger a refresh or specific property change.
                        // For now, let's trust bindings.
                        break; // Assume 1:1 mapping
                    }
                }
            });
        };

        _desktopWatcher.FileRenamed += (_, oldPath, newPath) =>
        {
            Dispatcher.Invoke(() =>
            {
                // Remove old
                foreach (var fence in _viewModel.Fences)
                {
                    var item = fence.Items.FirstOrDefault(i => i.FullPath.Equals(oldPath, StringComparison.OrdinalIgnoreCase));
                    if (item != null)
                    {
                        fence.Items.Remove(item);
                        fence.Model.Items.Remove(item.Model);
                        _fenceManager.Save();
                        break;
                    }
                }

                // Add new (re-classify)
                try {
                   // We need to re-classify manually since DesktopWatcher only provided event,
                   // but OnCreated logic in DesktopWatcher already did classification? 
                   // No, OnRenamed in DesktopWatcher just passed paths.
                   // We need to trigger classification here or simulate "Created".
                   // Actually, usually a Rename might be treated as Removed + Created by watcher depending on implementation,
                   // but we handled Renamed explicitly.
                   // Let's manually classify.
                   
                   // Check if file still exists (Rename might be rapid)
                   if (System.IO.File.Exists(newPath) || System.IO.Directory.Exists(newPath))
                   {
                        string name = System.IO.Path.GetFileName(newPath);
                        string ext = System.IO.Path.GetExtension(newPath);
                        bool isDir = System.IO.Directory.Exists(newPath);
                        string category = Core.FileClassifier.ClassifySingleFile(newPath, _fenceManager.Settings);
                        
                        var newItem = new Models.FenceItem
                        {
                            Name = name,
                            FullPath = newPath,
                            Extension = ext,
                            IsDirectory = isDir
                        };
                        
                        var targetVm = _viewModel.Fences.FirstOrDefault(f => f.Model.Category == category);
                        if (targetVm != null)
                        {
                            targetVm.AddItem(newItem);
                            _fenceManager.Save();
                        }
                   }
                } catch { }
            });
        };

        _desktopWatcher.Start();
    }

    // ─── Input dialog ─────────────────────────────────────────────

    private static string? ShowInputDialog(string title, string prompt, string defaultValue)
    {
        var dlg = new Window
        {
            Title = title,
            Width = 340,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            WindowStyle = WindowStyle.ToolWindow,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize
        };

        if (Application.Current is App app && app.GetType().GetField("_hiddenOwner", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(app) is Window owner)
        {
            dlg.Owner = owner;
        }

        var sp = new System.Windows.Controls.StackPanel { Margin = new Thickness(16) };
        sp.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = prompt,
            Margin = new Thickness(0, 0, 0, 8),
            FontSize = 13
        });

        var tb = new System.Windows.Controls.TextBox
        {
            Text = defaultValue,
            FontSize = 13,
            Padding = new Thickness(6, 4, 6, 4)
        };
        tb.SelectAll();
        sp.Children.Add(tb);

        var btnPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };

        string okText = Application.Current.TryFindResource("Dlg_OK") as string ?? "OK";
        string cancelText = Application.Current.TryFindResource("Dlg_Cancel") as string ?? "Cancel";

        var okBtn = new System.Windows.Controls.Button
        {
            Content = okText,
            Width = 72,
            Height = 28,
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true
        };
        okBtn.Click += (s, e) => { dlg.DialogResult = true; dlg.Close(); };

        var cancelBtn = new System.Windows.Controls.Button
        {
            Content = cancelText,
            Width = 72,
            Height = 28,
            IsCancel = true
        };

        btnPanel.Children.Add(okBtn);
        btnPanel.Children.Add(cancelBtn);
        sp.Children.Add(btnPanel);
        dlg.Content = sp;

        return dlg.ShowDialog() == true ? tb.Text : null;
    }

    protected override void OnClosed(EventArgs e)
    {
        foreach (var mapper in _diskMappers) mapper.Dispose();
        _desktopWatcher?.Dispose();

        // Restore desktop icons
        RestoreDesktopIcons();

        _mouseHook?.Dispose();

        DesktopEmbedder.Detach(this);
        base.OnClosed(e);
    }
}