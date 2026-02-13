using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
using PureDesktop.Models;
using PureDesktop.ViewModels;



namespace PureDesktop.Views;

public partial class FenceControl : System.Windows.Controls.UserControl
{
    private System.Windows.Point _dragStartPoint;
    private bool _isDragging;
    private FenceItemViewModel? _dragItem;

    private const double MinFenceWidth = 160;
    private const double MinFenceHeight = 100;
    private const double CollapsedHeight = 30;
    private double _expandedHeight;

    public FenceControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is FenceViewModel vm)
        {
            ApplyViewMode(vm);
            UpdateStatusBar();
        }
    }

    // ─── View Mode ────────────────────────────────────────────────



    private void ApplyViewMode(FenceViewModel vm)
    {
        bool isList = vm.ViewMode == "list";
        FenceListBox.Visibility = isList ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        FenceGridBox.Visibility = isList ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        ViewModeBtn.Content = isList ? "☰" : "⊞";
        ViewModeBtn.ToolTip = System.Windows.Application.Current.TryFindResource(
            isList ? "Fence_ViewGrid" : "Fence_ViewList") as string;
    }

    private void OnViewModeClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is FenceViewModel vm)
        {
            vm.ToggleViewMode();
            ApplyViewMode(vm);
            (Window.GetWindow(this) as MainWindow)?.SaveLayout();
        }
    }

    // ─── Hover reveal ────────────────────────────────────────────

    private void OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        AnimateOpacity(TitleBar, 1.0, 150);
        AnimateOpacity(ResizeRight, 1.0, 150);
        AnimateOpacity(ResizeBottom, 1.0, 150);
        AnimateOpacity(ResizeCorner, 1.0, 150);
        AnimateOpacity(ResizeLeft, 1.0, 150);
        AnimateOpacity(ResizeTop, 1.0, 150);
    }

    private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        AnimateOpacity(TitleBar, 0.0, 300);
        AnimateOpacity(ResizeRight, 0.0, 300);
        AnimateOpacity(ResizeBottom, 0.0, 300);
        AnimateOpacity(ResizeCorner, 0.0, 300);
        AnimateOpacity(ResizeLeft, 0.0, 300);
        AnimateOpacity(ResizeTop, 0.0, 300);
    }

    private static void AnimateOpacity(UIElement element, double to, int durationMs)
    {
        var animation = new DoubleAnimation(to, TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        element.BeginAnimation(System.Windows.UIElement.OpacityProperty, animation);
    }

    // ─── Title bar drag (move fence) ─────────────────────────────

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is FenceViewModel { IsLocked: true }) return;
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _dragStartPoint = e.GetPosition(this.Parent as UIElement);
            _isDragging = true;
            TitleBar.CaptureMouse();
            TitleBar.MouseMove += OnTitleBarMouseMove;
            TitleBar.MouseLeftButtonUp += OnTitleBarMouseUp;
        }
    }

    // ─── Inline rename ───────────────────────────────────────────

    private void OnTitleDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            e.Handled = true;
            BeginInlineRename();
        }
    }

    private void BeginInlineRename()
    {
        if (DataContext is not FenceViewModel vm) return;
        TitleText.Visibility = System.Windows.Visibility.Collapsed;
        TitleEditBox.Text = vm.Model.Title;
        TitleEditBox.Visibility = System.Windows.Visibility.Visible;
        TitleEditBox.Focus();
        TitleEditBox.SelectAll();
    }

    private void CommitRename()
    {
        if (DataContext is not FenceViewModel vm) return;
        string newTitle = TitleEditBox.Text.Trim();
        if (!string.IsNullOrEmpty(newTitle))
        {
            vm.Model.Title = newTitle;
            vm.RefreshItems();
            (Window.GetWindow(this) as MainWindow)?.SaveLayout();
        }
        TitleEditBox.Visibility = System.Windows.Visibility.Collapsed;
        TitleText.Visibility = System.Windows.Visibility.Visible;
    }

    private void OnTitleEditLostFocus(object sender, RoutedEventArgs e) => CommitRename();

    private void OnTitleEditKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)  { CommitRename(); e.Handled = true; }
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            TitleEditBox.Visibility = System.Windows.Visibility.Collapsed;
            TitleText.Visibility = System.Windows.Visibility.Visible;
            e.Handled = true;
        }
    }

    private void OnTitleBarMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging) return;

        var current = e.GetPosition(this.Parent as UIElement);
        var vm = DataContext as FenceViewModel;
        if (vm == null) return;

        double dx = current.X - _dragStartPoint.X;
        double dy = current.Y - _dragStartPoint.Y;

        double newX = vm.X + dx;
        double newY = vm.Y + dy;
        double w = vm.Width;
        double h = vm.Height;

        ApplySnapping(ref newX, ref newY, ref w, ref h, false);

        vm.X = newX;
        vm.Y = newY;

        Canvas.SetLeft(this, vm.X);
        Canvas.SetTop(this, vm.Y);

        // Force Z-order during drag to stay in the desktop layer
        (Window.GetWindow(this) as MainWindow)?.RefreshZOrder();

        _dragStartPoint = current;
    }

    private void OnTitleBarMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        TitleBar.ReleaseMouseCapture();
        TitleBar.MouseMove -= OnTitleBarMouseMove;
        TitleBar.MouseLeftButtonUp -= OnTitleBarMouseUp;

        (Window.GetWindow(this) as MainWindow)?.SaveLayout();
    }

    // ─── Resize ──────────────────────────────────────────────────

    private void OnResizeRight(object sender, DragDeltaEventArgs e)
    {
        if (DataContext is FenceViewModel { IsLocked: true }) return;
        if (DataContext is FenceViewModel vm)
        {
            double newW = vm.Width + e.HorizontalChange;
            if (newW >= MinFenceWidth)
            {
                double x = vm.X;
                double y = vm.Y;
                double h = vm.Height;
                ApplySnapping(ref x, ref y, ref newW, ref h, true);
                vm.Width = newW;
            }
        }
    }

    private void OnResizeBottom(object sender, DragDeltaEventArgs e)
    {
        if (DataContext is FenceViewModel { IsLocked: true }) return;
        if (DataContext is FenceViewModel vm)
        {
            double newH = vm.Height + e.VerticalChange;
            if (newH >= MinFenceHeight)
            {
                double x = vm.X;
                double y = vm.Y;
                double w = vm.Width;
                ApplySnapping(ref x, ref y, ref w, ref newH, true);
                vm.Height = newH;
            }
        }
    }

    private void OnResizeCorner(object sender, DragDeltaEventArgs e)
    {
        if (DataContext is FenceViewModel { IsLocked: true }) return;
        if (DataContext is FenceViewModel vm)
        {
            double newW = vm.Width + e.HorizontalChange;
            double newH = vm.Height + e.VerticalChange;
            if (newW >= MinFenceWidth && newH >= MinFenceHeight)
            {
                double x = vm.X;
                double y = vm.Y;
                ApplySnapping(ref x, ref y, ref newW, ref newH, true);
                vm.Width = newW;
                vm.Height = newH;
            }
        }
    }

    private void OnResizeLeft(object sender, DragDeltaEventArgs e)
    {
        if (DataContext is FenceViewModel { IsLocked: true }) return;
        if (DataContext is FenceViewModel vm)
        {
            double newW = vm.Width - e.HorizontalChange;
            double newX = vm.X + e.HorizontalChange;
            if (newW >= MinFenceWidth)
            {
                double y = vm.Y;
                double h = vm.Height;
                ApplySnapping(ref newX, ref y, ref newW, ref h, true);
                vm.Width = newW;
                vm.X = newX;
                Canvas.SetLeft(this, vm.X);
            }
        }
    }

    private void OnResizeTop(object sender, DragDeltaEventArgs e)
    {
        if (DataContext is FenceViewModel { IsLocked: true }) return;
        if (DataContext is FenceViewModel vm)
        {
            double newH = vm.Height - e.VerticalChange;
            double newY = vm.Y + e.VerticalChange;
            if (newH >= MinFenceHeight)
            {
                double x = vm.X;
                double w = vm.Width;
                ApplySnapping(ref x, ref newY, ref w, ref newH, true);
                vm.Height = newH;
                vm.Y = newY;
                Canvas.SetTop(this, vm.Y);
            }
        }
    }

    private void OnResizeCompleted(object sender, DragCompletedEventArgs e)
    {
        (Window.GetWindow(this) as MainWindow)?.SaveLayout();
    }

    // ─── Collapse/Expand ─────────────────────────────────────────

    private void OnCollapseClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is FenceViewModel vm)
        {
            vm.IsCollapsed = !vm.IsCollapsed;

            if (vm.IsCollapsed)
            {
                // Save current height, then shrink to title-bar only
                _expandedHeight = vm.Height;
                vm.Height = CollapsedHeight;
            }
            else
            {
                // Restore original height
                vm.Height = _expandedHeight > CollapsedHeight ? _expandedHeight : 280;
            }

            FenceGridBox.Visibility = vm.IsCollapsed ? System.Windows.Visibility.Collapsed : 
                (vm.IsListView ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible);
            FenceListBox.Visibility = vm.IsCollapsed ? System.Windows.Visibility.Collapsed :
                (vm.IsListView ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed);
            CollapseBtn.Content = vm.IsCollapsed ? "▼" : "━";
            CollapseBtn.ToolTip = System.Windows.Application.Current.TryFindResource(
                vm.IsCollapsed ? "Fence_Expand" : "Fence_Collapse") as string;

            (Window.GetWindow(this) as MainWindow)?.SaveLayout();
        }
    }

    private void OnLockClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is FenceViewModel vm)
        {
            vm.IsLocked = !vm.IsLocked;
            (Window.GetWindow(this) as MainWindow)?.SaveLayout();
        }
    }

    // ─── Sort / Classify ─────────────────────────────────────────

    private void OnSortBtnClick(object sender, RoutedEventArgs e)
    {
        SortMenu.IsOpen = true;
    }

    private void OnClassifyBtnClick(object sender, RoutedEventArgs e)
    {
        ClassifyMenu.IsOpen = true;
    }

    // ── Sorting (within each group if grouped) ──────────────────

    private void OnSortByName(object sender, RoutedEventArgs e)
        => SortWithinGroups((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

    private void OnSortByType(object sender, RoutedEventArgs e)
        => SortWithinGroups((a, b) =>
        {
            int c = string.Compare(a.Model.Extension, b.Model.Extension, StringComparison.OrdinalIgnoreCase);
            return c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

    private void OnSortByDate(object sender, RoutedEventArgs e)
        => SortWithinGroups((a, b) =>
        {
            DateTime da, db;
            try { da = System.IO.File.GetLastWriteTime(a.FullPath); } catch { da = DateTime.MinValue; }
            try { db = System.IO.File.GetLastWriteTime(b.FullPath); } catch { db = DateTime.MinValue; }
            return db.CompareTo(da); // newest first
        });

    /// <summary>
    /// Sort items within each group section.  If no headers exist, sort the entire list.
    /// </summary>
    private void SortWithinGroups(Comparison<FenceItemViewModel> comparison)
    {
        if (DataContext is not FenceViewModel vm) return;

        var items = vm.Items.ToList();
        var result = new List<FenceItemViewModel>();

        // Split into segments: each segment starts with a header (or the first file)
        var segment = new List<FenceItemViewModel>();
        FenceItemViewModel? header = null;

        foreach (var item in items)
        {
            if (item.IsGroupHeader)
            {
                // Flush previous segment
                if (segment.Count > 0 || header != null)
                {
                    if (header != null) result.Add(header);
                    segment.Sort(comparison);
                    result.AddRange(segment);
                    segment.Clear();
                }
                header = item;
            }
            else
            {
                segment.Add(item);
            }
        }
        // Flush last segment
        if (header != null) result.Add(header);
        segment.Sort(comparison);
        result.AddRange(segment);

        // Apply
        vm.Items.Clear();
        vm.Model.Items.Clear();
        foreach (var item in result)
        {
            vm.Items.Add(item);
            vm.Model.Items.Add(item.Model);
        }
        (Window.GetWindow(this) as MainWindow)?.SaveLayout();
    }

    // ── Classify (insert group headers) ─────────────────────────

    // Friendly display names for common extension groups
    private static readonly Dictionary<string, string> TypeLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        { "!dir", "📁 文件夹" },
        { ".doc", "Word 文档" }, { ".docx", "Word 文档" }, { ".rtf", "Word 文档" },
        { ".xls", "Excel 表格" }, { ".xlsx", "Excel 表格" }, { ".csv", "Excel 表格" },
        { ".ppt", "PPT 演示" }, { ".pptx", "PPT 演示" },
        { ".pdf", "PDF" },
        { ".txt", "文本文件" }, { ".md", "文本文件" }, { ".log", "文本文件" },
        { ".jpg", "图片" }, { ".jpeg", "图片" }, { ".png", "图片" },
        { ".gif", "图片" }, { ".bmp", "图片" }, { ".svg", "图片" }, { ".webp", "图片" },
        { ".mp4", "视频" }, { ".avi", "视频" }, { ".mkv", "视频" }, { ".mov", "视频" },
        { ".mp3", "音频" }, { ".wav", "音频" }, { ".flac", "音频" }, { ".aac", "音频" },
        { ".zip", "压缩包" }, { ".rar", "压缩包" }, { ".7z", "压缩包" }, { ".tar", "压缩包" },
        { ".exe", "程序" }, { ".msi", "程序" }, { ".bat", "脚本" }, { ".ps1", "脚本" },
        { ".lnk", "快捷方式" },
    };

    private static string GetTypeLabel(FenceItemViewModel item)
    {
        if (item.IsDirectory) return TypeLabels["!dir"];
        if (TypeLabels.TryGetValue(item.Model.Extension, out var label)) return label;
        return string.IsNullOrEmpty(item.Model.Extension) ? "其他" : $"{item.Model.Extension.TrimStart('.').ToUpper()} 文件";
    }

    private void OnGroupByType(object sender, RoutedEventArgs e)
    {
        if (DataContext is not FenceViewModel vm) return;

        // Strip existing headers
        var realItems = vm.Items.Where(i => !i.IsGroupHeader).ToList();

        // Group by friendly type label
        var groups = realItems
            .GroupBy(i => GetTypeLabel(i))
            .OrderBy(g => g.Key == TypeLabels["!dir"] ? 0 : 1)
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        var result = new List<FenceItemViewModel>();
        foreach (var g in groups)
        {
            // Insert header
            var headerModel = new FenceItem
            {
                Name = g.Key,
                IsGroupHeader = true,
                GroupLabel = g.Key
            };
            result.Add(new FenceItemViewModel(headerModel));
            result.AddRange(g.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase));
        }

        ApplyItems(vm, result);
    }

    private void OnGroupByDate(object sender, RoutedEventArgs e)
    {
        if (DataContext is not FenceViewModel vm) return;

        var realItems = vm.Items.Where(i => !i.IsGroupHeader).ToList();
        var now = DateTime.Now;

        string DateBucket(FenceItemViewModel item)
        {
            try
            {
                var age = now - System.IO.File.GetLastWriteTime(item.FullPath);
                if (age.TotalDays < 1) return "今天";
                if (age.TotalDays < 7) return "近一周";
                if (age.TotalDays < 30) return "近一个月";
                return "更早";
            }
            catch { return "更早"; }
        }

        var bucketOrder = new Dictionary<string, int>
            { ["今天"] = 0, ["近一周"] = 1, ["近一个月"] = 2, ["更早"] = 3 };

        var groups = realItems
            .GroupBy(i => DateBucket(i))
            .OrderBy(g => bucketOrder.GetValueOrDefault(g.Key, 99));

        var result = new List<FenceItemViewModel>();
        foreach (var g in groups)
        {
            var headerModel = new FenceItem
            {
                Name = g.Key,
                IsGroupHeader = true,
                GroupLabel = g.Key
            };
            result.Add(new FenceItemViewModel(headerModel));
            result.AddRange(g.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase));
        }

        ApplyItems(vm, result);
    }

    private void ApplyItems(FenceViewModel vm, List<FenceItemViewModel> items)
    {
        vm.Items.Clear();
        vm.Model.Items.Clear();
        foreach (var item in items)
        {
            vm.Items.Add(item);
            vm.Model.Items.Add(item.Model);
        }
        (Window.GetWindow(this) as MainWindow)?.SaveLayout();
        UpdateStatusBar();
    }

    // ─── Delete ──────────────────────────────────────────────────

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not FenceViewModel vm) return;

        string msg = System.Windows.Application.Current.TryFindResource("Dlg_ConfirmDelete") as string
            ?? "Delete this fence?";
        var result = System.Windows.MessageBox.Show(msg, "PureDesktop",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            var mainWin = Window.GetWindow(this) as MainWindow;
            mainWin?.RemoveFence(vm);
        }
    }

    private static string? ShowInputDialog(string title, string prompt, string defaultValue)
    {
        var dlg = new Window
        {
            Title = title,
            Width = 340,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            WindowStyle = WindowStyle.ToolWindow,
            ResizeMode = ResizeMode.NoResize
        };

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

        string okText = System.Windows.Application.Current.TryFindResource("Dlg_OK") as string ?? "OK";
        string cancelText = System.Windows.Application.Current.TryFindResource("Dlg_Cancel") as string ?? "Cancel";

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

    // ─── Native Context Menu ─────────────────────────────────────

    private void OnItemMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is FenceItemViewModel item)
        {
            if (item.IsGroupHeader) return;

            // Show native shell context menu
            var screenPos = fe.PointToScreen(e.GetPosition(fe));
            Core.ShellContextMenu.Show(Window.GetWindow(this), item.FullPath, screenPos);
            e.Handled = true;
        }
    }

    // ─── Item double-click → open file ───────────────────────────

    private void OnItemMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not FenceViewModel vm) return;

        if (e.ClickCount == 2 && sender is FrameworkElement fe && fe.DataContext is FenceItemViewModel item)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = item.FullPath,
                    UseShellExecute = true
                });
            }
            catch { }
            e.Handled = true;
        }
        else if (e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement feDrag
            && feDrag.DataContext is FenceItemViewModel dragItem)
        {
            _dragItem = dragItem;
            _dragStartPoint = e.GetPosition(this);
            
            // Ensure focus for keyboard shortcuts
            feDrag.Focus();
            var listBox = (vm.IsListView ? FenceListBox : FenceGridBox);
            listBox.Focus();
            listBox.SelectedItem = dragItem;
        }
    }

    // ─── Item drag (between fences) ──────────────────────────────

    private void OnItemMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragItem == null) return;

        var current = e.GetPosition(this);
        if (Math.Abs(current.X - _dragStartPoint.X) < 8 && Math.Abs(current.Y - _dragStartPoint.Y) < 8)
            return;

        var data = new System.Windows.DataObject("FenceItem", _dragItem);
        data.SetData("SourceFence", DataContext);
        DragDrop.DoDragDrop(this, data, System.Windows.DragDropEffects.Move);
        _dragItem = null;
    }

    private void OnDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent("FenceItem") || e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            e.Effects = System.Windows.DragDropEffects.Move;
        else
            e.Effects = System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        // Handle drag from another fence
        if (e.Data.GetData("FenceItem") is FenceItemViewModel item
            && e.Data.GetData("SourceFence") is FenceViewModel source
            && DataContext is FenceViewModel target
            && source != target)
        {
            source.Items.Remove(item);
            target.Items.Add(item);

            source.Model.Items.Remove(item.Model);
            target.Model.Items.Add(item.Model);

            (Window.GetWindow(this) as MainWindow)?.SaveLayout();
            return;
        }

        // Handle drag from Windows Explorer
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            && DataContext is FenceViewModel targetVm)
        {
            var files = e.Data.GetData(System.Windows.DataFormats.FileDrop) as string[];
            if (files == null) return;

            foreach (var filePath in files)
            {
                string name = System.IO.Path.GetFileName(filePath);
                string ext = System.IO.Path.GetExtension(filePath);
                bool isDir = System.IO.Directory.Exists(filePath);

                // Check if already in this fence
                if (targetVm.Model.Items.Any(i => i.FullPath == filePath))
                    continue;

                var newItem = new FenceItem
                {
                    Name = name,
                    FullPath = filePath,
                    Extension = ext,
                    IsDirectory = isDir
                };
                targetVm.AddItem(newItem);
            }

            (Window.GetWindow(this) as MainWindow)?.SaveLayout();
        }
    }

    // ─── Classify: None (remove headers) ───────────────────────

    private void OnGroupNone(object sender, RoutedEventArgs e)
    {
        if (DataContext is not FenceViewModel vm) return;
        var realItems = vm.Items.Where(i => !i.IsGroupHeader).ToList();
        ApplyItems(vm, realItems);
    }

    // ─── Status bar ──────────────────────────────────────────────

    private void UpdateStatusBar()
    {
        if (DataContext is FenceViewModel vm)
        {
            var items = vm.Items.Where(i => !i.IsGroupHeader).ToList();
            int count = items.Count;
            long totalSize = 0;
            foreach (var item in items)
            {
                if (!item.IsDirectory)
                {
                    try
                    {
                        var info = new System.IO.FileInfo(item.FullPath);
                        if (info.Exists) totalSize += info.Length;
                    }
                    catch { }
                }
            }

            string sizeStr = totalSize < 1024 ? $"{totalSize} B"
                : totalSize < 1048576 ? $"{totalSize / 1024.0:F1} KB"
                : totalSize < 1073741824 ? $"{totalSize / 1048576.0:F1} MB"
                : $"{totalSize / 1073741824.0:F2} GB";

            StatusText.Text = $"{count} 个项目  ({sizeStr})";
        }
    }

    // ─── Context menu: Replaced by Native Shell Menu ─────────────
    private void DeleteItem(FenceItemViewModel item)
    {
         if (DataContext is not FenceViewModel vm) return;
         bool shouldRemove = false;
         try
         {
             if (item.IsDirectory)
             {
                 if (System.IO.Directory.Exists(item.FullPath))
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(item.FullPath, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
             }
             else
             {
                 if (System.IO.File.Exists(item.FullPath))
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(item.FullPath, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
             }
             shouldRemove = true;
         }
         catch (Exception ex)
         {
             // If already missing, force remove
             if (ex is System.IO.FileNotFoundException || ex is System.IO.DirectoryNotFoundException)
                 shouldRemove = true;
         }

         // Double check existence
         if (!shouldRemove)
         {
             if (item.IsDirectory && !System.IO.Directory.Exists(item.FullPath)) shouldRemove = true;
             else if (!item.IsDirectory && !System.IO.File.Exists(item.FullPath)) shouldRemove = true;
         }

         if (shouldRemove)
         {
             vm.Items.Remove(item);
             vm.Model.Items.Remove(item.Model);
             UpdateStatusBar();
             (Window.GetWindow(this) as MainWindow)?.SaveLayout();
         }
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var listBox = sender as System.Windows.Controls.ListBox;
        if (listBox == null) return;

        if (e.Key == System.Windows.Input.Key.Delete)
        {
            var selected = listBox.SelectedItems.Cast<FenceItemViewModel>().ToList();
            if (selected.Count > 0)
            {
                foreach(var item in selected)
                {
                    DeleteItem(item);
                }
                e.Handled = true;
            }
        }
        else if (e.Key == System.Windows.Input.Key.F2)
        {
            var item = listBox.SelectedItem as FenceItemViewModel;
            if (item != null)
            {
                item.IsRenaming = true;
                e.Handled = true;
            }
        }
        else if ((e.Key == System.Windows.Input.Key.System || e.Key == System.Windows.Input.Key.Enter) && 
                 (Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) == System.Windows.Input.ModifierKeys.Alt)
        {
            var item = listBox.SelectedItem as FenceItemViewModel;
            if (item != null)
            {
                try
                {
                    var info = new Helpers.Win32Api.SHELLEXECUTEINFO();
                    info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(info);
                    info.lpVerb = "properties";
                    info.lpFile = item.FullPath;
                    info.nShow = Helpers.Win32Api.SW_SHOW;
                    info.fMask = Helpers.Win32Api.SEE_MASK_INVOKEIDLIST;
                    Helpers.Win32Api.ShellExecuteEx(ref info);
                }
                catch { }
                e.Handled = true;
            }
        }
        else if ((Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
        {
            if (e.Key == System.Windows.Input.Key.C)
            {
                var selected = listBox.SelectedItems.Cast<FenceItemViewModel>().Select(i => i.FullPath).ToList();
                if (selected.Count > 0)
                {
                    var files = new System.Collections.Specialized.StringCollection();
                    files.AddRange(selected.ToArray());
                    System.Windows.Clipboard.SetFileDropList(files);
                }
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.X)
            {
                 var selected = listBox.SelectedItems.Cast<FenceItemViewModel>().Select(i => i.FullPath).ToArray();
                 if (selected.Length > 0)
                 {
                     var data = new System.Windows.DataObject();
                     data.SetData(System.Windows.DataFormats.FileDrop, selected);
                     // 1 = Copy, 2 = Move
                     byte[] moveEffect = { 2, 0, 0, 0 };
                     var ms = new System.IO.MemoryStream(moveEffect);
                     data.SetData("Preferred DropEffect", ms);
                     System.Windows.Clipboard.SetDataObject(data, true);
                 }
                 e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.V)
            {
                if (DataContext is FenceViewModel vm && System.Windows.Clipboard.ContainsFileDropList())
                {
                    try
                    {
                        var files = System.Windows.Clipboard.GetFileDropList();
                        string targetDir = vm.Model.MappedFolderPath;
                        if (string.IsNullOrEmpty(targetDir) || !System.IO.Directory.Exists(targetDir))
                        {
                            targetDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                        }

                        foreach (string filePath in files)
                        {
                            if (System.IO.File.Exists(filePath))
                            {
                                string fileName = System.IO.Path.GetFileName(filePath);
                                string destPath = System.IO.Path.Combine(targetDir, fileName);

                                if (System.IO.File.Exists(destPath))
                                {
                                    string name = System.IO.Path.GetFileNameWithoutExtension(fileName);
                                    string ext = System.IO.Path.GetExtension(fileName);
                                    int i = 1;
                                    while (System.IO.File.Exists(destPath))
                                    {
                                        destPath = System.IO.Path.Combine(targetDir, $"{name} ({i++}){ext}");
                                    }
                                }
                                System.IO.File.Copy(filePath, destPath);
                            }
                            else if (System.IO.Directory.Exists(filePath))
                            {
                                string dirName = System.IO.Path.GetFileName(filePath);
                                string destPath = System.IO.Path.Combine(targetDir, dirName);
                                if (System.IO.Directory.Exists(destPath))
                                {
                                     int i = 1;
                                     while (System.IO.Directory.Exists(destPath))
                                     {
                                         destPath = System.IO.Path.Combine(targetDir, $"{dirName} ({i++})");
                                     }
                                }
                                Microsoft.VisualBasic.FileIO.FileSystem.CopyDirectory(filePath, destPath);
                            }
                        }
                        e.Handled = true;
                    }
                    catch { }
                }
            }
        }
    }

    private void OnRenameTextBoxVisible(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue && sender is System.Windows.Controls.TextBox tb)
        {
            tb.Focus();
            tb.SelectAll();
        }
    }

    private void OnRenameTextBoxKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            CommitRename(sender as System.Windows.Controls.TextBox);
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Escape)
        {
            CancelRename(sender as System.Windows.Controls.TextBox);
            e.Handled = true;
        }
    }

    private void OnRenameTextBoxLostFocus(object sender, RoutedEventArgs e)
    {
        CommitRename(sender as System.Windows.Controls.TextBox);
    }

    private void CancelRename(System.Windows.Controls.TextBox? tb)
    {
        if (tb?.DataContext is FenceItemViewModel item)
        {
            item.IsRenaming = false;
        }
    }

    private void CommitRename(System.Windows.Controls.TextBox? tb)
    {
        if (tb == null || tb.DataContext is not FenceItemViewModel item) return;
        
        string newName = tb.Text.Trim();
        if (string.IsNullOrEmpty(newName) || newName == item.Name)
        {
            item.IsRenaming = false;
            return;
        }

        try
        {
            string dir = Path.GetDirectoryName(item.FullPath)!;
            string newPath = Path.Combine(dir, newName);
            
            if (item.IsDirectory)
                Directory.Move(item.FullPath, newPath);
            else
                File.Move(item.FullPath, newPath);

            // Update Model
            item.Model.Name = newName;
            item.Model.FullPath = newPath;
            item.Model.Extension = Path.GetExtension(newName);
            
            // Notify
            item.OnPropertyChanged("Name");
            item.OnPropertyChanged("FullPath");
            item.OnPropertyChanged("Extension");
            
            UpdateStatusBar();
            (Window.GetWindow(this) as MainWindow)?.SaveLayout();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"重命名失败: {ex.Message}", "PureDesktop", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            item.IsRenaming = false;
        }
    }

    // ─── Smart Snapping ──────────────────────────────────────────

    private const double SnapThreshold = 10;

    private void ApplySnapping(ref double x, ref double y, ref double w, ref double h, bool isResizing = false)
    {
        // Use local coordinates (0 to VirtualScreenWidth/Height)
        // because MainWindow is already positioned/resized to cover the entire virtual desktop.
        Rect workArea = new Rect(0, 0, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);

        // 1. Snap to screen edges
        // Horizontal
        if (Math.Abs(x - workArea.Left) < SnapThreshold) x = workArea.Left;
        if (Math.Abs((x + w) - workArea.Right) < SnapThreshold)
        {
            if (isResizing) w = workArea.Right - x;
            else x = workArea.Right - w;
        }

        // Vertical
        if (Math.Abs(y - workArea.Top) < SnapThreshold) y = workArea.Top;
        if (Math.Abs((y + h) - workArea.Bottom) < SnapThreshold)
        {
            if (isResizing) h = workArea.Bottom - y;
            else y = workArea.Bottom - h;
        }

        // 2. Snap to other fences
        var mainWin = Window.GetWindow(this) as MainWindow;
        if (mainWin?.DataContext is MainViewModel mainVm)
        {
            foreach (var other in mainVm.Fences)
            {
                if (other == DataContext) continue;
                if (other.IsCollapsed) continue;

                // Horizontal alignment/snapping
                // Left edge snap
                if (Math.Abs(x - (other.X + other.Width)) < SnapThreshold) x = other.X + other.Width;
                else if (Math.Abs(x - other.X) < SnapThreshold) x = other.X;

                // Right edge snap
                if (Math.Abs((x + w) - other.X) < SnapThreshold)
                {
                    if (isResizing) w = other.X - x;
                    else x = other.X - w;
                }
                else if (Math.Abs((x + w) - (other.X + other.Width)) < SnapThreshold)
                {
                    if (isResizing) w = (other.X + other.Width) - x;
                    else x = (other.X + other.Width) - w;
                }

                // Vertical alignment/snapping
                // Top edge snap
                if (Math.Abs(y - (other.Y + other.Height)) < SnapThreshold) y = other.Y + other.Height;
                else if (Math.Abs(y - other.Y) < SnapThreshold) y = other.Y;

                // Bottom edge snap
                if (Math.Abs((y + h) - other.Y) < SnapThreshold)
                {
                    if (isResizing) h = other.Y - y;
                    else y = other.Y - h;
                }
                else if (Math.Abs((y + h) - (other.Y + other.Height)) < SnapThreshold)
                {
                    if (isResizing) h = (other.Y + other.Height) - y;
                    else y = (other.Y + other.Height) - h;
                }
            }
        }
    }
}
