using System.Text.Json.Serialization;

namespace PureDesktop.Models;

/// <summary>
/// Represents a single fence (grid container) on the desktop.
/// </summary>
public class Fence
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Title { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 320;
    public double Height { get; set; } = 280;
    public List<FenceItem> Items { get; set; } = new();

    /// <summary>
    /// If non-null, this fence is a disk-mapped fence pointing to this folder path.
    /// </summary>
    public string? MappedFolderPath { get; set; }

    /// <summary>
    /// Category key for auto-classification (e.g., "shortcuts", "documents", "images").
    /// </summary>
    public string? Category { get; set; }

    public bool IsCollapsed { get; set; } = false;

    /// <summary>
    /// View mode: "list" or "grid". Default "list".
    /// </summary>
    public string ViewMode { get; set; } = "list";

    public bool IsLocked { get; set; } = false;
}

/// <summary>
/// Represents a single file/folder item inside a fence.
/// </summary>
public class FenceItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }

    /// <summary>
    /// When true this item is a visual group header, not a real file.
    /// </summary>
    public bool IsGroupHeader { get; set; }

    /// <summary>
    /// Display label for the group header (e.g. "Word 文档", "PDF").
    /// </summary>
    public string? GroupLabel { get; set; }

    [JsonIgnore]
    public object? IconSource { get; set; }
}

/// <summary>
/// Root settings object persisted to fences.json.
/// </summary>
public class AppSettings
{
    public string Language { get; set; } = "zh-CN";

    /// <summary>
    /// Theme mode: "system", "light", or "dark". Default "system".
    /// </summary>
    public string ThemeMode { get; set; } = "system";

    /// <summary>
    /// Global fence opacity (0.1 ~ 1.0). Default 0.85.
    /// </summary>
    public double FenceOpacity { get; set; } = 0.85;

    public bool EnableAutoHide { get; set; } = false;
    public int AutoHideSeconds { get; set; } = 5;
    public string AccentColor { get; set; } = "#FF0078D4";
    public bool ShowTrayIcon { get; set; } = true;

    public List<Fence> Fences { get; set; } = new();

    /// <summary>
    /// Custom classification rules: fence title -> list of extensions (e.g., ".docx", ".pdf").
    /// </summary>
    public Dictionary<string, List<string>> CustomRules { get; set; } = new();

    /// <summary>
    /// Extensions to exclude from auto-classification (e.g., ".tmp").
    /// </summary>
    public List<string> BlacklistExtensions { get; set; } = new();

    /// <summary>
    /// File names to exclude from auto-classification.
    /// </summary>
    public List<string> BlacklistFiles { get; set; } = new();

    /// <summary>
    /// Folder names to exclude from auto-classification.
    /// </summary>
    public List<string> BlacklistFolders { get; set; } = new();
}
