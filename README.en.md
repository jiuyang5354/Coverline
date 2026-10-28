[中文](README.md) | **English**

# Coverline

A now-playing widget for the Windows desktop. Album art, title, and artist sit quietly on your desktop; previous, play/pause, and next appear on hover. Works with players that report to Windows media controls, including Spotify, Apple Music for Windows, and browsers.

Inspired by [Sleeve for Mac](https://replay.software/sleeve) by Replay Software. Coverline is independently written, is not affiliated with Sleeve or its developer, and uses none of their code or assets.

![Liquid Glass preview](docs/liquid-glass-preview.png)

This is an HTML/CSS mock using the same dimensions and colors as the WPF implementation, not a Windows screenshot. [Dark preview](docs/liquid-glass-preview-dark.png).

## Liquid Glass

The default theme from v0.2 uses DWM system acrylic on Windows 11 22H2+, accent acrylic on Windows 10, and an opaque fallback when unavailable. It combines faint tinting, top lighting, a 1px bevel rim, a specular arc, and 26px corners. Artwork is inset and subtly tints the glass with its average color.

Type follows Windows app light/dark mode, with a soft scrim for readability. Controls occupy a small glass capsule. Disabling Windows transparency switches to a high-contrast opaque panel. The tray's **Glass backdrop** menu offers Auto, System acrylic, Classic blur, and Opaque.

## Features

- Reads title, artwork, and playback state through SMTC (`GlobalSystemMediaTransportControlsSessionManager`). Displays an idle state when nothing is playing.
- Borderless widget, normally beneath other windows, with optional always-on-top. Initially appears above the taskbar at the bottom center of the primary monitor.
- Hover playback controls and crossfades between tracks.
- Drag to reposition; the position is saved. Reset position from the tray.
- Themes: Liquid Glass, Editorial, Dark Glass, and Artwork Only.
- Small/medium/large sizes; artwork + text, artwork only, or text only layouts.
- Tray options for hover controls, topmost mode, startup at login, updates, reset, and exit. Click the tray icon to show or hide the widget.
- Settings: `%AppData%\Coverline\settings.json`.

## Download and run

Download `Coverline-Windows-vX.Y.Z.zip` from [Releases](https://github.com/jiuyang5354/Coverline/releases), extract it, and run `Coverline.exe`. No installer is required. Supports Windows 10 1809+ / Windows 11 with .NET Framework 4.8.

## NetEase Cloud Music

The stock Win32 client does not report now-playing to Windows. To connect it:

1. Install [BetterNCM](https://github.com/std-microblock/BetterNCM-Installer/releases/latest).
2. Install [InfLink-rs](https://github.com/apoint123/inflink-rs) from its store, or put its `.plugin` release file in `C:\betterncm\plugins`.
3. Restart NetEase Cloud Music.

Do not install the old InfLink alongside InfLink-rs. Consult the plugin's README for tested client versions. From v0.3.2, Coverline shows a disconnected hint when NetEase is running without SMTC reporting, and the tray links to the plugin page.

## Automatic updates

From v0.3, Coverline checks Releases, including prereleases, 30 seconds after launch and every six hours. Downloads go to `%LocalAppData%\Coverline\updates\` and are verified using the release's `SHA256SUMS.txt`.

Installation waits until playback is idle and the pointer is outside the widget. Coverline exits, replaces files, keeps `Coverline.old.exe`, rolls back on failure, and relaunches with a notification. Tray options provide a manual check, automatic-update toggle (enabled by default), and version information.

An unwritable installation folder never triggers elevation; Coverline notifies you and opens Releases for a manual download. A user-owned folder such as `%LocalAppData%\Programs\Coverline` is recommended. Logs are in `%LocalAppData%\Coverline\update.log`. Network failures, timeouts, and GitHub rate limits are skipped until the next check.

Versions v0.2.0 and earlier cannot update themselves. Upgrade manually once to v0.3.0 or later.

### GitHub connectivity fallbacks, v0.3.1+

If GitHub's API fails after eight seconds, version metadata is read from `latest.json` in this repository's `update-feed` branch through jsDelivr: `cdn.jsdelivr.net`, `fastly.jsdelivr.net`, then `testingcf.jsdelivr.net`. CI publishes the version, ZIP name, and SHA-256 there after a release.

Downloads try GitHub first, then `https://ghfast.top/`, `https://gh-proxy.com/`, `https://ghproxy.net/`, and `https://gh.llkk.cc/`, each prefixed to the complete download URL and subject to its own timeout. Hashes are accepted only from GitHub or the jsDelivr metadata. A mismatched response, including an HTML error page, is discarded.

To replace the proxy list, quit Coverline, edit `"updateMirrors": [...]` in its settings, then relaunch. An empty array uses GitHub only. Public proxies may fail or slow down; jsDelivr may cache older metadata for several hours. v0.3.0 needs a working GitHub connection once to obtain fallback support.

## Build and rename

```powershell
cd windows
dotnet build -c Release
```

The product name is centralized in `windows/src/AppInfo.cs` as `AppInfo.Name`. Renaming also requires updating `AssemblyName` and the project filename in `windows/Coverline.csproj`, plus `APP` in `.github/workflows/build.yml`.

## Known limitations

- Desktop-layer mode places the window below other windows; Win+D also hides the widget.
- Only SMTC-reporting players are supported. Some omit artwork or previous-track control.
- Editorial headings prefer Noto Serif SC / Source Han Serif, falling back to Georgia and SimSun.
- Position uses primary-monitor coordinates. If the widget becomes invisible after disconnecting a monitor, it returns to the bottom center.
- Glass uses a non-layered window with a rounded region, so there is no outer shadow; slight corner aliasing at 100% scaling and acrylic lag on Windows 10 are possible.
- Light/dark mode follows Windows app mode rather than sampling wallpaper brightness. Some Windows 11 builds render inactive system-acrylic windows as solid; choose Classic blur if needed.
- Updates use a hidden PowerShell helper. Policies that block PowerShell prevent updates and retain the old version. Unsigned downloads may trigger SmartScreen.

The v0.1 blank-text issue caused by burst playback updates was fixed in v0.2: one fade-out runs at a time, and the latest content always fades back in. The Editorial theme's paper panel is now opaque.

## License

[MIT](LICENSE).
