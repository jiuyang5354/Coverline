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

        private static readonly FontFamily Serif = new FontFamily("Noto Serif SC, Source Han Serif SC, Georgia, SimSun");
        private static readonly FontFamily Mono = new FontFamily("Cascadia Mono, Consolas");
        private static readonly FontFamily Sans = new FontFamily("Segoe UI Variable Text, Segoe UI, Microsoft YaHei UI");
        private static readonly FontFamily SansDisplay = new FontFamily("Segoe UI Variable Display, Segoe UI, Microsoft YaHei UI");

        public static Theme Get(ThemeKind kind)
        {
            switch (kind)
            {
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
                default: return "编辑部 Editorial（默认）";
            }
        }

        // Paper card, serif title, mono label, a single orange accent, lots of air.
        private static Theme Editorial()
        {
            return new Theme
            {
                Kind = ThemeKind.Editorial,
                HasCard = true,
                CardBackground = B(0xF5, 0xF6, 0xF4, 0xEF),
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
