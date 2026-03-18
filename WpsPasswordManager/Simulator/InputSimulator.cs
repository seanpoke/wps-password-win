using System;
using System.Runtime.InteropServices;

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
    }
}