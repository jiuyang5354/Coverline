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

    // A small vector button (previous / play-pause / next). With a halo brush it becomes the
    // Liquid Glass variant: a soft light disc fades in on hover and the button presses in and
    // springs back.
    internal sealed class IconButton : Border
    {
        private readonly Path glyph;
        private readonly Brush normal;
        private readonly Brush hover;
        private readonly Ellipse halo;
        private readonly ScaleTransform press;
        private bool enabled = true;

        public event Action Click;

        public IconButton(Geometry geometry, double size, Brush normal, Brush hover)
            : this(geometry, size, normal, hover, null)
        {
        }

        public IconButton(Geometry geometry, double size, Brush normal, Brush hover, Brush haloBrush)
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
                Width = size * (haloBrush != null ? 0.40 : 0.5),
                Height = size * (haloBrush != null ? 0.40 : 0.5),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                SnapsToDevicePixels = true
            };
            if (haloBrush != null)
            {
                halo = new Ellipse { Fill = haloBrush, Opacity = 0, IsHitTestVisible = false };
                var g = new Grid();
                g.Children.Add(halo);
                g.Children.Add(glyph);
                Child = g;
                press = new ScaleTransform(1, 1);
                RenderTransformOrigin = new Point(0.5, 0.5);
                RenderTransform = press;
            }
            else
            {
                Child = glyph;
            }
            MouseEnter += delegate
            {
                if (!enabled) return;
                if (halo != null) Fade(halo, 1, 140); else glyph.Fill = hover;
            };
            MouseLeave += delegate
            {
                if (halo != null) { Fade(halo, 0, 240); Spring(); }
                else glyph.Fill = normal;
            };
            MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                if (enabled && press != null) Scale(0.86, 90, new QuadraticEase { EasingMode = EasingMode.EaseOut });
            };
            MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                if (press != null) Spring();
                var handler = Click;
                if (enabled && handler != null) handler();
            };
        }

        private static void Fade(UIElement element, double to, int ms)
        {
            element.BeginAnimation(OpacityProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }

        // Restrained spring: one small overshoot, settles in ~0.4 s.
        private void Spring()
        {
            if (press == null) return;
            Scale(1.0, 420, new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 1, Springiness = 6 });
        }

        private void Scale(double to, int ms, IEasingFunction ease)
        {
            var a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease };
            press.BeginAnimation(ScaleTransform.ScaleXProperty, a);
            press.BeginAnimation(ScaleTransform.ScaleYProperty, a);
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
        private const double EdgeGap = 24;    // distance from the work-area edge for the default spot

        private readonly Settings settings;
        private readonly MediaWatcher media;
        private readonly DispatcherTimer saveTimer;
        private IntPtr handle;
        private Theme theme;
        private double scale;
        private bool hovering;
        private bool placed;
        private NowPlaying shown = NowPlaying.Empty;
        private readonly bool glassWindow;     // Liquid Glass: non-layered window over a DWM backdrop
        private readonly Action<NowPlaying> mediaHandler;
        private BackdropResult backdropResult = BackdropResult.Solid;
        private double cardRadius;
        private bool textFading;               // a text fade-out is running; it fills in `shown` when done
        private FrameworkElement rootElement;
        private SolidColorBrush coverTint;
        private string coverKey;

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
            // Liquid Glass needs a non-layered window, otherwise DWM draws no backdrop behind it
            // (see the comment above Native.ExtendFrame). AllowsTransparency cannot change once the
            // window exists, so switching to or from Liquid Glass recreates the window.
            glassWindow = WantsGlassWindow(settings);
            AllowsTransparency = !glassWindow;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.Manual;
            UseLayoutRounding = true;
            Topmost = settings.Topmost;
            if (!glassWindow) Opacity = 0; // Window.Opacity only works on layered windows; glass fades its content instead
            Left = -32000;
            Top = -32000;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);

            saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            saveTimer.Tick += delegate { saveTimer.Stop(); SavePosition(); };

            SourceInitialized += delegate
            {
                handle = new WindowInteropHelper(this).Handle;
                Native.MakeToolWindow(handle);
                var source = HwndSource.FromHwnd(handle);
                source.AddHook(WndProc);
                if (glassWindow)
                {
                    // Clear WPF's render target to transparent and let DWM paint the whole client
                    // area; then pick the backdrop and rebuild with the matching tint.
                    source.CompositionTarget.BackgroundColor = Colors.Transparent;
                    Native.ExtendFrame(handle);
                    Rebuild();
                }
            };
            Loaded += delegate { Place(); };
            SizeChanged += delegate { UpdateRegion(); };
            DpiChanged += delegate { UpdateRegion(); };
            LocationChanged += delegate { if (placed) { saveTimer.Stop(); saveTimer.Start(); } };
            MouseRightButtonUp += (s, e) =>
            {
                var handler = MenuRequested;
                if (handler != null) handler();
                e.Handled = true;
            };

            mediaHandler = np => Display(np, true);
            media.Changed += mediaHandler;
            Closed += delegate { media.Changed -= mediaHandler; };
            Rebuild();
        }

        public static bool WantsGlassWindow(Settings s)
        {
            return s.ThemeValue == ThemeKind.LiquidGlass;
        }

        public bool UsesGlassWindow
        {
            get { return glassWindow; }
        }

        // Transparent margin between the window edge and the visible card.
        public double CardInset
        {
            get { return glassWindow ? 0 : ShadowRoom; }
        }

        // ---------- glass backdrop ----------

        private BackdropResult ApplyBackdrop()
        {
            GlassBackdrop wanted = settings.BackdropValue;
            bool dark = !SystemLook.AppsUseLightTheme;
            Native.ClearBackdrop(handle);
            if (!SystemLook.TransparencyEnabled || wanted == GlassBackdrop.Off) return BackdropResult.Solid;
            if (wanted != GlassBackdrop.Blur && Native.TrySystemAcrylic(handle, dark))
            {
                // The widget never takes focus (WS_EX_NOACTIVATE), and DWM shows the flat
                // "inactive" fallback for inactive windows; WndProc keeps the frame painted active.
                Native.SendMessage(handle, Native.WM_NCACTIVATE, new IntPtr(1), IntPtr.Zero);
                return BackdropResult.SystemAcrylic;
            }
            if (wanted != GlassBackdrop.Blur && Native.WindowsBuild >= 17134 &&
                Native.SetAccent(handle, Native.ACCENT_ENABLE_ACRYLICBLURBEHIND, dark ? 0xB0202024u : 0xB8F3F3F5u))
                return BackdropResult.Acrylic;
            if (Native.SetAccent(handle, Native.ACCENT_ENABLE_BLURBEHIND, 0)) return BackdropResult.Blur;
            Native.ClearBackdrop(handle);
            return BackdropResult.Solid;
        }

        // Rounded window region: clips the DWM backdrop to the card's large corner radius.
        private void UpdateRegion()
        {
            if (!glassWindow || handle == IntPtr.Zero || cardRadius <= 0) return;
            var source = PresentationSource.FromVisual(this);
            if (source == null || source.CompositionTarget == null) return;
            Matrix m = source.CompositionTarget.TransformToDevice;
            int w = (int)Math.Round(ActualWidth * m.M11);
            int h = (int)Math.Round(ActualHeight * m.M22);
            int d = (int)Math.Round(cardRadius * 2 * m.M11);
            if (w > 0 && h > 0) Native.SetRoundRegion(handle, w, h, d);
        }

        // ---------- placement & z-order ----------

        private void Place()
        {
            UpdateLayout();
            double w = ActualWidth, h = ActualHeight;
            double vl = SystemParameters.VirtualScreenLeft, vt = SystemParameters.VirtualScreenTop;
            double vw = SystemParameters.VirtualScreenWidth, vh = SystemParameters.VirtualScreenHeight;
            if (settings.HasPosition && settings.UserMoved &&
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
            UpdateRegion();
            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320));
            if (glassWindow) { if (rootElement != null) rootElement.BeginAnimation(OpacityProperty, fade); }
            else BeginAnimation(OpacityProperty, fade);
        }

        // Default spot: bottom centre of the primary work area, just above the taskbar. Desktop
        // icons fill columns down the left and right edges, so both top corners (v0.1's spot) and
        // the bottom-right corner tend to cover them; the bottom middle is usually empty desktop.
        public void ResetPosition()
        {
            Rect work = SystemParameters.WorkArea;
            UpdateLayout();
            double inset = CardInset;
            Left = Math.Round(work.Left + (work.Width - ActualWidth) / 2);
            Top = Math.Round(work.Bottom - ActualHeight + inset - EdgeGap);
            settings.UserMoved = false;
            SavePosition();
        }

        // Moves the window so its visible card lands on the given screen point (theme switches).
        public void PlaceCardAt(double cardLeft, double cardTop)
        {
            settings.HasPosition = true;
            settings.Left = cardLeft - CardInset;
            settings.Top = cardTop - CardInset;
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
            if (msg == Native.WM_NCACTIVATE && glassWindow && backdropResult == BackdropResult.SystemAcrylic && wParam == IntPtr.Zero)
            {
                // Paint the frame as active even when another window has focus, so the system
                // acrylic stays live instead of dropping to its flat inactive colour.
                handled = true;
                return Native.DefWindowProc(hwnd, msg, new IntPtr(1), lParam);
            }
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
            if (glassWindow)
                backdropResult = handle != IntPtr.Zero ? ApplyBackdrop() : SystemLook.Predict(settings.BackdropValue);
            theme = Theme.Get(settings.ThemeValue, backdropResult);
            coverTint = null;
            coverKey = null;
            cardRadius = 0;
            textFading = false;
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
            if (theme.Liquid)
            {
                body = BuildGlassCard(row);
            }
            else if (theme.HasCard)
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

            var root = new Grid { Margin = new Thickness(CardInset) };
            root.Children.Add(body);
            if (glassWindow && !placed) root.Opacity = 0; // faded in by Place()
            rootElement = root;
            Content = root;

            hovering = body.IsMouseOver;
            UpdateControlsVisibility(false);
            var current = shown;
            shown = NowPlaying.Empty;
            Display(current, false);
            UpdateRegion();
        }

        // Liquid Glass card, back to front: tint, album colour, light falloff, specular arc,
        // content, then the bevel rims on top. No outer shadow: the window region that rounds the
        // DWM backdrop also clips anything drawn outside the card.
        private FrameworkElement BuildGlassCard(FrameworkElement content)
        {
            double r = Math.Round(theme.CardCorner * Math.Max(1.0, scale * 0.9));
            cardRadius = r;
            var corner = new CornerRadius(r);
            var surface = new Grid();

            surface.Children.Add(new Border { Background = theme.GlassTint, CornerRadius = corner });
            coverTint = new SolidColorBrush(Colors.Transparent);
            surface.Children.Add(new Border { Background = coverTint, Opacity = theme.CoverTintOpacity, CornerRadius = corner, IsHitTestVisible = false });
            surface.Children.Add(new Border { Background = theme.Sheen, CornerRadius = corner, IsHitTestVisible = false });
            surface.Children.Add(new Border { Background = theme.Specular, CornerRadius = corner, IsHitTestVisible = false });

            // Thin bright stroke riding the top-left bevel, fading out along both edges.
            double i = 1.5, reach = r * 2.6;
            var fig = new PathFigure { StartPoint = new Point(i, r + r * 1.4) };
            fig.Segments.Add(new LineSegment(new Point(i, r), true));
            fig.Segments.Add(new ArcSegment(new Point(r, i), new Size(r - i, r - i), 0, false, SweepDirection.Clockwise, true));
            fig.Segments.Add(new LineSegment(new Point(r + reach, i), true));
            var geo = new PathGeometry();
            geo.Figures.Add(fig);
            var lineBrush = new LinearGradientBrush { MappingMode = BrushMappingMode.Absolute, StartPoint = new Point(0, r * 2.4), EndPoint = new Point(r + reach, 0) };
            Color c = theme.SpecularLine;
            lineBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 0));
            lineBrush.GradientStops.Add(new GradientStop(Color.FromArgb(255, c.R, c.G, c.B), 0.42));
            lineBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1));
            surface.Children.Add(new Path
            {
                Data = geo,
                Stroke = lineBrush,
                StrokeThickness = 1.2,
                Opacity = theme.SpecularLineOpacity,
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Effect = new BlurEffect { Radius = 1.5 }
            });

            surface.Children.Add(new Border { Padding = new Thickness(theme.CardPadding * scale), Child = content });

            surface.Children.Add(new Border { BorderBrush = theme.Rim, BorderThickness = new Thickness(1), CornerRadius = corner, IsHitTestVisible = false });
            surface.Children.Add(new Border { BorderBrush = theme.RimInner, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(Math.Max(0, r - 1)), Margin = new Thickness(1), IsHitTestVisible = false });

            surface.SizeChanged += (s, e) =>
            {
                surface.Clip = new RectangleGeometry(new Rect(e.NewSize), r, r);
            };
            return surface;
        }

        private void UpdateCoverTint(NowPlaying np, bool animate)
        {
            if (coverTint == null) return;
            string key = np.Art == null ? "" : np.ArtKey;
            if (key == coverKey) return;
            coverKey = key;
            Color target = CoverColor.Of(np.Art, key);
            if (np.Art == null) target = Color.FromArgb(0, coverTint.Color.R, coverTint.Color.G, coverTint.Color.B);
            if (animate)
            {
                coverTint.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(target, TimeSpan.FromMilliseconds(600)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
            }
            else
            {
                coverTint.BeginAnimation(SolidColorBrush.ColorProperty, null);
                coverTint.Color = target;
            }
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

            if (overlayControls && theme.Liquid)
            {
                // Liquid Glass, art only: a dark glass capsule floats over the bottom of the art.
                Brush fill, rim, halo, glyph;
                Theme.OverArtCapsule(out fill, out rim, out halo, out glyph);
                var capsule = BuildCapsule(glyph, fill, rim, halo, size < 90 ? 0.8 : 0.9);
                capsule.VerticalAlignment = VerticalAlignment.Bottom;
                capsule.HorizontalAlignment = HorizontalAlignment.Center;
                capsule.Margin = new Thickness(0, 0, 0, Math.Round(size * 0.07));
                controls = capsule;
                grid.Children.Add(capsule);
            }
            else if (overlayControls)
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

            if (theme.ArtStroke != null)
            {
                // Hairline inner stroke: the sleeve reads as set into the glass.
                grid.Children.Insert(2, new Border { CornerRadius = new CornerRadius(r), BorderBrush = theme.ArtStroke, BorderThickness = new Thickness(1), IsHitTestVisible = false });
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

            if (theme.Liquid)
            {
                var capsule = BuildCapsule(theme.ControlBrush, theme.CapsuleFill, theme.CapsuleRim, theme.ButtonHalo, 0.86);
                capsule.HorizontalAlignment = HorizontalAlignment.Left;
                capsule.Margin = new Thickness(0, 9 * scale, 0, 0);
                controls = capsule;
                stack.Children.Add(capsule);
            }
            else
            {
                var buttons = BuildButtons(theme.ControlBrush, theme.ControlHover, 1.0);
                buttons.HorizontalAlignment = HorizontalAlignment.Left;
                buttons.Margin = new Thickness(-6 * scale, 10 * scale, 0, 0);
                controls = buttons;
                stack.Children.Add(buttons);
            }

            if (theme.TextScrim == null) return stack;

            // A soft, edge-less halo behind the words (light glass: brighter, dark glass: darker)
            // keeps the type legible over busy or high-contrast wallpapers.
            Color sc = ((SolidColorBrush)theme.TextScrim).Color;
            var halo = new RadialGradientBrush { Center = new Point(0.45, 0.5), GradientOrigin = new Point(0.45, 0.5), RadiusX = 0.62, RadiusY = 0.62 };
            halo.GradientStops.Add(new GradientStop(sc, 0));
            halo.GradientStops.Add(new GradientStop(sc, 0.55));
            halo.GradientStops.Add(new GradientStop(Color.FromArgb(0, sc.R, sc.G, sc.B), 1));
            var wrap = new Grid { VerticalAlignment = VerticalAlignment.Center };
            wrap.Children.Add(new Border { Background = halo, Margin = new Thickness(-12 * scale, -10 * scale, -8 * scale, -10 * scale), IsHitTestVisible = false });
            wrap.Children.Add(stack);
            return wrap;
        }

        // Liquid Glass control capsule: three buttons on a small pill of glass.
        private Border BuildCapsule(Brush glyph, Brush fill, Brush rim, Brush halo, double factor)
        {
            double b = Math.Round(28 * scale * factor);
            previous = new IconButton(Glyphs.Previous, b, glyph, glyph, halo);
            playPause = new IconButton(Glyphs.Play, Math.Round(b * 1.12), glyph, glyph, halo);
            next = new IconButton(Glyphs.Next, b, glyph, glyph, halo);
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
            double h = Math.Round(b * 1.12) + 4;
            return new Border
            {
                Background = fill,
                BorderBrush = rim,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(h / 2),
                Padding = new Thickness(Math.Round(4 * scale), 1, Math.Round(4 * scale), 1),
                Child = row
            };
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
            double l = Left, t = Top;
            try { DragMove(); }
            catch (InvalidOperationException) { }
            if (Math.Abs(Left - l) > 0.5 || Math.Abs(Top - t) > 0.5)
            {
                settings.UserMoved = true; // from now on the position the user chose is kept
                SavePosition();
            }
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
            UpdateCoverTint(np, animate);

            // v0.1 bug (the "blank card"): players such as QQ Music publish several snapshots in a
            // burst (title, then artwork, then playback state). The fade-out's completion handler
            // bailed out whenever a newer snapshot had arrived ("shown != np"), and the newer
            // snapshot had the same text so it never started a fade-in, leaving the title, artist
            // and label held at opacity 0 by the finished fade-out. Now one fade runs at a time,
            // it always fades back in with whatever is newest, and the no-fade path clears any
            // held animation.
            if (textBlock != null)
            {
                if (textChanged && animate)
                {
                    if (!textFading)
                    {
                        textFading = true;
                        FrameworkElement target = textBlock;
                        var outAnim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(160));
                        outAnim.Completed += delegate
                        {
                            if (target != textBlock) return; // widget was rebuilt meanwhile
                            textFading = false;
                            FillText(shown);
                            textBlock.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                        };
                        textBlock.BeginAnimation(OpacityProperty, outAnim);
                    }
                    // else: the running fade-out will fill in `shown` (this snapshot) when it ends.
                }
                else if (!textFading)
                {
                    textBlock.BeginAnimation(OpacityProperty, null);
                    textBlock.Opacity = 1;
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
