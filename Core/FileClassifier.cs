using PureDesktop.Models;

namespace PureDesktop.Core;

/// <summary>
/// Classifies desktop files into 12 categories by file extension.
/// Supports custom rules and blacklist filtering.
/// </summary>
public static class FileClassifier
{
    private static readonly Dictionary<string, HashSet<string>> CategoryExtensions = new()
    {
        ["shortcuts"] = new(StringComparer.OrdinalIgnoreCase) { ".lnk", ".url" },
        ["documents"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ".doc", ".docx", ".rtf", ".odt", ".wps", ".txt", ".md",
            ".xls", ".xlsx", ".csv", ".ods", ".et", ".etx",
            ".ppt", ".pptx", ".odp", ".dps", ".key",
            ".pdf"
        },
        ["images"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".svg", ".webp", ".ico",
            ".tiff", ".tif", ".raw", ".psd", ".ai", ".eps"
        },
        ["videos"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v",
            ".rmvb", ".rm", ".3gp", ".ts"
        },
        ["audio"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a", ".ape"
        },
        ["archives"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".iso"
        },
        ["installers"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".msi", ".msix", ".appx", ".deb", ".dmg"
        },
        ["code"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".java", ".py", ".js", ".ts", ".html", ".css", ".cpp",
            ".c", ".h", ".go", ".rs", ".rb", ".php", ".swift", ".kt",
            ".json", ".xml", ".yaml", ".yml", ".ini", ".log", ".bat",
            ".cmd", ".ps1", ".sh", ".vbs", ".sql", ".r", ".m"
        }
    };

    /// <summary>
    /// Classifies files from the desktop directory into categorized fences.
    /// Supports custom rules and blacklist from settings.
    /// </summary>
    public static List<Fence> ClassifyDesktopFiles(AppSettings? settings = null)
    {
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

        var blacklistExts = new HashSet<string>(
            settings?.BlacklistExtensions ?? new(), StringComparer.OrdinalIgnoreCase);
        var blacklistFiles = new HashSet<string>(
            settings?.BlacklistFiles ?? new(), StringComparer.OrdinalIgnoreCase);
        var blacklistFolders = new HashSet<string>(
            settings?.BlacklistFolders ?? new(), StringComparer.OrdinalIgnoreCase);

        var allItems = new List<(string path, bool isDir)>();

        // Gather files from user desktop
        if (Directory.Exists(desktopPath))
        {
            foreach (var f in Directory.GetFiles(desktopPath))
                allItems.Add((f, false));
            foreach (var d in Directory.GetDirectories(desktopPath))
                allItems.Add((d, true));
        }

        // Gather files from common (public) desktop
        if (Directory.Exists(commonDesktop))
        {
            foreach (var f in Directory.GetFiles(commonDesktop))
                allItems.Add((f, false));
        }

        // Classify into buckets
        var buckets = new Dictionary<string, List<FenceItem>>
        {
            ["shortcuts"] = new(),
            ["folders"] = new(),
            ["documents"] = new(),
            ["images"] = new(),
            ["videos"] = new(),
            ["audio"] = new(),
            ["archives"] = new(),
            ["installers"] = new(),
            ["code"] = new(),
            ["other"] = new()
        };

        foreach (var (path, isDir) in allItems)
        {
            string name = Path.GetFileName(path);
            string ext = Path.GetExtension(path);

            // Skip hidden/system files
            try
            {
                var attrs = File.GetAttributes(path);
                if (attrs.HasFlag(FileAttributes.Hidden) || attrs.HasFlag(FileAttributes.System))
                    continue;
            }
            catch { continue; }

            // Blacklist checks
            if (blacklistFiles.Contains(name)) continue;
            if (!string.IsNullOrEmpty(ext) && blacklistExts.Contains(ext)) continue;
            if (isDir && blacklistFolders.Contains(name)) continue;

            var item = new FenceItem
            {
                Name = name,
                FullPath = path,
                Extension = ext,
                IsDirectory = isDir
            };

            if (isDir)
            {
                buckets["folders"].Add(item);
            }
            else
            {
                // Check custom rules first
                string category = GetCategoryWithCustomRules(ext, settings);
                buckets[category].Add(item);
            }
        }

        // Create fences from non-empty buckets, arranged in a grid
        var fences = new List<Fence>();
        double x = 20, y = 20;
        double fenceW = 300, fenceH = 260;
        double gap = 12;
        int screenW = Helpers.Win32Api.GetSystemMetrics(Helpers.Win32Api.SM_CXSCREEN);

        foreach (var (category, items) in buckets)
        {
            if (items.Count == 0) continue;

            fences.Add(new Fence
            {
                Title = GetCategoryDisplayKey(category),
                Category = category,
                X = x,
                Y = y,
                Width = fenceW,
                Height = fenceH,
                Items = items,
                ViewMode = "list"
            });

            x += fenceW + gap;
            if (x + fenceW > screenW)
            {
                x = 20;
                y += fenceH + gap;
            }
        }

        return fences;
    }

    /// <summary>
    /// Classify a single file into the appropriate category.
    /// </summary>
    public static string ClassifySingleFile(string filePath, AppSettings? settings = null)
    {
        string ext = Path.GetExtension(filePath);
        bool isDir = Directory.Exists(filePath);
        if (isDir) return "folders";
        return GetCategoryWithCustomRules(ext, settings);
    }

    /// <summary>
    /// Gets the category for a given file extension, checking custom rules first.
    /// </summary>
    private static string GetCategoryWithCustomRules(string extension, AppSettings? settings)
    {
        // Check custom rules first
        if (settings?.CustomRules != null)
        {
            foreach (var (targetFence, exts) in settings.CustomRules)
            {
                if (exts.Any(e => string.Equals(e, extension, StringComparison.OrdinalIgnoreCase)))
                    return targetFence;
            }
        }

        return GetCategory(extension);
    }

    /// <summary>
    /// Gets the category for a given file extension from built-in rules.
    /// </summary>
    public static string GetCategory(string extension)
    {
        foreach (var (category, extensions) in CategoryExtensions)
        {
            if (extensions.Contains(extension))
                return category;
        }
        return "other";
    }

    /// <summary>
    /// Returns the i18n resource key for a category name.
    /// </summary>
    public static string GetCategoryDisplayKey(string category) => category switch
    {
        "shortcuts" => "Cat_Shortcuts",
        "folders" => "Cat_Folders",
        "documents" => "Cat_Documents",
        "images" => "Cat_Images",
        "videos" => "Cat_Videos",
        "audio" => "Cat_Audio",
        "archives" => "Cat_Archives",
        "installers" => "Cat_Installers",
        "code" => "Cat_Code",
        "other" => "Cat_Other",
        _ => "Cat_Other"
    };
}
