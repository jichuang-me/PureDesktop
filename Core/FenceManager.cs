using System.Text.Json;
using PureDesktop.Models;

namespace PureDesktop.Core;

/// <summary>
/// Manages fence lifecycle: creation, deletion, persistence, and file operations.
/// </summary>
public class FenceManager
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PureDesktop");
    private static readonly string ConfigFile = Path.Combine(ConfigDir, "fences.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AppSettings Settings { get; private set; } = new();

    /// <summary>
    /// Load settings from disk, or create defaults.
    /// </summary>
    public void Load()
    {
        try
        {
            if (File.Exists(ConfigFile))
            {
                string json = File.ReadAllText(ConfigFile);
                Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
        }
        catch
        {
            Settings = new AppSettings();
        }
    }

    /// <summary>
    /// Save current settings to disk.
    /// </summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            string json = JsonSerializer.Serialize(Settings, JsonOptions);
            File.WriteAllText(ConfigFile, json);
        }
        catch { }
    }

    /// <summary>
    /// Run auto-classification: scan the desktop and populate fences.
    /// </summary>
    public void AutoClassify()
    {
        var classified = FileClassifier.ClassifyDesktopFiles(Settings);
        Settings.Fences.Clear();
        Settings.Fences.AddRange(classified);
        Save();
    }

    /// <summary>
    /// Add a new empty fence.
    /// </summary>
    public Fence AddFence(string title, double x, double y)
    {
        var fence = new Fence
        {
            Title = title,
            X = x,
            Y = y,
            Width = 300,
            Height = 260
        };
        Settings.Fences.Add(fence);
        Save();
        return fence;
    }

    /// <summary>
    /// Rename an existing fence by ID.
    /// </summary>
    public void RenameFence(string fenceId, string newTitle)
    {
        var fence = Settings.Fences.FirstOrDefault(f => f.Id == fenceId);
        if (fence != null)
        {
            fence.Title = newTitle;
            Save();
        }
    }

    /// <summary>
    /// Add a new disk-mapped fence pointing to a folder.
    /// </summary>
    public Fence AddMappedFence(string title, string folderPath, double x, double y)
    {
        var fence = new Fence
        {
            Title = title,
            MappedFolderPath = folderPath,
            X = x,
            Y = y,
            Width = 300,
            Height = 260
        };

        RefreshMappedFence(fence);
        Settings.Fences.Add(fence);
        Save();
        return fence;
    }

    /// <summary>
    /// Refresh the contents of a disk-mapped fence from the source folder.
    /// </summary>
    public void RefreshMappedFence(Fence fence)
    {
        if (string.IsNullOrEmpty(fence.MappedFolderPath) || !Directory.Exists(fence.MappedFolderPath))
            return;

        fence.Items.Clear();

        try
        {
            foreach (var dir in Directory.GetDirectories(fence.MappedFolderPath))
            {
                var attrs = File.GetAttributes(dir);
                if (attrs.HasFlag(FileAttributes.Hidden) || attrs.HasFlag(FileAttributes.System))
                    continue;

                fence.Items.Add(new FenceItem
                {
                    Name = Path.GetFileName(dir),
                    FullPath = dir,
                    IsDirectory = true
                });
            }

            foreach (var file in Directory.GetFiles(fence.MappedFolderPath))
            {
                var attrs = File.GetAttributes(file);
                if (attrs.HasFlag(FileAttributes.Hidden) || attrs.HasFlag(FileAttributes.System))
                    continue;

                fence.Items.Add(new FenceItem
                {
                    Name = Path.GetFileName(file),
                    FullPath = file,
                    Extension = Path.GetExtension(file),
                    IsDirectory = false
                });
            }
        }
        catch { }
    }

    /// <summary>
    /// Remove a fence by its ID.
    /// </summary>
    public void RemoveFence(string fenceId)
    {
        Settings.Fences.RemoveAll(f => f.Id == fenceId);
        Save();
    }

    /// <summary>
    /// Move an item from one fence to another.
    /// </summary>
    public void MoveItem(FenceItem item, Fence source, Fence target)
    {
        source.Items.Remove(item);
        target.Items.Add(item);
        Save();
    }
}
