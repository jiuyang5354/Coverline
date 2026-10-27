using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Coverline
{
    // What actually ended up behind the Liquid Glass card.
    internal enum BackdropResult
    {
        SystemAcrylic, // Windows 11 22H2+: DWMWA_SYSTEMBACKDROP_TYPE = transient window (acrylic)
        Acrylic,       // Windows 10 1803+: accent policy ACCENT_ENABLE_ACRYLICBLURBEHIND
        Blur,          // accent policy ACCENT_ENABLE_BLURBEHIND (plain blur, no luminosity)
        Solid          // transparency effects off, or no backdrop API worked: opaque panel
    }

    // Windows personalisation switches the glass has to respect.
    internal static class SystemLook
    {
        private const string Personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        // Settings > Personalisation > Colours > Transparency effects.
        public static bool TransparencyEnabled
        {
            get { return ReadDword("EnableTransparency", 1) != 0; }
        }

        // Settings > Personalisation > Colours > Choose your default app mode.
        public static bool AppsUseLightTheme
        {
            get { return ReadDword("AppsUseLightTheme", 1) != 0; }
        }

        private static int ReadDword(string name, int fallback)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(Personalize))
                {
                    if (key == null) return fallback;
                    object v = key.GetValue(name);
                    return v is int ? (int)v : fallback;
                }
            }
            catch (Exception) { return fallback; }
        }

        // Which backdrop Liquid Glass will get for a given preference, without touching a window.
        public static BackdropResult Predict(GlassBackdrop wanted)
        {
            if (!TransparencyEnabled || wanted == GlassBackdrop.Off) return BackdropResult.Solid;
            if (wanted != GlassBackdrop.Blur && Native.WindowsBuild >= 22621) return BackdropResult.SystemAcrylic;
            if (wanted != GlassBackdrop.Blur && Native.WindowsBuild >= 17134) return BackdropResult.Acrylic;
            return BackdropResult.Blur;
        }
    }

    internal static class CoverColor
    {
        private static string lastKey;
        private static Color lastColor;

        // Average colour of the artwork, favouring saturated pixels, pulled toward a mid lightness
        // so it reads as a tint rather than a stain. Alpha is 255; the caller sets the strength.
        public static Color Of(BitmapSource art, string key)
        {
            if (art == null) return Colors.Transparent;
            if (key != null && key == lastKey) return lastColor;
            Color result = Colors.Transparent;
            try
            {
                BitmapSource src = art;
                if (src.PixelWidth > 24 || src.PixelHeight > 24)
                    src = new TransformedBitmap(art, new ScaleTransform(24.0 / art.PixelWidth, 24.0 / art.PixelHeight));
                var bgra = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
                int w = bgra.PixelWidth, h = bgra.PixelHeight;
                var px = new byte[w * h * 4];
                bgra.CopyPixels(px, w * 4, 0);
                double r = 0, g = 0, b = 0, n = 0;
                for (int i = 0; i < px.Length; i += 4)
                {
                    double pb = px[i] / 255.0, pg = px[i + 1] / 255.0, pr = px[i + 2] / 255.0, pa = px[i + 3] / 255.0;
                    double max = Math.Max(pr, Math.Max(pg, pb)), min = Math.Min(pr, Math.Min(pg, pb));
                    double weight = pa * (0.25 + (max - min));
                    r += pr * weight; g += pg * weight; b += pb * weight; n += weight;
                }
                if (n > 0)
                {
                    r /= n; g /= n; b /= n;
                    double l = 0.299 * r + 0.587 * g + 0.114 * b;
                    double target = Math.Max(0.38, Math.Min(0.62, l));
                    double shift = target - l;
                    result = Color.FromRgb(Byte(r + shift), Byte(g + shift), Byte(b + shift));
                }
            }
            catch (Exception ex)
            {
                Log.Write("cover colour: " + ex.Message);
            }
            lastKey = key;
            lastColor = result;
            return result;
        }

        private static byte Byte(double v)
        {
            return (byte)Math.Round(Math.Max(0, Math.Min(1, v)) * 255);
        }
    }
}
