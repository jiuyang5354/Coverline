using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Coverline
{
    // Two stacked layers that crossfade between images. Used for the artwork and for the
    // blurred glass backdrop.
    internal sealed class CrossfadeImage : Grid
    {
        private Border front;
        private Border back;
        private string key = "\u0000";

        public CrossfadeImage(double corner)
        {
            front = Layer(corner);
            back = Layer(corner);
            back.Opacity = 0;
            Children.Add(back);
            Children.Add(front);
        }

        private static Border Layer(double corner)
        {
            return new Border { CornerRadius = new CornerRadius(corner), IsHitTestVisible = false };
        }

        public bool HasImage { get; private set; }

        public void Set(ImageSource image, string imageKey, bool animate)
        {
            if (imageKey == key) return;
            key = imageKey;
            HasImage = image != null;
            Brush brush = null;
            if (image != null)
            {
                var ib = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
                RenderOptions.SetBitmapScalingMode(ib, BitmapScalingMode.HighQuality);
                ib.Freeze();
                brush = ib;
            }

            if (!animate)
            {
                front.BeginAnimation(OpacityProperty, null);
                back.BeginAnimation(OpacityProperty, null);
                front.Background = brush;
                front.Opacity = 1;
                back.Opacity = 0;
                back.Background = null;
                return;
            }

            // Incoming image goes on the back layer, which is raised and faded in over the old one.
            var incoming = back;
            var outgoing = front;
            incoming.BeginAnimation(OpacityProperty, null);
            incoming.Background = brush;
            incoming.Opacity = 0;
            Children.Remove(incoming);
            Children.Add(incoming);
            front = incoming;
            back = outgoing;

            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(480)) { EasingFunction = ease };
            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(480)) { EasingFunction = ease, BeginTime = TimeSpan.FromMilliseconds(120) };
            Border done = outgoing;
            fadeOut.Completed += delegate { if (done == back) done.Background = null; };
            incoming.BeginAnimation(OpacityProperty, fadeIn);
            outgoing.BeginAnimation(OpacityProperty, fadeOut);
        }
    }

    // A small vector button (previous / play-pause / next).
    internal sealed class IconButton : Border
    {
        private readonly Path glyph;
        private readonly Brush normal;
        private readonly Brush hover;
        private bool enabled = true;

        public event Action Click;

        public IconButton(Geometry geometry, double size, Brush normal, Brush hover)
        {
            this.normal = normal;
            this.hover = hover;
            Width = size;
            Height = size;
            Background = Brushes.Transparent; // hit-testable
            Cursor = Cursors.Hand;
            glyph = new Path
            {
                Data = geometry,
                Fill = normal,
                Stretch = Stretch.Uniform,
                Width = size * 0.5,
                Height = size * 0.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                SnapsToDevicePixels = true
            };
            Child = glyph;
            MouseEnter += delegate { if (enabled) glyph.Fill = hover; };
            MouseLeave += delegate { glyph.Fill = normal; };
            MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                var handler = Click;
                if (enabled && handler != null) handler();
            };
        }

        public Geometry Glyph
        {
            set { glyph.Data = value; }
        }

        public bool Enabled
        {
            get { return enabled; }
            set
            {
                enabled = value;
                Opacity = value ? 1.0 : 0.3;
                Cursor = value ? Cursors.Hand : Cursors.Arrow;
            }
        }
    }

    internal static class Glyphs
    {
        public static readonly Geometry Play = G("M4,2 L14,8 L4,14 Z");
        public static readonly Geometry Pause = G("M3,2 L6.5,2 L6.5,14 L3,14 Z M9.5,2 L13,2 L13,14 L9.5,14 Z");
        public static readonly Geometry Previous = G("M2,2 L4,2 L4,14 L2,14 Z M14,2 L5,8 L14,14 Z");
        public static readonly Geometry Next = G("M12,2 L14,2 L14,14 L12,14 Z M2,2 L11,8 L2,14 Z");
        public static readonly Geometry Note = G("M6,2 L14,0 L14,11 A2.5,2.2 0 1 1 12.5,9 L12.5,3.2 L7.5,4.4 L7.5,13 A2.5,2.2 0 1 1 6,11 Z");

        private static Geometry G(string data)
        {
            var g = Geometry.Parse(data);
            g.Freeze();
            return g;
        }
    }

    // The floating now-playing widget.
    internal sealed class WidgetWindow : Window
    {
        private const double ShadowRoom = 30; // transparent margin so shadows are not clipped

        private readonly Settings settings;
        private readonly MediaWatcher media;
        private readonly DispatcherTimer saveTimer;
        private IntPtr handle;
        private Theme theme;
        private double scale;
        private bool hovering;
        private bool placed;
        private NowPlaying shown = NowPlaying.Empty;

        // Parts of the current visual tree (rebuilt whenever theme, size or layout changes).
        private CrossfadeImage art;
        private Path artPlaceholder;
        private CrossfadeImage backdrop;
        private FrameworkElement textBlock;
        private TextBlock label;
        private Ellipse labelDot;
        private TextBlock title;
        private TextBlock artist;
        private FrameworkElement controls;
        private IconButton playPause;
        private IconButton previous;
        private IconButton next;

        public event Action MenuRequested;

        public WidgetWindow(Settings settings, MediaWatcher media)
        {
            this.settings = settings;
            this.media = media;
            Title = AppInfo.Name;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.Manual;
            UseLayoutRounding = true;
            Topmost = settings.Topmost;
            Opacity = 0;
            Left = -32000;
            Top = -32000;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);

            saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            saveTimer.Tick += delegate { saveTimer.Stop(); SavePosition(); };

            SourceInitialized += delegate
            {
                handle = new WindowInteropHelper(this).Handle;
                Native.MakeToolWindow(handle);
                HwndSource.FromHwnd(handle).AddHook(WndProc);
            };
            Loaded += delegate { Place(); };
            LocationChanged += delegate { if (placed) { saveTimer.Stop(); saveTimer.Start(); } };
            MouseRightButtonUp += (s, e) =>
            {
                var handler = MenuRequested;
                if (handler != null) handler();
                e.Handled = true;
            };

            media.Changed += np => Display(np, true);
            Rebuild();
        }

        // ---------- placement & z-order ----------

        private void Place()
        {
            UpdateLayout();
            double w = ActualWidth, h = ActualHeight;
            double vl = SystemParameters.VirtualScreenLeft, vt = SystemParameters.VirtualScreenTop;
            double vw = SystemParameters.VirtualScreenWidth, vh = SystemParameters.VirtualScreenHeight;
            if (settings.HasPosition &&
                settings.Left + w > vl + 40 && settings.Left < vl + vw - 40 &&
                settings.Top + h > vt + 40 && settings.Top < vt + vh - 40)
            {
                Left = settings.Left;
                Top = settings.Top;
            }
            else
            {
                ResetPosition();
            }
            placed = true;
            if (!Topmost && handle != IntPtr.Zero) Native.SendToBottom(handle);
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320)));
        }

        public void ResetPosition()
        {
            Rect work = SystemParameters.WorkArea;
            UpdateLayout();
            // Top-right corner of the primary screen; the shadow margin doubles as breathing room.
            Left = work.Right - ActualWidth - 8;
            Top = work.Top + 8;
            SavePosition();
        }

        private void SavePosition()
        {
            if (!placed) return;
            settings.HasPosition = true;
            settings.Left = Left;
            settings.Top = Top;
            settings.Save();
        }

        public void ApplyTopmost()
        {
            Topmost = settings.Topmost;
            if (!Topmost && handle != IntPtr.Zero) Native.SendToBottom(handle);
        }

        // While not topmost, every z-order change is redirected to the bottom of the stack, so the
        // widget sits on the desktop beneath your windows instead of floating over them.
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Native.WM_WINDOWPOSCHANGING && !settings.Topmost && lParam != IntPtr.Zero)
            {
                var pos = (Native.WINDOWPOS)Marshal.PtrToStructure(lParam, typeof(Native.WINDOWPOS));
                if ((pos.flags & Native.SWP_NOZORDER) == 0 && pos.hwndInsertAfter != Native.HWND_BOTTOM)
                {
                    pos.hwndInsertAfter = Native.HWND_BOTTOM;
                    Marshal.StructureToPtr(pos, lParam, false);
                }
            }
            return IntPtr.Zero;
        }

        // ---------- building ----------

        public void Rebuild()
        {
            theme = Theme.Get(settings.ThemeValue);
            scale = Theme.Scale(settings.SizeValue);
            WidgetLayout layout = theme.ForceArtOnly ? WidgetLayout.ArtOnly : settings.LayoutValue;
            bool withArt = layout != WidgetLayout.TextOnly;
            bool withText = layout != WidgetLayout.ArtOnly;

            art = null; artPlaceholder = null; backdrop = null; textBlock = null;
            label = null; labelDot = null; title = null; artist = null;
            controls = null; playPause = null; previous = null; next = null;

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            FrameworkElement artElement = null;
            if (withArt)
            {
                artElement = BuildArt(!withText);
                row.Children.Add(artElement);
            }
            if (withText)
            {
                var text = BuildText();
                if (withArt) text.Margin = new Thickness(theme.Gap * scale, 0, 0, 0);
                row.Children.Add(text);
            }

            FrameworkElement body;
            if (theme.HasCard)
            {
                body = BuildCard(row);
            }
            else
            {
                body = row;
            }
            body.MouseEnter += delegate { SetHover(true); };
            body.MouseLeave += delegate { SetHover(false); };
            body.MouseLeftButtonDown += OnBodyMouseDown;

            var root = new Grid { Margin = new Thickness(ShadowRoom) };
            root.Children.Add(body);
            Content = root;

            hovering = body.IsMouseOver;
            UpdateControlsVisibility(false);
            var current = shown;
            shown = NowPlaying.Empty;
            Display(current, false);
        }

        private FrameworkElement BuildCard(FrameworkElement content)
        {
            double r = theme.CardCorner * Math.Max(1.0, scale * 0.9);
            var surface = new Grid();

            if (theme.ArtBackdrop)
            {
                // Blurred, enlarged artwork behind a dark tint: a glass card that takes on the
                // colour of whatever is playing.
                backdrop = new CrossfadeImage(0)
                {
                    Margin = new Thickness(-60),
                    Effect = new BlurEffect { Radius = 48, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Performance },
                    Opacity = 0.9
                };
                surface.Children.Add(backdrop);
                surface.Children.Add(new Border { Background = theme.BackdropTint, IsHitTestVisible = false });
            }

            var inner = new Border { Padding = new Thickness(theme.CardPadding * scale), Child = content };
            surface.Children.Add(inner);
            surface.SizeChanged += (s, e) =>
            {
                surface.Clip = new RectangleGeometry(new Rect(e.NewSize), Math.Max(0, r - 1), Math.Max(0, r - 1));
            };

            return new Border
            {
                Background = theme.CardBackground,
                BorderBrush = theme.CardBorder,
                BorderThickness = new Thickness(theme.CardBorderThickness),
                CornerRadius = new CornerRadius(r),
                Child = surface,
                Effect = new DropShadowEffect
                {
                    BlurRadius = theme.CardShadowBlur,
                    ShadowDepth = 6,
                    Direction = 270,
                    Opacity = theme.CardShadowOpacity,
                    Color = Colors.Black,
                    RenderingBias = RenderingBias.Quality
                }
            };
        }

        private FrameworkElement BuildArt(bool overlayControls)
        {
            double size = Math.Round(theme.ArtBase * scale);
            double r = theme.ArtCorner * Math.Max(1.0, scale);
            var grid = new Grid { Width = size, Height = size };

            artPlaceholder = new Path
            {
                Data = Glyphs.Note,
                Fill = theme.ArtPlaceholderMark,
                Stretch = Stretch.Uniform,
                Width = size * 0.26,
                Height = size * 0.26,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            art = new CrossfadeImage(r);
            grid.Children.Add(artPlaceholder);
            grid.Children.Add(art);

            if (overlayControls)
            {
                // Art-only: controls appear over the artwork on a soft scrim.
                var scrim = new Border
                {
                    CornerRadius = new CornerRadius(r),
                    Background = new LinearGradientBrush(Color.FromArgb(0x10, 0, 0, 0), Color.FromArgb(0x8C, 0, 0, 0), 90),
                };
                var buttons = BuildButtons(Brushes.White, Brushes.White, size < 90 ? 0.85 : 1.0);
                buttons.VerticalAlignment = VerticalAlignment.Bottom;
                buttons.HorizontalAlignment = HorizontalAlignment.Center;
                buttons.Margin = new Thickness(0, 0, 0, Math.Round(size * 0.06));
                var overlay = new Grid();
                overlay.Children.Add(scrim);
                overlay.Children.Add(buttons);
                controls = overlay;
                grid.Children.Add(overlay);
            }

            return new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(r),
                Background = theme.ArtPlaceholder,
                Child = grid,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect
                {
                    BlurRadius = theme.ArtShadowBlur * scale,
                    ShadowDepth = 3 * scale,
                    Direction = 270,
                    Opacity = theme.ArtShadowOpacity,
                    Color = Colors.Black,
                    RenderingBias = RenderingBias.Quality
                }
            };
        }

        private FrameworkElement BuildText()
        {
            double width = Math.Round(theme.TextWidth * scale);
            var stack = new StackPanel { Width = width, VerticalAlignment = VerticalAlignment.Center };

            var labelRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8 * scale) };
            double dot = Math.Max(4, Math.Round(5 * scale));
            labelDot = new Ellipse { Width = dot, Height = dot, Fill = theme.Accent, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7 * scale, 0) };
            label = new TextBlock
            {
                FontFamily = theme.LabelFont,
                FontSize = theme.LabelSize * scale,
                Foreground = theme.LabelBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = width - dot - 8
            };
            labelRow.Children.Add(labelDot);
            labelRow.Children.Add(label);

            double titleSize = theme.TitleSize * scale;
            title = new TextBlock
            {
                FontFamily = theme.TitleFont,
                FontWeight = theme.TitleWeight,
                FontSize = titleSize,
                Foreground = theme.TitleBrush,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                LineHeight = Math.Round(titleSize * 1.28),
                MaxHeight = Math.Round(titleSize * 1.28) * 2 + 1
            };
            artist = new TextBlock
            {
                FontFamily = theme.ArtistFont,
                FontSize = theme.ArtistSize * scale,
                Foreground = theme.ArtistBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 5 * scale, 0, 0)
            };

            var words = new StackPanel();
            words.Children.Add(labelRow);
            words.Children.Add(title);
            words.Children.Add(artist);
            textBlock = words;
            stack.Children.Add(words);

            var buttons = BuildButtons(theme.ControlBrush, theme.ControlHover, 1.0);
            buttons.HorizontalAlignment = HorizontalAlignment.Left;
            buttons.Margin = new Thickness(-6 * scale, 10 * scale, 0, 0);
            controls = buttons;
            stack.Children.Add(buttons);
            return stack;
        }

        private StackPanel BuildButtons(Brush normal, Brush hover, double factor)
        {
            double b = Math.Round(28 * scale * factor);
            previous = new IconButton(Glyphs.Previous, b, normal, hover);
            playPause = new IconButton(Glyphs.Play, b * 1.15, normal, hover);
            next = new IconButton(Glyphs.Next, b, normal, hover);
            previous.Click += media.Previous;
            playPause.Click += media.PlayPause;
            next.Click += media.Next;
            previous.ToolTip = "上一首";
            playPause.ToolTip = "播放 / 暂停";
            next.ToolTip = "下一首";
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(previous);
            row.Children.Add(playPause);
            row.Children.Add(next);
            return row;
        }

        // ---------- state ----------

        private void OnBodyMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed) return;
            try { DragMove(); }
            catch (InvalidOperationException) { }
        }

        private void SetHover(bool value)
        {
            hovering = value;
            UpdateControlsVisibility(true);
        }

        private void UpdateControlsVisibility(bool animate)
        {
            if (controls == null) return;
            bool allowed = settings.ShowControls && shown.HasSession;
            controls.Visibility = settings.ShowControls ? Visibility.Visible : Visibility.Collapsed;
            controls.IsHitTestVisible = allowed && hovering;
            double target = allowed && hovering ? 1 : 0;
            if (animate)
                controls.BeginAnimation(OpacityProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(target > 0 ? 180 : 260)));
            else
            {
                controls.BeginAnimation(OpacityProperty, null);
                controls.Opacity = target;
            }
        }

        private void Display(NowPlaying np, bool animate)
        {
            var old = shown;
            shown = np;
            bool textChanged = old.TextKey != np.TextKey || old.HasSession != np.HasSession || !animate;

            if (art != null) art.Set(np.Art, np.Art == null ? "" : np.ArtKey, animate);
            if (artPlaceholder != null) artPlaceholder.Visibility = np.Art == null ? Visibility.Visible : Visibility.Collapsed;
            if (backdrop != null) backdrop.Set(np.Art, np.Art == null ? "" : np.ArtKey, animate);

            if (textBlock != null)
            {
                if (textChanged && animate)
                {
                    var outAnim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(160));
                    outAnim.Completed += delegate
                    {
                        if (shown != np) return; // superseded by a newer track
                        FillText(np);
                        textBlock.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                    };
                    textBlock.BeginAnimation(OpacityProperty, outAnim);
                }
                else
                {
                    FillText(np);
                }
            }

            if (playPause != null)
            {
                playPause.Glyph = np.IsPlaying ? Glyphs.Pause : Glyphs.Play;
                playPause.Enabled = np.CanPlayPause;
                previous.Enabled = np.CanPrevious;
                next.Enabled = np.CanNext;
            }
            UpdateControlsVisibility(animate);
            ToolTip = np.HasSession ? (np.Title + (np.Artist.Length > 0 ? " — " + np.Artist : "")) : null;
        }

        private void FillText(NowPlaying np)
        {
            if (!np.HasSession)
            {
                label.Text = "NOTHING PLAYING";
                labelDot.Fill = theme.AccentIdle;
                title.Text = "此刻安静";
                artist.Text = "在 Spotify、Apple Music 或浏览器里播放音乐";
                return;
            }
            string state = np.IsPlaying ? "NOW PLAYING" : "PAUSED";
            label.Text = np.Source.Length > 0 ? state + " · " + np.Source.ToUpperInvariant() : state;
            labelDot.Fill = np.IsPlaying ? theme.Accent : theme.AccentIdle;
            title.Text = np.Title.Length > 0 ? np.Title : "未知曲目";
            artist.Text = np.Artist.Length > 0 ? np.Artist : (np.Album.Length > 0 ? np.Album : " ");
        }
    }
}
