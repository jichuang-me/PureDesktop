# PureDesktop

[English](#english) | [简体中文](#简体中文)

---

<a name="english"></a>

## English Description

PureDesktop is a lightweight, modern Windows desktop organizer designed to keep your workspace clean and productive. It automatically classifies your files into themed "fences" and stays invisible until you need it.

### 🌟 Key Features

- **Smart Classification**: 1-click organization of desktop files into categories (Documents, Pictures, Programs, etc.).
- **Mapped Fences**: Map any folder on your PC into a desktop fence with real-time synchronization.
- **Shell Integration**: Right-click any file in File Explorer to send it to a specific fence.
- **Exclusion Rules**: Define files, extensions, or folders to be ignored by the organizer.
- **Smart Appearance**:
  - **Acrylic Blur**: Premium Windows 11 acrylic effect for fences.
  - **Theme Support**: Follows system Light/Dark mode or manual override.
  - **Dynamic Opacity**: Adjust transparency for a custom look.
- **Productivity Tools**:
  - **Auto-Hide**: Fences fade out when idle to reveal your wallpaper.
  - **Sorting & Grouping**: Sort items by name, type, or date; group by type or date.
  - **View Modes**: Switch between Grid and List views.
- **Hidden Power**: Fully hidden from taskbar and Alt+Tab; accessible via tray or desktop double-click.

### ⌨️ Keyboard Shortcuts

| Shortcut | Action |
| :--- | :--- |
| **F2** | Rename selected item |
| **Alt + Enter** | Open file properties dialog |
| **Delete** | Move selected item(s) to Recycle Bin |
| **Ctrl + C / X** | Copy / Cut selected item(s) |
| **Ctrl + V** | Paste files into a mapped fence (or to desktop) |
| **Esc** | Close active dialog or collapse fence |

### 🖱️ Tray & Mouse Operations

- **Tray Left Click**: Toggle fence visibility.
- **Tray Right Click**: Access Settings, Add Mapped Fence, Theme, Language, and Exclusions.
- **Desktop Double-Click**: Show/Hide all fences.
- **Fence Title-Click**: Collapse/Expand the fence.

### 🛠️ Build & Run

- Requires **.NET 8 SDK**.
- Run `dotnet build` in the `PureDesktop` directory.
- Launch `PureDesktop.exe` from `bin/Debug/net8.0-windows/`.

---

<a name="简体中文"></a>

## 简体中文说明

PureDesktop 是一款轻量级、现代化的 Windows 桌面整理工具，旨在让您的工作空间保持整洁高效。它能自动将文件分类到主题“分栏（Fences）”中，并在不需要时保持隐身。

### 🌟 核心功能

- **一键整理**：智能自动分类桌面文件（文档、图片、程序、压缩包等）。
- **文件夹映射**：将电脑任意文件夹映射为桌面格子，支持实时同步。
- **右键菜单集成**：在资源管理器右键点击文件，即可快速发送至指定格子。
- **排除规则**：自定义排除特定后缀、文件名或文件夹，防止误整理。
- **精美视觉**：
  - **亚克力效果**：采用 Win11 风格的亚克力背景，质感高级。
  - **主题适配**：完美支持系统深/浅色模式切换。
  - **透明度调节**：自由调整格子透明度。
- **高效管理**：
  - **自动隐藏**：闲置时自动淡出，还原精美壁纸。
  - **排序与分组**：支持按名称、类型、修改日期排序或分组。
  - **视图切换**：支持图标网格与详细列表两种视图模式。
- **极简体验**：不占任务栏，不占 Alt+Tab；通过托盘图标或桌面双击快速唤醒。

### ⌨️ 快捷键支持

| 快捷键 | 动作 |
| :--- | :--- |
| **F2** | 重命名所选项目 |
| **Alt + Enter** | 打开文件属性对话框 |
| **Delete** | 将所选项目移至回收站 |
| **Ctrl + C / X** | 复制 / 剪切项目 |
| **Ctrl + V** | 粘贴文件到分栏（或桌面） |
| **Esc** | 关闭对话框或收起格子 |

### 🖱️ 操作指南

- **托盘左键**：快速切换格子显示/隐藏。
- **托盘右键**：访问设置、添加映射、切换主题语言、管理排除项。
- **桌面双击**：显示/隐藏所有格子。
- **标题点击**：折叠或展开格子。

### 🛠️ 编译与运行

- 需要安装 **.NET 8 SDK**。
- 在 `PureDesktop` 目录下运行 `dotnet build`。
- 从 `bin/Debug/net8.0-windows/` 目录运行 `PureDesktop.exe`。

---
© 2026 jichuang. Licensed under the MIT License.
