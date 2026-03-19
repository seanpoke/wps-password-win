using System;
using System.Runtime.InteropServices;
using System.Text;

namespace WpsPasswordManager.Simulator
{
    public class InputSimulator
    {
        // Win32 API 定义
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        // 常量定义
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint WM_SETTEXT = 0x000C;
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;

        // 模拟密码输入
        public void SimulatePasswordInput(IntPtr editHandle, string password)
        {
            // 确保窗口在前台
            SetForegroundWindow(editHandle);
            System.Threading.Thread.Sleep(50);

            // 先尝试使用 WM_SETTEXT 消息设置文本
            bool success = SetWindowText(editHandle, password);
            if (success)
            {
                // 如果成功，直接返回
                return;
            }
            
            // 如果失败，回退到模拟按键输入
            // 模拟输入密码
            foreach (char c in password)
            {
                SimulateKeyPress(c);
                System.Threading.Thread.Sleep(10);
            }
        }

        // 模拟密码输入（当找不到输入框时）
        public void SimulatePasswordInputWithTab(IntPtr dialogHandle, string password)
        {
            // 确保窗口在前台
            SetForegroundWindow(dialogHandle);
            System.Threading.Thread.Sleep(100);
            
            // 清空第一个输入框
            // 模拟Ctrl+A选择所有文本
            keybd_event(0x11, 0, 0, UIntPtr.Zero); // Ctrl键
            keybd_event(0x41, 0, 0, UIntPtr.Zero); // A键
            keybd_event(0x41, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(0x11, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            System.Threading.Thread.Sleep(50);
            
            // 模拟Delete键
            keybd_event(0x2E, 0, 0, UIntPtr.Zero);
            keybd_event(0x2E, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            System.Threading.Thread.Sleep(50);
            
            // 输入密码（第一个输入框）
            foreach (char c in password)
            {
                SimulateKeyPress(c);
                System.Threading.Thread.Sleep(10);
            }
            
            // 按一次Tab键，切换到「再次输入密码(P)」输入框
            keybd_event(0x09, 0, 0, UIntPtr.Zero); // Tab键
            keybd_event(0x09, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            System.Threading.Thread.Sleep(50);
            
            // 清空第二个输入框
            // 模拟Ctrl+A选择所有文本
            keybd_event(0x11, 0, 0, UIntPtr.Zero); // Ctrl键
            keybd_event(0x41, 0, 0, UIntPtr.Zero); // A键
            keybd_event(0x41, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(0x11, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            System.Threading.Thread.Sleep(50);
            
            // 模拟Delete键
            keybd_event(0x2E, 0, 0, UIntPtr.Zero);
            keybd_event(0x2E, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            System.Threading.Thread.Sleep(50);
            
            // 输入确认密码
            foreach (char c in password)
            {
                SimulateKeyPress(c);
                System.Threading.Thread.Sleep(10);
            }
        }

        // Win32 API 定义：设置窗口文本
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetWindowText(IntPtr hWnd, string lpString);

        // Win32 API 定义：获取当前活动窗口
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetForegroundWindow();

        // Win32 API 定义：获取当前活动窗口的线程ID和进程ID
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        // Win32 API 定义：获取焦点窗口
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetFocus();

        // 模拟按键
        private void SimulateKeyPress(char c)
        {
            byte virtualKey = (byte)char.ToUpper(c);
            
            // 按下键
            keybd_event(virtualKey, 0, 0, UIntPtr.Zero);
            // 释放键
            keybd_event(virtualKey, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        // 清空输入框
        public void ClearInput(IntPtr editHandle)
        {
            // 确保窗口在前台
            SetForegroundWindow(editHandle);
            System.Threading.Thread.Sleep(50);

            // 模拟Ctrl+A选择所有文本
            keybd_event(0x11, 0, 0, UIntPtr.Zero); // Ctrl键
            keybd_event(0x41, 0, 0, UIntPtr.Zero); // A键
            keybd_event(0x41, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(0x11, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            System.Threading.Thread.Sleep(50);

            // 模拟Delete键
            keybd_event(0x2E, 0, 0, UIntPtr.Zero);
            keybd_event(0x2E, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            System.Threading.Thread.Sleep(50);
        }

        // 模拟Ctrl+S快捷键
        public void SimulateCtrlS()
        {
            Console.WriteLine("=====================================");
            Console.WriteLine("开始执行保存操作");
            Console.WriteLine("=====================================");
            
            // 重置WPS主窗口查找状态
            IntPtr wpsMainWindow = IntPtr.Zero;
            
            // 尝试多次获取WPS主窗口，确保找到正确的窗口
            int maxRetries = 3;
            for (int i = 0; i < maxRetries; i++)
            {
                wpsMainWindow = FindWpsMainWindow();
                if (wpsMainWindow != IntPtr.Zero)
                {
                    Console.WriteLine($"第{i+1}次尝试找到WPS主窗口: {wpsMainWindow}");
                    break;
                }
                Console.WriteLine($"第{i+1}次尝试未找到WPS主窗口，等待后重试...");
                System.Threading.Thread.Sleep(200);
            }
            
            // 尝试获取WPS主窗口并设置为前台
            if (wpsMainWindow != IntPtr.Zero)
            {
                try
                {
                    // 先最小化再还原，确保窗口真正激活
                    Console.WriteLine("尝试最小化窗口");
                    ShowWindow(wpsMainWindow, SW_MINIMIZE);
                    System.Threading.Thread.Sleep(150);
                    
                    Console.WriteLine("尝试还原窗口");
                    ShowWindow(wpsMainWindow, SW_RESTORE);
                    System.Threading.Thread.Sleep(250);
                    
                    // 多次尝试设置窗口为前台，确保成功
                    for (int i = 0; i < 3; i++)
                    {
                        Console.WriteLine($"第{i+1}次尝试设置窗口为前台");
                        bool setForegroundSuccess = SetForegroundWindow(wpsMainWindow);
                        Console.WriteLine($"设置WPS主窗口为前台: {setForegroundSuccess}");
                        
                        // 验证窗口是否真的获得了焦点
                    IntPtr currentForeground = GetForegroundWindow();
                    Console.WriteLine($"当前前台窗口: {currentForeground}, 是否为WPS主窗口: {currentForeground == wpsMainWindow}");
                    
                    if (currentForeground == wpsMainWindow)
                    {
                        Console.WriteLine("窗口焦点设置成功！");
                        break;
                    }
                    System.Threading.Thread.Sleep(150);
                }
                
                // 最终确认焦点
                System.Threading.Thread.Sleep(300); // 增加延迟确保窗口完全激活
            }
            catch (Exception ex)
            {
                Console.WriteLine($"窗口操作出错: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("未找到WPS主窗口");
            // 尝试获取当前活动窗口
            IntPtr activeWindow = GetForegroundWindow();
            Console.WriteLine($"当前活动窗口: {activeWindow}");
            // 尝试将当前活动窗口设置为前台（可能已经是WPS窗口）
            SetForegroundWindow(activeWindow);
            System.Threading.Thread.Sleep(300);
        }
        
        // 模拟Ctrl+S
        try
        {
            Console.WriteLine("开始模拟Ctrl+S");
            // 先确保键盘状态干净
            keybd_event(0x11, 0, KEYEVENTF_KEYUP, UIntPtr.Zero); // 确保Ctrl键未按下
            keybd_event(0x53, 0, KEYEVENTF_KEYUP, UIntPtr.Zero); // 确保S键未按下
            System.Threading.Thread.Sleep(50);
            
            // 模拟按下Ctrl+S
            Console.WriteLine("按下Ctrl键");
            keybd_event(0x11, 0, 0, UIntPtr.Zero); // Ctrl键按下
            System.Threading.Thread.Sleep(100);
            
            Console.WriteLine("按下S键");
            keybd_event(0x53, 0, 0, UIntPtr.Zero); // S键按下
            System.Threading.Thread.Sleep(100);
            
            Console.WriteLine("释放S键");
            keybd_event(0x53, 0, KEYEVENTF_KEYUP, UIntPtr.Zero); // S键释放
            System.Threading.Thread.Sleep(50);
            
            Console.WriteLine("释放Ctrl键");
            keybd_event(0x11, 0, KEYEVENTF_KEYUP, UIntPtr.Zero); // Ctrl键释放
            System.Threading.Thread.Sleep(200);
            
            Console.WriteLine("模拟Ctrl+S完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"快捷键模拟出错: {ex.Message}");
        }
        
        // 额外尝试：使用SendMessage发送WM_COMMAND消息触发保存
        if (wpsMainWindow != IntPtr.Zero)
        {
            try
            {
                Console.WriteLine("尝试使用SendMessage发送保存命令");
                // 发送WM_COMMAND消息，ID_FILE_SAVE = 3
                IntPtr result = SendMessage(wpsMainWindow, WM_COMMAND, (IntPtr)3, IntPtr.Zero);
                Console.WriteLine($"SendMessage保存命令已发送，返回值: {result}");
                System.Threading.Thread.Sleep(500);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SendMessage出错: {ex.Message}");
            }
        }
        
        // 额外尝试：使用更通用的保存命令
        if (wpsMainWindow != IntPtr.Zero)
        {
            try
            {
                Console.WriteLine("尝试使用更通用的保存命令");
                // 发送WM_COMMAND消息，使用不同的ID
                IntPtr result = SendMessage(wpsMainWindow, WM_COMMAND, (IntPtr)65499, IntPtr.Zero); // 尝试另一个可能的保存ID
                Console.WriteLine($"通用保存命令已发送，返回值: {result}");
                System.Threading.Thread.Sleep(300);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"通用保存命令出错: {ex.Message}");
            }
        }
        
        Console.WriteLine("=====================================");
        Console.WriteLine("保存操作执行完毕");
        Console.WriteLine("=====================================");
    }
        
        // 查找WPS主窗口
        private IntPtr FindWpsMainWindow()
        {
            Console.WriteLine("开始查找WPS主窗口");
            // 枚举所有顶级窗口
            IntPtr foundHandle = IntPtr.Zero;
            int windowCount = 0;
            
            EnumWindows((IntPtr hWnd, IntPtr lParam) =>
            {
                windowCount++;
                // 获取窗口标题
                StringBuilder windowTitle = new StringBuilder(256);
                GetWindowText(hWnd, windowTitle, windowTitle.Capacity);
                string title = windowTitle.ToString();
                
                // 检查是否是WPS相关窗口
                if (title.Contains("WPS") || title.Contains("wps"))
                {
                    // 获取窗口类名
                    StringBuilder className = new StringBuilder(256);
                    GetClassName(hWnd, className, className.Capacity);
                    string classStr = className.ToString();
                    
                    // 检查是否是WPS进程的窗口
                    uint processId;
                    GetWindowThreadProcessId(hWnd, out processId);
                    try
                    {
                        System.Diagnostics.Process process = System.Diagnostics.Process.GetProcessById((int)processId);
                        Console.WriteLine($"找到WPS相关窗口: 句柄={hWnd}, 标题={title}, 类名={classStr}, 进程名={process.ProcessName}");
                        
                        // 检查是否是主窗口（标题包含" - WPS Office"）
                        if (title.Contains(" - WPS Office") && process.ProcessName == "wps")
                        {
                            Console.WriteLine($"找到WPS主窗口: {hWnd}");
                            foundHandle = hWnd;
                            return false; // 找到后停止枚举
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"获取进程信息时出错: {ex.Message}");
                    }
                }
                return true; // 继续枚举
            }, IntPtr.Zero);
            
            Console.WriteLine($"窗口枚举完成，共检查 {windowCount} 个窗口");
            if (foundHandle != IntPtr.Zero)
            {
                Console.WriteLine($"成功找到WPS主窗口: {foundHandle}");
            }
            else
            {
                Console.WriteLine("未找到WPS主窗口");
                // 尝试直接获取所有WPS进程的主窗口
                Console.WriteLine("尝试直接获取WPS进程的主窗口");
                System.Diagnostics.Process[] wpsProcesses = System.Diagnostics.Process.GetProcessesByName("wps");
                Console.WriteLine($"找到 {wpsProcesses.Length} 个WPS进程");
                foreach (System.Diagnostics.Process process in wpsProcesses)
                {
                    try
                    {
                        if (process.MainWindowHandle != IntPtr.Zero)
                        {
                            StringBuilder windowTitle = new StringBuilder(256);
                            GetWindowText(process.MainWindowHandle, windowTitle, windowTitle.Capacity);
                            string title = windowTitle.ToString();
                            Console.WriteLine($"WPS进程 {process.Id} 主窗口: 句柄={process.MainWindowHandle}, 标题={title}");
                            if (!string.IsNullOrEmpty(title))
                            {
                                foundHandle = process.MainWindowHandle;
                                Console.WriteLine($"使用WPS进程主窗口: {foundHandle}");
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"处理WPS进程时出错: {ex.Message}");
                    }
                }
            }
            return foundHandle;
        }
        
        // Win32 API 定义：枚举所有顶级窗口
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        
        // 枚举窗口的委托
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        
        // Win32 API 定义：获取窗口文本
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        
        // Win32 API 定义：获取窗口类名
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
        
        // Win32 API 定义：显示窗口
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        // Win32 API 定义：发送消息
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        
        // 常量定义
        private const int SW_MINIMIZE = 6;
        private const int SW_RESTORE = 9;
        private const uint WM_COMMAND = 0x0111;

    }
}