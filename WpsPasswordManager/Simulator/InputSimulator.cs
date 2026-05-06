using System;
using System.Runtime.InteropServices;
using System.Text;

namespace WpsPasswordManager.Simulator
{
    public class InputSimulator
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetWindowText(IntPtr hWnd, string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetFocus();

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint WM_SETTEXT = 0x000C;
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;
        private const int SW_MINIMIZE = 6;
        private const int SW_RESTORE = 9;
        private const uint WM_COMMAND = 0x0111;
        private const int VK_ENTER = 0x0D;

        public void SimulatePasswordInput(IntPtr editHandle, string password)
        {
            SetForegroundWindow(editHandle);
            System.Threading.Thread.Sleep(50);

            bool success = SetWindowText(editHandle, password);
            if (success)
            {
                return;
            }

            SimulateTextInput(password);
        }

        public void SimulateTextInput(string text)
        {
            foreach (char c in text)
            {
                SimulateKeyPress(c);
                System.Threading.Thread.Sleep(10);
            }
        }

        public void SimulatePasswordInputWithTab(IntPtr dialogHandle, string password)
        {
            SetForegroundWindow(dialogHandle);
            System.Threading.Thread.Sleep(100);

            SelectAllAndDelete();
            SimulateTextInput(password);

            keybd_event(0x09, 0, 0, UIntPtr.Zero);
            keybd_event(0x09, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            System.Threading.Thread.Sleep(50);

            SelectAllAndDelete();
            SimulateTextInput(password);
        }

        public void SelectAllAndDelete()
        {
            keybd_event(0x11, 0, 0, UIntPtr.Zero);
            keybd_event(0x41, 0, 0, UIntPtr.Zero);
            keybd_event(0x41, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(0x11, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            System.Threading.Thread.Sleep(50);

            keybd_event(0x2E, 0, 0, UIntPtr.Zero);
            keybd_event(0x2E, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            System.Threading.Thread.Sleep(50);
        }

        private void SimulateKeyPress(char c)
        {
            byte virtualKey = GetVirtualKey(c);
            bool shiftNeeded = char.IsUpper(c) || IsShiftRequired(c);

            if (shiftNeeded)
            {
                keybd_event(0x10, 0, 0, UIntPtr.Zero);
            }

            keybd_event(virtualKey, 0, 0, UIntPtr.Zero);
            keybd_event(virtualKey, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

            if (shiftNeeded)
            {
                keybd_event(0x10, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
        }

        private byte GetVirtualKey(char c)
        {
            c = char.ToUpper(c);
            switch (c)
            {
                case '0': return 0x30;
                case '1': return 0x31;
                case '2': return 0x32;
                case '3': return 0x33;
                case '4': return 0x34;
                case '5': return 0x35;
                case '6': return 0x36;
                case '7': return 0x37;
                case '8': return 0x38;
                case '9': return 0x39;
                case 'A': return 0x41;
                case 'B': return 0x42;
                case 'C': return 0x43;
                case 'D': return 0x44;
                case 'E': return 0x45;
                case 'F': return 0x46;
                case 'G': return 0x47;
                case 'H': return 0x48;
                case 'I': return 0x49;
                case 'J': return 0x4A;
                case 'K': return 0x4B;
                case 'L': return 0x4C;
                case 'M': return 0x4D;
                case 'N': return 0x4E;
                case 'O': return 0x4F;
                case 'P': return 0x50;
                case 'Q': return 0x51;
                case 'R': return 0x52;
                case 'S': return 0x53;
                case 'T': return 0x54;
                case 'U': return 0x55;
                case 'V': return 0x56;
                case 'W': return 0x57;
                case 'X': return 0x58;
                case 'Y': return 0x59;
                case 'Z': return 0x5A;
                case '!': return 0x31;
                case '@': return 0x32;
                case '#': return 0x33;
                case '$': return 0x34;
                case '%': return 0x35;
                case '^': return 0x36;
                case '&': return 0x37;
                case '*': return 0x38;
                case '(': return 0x39;
                case ')': return 0x30;
                case '-': return 0xBD;
                case '_': return 0xBD;
                case '=': return 0xBB;
                case '+': return 0xBB;
                case '[': return 0xDB;
                case '{': return 0xDB;
                case ']': return 0xDD;
                case '}': return 0xDD;
                case '\\': return 0xDC;
                case '|': return 0xDC;
                case ';': return 0xBA;
                case ':': return 0xBA;
                case '\'': return 0xDE;
                case '"': return 0xDE;
                case ',': return 0xBC;
                case '<': return 0xBC;
                case '.': return 0xBE;
                case '>': return 0xBE;
                case '/': return 0xBF;
                case '?': return 0xBF;
                default: return (byte)c;
            }
        }

        private bool IsShiftRequired(char c)
        {
            string shiftChars = "!@#$%^&*()_+{}|:\"<>?";
            return shiftChars.Contains(c);
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        public void ClearInput(IntPtr editHandle)
        {
            SetForegroundWindow(editHandle);
            System.Threading.Thread.Sleep(50);
            SelectAllAndDelete();
        }

        public void SimulateMouseClick(IntPtr buttonHandle)
        {
            RECT rect = new RECT();
            if (GetWindowRect(buttonHandle, ref rect))
            {
                int x = (rect.Left + rect.Right) / 2;
                int y = (rect.Top + rect.Bottom) / 2;

                SetCursorPos(x, y);
                System.Threading.Thread.Sleep(50);

                mouse_event(MOUSEEVENTF_LEFTDOWN, x, y, 0, UIntPtr.Zero);
                mouse_event(MOUSEEVENTF_LEFTUP, x, y, 0, UIntPtr.Zero);
            }
            else
            {
                PostMessage(buttonHandle, WM_LBUTTONDOWN, IntPtr.Zero, IntPtr.Zero);
                System.Threading.Thread.Sleep(50);
                PostMessage(buttonHandle, WM_LBUTTONUP, IntPtr.Zero, IntPtr.Zero);
            }
        }

        public void SimulateEnterKey()
        {
            keybd_event((byte)VK_ENTER, 0, 0, UIntPtr.Zero);
            keybd_event((byte)VK_ENTER, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public void SimulateTabKey()
        {
            keybd_event(0x09, 0, 0, UIntPtr.Zero);
            keybd_event(0x09, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public void SimulateHomeKey()
        {
            keybd_event(0x24, 0, 0, UIntPtr.Zero);
            keybd_event(0x24, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public void SimulateCtrlS()
        {
            keybd_event(0x11, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(0x53, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            System.Threading.Thread.Sleep(50);

            keybd_event(0x11, 0, 0, UIntPtr.Zero);
            System.Threading.Thread.Sleep(100);
            keybd_event(0x53, 0, 0, UIntPtr.Zero);
            System.Threading.Thread.Sleep(100);
            keybd_event(0x53, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            System.Threading.Thread.Sleep(50);
            keybd_event(0x11, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            System.Threading.Thread.Sleep(200);
        }

        private IntPtr FindWpsMainWindow()
        {
            IntPtr foundHandle = IntPtr.Zero;
            EnumWindows((IntPtr hWnd, IntPtr lParam) =>
            {
                StringBuilder windowTitle = new StringBuilder(256);
                GetWindowText(hWnd, windowTitle, windowTitle.Capacity);
                string title = windowTitle.ToString();

                if (title.Contains("WPS") || title.Contains("wps"))
                {
                    StringBuilder className = new StringBuilder(256);
                    GetClassName(hWnd, className, className.Capacity);
                    string classStr = className.ToString();

                    uint processId;
                    GetWindowThreadProcessId(hWnd, out processId);
                    try
                    {
                        System.Diagnostics.Process process = System.Diagnostics.Process.GetProcessById((int)processId);
                        if (title.Contains(" - WPS Office") && process.ProcessName == "wps")
                        {
                            foundHandle = hWnd;
                            return false;
                        }
                    }
                    catch { }
                }
                return true;
            }, IntPtr.Zero);

            if (foundHandle == IntPtr.Zero)
            {
                System.Diagnostics.Process[] wpsProcesses = System.Diagnostics.Process.GetProcessesByName("wps");
                foreach (System.Diagnostics.Process process in wpsProcesses)
                {
                    try
                    {
                        if (process.MainWindowHandle != IntPtr.Zero)
                        {
                            foundHandle = process.MainWindowHandle;
                            break;
                        }
                    }
                    catch { }
                }
            }
            return foundHandle;
        }
    }
}
