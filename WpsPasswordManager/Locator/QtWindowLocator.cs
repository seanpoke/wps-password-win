using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using WpsPasswordManager.Utils;

namespace WpsPasswordManager.Locator
{
    public class QtWindowLocator
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
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private readonly List<string> _passwordDialogTitles = new List<string>
        {
            "文档已加密",
            "密码加密"
        };

        private readonly List<string> _qtClassNames = new List<string>
        {
            "Qt5QWindowIcon",
            "Qt5QWindow", 
            "Qt5Dialog",
            "#32770",
            "QDialog",
            "QWidget"
        };

        public IntPtr FindPasswordDialog()
        {
            IntPtr handle = FindByDirectSearch();
            if (handle != IntPtr.Zero)
            {
                Logger.Info($"通过直接搜索找到密码对话框: {handle}");
                return handle;
            }

            handle = FindByWpsProcessWindows();
            if (handle != IntPtr.Zero)
            {
                Logger.Info($"通过WPS进程窗口枚举找到密码对话框: {handle}");
                return handle;
            }

            handle = FindByExtendedSearch();
            if (handle != IntPtr.Zero)
            {
                Logger.Info($"通过扩展搜索找到密码对话框: {handle}");
                return handle;
            }

            return IntPtr.Zero;
        }

        private IntPtr FindByDirectSearch()
        {
            foreach (string title in _passwordDialogTitles)
            {
                foreach (string className in _qtClassNames)
                {
                    IntPtr handle = FindWindow(className, title);
                    if (handle != IntPtr.Zero && IsValidPasswordDialog(handle))
                    {
                        return handle;
                    }
                }

                IntPtr handleWithoutClass = FindWindow(null, title);
                if (handleWithoutClass != IntPtr.Zero && IsValidPasswordDialog(handleWithoutClass))
                {
                    return handleWithoutClass;
                }
            }
            return IntPtr.Zero;
        }

        private IntPtr FindByWpsProcessWindows()
        {
            HashSet<int> wpsProcessIds = GetWpsProcessIds();
            if (wpsProcessIds.Count == 0)
                return IntPtr.Zero;

            IntPtr foundHandle = IntPtr.Zero;
            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd))
                    return true;

                StringBuilder windowTitle = new StringBuilder(256);
                GetWindowText(hWnd, windowTitle, windowTitle.Capacity);
                string title = windowTitle.ToString();

                if (title != "密码加密" && title != "文档已加密")
                    return true;

                StringBuilder className = new StringBuilder(256);
                GetClassName(hWnd, className, className.Capacity);
                string classStr = className.ToString();

                uint processId;
                GetWindowThreadProcessId(hWnd, out processId);

                if (wpsProcessIds.Contains((int)processId))
                {
                    Logger.Debug($"找到WPS窗口: 句柄={hWnd}, 标题={title}, 类名={classStr}");
                    if (IsValidPasswordDialog(hWnd))
                    {
                        foundHandle = hWnd;
                        return false;
                    }
                }
                return true;
            }, IntPtr.Zero);

            return foundHandle;
        }

        private IntPtr FindByExtendedSearch()
        {
            IntPtr foundHandle = IntPtr.Zero;
            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd))
                    return true;

                StringBuilder windowTitle = new StringBuilder(256);
                GetWindowText(hWnd, windowTitle, windowTitle.Capacity);
                string title = windowTitle.ToString();

                if (title != "密码加密" && title != "文档已加密")
                    return true;

                StringBuilder className = new StringBuilder(256);
                GetClassName(hWnd, className, className.Capacity);
                string classStr = className.ToString();

                if (IsQtWindow(classStr) && IsWpsProcessWindow(hWnd))
                {
                    Logger.Debug($"通过扩展搜索找到窗口: 句柄={hWnd}, 标题={title}, 类名={classStr}");
                    if (IsValidPasswordDialog(hWnd))
                    {
                        foundHandle = hWnd;
                        return false;
                    }
                }
                return true;
            }, IntPtr.Zero);

            return foundHandle;
        }

        private bool IsValidPasswordDialog(IntPtr hWnd)
        {
            try
            {
                StringBuilder windowTitle = new StringBuilder(256);
                GetWindowText(hWnd, windowTitle, windowTitle.Capacity);
                string title = windowTitle.ToString();

                if (title != "密码加密" && title != "文档已加密")
                    return false;

                StringBuilder className = new StringBuilder(256);
                GetClassName(hWnd, className, className.Capacity);
                string classStr = className.ToString();

                if (!IsQtWindow(classStr) && !classStr.Equals("#32770"))
                    return false;

                return IsWpsProcessWindow(hWnd);
            }
            catch
            {
                return false;
            }
        }

       

        private bool IsQtWindow(string className)
        {
            if (string.IsNullOrEmpty(className))
                return false;

            string[] qtPatterns = { "Qt", "QDialog", "QWidget", "QWindow" };
            foreach (string pattern in qtPatterns)
            {
                if (className.Contains(pattern))
                    return true;
            }
            return false;
        }

        private bool IsWpsProcessWindow(IntPtr hWnd)
        {
            uint processId;
            GetWindowThreadProcessId(hWnd, out processId);

            try
            {
                Process process = Process.GetProcessById((int)processId);
                string processName = process.ProcessName.ToLower();
                return processName == "wps" || processName == "wpscloudsvr";
            }
            catch
            {
                return false;
            }
        }

        private HashSet<int> GetWpsProcessIds()
        {
            HashSet<int> processIds = new HashSet<int>();
            try
            {
                Process[] processes = Process.GetProcessesByName("wps");
                foreach (Process process in processes)
                {
                    processIds.Add(process.Id);
                }
            }
            catch { }
            return processIds;
        }

        public string GetWindowTitle(IntPtr hWnd)
        {
            StringBuilder sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public string GetWindowClassName(IntPtr hWnd)
        {
            StringBuilder sb = new StringBuilder(256);
            GetClassName(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public bool IsDecryptDialog(IntPtr hWnd)
        {
            string title = GetWindowTitle(hWnd);
            return title.Contains("文档已加密");
        }

        public bool IsEncryptDialog(IntPtr hWnd)
        {
            string title = GetWindowTitle(hWnd);
            return title.Contains("密码加密");
        }
    }
}
