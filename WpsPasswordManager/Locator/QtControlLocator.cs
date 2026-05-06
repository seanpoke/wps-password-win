using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using WpsPasswordManager.Utils;

namespace WpsPasswordManager.Locator
{
    public class QtControlLocator
    {
        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, StringBuilder lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetDlgCtrlID(IntPtr hWnd);

        private delegate bool EnumChildWindowsProc(IntPtr hwnd, IntPtr lParam);

        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const uint WM_GETTEXT = 0x000D;
        private const int GWL_STYLE = -16;
        private const uint ES_PASSWORD = 0x0020;

        private readonly List<string> _passwordEditClasses = new List<string>
        {
            "QLineEdit",
            "QTextEdit",
            "QPlainTextEdit",
            "LineEdit",
            "Edit",
            "KDPwdLineEditReveal",
            "Pwd",
            "Text",
            "Input",
            "Form",
            "Widget"
        };

        private readonly List<string> _buttonClasses = new List<string>
        {
            "QPushButton",
            "QToolButton",
            "Button",
            "PushButton",
            "CommandLinkButton"
        };

        public IntPtr FindPasswordEdit(IntPtr dialogHandle)
        {
            if (dialogHandle == IntPtr.Zero)
                return IntPtr.Zero;

            Logger.Debug($"开始定位密码输入框，对话框句柄: {dialogHandle}");

            IntPtr result = IntPtr.Zero;

            result = FindPasswordEditByQtClasses(dialogHandle);
            if (result != IntPtr.Zero)
                return result;

            result = FindPasswordEditByStandardClasses(dialogHandle);
            if (result != IntPtr.Zero)
                return result;

            result = FindPasswordEditByDialogControls(dialogHandle);
            if (result != IntPtr.Zero)
                return result;

            result = FindPasswordEditByPosition(dialogHandle);
            if (result != IntPtr.Zero)
                return result;

            Logger.Warning("未找到编辑控件");
            return IntPtr.Zero;
        }

        private IntPtr FindPasswordEditByQtClasses(IntPtr dialogHandle)
        {
            List<IntPtr> allControls = new List<IntPtr>();
            CollectAllControls(dialogHandle, allControls);

            Logger.Debug($"通过QT类名查找: 共找到 {allControls.Count} 个子控件");

            foreach (IntPtr control in allControls)
            {
                StringBuilder className = new StringBuilder(256);
                GetClassName(control, className, className.Capacity);
                string classStr = className.ToString();

                foreach (string editClass in _passwordEditClasses)
                {
                    if (classStr.Contains(editClass))
                    {
                        Logger.Debug($"找到匹配的QT编辑控件: 句柄={control}, 类名={classStr}");
                        
                        if (IsPasswordControl(control))
                        {
                            Logger.Info($"找到密码输入框: {control}");
                            return control;
                        }
                    }
                }
            }

            if (allControls.Count > 0)
            {
                foreach (IntPtr control in allControls)
                {
                    StringBuilder className = new StringBuilder(256);
                    GetClassName(control, className, className.Capacity);
                    string classStr = className.ToString();

                    if (classStr.StartsWith("Qt") || classStr.Contains("QWidget"))
                    {
                        IntPtr childEdit = FindEditInQtWidget(control);
                        if (childEdit != IntPtr.Zero)
                            return childEdit;
                    }
                }
            }

            return IntPtr.Zero;
        }

        private IntPtr FindEditInQtWidget(IntPtr widgetHandle)
        {
            List<IntPtr> children = new List<IntPtr>();
            CollectAllControls(widgetHandle, children);

            foreach (IntPtr child in children)
            {
                StringBuilder className = new StringBuilder(256);
                GetClassName(child, className, className.Capacity);
                string classStr = className.ToString();

                if (classStr.Contains("Edit") || classStr.Contains("Text"))
                {
                    if (IsPasswordControl(child))
                    {
                        Logger.Info($"在QT控件中找到密码输入框: {child}, 类名={classStr}");
                        return child;
                    }
                }
            }

            return IntPtr.Zero;
        }

        private IntPtr FindPasswordEditByStandardClasses(IntPtr dialogHandle)
        {
            IntPtr child = IntPtr.Zero;
            
            while (true)
            {
                child = FindWindowEx(dialogHandle, child, "Edit", null);
                if (child == IntPtr.Zero)
                    break;

                if (IsPasswordControl(child))
                {
                    Logger.Info($"通过标准类名找到密码输入框: {child}");
                    return child;
                }
            }

            return IntPtr.Zero;
        }

        private IntPtr FindPasswordEditByDialogControls(IntPtr dialogHandle)
        {
            List<IntPtr> controls = new List<IntPtr>();
            CollectAllControls(dialogHandle, controls);

            foreach (IntPtr control in controls)
            {
                int ctrlId = GetDlgCtrlID(control);
                if (ctrlId != 0)
                {
                    StringBuilder className = new StringBuilder(256);
                    GetClassName(control, className, className.Capacity);
                    
                    if (className.ToString().Contains("Edit") || IsPasswordControl(control))
                    {
                        Logger.Info($"通过对话框控件ID找到密码输入框: {control}, ID={ctrlId}");
                        return control;
                    }
                }
            }

            return IntPtr.Zero;
        }

        private IntPtr FindPasswordEditByPosition(IntPtr dialogHandle)
        {
            List<IntPtr> controls = new List<IntPtr>();
            CollectAllControls(dialogHandle, controls);

            if (controls.Count == 0)
                return IntPtr.Zero;

            List<IntPtr> sortedControls = controls.FindAll(c => IsVisibleAndSizable(c));
            sortedControls.Sort((a, b) => GetControlTop(a).CompareTo(GetControlTop(b)));

            foreach (IntPtr control in sortedControls)
            {
                StringBuilder className = new StringBuilder(256);
                GetClassName(control, className, className.Capacity);
                string classStr = className.ToString();

                if (IsPotentialEditControl(classStr))
                {
                    Logger.Info($"通过位置排序找到密码输入框: {control}, 类名={classStr}");
                    return control;
                }
            }

            if (sortedControls.Count > 0)
            {
                IntPtr firstControl = sortedControls[0];
                StringBuilder className = new StringBuilder(256);
                GetClassName(firstControl, className, className.Capacity);
                Logger.Info($"返回第一个可见控件作为密码输入框: {firstControl}, 类名={className}");
                return firstControl;
            }

            return IntPtr.Zero;
        }

        private bool IsVisibleAndSizable(IntPtr hWnd)
        {
            if (!IsWindowVisible(hWnd))
                return false;

            RECT rect = new RECT();
            if (!GetWindowRect(hWnd, ref rect))
                return false;

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            return width > 50 && height > 20;
        }

        private int GetControlTop(IntPtr hWnd)
        {
            RECT rect = new RECT();
            if (GetWindowRect(hWnd, ref rect))
                return rect.Top;
            return 0;
        }

        private bool IsPotentialEditControl(string className)
        {
            string[] editKeywords = { "Edit", "Text", "Input", "Line", "Field", "Box" };
            foreach (string keyword in editKeywords)
            {
                if (className.Contains(keyword))
                    return true;
            }
            return false;
        }

        private bool IsPasswordControl(IntPtr controlHandle)
        {
            uint style = GetWindowLongPtr(controlHandle, GWL_STYLE);
            if ((style & ES_PASSWORD) != 0)
                return true;

            StringBuilder className = new StringBuilder(256);
            GetClassName(controlHandle, className, className.Capacity);
            string classStr = className.ToString();

            if (classStr.Contains("Pwd") || classStr.Contains("pwd") || classStr.Contains("Password"))
                return true;

            StringBuilder text = new StringBuilder(256);
            SendMessage(controlHandle, WM_GETTEXT, (IntPtr)256, text);
            string editText = text.ToString();

            if (editText.Length > 0 && IsPasswordMasked(editText))
                return true;

            return false;
        }

        private bool IsPasswordMasked(string text)
        {
            foreach (char c in text)
            {
                if (c != '*' && c != '\u25CF' && c != '\u25A0')
                    return false;
            }
            return text.Length > 0;
        }

        public IntPtr FindConfirmPasswordEdit(IntPtr dialogHandle, IntPtr firstEdit)
        {
            if (dialogHandle == IntPtr.Zero || firstEdit == IntPtr.Zero)
                return IntPtr.Zero;

            Logger.Debug($"开始定位确认密码输入框，对话框句柄: {dialogHandle}, 第一个输入框: {firstEdit}");

            List<IntPtr> allEdits = new List<IntPtr>();
            CollectEditControls(dialogHandle, allEdits);

            Logger.Debug($"找到 {allEdits.Count} 个编辑控件");

            int firstIndex = allEdits.IndexOf(firstEdit);
            if (firstIndex >= 0 && firstIndex + 1 < allEdits.Count)
            {
                IntPtr confirmEdit = allEdits[firstIndex + 1];
                Logger.Info($"找到确认密码输入框: {confirmEdit}");
                return confirmEdit;
            }

            if (allEdits.Count >= 2)
            {
                IntPtr confirmEdit = allEdits[1];
                Logger.Info($"返回第二个编辑控件作为确认密码输入框: {confirmEdit}");
                return confirmEdit;
            }

            Logger.Warning("未找到确认密码输入框");
            return IntPtr.Zero;
        }

        public IntPtr FindConfirmButton(IntPtr dialogHandle)
        {
            if (dialogHandle == IntPtr.Zero)
                return IntPtr.Zero;

            Logger.Debug($"开始定位确认按钮，对话框句柄: {dialogHandle}");

            string[] buttonTexts = { "确定", "OK", "应用", "打开", "保存", "下一步" };

            foreach (string text in buttonTexts)
            {
                IntPtr button = FindButtonByText(dialogHandle, text);
                if (button != IntPtr.Zero)
                {
                    Logger.Info($"找到按钮 '{text}': {button}");
                    return button;
                }
            }

            IntPtr anyButton = FindAnyButton(dialogHandle);
            if (anyButton != IntPtr.Zero)
            {
                Logger.Info($"找到任意按钮: {anyButton}");
                return anyButton;
            }

            Logger.Warning("未找到确认按钮");
            return IntPtr.Zero;
        }

        private void CollectAllControls(IntPtr parentHandle, List<IntPtr> controls)
        {
            EnumChildWindows(parentHandle, (hwnd, lParam) =>
            {
                if (IsWindowVisible(hwnd))
                {
                    StringBuilder className = new StringBuilder(256);
                    GetClassName(hwnd, className, className.Capacity);
                    string classStr = className.ToString();

                    Logger.Debug($"收集到控件: 句柄={hwnd}, 类名={classStr}");
                    controls.Add(hwnd);
                }

                CollectAllControls(hwnd, controls);
                return true;
            }, IntPtr.Zero);
        }

        private void CollectEditControls(IntPtr parentHandle, List<IntPtr> editControls)
        {
            EnumChildWindows(parentHandle, (hwnd, lParam) =>
            {
                StringBuilder className = new StringBuilder(256);
                GetClassName(hwnd, className, className.Capacity);
                string classStr = className.ToString();

                if (IsEditControl(classStr))
                {
                    editControls.Add(hwnd);
                    Logger.Debug($"收集到编辑控件: 句柄={hwnd}, 类名={classStr}");
                }

                CollectEditControls(hwnd, editControls);
                return true;
            }, IntPtr.Zero);
        }

        private bool IsEditControl(string className)
        {
            foreach (string editClass in _passwordEditClasses)
            {
                if (className.Contains(editClass))
                    return true;
            }
            return false;
        }

        private IntPtr FindButtonByText(IntPtr parentHandle, string buttonText)
        {
            IntPtr foundHandle = IntPtr.Zero;

            EnumChildWindows(parentHandle, (hwnd, lParam) =>
            {
                StringBuilder className = new StringBuilder(256);
                GetClassName(hwnd, className, className.Capacity);
                string classStr = className.ToString();

                if (!IsButtonControl(classStr))
                {
                    IntPtr childFound = FindButtonByText(hwnd, buttonText);
                    if (childFound != IntPtr.Zero)
                    {
                        foundHandle = childFound;
                        return false;
                    }
                    return true;
                }

                StringBuilder windowText = new StringBuilder(256);
                GetWindowText(hwnd, windowText, windowText.Capacity);
                string text = windowText.ToString();

                if (text == buttonText)
                {
                    foundHandle = hwnd;
                    return false;
                }

                IntPtr childFound2 = FindButtonByText(hwnd, buttonText);
                if (childFound2 != IntPtr.Zero)
                {
                    foundHandle = childFound2;
                    return false;
                }

                return true;
            }, IntPtr.Zero);

            return foundHandle;
        }

        private IntPtr FindAnyButton(IntPtr parentHandle)
        {
            IntPtr foundHandle = IntPtr.Zero;

            EnumChildWindows(parentHandle, (hwnd, lParam) =>
            {
                StringBuilder className = new StringBuilder(256);
                GetClassName(hwnd, className, className.Capacity);
                string classStr = className.ToString();

                if (IsButtonControl(classStr))
                {
                    StringBuilder windowText = new StringBuilder(256);
                    GetWindowText(hwnd, windowText, windowText.Capacity);
                    string text = windowText.ToString();

                    if (!string.IsNullOrEmpty(text))
                    {
                        foundHandle = hwnd;
                        return false;
                    }
                }

                IntPtr childFound = FindAnyButton(hwnd);
                if (childFound != IntPtr.Zero)
                {
                    foundHandle = childFound;
                    return false;
                }

                return true;
            }, IntPtr.Zero);

            return foundHandle;
        }

        private bool IsButtonControl(string className)
        {
            foreach (string buttonClass in _buttonClasses)
            {
                if (className.Contains(buttonClass))
                    return true;
            }
            return false;
        }

        public string GetControlText(IntPtr controlHandle)
        {
            if (controlHandle == IntPtr.Zero)
                return string.Empty;

            StringBuilder sb = new StringBuilder(256);
            SendMessage(controlHandle, WM_GETTEXT, (IntPtr)256, sb);
            return sb.ToString();
        }

        public void LogControlTree(IntPtr parentHandle, int level = 0)
        {
            string indent = new string(' ', level * 2);

            EnumChildWindows(parentHandle, (hwnd, lParam) =>
            {
                StringBuilder className = new StringBuilder(256);
                GetClassName(hwnd, className, className.Capacity);
                string classStr = className.ToString();

                StringBuilder windowText = new StringBuilder(256);
                GetWindowText(hwnd, windowText, windowText.Capacity);
                string text = windowText.ToString();

                Logger.Debug($"{indent}控件: 句柄={hwnd}, 类名={classStr}, 文本={text}");

                LogControlTree(hwnd, level + 1);
                return true;
            }, IntPtr.Zero);
        }
    }
}