# PureDesktop v1.1.0

[English](#english) | [简体中文](#简体中文)

---

<a name="english"></a>

## English Description

PureDesktop is a high-performance Windows desktop organizer designed to provide a "Pure" and clutter-free workspace. It replaces traditional desktop icons with organized, acrylic-styled fences and embeds itself directly into the desktop layer.

### 🌟 Key Features

- **Smart Incremental Organize**: Automatically classifies desktop items. It preserves existing fence layouts and only organizes new or unassigned files.
- **Three-Tier Classification**: Simplifies desktop management into three primary categories: **Folders**, **Common Files** (Documents/Shortcuts/Code), and **Other**.
- **Embedded Desktop Layer**: Hides default Windows desktop icons (`SysListView32`) and stays behind other windows, maintaining a clean look even after a system reboot or display change.
- **Native Shell Integration**: Supports the full Windows native context menu inside fences (Open, Copy, Cut, Properties, etc.).
- **Built-in Recycle Bin**: A dedicated, draggable desk icon with full empty and restore capabilities.
- **Mapped Fences**: Sync any local folder to your desktop with real-time file system monitoring.
- **Personalization**:
  - **Visuals**: Real-time Windows 11 style acrylic blur effects.
  - **Themes**: Full support for Light, Dark, and System-following modes.
  - **Colors**: Customizable accent colors (hex input or system colors) and global transparency control.
- **Efficiency**:
  - **Auto-Hide**: Fences can automatically fade out when idle.
  - **Quick Toggle**: Hide or show all fences by double-clicking empty desktop space.
  - **Magnetic Snapping**: Enhanced snapping effort (24px) for perfect alignment between fences and screen edges.
  - **Persistence**: Remembers your theme, language, and accent color settings upon restart.

### 📁 Classification Categories

The current version organizes files into:

- **Folders**: All directory entries.
- **Common Files**: Includes Shortcuts (`.lnk`, `.url`), Documents (Office, PDF, TXT), and Source Code (`.cs`, `.js`, `.py`, etc.).
- **Other**: Any file not specified in the common files list.

### ⌨️ Keyboard & Mouse Shortcuts

| Shortcut | Action |
| :--- | :--- |
| **Double-Click Desktop** | Toggle visibility of all fences |
| **Double-Click Title** | Rename fence |
| **Alt + Enter** | View file properties |
| **Delete** | Move selected item(s) to Recycle Bin |
| **Ctrl + C / X** | Copy / Cut selected items |
| **Ctrl + V** | Paste files into a fence |
| **F2** | Rename selected item |

---

<a name="简体中文"></a>

## 简体中文说明

PureDesktop 是一款高性能 Windows 桌面整理工具，致力于为您提供零干扰的“纯净”办公环境。它通过隐藏系统默认图标并引入亚克力风格的盒子（Fences）来管理您的桌面，并深度嵌入系统底层。

### 🌟 核心功能

- **智能增量整理**：一键自动分类桌面项目。具备增量识别能力，仅处理新产生的文件，不会破坏您已经手动调整好的格子布局。
- **三段式简洁分类**：默认将桌面文件简化为三大类：**文件夹**、**通用文件**（文档/快捷方式/代码）以及**其他**。
- **底层桌面嵌入**：自动隐藏 Windows 默认桌面图标 (`SysListView32`)，格子始终置于底层，支持跨屏显示和分辨率更改后的自动校准。
- **原生系统集成**：格子内支持唤起完整的 Windows 原生右键菜单，操作习惯与系统资源管理器保持一致。
- **内置回收站**：独立且可移动的回收站图标，支持直接清空或查看删除项。
- **文件夹映射**：支持将电脑任意文件夹映射到桌面格子中，并保持实时同步。
- **个性化定制**：
  - **亚克力特效**：适配 Win11 风格的实时模糊效果。
  - **主题模式**：完美适配深色、浅色及跟随系统的主题模式。
  - **色彩控制**：支持自定义主题色（十六进制输入、色块预设）及全局透明度调节。
- **交互效率**：
  - **自动隐藏**：格子可在桌面闲置一段时间后自动淡出。
  - **快速切换**：在桌面空白处双击可一键显示或隐藏所有内容。
  - **强磁吸附**：强化的 24px 磁吸阈值，让不同格子间、格子与屏幕边缘的对齐更轻松。
  - **状态持久化**：软件重启后自动恢复上次的主题模式、语言和主题色设置。

### 📁 分类逻辑

当前版本将项目分类为：

- **文件夹**：所有目录类项目。
- **通用文件**：涵盖快捷方式 (`.lnk`, `.url`)、办公文档 (Office, PDF, TXT) 以及各种源代码文件 (`.cs`, `.js`, `.py` 等)。
- **其他**：未包含在上述列表中的其他所有后缀文件。

### ⌨️ 键鼠快捷键

| 快捷键 | 动作 |
| :--- | :--- |
| **双击桌面空白处** | 显示/隐藏所有格子内容 |
| **双击格子标题** | 进入格子重命名模式 |
| **Alt + Enter** | 查看文件属性 |
| **Delete** | 将选中项目移至回收站 |
| **Ctrl + C / X** | 复制 / 剪切选中项目 |
| **Ctrl + V** | 粘贴文件至当前格子 |
| **F2** | 重命名选中项目 |

---
© 2026 jichuang. Licensed under the MIT License.
