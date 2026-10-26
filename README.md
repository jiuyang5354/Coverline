# Coverline

Windows 桌面上的「正在播放」小组件。封面、曲名、艺人安静地待在桌面上，鼠标移上去才出现上一首 / 播放暂停 / 下一首。Spotify、Apple Music for Windows、浏览器里的网页播放器，只要会出现在 Windows 音量浮窗里，都能显示。

A now-playing widget for the Windows desktop: album art, title and artist sit quietly on your desktop, with previous / play-pause / next appearing on hover. Works with anything that reports to Windows' media controls — Spotify, Apple Music for Windows, browsers and more.

> 灵感来自 Mac 应用 [Sleeve](https://replay.software/sleeve)（Replay Software）。Coverline 是从零编写的独立开源项目，与 Sleeve 及其开发者没有任何关联，也没有使用其代码或素材。
> Inspired by Sleeve for Mac by Replay Software. Coverline is an independent, open-source implementation written from scratch and is not affiliated with Sleeve or its developer.

## 功能 (v0.1)

- **读取系统正在播放**：通过 Windows 的系统媒体传输控件（SMTC，`GlobalSystemMediaTransportControlsSessionManager`）读取当前曲目、封面和播放状态；没有播放时显示「此刻安静」的空闲状态
- **桌面小组件**：无边框、透明背景，封面带圆角和柔和投影；默认贴在桌面层（在其他窗口下方），可选置顶
- **悬停控制**：鼠标移到组件上才显示上一首 / 播放暂停 / 下一首，不用时保持干净
- **换歌淡入淡出**：封面和文字在切歌时平滑交叉淡化
- **随意摆放**：按住拖动，位置自动记住
- **托盘菜单**（右键托盘图标或组件本身）：
  - 主题：**编辑部**（默认：纸色卡片、衬线标题、等宽小标签、一个橙色点缀）/ **暗色玻璃**（随封面变色的磨砂深色卡片）/ **纯封面**
  - 尺寸：小 / 中 / 大
  - 布局：封面 + 文字 / 仅封面 / 仅文字
  - 悬停时显示播放控制、置顶显示、开机自动启动、重置位置、退出
- 单击托盘图标显示 / 隐藏组件
- 设置保存为 JSON：`%AppData%\Coverline\settings.json`

## 下载与运行

在 [Releases](../../releases) 下载 `Coverline-Windows-vX.Y.Z.zip`，解压后运行 `Coverline.exe`。程序常驻托盘，无需安装。需要 Windows 10 1809 或更高版本 / Windows 11（系统自带 .NET Framework 4.8）。

## 构建

```powershell
cd windows
dotnet build -c Release
```

## 改名

产品名集中在 `windows/src/AppInfo.cs` 的 `AppInfo.Name`。改名时另外同步两处：`windows/Coverline.csproj` 里的 `AssemblyName`（以及项目文件名），和 `.github/workflows/build.yml` 顶部的 `APP`。

## 已知限制

- 「贴在桌面层」是把窗口放在所有窗口最下方；按 Win+D 显示桌面时组件会和其他窗口一起被收起。
- 只有向 Windows 媒体控件报告信息的播放器才能显示；少数播放器不提供封面或不支持上一首。
- 标题衬线字体优先使用 Noto Serif SC / 思源宋体，未安装时退回 Georgia + 宋体。
- 目前位置按主显示器坐标记忆；拔掉外接显示器后，如果组件不在可见区域会自动回到右上角。

## License

MIT
