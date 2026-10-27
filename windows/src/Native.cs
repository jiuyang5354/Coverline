using System;
using System.Runtime.InteropServices;

namespace Coverline
{
    internal static class Native
    {
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int WM_WINDOWPOSCHANGING = 0x0046;
        public const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;
        public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public uint flags;
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        // Keeps the widget out of the taskbar and Alt+Tab, and stops clicks from stealing focus
        // from whatever the user is working in.
        public static void MakeToolWindow(IntPtr hwnd)
        {
            int ex = GetWindowLong32(hwnd, GWL_EXSTYLE);
            SetWindowLong32(hwnd, GWL_EXSTYLE, ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        }

        public static void SendToBottom(IntPtr hwnd)
        {
            SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        // ---------- glass backdrop (Liquid Glass theme) ----------
        //
        // Why the Liquid Glass window is NOT a WPF layered window (AllowsTransparency = true):
        // a layered window is composed from a per-pixel-alpha bitmap (UpdateLayeredWindow), and DWM
        // does not draw a system backdrop (Mica / Acrylic) behind layered windows, so the blur
        // would silently never appear. The Liquid Glass window is therefore an ordinary window
        // (AllowsTransparency = false) whose WPF render target is cleared to transparent
        // (HwndSource.CompositionTarget.BackgroundColor) with the DWM frame extended over the whole
        // client area (margins -1). Transparent WPF pixels then show whatever DWM paints behind
        // them: the Windows 11 system acrylic, or the Windows 10 accent blur. The large rounded
        // corners come from a window region, because DWM's own corner preference is fixed at
        // ~8 px; with a region set we ask DWM not to round (it would fight the region).
        // The other themes keep the layered window: they draw their own opaque card and soft
        // WPF drop shadow, which only a per-pixel-alpha window can show.

        public const int WM_NCACTIVATE = 0x0086;
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        public const int DWMWA_BORDER_COLOR = 34;
        public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        public const int DWMSBT_NONE = 1;
        public const int DWMSBT_TRANSIENTWINDOW = 3; // "Acrylic"
        public const int DWMWCP_DONOTROUND = 1;
        public const int DWMWA_COLOR_NONE = unchecked((int)0xFFFFFFFE);

        private const int WCA_ACCENT_POLICY = 19;
        public const int ACCENT_DISABLED = 0;
        public const int ACCENT_ENABLE_BLURBEHIND = 3;
        public const int ACCENT_ENABLE_ACRYLICBLURBEHIND = 4;

        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS
        {
            public int Left;
            public int Right;
            public int Top;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public int AccentState;
            public int AccentFlags;
            public int GradientColor; // AABBGGRR
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct OSVERSIONINFOEX
        {
            public int dwOSVersionInfoSize;
            public int dwMajorVersion;
            public int dwMinorVersion;
            public int dwBuildNumber;
            public int dwPlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szCSDVersion;
            public ushort wServicePackMajor;
            public ushort wServicePackMinor;
            public ushort wSuiteMask;
            public byte wProductType;
            public byte wReserved;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int w, int h);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr obj);
        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, bool redraw);
        [DllImport("user32.dll")]
        public static extern IntPtr DefWindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("ntdll.dll")]
        private static extern int RtlGetVersion(ref OSVERSIONINFOEX info);

        private static int build = -1;

        // Real Windows build number (Environment.OSVersion can be shimmed).
        public static int WindowsBuild
        {
            get
            {
                if (build >= 0) return build;
                build = 0;
                try
                {
                    var info = new OSVERSIONINFOEX();
                    info.dwOSVersionInfoSize = Marshal.SizeOf(typeof(OSVERSIONINFOEX));
                    if (RtlGetVersion(ref info) == 0) build = info.dwBuildNumber;
                }
                catch (Exception) { }
                if (build == 0) build = Environment.OSVersion.Version.Build;
                return build;
            }
        }

        private static bool SetDwmInt(IntPtr hwnd, int attribute, int value)
        {
            try { return DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int)) == 0; }
            catch (Exception) { return false; }
        }

        // Lets DWM paint behind the whole client area; transparent WPF pixels reveal the backdrop.
        public static bool ExtendFrame(IntPtr hwnd)
        {
            try
            {
                var m = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
                return DwmExtendFrameIntoClientArea(hwnd, ref m) == 0;
            }
            catch (Exception) { return false; }
        }

        // Windows 11 22H2+ (build 22621): DWM system acrylic. dark picks the dark acrylic tint.
        public static bool TrySystemAcrylic(IntPtr hwnd, bool dark)
        {
            if (WindowsBuild < 22621) return false;
            SetAccent(hwnd, ACCENT_DISABLED, 0);
            SetDwmInt(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, dark ? 1 : 0);
            SetDwmInt(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_DONOTROUND);
            SetDwmInt(hwnd, DWMWA_BORDER_COLOR, DWMWA_COLOR_NONE);
            return SetDwmInt(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, DWMSBT_TRANSIENTWINDOW);
        }

        // Windows 10 1803+ (and 11): undocumented accent policy. Acrylic or plain blur behind the
        // window, tinted by gradientArgb (0xAARRGGBB).
        public static bool SetAccent(IntPtr hwnd, int state, uint gradientArgb)
        {
            IntPtr mem = IntPtr.Zero;
            try
            {
                uint a = (gradientArgb >> 24) & 0xFF, r = (gradientArgb >> 16) & 0xFF, g = (gradientArgb >> 8) & 0xFF, b = gradientArgb & 0xFF;
                var policy = new AccentPolicy
                {
                    AccentState = state,
                    AccentFlags = 2,
                    GradientColor = unchecked((int)((a << 24) | (b << 16) | (g << 8) | r)),
                    AnimationId = 0
                };
                int size = Marshal.SizeOf(typeof(AccentPolicy));
                mem = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(policy, mem, false);
                var data = new WindowCompositionAttributeData { Attribute = WCA_ACCENT_POLICY, Data = mem, SizeOfData = size };
                return SetWindowCompositionAttribute(hwnd, ref data) != 0;
            }
            catch (Exception) { return false; }
            finally
            {
                if (mem != IntPtr.Zero) Marshal.FreeHGlobal(mem);
            }
        }

        // Removes every backdrop (opaque fallback).
        public static void ClearBackdrop(IntPtr hwnd)
        {
            if (WindowsBuild >= 22621) SetDwmInt(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, DWMSBT_NONE);
            SetAccent(hwnd, ACCENT_DISABLED, 0);
        }

        // Clips the window (and the DWM backdrop behind it) to a rounded rectangle, in device pixels.
        public static void SetRoundRegion(IntPtr hwnd, int width, int height, int diameter)
        {
            try
            {
                IntPtr rgn = CreateRoundRectRgn(0, 0, width + 1, height + 1, diameter, diameter);
                if (rgn == IntPtr.Zero) return;
                if (SetWindowRgn(hwnd, rgn, true) == 0) DeleteObject(rgn); // on success the system owns rgn
            }
            catch (Exception) { }
        }

        public static void ClearRegion(IntPtr hwnd)
        {
            try { SetWindowRgn(hwnd, IntPtr.Zero, true); }
            catch (Exception) { }
        }
    }
}
