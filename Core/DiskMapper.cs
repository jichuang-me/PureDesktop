using System.IO;
using PureDesktop.Models;

namespace PureDesktop.Core;

/// <summary>
/// Watches a disk-mapped folder for file changes and synchronizes the fence.
/// </summary>
public class DiskMapper : IDisposable
{
    private FileSystemWatcher? _watcher;
    private readonly Fence _fence;
    private readonly FenceManager _manager;
    private readonly Action _onChanged;

    public DiskMapper(Fence fence, FenceManager manager, Action onChanged)
    {
        _fence = fence;
        _manager = manager;
        _onChanged = onChanged;
    }

    /// <summary>
    /// Start watching the mapped folder.
    /// </summary>
    public void Start()
    {
        if (string.IsNullOrEmpty(_fence.MappedFolderPath) || !Directory.Exists(_fence.MappedFolderPath))
            return;

        _watcher = new FileSystemWatcher(_fence.MappedFolderPath)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };

        _watcher.Created += OnFileChanged;
        _watcher.Deleted += OnFileChanged;
        _watcher.Renamed += OnFileRenamed;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            _manager.RefreshMappedFence(_fence);
            _onChanged();
        });
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            _manager.RefreshMappedFence(_fence);
            _onChanged();
        });
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}
