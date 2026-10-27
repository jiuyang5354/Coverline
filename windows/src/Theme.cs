using System.Windows;
using System.Windows.Media;

namespace Coverline
{
    // A theme preset: every colour, font and proportion the widget uses. Sizes are for the
    // Medium widget and are multiplied by the size scale.
    internal sealed class Theme
    {
        public ThemeKind Kind;

        // Card
        public bool HasCard;
        public Brush CardBackground;
        public Brush CardBorder;
        public double CardBorderThickness;
        public double CardCorner;
        public double CardPadding;
        public double CardShadowOpacity;
        public double CardShadowBlur;
        public bool ArtBackdrop;         // blurred album art behind the card (glass)
        public Brush BackdropTint;

        // Artwork
        public double ArtBase;           // edge length at Medium
        public double ArtCorner;
        public double ArtShadowOpacity;
        public double ArtShadowBlur;
        public Brush ArtPlaceholder;
        public Brush ArtPlaceholderMark;
        public double Gap;               // between art and text

        // Text
        public double TextWidth;
        public FontFamily TitleFont;
        public FontWeight TitleWeight;
        public double TitleSize;
        public Brush TitleBrush;
        public FontFamily ArtistFont;
        public double ArtistSize;
        public Brush ArtistBrush;
        public FontFamily LabelFont;
        public double LabelSize;
        public Brush LabelBrush;
        public Brush Accent;
        public Brush AccentIdle;

        // Controls
        public Brush ControlBrush;
        public Brush ControlHover;
        public bool ForceArtOnly;

        // Liquid Glass only (null / 0 elsewhere). The window behind is DWM acrylic or blur; these
        // layers sit on top of it to give the material its tint, bevel and specular light.
        public bool Liquid;
        public bool Dark;
        public BackdropResult Backdrop;
        public Brush GlassTint;          // flat tint over the blurred desktop
        public double CoverTintOpacity;  // album-art average colour bleeding into the glass
        public Brush Sheen;              // top-to-bottom light falloff (bright upper edge)
        public Brush Specular;           // soft arc of light in the top-left corner
        public Color SpecularLine;       // the thin bright stroke riding the top-left bevel
        public double SpecularLineOpacity;
        public Brush Rim;                // 1px inner rim: bright top, dark bottom
        public Brush RimInner;           // second, fainter rim one pixel in (bevel thickness)
        public Brush TextScrim;          // soft halo behind the text for contrast on busy wallpapers
        public Brush ArtStroke;          // hairline inner stroke on the artwork
        public Brush CapsuleFill;
        public Brush CapsuleRim;
        public Brush ButtonHalo;

        private static readonly FontFamily Serif = new FontFamily("Noto Serif SC, Source Han Serif SC, Georgia, SimSun");
        private static readonly FontFamily Mono = new FontFamily("Cascadia Mono, Consolas");
        private static readonly FontFamily Sans = new FontFamily("Segoe UI Variable Text, Segoe UI, Microsoft YaHei UI");
        private static readonly FontFamily SansDisplay = new FontFamily("Segoe UI Variable Display, Segoe UI, Microsoft YaHei UI");

        public static Theme Get(ThemeKind kind)
        {
            return Get(kind, BackdropResult.Solid);
        }

        public static Theme Get(ThemeKind kind, BackdropResult backdrop)
        {
            switch (kind)
            {
                case ThemeKind.LiquidGlass: return LiquidGlass(!SystemLook.AppsUseLightTheme, backdrop);
                case ThemeKind.Glass: return Glass();
                case ThemeKind.ArtOnly: return ArtOnly();
                default: return Editorial();
            }
        }

        public static string NameOf(ThemeKind kind)
        {
            switch (kind)
            {
                case ThemeKind.Glass: return "暗色玻璃 Dark Glass";
                case ThemeKind.ArtOnly: return "纯封面 Art Only";
                case ThemeKind.LiquidGlass: return "液态玻璃 Liquid Glass（默认）";
                default: return "编辑部 Editorial";
            }
        }

        // Paper card, serif title, mono label, a single orange accent, lots of air.
        private static Theme Editorial()
        {
            return new Theme
            {
                Kind = ThemeKind.Editorial,
                HasCard = true,
                CardBackground = B(0xFF, 0xF6, 0xF4, 0xEF),
                CardBorder = B(0x22, 0x16, 0x16, 0x14),
                CardBorderThickness = 1,
                CardCorner = 6,
                CardPadding = 22,
                CardShadowOpacity = 0.16,
                CardShadowBlur = 28,
                ArtBase = 96,
                ArtCorner = 4,
                ArtShadowOpacity = 0.22,
                ArtShadowBlur = 14,
                ArtPlaceholder = B(0xFF, 0xE9, 0xE6, 0xDF),
                ArtPlaceholderMark = B(0xFF, 0xB4, 0xAF, 0xA6),
                Gap = 20,
                TextWidth = 188,
                TitleFont = Serif,
                TitleWeight = FontWeights.Normal,
                TitleSize = 18,
                TitleBrush = B(0xFF, 0x16, 0x16, 0x14),
                ArtistFont = Sans,
                ArtistSize = 12,
                ArtistBrush = B(0xFF, 0x6E, 0x6A, 0x63),
                LabelFont = Mono,
                LabelSize = 9.5,
                LabelBrush = B(0xFF, 0x8A, 0x86, 0x7E),
                Accent = B(0xFF, 0xFF, 0x5A, 0x1F),
                AccentIdle = B(0xFF, 0xC9, 0xC5, 0xBD),
                ControlBrush = B(0xFF, 0x16, 0x16, 0x14),
                ControlHover = B(0xFF, 0xFF, 0x5A, 0x1F)
            };
        }

        // Smoked glass tinted by the blurred artwork, white type, no accent colour.
        private static Theme Glass()
        {
            return new Theme
            {
                Kind = ThemeKind.Glass,
                HasCard = true,
                CardBackground = B(0x8C, 0x12, 0x12, 0x14),
                CardBorder = B(0x26, 0xFF, 0xFF, 0xFF),
                CardBorderThickness = 1,
                CardCorner = 18,
                CardPadding = 16,
                CardShadowOpacity = 0.35,
                CardShadowBlur = 30,
                ArtBackdrop = true,
                BackdropTint = B(0x99, 0x14, 0x14, 0x16),
                ArtBase = 96,
                ArtCorner = 10,
                ArtShadowOpacity = 0.45,
                ArtShadowBlur = 16,
                ArtPlaceholder = B(0xFF, 0x2A, 0x2A, 0x2E),
                ArtPlaceholderMark = B(0x66, 0xFF, 0xFF, 0xFF),
                Gap = 16,
                TextWidth = 184,
                TitleFont = SansDisplay,
                TitleWeight = FontWeights.SemiBold,
                TitleSize = 15,
                TitleBrush = B(0xFF, 0xFF, 0xFF, 0xFF),
                ArtistFont = Sans,
                ArtistSize = 12,
                ArtistBrush = B(0xA6, 0xFF, 0xFF, 0xFF),
                LabelFont = Mono,
                LabelSize = 9,
                LabelBrush = B(0x73, 0xFF, 0xFF, 0xFF),
                Accent = B(0xFF, 0xFF, 0xFF, 0xFF),
                AccentIdle = B(0x4D, 0xFF, 0xFF, 0xFF),
                ControlBrush = B(0xE6, 0xFF, 0xFF, 0xFF),
                ControlHover = B(0xFF, 0xFF, 0xFF, 0xFF)
            };
        }

        // Apple-style liquid glass: a whisper of tint over a real DWM blur, light falling from the
        // top edge, a bright bevel rim and a soft specular arc. Light or dark follows the Windows
        // app theme so the type is always dark-on-light or light-on-dark. Every number here is
        // mirrored in docs/liquid-glass-preview.html.
        private static Theme LiquidGlass(bool dark, BackdropResult backdrop)
        {
            byte ink = 0x1C; // light-mode text: near-black #1C1C1E
            var t = new Theme
            {
                Kind = ThemeKind.LiquidGlass,
                Liquid = true,
                Dark = dark,
                Backdrop = backdrop,
                HasCard = true,
                CardCorner = 26,
                CardPadding = 14,
                ArtBase = 96,
                ArtCorner = 12,              // concentric with the card: 26 - 14
                ArtShadowOpacity = dark ? 0.30 : 0.18,
                ArtShadowBlur = 12,
                ArtPlaceholder = dark ? B(0x33, 0xFF, 0xFF, 0xFF) : B(0x40, 0xFF, 0xFF, 0xFF),
                ArtPlaceholderMark = dark ? B(0x80, 0xFF, 0xFF, 0xFF) : B(0x66, ink, ink, 0x1E),
                Gap = 14,
                TextWidth = 184,
                TitleFont = SansDisplay,
                TitleWeight = FontWeights.SemiBold,
                TitleSize = 15,
                ArtistFont = Sans,
                ArtistSize = 12,
                LabelFont = Mono,
                LabelSize = 9,
                CoverTintOpacity = backdrop == BackdropResult.Solid ? 0.10 : (dark ? 0.20 : 0.14),
                SpecularLine = Colors.White,
                SpecularLineOpacity = dark ? 0.55 : 0.9
            };

            if (dark)
            {
                t.TitleBrush = B(0xFF, 0xFF, 0xFF, 0xFF);
                t.ArtistBrush = B(0xAD, 0xFF, 0xFF, 0xFF);
                t.LabelBrush = B(0x85, 0xFF, 0xFF, 0xFF);
                t.Accent = B(0xFF, 0xFF, 0xFF, 0xFF);
                t.AccentIdle = B(0x40, 0xFF, 0xFF, 0xFF);
                t.ControlBrush = B(0xF2, 0xFF, 0xFF, 0xFF);
                t.ControlHover = t.ControlBrush;
            }
            else
            {
                t.TitleBrush = B(0xFF, ink, ink, 0x1E);
                t.ArtistBrush = B(0xA3, ink, ink, 0x1E);
                t.LabelBrush = B(0x85, ink, ink, 0x1E);
                t.Accent = B(0xFF, ink, ink, 0x1E);
                t.AccentIdle = B(0x40, ink, ink, 0x1E);
                t.ControlBrush = B(0xE6, ink, ink, 0x1E);
                t.ControlHover = t.ControlBrush;
            }

            switch (backdrop)
            {
                case BackdropResult.Blur:     // plain blur has no luminosity layer: tint harder
                    t.GlassTint = dark ? B(0xA8, 0x1A, 0x1B, 0x1F) : B(0xA0, 0xF4, 0xF5, 0xF7);
                    t.TextScrim = dark ? B(0x38, 0, 0, 0) : B(0x4D, 0xFF, 0xFF, 0xFF);
                    break;
                case BackdropResult.Solid:    // transparency off: high-contrast opaque panel
                    t.GlassTint = dark ? B(0xFF, 0x1E, 0x1F, 0x23) : B(0xFF, 0xF3, 0xF4, 0xF6);
                    t.TextScrim = null;
                    break;
                default:                      // DWM / accent acrylic already carries luminosity
                    t.GlassTint = dark ? B(0x1A, 0x10, 0x10, 0x14) : B(0x24, 0xFF, 0xFF, 0xFF);
                    t.TextScrim = dark ? B(0x24, 0, 0, 0) : B(0x33, 0xFF, 0xFF, 0xFF);
                    break;
            }

            t.Sheen = V(dark ? A(0x1A, Colors.White) : A(0x4D, Colors.White), 0,
                        A(0x00, Colors.White), 0.48,
                        dark ? A(0x1F, Colors.Black) : A(0x08, Colors.Black), 1);
            var spec = new RadialGradientBrush
            {
                GradientOrigin = new Point(0.10, 0.0),
                Center = new Point(0.16, 0.02),
                RadiusX = 0.55,
                RadiusY = 0.95
            };
            spec.GradientStops.Add(new GradientStop(dark ? A(0x47, Colors.White) : A(0x8C, Colors.White), 0));
            spec.GradientStops.Add(new GradientStop(A(0x00, Colors.White), 1));
            spec.Freeze();
            t.Specular = spec;
            t.Rim = V(dark ? A(0x61, Colors.White) : A(0xD9, Colors.White), 0,
                      dark ? A(0x1A, Colors.White) : A(0x40, Colors.White), 0.5,
                      dark ? A(0x73, Colors.Black) : A(0x1A, Colors.Black), 1);
            t.RimInner = V(dark ? A(0x1A, Colors.White) : A(0x66, Colors.White), 0,
                           A(0x00, Colors.White), 0.35,
                           A(0x00, Colors.White), 1);
            t.ArtStroke = V(A(0x4D, Colors.White), 0, A(0x14, Colors.White), 0.5, A(0x29, Colors.Black), 1);
            t.CapsuleFill = dark ? B(0x1A, 0xFF, 0xFF, 0xFF) : B(0x61, 0xFF, 0xFF, 0xFF);
            t.CapsuleRim = V(dark ? A(0x4D, Colors.White) : A(0xE6, Colors.White), 0,
                             dark ? A(0x14, Colors.White) : A(0x33, Colors.White), 0.5,
                             dark ? A(0x4D, Colors.Black) : A(0x14, Colors.Black), 1);
            t.ButtonHalo = dark ? B(0x2E, 0xFF, 0xFF, 0xFF) : B(0xBF, 0xFF, 0xFF, 0xFF);
            return t;
        }

        // Dark glass capsule for controls laid over the artwork (Art-only layout).
        public static void OverArtCapsule(out Brush fill, out Brush rim, out Brush halo, out Brush glyph)
        {
            fill = B(0x5C, 0x16, 0x16, 0x18);
            rim = V(A(0x59, Colors.White), 0, A(0x14, Colors.White), 0.5, A(0x33, Colors.Black), 1);
            halo = B(0x38, 0xFF, 0xFF, 0xFF);
            glyph = B(0xFF, 0xFF, 0xFF, 0xFF);
        }

        private static Color A(byte alpha, Color c)
        {
            return Color.FromArgb(alpha, c.R, c.G, c.B);
        }

        // Vertical three-stop gradient, top to bottom.
        private static Brush V(Color c0, double o0, Color c1, double o1, Color c2, double o2)
        {
            var g = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            g.GradientStops.Add(new GradientStop(c0, o0));
            g.GradientStops.Add(new GradientStop(c1, o1));
            g.GradientStops.Add(new GradientStop(c2, o2));
            g.Freeze();
            return g;
        }

        // Just the record sleeve, floating on the desktop.
        private static Theme ArtOnly()
        {
            return new Theme
            {
                Kind = ThemeKind.ArtOnly,
                HasCard = false,
                ArtBase = 132,
                ArtCorner = 10,
                ArtShadowOpacity = 0.38,
                ArtShadowBlur = 26,
                ArtPlaceholder = B(0xFF, 0xE9, 0xE6, 0xDF),
                ArtPlaceholderMark = B(0xFF, 0xB4, 0xAF, 0xA6),
                Gap = 0,
                TextWidth = 0,
                TitleFont = Serif,
                TitleWeight = FontWeights.Normal,
                TitleSize = 18,
                TitleBrush = Brushes.White,
                ArtistFont = Sans,
                ArtistSize = 12,
                ArtistBrush = Brushes.White,
                LabelFont = Mono,
                LabelSize = 9,
                LabelBrush = Brushes.White,
                Accent = Brushes.White,
                AccentIdle = Brushes.White,
                ControlBrush = B(0xF2, 0xFF, 0xFF, 0xFF),
                ControlHover = B(0xFF, 0xFF, 0xFF, 0xFF),
                ForceArtOnly = true
            };
        }

        private static Brush B(byte a, byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
            brush.Freeze();
            return brush;
        }

        public static double Scale(WidgetSize size)
        {
            switch (size)
            {
                case WidgetSize.Small: return 0.8;
                case WidgetSize.Large: return 1.3;
                default: return 1.0;
            }
        }
    }
}
