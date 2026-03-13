using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using WpsPasswordManager.Utils;

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
            // 双基准环境特征匹配
            IntPtr dialogHandle = FindWindow("#32770", "文档加密");
            if (dialogHandle != IntPtr.Zero)
            {
                LogWindowInfo("找到文档加密窗口", dialogHandle);
                return dialogHandle;
            }
            
            // 尝试查找"密码加密"窗口
            dialogHandle = FindWindow("#32770", "密码加密");
            if (dialogHandle != IntPtr.Zero)
            {
                LogWindowInfo("找到密码加密窗口", dialogHandle);
                return dialogHandle;
            }
            
            // 尝试查找"文档已加密"窗口（解密对话框）
            dialogHandle = FindWindow("#32770", "文档已加密");
            if (dialogHandle != IntPtr.Zero)
            {
                LogWindowInfo("找到文档已加密窗口（解密对话框）", dialogHandle);
                return dialogHandle;
            }
            
            // 兼容环境模糊匹配所有窗口（包括子窗口）
            dialogHandle = FindWindowByPartialTitleAll("密码");
            if (dialogHandle != IntPtr.Zero)
            {
                LogWindowInfo("通过模糊匹配找到密码相关窗口", dialogHandle);
            }
            else
            {
                // 尝试模糊匹配"文档已加密"
                dialogHandle = FindWindowByPartialTitleAll("文档已加密");
                if (dialogHandle != IntPtr.Zero)
                {
                    LogWindowInfo("通过模糊匹配找到文档已加密窗口", dialogHandle);
                }
                else
                {
                    Logger.Debug("未找到密码对话框");
                }
            }
            return dialogHandle;
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
                    // 枚举进程的所有窗口
                    EnumWindowsProc callback = (IntPtr windowHandle, IntPtr lParam) =>
                    {
                        uint processId;
                        GetWindowThreadProcessId(windowHandle, out processId);
                        
                        if (processId == process.Id)
                        {
                            StringBuilder windowTitle = new StringBuilder(256);
                            GetWindowText(windowHandle, windowTitle, windowTitle.Capacity);
                            
                            if (windowTitle.ToString().Contains(partialTitle))
                            {
                                hWnd = windowHandle;
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

        // 枚举窗口的委托
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        // 枚举所有顶级窗口
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        
        // Win32 API 定义：设置窗口为前台窗口
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        
        // Win32 API 定义：获取焦点窗口
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetFocus();
        
        // Win32 API 定义：模拟键盘事件
        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        // 定位密码输入框
        public IntPtr FindPasswordEdit(IntPtr dialogHandle)
        {
            if (dialogHandle == IntPtr.Zero)
            {
                Logger.Warning("对话框句柄为空，无法定位密码输入框");
                return IntPtr.Zero;
            }

            Logger.Debug($"开始定位密码输入框，对话框句柄: {dialogHandle}");

            // 尝试使用 FindWindowEx 查找所有子窗口
            IntPtr childHandle = IntPtr.Zero;
            do
            {
                childHandle = FindWindowEx(dialogHandle, childHandle, null, null);
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
                    
                    Logger.Debug($"检查窗口: 句柄={childHandle}, 类名={classNameStr}, 文本={windowTextStr}");
                    
                    // 尝试向窗口发送 WM_SETTEXT 消息，看是否能设置文本
                    bool canSetText = SetWindowText(childHandle, "test");
                    if (canSetText)
                    {
                        Logger.Debug($"找到可设置文本的窗口: 句柄={childHandle}, 类名={classNameStr}");
                        return childHandle;
                    }
                    
                    // 递归查找子窗口
                    IntPtr grandChildHandle = FindPasswordEdit(childHandle);
                    if (grandChildHandle != IntPtr.Zero)
                    {
                        return grandChildHandle;
                    }
                }
            } while (childHandle != IntPtr.Zero);
            
            Logger.Warning("未找到密码输入框");
            return IntPtr.Zero;
        }
        
        // Win32 API 定义：设置窗口文本
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetWindowText(IntPtr hWnd, string lpString);
        
        // 查找Qt编辑控件
        private IntPtr FindQtEditControl(IntPtr parentHandle)
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
                        
                        // 检查是否为Qt编辑控件
                        if (classNameStr.Contains("QLineEdit") || classNameStr.Contains("QTextEdit") || 
                            classNameStr.Contains("QPlainTextEdit") || classNameStr.Contains("LineEdit"))
                        {
                            Logger.Debug($"找到Qt编辑控件: 句柄={childHandle}, 类名={classNameStr}");
                            return childHandle;
                        }
                        
                        // 递归查找子窗口
                        IntPtr foundHandle = FindQtEditControl(childHandle);
                        if (foundHandle != IntPtr.Zero)
                        {
                            return foundHandle;
                        }
                    }
                } while (childHandle != IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Logger.Error($"查找Qt编辑控件时出错: {ex.Message}");
            }
            return IntPtr.Zero;
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
            // 这里需要根据实际情况实现
            // 可以通过获取对话框的关联进程，然后分析进程的命令行参数
            // 或者通过其他方式获取文档路径
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