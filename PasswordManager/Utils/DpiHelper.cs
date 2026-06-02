using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PasswordManager.Utils
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

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, MonitorDpiType dpiType, out uint dpiX, out uint dpiY);

        private const int LOGPIXELSX = 88;
        private const int DPI_AWARENESS_CONTEXT_UNAWARE = -1;
        private const int DPI_AWARENESS_CONTEXT_SYSTEM_AWARE = -2;
        private const int DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE = -3;
        private const int DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4;

        private static float _dpiScale = 1.0f;
        private static bool _isInitialized = false;
        
        private static readonly HashSet<IntPtr> _scaledControls = new HashSet<IntPtr>();

        public enum MonitorDpiType
        {
            MDT_EFFECTIVE_DPI = 0,
            MDT_ANGULAR_DPI = 1,
            MDT_RAW_DPI = 2
        }

        public static void InitializeDpiAwareness()
        {
            if (_isInitialized) return;

            try
            {
                if (Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 17134)
                {
                    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
                }
                else if (Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 14393)
                {
                    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE);
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

        public static float GetDpiScaleForWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                return GetDpiScale();
            }

            try
            {
                IntPtr hMonitor = MonitorFromWindow(hWnd, 0);
                uint dpiX, dpiY;
                if (GetDpiForMonitor(hMonitor, MonitorDpiType.MDT_EFFECTIVE_DPI, out dpiX, out dpiY) == 0)
                {
                    return dpiX / 96.0f;
                }
            }
            catch
            {
            }

            return GetDpiScale();
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
            if (control == null || control.IsDisposed) 
            {
                Logger.Debug($"ApplyDpiScale: control is null or disposed");
                return;
            }
            
            IntPtr handle = control.Handle;
            string controlName = control.GetType().Name;
            
            if (_scaledControls.Contains(handle)) 
            {
                Logger.Debug($"ApplyDpiScale: {controlName} (handle:{handle}) already scaled, skipping");
                return;
            }
            
            float scale = GetDpiScale();
            Logger.Debug($"ApplyDpiScale: {controlName} (handle:{handle}), scale={scale:F2}, size before={control.Size}");
            
            if (Math.Abs(scale - 1.0f) < 0.01f) 
            {
                Logger.Debug($"ApplyDpiScale: {controlName} scale is 1.0, no scaling needed");
                return;
            }

            control.Scale(new System.Drawing.SizeF(scale, scale));
            _scaledControls.Add(handle);
            Logger.Debug($"ApplyDpiScale: {controlName} scaled successfully, size after={control.Size}");
        }
        
        public static void ClearScaledControl(IntPtr handle)
        {
            _scaledControls.Remove(handle);
        }
    }
}