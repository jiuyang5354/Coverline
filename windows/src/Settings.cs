using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using Microsoft.Win32;

namespace Coverline
{
    internal enum ThemeKind { Editorial, Glass, ArtOnly, LiquidGlass }
    internal enum WidgetSize { Small, Medium, Large }
    internal enum WidgetLayout { ArtText, ArtOnly, TextOnly }
    // What sits behind the Liquid Glass card. Auto = system acrylic on Windows 11 22H2+,
    // acrylic blur-behind on Windows 10, opaque panel when neither is available.
    internal enum GlassBackdrop { Auto, SystemAcrylic, Blur, Off }

    // Persisted as %AppData%\Coverline\settings.json. Enum values are stored as strings so the
    // file stays readable and survives reordering.
    [DataContract]
    internal sealed class Settings
    {
        [DataMember(Name = "theme")] public string Theme;
        [DataMember(Name = "size")] public string Size;
        [DataMember(Name = "layout")] public string Layout;
        [DataMember(Name = "showControls")] public bool ShowControls;
        [DataMember(Name = "topmost")] public bool Topmost;
        [DataMember(Name = "hasPosition")] public bool HasPosition;
        [DataMember(Name = "left")] public double Left;
        [DataMember(Name = "top")] public double Top;
        [DataMember(Name = "hidden")] public bool Hidden;
        [DataMember(Name = "backdrop")] public string Backdrop;
        // True once the user has dragged the widget. v0.1 files lack it, so a v0.1 auto-placed
        // position (top-right, over desktop icons) is replaced by the new default on upgrade.
        [DataMember(Name = "userMoved")] public bool UserMoved;

        public Settings()
        {
            SetDefaults();
        }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            // DataContract serializers skip the constructor; members missing from an older file keep these.
            SetDefaults();
        }

        private void SetDefaults()
        {
            // New installs start on Liquid Glass; existing settings.json files keep their theme.
            Theme = ThemeKind.LiquidGlass.ToString();
            Size = WidgetSize.Medium.ToString();
            Layout = WidgetLayout.ArtText.ToString();
            ShowControls = true;
            Topmost = false;
            HasPosition = false;
            Left = 0;
            Top = 0;
            Hidden = false;
            Backdrop = GlassBackdrop.Auto.ToString();
            UserMoved = false;
        }

        public ThemeKind ThemeValue
        {
            get { return Parse(Theme, ThemeKind.LiquidGlass); }
            set { Theme = value.ToString(); }
        }

        public WidgetSize SizeValue
        {
            get { return Parse(Size, WidgetSize.Medium); }
            set { Size = value.ToString(); }
        }

        public GlassBackdrop BackdropValue
        {
            get { return Parse(Backdrop, GlassBackdrop.Auto); }
            set { Backdrop = value.ToString(); }
        }

        public WidgetLayout LayoutValue
        {
            get { return Parse(Layout, WidgetLayout.ArtText); }
            set { Layout = value.ToString(); }
        }

        private static T Parse<T>(string s, T fallback) where T : struct
        {
            T v;
            return !string.IsNullOrEmpty(s) && Enum.TryParse(s, true, out v) ? v : fallback;
        }

        public static string Root
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppInfo.Name); }
        }

        private static string FilePath
        {
            get { return Path.Combine(Root, "settings.json"); }
        }

        public static Settings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    using (var stream = File.OpenRead(FilePath))
                    {
                        var s = new DataContractJsonSerializer(typeof(Settings)).ReadObject(stream) as Settings;
                        if (s != null)
                        {
                            if (double.IsNaN(s.Left) || double.IsInfinity(s.Left) || double.IsNaN(s.Top) || double.IsInfinity(s.Top))
                                s.HasPosition = false;
                            return s;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("settings load: " + ex.Message);
            }
            return new Settings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Root);
                using (var buffer = new MemoryStream())
                {
                    using (var writer = JsonReaderWriterFactory.CreateJsonWriter(buffer, Encoding.UTF8, false, true, "  "))
                    {
                        new DataContractJsonSerializer(typeof(Settings)).WriteObject(writer, this);
                        writer.Flush();
                    }
                    string tmp = FilePath + ".tmp";
                    File.WriteAllBytes(tmp, buffer.ToArray());
                    if (File.Exists(FilePath)) File.Delete(FilePath);
                    File.Move(tmp, FilePath);
                }
            }
            catch (Exception ex)
            {
                Log.Write("settings save: " + ex.Message);
            }
        }
    }

    internal static class Startup
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool Enabled
        {
            get
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                    return key != null && key.GetValue(AppInfo.Name) != null;
            }
            set
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (value) key.SetValue(AppInfo.Name, "\"" + Assembly.GetExecutingAssembly().Location + "\"");
                    else key.DeleteValue(AppInfo.Name, false);
                }
            }
        }
    }

    internal static class Log
    {
        public static void Write(string message)
        {
            try
            {
                Directory.CreateDirectory(Settings.Root);
                File.AppendAllText(Path.Combine(Settings.Root, "error.log"),
                    DateTime.Now.ToString("s") + " " + message + Environment.NewLine);
            }
            catch { }
        }
    }
}
