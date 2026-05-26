using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using WpsPasswordManager.Utils;
using WpsPasswordManager.Locator;

#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8605, CS8618, CS8625

namespace WpsPasswordManager.Monitor
{
    public class WpsMonitor
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, MonitorDpiType dpiType, out uint dpiX, out uint dpiY);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public enum MonitorDpiType
        {
            MDT_EFFECTIVE_DPI = 0,
            MDT_ANGULAR_DPI = 1,
            MDT_RAW_DPI = 2
        }

        private readonly QtWindowLocator _qtWindowLocator = new QtWindowLocator();

        public bool IsWpsRunning()
        {
            Process[] processes = Process.GetProcessesByName("wps");
            return processes.Length > 0;
        }

        public IntPtr FindPasswordDialog()
        {
            IntPtr handle = _qtWindowLocator.FindPasswordDialog();
            if (handle != IntPtr.Zero)
            {
                return handle;
            }

            string[] dialogTitles = { "密码加密", "文档已加密" };
            foreach (string title in dialogTitles)
            {
                string[] classNames = { "Qt5QWindow", "#32770", "", "QWidget", "QDialog" };
                foreach (string className in classNames)
                {
                    IntPtr dialogHandle = FindWindow(className, title);
                    if (dialogHandle != IntPtr.Zero)
                    {
                        return dialogHandle;
                    }
                }
            }

            return FindWpsPasswordDialogByEnumeration();
        }

        private IntPtr FindWpsPasswordDialogByEnumeration()
        {
            IntPtr foundHandle = IntPtr.Zero;

            System.Collections.Generic.HashSet<int> wpsProcessIds = new System.Collections.Generic.HashSet<int>();
            try
            {
                Process[] wpsProcesses = Process.GetProcessesByName("wps");
                foreach (Process process in wpsProcesses)
                {
                    wpsProcessIds.Add(process.Id);
                }
            }
            catch { }

            if (wpsProcessIds.Count == 0)
            {
                return IntPtr.Zero;
            }

            EnumWindows((IntPtr hWnd, IntPtr lParam) =>
            {
                if (!IsWindowVisible(hWnd))
                    return true;

                StringBuilder windowTitle = new StringBuilder(256);
                GetWindowText(hWnd, windowTitle, windowTitle.Capacity);
                string title = windowTitle.ToString();

                if (title != "密码加密" && title != "文档已加密")
                {
                    return true;
                }

                uint processId;
                GetWindowThreadProcessId(hWnd, out processId);

                if (wpsProcessIds.Contains((int)processId))
                {
                    foundHandle = hWnd;
                    return false;
                }

                return true;
            }, IntPtr.Zero);

            return foundHandle;
        }

        public string GetDocumentName(IntPtr dialogHandle)
        {
            try
            {
                IntPtr activeWindow = GetForegroundWindow();
                string docName = string.Empty;

                if (activeWindow != IntPtr.Zero)
                {
                    StringBuilder windowTitle = new StringBuilder(256);
                    GetWindowText(activeWindow, windowTitle, windowTitle.Capacity);
                    string title = windowTitle.ToString();

                    bool isEncryptDialog = title.Contains("文档已加密") || title.Contains("密码加密");

                    if (isEncryptDialog)
                    {
                        IntPtr parentWindow = GetParent(activeWindow);
                        if (parentWindow != IntPtr.Zero)
                        {
                            StringBuilder parentTitle = new StringBuilder(256);
                            GetWindowText(parentWindow, parentTitle, parentTitle.Capacity);
                            string parentWindowTitle = parentTitle.ToString();

                            if (!string.IsNullOrEmpty(parentWindowTitle) && parentWindowTitle.Contains(" - WPS Office"))
                            {
                                docName = parentWindowTitle.Replace(" - WPS Office", "");
                            }
                        }
                    }
                    else if (!string.IsNullOrEmpty(title) && title.Contains(" - WPS Office"))
                    {
                        docName = title.Replace(" - WPS Office", "");
                    }
                }

                if (string.IsNullOrEmpty(docName))
                {
                    List<string> wpsWindowTitles = new List<string>();

                    EnumWindows((IntPtr hWnd, IntPtr lParam) =>
                    {
                        StringBuilder windowTitle = new StringBuilder(256);
                        GetWindowText(hWnd, windowTitle, windowTitle.Capacity);
                        string title = windowTitle.ToString();

                        if (!string.IsNullOrEmpty(title) && title.Contains(" - WPS Office"))
                        {
                            wpsWindowTitles.Add(title);
                        }

                        return true;
                    }, IntPtr.Zero);

                    if (wpsWindowTitles.Count > 0)
                    {
                        string firstWpsWindowTitle = wpsWindowTitles[0];
                        docName = firstWpsWindowTitle.Replace(" - WPS Office", "");
                    }
                }

                return docName;
            }
            catch (Exception ex)
            {
                Logger.Error($"获取文档名称时出错: {ex.Message}");
                return string.Empty;
            }
        }

        private static readonly object _pathMatchCacheLock = new object();
        private static readonly Dictionary<string, string> _pathMatchCache = new Dictionary<string, string>();
        private static long _lastCacheCleanupTime = 0;
        private const long CACHE_CLEANUP_INTERVAL = 300000;

        public string GetDocumentPath(IntPtr dialogHandle)
        {
            try
            {
                string docName = GetDocumentName(dialogHandle);

                if (!string.IsNullOrEmpty(docName))
                {
                    CleanupCacheIfNeeded();

                    string cachedPath;
                    lock (_pathMatchCacheLock)
                    {
                        if (_pathMatchCache.TryGetValue(docName, out cachedPath))
                        {
                            if (File.Exists(cachedPath))
                            {
                                Logger.Info($"[路径匹配] 从缓存中找到文档路径: {cachedPath}");
                                return cachedPath;
                            }
                            _pathMatchCache.Remove(docName);
                        }
                    }

                    List<string> possiblePaths = GlobalState.Instance.GetPossiblePaths();
                    
                    for (int i = possiblePaths.Count - 1; i >= 0; i--)
                    {
                        string path = possiblePaths[i];
                        if (string.IsNullOrEmpty(path))
                        {
                            continue;
                        }

                        string fileName = Path.GetFileName(path);
                        string pattern = $"^{Regex.Escape(fileName)}$";
                        if (Regex.IsMatch(docName, pattern, RegexOptions.IgnoreCase))
                        {
                            if (File.Exists(path))
                            {
                                Logger.Info($"[路径匹配] 成功匹配文档路径: {path}");
                                lock (_pathMatchCacheLock)
                                {
                                    _pathMatchCache[docName] = path;
                                }
                                return path;
                            }
                        }
                    }


                }
            }
            catch (Exception ex)
            {
                Logger.Error($"获取文档路径时出错: {ex.Message}");
            }

            return null;
        }

        private void CleanupCacheIfNeeded()
        {
            long currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            if (currentTime - _lastCacheCleanupTime > CACHE_CLEANUP_INTERVAL)
            {
                lock (_pathMatchCacheLock)
                {
                    List<string> keysToRemove = new List<string>();
                    foreach (var kvp in _pathMatchCache)
                    {
                        if (!File.Exists(kvp.Value))
                        {
                            keysToRemove.Add(kvp.Key);
                        }
                    }
                    foreach (string key in keysToRemove)
                    {
                        _pathMatchCache.Remove(key);
                    }
                    _lastCacheCleanupTime = currentTime;
                    Logger.Info($"[路径匹配] 缓存清理完成，移除 {keysToRemove.Count} 条无效记录");
                }
            }
        }

        private string ResolveShortcut(string lnkPath)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                string targetPath = shortcut.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, shortcut, null) as string;
                return targetPath;
            }
            catch { }
            return string.Empty;
        }

        public float GetDpiScale()
        {
            IntPtr hWnd = Process.GetCurrentProcess().MainWindowHandle;
            if (hWnd == IntPtr.Zero)
            {
                return 1.0f;
            }

            IntPtr hMonitor = MonitorFromWindow(hWnd, 0);
            uint dpiX, dpiY;
            GetDpiForMonitor(hMonitor, MonitorDpiType.MDT_EFFECTIVE_DPI, out dpiX, out dpiY);
            return dpiX / 96.0f;
        }

        public RECT GetWindowRect(IntPtr hWnd)
        {
            RECT rect = new RECT();
            GetWindowRect(hWnd, ref rect);
            return rect;
        }
    }
}
