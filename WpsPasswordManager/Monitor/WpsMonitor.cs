using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using WpsPasswordManager.Utils;

#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8605, CS8618, CS8625

namespace WpsPasswordManager.Monitor
{
    public class WpsMonitor
    {
        // Win32 API 定义
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, MonitorDpiType dpiType, out uint dpiX, out uint dpiY);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        // 结构体定义
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

        // 监控 WPS 进程是否运行
        public bool IsWpsRunning()
        {
            Process[] processes = Process.GetProcessesByName("wps");
            return processes.Length > 0;
        }

        // 查找 WPS 密码对话框
        public IntPtr FindPasswordDialog()
        {
            Logger.Debug("开始查找密码对话框");
            
            // 尝试查找所有可能的密码对话框标题
            string[] dialogTitles = { "密码加密", "文档加密", "文档已加密", "密码" };
            
            foreach (string title in dialogTitles)
            {
                // 尝试使用不同的类名查找窗口
                string[] classNames = { "Qt5QWindow", "#32770", "", "QWidget", "QDialog" };
                
                foreach (string className in classNames)
                {
                    IntPtr handle = FindWindow(className, title);
                    if (handle != IntPtr.Zero)
                    {
                        LogWindowInfo($"找到{title}窗口（类名: {className}）", handle);
                        return handle;
                    }
                }
            }
            
            // 快速尝试：只查找WPS进程的窗口，不递归检查子窗口
            Logger.Debug("尝试查找WPS进程的密码相关窗口");
            IntPtr dialogHandle = FindWpsPasswordDialogByEnumeration();
            if (dialogHandle != IntPtr.Zero)
            {
                LogWindowInfo("通过枚举找到WPS密码相关窗口", dialogHandle);
                return dialogHandle;
            }
            else
            {
                Logger.Debug("未找到密码对话框");
            }
            return IntPtr.Zero;
        }
        
        // 通过枚举所有WPS窗口查找密码对话框
        private IntPtr FindWpsPasswordDialogByEnumeration()
        {
            IntPtr foundHandle = IntPtr.Zero;
            
            // 先获取所有WPS进程的ID列表
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
            
            // 如果没有WPS进程，直接返回
            if (wpsProcessIds.Count == 0)
            {
                return IntPtr.Zero;
            }
            
            // 枚举所有顶级窗口
            EnumWindows((IntPtr hWnd, IntPtr lParam) =>
            {
                // 获取窗口标题
                StringBuilder windowTitle = new StringBuilder(256);
                GetWindowText(hWnd, windowTitle, windowTitle.Capacity);
                string title = windowTitle.ToString();
                
                // 检查窗口标题是否包含密码相关关键词
                if (!title.Contains("密码") && !title.Contains("加密") && !title.Contains("解密"))
                {
                    return true; // 继续枚举
                }
                
                // 获取窗口类名
                StringBuilder className = new StringBuilder(256);
                GetClassName(hWnd, className, className.Capacity);
                string classStr = className.ToString();
                
                // 检查是否是WPS进程的窗口
                uint processId;
                GetWindowThreadProcessId(hWnd, out processId);
                
                // 只检查进程ID是否在WPS进程ID列表中，避免每次都调用Process.GetProcessById
                if (wpsProcessIds.Contains((int)processId))
                {
                    Logger.Debug($"找到WPS窗口: 句柄={hWnd}, 标题={title}, 类名={classStr}");
                    foundHandle = hWnd;
                    return false; // 找到后停止枚举
                }
                
                return true; // 继续枚举
            }, IntPtr.Zero);
            
            return foundHandle;
        }

        // 记录窗口详细信息
        private void LogWindowInfo(string message, IntPtr hWnd)
        {
            try
            {
                // 获取窗口类名
                StringBuilder className = new StringBuilder(256);
                int classNameResult = GetClassName(hWnd, className, className.Capacity);
                string classNameStr = className.ToString();
                
                // 获取窗口标题
                StringBuilder windowTitle = new StringBuilder(256);
                int windowTitleResult = GetWindowText(hWnd, windowTitle, windowTitle.Capacity);
                string windowTitleStr = windowTitle.ToString();
                
                // 获取窗口矩形
                RECT rect = new RECT();
                bool rectResult = GetWindowRect(hWnd, ref rect);
                
                Logger.Debug($"{message}: 句柄={hWnd}, 类名={classNameStr} (获取结果: {classNameResult}), 标题={windowTitleStr} (获取结果: {windowTitleResult}), 位置=({rect.Left}, {rect.Top}, {rect.Right}, {rect.Bottom}) (获取结果: {rectResult})");
            }
            catch (Exception ex)
            {
                Logger.Error($"记录窗口信息时出错: {ex.Message}");
            }
        }

        // 根据部分标题查找窗口
        private IntPtr FindWindowByPartialTitle(string partialTitle)
        {
            IntPtr hWnd = IntPtr.Zero;
            Process[] processes = Process.GetProcesses();
            
            foreach (Process process in processes)
            {
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    StringBuilder windowTitle = new StringBuilder(256);
                    GetWindowText(process.MainWindowHandle, windowTitle, windowTitle.Capacity);
                    
                    if (windowTitle.ToString().Contains(partialTitle))
                    {
                        hWnd = process.MainWindowHandle;
                        break;
                    }
                }
            }
            
            return hWnd;
        }

        // 根据部分标题查找所有窗口（包括子窗口）
        private IntPtr FindWindowByPartialTitleAll(string partialTitle)
        {
            IntPtr hWnd = IntPtr.Zero;
            Process[] processes = Process.GetProcesses();
            
            foreach (Process process in processes)
            {
                // 只处理 WPS 进程
                if (process.ProcessName.ToLower() == "wps")
                {
                    // 枚举进程的所有顶级窗口
                    EnumWindowsProc callback = (IntPtr windowHandle, IntPtr lParam) =>
                    {
                        uint processId;
                        GetWindowThreadProcessId(windowHandle, out processId);
                        
                        if (processId == process.Id)
                        {
                            // 检查当前窗口
                            StringBuilder windowTitle = new StringBuilder(256);
                            GetWindowText(windowHandle, windowTitle, windowTitle.Capacity);
                            
                            if (windowTitle.ToString().Contains(partialTitle))
                            {
                                hWnd = windowHandle;
                                return false; // 找到后停止枚举
                            }
                            
                            // 递归检查子窗口
                            IntPtr childWnd = FindChildWindowByPartialTitle(windowHandle, partialTitle);
                            if (childWnd != IntPtr.Zero)
                            {
                                hWnd = childWnd;
                                return false; // 找到后停止枚举
                            }
                        }
                        return true; // 继续枚举
                    };
                    
                    EnumWindows(callback, IntPtr.Zero);
                    
                    if (hWnd != IntPtr.Zero)
                    {
                        break;
                    }
                }
            }
            
            return hWnd;
        }
        
        // 递归查找子窗口
        private IntPtr FindChildWindowByPartialTitle(IntPtr parentHandle, string partialTitle)
        {
            IntPtr hWnd = IntPtr.Zero;
            
            // 枚举子窗口
            EnumChildWindowsProc callback = (IntPtr windowHandle, IntPtr lParam) =>
            {
                // 检查当前窗口
                StringBuilder windowTitle = new StringBuilder(256);
                GetWindowText(windowHandle, windowTitle, windowTitle.Capacity);
                
                if (windowTitle.ToString().Contains(partialTitle))
                {
                    hWnd = windowHandle;
                    return false; // 找到后停止枚举
                }
                
                // 递归检查子窗口
                IntPtr childWnd = FindChildWindowByPartialTitle(windowHandle, partialTitle);
                if (childWnd != IntPtr.Zero)
                {
                    hWnd = childWnd;
                    return false; // 找到后停止枚举
                }
                
                return true; // 继续枚举
            };
            
            EnumChildWindows(parentHandle, callback, IntPtr.Zero);
            
            return hWnd;
        }

        // 枚举窗口的委托
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        // 枚举所有顶级窗口
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        
        // 获取当前活动窗口
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        
        // Win32 API 定义：设置窗口为前台窗口
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        
        // Win32 API 定义：获取焦点窗口
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetFocus();
        
        // Win32 API 定义：模拟键盘事件
        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        // Win32 API 定义：发送消息
[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, StringBuilder lParam);
        
// Win32 API 定义：发送消息（设置文本）
[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam);
        
// Win32 API 定义：发送消息（通用）
[DllImport("user32.dll", SetLastError = true)]
private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        
        // 常量定义
        private const uint WM_GETTEXT = 0x000D;
        private const uint WM_SETTEXT = 0x000C;

        // 定位密码输入框
        public IntPtr FindPasswordEdit(IntPtr dialogHandle)
        {
            if (dialogHandle == IntPtr.Zero)
            {
                Logger.Warning("对话框句柄为空，无法定位密码输入框");
                return IntPtr.Zero;
            }

            Logger.Debug($"开始定位密码输入框，对话框句柄: {dialogHandle}");

            // 获取窗口类名，检查是否为Qt窗口
            StringBuilder dialogClassName = new StringBuilder(256);
            GetClassName(dialogHandle, dialogClassName, dialogClassName.Capacity);
            string dialogClass = dialogClassName.ToString();
            Logger.Debug($"对话框类名: {dialogClass}");

            // 1. 尝试使用 EnumChildWindows 枚举所有子窗口
            IntPtr foundHandle = IntPtr.Zero;
            int windowCount = 0;
            
            EnumChildWindows(dialogHandle, (hwnd, lParam) =>
            {
                windowCount++;
                // 获取窗口类名
                StringBuilder className = new StringBuilder(256);
                GetClassName(hwnd, className, className.Capacity);
                string classNameStr = className.ToString();
                
                // 获取窗口文本
                StringBuilder windowText = new StringBuilder(256);
                GetWindowText(hwnd, windowText, windowText.Capacity);
                string windowTextStr = windowText.ToString();
                
                Logger.Debug($"检查窗口 {windowCount}: 句柄={hwnd}, 类名={classNameStr}, 文本={windowTextStr}");
                
                // 检查是否为编辑控件
                if (IsEditControl(classNameStr))
                {
                    // 尝试读取文本，确认是否为输入框
                    StringBuilder testText = new StringBuilder(256);
                    SendMessage(hwnd, WM_GETTEXT, (IntPtr)256, testText);
                    string text = testText.ToString();
                    
                    // 即使文本为空，只要能读取，也认为是输入框
                    Logger.Debug($"找到可能的输入框: 句柄={hwnd}, 类名={classNameStr}, 文本={text}");
                    foundHandle = hwnd;
                    return false; // 找到后停止枚举
                }
                
                // 递归查找子窗口
                IntPtr childFoundHandle = FindPasswordEdit(hwnd);
                if (childFoundHandle != IntPtr.Zero)
                {
                    foundHandle = childFoundHandle;
                    return false; // 找到后停止枚举
                }
                
                return true; // 继续枚举
            }, IntPtr.Zero);
            
            if (foundHandle != IntPtr.Zero)
            {
                Logger.Info($"成功找到密码输入框: {foundHandle}");
                return foundHandle;
            }
            
            // 2. 尝试使用 FindWindowEx 查找所有子窗口
            IntPtr childHandle = IntPtr.Zero;
            int childCount = 0;
            do
            {
                childHandle = FindWindowEx(dialogHandle, childHandle, null, null);
                if (childHandle != IntPtr.Zero)
                {
                    childCount++;
                    Logger.Debug($"检查子窗口 {childCount}: 句柄={childHandle}");
                    // 递归查找子窗口
                    IntPtr grandChildHandle = FindPasswordEdit(childHandle);
                    if (grandChildHandle != IntPtr.Zero)
                    {
                        return grandChildHandle;
                    }
                }
            } while (childHandle != IntPtr.Zero);
            
            // 3. 尝试直接查找Qt输入框
            IntPtr qtEdit = FindQtEditControl(dialogHandle);
            if (qtEdit != IntPtr.Zero)
            {
                Logger.Info($"找到Qt输入框: {qtEdit}");
                return qtEdit;
            }
            
            Logger.Warning("未找到密码输入框");
            return IntPtr.Zero;
        }
        
        // 检查是否为编辑控件
        private bool IsEditControl(string className)
        {
            string[] editControlClasses = {
                "Edit", "TextBox", "RichEdit", "RichEdit20W", "RichEdit50W",
                "QLineEdit", "QTextEdit", "QPlainTextEdit", "LineEdit", "TextEdit",
                "INPUT", "edit", "text", "Text", "Edit", "qt", "Qt",
                "QWidget", "QDialog", "QMainWindow", "QFrame"
            };
            
            foreach (string controlClass in editControlClasses)
            {
                if (className == controlClass || className.Contains(controlClass))
                {
                    return true;
                }
            }
            return false;
        }
        
        // Win32 API 定义：设置窗口文本
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetWindowText(IntPtr hWnd, string lpString);
        
        // 查找Qt编辑控件
        private IntPtr FindQtEditControl(IntPtr parentHandle)
        {
            try
            {
                Logger.Debug($"开始查找Qt编辑控件，父窗口句柄: {parentHandle}");
                
                // 枚举所有子窗口
                IntPtr foundHandle = IntPtr.Zero;
                EnumChildWindows(parentHandle, (hwnd, lParam) =>
                {
                    // 获取窗口类名
                    StringBuilder className = new StringBuilder(256);
                    GetClassName(hwnd, className, className.Capacity);
                    string classNameStr = className.ToString();
                    
                    // 获取窗口文本
                    StringBuilder windowText = new StringBuilder(256);
                    GetWindowText(hwnd, windowText, windowText.Capacity);
                    string windowTextStr = windowText.ToString();
                    
                    Logger.Debug($"检查Qt窗口: 句柄={hwnd}, 类名={classNameStr}, 文本={windowTextStr}");
                    
                    // 检查是否为Qt编辑控件
                    if (classNameStr.Contains("QLineEdit") || 
                        classNameStr.Contains("QTextEdit") || 
                        classNameStr.Contains("QPlainTextEdit") || 
                        classNameStr.Contains("LineEdit") ||
                        classNameStr.Contains("Edit") ||
                        classNameStr.Contains("Text"))
                    {
                        // 尝试向窗口发送 WM_GETTEXT 消息，看是否能获取文本
                        StringBuilder testText = new StringBuilder(256);
                        SendMessage(hwnd, WM_GETTEXT, (IntPtr)256, testText);
                        string text = testText.ToString();
                        
                        // 即使文本为空，只要能读取，也认为是输入框
                        Logger.Debug($"找到可能的Qt输入框: 句柄={hwnd}, 类名={classNameStr}, 文本={text}");
                        foundHandle = hwnd;
                        return false; // 找到后停止枚举
                    }
                    
                    // 递归查找子窗口
                    IntPtr childFoundHandle = FindQtEditControl(hwnd);
                    if (childFoundHandle != IntPtr.Zero)
                    {
                        foundHandle = childFoundHandle;
                        return false; // 找到后停止枚举
                    }
                    
                    return true; // 继续枚举
                }, IntPtr.Zero);
                
                return foundHandle;
            }
            catch (Exception ex)
            {
                Logger.Error($"查找Qt编辑控件时出错: {ex.Message}");
                return IntPtr.Zero;
            }
        }
        
        // 递归查找编辑控件（包括所有子窗口）
        private IntPtr FindEditControlRecursive(IntPtr parentHandle)
        {
            try
            {
                Logger.Debug($"开始递归查找编辑控件，父窗口句柄: {parentHandle}");
                
                // 尝试使用 EnumChildWindows 枚举所有子窗口
                IntPtr foundHandle = IntPtr.Zero;
                EnumChildWindows(parentHandle, (hwnd, lParam) =>
                {
                    // 获取窗口类名
                    StringBuilder className = new StringBuilder(256);
                    int classNameResult = GetClassName(hwnd, className, className.Capacity);
                    string classNameStr = className.ToString();
                    
                    // 获取窗口标题
                    StringBuilder windowTitle = new StringBuilder(256);
                    int windowTitleResult = GetWindowText(hwnd, windowTitle, windowTitle.Capacity);
                    string windowTitleStr = windowTitle.ToString();
                    
                    // 记录当前检查的窗口
                    Logger.Debug($"检查窗口: 句柄={hwnd}, 类名={classNameStr} (获取结果: {classNameResult}), 标题={windowTitleStr} (获取结果: {windowTitleResult})");
                    
                    // 检查当前窗口是否为编辑控件
                    if (classNameStr == "Edit")
                    {
                        Logger.Debug($"找到编辑控件: 句柄={hwnd}");
                        foundHandle = hwnd;
                        return false; // 找到后停止枚举
                    }
                    
                    // 尝试查找其他可能的输入框类名（包括Qt控件）
                    if (classNameStr.Contains("Edit") || classNameStr.Contains("edit") || 
                        classNameStr.Contains("INPUT") || classNameStr.Contains("Static") || 
                        classNameStr.Contains("LineEdit") || classNameStr.Contains("QLineEdit") ||
                        classNameStr.Contains("Qt") || classNameStr.Contains("qt"))
                    {
                        Logger.Debug($"找到可能的输入框: 句柄={hwnd}, 类名={classNameStr}");
                        foundHandle = hwnd;
                        return false; // 找到后停止枚举
                    }
                    
                    // 递归查找子窗口
                    IntPtr childFoundHandle = FindEditControlRecursive(hwnd);
                    if (childFoundHandle != IntPtr.Zero)
                    {
                        foundHandle = childFoundHandle;
                        return false; // 找到后停止枚举
                    }
                    
                    return true; // 继续枚举
                }, IntPtr.Zero);
                
                if (foundHandle != IntPtr.Zero)
                {
                    return foundHandle;
                }
                
                // 如果 EnumChildWindows 失败，尝试使用 FindWindowEx
                IntPtr childHandle = IntPtr.Zero;
                int childCount = 0;
                do
                {
                    childHandle = FindWindowEx(parentHandle, childHandle, null, null);
                    if (childHandle != IntPtr.Zero)
                    {
                        childCount++;
                        // 获取窗口类名
                        StringBuilder className = new StringBuilder(256);
                        int classNameResult = GetClassName(childHandle, className, className.Capacity);
                        string classNameStr = className.ToString();
                        
                        // 获取窗口标题
                        StringBuilder windowTitle = new StringBuilder(256);
                        int windowTitleResult = GetWindowText(childHandle, windowTitle, windowTitle.Capacity);
                        string windowTitleStr = windowTitle.ToString();
                        
                        // 记录当前检查的窗口
                        Logger.Debug($"检查窗口 {childCount}: 句柄={childHandle}, 类名={classNameStr} (获取结果: {classNameResult}), 标题={windowTitleStr} (获取结果: {windowTitleResult})");
                        
                        // 检查当前窗口是否为编辑控件
                        if (classNameStr == "Edit")
                        {
                            Logger.Debug($"找到编辑控件: 句柄={childHandle}");
                            return childHandle;
                        }
                        
                        // 尝试查找其他可能的输入框类名（包括Qt控件）
                        if (classNameStr.Contains("Edit") || classNameStr.Contains("edit") || 
                            classNameStr.Contains("INPUT") || classNameStr.Contains("Static") || 
                            classNameStr.Contains("LineEdit") || classNameStr.Contains("QLineEdit") ||
                            classNameStr.Contains("Qt") || classNameStr.Contains("qt"))
                        {
                            Logger.Debug($"找到可能的输入框: 句柄={childHandle}, 类名={classNameStr}");
                            return childHandle;
                        }
                        
                        // 递归查找子窗口
                        IntPtr recursiveFoundHandle = FindEditControlRecursive(childHandle);
                        if (recursiveFoundHandle != IntPtr.Zero)
                        {
                            return recursiveFoundHandle;
                        }
                    }
                } while (childHandle != IntPtr.Zero);
                
                Logger.Debug($"递归查找结束，未找到编辑控件，父窗口句柄: {parentHandle}");
                return IntPtr.Zero;
            }
            catch (Exception ex)
            {
                Logger.Error($"递归查找编辑控件时出错: {ex.Message}");
                return IntPtr.Zero;
            }
        }

        // 枚举子窗口的委托
        private delegate bool EnumChildWindowsProc(IntPtr hwnd, IntPtr lParam);

        // 枚举所有子窗口
        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildWindowsProc lpEnumFunc, IntPtr lParam);

        // 定位确认密码输入框
        public IntPtr FindConfirmPasswordEdit(IntPtr dialogHandle, IntPtr firstEditHandle)
        {
            if (dialogHandle == IntPtr.Zero || firstEditHandle == IntPtr.Zero)
            {
                Logger.Warning("对话框句柄或第一个输入框句柄为空，无法定位确认密码输入框");
                return IntPtr.Zero;
            }

            Logger.Debug($"开始定位确认密码输入框，对话框句柄: {dialogHandle}, 第一个输入框句柄: {firstEditHandle}");

            // 尝试在第一个输入框之后查找下一个编辑控件
            IntPtr secondEditHandle = FindWindowEx(dialogHandle, firstEditHandle, "Edit", null);
            if (secondEditHandle != IntPtr.Zero)
            {
                Logger.Debug($"通过直接查找找到第二个编辑控件: {secondEditHandle}");
                return secondEditHandle;
            }
            
            // 尝试查找其他可能的输入框类名
            string[] possibleClassNames = { "Edit", "TextBox", "QLineEdit", "LineEdit", "RichEdit", "RichEdit20W", "RichEdit50W" };
            foreach (string className in possibleClassNames)
            {
                secondEditHandle = FindWindowEx(dialogHandle, firstEditHandle, className, null);
                if (secondEditHandle != IntPtr.Zero)
                {
                    Logger.Debug($"通过类名 {className} 找到第二个输入框: {secondEditHandle}");
                    return secondEditHandle;
                }
            }
            
            // 尝试递归查找所有子窗口，找到第二个编辑控件
            System.Collections.Generic.List<IntPtr> editControls = new System.Collections.Generic.List<IntPtr>();
            CollectAllEditControls(dialogHandle, editControls);
            
            Logger.Debug($"找到 {editControls.Count} 个编辑控件");
            
            // 找到第一个输入框在列表中的位置，返回下一个编辑控件
            int firstEditIndex = editControls.IndexOf(firstEditHandle);
            if (firstEditIndex >= 0 && firstEditIndex + 1 < editControls.Count)
            {
                secondEditHandle = editControls[firstEditIndex + 1];
                Logger.Debug($"通过列表查找找到第二个编辑控件: {secondEditHandle}");
                return secondEditHandle;
            }
            else if (editControls.Count > 1)
            {
                // 如果找不到第一个输入框，返回第二个编辑控件
                secondEditHandle = editControls[1];
                Logger.Debug($"返回第二个编辑控件: {secondEditHandle}");
                return secondEditHandle;
            }
            
            Logger.Warning("未找到确认密码输入框");
            return IntPtr.Zero;
        }
        
        // 收集所有编辑控件
        private void CollectAllEditControls(IntPtr parentHandle, System.Collections.Generic.List<IntPtr> editControls)
        {
            try
            {
                IntPtr childHandle = IntPtr.Zero;
                do
                {
                    childHandle = FindWindowEx(parentHandle, childHandle, null, null);
                    if (childHandle != IntPtr.Zero)
                    {
                        // 获取窗口类名
                        StringBuilder className = new StringBuilder(256);
                        GetClassName(childHandle, className, className.Capacity);
                        string classNameStr = className.ToString();
                        
                        // 检查是否为编辑控件
                        if (classNameStr.Contains("Edit") || classNameStr.Contains("TextBox") || 
                            classNameStr.Contains("QLineEdit") || classNameStr.Contains("LineEdit") ||
                            classNameStr.Contains("RichEdit"))
                        {
                            editControls.Add(childHandle);
                            Logger.Debug($"收集到编辑控件: 句柄={childHandle}, 类名={classNameStr}");
                        }
                        
                        // 递归查找子窗口
                        CollectAllEditControls(childHandle, editControls);
                    }
                } while (childHandle != IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Logger.Error($"收集编辑控件时出错: {ex.Message}");
            }
        }

        // 递归查找第二个编辑控件（排除第一个）
        private IntPtr FindSecondEditControlRecursive(IntPtr parentHandle, IntPtr excludeHandle)
        {
            IntPtr childHandle = IntPtr.Zero;
            int editCount = 0;
            IntPtr secondEditHandle = IntPtr.Zero;
            
            // 遍历所有子窗口
            do
            {
                childHandle = FindWindowEx(parentHandle, childHandle, null, null);
                if (childHandle != IntPtr.Zero)
                {
                    // 检查当前窗口是否为编辑控件
                    StringBuilder className = new StringBuilder(256);
                    GetClassName(childHandle, className, className.Capacity);
                    if (className.ToString() == "Edit" && childHandle != excludeHandle)
                    {
                        editCount++;
                        if (editCount == 1) // 找到第一个非排除的编辑控件
                        {
                            secondEditHandle = childHandle;
                            break;
                        }
                    }
                    
                    // 递归查找子窗口
                    IntPtr foundHandle = FindSecondEditControlRecursive(childHandle, excludeHandle);
                    if (foundHandle != IntPtr.Zero)
                    {
                        return foundHandle;
                    }
                }
            } while (childHandle != IntPtr.Zero);
            
            return secondEditHandle;
        }

        // 定位确定按钮
        public IntPtr FindOKButton(IntPtr dialogHandle)
        {
            if (dialogHandle == IntPtr.Zero)
            {
                Logger.Warning("对话框句柄为空，无法定位确定按钮");
                return IntPtr.Zero;
            }

            // 查找按钮
            IntPtr buttonHandle = FindWindowEx(dialogHandle, IntPtr.Zero, "Button", "确定");
            if (buttonHandle != IntPtr.Zero)
            {
                Logger.Debug("找到确定按钮");
                return buttonHandle;
            }
            
            // 兼容环境查找
            buttonHandle = FindWindowEx(dialogHandle, IntPtr.Zero, "Button", null);
            if (buttonHandle != IntPtr.Zero)
            {
                Logger.Debug("通过兼容模式找到按钮");
            }
            else
            {
                Logger.Warning("未找到确定按钮");
            }
            return buttonHandle;
        }

        // 定位「运用」按钮
        public IntPtr FindApplyButton(IntPtr dialogHandle)
        {
            if (dialogHandle == IntPtr.Zero)
            {
                Logger.Warning("对话框句柄为空，无法定位运用按钮");
                return IntPtr.Zero;
            }

            Logger.Debug($"开始查找应用按钮，对话框句柄: {dialogHandle}");

            // 尝试多种按钮文本
            string[] buttonTexts = { "应用", "确定", "OK" };
            
            foreach (string buttonText in buttonTexts)
            {
                // 查找标准按钮
                IntPtr buttonHandle = FindWindowEx(dialogHandle, IntPtr.Zero, "Button", buttonText);
                if (buttonHandle != IntPtr.Zero)
                {
                    Logger.Info($"找到{buttonText}按钮");
                    LogWindowInfo($"找到{buttonText}按钮", buttonHandle);
                    return buttonHandle;
                }
                
                // 尝试查找Qt按钮
                buttonHandle = FindQtButton(dialogHandle, buttonText);
                if (buttonHandle != IntPtr.Zero)
                {
                    Logger.Info($"找到Qt{buttonText}按钮");
                    LogWindowInfo($"找到Qt{buttonText}按钮", buttonHandle);
                    return buttonHandle;
                }
                
                // 尝试查找Qt5按钮
                buttonHandle = FindQt5Button(dialogHandle, buttonText);
                if (buttonHandle != IntPtr.Zero)
                {
                    Logger.Info($"找到Qt5{buttonText}按钮");
                    LogWindowInfo($"找到Qt5{buttonText}按钮", buttonHandle);
                    return buttonHandle;
                }
                
                // 枚举所有子窗口，查找可能的按钮
                buttonHandle = FindButtonByEnumeration(dialogHandle, buttonText);
                if (buttonHandle != IntPtr.Zero)
                {
                    Logger.Info($"通过枚举找到{buttonText}按钮");
                    LogWindowInfo($"通过枚举找到{buttonText}按钮", buttonHandle);
                    return buttonHandle;
                }
            }
            
            // 尝试查找所有可能的按钮控件，不限制文本
            Logger.Debug("尝试查找所有可能的按钮控件");
            IntPtr anyButton = FindAnyButton(dialogHandle);
            if (anyButton != IntPtr.Zero)
            {
                Logger.Info("找到任意按钮");
                LogWindowInfo("找到任意按钮", anyButton);
                return anyButton;
            }
            
            // 枚举所有子窗口，记录详细信息
            Logger.Debug("开始枚举所有子窗口，查找应用按钮");
            
            // 尝试使用FindWindowEx枚举子窗口
            IntPtr childHandle = IntPtr.Zero;
            int childCount = 0;
            do
            {
                childHandle = FindWindowEx(dialogHandle, childHandle, null, null);
                if (childHandle != IntPtr.Zero)
                {
                    childCount++;
                    StringBuilder className = new StringBuilder(256);
                    GetClassName(childHandle, className, className.Capacity);
                    string classNameStr = className.ToString();
                    
                    StringBuilder windowText = new StringBuilder(256);
                    GetWindowText(childHandle, windowText, windowText.Capacity);
                    string windowTextStr = windowText.ToString();
                    
                    Logger.Debug($"子窗口 {childCount}: 句柄={childHandle}, 类名={classNameStr}, 文本={windowTextStr}");
                    
                    // 递归查找子窗口
                    FindChildWindows(childHandle, 1);
                }
            } while (childHandle != IntPtr.Zero);
            
            Logger.Debug($"共找到 {childCount} 个子窗口");
            
            // 未找到应用按钮的处理
            Logger.Warning("未找到应用按钮");
            return IntPtr.Zero;
        }
        
        // 递归查找子窗口
        private void FindChildWindows(IntPtr parentHandle, int level)
        {
            IntPtr childHandle = IntPtr.Zero;
            int childCount = 0;
            do
            {
                childHandle = FindWindowEx(parentHandle, childHandle, null, null);
                if (childHandle != IntPtr.Zero)
                {
                    childCount++;
                    StringBuilder className = new StringBuilder(256);
                    GetClassName(childHandle, className, className.Capacity);
                    string classNameStr = className.ToString();
                    
                    StringBuilder windowText = new StringBuilder(256);
                    GetWindowText(childHandle, windowText, windowText.Capacity);
                    string windowTextStr = windowText.ToString();
                    
                    string indent = new string(' ', level * 2);
                    Logger.Debug($"{indent}子窗口 {childCount}: 句柄={childHandle}, 类名={classNameStr}, 文本={windowTextStr}");
                    
                    // 递归查找更深层的子窗口
                    FindChildWindows(childHandle, level + 1);
                }
            } while (childHandle != IntPtr.Zero);
        }
        
        // 查找Qt按钮
        private IntPtr FindQtButton(IntPtr parentHandle, string buttonText)
        {
            IntPtr foundHandle = IntPtr.Zero;
            
            EnumChildWindows(parentHandle, (hwnd, lParam) =>
            {
                StringBuilder className = new StringBuilder(256);
                GetClassName(hwnd, className, className.Capacity);
                string classNameStr = className.ToString();
                
                StringBuilder windowText = new StringBuilder(256);
                GetWindowText(hwnd, windowText, windowText.Capacity);
                string windowTextStr = windowText.ToString();
                
                Logger.Debug($"检查Qt窗口: 句柄={hwnd}, 类名={classNameStr}, 文本={windowTextStr}");
                
                // 检查是否为Qt按钮
                if (IsButtonControl(classNameStr) && windowTextStr == buttonText)
                {
                    foundHandle = hwnd;
                    return false;
                }
                
                // 递归查找子窗口
                IntPtr childFoundHandle = FindQtButton(hwnd, buttonText);
                if (childFoundHandle != IntPtr.Zero)
                {
                    foundHandle = childFoundHandle;
                    return false;
                }
                
                return true;
            }, IntPtr.Zero);
            
            return foundHandle;
        }
        
        // 专门针对Qt5窗口的按钮查找方法
        private IntPtr FindQt5Button(IntPtr parentHandle, string buttonText)
        {
            IntPtr foundHandle = IntPtr.Zero;
            
            // 尝试使用不同的Qt5按钮类名
            string[] qtButtonClasses = { "QPushButton", "QToolButton", "QAbstractButton", "Button", "PushButton" };
            
            foreach (string qtClass in qtButtonClasses)
            {
                IntPtr buttonHandle = FindWindowEx(parentHandle, IntPtr.Zero, qtClass, buttonText);
                if (buttonHandle != IntPtr.Zero)
                {
                    Logger.Info($"找到Qt5 {buttonText}按钮: {buttonHandle}");
                    return buttonHandle;
                }
            }
            
            // 尝试使用FindWindowEx递归查找所有子窗口
            IntPtr childHandle = IntPtr.Zero;
            do
            {
                childHandle = FindWindowEx(parentHandle, childHandle, null, null);
                if (childHandle != IntPtr.Zero)
                {
                    // 获取窗口类名
                    StringBuilder className = new StringBuilder(256);
                    GetClassName(childHandle, className, className.Capacity);
                    string classNameStr = className.ToString();
                    
                    // 获取窗口文本
                    StringBuilder windowText = new StringBuilder(256);
                    GetWindowText(childHandle, windowText, windowText.Capacity);
                    string windowTextStr = windowText.ToString();
                    
                    // 检查是否为Qt按钮且文本匹配
                    if ((classNameStr.Contains("QPushButton") || classNameStr.Contains("QToolButton") || 
                         classNameStr.Contains("Button") || classNameStr.Contains("PushButton")) && 
                        windowTextStr == buttonText)
                    {
                        Logger.Info($"通过FindWindowEx找到Qt5 {buttonText}按钮: {childHandle}");
                        return childHandle;
                    }
                    
                    // 递归查找子窗口
                    IntPtr grandChildHandle = FindQt5Button(childHandle, buttonText);
                    if (grandChildHandle != IntPtr.Zero)
                    {
                        return grandChildHandle;
                    }
                }
            } while (childHandle != IntPtr.Zero);
            
            // 尝试使用EnumChildWindows作为备用方案
            EnumChildWindows(parentHandle, (hwnd, lParam) =>
            {
                StringBuilder className = new StringBuilder(256);
                GetClassName(hwnd, className, className.Capacity);
                string classNameStr = className.ToString();
                
                StringBuilder windowText = new StringBuilder(256);
                GetWindowText(hwnd, windowText, windowText.Capacity);
                string windowTextStr = windowText.ToString();
                
                // 检查是否为Qt按钮且文本匹配
                if ((classNameStr.Contains("QPushButton") || classNameStr.Contains("QToolButton") || 
                     classNameStr.Contains("Button") || classNameStr.Contains("PushButton")) && 
                    windowTextStr == buttonText)
                {
                    Logger.Info($"通过EnumChildWindows找到Qt5 {buttonText}按钮: {hwnd}");
                    foundHandle = hwnd;
                    return false;
                }
                
                // 继续递归查找
                return true;
            }, IntPtr.Zero);
            
            return foundHandle;
        }
        
        // 通过枚举查找按钮
        private IntPtr FindButtonByEnumeration(IntPtr parentHandle, string buttonText)
        {
            IntPtr foundHandle = IntPtr.Zero;
            
            EnumChildWindows(parentHandle, (hwnd, lParam) =>
            {
                StringBuilder windowText = new StringBuilder(256);
                GetWindowText(hwnd, windowText, windowText.Capacity);
                string windowTextStr = windowText.ToString();
                
                // 检查窗口文本是否匹配
                if (windowTextStr == buttonText)
                {
                    // 检查是否为按钮控件
                    StringBuilder className = new StringBuilder(256);
                    GetClassName(hwnd, className, className.Capacity);
                    string classNameStr = className.ToString();
                    
                    if (IsButtonControl(classNameStr))
                    {
                        Logger.Debug($"找到按钮控件: 句柄={hwnd}, 类名={classNameStr}, 文本={windowTextStr}");
                        foundHandle = hwnd;
                        return false;
                    }
                }
                
                // 递归查找子窗口
                IntPtr childFoundHandle = FindButtonByEnumeration(hwnd, buttonText);
                if (childFoundHandle != IntPtr.Zero)
                {
                    foundHandle = childFoundHandle;
                    return false;
                }
                
                return true;
            }, IntPtr.Zero);
            
            return foundHandle;
        }
        
        // 查找任何按钮
        private IntPtr FindAnyButton(IntPtr parentHandle)
        {
            IntPtr foundHandle = IntPtr.Zero;
            
            EnumChildWindows(parentHandle, (hwnd, lParam) =>
            {
                StringBuilder className = new StringBuilder(256);
                GetClassName(hwnd, className, className.Capacity);
                string classNameStr = className.ToString();
                
                StringBuilder windowText = new StringBuilder(256);
                GetWindowText(hwnd, windowText, windowText.Capacity);
                string windowTextStr = windowText.ToString();
                
                Logger.Debug($"检查按钮窗口: 句柄={hwnd}, 类名={classNameStr}, 文本={windowTextStr}");
                
                // 检查是否为按钮控件
                if (IsButtonControl(classNameStr))
                {
                    foundHandle = hwnd;
                    return false;
                }
                
                // 递归查找子窗口
                IntPtr childFoundHandle = FindAnyButton(hwnd);
                if (childFoundHandle != IntPtr.Zero)
                {
                    foundHandle = childFoundHandle;
                    return false;
                }
                
                return true;
            }, IntPtr.Zero);
            
            return foundHandle;
        }
        
        // 检查是否为按钮控件
        private bool IsButtonControl(string className)
        {
            string[] buttonControlClasses = {
                "Button", "PushButton", "QPushButton", "QToolButton",
                "button", "pushbutton", "QPushButton", "Button"
            };
            
            foreach (string controlClass in buttonControlClasses)
            {
                if (className == controlClass || className.Contains(controlClass))
                {
                    return true;
                }
            }
            return false;
        }
        
        // 检测按钮是否被点击
        public bool IsButtonClicked(IntPtr buttonHandle)
        {
            if (buttonHandle == IntPtr.Zero)
            {
                return false;
            }
            
            try
            {
                // 获取按钮文本
                StringBuilder buttonText = new StringBuilder(256);
                GetWindowText(buttonHandle, buttonText, buttonText.Capacity);
                string text = buttonText.ToString();
                
                // 检查按钮文本是否为应用按钮
                string[] validButtonTexts = { "应用", "确定", "OK" };
                bool isValidButton = false;
                foreach (string validText in validButtonTexts)
                {
                    if (text == validText)
                    {
                        isValidButton = true;
                        break;
                    }
                }
                
                if (!isValidButton)
                {
                    // 不是应用按钮，直接返回false
                    return false;
                }
                
                // 检查按钮是否有焦点
                IntPtr focusedWindow = GetFocus();
                if (focusedWindow == buttonHandle)
                {
                    // 检查鼠标左键是否被按下
                    if (GetAsyncKeyState(VK_LBUTTON) < 0)
                    {
                        // 检查鼠标位置是否在按钮范围内
                        RECT buttonRect = new RECT();
                        if (GetWindowRect(buttonHandle, ref buttonRect))
                        {
                            POINT mousePos;
                            if (GetCursorPos(out mousePos))
                            {
                                bool isMouseOver = mousePos.X >= buttonRect.Left && 
                                                 mousePos.X <= buttonRect.Right && 
                                                 mousePos.Y >= buttonRect.Top && 
                                                 mousePos.Y <= buttonRect.Bottom;
                                if (isMouseOver)
                                {
                                    Logger.Debug("检测到鼠标点击应用按钮");
                                    return true;
                                }
                            }
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"检测按钮状态时出错: {ex.Message}");
                return false;
            }
        }
        
        // Win32 API 定义：获取异步键盘状态
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        
        // Win32 API 定义：获取光标位置
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);
        
        // 结构体定义
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }
        
        // 常量定义
        private const int VK_LBUTTON = 0x01;

        // 获取输入框文本
        public string GetInputText(IntPtr editHandle)
        {
            if (editHandle == IntPtr.Zero)
            {
                Logger.Warning("输入框句柄为空，无法获取文本");
                return string.Empty;
            }

            try
            {
                // 尝试使用 GetWindowText 获取文本
                StringBuilder sb = new StringBuilder(256);
                int length = GetWindowText(editHandle, sb, sb.Capacity);
                if (length > 0)
                {
                    string text = sb.ToString();
                    Logger.Debug($"通过 GetWindowText 获取到输入框文本: {text}");
                    return text;
                }
                else
                {
                    // 尝试使用 SendMessage WM_GETTEXT 获取文本
                    StringBuilder sb2 = new StringBuilder(256);
                    SendMessage(editHandle, WM_GETTEXT, (IntPtr)256, sb2);
                    string text = sb2.ToString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        Logger.Debug($"通过 WM_GETTEXT 获取到输入框文本: {text}");
                        return text;
                    }
                    else
                    {
                        // 尝试使用 SendMessage WM_GETTEXTLENGTH 获取文本长度，然后获取文本
                        int textLength = (int)SendMessage(editHandle, 0x000E, IntPtr.Zero, IntPtr.Zero); // WM_GETTEXTLENGTH
                        if (textLength > 0)
                        {
                            StringBuilder sb3 = new StringBuilder(textLength + 1);
                            SendMessage(editHandle, WM_GETTEXT, (IntPtr)(textLength + 1), sb3);
                            string text3 = sb3.ToString();
                            if (!string.IsNullOrEmpty(text3))
                            {
                                Logger.Debug($"通过 WM_GETTEXTLENGTH + WM_GETTEXT 获取到输入框文本: {text3}");
                                return text3;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"获取输入框文本时出错: {ex.Message}");
            }

            return string.Empty;
        }

        // 定位「打开文件密码(O)」标签
        public IntPtr FindOpenPasswordLabel(IntPtr dialogHandle)
        {
            if (dialogHandle == IntPtr.Zero)
            {
                Logger.Warning("对话框句柄为空，无法定位打开文件密码标签");
                return IntPtr.Zero;
            }

            // 尝试查找「打开文件密码(O)」标签
            IntPtr labelHandle = FindLabelByPartialText(dialogHandle, "打开文件密码");
            if (labelHandle != IntPtr.Zero)
            {
                Logger.Debug("找到打开文件密码标签");
                return labelHandle;
            }
            else
            {
                Logger.Warning("未找到打开文件密码标签");
            }
            return labelHandle;
        }

        // 根据部分文本查找标签（支持Qt窗口）
        private IntPtr FindLabelByPartialText(IntPtr parentHandle, string partialText)
        {
            IntPtr foundHandle = IntPtr.Zero;
            
            // 枚举所有子窗口
            EnumChildWindows(parentHandle, (hwnd, lParam) =>
            {
                // 获取窗口类名
                StringBuilder className = new StringBuilder(256);
                GetClassName(hwnd, className, className.Capacity);
                string classNameStr = className.ToString();
                
                // 获取窗口文本
                StringBuilder windowText = new StringBuilder(256);
                GetWindowText(hwnd, windowText, windowText.Capacity);
                string windowTextStr = windowText.ToString();
                
                // 记录当前检查的窗口
                Logger.Debug($"检查窗口: 句柄={hwnd}, 类名={classNameStr}, 文本={windowTextStr}");
                
                // 检查是否包含目标文本（不限制控件类型）
                if (windowTextStr.Contains(partialText))
                {
                    foundHandle = hwnd;
                    return false; // 找到后停止枚举
                }
                
                // 递归查找子窗口
                IntPtr childFoundHandle = FindLabelByPartialText(hwnd, partialText);
                if (childFoundHandle != IntPtr.Zero)
                {
                    foundHandle = childFoundHandle;
                    return false; // 找到后停止枚举
                }
                
                return true; // 继续枚举
            }, IntPtr.Zero);
            
            return foundHandle;
        }

        // 获取窗口矩形
        public RECT GetWindowRect(IntPtr hWnd)
        {
            RECT rect = new RECT();
            GetWindowRect(hWnd, ref rect);
            return rect;
        }

        // 解析文档路径
        public string GetDocumentPath(IntPtr dialogHandle)
        {
            try
            {
                // 尝试获取当前活动的WPS窗口
                IntPtr activeWindow = GetForegroundWindow();
                if (activeWindow != IntPtr.Zero)
                {
                    // 获取窗口标题
                    StringBuilder windowTitle = new StringBuilder(256);
                    GetWindowText(activeWindow, windowTitle, windowTitle.Capacity);
                    string title = windowTitle.ToString();
                    Logger.Debug($"当前活动窗口: {activeWindow}, 标题: {title}");
                    
                    // 尝试从活动窗口标题中提取文档名
                    if (!string.IsNullOrEmpty(title) && title.Contains(" - WPS Office"))
                    {
                        string docName = title.Replace(" - WPS Office", "");
                        Logger.Debug($"从活动窗口标题中提取的文档名: {docName}");
                    }
                }
                
                // 尝试获取所有WPS进程，查找包含文档路径的进程
                Process[] wpsProcesses = Process.GetProcessesByName("wps");
                foreach (Process wpsProcess in wpsProcesses)
                {
                    try
                    {
                        // 获取主模块文件路径
                        string mainModulePath = wpsProcess.MainModule.FileName;
                        Logger.Debug($"WPS 进程 {wpsProcess.Id} 主模块路径: {mainModulePath}");
                        
                        // 尝试获取进程的主窗口标题
                        StringBuilder mainWindowTitle = new StringBuilder(256);
                        GetWindowText(wpsProcess.MainWindowHandle, mainWindowTitle, mainWindowTitle.Capacity);
                        string mainTitle = mainWindowTitle.ToString();
                        Logger.Debug($"WPS 进程 {wpsProcess.Id} 主窗口标题: {mainTitle}");
                        
                        // 尝试从主窗口标题中提取文档路径
                        // WPS的主窗口标题通常格式为：文档名 - WPS Office
                        if (!string.IsNullOrEmpty(mainTitle) && mainTitle.Contains(" - WPS Office"))
                        {
                            string docName = mainTitle.Replace(" - WPS Office", "");
                            Logger.Debug($"从主窗口标题中提取的文档名: {docName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"处理WPS进程 {wpsProcess.Id} 时出错: {ex.Message}");
                    }
                }
                
                // 尝试查找最近打开的WPS文档
                string recentDocsPath = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
                Logger.Debug($"最近文档路径: {recentDocsPath}");
                
                // 查找最近的WPS文档
                string[] recentFiles = Directory.GetFiles(recentDocsPath, "*.lnk");
                Array.Sort(recentFiles, (a, b) => File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)));
                
                foreach (string lnkPath in recentFiles)
                {
                    try
                    {
                        // 解析快捷方式，获取目标文件路径
                        string targetPath = ResolveShortcut(lnkPath);
                        if (!string.IsNullOrEmpty(targetPath) && 
                            (targetPath.EndsWith(".docx", StringComparison.OrdinalIgnoreCase) ||
                             targetPath.EndsWith(".doc", StringComparison.OrdinalIgnoreCase) ||
                             targetPath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                             targetPath.EndsWith(".xls", StringComparison.OrdinalIgnoreCase) ||
                             targetPath.EndsWith(".pptx", StringComparison.OrdinalIgnoreCase) ||
                             targetPath.EndsWith(".ppt", StringComparison.OrdinalIgnoreCase)) &&
                            System.IO.File.Exists(targetPath))
                        {
                            Logger.Debug($"找到最近的WPS文档: {targetPath}");
                            return targetPath;
                        }
                    }
                    catch { }
                }
                
                // 尝试在常见的文档目录中查找WPS文档
                string[] commonDocFolders = {
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\WPS Cloud Files",
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\test"
                };
                
                foreach (string folder in commonDocFolders)
                {
                    try
                    {
                        if (System.IO.Directory.Exists(folder))
                        {
                            string[] wpsFiles = System.IO.Directory.GetFiles(folder, "*.docx").Concat(
                                System.IO.Directory.GetFiles(folder, "*.doc")).Concat(
                                System.IO.Directory.GetFiles(folder, "*.xlsx")).Concat(
                                System.IO.Directory.GetFiles(folder, "*.xls")).Concat(
                                System.IO.Directory.GetFiles(folder, "*.pptx")).Concat(
                                System.IO.Directory.GetFiles(folder, "*.ppt")).ToArray();
                            
                            // 按修改时间排序，返回最近修改的文件
                            Array.Sort(wpsFiles, (a, b) => System.IO.File.GetLastWriteTime(b).CompareTo(System.IO.File.GetLastWriteTime(a)));
                            
                            if (wpsFiles.Length > 0)
                            {
                                string recentFile = wpsFiles[0];
                                Logger.Debug($"在 {folder} 中找到最近的WPS文档: {recentFile}");
                                return recentFile;
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"获取文档路径时出错: {ex.Message}");
            }
            
            return string.Empty;
        }

        // 解析快捷方式，获取目标文件路径
        private string ResolveShortcut(string lnkPath)
        {
            try
            {
                // 使用Shell32 COM组件解析快捷方式
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                string targetPath = shortcut.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, shortcut, null) as string;
                return targetPath;
            }
            catch { }
            return string.Empty;
        }

        // 获取 DPI 缩放比例
        public float GetDpiScale()
        {
            IntPtr hWnd = Process.GetCurrentProcess().MainWindowHandle;
            if (hWnd == IntPtr.Zero)
            {
                // 如果没有主窗口，使用屏幕 DPI
                return 1.0f;
            }

            IntPtr hMonitor = MonitorFromWindow(hWnd, 0);
            uint dpiX, dpiY;
            GetDpiForMonitor(hMonitor, MonitorDpiType.MDT_EFFECTIVE_DPI, out dpiX, out dpiY);
            return dpiX / 96.0f;
        }

        // 检测系统版本
        public string GetWindowsVersion()
        {
            return Environment.OSVersion.VersionString;
        }

        // 检测 WPS 版本
        public string GetWpsVersion()
        {
            Process[] processes = Process.GetProcessesByName("wps");
            if (processes.Length > 0)
            {
                try
                {
                    ProcessModule mainModule = processes[0].MainModule;
                    if (mainModule != null)
                    {
                        FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(mainModule.FileName);
                        return versionInfo.FileVersion;
                    }
                }
                catch { }
            }
            return string.Empty;
        }
    }
}