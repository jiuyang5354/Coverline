Coverline v0.3：自动更新。

- 新增自动更新：启动 30 秒后、之后每 6 小时检查 GitHub Releases（包括预发布版），后台下载并用 SHA256SUMS.txt 校验，没有播放音乐且鼠标不在组件上时静默重启完成更新；旧版备份为 Coverline.old.exe，替换失败自动回滚。更新后首次启动会提示「已更新到 vX.Y.Z」。
- 托盘菜单新增：当前版本号、「检查更新」（立即检查，气泡显示结果；下载好后点气泡可立即更新）、「自动更新」开关（默认开启）。
- 安装目录不可写（如 Program Files）时不会请求管理员权限，只提示并打开 Releases 页面。
- 离线、超时、GitHub 限流时静默跳过；日志写在 %LocalAppData%\Coverline\update.log。

**v0.2.0 及更早版本需要手动下载一次 v0.3.0**（解压覆盖旧文件即可，设置会保留），之后的版本会自动更新。

New in v0.3: auto update. Coverline checks GitHub Releases (prereleases included) 30 s after launch and every 6 hours, downloads and verifies the new build against SHA256SUMS.txt, and installs it when idle (nothing playing, pointer not on the widget), keeping Coverline.old.exe and rolling back if the swap fails. Tray menu gains the version, Check for updates and an Auto update switch (on by default). Read-only install folders get a notification and the Releases page instead of an admin prompt. **v0.2.0 and earlier: download v0.3.0 manually once; it updates itself from then on.**

需要 Windows 10 1809 或更高版本。程序未签名，首次运行可能出现 SmartScreen 提示。灵感来自 Mac 应用 Sleeve（Replay Software），本项目为独立实现，与其无关联。
