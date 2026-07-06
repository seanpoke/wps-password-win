using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using PasswordManager.Utils;
using PasswordManager.Locator;

#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8605, CS8618, CS8625

namespace PasswordManager.Monitor
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
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

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
                    bool isPasswordEncryptDialog = title.Contains("密码加密");
                    bool isDocEncryptDialog = title.Contains("文档已加密");

                    if (isEncryptDialog)
                        {
                            uint processId;
                            GetWindowThreadProcessId(activeWindow, out processId);
                            
                            List<IntPtr> wpsMainWindows = new List<IntPtr>();
                            List<string> allWpsWindowsInfo = new List<string>();
                            
                            EnumWindows((IntPtr hWnd, IntPtr lParam) =>
                            {
                                uint pid;
                                GetWindowThreadProcessId(hWnd, out pid);
                                StringBuilder windowTitle = new StringBuilder(256);
                                GetWindowText(hWnd, windowTitle, windowTitle.Capacity);
                                string title = windowTitle.ToString();
                                
                                StringBuilder className = new StringBuilder(256);
                                GetClassName(hWnd, className, className.Capacity);
                                string classStr = className.ToString();
                                
                                bool isWpsWindow = false;
                                bool isMainWindow = false;
                                bool isSameProcess = (pid == processId);
                                
                                if (!string.IsNullOrEmpty(title))
                                {
                                    if (title.EndsWith(" - WPS Office") && title != "WPS Office")
                                    {
                                        isWpsWindow = true;
                                        isMainWindow = true;
                                    }
                                    else if (title.Contains(" - WPS"))
                                    {
                                        isWpsWindow = true;
                                    }
                                    else if (title.Contains(".docx") || title.Contains(".xlsx") || title.Contains(".pptx"))
                                    {
                                        isWpsWindow = true;
                                        isMainWindow = true;
                                    }
                                }
                                    
                                if (!isWpsWindow && !string.IsNullOrEmpty(classStr))
                                {
                                    if (classStr.Contains("WPS") || classStr.Contains("Kingsoft") || classStr.Contains("kwps"))
                                    {
                                        isWpsWindow = true;
                                    }
                                }
                                    
                                if (isWpsWindow)
                                {
                                    string windowInfo = $"进程ID: {pid}, 句柄: {hWnd}, 标题: '{title}', 类名: '{classStr}', 是否同进程: {isSameProcess}, 是否主窗口: {isMainWindow}";
                                    allWpsWindowsInfo.Add(windowInfo);
                                    
                                    if (isSameProcess)
                                        {
                                            wpsMainWindows.Add(hWnd);
                                        }
                                }
                                return true;
                            }, IntPtr.Zero);
                            
                            if (wpsMainWindows.Count > 0)
                            {
                                IntPtr mainWindowHandle = wpsMainWindows[0];
                                StringBuilder mainTitle = new StringBuilder(256);
                                GetWindowText(mainWindowHandle, mainTitle, mainTitle.Capacity);
                                string mainWindowTitle = mainTitle.ToString();
                                
                                if (mainWindowTitle.EndsWith(" - WPS Office"))
                                {
                                    docName = mainWindowTitle.Replace(" - WPS Office", "");
                                }
                                else if (mainWindowTitle.Contains(" - WPS"))
                                {
                                    docName = mainWindowTitle.Substring(0, mainWindowTitle.IndexOf(" - WPS"));
                                }
                                else
                                {
                                    docName = mainWindowTitle;
                                }
                            }
                            else
                            {
                                string crossProcessDocName = string.Empty;
                                foreach (string info in allWpsWindowsInfo)
                                {
                                    
                                    int titleStartIdx = info.IndexOf("标题: '") + 5;
                                    if (titleStartIdx > 3)
                                    {
                                        int titleEndIdx = info.IndexOf("', 类名:", titleStartIdx);
                                        if (titleEndIdx > titleStartIdx)
                                        {
                                            string crossWindowTitle = info.Substring(titleStartIdx, titleEndIdx - titleStartIdx);
                                            if (crossWindowTitle.EndsWith(" - WPS Office"))
                                            {
                                                crossProcessDocName = crossWindowTitle.Replace(" - WPS Office", "");
                                                break;
                                            }
                                        }
                                    }
                                }
                                
                                if (!string.IsNullOrEmpty(crossProcessDocName))
                                {
                                    docName = crossProcessDocName;
                                    
                                    if (docName.StartsWith("[只读]"))
                                    {
                                        string candidateName = docName.Substring(4);
                                        string matchedName = TryMatchDocumentName(candidateName);
                                        if (!string.IsNullOrEmpty(matchedName))
                                        {
                                            docName = matchedName;
                                        }
                                    }
                                }
                            }
                        }
                    else if (!string.IsNullOrEmpty(title) && title.Contains(" - WPS Office"))
                    {
                        docName = title.Replace(" - WPS Office", "");
                        
                        if (docName.StartsWith("[只读]"))
                        {
                            string candidateName = docName.Substring(4);
                            string matchedName = TryMatchDocumentName(candidateName);
                            if (!string.IsNullOrEmpty(matchedName))
                            {
                                docName = matchedName;
                            }
                        }
                    }
                    else if (!string.IsNullOrEmpty(title) && title.StartsWith("[只读]"))
                    {
                        string candidateName = title.Substring(4);
                        string matchedName = TryMatchDocumentName(candidateName);
                        if (!string.IsNullOrEmpty(matchedName))
                        {
                            docName = matchedName;
                        }
                        else
                        {
                            docName = title;
                        }
                    }
                    else if (!string.IsNullOrEmpty(title))
                    {
                        string matchedName = TryMatchDocumentName(title);
                        if (!string.IsNullOrEmpty(matchedName))
                        {
                            docName = matchedName;
                        }
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
                        else if (!string.IsNullOrEmpty(title) && title.StartsWith("[只读]"))
                    {
                        string candidateName = title.Substring(4);
                        string matchedName = TryMatchDocumentName(candidateName);
                        if (!string.IsNullOrEmpty(matchedName))
                        {
                            wpsWindowTitles.Add(matchedName + " - WPS Office");
                        }
                        else
                        {
                            wpsWindowTitles.Add(title);
                        }
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

        private string TryMatchDocumentName(string candidateName)
        {
            if (string.IsNullOrEmpty(candidateName))
            {
                return string.Empty;
            }

            List<string> possiblePaths = GlobalState.Instance.GetPossiblePaths();
            
            if (possiblePaths.Count == 0)
            {
                return string.Empty;
            }

            List<string> matchedFiles = new List<string>();

            foreach (string path in possiblePaths)
            {
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                string fileName = Path.GetFileName(path);
                
                if (string.Equals(fileName, candidateName, StringComparison.OrdinalIgnoreCase))
                {
                    matchedFiles.Add(fileName);
                }
            }
            
            if (matchedFiles.Count == 1)
            {
                return matchedFiles[0];
            }

            return string.Empty;
        }





        public string GetDocumentPath(IntPtr dialogHandle)
        {
            try
            {
                string docName = GetDocumentName(dialogHandle);

                if (!string.IsNullOrEmpty(docName))
                {
                    List<string> possiblePaths = GlobalState.Instance.GetPossiblePaths();

                    string targetDocName = docName.StartsWith("[只读]") ? docName.Substring(4).Trim() : docName;

                    for (int i = possiblePaths.Count - 1; i >= 0; i--)
                    {
                        string path = possiblePaths[i];
                        if (string.IsNullOrEmpty(path))
                        {
                            continue;
                        }

                        string fileName = Path.GetFileName(path);
                        string pattern = $"^{Regex.Escape(fileName)}$";
                        if (Regex.IsMatch(targetDocName, pattern, RegexOptions.IgnoreCase))
                        {
                            if (File.Exists(path))
                            {
                                Logger.Info($"[路径匹配] 成功匹配文档路径: {path}");
                                return path;
                            }
                        }
                    }

                    if (docName.StartsWith("[只读]"))
                    {
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
                                    Logger.Info($"[路径匹配] 使用原始名称匹配文档路径: {path}");
                                    return path;
                                }
                            }
                        }
                    }
                }
                else
                {
                    List<string> possiblePaths = GlobalState.Instance.GetPossiblePaths();
                    foreach (string path in possiblePaths)
                    {
                        if (!string.IsNullOrEmpty(path) && File.Exists(path))
                        {
                            return path;
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
