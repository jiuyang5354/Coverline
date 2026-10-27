Coverline v0.3.2：网易云音乐提示。

- 网易云音乐 Windows 原版不向系统报告正在播放，需要安装 BetterNCM + InfLink-rs 插件才能显示封面和进度。
- 现在检测到网易云音乐（cloudmusic.exe）在运行、却没有向系统报告时，组件会显示「网易云音乐未接入」，悬停可看说明。
- 托盘菜单新增「网易云音乐支持（InfLink-rs）」，一键打开插件页面。
- README 新增网易云音乐安装步骤。

New in v0.3.2: when NetEase Cloud Music is running but not reporting to Windows, the widget says so, and Tray → NetEase Cloud Music opens the InfLink-rs plugin page. The stock Win32 client needs BetterNCM + InfLink-rs to show art and progress.

v0.3.0 及以上会自动更新到本版本。需要 Windows 10 1809 或更高版本。程序未签名，首次运行可能出现 SmartScreen 提示。灵感来自 Mac 应用 Sleeve（Replay Software），本项目为独立实现，与其无关联。
