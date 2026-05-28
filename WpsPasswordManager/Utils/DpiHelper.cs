using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WpsPasswordManager.Utils
{
    public static class DpiHelper
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(int dpiFlag);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        private const int LOGPIXELSX = 88;
        private const int PROCESS_PER_MONITOR_DPI_AWARE = 2;

        private static float _dpiScale = 1.0f;
        private static bool _isInitialized = false;

        public static void InitializeDpiAwareness()
        {
            if (_isInitialized) return;

            try
            {
                if (Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 14393)
                {
                    SetProcessDpiAwarenessContext(PROCESS_PER_MONITOR_DPI_AWARE);
                }
                else
                {
                    SetProcessDPIAware();
                }
            }
            catch
            {
                try
                {
                    SetProcessDPIAware();
                }
                catch
                {
                }
            }

            UpdateDpiScale();
            _isInitialized = true;
        }

        public static void UpdateDpiScale()
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            if (hdc != IntPtr.Zero)
            {
                int dpi = GetDeviceCaps(hdc, LOGPIXELSX);
                _dpiScale = dpi / 96.0f;
                ReleaseDC(IntPtr.Zero, hdc);
            }
        }

        public static float GetDpiScale()
        {
            if (!_isInitialized)
            {
                InitializeDpiAwareness();
            }
            return _dpiScale;
        }

        public static int ScaleValue(int value)
        {
            return (int)(value * GetDpiScale());
        }

        public static float ScaleValue(float value)
        {
            return value * GetDpiScale();
        }

        public static System.Drawing.Size ScaleSize(System.Drawing.Size size)
        {
            return new System.Drawing.Size(ScaleValue(size.Width), ScaleValue(size.Height));
        }

        public static System.Drawing.Point ScalePoint(System.Drawing.Point point)
        {
            return new System.Drawing.Point(ScaleValue(point.X), ScaleValue(point.Y));
        }

        public static System.Drawing.Font ScaleFont(System.Drawing.Font font)
        {
            float scaledSize = ScaleValue(font.Size);
            return new System.Drawing.Font(font.FontFamily, scaledSize, font.Style);
        }

        public static void ApplyDpiScale(Control control)
        {
            float scale = GetDpiScale();
            if (Math.Abs(scale - 1.0f) < 0.01f) return;

            control.Scale(new System.Drawing.SizeF(scale, scale));
        }
    }
}