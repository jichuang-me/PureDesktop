using PureDesktop.Models;

namespace PureDesktop.Core;

/// <summary>
/// Watches the desktop directory for new files and auto-classifies them into fences.
/// Event-driven via FileSystemWatcher for low CPU usage.
/// </summary>
public class DesktopWatcher : IDisposable
{
    public event Action<string, FenceItem>? FileAdded;
    public event Action<string, string>? FileRemoved; // category, fullPath
    public event Action<string, string, string>? FileRenamed; // category, oldPath, newPath

    private FileSystemWatcher? _userWatcher;
    private FileSystemWatcher? _commonWatcher;
    private readonly FenceManager _manager;

    public DesktopWatcher(FenceManager manager)
    {
        _manager = manager;
    }

    public void Start()
    {
        string userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

        if (Directory.Exists(userDesktop))
        {
            _userWatcher = CreateWatcher(userDesktop);
        }

        if (Directory.Exists(commonDesktop) && commonDesktop != userDesktop)
        {
            _commonWatcher = CreateWatcher(commonDesktop);
        }
    }

    private FileSystemWatcher CreateWatcher(string path)
    {
        var watcher = new FileSystemWatcher(path)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };

        watcher.Created += OnCreated;
        watcher.Deleted += OnDeleted;
        watcher.Renamed += OnRenamed;
        return watcher;
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        // Small delay to let file finish writing
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(async () =>
        {
            await Task.Delay(500);
            if (!File.Exists(e.FullPath) && !Directory.Exists(e.FullPath)) return;

            string name = Path.GetFileName(e.FullPath);
            string ext = Path.GetExtension(e.FullPath);

            // Check blacklist
            var settings = _manager.Settings;
            if (settings.BlacklistFiles.Contains(name, StringComparer.OrdinalIgnoreCase))
                return;
            if (!string.IsNullOrEmpty(ext) &&
                settings.BlacklistExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                return;

            try
            {
                var attrs = File.GetAttributes(e.FullPath);
                if (attrs.HasFlag(FileAttributes.Hidden) || attrs.HasFlag(FileAttributes.System))
                    return;
            }
            catch { return; }

            bool isDir = Directory.Exists(e.FullPath);
            if (isDir && settings.BlacklistFolders.Contains(name, StringComparer.OrdinalIgnoreCase))
                return;

            string category = FileClassifier.ClassifySingleFile(e.FullPath, settings);

            var item = new FenceItem
            {
                Name = name,
                FullPath = e.FullPath,
                Extension = ext,
                IsDirectory = isDir
            };

            FileAdded?.Invoke(category, item);
        });
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            // We don't know the category easily without searching or storing map
            // So we pass null category to imply "search all" or let listener handle it
            // But to be safe, let's let MainWindow search
            FileRemoved?.Invoke(null!, e.FullPath);
        });
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            FileRenamed?.Invoke(null!, e.OldFullPath, e.FullPath);
        });
    }

    public void Dispose()
    {
        if (_userWatcher != null)
        {
            _userWatcher.Created -= OnCreated;
            _userWatcher.Deleted -= OnDeleted;
            _userWatcher.Renamed -= OnRenamed;
            _userWatcher.Dispose();
        }
        if (_commonWatcher != null)
        {
            _commonWatcher.Created -= OnCreated;
            _commonWatcher.Deleted -= OnDeleted;
            _commonWatcher.Renamed -= OnRenamed;
            _commonWatcher.Dispose();
        }
    }
}
