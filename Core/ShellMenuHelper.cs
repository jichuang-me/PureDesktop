using System;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PureDesktop.Core;

/// <summary>
/// Handles integration into the Windows Desktop right-click menu using Registry.
/// </summary>
public static class ShellMenuHelper
{
    private const string MenuKeyPath = @"Directory\Background\shell\PureDesktop";

    public static void Register()
    {
        try
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (string.IsNullOrEmpty(exePath)) return;

            // 1. Create the main entry with SubCommands
            using (var key = Registry.ClassesRoot.CreateSubKey(MenuKeyPath))
            {
                key.SetValue("", "PureDesktop");
                key.SetValue("Icon", exePath);
                key.SetValue("SubCommands", ""); // Mark as a sub-menu
            }

            // 2. Create sub-items (Secondary menu)
            // Use shell\PureDesktop\shell for actual items in Win10/11
            var subItemsKey = $@"{MenuKeyPath}\shell";
            
            using (var subKey = Registry.ClassesRoot.CreateSubKey($@"{subItemsKey}\Organize"))
            {
                subKey.SetValue("", "一键整理");
                using (var cmdKey = subKey.CreateSubKey("command"))
                {
                    cmdKey.SetValue("", $"\"{exePath}\" --organize");
                }
            }

            using (var subKey = Registry.ClassesRoot.CreateSubKey($@"{subItemsKey}\Settings"))
            {
                subKey.SetValue("", "设置");
                using (var cmdKey = subKey.CreateSubKey("command"))
                {
                    cmdKey.SetValue("", $"\"{exePath}\" --settings");
                }
            }

            using (var subKey = Registry.ClassesRoot.CreateSubKey($@"{subItemsKey}\Exit"))
            {
                subKey.SetValue("", "退出");
                using (var cmdKey = subKey.CreateSubKey("command"))
                {
                    cmdKey.SetValue("", $"\"{exePath}\" --exit");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to register shell menu: {ex.Message}");
        }
    }

    public static void Unregister()
    {
        try
        {
            Registry.ClassesRoot.DeleteSubKeyTree(MenuKeyPath, false);
        }
        catch { }
    }
}
