using System;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows;
using Forms = System.Windows.Forms;

namespace Coverline
{
    internal sealed class Controller : IDisposable
    {
        private readonly Settings settings;
        private readonly MediaWatcher media;
        private readonly WidgetWindow widget;
        private readonly Forms.NotifyIcon tray;
        private readonly Forms.ContextMenuStrip menu;
        private Forms.ToolStripMenuItem layoutMenu;
        private Forms.ToolStripMenuItem visibleItem;

        public Controller()
        {
            settings = Settings.Load();
            media = new MediaWatcher(Application.Current.Dispatcher);
            widget = new WidgetWindow(settings, media);

            menu = BuildMenu();
            tray = new Forms.NotifyIcon
            {
                Icon = LoadIcon(),
                Text = AppInfo.Name,
                Visible = true,
                ContextMenuStrip = menu
            };
            tray.MouseClick += (s, e) => { if (e.Button == Forms.MouseButtons.Left) SetVisible(!widget.IsVisible); };
            widget.MenuRequested += () => menu.Show(Forms.Cursor.Position);
            media.Changed += np => UpdateTrayText(np);

            if (!settings.Hidden) widget.Show();
            media.Start();
        }

        private void UpdateTrayText(NowPlaying np)
        {
            string text = np.HasSession ? np.Title + (np.Artist.Length > 0 ? " — " + np.Artist : "") : AppInfo.Name;
            if (text.Length > 63) text = text.Substring(0, 62) + "…"; // NotifyIcon.Text limit
            try { tray.Text = text; } catch (ArgumentException) { }
        }

        private void SetVisible(bool visible)
        {
            if (visible) widget.Show(); else widget.Hide();
            settings.Hidden = !visible;
            settings.Save();
            if (visibleItem != null) visibleItem.Checked = visible;
        }

        private Forms.ContextMenuStrip BuildMenu()
        {
            var m = new Forms.ContextMenuStrip();

            visibleItem = new Forms.ToolStripMenuItem("显示组件 Show widget") { Checked = !settings.Hidden };
            visibleItem.Click += delegate { SetVisible(!widget.IsVisible); };
            m.Items.Add(visibleItem);
            m.Items.Add(new Forms.ToolStripSeparator());

            var themeMenu = new Forms.ToolStripMenuItem("主题 Theme");
            foreach (ThemeKind kind in new[] { ThemeKind.Editorial, ThemeKind.Glass, ThemeKind.ArtOnly })
            {
                ThemeKind k = kind;
                var item = new Forms.ToolStripMenuItem(Theme.NameOf(k)) { Tag = k };
                item.Click += delegate { settings.ThemeValue = k; Apply(); };
                themeMenu.DropDownItems.Add(item);
            }
            m.Items.Add(themeMenu);

            var sizeMenu = new Forms.ToolStripMenuItem("尺寸 Size");
            AddChoice(sizeMenu, "小 S", WidgetSize.Small);
            AddChoice(sizeMenu, "中 M", WidgetSize.Medium);
            AddChoice(sizeMenu, "大 L", WidgetSize.Large);
            m.Items.Add(sizeMenu);

            layoutMenu = new Forms.ToolStripMenuItem("布局 Layout");
            AddChoice(layoutMenu, "封面 + 文字 Art + text", WidgetLayout.ArtText);
            AddChoice(layoutMenu, "仅封面 Art only", WidgetLayout.ArtOnly);
            AddChoice(layoutMenu, "仅文字 Text only", WidgetLayout.TextOnly);
            m.Items.Add(layoutMenu);

            m.Items.Add(new Forms.ToolStripSeparator());

            var controls = new Forms.ToolStripMenuItem("悬停时显示播放控制 Show controls") { Checked = settings.ShowControls, CheckOnClick = true };
            controls.CheckedChanged += delegate { settings.ShowControls = controls.Checked; Apply(); };
            m.Items.Add(controls);

            var topmost = new Forms.ToolStripMenuItem("置顶显示 Always on top") { Checked = settings.Topmost, CheckOnClick = true };
            topmost.CheckedChanged += delegate { settings.Topmost = topmost.Checked; settings.Save(); widget.ApplyTopmost(); };
            m.Items.Add(topmost);

            var startup = new Forms.ToolStripMenuItem("开机自动启动 Launch at startup") { CheckOnClick = true };
            try { startup.Checked = Startup.Enabled; } catch { }
            startup.CheckedChanged += delegate { try { Startup.Enabled = startup.Checked; } catch (Exception ex) { Log.Write("startup: " + ex.Message); } };
            m.Items.Add(startup);

            m.Items.Add("重置位置 Reset position", null, delegate { if (!widget.IsVisible) SetVisible(true); widget.ResetPosition(); });

            m.Items.Add(new Forms.ToolStripSeparator());
            m.Items.Add("关于 " + AppInfo.Name, null, delegate
            {
                MessageBox.Show(AppInfo.Name + " " + AppInfo.Version +
                    "\n\nWindows 桌面上的「正在播放」小组件：封面、曲名、艺人和播放控制。\n" +
                    "A now-playing desktop widget for Windows.\n\n" +
                    "灵感来自 Mac 应用 Sleeve（Replay Software）。本项目为独立开发的开源软件，与 Sleeve 及其开发者无关联，未使用其任何代码或素材。\n" +
                    "Inspired by Sleeve for Mac by Replay Software. Not affiliated; no Sleeve code or assets are used.\n\n" +
                    AppInfo.Repo + "\nMIT License",
                    "关于 " + AppInfo.Name);
            });
            m.Items.Add("退出 Quit", null, delegate { Application.Current.Shutdown(); });

            m.Opening += delegate { SyncChecks(m, themeMenu, sizeMenu); };
            return m;
        }

        private void AddChoice<T>(Forms.ToolStripMenuItem parent, string text, T value) where T : struct
        {
            var item = new Forms.ToolStripMenuItem(text) { Tag = value };
            item.Click += delegate
            {
                object v = value;
                if (v is WidgetSize) settings.SizeValue = (WidgetSize)v;
                else if (v is WidgetLayout) settings.LayoutValue = (WidgetLayout)v;
                Apply();
            };
            parent.DropDownItems.Add(item);
        }

        private void SyncChecks(Forms.ContextMenuStrip m, Forms.ToolStripMenuItem themeMenu, Forms.ToolStripMenuItem sizeMenu)
        {
            foreach (Forms.ToolStripItem i in themeMenu.DropDownItems)
            {
                var mi = i as Forms.ToolStripMenuItem;
                if (mi != null) mi.Checked = mi.Tag is ThemeKind && (ThemeKind)mi.Tag == settings.ThemeValue;
            }
            foreach (Forms.ToolStripItem i in sizeMenu.DropDownItems)
            {
                var mi = i as Forms.ToolStripMenuItem;
                if (mi != null) mi.Checked = mi.Tag is WidgetSize && (WidgetSize)mi.Tag == settings.SizeValue;
            }
            bool forced = settings.ThemeValue == ThemeKind.ArtOnly;
            layoutMenu.Enabled = !forced;
            layoutMenu.ToolTipText = forced ? "「纯封面」主题只显示封面" : null;
            foreach (Forms.ToolStripItem i in layoutMenu.DropDownItems)
            {
                var mi = i as Forms.ToolStripMenuItem;
                if (mi != null) mi.Checked = mi.Tag is WidgetLayout && (WidgetLayout)mi.Tag == settings.LayoutValue;
            }
            visibleItem.Checked = widget.IsVisible;
        }

        private void Apply()
        {
            settings.Save();
            widget.Rebuild();
        }

        private static Icon LoadIcon()
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico");
            return stream != null ? new Icon(stream) : SystemIcons.Application;
        }

        public void Dispose()
        {
            media.Dispose();
            settings.Save();
            tray.Visible = false;
            tray.Dispose();
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, @"Local\" + AppInfo.Name + ".SingleInstance", out created))
            {
                if (!created) return;
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                Controller controller = null;
                app.Startup += delegate { controller = new Controller(); };
                app.Exit += delegate { if (controller != null) controller.Dispose(); };
                app.DispatcherUnhandledException += (s, e) =>
                {
                    Log.Write(e.Exception.ToString());
                    e.Handled = true;
                };
                app.Run();
            }
        }
    }
}
