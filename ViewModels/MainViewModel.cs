using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PureDesktop.Models;
using PureDesktop.Helpers;

namespace PureDesktop.ViewModels;

/// <summary>
/// ViewModel for a single FenceItem (file/folder displayed in a fence).
/// </summary>
public class FenceItemViewModel : INotifyPropertyChanged
{
    private readonly FenceItem _model;

    public FenceItemViewModel(FenceItem model)
    {
        _model = model;
        if (!model.IsGroupHeader) LoadIcon();
    }

    public FenceItem Model => _model;
    public string Name => _model.Name;
    public string FullPath => _model.FullPath;
    public bool IsDirectory => _model.IsDirectory;
    public bool IsGroupHeader => _model.IsGroupHeader;
    public string? GroupLabel => _model.GroupLabel;

    private System.Windows.Media.ImageSource? _icon;
    public System.Windows.Media.ImageSource? Icon
    {
        get => _icon;
        set { _icon = value; OnPropertyChanged(); }
    }

    private bool _isRenaming;
    public bool IsRenaming
    {
        get => _isRenaming;
        set
        {
            if (_isRenaming != value)
            {
                _isRenaming = value;
                OnPropertyChanged();
                if (_isRenaming) RenameText = Name;
            }
        }
    }

    private string _renameText = "";
    public string RenameText
    {
        get => _renameText;
        set { _renameText = value; OnPropertyChanged(); }
    }

    // ─── Lazy preview text (generated on first access) ──────────
    private string? _previewText;
    private bool _previewLoaded;

    public string PreviewText
    {
        get
        {
            if (!_previewLoaded)
            {
                _previewLoaded = true;
                _previewText = BuildPreview();
            }
            return _previewText ?? Name;
        }
    }

    private string? BuildPreview()
    {
        try
        {
            if (IsGroupHeader) return GroupLabel;
            string path = _model.FullPath;

            if (_model.IsDirectory)
            {
                if (!System.IO.Directory.Exists(path)) return Name;
                var entries = System.IO.Directory.EnumerateFileSystemEntries(path)
                    .Take(8)
                    .Select(System.IO.Path.GetFileName)
                    .ToList();
                int total = System.IO.Directory.EnumerateFileSystemEntries(path).Count();
                string list = string.Join("\n", entries);
                if (total > 8) list += $"\n… 共 {total} 项";
                return $"📂 {Name}\n{list}";
            }

            if (!System.IO.File.Exists(path)) return Name;
            var fi = new System.IO.FileInfo(path);
            string sizeStr = fi.Length < 1024 ? $"{fi.Length} B"
                : fi.Length < 1048576 ? $"{fi.Length / 1024.0:F1} KB"
                : $"{fi.Length / 1048576.0:F1} MB";
            string dateStr = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm");

            // Text-previewable extensions
            var textExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".txt",".log",".md",".json",".xml",".csv",".ini",".cfg",
                ".yml",".yaml",".toml",".bat",".cmd",".ps1",".sh",
                ".cs",".py",".js",".ts",".html",".css",".java",".c",".cpp",".h"
            };

            if (textExts.Contains(_model.Extension))
            {
                using var sr = new System.IO.StreamReader(path,
                    System.Text.Encoding.UTF8, true,
                    new System.IO.FileStreamOptions
                    {
                        Access = System.IO.FileAccess.Read,
                        Share = System.IO.FileShare.ReadWrite,
                        BufferSize = 512
                    });
                char[] buf = new char[500];
                int read = sr.Read(buf, 0, 500);
                string preview = new string(buf, 0, read).TrimEnd();
                if (read == 500) preview += " …";
                return $"{Name}  ({sizeStr})\n{dateStr}\n───\n{preview}";
            }

            return $"{Name}\n{sizeStr}  |  {dateStr}";
        }
        catch
        {
            return Name;
        }
    }

    private void LoadIcon()
    {
        try
        {
            Icon = IconHelper.GetIcon(_model.FullPath);
        }
        catch { }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// ViewModel for a single Fence (grid container).
/// </summary>
public class FenceViewModel : INotifyPropertyChanged
{
    private readonly Fence _model;

    public FenceViewModel(Fence model)
    {
        _model = model;
        Items = new ObservableCollection<FenceItemViewModel>(
            model.Items.Select(i => new FenceItemViewModel(i)));
    }

    public Fence Model => _model;

    public string Title
    {
        get
        {
            // Resolve from i18n resource
            var key = _model.Title;
            if (System.Windows.Application.Current?.TryFindResource(key) is string localized)
                return localized;
            return key;
        }
    }

    public double X
    {
        get => _model.X;
        set { _model.X = value; OnPropertyChanged(); }
    }

    public double Y
    {
        get => _model.Y;
        set { _model.Y = value; OnPropertyChanged(); }
    }

    public double Width
    {
        get => _model.Width;
        set { _model.Width = value; OnPropertyChanged(); }
    }

    public double Height
    {
        get => _model.Height;
        set { _model.Height = value; OnPropertyChanged(); }
    }

    public bool IsCollapsed
    {
        get => _model.IsCollapsed;
        set { _model.IsCollapsed = value; OnPropertyChanged(); OnPropertyChanged(nameof(ContentHeight)); }
    }

    public double ContentHeight => IsCollapsed ? 0 : Height - 32;

    /// <summary>
    /// View mode: "list" or "grid".
    /// </summary>
    public string ViewMode
    {
        get => _model.ViewMode;
        set
        {
            _model.ViewMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsListView));
            OnPropertyChanged(nameof(IsGridView));
        }
    }

    public bool IsListView => ViewMode == "list";
    public bool IsGridView => ViewMode == "grid";

    public bool IsLocked
    {
        get => _model.IsLocked;
        set { _model.IsLocked = value; OnPropertyChanged(); }
    }

    public void ToggleViewMode()
    {
        ViewMode = ViewMode == "list" ? "grid" : "list";
    }

    public ObservableCollection<FenceItemViewModel> Items { get; }

    public bool IsMapped => !string.IsNullOrEmpty(_model.MappedFolderPath);

    /// <summary>
    /// Remove an item from this fence.
    /// </summary>
    public void RemoveItem(FenceItemViewModel item)
    {
        Items.Remove(item);
        _model.Items.Remove(item.Model);
    }

    /// <summary>
    /// Add an item to this fence.
    /// </summary>
    public void AddItem(FenceItem model)
    {
        _model.Items.Add(model);
        Items.Add(new FenceItemViewModel(model));
    }

    /// <summary>
    /// Refresh items from model (used after disk mapper sync).
    /// </summary>
    public void RefreshItems()
    {
        // Cleanup non-existent files (ghost items)
        var itemsToRemove = _model.Items.Where(i => 
            !i.IsGroupHeader && 
            !System.IO.File.Exists(i.FullPath) && 
            !System.IO.Directory.Exists(i.FullPath)).ToList();
            
        foreach(var item in itemsToRemove)
            _model.Items.Remove(item);

        Items.Clear();
        foreach (var item in _model.Items)
            Items.Add(new FenceItemViewModel(item));
        OnPropertyChanged(nameof(Title));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Main ViewModel for the entire desktop overlay.
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    public ObservableCollection<FenceViewModel> Fences { get; } = new();

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public void LoadFromSettings(Models.AppSettings settings)
    {
        Fences.Clear();
        foreach (var fence in settings.Fences)
        {
            var vm = new FenceViewModel(fence);
            vm.RefreshItems(); // This will cleanup ghost files inside each fence
            Fences.Add(vm);
        }
        FenceOpacity = settings.FenceOpacity;
    }

    private double _fenceOpacity = 0.85;
    /// <summary>
    /// Global fence opacity (0.1 ~ 1.0), applied to all fences.
    /// </summary>
    public double FenceOpacity
    {
        get => _fenceOpacity;
        set
        {
            _fenceOpacity = Math.Max(0.1, Math.Min(1.0, value));
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
