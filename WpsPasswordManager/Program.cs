using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Text;
using System.Diagnostics;
using WpsPasswordManager.Monitor;
using WpsPasswordManager.Business;
using WpsPasswordManager.Simulator;
using WpsPasswordManager.UI;
using WpsPasswordManager.Utils;

#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8605, CS8618, CS8625

namespace WpsPasswordManager
{
    internal static class Program
    {
        // Win32 API 定义
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        // 枚举子窗口的委托
        private delegate bool EnumChildWindowsProc(IntPtr hwnd, IntPtr lParam);

        // Win32 API 定义：模拟键盘事件
        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        // Win32 API 定义：查找子窗口
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        // Win32 API 定义：设置窗口文本
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetWindowText(IntPtr hWnd, string lpString);

        // Win32 API 定义：获取窗口文本
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        // 鼠标钩子相关的Win32 API
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT pt);



        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

        // 钩子回调函数
        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        // 鼠标钩子变量
        private static HookProc _mouseHookProc;
        private static IntPtr _mouseHook;

        // 常量定义
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint WM_CANCELMODE = 0x001F;

        // 结构体定义
        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public int mouseData;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Logger.Info("WPS 密码自动填充插件启动");

            // 暂时注释掉鼠标钩子安装
            // InstallMouseHook();
            // Logger.Info("鼠标钩子已安装");

            // 初始化各个模块
            Logger.Info("初始化各个模块");
            WpsMonitor monitor = new WpsMonitor();
            PasswordGenerator passwordGenerator = new PasswordGenerator();
            MetadataManager metadataManager = new MetadataManager();
            InputSimulator simulator = new InputSimulator();
            TrayIcon trayIcon = new TrayIcon();
            FloatingButton floatingButton = new FloatingButton(monitor);
            NotificationForm notificationForm = new NotificationForm();

            // 初始化系统托盘
            Logger.Info("初始化系统托盘");
            trayIcon.Initialize();
            trayIcon.ExitClicked += (sender, e) =>
            {
                Logger.Info("用户点击退出");
                Application.Exit();
            };
            trayIcon.OpenFolderClicked += (sender, e) =>
            {
                Logger.Info("用户点击打开文件夹");
                System.Diagnostics.Process.Start(Environment.CurrentDirectory);
            };

            // 悬浮按钮事件
            floatingButton.GeneratePasswordClicked += (sender, e) =>
            {
                Logger.Info("用户点击生成密码按钮");
                
                // 查找密码对话框
                IntPtr dialogHandle = monitor.FindPasswordDialog();
                Logger.Debug($"找到密码对话框: {dialogHandle}");
                
                if (dialogHandle != IntPtr.Zero)
                {
                    // 生成密码
                    string password = passwordGenerator.GeneratePassword();
                    Logger.Debug($"生成密码: {password}");
                    
                    // 先将密码对话框设置为活动窗口
                    SetForegroundWindow(dialogHandle);
                    System.Threading.Thread.Sleep(100);
                    
                    // 尝试找到密码输入框
                    IntPtr passwordEdit = monitor.FindPasswordEdit(dialogHandle);
                    IntPtr confirmPasswordEdit = monitor.FindConfirmPasswordEdit(dialogHandle, passwordEdit);
                    
                    Logger.Debug($"密码输入框句柄: {passwordEdit}, 确认密码输入框句柄: {confirmPasswordEdit}");
                    
                    // 如果找到输入框，直接填充
                    if (passwordEdit != IntPtr.Zero)
                    {
                        Logger.Debug($"找到密码输入框: {passwordEdit}");
                        // 先清空输入框
                        simulator.ClearInput(passwordEdit);
                        Logger.Debug("已清空密码输入框");
                        // 填充密码
                        simulator.SimulatePasswordInput(passwordEdit, password);
                        Logger.Info("密码已填充到「打开文件密码(O)」输入框");
                        
                        // 填充确认密码输入框
                        if (confirmPasswordEdit != IntPtr.Zero)
                        {
                            Logger.Debug($"找到确认密码输入框: {confirmPasswordEdit}");
                            // 先清空输入框
                            simulator.ClearInput(confirmPasswordEdit);
                            Logger.Debug("已清空确认密码输入框");
                            // 填充密码
                            simulator.SimulatePasswordInput(confirmPasswordEdit, password);
                            Logger.Info("密码已填充到「再次输入密码(P)」输入框");
                        }
                        else
                        {
                            Logger.Warning("未找到确认密码输入框");
                        }
                    }
                    else
                    {
                        Logger.Warning("未找到密码输入框，尝试模拟点击和输入");
                        // 如果未找到输入框，尝试模拟点击和输入
                        simulator.SimulatePasswordInputWithTab(dialogHandle, password);
                        Logger.Info("通过模拟操作填充密码");
                    }
                }
                else
                {
                    Logger.Warning("未找到密码对话框");
                }
            };

            // 获取主线程的同步上下文
            var mainThreadSyncContext = SynchronizationContext.Current;
            
            // 启动监控线程
            Thread monitorThread = new Thread(() =>
            {
                // 记录上一次检测到的密码加密窗口句柄
                IntPtr lastPasswordEncryptDialog = IntPtr.Zero;
                // 记录上一次检测到的密码加密窗口的密码
                string lastPassword = string.Empty;
                // 记录上一次检测到的密码加密窗口的标题
                string lastDialogTitle = string.Empty;
                // 记录上一次显示悬浮按钮的对话框句柄
                IntPtr lastShownDialog = IntPtr.Zero;
                
                while (true)
                {
                    try
                    {
                        long startTime = DateTime.Now.Ticks;
                        Logger.Debug($"循环开始，时间戳: {startTime}");
                        
                        // 检查WPS是否运行
                        long checkWpsStart = DateTime.Now.Ticks;
                        if (monitor.IsWpsRunning())
                        {
                            long checkWpsEnd = DateTime.Now.Ticks;
                            Logger.Debug($"检查WPS运行状态耗时: {(checkWpsEnd - checkWpsStart) / 10000}ms");
                            
                            Logger.Debug("WPS 正在运行");
                            // 检查加密对话框
                            long findDialogStart = DateTime.Now.Ticks;
                            IntPtr encryptDialog = monitor.FindPasswordDialog();
                            long findDialogEnd = DateTime.Now.Ticks;
                            Logger.Debug($"查找密码对话框耗时: {(findDialogEnd - findDialogStart) / 10000}ms");
                            if (encryptDialog != IntPtr.Zero)
                            {
                                // 获取对话框标题
                                StringBuilder dialogTitle = new StringBuilder(256);
                                GetWindowText(encryptDialog, dialogTitle, dialogTitle.Capacity);
                                string title = dialogTitle.ToString();
                                
                                Logger.Debug($"找到对话框: {encryptDialog}, 标题: {title}");
                                
                                // 只有在加密窗口中显示悬浮按钮，解密窗口不显示
                if (title == "文档加密" || title == "密码加密")
                {
                    Logger.Debug($"找到加密对话框: {encryptDialog}, 标题: {title}");
                    // 每次循环都更新按钮位置，确保按钮能随着窗口拖动而移动
                    // 使用同步上下文在主线程中执行UI操作
                    mainThreadSyncContext?.Post((state) =>
                    {
                        floatingButton.ShowAtDialog(encryptDialog);
                    }, null);
                                    
                                    // 如果是密码加密窗口，记录密码
                                        if (title == "密码加密")
                                        {
                                            Logger.Info("找到密码加密窗口，开始处理");
                                            
                                            // 记录窗口信息，无论是否找到密码输入框
                                            lastPasswordEncryptDialog = encryptDialog;
                                            lastDialogTitle = title;
                                            Logger.Info($"记录密码加密窗口: {encryptDialog}");
                                            
                                            // 尝试获取密码提示输入框内容
                                            long getHintStart = DateTime.Now.Ticks;
                                            string passwordHint = GetPasswordHintFromDialog(encryptDialog);
                                            long getHintEnd = DateTime.Now.Ticks;
                                            Logger.Debug($"获取密码提示耗时: {(getHintEnd - getHintStart) / 10000}ms");
                                            
                                            if (!string.IsNullOrEmpty(passwordHint))
                                            {
                                                Logger.Info($"获取到密码提示: {passwordHint}");
                                            }
                                            else
                                            {
                                                Logger.Warning("未获取到密码提示");
                                            }
                                            
                                            // 查找第一个密码输入框
                                            long findEditStart = DateTime.Now.Ticks;
                                            IntPtr passwordEdit = monitor.FindPasswordEdit(encryptDialog);
                                            long findEditEnd = DateTime.Now.Ticks;
                                            Logger.Debug($"查找密码输入框耗时: {(findEditEnd - findEditStart) / 10000}ms");
                                            if (passwordEdit != IntPtr.Zero)
                                            {
                                                // 获取输入框文本
                                                long getTextStart = DateTime.Now.Ticks;
                                                string password = monitor.GetInputText(passwordEdit);
                                                long getTextEnd = DateTime.Now.Ticks;
                                                Logger.Debug($"获取输入框文本耗时: {(getTextEnd - getTextStart) / 10000}ms");
                                                
                                                if (!string.IsNullOrEmpty(password))
                                                {
                                                    lastPassword = password;
                                                    Logger.Info($"记录密码: {password}");
                                                    Logger.Info($"获取到【打开文件密码(O)】输入框内容: {password}");
                                                }
                                                else
                                                {
                                                    Logger.Warning("密码输入框为空");
                                                    // 尝试使用UI Automation获取密码
                                                    long getUiaPasswordStart = DateTime.Now.Ticks;
                                                    string uiaPassword = GetPasswordFromDialog(encryptDialog);
                                                    long getUiaPasswordEnd = DateTime.Now.Ticks;
                                                    Logger.Debug($"通过UI Automation获取密码耗时: {(getUiaPasswordEnd - getUiaPasswordStart) / 10000}ms");
                                                    
                                                    if (!string.IsNullOrEmpty(uiaPassword))
                                                    {
                                                        lastPassword = uiaPassword;
                                                        Logger.Info($"通过UI Automation获取到【打开文件密码(O)】输入框内容: {uiaPassword}");
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                Logger.Warning("未找到密码输入框，尝试使用UI Automation获取");
                                                // 尝试使用UI Automation获取密码
                                                long getUiaPasswordStart = DateTime.Now.Ticks;
                                                string uiaPassword = GetPasswordFromDialog(encryptDialog);
                                                long getUiaPasswordEnd = DateTime.Now.Ticks;
                                                Logger.Debug($"通过UI Automation获取密码耗时: {(getUiaPasswordEnd - getUiaPasswordStart) / 10000}ms");
                                                
                                                if (!string.IsNullOrEmpty(uiaPassword))
                                                {
                                                    lastPassword = uiaPassword;
                                                    Logger.Info($"通过UI Automation获取到【打开文件密码(O)】输入框内容: {uiaPassword}");
                                                }
                                            }
                                            
                                            // 无论是否找到输入框，都尝试查找应用按钮
                                            long findButtonStart = DateTime.Now.Ticks;
                                            IntPtr applyButton = monitor.FindApplyButton(encryptDialog);
                                            long findButtonEnd = DateTime.Now.Ticks;
                                            Logger.Debug($"查找应用按钮耗时: {(findButtonEnd - findButtonStart) / 10000}ms");
                                            
                                            if (applyButton != IntPtr.Zero)
                                            {
                                                Logger.Info("找到应用按钮");
                                                
                                                // 检查应用按钮是否被点击
                                                long checkButtonStart = DateTime.Now.Ticks;
                                                bool isButtonClicked = monitor.IsButtonClicked(applyButton);
                                                long checkButtonEnd = DateTime.Now.Ticks;
                                                Logger.Debug($"检查应用按钮状态耗时: {(checkButtonEnd - checkButtonStart) / 10000}ms");
                                                if (isButtonClicked)
                                                {
                                                    Logger.Info("检测到应用按钮点击");
                                                    
                                                    // 显示弹框提示
                                                    try
                                                    {
                                                        System.Threading.Thread notificationThread = new System.Threading.Thread(() =>
                                                        {
                                                            try
                                                            {
                                                                NotificationForm tempNotificationForm = new NotificationForm();
                                                                tempNotificationForm.ShowNotification("检测到应用按钮点击，正在处理...");
                                                                Application.Run();
                                                            }
                                                            catch (Exception ex)
                                                            {
                                                                Logger.Error($"显示弹框时出错: {ex.Message}");
                                                            }
                                                        });
                                                        notificationThread.SetApartmentState(System.Threading.ApartmentState.STA);
                                                        notificationThread.Start();
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        Logger.Error($"显示弹框时出错: {ex.Message}");
                                                    }
                                                    
                                                    // 尝试获取文档路径并写入密码
                                                    try
                                                    {
                                                        // 尝试获取文档路径
                                                        long getPathStart = DateTime.Now.Ticks;
                                                        string documentPath = monitor.GetDocumentPath(IntPtr.Zero);
                                                        long getPathEnd = DateTime.Now.Ticks;
                                                        Logger.Debug($"获取文档路径耗时: {(getPathEnd - getPathStart) / 10000}ms");
                                                        
                                                        if (!string.IsNullOrEmpty(documentPath))
                                                        {
                                                            Logger.Info($"获取到文档路径: {documentPath}");
                                                            
                                                            // 检查文件是否存在
                                                            if (System.IO.File.Exists(documentPath))
                                                            {
                                                                Logger.Info($"文件存在: {documentPath}");
                                                                
                                                                // 检查是否有密码
                                                                if (!string.IsNullOrEmpty(lastPassword))
                                                                {
                                                                    // 写入密码到文档元数据
                                                                    long writePasswordStart = DateTime.Now.Ticks;
                                                                    bool success = metadataManager.WritePasswordToMetadata(documentPath, lastPassword);
                                                                    long writePasswordEnd = DateTime.Now.Ticks;
                                                                    Logger.Debug($"写入密码到文档元数据耗时: {(writePasswordEnd - writePasswordStart) / 10000}ms");
                                                                    if (success)
                                                                    {
                                                                        Logger.Info($"密码已成功写入文档元数据: {documentPath}");
                                                                        // 显示成功提示
                                                                        System.Threading.Thread notificationThread = new System.Threading.Thread(() =>
                                                                        {
                                                                            NotificationForm tempNotificationForm = new NotificationForm();
                                                                            tempNotificationForm.ShowNotification("密码已成功写入文档元数据");
                                                                            Application.Run();
                                                                        });
                                                                        notificationThread.SetApartmentState(System.Threading.ApartmentState.STA);
                                                                        notificationThread.Start();
                                                                    }
                                                                    else
                                                                    {
                                                                        Logger.Error("写入密码到文档元数据失败");
                                                                        // 显示失败提示
                                                                        System.Threading.Thread notificationThread = new System.Threading.Thread(() =>
                                                                        {
                                                                            NotificationForm tempNotificationForm = new NotificationForm();
                                                                            tempNotificationForm.ShowNotification("写入密码到文档元数据失败");
                                                                            Application.Run();
                                                                        });
                                                                        notificationThread.SetApartmentState(System.Threading.ApartmentState.STA);
                                                                        notificationThread.Start();
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    Logger.Warning("未获取到密码");
                                                                    // 显示提示
                                                                    System.Threading.Thread notificationThread = new System.Threading.Thread(() =>
                                                                    {
                                                                        NotificationForm tempNotificationForm = new NotificationForm();
                                                                        tempNotificationForm.ShowNotification("未获取到密码，但检测到应用按钮点击");
                                                                        Application.Run();
                                                                    });
                                                                    notificationThread.SetApartmentState(System.Threading.ApartmentState.STA);
                                                                    notificationThread.Start();
                                                                }
                                                            }
                                                            else
                                                            {
                                                                Logger.Error($"文件不存在: {documentPath}");
                                                                // 显示失败提示
                                                                System.Threading.Thread notificationThread = new System.Threading.Thread(() =>
                                                                {
                                                                    NotificationForm tempNotificationForm = new NotificationForm();
                                                                    tempNotificationForm.ShowNotification("文件不存在，无法写入密码");
                                                                    Application.Run();
                                                                });
                                                                notificationThread.SetApartmentState(System.Threading.ApartmentState.STA);
                                                                notificationThread.Start();
                                                            }
                                                        }
                                                        else
                                                        {
                                                            Logger.Warning("无法获取文档路径");
                                                            // 显示失败提示
                                                            System.Threading.Thread notificationThread = new System.Threading.Thread(() =>
                                                            {
                                                                NotificationForm tempNotificationForm = new NotificationForm();
                                                                tempNotificationForm.ShowNotification("无法获取文档路径");
                                                                Application.Run();
                                                            });
                                                            notificationThread.SetApartmentState(System.Threading.ApartmentState.STA);
                                                            notificationThread.Start();
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        Logger.Error($"处理应用按钮点击时出错: {ex.Message}");
                                                        // 即使出错也要显示提示
                                                        try
                                                        {
                                                            System.Threading.Thread notificationThread = new System.Threading.Thread(() =>
                                                            {
                                                                NotificationForm tempNotificationForm = new NotificationForm();
                                                                tempNotificationForm.ShowNotification("检测到应用按钮点击，但处理过程中出错");
                                                                Application.Run();
                                                            });
                                                            notificationThread.SetApartmentState(System.Threading.ApartmentState.STA);
                                                            notificationThread.Start();
                                                        }
                                                        catch (Exception ex2)
                                                        {
                                                            Logger.Error($"显示错误提示时出错: {ex2.Message}");
                                                        }
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                Logger.Warning("未找到应用按钮");
                                            }
                                        }
                                }
                                else
                                {
                                    Logger.Debug($"找到解密对话框: {encryptDialog}, 标题: {title}，不显示悬浮按钮");
                                    // 隐藏悬浮按钮
                                    mainThreadSyncContext?.Post((state) =>
                                    {
                                        floatingButton.HideButton();
                                    }, null);
                                }
                            }
                            else
                            {
                                // 隐藏悬浮按钮
                                mainThreadSyncContext?.Post((state) =>
                                {
                                    floatingButton.HideButton();
                                }, null);
                            Logger.Debug("未找到加密对话框，隐藏悬浮按钮");
                            // 重置显示状态
                            lastShownDialog = IntPtr.Zero;
                            
                            // 检查是否密码加密窗口刚关闭
                                if (lastPasswordEncryptDialog != IntPtr.Zero && lastDialogTitle == "密码加密")
                                {
                                    Logger.Info("密码加密窗口已关闭");
                                    
                                    // 重置记录
                                    lastPasswordEncryptDialog = IntPtr.Zero;
                                    lastPassword = string.Empty;
                                    lastDialogTitle = string.Empty;
                                    Logger.Info("重置窗口记录");
                                }
                            }
                            
                            // 检查解密对话框
                            IntPtr decryptDialog = monitor.FindPasswordDialog();
                            if (decryptDialog != IntPtr.Zero)
                            {
                                // 获取对话框标题
                                StringBuilder dialogTitle = new StringBuilder(256);
                                GetWindowText(decryptDialog, dialogTitle, dialogTitle.Capacity);
                                string title = dialogTitle.ToString();
                                
                                // 只有在解密窗口中自动填充密码
                                if (title == "文档已加密")
                                {
                                    Logger.Info($"找到解密对话框: {decryptDialog}, 标题: {title}");
                                    
                                    // 尝试获取文档路径
                                    string documentPath = monitor.GetDocumentPath(decryptDialog);
                                    if (!string.IsNullOrEmpty(documentPath))
                                    {
                                        Logger.Info($"获取到文档路径: {documentPath}");
                                        // 从文档元数据中读取密码
                                        string password = metadataManager.ReadPasswordFromMetadata(documentPath);
                                        if (!string.IsNullOrEmpty(password))
                                        {
                                            Logger.Info($"从文档元数据中读取到密码: {password}");
                                        }
                                        else
                                        {
                                            // 如果从元数据中读取失败，使用默认密码
                                            password = "z0rfi7llkdc";
                                            Logger.Info($"从文档元数据中读取密码失败，使用默认密码: {password}");
                                        }
                                        
                                        // 确保对话框在前台
                                        SetForegroundWindow(decryptDialog);
                                        Thread.Sleep(200);
                                        
                                        // 直接模拟键盘输入密码
                                        foreach (char c in password)
                                        {
                                            keybd_event((byte)char.ToUpper(c), 0, 0, UIntPtr.Zero);
                                            keybd_event((byte)char.ToUpper(c), 0, 2, UIntPtr.Zero);
                                            Thread.Sleep(20);
                                        }
                                        Logger.Info("密码已填充到解密输入框");
                                        
                                        // 等待一小段时间
                                        Thread.Sleep(200);
                                        
                                        // 模拟按 Enter 键确认
                                        keybd_event(0x0D, 0, 0, UIntPtr.Zero); // Enter键
                                        keybd_event(0x0D, 0, 2, UIntPtr.Zero);
                                        Logger.Info("已按下 Enter 键确认");
                                        
                                        // 等待对话框关闭
                                        Thread.Sleep(500);
                                    }
                                    else
                                    {
                                        Logger.Warning("无法获取文档路径，无法自动填充密码");
                                    }
                                }
                            }
                            else
                            {
                                Logger.Debug("未找到解密对话框");
                            }
                        }
                        else
                        {
                            // 隐藏悬浮按钮
                            floatingButton.HideButton();
                            Logger.Debug("WPS 未运行，隐藏悬浮按钮");
                            
                            // 重置记录
                            lastPasswordEncryptDialog = IntPtr.Zero;
                            lastPassword = string.Empty;
                            lastDialogTitle = string.Empty;
                        }
                        
                        long endTime = DateTime.Now.Ticks;
                        Logger.Debug($"循环结束，总耗时: {(endTime - startTime) / 10000}ms");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"监控线程错误: {ex.Message}");
                    }

                    Thread.Sleep(500); // 500毫秒扫描一次，提高响应速度
                }
            });

            monitorThread.IsBackground = true;
            monitorThread.Start();

            // 显示启动提示
            Logger.Info("插件已启动，正在监控WPS进程");
            trayIcon.ShowBalloonTip("WPS 密码自动填充插件", "插件已启动，正在监控WPS进程...");

            // 运行应用程序
            Application.Run();
        }

        // 暂时注释掉鼠标钩子安装方法
        /*
        private static void InstallMouseHook()
        {
            _mouseHookProc = MouseHookCallback;
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseHookProc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }
        */

        // 鼠标钩子回调函数
        private static IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            // 暂时注释掉鼠标左键按下的验证和触发逻辑
            /*
            if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN) // 鼠标左键按下
            {
                MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                
                // 记录鼠标点击位置
                Logger.Debug($"鼠标左键按下，位置: ({hookStruct.pt.X}, {hookStruct.pt.Y})");
                
                // 获取鼠标点击位置的窗口
                IntPtr hWnd = WindowFromPoint(hookStruct.pt);
                Logger.Debug($"点击位置的窗口句柄: {hWnd}");
                
                // 检查窗口是否有效
                if (hWnd != IntPtr.Zero)
                {
                    // 获取窗口标题
                    StringBuilder title = new StringBuilder(256);
                    GetWindowText(hWnd, title, title.Capacity);
                    string titleStr = title.ToString();
                    Logger.Debug($"窗口标题: {titleStr}");
                    
                    // 检查是否是WPS的密码加密窗口
                    if (IsWpsPasswordDialog(hWnd))
                    {
                        Logger.Debug("找到WPS密码加密窗口");
                        
                        // 尝试获取密码提示输入框内容
                        string passwordHint = GetPasswordHintFromDialog(hWnd);
                        if (!string.IsNullOrEmpty(passwordHint))
                        {
                            Logger.Info($"获取到密码提示: {passwordHint}");
                        }
                        
                        // 检查是否点击了应用按钮
                        if (IsApplyButton(hWnd, hookStruct.pt))
                        {
                            // 打印信息
                            Console.WriteLine("我是应用按钮");
                            Logger.Info("检测到应用按钮点击");
                            Logger.Info("拦截到了鼠标请求");
                            
                            // 1. 暂停窗口关闭
                            SendMessage(hWnd, WM_CANCELMODE, IntPtr.Zero, IntPtr.Zero);
                            Logger.Debug("发送WM_CANCELMODE消息");
                            
                            // 2. 在后台线程中处理密码获取和点击模拟
                            POINT mousePoint = hookStruct.pt;
                            IntPtr dialogHandle = hWnd;
                            ThreadPool.QueueUserWorkItem((state) =>
                            {
                                try
                                {
                                    // 尝试获取输入框内容
                                    string password = GetPasswordFromDialog(dialogHandle);
                                    Logger.Info($"获取到密码: {password}");
                                    
                                    // 尝试获取密码提示输入框内容
                                    string hint = GetPasswordHintFromDialog(dialogHandle);
                                    if (!string.IsNullOrEmpty(hint))
                                    {
                                        Logger.Info($"获取到密码提示: {hint}");
                                    }
                                    
                                    // 3. 放行点击动作（模拟一次点击）
                                    Thread.Sleep(100); // 短暂延迟，确保窗口状态稳定
                                    SimulateMouseClick(mousePoint);
                                    Logger.Debug("模拟鼠标点击");
                                    
                                    // 4. 处理密码
                                    // ProcessPassword(password);
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error($"后台处理密码时出错: {ex.Message}");
                                }
                            });
                            
                            // 5. 返回1，表示已经处理了这个消息，不再传递给其他钩子
                            return (IntPtr)1;
                        }
                        else
                        {
                            Logger.Debug("未点击应用按钮");
                        }
                    }
                    else
                    {
                        Logger.Debug("不是WPS密码加密窗口");
                    }
                }
                else
                {
                    Logger.Debug("未找到窗口");
                }
            }
            */
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }
        
        // 从密码对话框获取密码提示
        private static string GetPasswordHintFromDialog(IntPtr dialogHandle)
        {
            try
            {
                Logger.Debug("尝试从密码对话框获取密码提示");
                
                // 尝试使用UI Automation获取密码提示
                string passwordHint = GetPasswordHintUsingUIAutomation(dialogHandle);
                if (!string.IsNullOrEmpty(passwordHint))
                {
                    Logger.Info($"通过UI Automation获取到密码提示: {passwordHint}");
                    return passwordHint;
                }
                
                Logger.Warning("UI Automation获取密码提示失败");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Logger.Error($"获取密码提示时出错: {ex.Message}");
                return string.Empty;
            }
        }
        
        // 使用UI Automation获取密码提示
        private static string GetPasswordHintUsingUIAutomation(IntPtr dialogHandle)
        {
            try
            {
                Logger.Debug("开始使用UI Automation获取密码提示");
                
                // 尝试加载UIAutomationClient程序集
                System.Reflection.Assembly uiaClient = null;
                try
                {
                    uiaClient = System.Reflection.Assembly.Load("UIAutomationClient, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
                    Logger.Debug("成功加载UIAutomationClient程序集");
                }
                catch (Exception ex)
                {
                    Logger.Error($"加载UIAutomationClient程序集时出错: {ex.Message}");
                    
                    // 尝试加载UIAutomationClient的不同版本
                    try
                    {
                        uiaClient = System.Reflection.Assembly.Load("UIAutomationClient");
                        Logger.Debug("成功加载UIAutomationClient程序集（无版本）");
                    }
                    catch (Exception ex2)
                    {
                        Logger.Error($"加载UIAutomationClient程序集（无版本）时出错: {ex2.Message}");
                        return string.Empty;
                    }
                }
                
                if (uiaClient == null)
                {
                    Logger.Warning("无法加载UIAutomationClient程序集");
                    return string.Empty;
                }
                
                // 尝试加载UIAutomationTypes程序集（包含TreeScope等枚举）
                System.Reflection.Assembly uiaTypes = null;
                try
                {
                    uiaTypes = System.Reflection.Assembly.Load("UIAutomationTypes, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
                    Logger.Debug("成功加载UIAutomationTypes程序集");
                }
                catch (Exception ex)
                {
                    Logger.Error($"加载UIAutomationTypes程序集时出错: {ex.Message}");
                    
                    // 尝试加载UIAutomationTypes的不同版本
                    try
                    {
                        uiaTypes = System.Reflection.Assembly.Load("UIAutomationTypes");
                        Logger.Debug("成功加载UIAutomationTypes程序集（无版本）");
                    }
                    catch (Exception ex2)
                    {
                        Logger.Error($"加载UIAutomationTypes程序集（无版本）时出错: {ex2.Message}");
                        return string.Empty;
                    }
                }
                
                if (uiaTypes == null)
                {
                    Logger.Warning("无法加载UIAutomationTypes程序集");
                    return string.Empty;
                }
                
                // 尝试获取AutomationElement类
                Type automationElementType = null;
                try
                {
                    automationElementType = uiaClient.GetType("System.Windows.Automation.AutomationElement");
                    if (automationElementType != null)
                    {
                        Logger.Debug("成功获取AutomationElement类型");
                    }
                    else
                    {
                        Logger.Warning("无法获取AutomationElement类型");
                        
                        // 尝试获取所有类型，看看有哪些可用
                        Type[] types = uiaClient.GetTypes();
                        Logger.Debug($"UIAutomationClient程序集包含 {types.Length} 个类型");
                        foreach (Type type in types)
                        {
                            if (type.FullName.Contains("Automation"))
                            {
                                Logger.Debug($"找到Automation相关类型: {type.FullName}");
                            }
                        }
                        
                        return string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"获取AutomationElement类型时出错: {ex.Message}");
                    return string.Empty;
                }
                
                // 调用FromHandle方法获取对话框元素
                object dialogElement = automationElementType.GetMethod("FromHandle").Invoke(null, new object[] { dialogHandle });
                if (dialogElement == null)
                {
                    Logger.Warning("无法获取对话框的AutomationElement");
                    return string.Empty;
                }
                
                // 获取TreeScope枚举（从UIAutomationTypes程序集）
                Type treeScopeType = uiaTypes.GetType("System.Windows.Automation.TreeScope");
                if (treeScopeType == null)
                {
                    Logger.Warning("无法获取TreeScope类型");
                    return string.Empty;
                }
                object treeScopeDescendants = Enum.Parse(treeScopeType, "Descendants");
                
                // 获取PropertyCondition类（先尝试从UIAutomationClient程序集获取）
                Type propertyConditionType = uiaClient.GetType("System.Windows.Automation.PropertyCondition");
                if (propertyConditionType == null)
                {
                    // 如果UIAutomationClient中没有，再尝试从UIAutomationTypes程序集获取
                    propertyConditionType = uiaTypes.GetType("System.Windows.Automation.PropertyCondition");
                    if (propertyConditionType == null)
                    {
                        Logger.Warning("无法获取PropertyCondition类型");
                        return string.Empty;
                    }
                }
                
                // 获取AutomationElement.NameProperty
                object nameProperty = null;
                System.Reflection.PropertyInfo namePropertyInfo = automationElementType.GetProperty("NameProperty");
                if (namePropertyInfo != null)
                {
                    nameProperty = namePropertyInfo.GetValue(null);
                }
                else
                {
                    // 如果属性获取失败，尝试获取字段
                    System.Reflection.FieldInfo namePropertyField = automationElementType.GetField("NameProperty", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (namePropertyField != null)
                    {
                        nameProperty = namePropertyField.GetValue(null);
                    }
                    else
                    {
                        Logger.Warning("无法获取NameProperty");
                        return string.Empty;
                    }
                }
                
                // 首先查找所有编辑控件
                Type controlTypeType = uiaTypes.GetType("System.Windows.Automation.ControlType");
                if (controlTypeType == null)
                {
                    Logger.Warning("无法获取ControlType类型");
                    return string.Empty;
                }
                System.Reflection.FieldInfo editField = controlTypeType.GetField("Edit");
                if (editField == null)
                {
                    Logger.Warning("无法获取Edit字段");
                    return string.Empty;
                }
                object editControlType = editField.GetValue(null);
                
                // 获取AutomationElement.ControlTypeProperty
                object controlTypeProperty = null;
                System.Reflection.PropertyInfo controlTypePropertyInfo = automationElementType.GetProperty("ControlTypeProperty");
                if (controlTypePropertyInfo != null)
                {
                    controlTypeProperty = controlTypePropertyInfo.GetValue(null);
                }
                else
                {
                    // 如果属性获取失败，尝试获取字段
                    System.Reflection.FieldInfo controlTypePropertyField = automationElementType.GetField("ControlTypeProperty", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (controlTypePropertyField != null)
                    {
                        controlTypeProperty = controlTypePropertyField.GetValue(null);
                    }
                    else
                    {
                        Logger.Warning("无法获取ControlTypeProperty");
                        return string.Empty;
                    }
                }
                
                // 创建编辑控件条件
                object editCondition = Activator.CreateInstance(propertyConditionType, new object[] { controlTypeProperty, editControlType });
                if (editCondition == null)
                {
                    Logger.Warning("无法创建编辑控件条件");
                    return string.Empty;
                }
                
                // 查找所有编辑控件
                System.Reflection.MethodInfo findAllMethod = automationElementType.GetMethod("FindAll");
                if (findAllMethod == null)
                {
                    Logger.Warning("无法获取FindAll方法");
                    return string.Empty;
                }
                object editElements = findAllMethod.Invoke(dialogElement, new object[] { treeScopeDescendants, editCondition });
                
                object hintElement = null;
                if (editElements != null)
                {
                    // 获取编辑控件数量
                    System.Reflection.PropertyInfo countProperty = editElements.GetType().GetProperty("Count");
                    if (countProperty != null)
                    {
                        int count = (int)countProperty.GetValue(editElements);
                        Logger.Debug($"找到 {count} 个编辑控件");
                        
                        if (count > 0)
                        {
                            // 获取get_Item方法
                            System.Reflection.MethodInfo getItemMethod = editElements.GetType().GetMethod("get_Item");
                            if (getItemMethod != null)
                            {
                                for (int i = 0; i < count; i++)
                                {
                                    object element = getItemMethod.Invoke(editElements, new object[] { i });
                                    if (element != null)
                                    {
                                        // 获取元素名称
                                        System.Reflection.PropertyInfo currentProperty = element.GetType().GetProperty("Current");
                                        if (currentProperty != null)
                                        {
                                            object current = currentProperty.GetValue(element);
                                            if (current != null)
                                            {
                                                System.Reflection.PropertyInfo namePropertyInfo2 = current.GetType().GetProperty("Name");
                                                if (namePropertyInfo2 != null)
                                                {
                                                    string elementName = (string)namePropertyInfo2.GetValue(current);
                                                    Logger.Debug($"编辑控件 #{i} 名称: {elementName}");
                                                    
                                                    // 检查是否是密码提示输入框
                                                    if (elementName.Contains("密码提示"))
                                                    {
                                                        hintElement = element;
                                                        Logger.Debug("找到密码提示输入框");
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                
                // 如果没有找到，尝试通过类名查找
                if (hintElement == null)
                {
                    System.Reflection.PropertyInfo classNamePropertyInfo2 = automationElementType.GetProperty("ClassNameProperty");
                    if (classNamePropertyInfo2 != null)
                    {
                        object classNameProperty = classNamePropertyInfo2.GetValue(null);
                        object classCondition = Activator.CreateInstance(propertyConditionType, new object[] { classNameProperty, "kd::KDTextField" });
                        if (classCondition != null)
                        {
                            System.Reflection.MethodInfo findFirstMethod = automationElementType.GetMethod("FindFirst");
                            if (findFirstMethod != null)
                            {
                                hintElement = findFirstMethod.Invoke(dialogElement, new object[] { treeScopeDescendants, classCondition });
                                if (hintElement != null)
                                {
                                    Logger.Debug("通过类名找到密码提示输入框");
                                }
                            }
                        }
                    }
                }
                
                if (hintElement != null)
                {
                    Logger.Debug("找到密码提示输入框");
                    
                    // 尝试使用ValuePattern获取内容
                    string hint = TryGetValuePattern(hintElement, uiaClient, 0);
                    if (!string.IsNullOrEmpty(hint))
                    {
                        return hint;
                    }
                    
                    // 尝试使用TextPattern获取内容
                    hint = TryGetTextPattern(hintElement, uiaClient, 0);
                    if (!string.IsNullOrEmpty(hint))
                    {
                        return hint;
                    }
                    
                    // 尝试直接获取Current.Value属性
                    try
                    {
                        System.Reflection.PropertyInfo currentProperty = hintElement.GetType().GetProperty("Current");
                        if (currentProperty != null)
                        {
                            Logger.Debug("找到Current属性");
                            object current = currentProperty.GetValue(hintElement);
                            if (current != null)
                            {
                                Logger.Debug("获取到Current对象");
                                // 打印Current对象的所有属性
                                System.Reflection.PropertyInfo[] properties = current.GetType().GetProperties();
                                Logger.Debug($"Current对象有 {properties.Length} 个属性");
                                foreach (System.Reflection.PropertyInfo prop in properties)
                                {
                                    try
                                    {
                                        object value = prop.GetValue(current);
                                        Logger.Debug($"Current.{prop.Name} = {value}");
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Debug($"获取Current.{prop.Name}时出错: {ex.Message}");
                                    }
                                }
                                
                                System.Reflection.PropertyInfo valueProperty = current.GetType().GetProperty("Value");
                                if (valueProperty != null)
                                {
                                    Logger.Debug("找到Value属性");
                                    string directValue = (string)valueProperty.GetValue(current);
                                    if (!string.IsNullOrEmpty(directValue))
                                    {
                                        Logger.Info($"直接从Current.Value获取到密码提示: {directValue}");
                                        return directValue;
                                    }
                                    else
                                    {
                                        Logger.Debug("Current.Value为空");
                                    }
                                }
                                else
                                {
                                    Logger.Debug("未找到Value属性");
                                }
                            }
                            else
                            {
                                Logger.Debug("Current对象为空");
                            }
                        }
                        else
                        {
                            Logger.Debug("未找到Current属性");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"尝试直接获取Current.Value时出错: {ex.Message}");
                        Logger.Error($"异常堆栈: {ex.StackTrace}");
                    }
                }
                else
                {
                    Logger.Warning("未找到密码提示输入框");
                }
                
                // 尝试通过类名查找
                System.Reflection.PropertyInfo classNamePropertyInfo = automationElementType.GetProperty("ClassNameProperty");
                if (classNamePropertyInfo != null)
                {
                    object classNameProperty = classNamePropertyInfo.GetValue(null);
                    object classCondition = Activator.CreateInstance(propertyConditionType, new object[] { classNameProperty, "kd::KDTextField" });
                    if (classCondition != null)
                    {
                        System.Reflection.MethodInfo findAllMethod2 = automationElementType.GetMethod("FindAll");
                        if (findAllMethod2 != null)
                        {
                            object elements = findAllMethod2.Invoke(dialogElement, new object[] { treeScopeDescendants, classCondition });
                            if (elements != null)
                            {
                                System.Reflection.PropertyInfo countProperty = elements.GetType().GetProperty("Count");
                                if (countProperty != null)
                                {
                                    int count = (int)countProperty.GetValue(elements);
                                    Logger.Debug($"找到 {count} 个kd::KDTextField元素");
                                    
                                    if (count > 0)
                                    {
                                        System.Reflection.MethodInfo getItemMethod = elements.GetType().GetMethod("get_Item");
                                        if (getItemMethod != null)
                                        {
                                            for (int i = 0; i < count; i++)
                                            {
                                                object element = getItemMethod.Invoke(elements, new object[] { i });
                                                if (element != null)
                                                {
                                                    // 获取元素名称
                                                    System.Reflection.PropertyInfo currentProperty = automationElementType.GetProperty("Current");
                                                    if (currentProperty != null)
                                                    {
                                                        object current = currentProperty.GetValue(element);
                                                        if (current != null)
                                                        {
                                                            System.Reflection.PropertyInfo elementNameProperty = current.GetType().GetProperty("Name");
                                                            if (elementNameProperty != null)
                                                            {
                                                                string name = (string)elementNameProperty.GetValue(current);
                                                                Logger.Debug($"元素 #{i} 名称: {name}");
                                                                
                                                                // 检查是否是密码提示输入框
                                                if (name.Contains("密码提示"))
                                                {
                                                    // 尝试使用ValuePattern获取内容
                                                    string hint = TryGetValuePattern(element, uiaClient, i);
                                                    if (!string.IsNullOrEmpty(hint))
                                                    {
                                                        return hint;
                                                    }
                                                    
                                                    // 尝试使用TextPattern获取内容
                                                    hint = TryGetTextPattern(element, uiaClient, i);
                                                    if (!string.IsNullOrEmpty(hint))
                                                    {
                                                        return hint;
                                                    }
                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                
                return string.Empty;
            }
            catch (Exception ex)
            {
                Logger.Error($"使用UI Automation获取密码提示时出错: {ex.Message}");
                Logger.Error($"异常堆栈: {ex.StackTrace}");
                return string.Empty;
            }
        }

        // 检查是否是WPS的密码加密窗口
        private static bool IsWpsPasswordDialog(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return false;
            
            // 获取窗口标题
            StringBuilder title = new StringBuilder(256);
            GetWindowText(hWnd, title, title.Capacity);
            string titleStr = title.ToString();
            
            // 获取窗口类名
            StringBuilder className = new StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            string classNameStr = className.ToString();
            
            // 直接检查窗口标题是否包含"密码加密"或"密码"
            bool isPasswordDialog = titleStr.Contains("密码加密") || titleStr.Contains("密码") || titleStr.Contains("Password");
            
            Logger.Debug($"窗口标题: {titleStr}, 类名: {classNameStr}, 是否为密码对话框: {isPasswordDialog}");
            
            // 如果标题包含密码相关词汇，则认为是密码加密窗口
            return isPasswordDialog;
        }

        // 检查是否点击了应用按钮
        private static bool IsApplyButton(IntPtr dialogHandle, POINT mousePos)
        {
            try
            {
                Logger.Debug($"开始检查应用按钮，对话框句柄: {dialogHandle}, 鼠标位置: ({mousePos.X}, {mousePos.Y})");
                
                // 首先检查鼠标是否在对话框范围内
                RECT dialogRect = new RECT();
                if (GetWindowRect(dialogHandle, ref dialogRect))
                {
                    Logger.Debug($"对话框矩形: 左={dialogRect.Left}, 上={dialogRect.Top}, 右={dialogRect.Right}, 下={dialogRect.Bottom}");
                    
                    bool isInDialog = mousePos.X >= dialogRect.Left && mousePos.X <= dialogRect.Right &&
                                     mousePos.Y >= dialogRect.Top && mousePos.Y <= dialogRect.Bottom;
                    
                    if (isInDialog)
                    {
                        Logger.Debug($"鼠标在对话框范围内: {isInDialog}");
                        
                        // 计算按钮区域 - 更精确的按钮位置估计
                        int dialogHeight = dialogRect.Bottom - dialogRect.Top;
                        int dialogWidth = dialogRect.Right - dialogRect.Left;
                        
                        // 通常应用按钮位于右下角，高度约为30-40像素
                        int buttonHeight = 40;
                        int buttonWidth = 80;
                        
                        // 计算应用按钮的大致位置
                        int buttonLeft = dialogRect.Right - buttonWidth - 20; // 右边距20
                        int buttonTop = dialogRect.Bottom - buttonHeight - 15; // 下边距15
                        int buttonRight = dialogRect.Right - 20;
                        int buttonBottom = dialogRect.Bottom - 15;
                        
                        Logger.Debug($"应用按钮区域: 左={buttonLeft}, 上={buttonTop}, 右={buttonRight}, 下={buttonBottom}");
                        
                        // 检查鼠标是否在应用按钮区域
                        bool isInButtonArea = mousePos.X >= buttonLeft && mousePos.X <= buttonRight &&
                                             mousePos.Y >= buttonTop && mousePos.Y <= buttonBottom;
                        
                        Logger.Debug($"鼠标是否在应用按钮区域: {isInButtonArea}");
                        
                        if (isInButtonArea)
                        {
                            Logger.Info("检测到点击应用按钮区域");
                            return true;
                        }
                        else
                        {
                            // 备用方案：检查是否在对话框的右下角区域
                            bool isInBottomRight = mousePos.X >= dialogRect.Right - 150 && 
                                                  mousePos.Y >= dialogRect.Bottom - 80;
                            if (isInBottomRight)
                            {
                                Logger.Info("检测到点击对话框右下角区域（备用检测）");
                                return true;
                            }
                        }
                    }
                    else
                    {
                        Logger.Debug($"鼠标不在对话框范围内: {isInDialog}");
                    }
                }
                else
                {
                    Logger.Debug("无法获取对话框矩形");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"检查应用按钮时出错: {ex.Message}");
            }
            
            Logger.Debug("未检测到应用按钮点击");
            return false;
        }

        // 模拟鼠标点击
        private static void SimulateMouseClick(POINT point)
        {
            // 移动鼠标到指定位置
            SetCursorPos(point.X, point.Y);
            
            // 模拟鼠标左键按下
            mouse_event(MOUSEEVENTF_LEFTDOWN, point.X, point.Y, 0, UIntPtr.Zero);
            
            // 模拟鼠标左键释放
            mouse_event(MOUSEEVENTF_LEFTUP, point.X, point.Y, 0, UIntPtr.Zero);
        }
        
        // 尝试显示密码（点击小眼睛按钮）
        private static bool TryRevealPassword(object passwordEdit, Type automationElementType)
        {
            try
            {
                Logger.Debug("尝试找到并点击小眼睛按钮");
                
                // 获取密码输入框的子元素，寻找小眼睛按钮
                System.Reflection.MethodInfo findAllMethod = automationElementType.GetMethod("FindAll");
                if (findAllMethod != null)
                {
                    try
                    {
                        // 获取TreeScope枚举
                        System.Reflection.Assembly uiaTypesAssembly = System.Reflection.Assembly.Load("UIAutomationTypes");
                        if (uiaTypesAssembly != null)
                        {
                            Type treeScopeType = uiaTypesAssembly.GetType("System.Windows.Automation.TreeScope");
                            if (treeScopeType != null)
                            {
                                object treeScopeDescendants = Enum.Parse(treeScopeType, "Descendants");
                                
                                // 获取ControlType.Button
                                Type controlTypeType = uiaTypesAssembly.GetType("System.Windows.Automation.ControlType");
                                if (controlTypeType != null)
                                {
                                    System.Reflection.FieldInfo buttonField = controlTypeType.GetField("Button");
                                    if (buttonField != null)
                                    {
                                        object buttonControlType = buttonField.GetValue(null);
                                        
                                        // 获取PropertyCondition类
                                        System.Reflection.Assembly uiaClientAssembly = System.Reflection.Assembly.Load("UIAutomationClient");
                                        if (uiaClientAssembly != null)
                                        {
                                            Type propertyConditionType = uiaClientAssembly.GetType("System.Windows.Automation.PropertyCondition");
                                            if (propertyConditionType != null)
                                            {
                                                // 获取ControlTypeProperty
                                                System.Reflection.PropertyInfo controlTypePropertyInfo = automationElementType.GetProperty("ControlTypeProperty");
                                                if (controlTypePropertyInfo != null)
                                                {
                                                    object controlTypeProperty = controlTypePropertyInfo.GetValue(null);
                                                    if (controlTypeProperty != null)
                                                    {
                                                        // 创建按钮条件
                                                        object buttonCondition = Activator.CreateInstance(propertyConditionType, new object[] { controlTypeProperty, buttonControlType });
                                                        if (buttonCondition != null)
                                                        {
                                                            // 查找所有按钮
                                                            object buttons = findAllMethod.Invoke(passwordEdit, new object[] { treeScopeDescendants, buttonCondition });
                                                            if (buttons != null)
                                                            {
                                                                // 获取按钮数量
                                                                System.Reflection.PropertyInfo countProperty = buttons.GetType().GetProperty("Count");
                                                                if (countProperty != null)
                                                                {
                                                                    int count = (int)countProperty.GetValue(buttons);
                                                                    Logger.Debug($"找到 {count} 个按钮");
                                                                    
                                                                    // 遍历按钮，寻找小眼睛按钮
                                                                    for (int i = 0; i < count; i++)
                                                                    {
                                                                        try
                                                                        {
                                                                            System.Reflection.MethodInfo getItemMethod = buttons.GetType().GetMethod("get_Item");
                                                                            if (getItemMethod != null)
                                                                            {
                                                                                object button = getItemMethod.Invoke(buttons, new object[] { i });
                                                                                if (button != null)
                                                                                {
                                                                                    // 获取按钮名称
                                                                                    System.Reflection.PropertyInfo currentProperty = button.GetType().GetProperty("Current");
                                                                                    if (currentProperty != null)
                                                                                    {
                                                                                        object current = currentProperty.GetValue(button);
                                                                                        if (current != null)
                                                                                        {
                                                                                            System.Reflection.PropertyInfo nameProperty = current.GetType().GetProperty("Name");
                                                                                            if (nameProperty != null)
                                                                                            {
                                                                                                string name = (string)nameProperty.GetValue(current);
                                                                                                if (!string.IsNullOrEmpty(name))
                                                                                                {
                                                                                                    Logger.Debug($"按钮 #{i} 名称: {name}");
                                                                                                    
                                                                                                    // 检查是否是小眼睛按钮
                                                                                                    if (name.Contains("眼睛") || name.Contains("eye") || name.Contains("reveal") || name.Contains("显示"))
                                                                                                    {
                                                                                                        Logger.Info("找到小眼睛按钮");
                                                                                                        
                                                                                                        // 获取按钮位置并点击
                                                                                                        System.Reflection.PropertyInfo boundingRectangleProperty = current.GetType().GetProperty("BoundingRectangle");
                                                                                                        if (boundingRectangleProperty != null)
                                                                                                        {
                                                                                                            object boundingRectangle = boundingRectangleProperty.GetValue(current);
                                                                                                            if (boundingRectangle != null)
                                                                                                            {
                                                                                                                try
                                                                                                                {
                                                                                                                    // 获取矩形坐标
                                                                                                                    System.Reflection.PropertyInfo leftProperty = boundingRectangle.GetType().GetProperty("Left");
                                                                                                                    System.Reflection.PropertyInfo topProperty = boundingRectangle.GetType().GetProperty("Top");
                                                                                                                    System.Reflection.PropertyInfo rightProperty = boundingRectangle.GetType().GetProperty("Right");
                                                                                                                    System.Reflection.PropertyInfo bottomProperty = boundingRectangle.GetType().GetProperty("Bottom");
                                                                                                                    
                                                                                                                    if (leftProperty != null && topProperty != null && rightProperty != null && bottomProperty != null)
                                                                                                                    {
                                                                                                                        // 处理可能的类型转换问题
                                                                                                                        object leftValue = leftProperty.GetValue(boundingRectangle);
                                                                                                                        object topValue = topProperty.GetValue(boundingRectangle);
                                                                                                                        object rightValue = rightProperty.GetValue(boundingRectangle);
                                                                                                                        object bottomValue = bottomProperty.GetValue(boundingRectangle);
                                                                                                                        
                                                                                                                        int left = Convert.ToInt32(leftValue);
                                                                                                                        int top = Convert.ToInt32(topValue);
                                                                                                                        int right = Convert.ToInt32(rightValue);
                                                                                                                        int bottom = Convert.ToInt32(bottomValue);
                                                                                                                        
                                                                                                                        // 计算按钮中心点
                                                                                                                        int centerX = (left + right) / 2;
                                                                                                                        int centerY = (top + bottom) / 2;
                                                                                                                        
                                                                                                                        // 模拟点击
                                                                                                                        POINT point = new POINT { X = centerX, Y = centerY };
                                                                                                                        SimulateMouseClick(point);
                                                                                                                        Logger.Info("已点击小眼睛按钮");
                                                                                                                        
                                                                                                                        // 等待一小段时间让密码显示
                                                                                                                        Thread.Sleep(100);
                                                                                                                        return true;
                                                                                                                    }
                                                                                                                }
                                                                                                                catch (Exception ex)
                                                                                                                {
                                                                                                                    Logger.Error($"获取按钮位置时出错: {ex.Message}");
                                                                                                                }
                                                                                                            }
                                                                                                        }
                                                                                                    }
                                                                                                }
                                                                                            }
                                                                                        }
                                                                                    }
                                                                                }
                                                                            }
                                                                        }
                                                                        catch (Exception ex)
                                                                        {
                                                                            Logger.Error($"处理按钮 #{i} 时出错: {ex.Message}");
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"查找小眼睛按钮时出错: {ex.Message}");
                    }
                }
                
                // 如果找不到小眼睛按钮，尝试其他方法
                Logger.Warning("未找到小眼睛按钮，尝试其他方法");
                
                // 尝试直接模拟点击密码输入框右侧区域（通常是小眼睛按钮的位置）
                try
                {
                    // 获取密码输入框的位置
                    System.Reflection.PropertyInfo currentProperty = passwordEdit.GetType().GetProperty("Current");
                    if (currentProperty != null)
                    {
                        object current = currentProperty.GetValue(passwordEdit);
                        if (current != null)
                        {
                            System.Reflection.PropertyInfo boundingRectangleProperty = current.GetType().GetProperty("BoundingRectangle");
                            if (boundingRectangleProperty != null)
                            {
                                object boundingRectangle = boundingRectangleProperty.GetValue(current);
                                if (boundingRectangle != null)
                                {
                                    System.Reflection.PropertyInfo leftProperty = boundingRectangle.GetType().GetProperty("Left");
                                    System.Reflection.PropertyInfo topProperty = boundingRectangle.GetType().GetProperty("Top");
                                    System.Reflection.PropertyInfo rightProperty = boundingRectangle.GetType().GetProperty("Right");
                                    System.Reflection.PropertyInfo bottomProperty = boundingRectangle.GetType().GetProperty("Bottom");
                                    
                                    if (leftProperty != null && topProperty != null && rightProperty != null && bottomProperty != null)
                                    {
                                        // 处理可能的类型转换问题
                                        object leftValue = leftProperty.GetValue(boundingRectangle);
                                        object topValue = topProperty.GetValue(boundingRectangle);
                                        object rightValue = rightProperty.GetValue(boundingRectangle);
                                        object bottomValue = bottomProperty.GetValue(boundingRectangle);
                                        
                                        int left = Convert.ToInt32(leftValue);
                                        int top = Convert.ToInt32(topValue);
                                        int right = Convert.ToInt32(rightValue);
                                        int bottom = Convert.ToInt32(bottomValue);
                                        
                                        // 点击输入框右侧（小眼睛按钮通常在那里）
                                        int eyeButtonX = right - 20; // 假设小眼睛按钮在右侧20像素处
                                        int eyeButtonY = (top + bottom) / 2;
                                        
                                        POINT point = new POINT { X = eyeButtonX, Y = eyeButtonY };
                                        SimulateMouseClick(point);
                                        Logger.Info("尝试点击密码输入框右侧区域（小眼睛按钮位置）");
                                        Thread.Sleep(100);
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"尝试点击小眼睛按钮位置时出错: {ex.Message}");
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"尝试显示密码时出错: {ex.Message}");
                return false;
            }
        }
        
        // 从密码对话框获取密码
        private static string GetPasswordFromDialog(IntPtr dialogHandle)
        {
            try
            {
                Logger.Debug("尝试从密码对话框获取密码");
                
                // 尝试使用UI Automation获取密码
                string password = GetPasswordUsingUIAutomation(dialogHandle);
                if (!string.IsNullOrEmpty(password))
                {
                    Logger.Info($"通过UI Automation获取到密码: {password}");
                    Logger.Info($"获取到【打开文件密码(O)】输入框内容: {password}");
                    return password;
                }
                
                Logger.Warning("UI Automation获取密码失败，尝试使用传统方法");
                
                // 确保对话框在前台
                SetForegroundWindow(dialogHandle);
                Thread.Sleep(100);
                
                // 查找密码输入框
                IntPtr passwordEdit = FindPasswordEditInDialog(dialogHandle);
                if (passwordEdit != IntPtr.Zero)
                {
                    // 使用SendMessage获取输入框文本
                    string password2 = GetWindowText(passwordEdit);
                    if (!string.IsNullOrEmpty(password2))
                    {
                        Logger.Info($"通过SendMessage获取到密码: {password2}");
                        Logger.Info($"获取到【打开文件密码(O)】输入框内容: {password2}");
                        return password2;
                    }
                    else
                    {
                        Logger.Warning("输入框文本为空");
                    }
                }
                else
                {
                    Logger.Warning("未找到密码输入框");
                }
                
                return string.Empty;
            }
            catch (Exception ex)
            {
                Logger.Error($"获取密码时出错: {ex.Message}");
                return string.Empty;
            }
        }
        
        // 使用UI Automation获取密码
        private static string GetPasswordUsingUIAutomation(IntPtr dialogHandle)
        {
            try
            {
                Logger.Debug("开始使用UI Automation获取密码");
                
                // 尝试加载UIAutomationClient程序集
                System.Reflection.Assembly uiaClient = null;
                try
                {
                    uiaClient = System.Reflection.Assembly.Load("UIAutomationClient, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
                    Logger.Debug("成功加载UIAutomationClient程序集");
                }
                catch (Exception ex)
                {
                    Logger.Error($"加载UIAutomationClient程序集时出错: {ex.Message}");
                    
                    // 尝试加载UIAutomationClient的不同版本
                    try
                    {
                        uiaClient = System.Reflection.Assembly.Load("UIAutomationClient");
                        Logger.Debug("成功加载UIAutomationClient程序集（无版本）");
                    }
                    catch (Exception ex2)
                    {
                        Logger.Error($"加载UIAutomationClient程序集（无版本）时出错: {ex2.Message}");
                        return string.Empty;
                    }
                }
                
                if (uiaClient == null)
                {
                    Logger.Warning("无法加载UIAutomationClient程序集");
                    return string.Empty;
                }
                
                // 尝试加载UIAutomationTypes程序集（包含TreeScope等枚举）
                System.Reflection.Assembly uiaTypes = null;
                try
                {
                    uiaTypes = System.Reflection.Assembly.Load("UIAutomationTypes, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
                    Logger.Debug("成功加载UIAutomationTypes程序集");
                }
                catch (Exception ex)
                {
                    Logger.Error($"加载UIAutomationTypes程序集时出错: {ex.Message}");
                    
                    // 尝试加载UIAutomationTypes的不同版本
                    try
                    {
                        uiaTypes = System.Reflection.Assembly.Load("UIAutomationTypes");
                        Logger.Debug("成功加载UIAutomationTypes程序集（无版本）");
                    }
                    catch (Exception ex2)
                    {
                        Logger.Error($"加载UIAutomationTypes程序集（无版本）时出错: {ex2.Message}");
                        return string.Empty;
                    }
                }
                
                if (uiaTypes == null)
                {
                    Logger.Warning("无法加载UIAutomationTypes程序集");
                    return string.Empty;
                }
                
                // 获取AutomationElement类
                Type automationElementType = uiaClient.GetType("System.Windows.Automation.AutomationElement");
                if (automationElementType == null)
                {
                    Logger.Warning("无法获取AutomationElement类型");
                    return string.Empty;
                }
                
                // 调用FromHandle方法获取对话框元素
                object dialogElement = automationElementType.GetMethod("FromHandle").Invoke(null, new object[] { dialogHandle });
                if (dialogElement == null)
                {
                    Logger.Warning("无法获取对话框的AutomationElement");
                    return string.Empty;
                }
                
                // 获取TreeScope枚举（从UIAutomationTypes程序集）
                Type treeScopeType = uiaTypes.GetType("System.Windows.Automation.TreeScope");
                if (treeScopeType == null)
                {
                    Logger.Warning("无法获取TreeScope类型");
                    return string.Empty;
                }
                object treeScopeDescendants = Enum.Parse(treeScopeType, "Descendants");
                
                // 获取ControlType类（从UIAutomationTypes程序集）
                Type controlTypeType = uiaTypes.GetType("System.Windows.Automation.ControlType");
                if (controlTypeType == null)
                {
                    Logger.Warning("无法获取ControlType类型");
                    return string.Empty;
                }
                System.Reflection.FieldInfo editField = controlTypeType.GetField("Edit");
                if (editField == null)
                {
                    Logger.Warning("无法获取Edit字段");
                    return string.Empty;
                }
                object editControlType = editField.GetValue(null);
                
                // 获取PropertyCondition类（先尝试从UIAutomationClient程序集获取）
                Type propertyConditionType = uiaClient.GetType("System.Windows.Automation.PropertyCondition");
                if (propertyConditionType == null)
                {
                    // 如果UIAutomationClient中没有，再尝试从UIAutomationTypes程序集获取
                    propertyConditionType = uiaTypes.GetType("System.Windows.Automation.PropertyCondition");
                    if (propertyConditionType == null)
                    {
                        Logger.Warning("无法获取PropertyCondition类型");
                        return string.Empty;
                    }
                }
                
                // 获取AutomationElement.ControlTypeProperty
                object controlTypeProperty = null;
                System.Reflection.PropertyInfo controlTypePropertyInfo = automationElementType.GetProperty("ControlTypeProperty");
                if (controlTypePropertyInfo != null)
                {
                    controlTypeProperty = controlTypePropertyInfo.GetValue(null);
                }
                else
                {
                    // 如果属性获取失败，尝试获取字段
                    System.Reflection.FieldInfo controlTypePropertyField = automationElementType.GetField("ControlTypeProperty", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (controlTypePropertyField != null)
                    {
                        controlTypeProperty = controlTypePropertyField.GetValue(null);
                    }
                    else
                    {
                        Logger.Warning("无法获取ControlTypeProperty");
                        return string.Empty;
                    }
                }
                
                // 创建PropertyCondition
                object editCondition = Activator.CreateInstance(propertyConditionType, new object[] { controlTypeProperty, editControlType });
                if (editCondition == null)
                {
                    Logger.Warning("无法创建编辑控件条件");
                    return string.Empty;
                }
                
                // 调用FindAll方法查找所有编辑控件
                System.Reflection.MethodInfo findAllMethod = automationElementType.GetMethod("FindAll");
                if (findAllMethod == null)
                {
                    Logger.Warning("无法获取FindAll方法");
                    return string.Empty;
                }
                object editElements = findAllMethod.Invoke(dialogElement, new object[] { treeScopeDescendants, editCondition });
                if (editElements == null)
                {
                    Logger.Warning("无法找到编辑控件");
                    return string.Empty;
                }
                
                // 获取编辑控件数量
                System.Reflection.PropertyInfo countProperty = editElements.GetType().GetProperty("Count");
                if (countProperty == null)
                {
                    Logger.Warning("无法获取编辑控件数量");
                    return string.Empty;
                }
                int count = (int)countProperty.GetValue(editElements);
                Logger.Info($"找到 {count} 个编辑控件");
                
                // 如果找到编辑控件，获取第一个的文本
                if (count > 0)
                {
                    // 获取第0个元素
                    System.Reflection.MethodInfo getItemMethod = editElements.GetType().GetMethod("get_Item");
                    if (getItemMethod == null)
                    {
                        Logger.Warning("无法获取get_Item方法");
                        return string.Empty;
                    }
                    
                    // 首先查找【打开文件密码】输入框
                    for (int i = 0; i < count; i++)
                    {
                        object passwordEdit = getItemMethod.Invoke(editElements, new object[] { i });
                        if (passwordEdit == null)
                        {
                            Logger.Debug($"无法获取编辑控件 #{i}");
                            continue;
                        }
                        
                        // 获取元素的名称和类名
                        System.Reflection.PropertyInfo currentProperty = automationElementType.GetProperty("Current");
                        if (currentProperty != null)
                        {
                            object current = currentProperty.GetValue(passwordEdit);
                            if (current != null)
                            {
                                System.Reflection.PropertyInfo nameProperty = current.GetType().GetProperty("Name");
                                if (nameProperty != null)
                                {
                                    string name = (string)nameProperty.GetValue(current);
                                    Logger.Debug($"编辑控件 #{i} 名称: {name}");
                                    
                                    // 优先处理【打开文件密码(O)】输入框
                                    if (name.Contains("打开文件密码"))
                                    {
                                        Logger.Info($"找到【打开文件密码】输入框，开始获取内容，实际名称: {name}");
                                        
                                        // 打印当前元素的所有属性，以便调试
                                        try
                                        {
                                            System.Reflection.PropertyInfo[] properties = current.GetType().GetProperties();
                                            foreach (System.Reflection.PropertyInfo prop in properties)
                                            {
                                                try
                                                {
                                                    object value = prop.GetValue(current);
                                                    Logger.Debug($"Current.{prop.Name} = {value}");
                                                }
                                                catch (Exception ex)
                                                {
                                                    Logger.Debug($"获取Current.{prop.Name}时出错: {ex.Message}");
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.Error($"打印属性时出错: {ex.Message}");
                                        }
                                        
                                        // 检查是否为Qt密码输入框
                                        System.Reflection.PropertyInfo classNameProperty = current.GetType().GetProperty("ClassName");
                                        if (classNameProperty != null)
                                        {
                                            string className = (string)classNameProperty.GetValue(current);
                                            Logger.Debug($"编辑控件 #{i} 类名: {className}");
                                            
                                            // 特别处理Qt密码输入框
                                            if (className.Contains("KDPwdLineEditReveal"))
                                            {
                                                Logger.Info($"发现Qt密码输入框: {className}");
                                                
                                                // 尝试使用ValuePattern获取密码（针对Qt密码输入框）
                                                try
                                                {
                                                    Type valuePatternType = uiaClient.GetType("System.Windows.Automation.ValuePattern");
                                                    if (valuePatternType != null)
                                                    {
                                                        Logger.Debug("尝试使用ValuePattern获取Qt密码输入框内容");
                                                        
                                                        // 获取ValuePattern.Pattern
                                                        object valuePatternProperty = null;
                                                        System.Reflection.PropertyInfo valuePatternPropertyInfo = valuePatternType.GetProperty("Pattern");
                                                        if (valuePatternPropertyInfo != null)
                                                        {
                                                            valuePatternProperty = valuePatternPropertyInfo.GetValue(null);
                                                        }
                                                        else
                                                        {
                                                            // 如果属性获取失败，尝试获取字段
                                                            System.Reflection.FieldInfo valuePatternField = valuePatternType.GetField("Pattern", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                                                            if (valuePatternField != null)
                                                            {
                                                                valuePatternProperty = valuePatternField.GetValue(null);
                                                            }
                                                        }
                                                        
                                                        if (valuePatternProperty != null)
                                                        {
                                                            // 尝试获取ValuePattern
                                                            System.Reflection.MethodInfo tryGetCurrentPatternMethod = automationElementType.GetMethod("TryGetCurrentPattern");
                                                            if (tryGetCurrentPatternMethod != null)
                                                            {
                                                                object[] patternParams = new object[2];
                                                                patternParams[0] = valuePatternProperty;
                                                                patternParams[1] = default(object);
                                                                bool gotPattern = (bool)tryGetCurrentPatternMethod.Invoke(passwordEdit, patternParams);
                                                                
                                                                if (gotPattern)
                                                                {
                                                                    object valuePattern = patternParams[1];
                                                                    if (valuePattern != null)
                                                                    {
                                                                        // 获取Value属性
                                                                        System.Reflection.PropertyInfo currentProp = valuePattern.GetType().GetProperty("Current");
                                                                        if (currentProp != null)
                                                                        {
                                                                            object currentObj = currentProp.GetValue(valuePattern);
                                                                            if (currentObj != null)
                                                                            {
                                                                                System.Reflection.PropertyInfo valueProperty = currentObj.GetType().GetProperty("Value");
                                                                                if (valueProperty != null)
                                                                                {
                                                                                    string password = (string)valueProperty.GetValue(currentObj);
                                                                                        if (!string.IsNullOrEmpty(password))
                                                                                        {
                                                                                            // 检查是否是掩码密码（黑点）
                                                                                            if (password.All(c => c == '*'))
                                                                                            {
                                                                                                Logger.Info($"获取到掩码密码，尝试模拟点击小眼睛按钮");
                                                                                                // 尝试模拟点击小眼睛按钮
                                                                                                bool revealed = TryRevealPassword(passwordEdit, automationElementType);
                                                                                                if (revealed)
                                                                                                {
                                                                                                    // 等待更长时间让密码完全显示
                                                                                                    Thread.Sleep(300);
                                                                                                    // 重新获取ValuePattern来确保获取最新的密码值
                                                                                                    try
                                                                                                    {
                                                                                                        object[] innerPatternParams = new object[2];
                                                                                                        innerPatternParams[0] = valuePatternProperty;
                                                                                                        innerPatternParams[1] = default(object);
                                                                                                        bool innerGotPattern = (bool)tryGetCurrentPatternMethod.Invoke(passwordEdit, innerPatternParams);
                                                                                                        if (innerGotPattern)
                                                                                                        {
                                                                                                            object innerValuePattern = innerPatternParams[1];
                                                                                                            if (innerValuePattern != null)
                                                                                                            {
                                                                                                                System.Reflection.PropertyInfo innerCurrentProp = innerValuePattern.GetType().GetProperty("Current");
                                                                                                                if (innerCurrentProp != null)
                                                                                                                {
                                                                                                                    object innerCurrentObj = innerCurrentProp.GetValue(innerValuePattern);
                                                                                                                    if (innerCurrentObj != null)
                                                                                                                    {
                                                                                                                        System.Reflection.PropertyInfo innerValueProperty = innerCurrentObj.GetType().GetProperty("Value");
                                                                                                                        if (innerValueProperty != null)
                                                                                                                        {
                                                                                                                            string revealedPassword = (string)innerValueProperty.GetValue(innerCurrentObj);
                                                                                                                            if (!string.IsNullOrEmpty(revealedPassword) && !revealedPassword.All(c => c == '*'))
                                                                                                                            {
                                                                                                                                Logger.Info($"成功获取到真实密码: {revealedPassword}");
                                                                                                                                return revealedPassword;
                                                                                                                            }
                                                                                                                            else
                                                                                                                            {
                                                                                                                                Logger.Info($"获取到的密码仍然是掩码: {revealedPassword}");
                                                                                                                            }
                                                                                                                        }
                                                                                                                    }
                                                                                                                }
                                                                                                            }
                                                                                                        }
                                                                                                    }
                                                                                                    catch (Exception ex)
                                                                                                    {
                                                                                                        Logger.Error($"重新获取ValuePattern时出错: {ex.Message}");
                                                                                                    }
                                                                                                    
                                                                                                    // 备用方案：尝试直接获取Current.Value属性
                                                                                                    try
                                                                                                    {
                                                                                                        System.Reflection.PropertyInfo innerCurrentProp = passwordEdit.GetType().GetProperty("Current");
                                                                                                        if (innerCurrentProp != null)
                                                                                                        {
                                                                                                            object innerCurrentObj = innerCurrentProp.GetValue(passwordEdit);
                                                                                                            if (innerCurrentObj != null)
                                                                                                            {
                                                                                                                System.Reflection.PropertyInfo innerValueProperty = innerCurrentObj.GetType().GetProperty("Value");
                                                                                                                if (innerValueProperty != null)
                                                                                                                {
                                                                                                                    string directValue = (string)innerValueProperty.GetValue(innerCurrentObj);
                                                                                                                    if (!string.IsNullOrEmpty(directValue) && !directValue.All(c => c == '*'))
                                                                                                                    {
                                                                                                                        Logger.Info($"通过直接获取Current.Value成功获取到真实密码: {directValue}");
                                                                                                                        return directValue;
                                                                                                                    }
                                                                                                                }
                                                                                                            }
                                                                                                        }
                                                                                                    }
                                                                                                    catch (Exception ex)
                                                                                                    {
                                                                                                        Logger.Error($"尝试直接获取Current.Value时出错: {ex.Message}");
                                                                                                    }
                                                                                                }
                                                                                            }
                                                                                            Logger.Info($"从Qt输入框 #{i} 使用ValuePattern获取到密码: {password}");
                                                                                            return password;
                                                                                        }
                                                                                        else
                                                                                        {
                                                                                            Logger.Debug("ValuePattern.Value为空");
                                                                                        }
                                                                                }
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    Logger.Error($"尝试使用ValuePattern获取Qt密码时出错: {ex.Message}");
                                                }
                                                
                                                // 尝试使用TextPattern获取密码
                                                try
                                                {
                                                    Type textPatternType = uiaClient.GetType("System.Windows.Automation.TextPattern");
                                                    if (textPatternType != null)
                                                    {
                                                        Logger.Debug("尝试使用TextPattern获取Qt密码输入框内容");
                                                        
                                                        // 获取TextPattern.Pattern
                                                        object textPatternProperty = null;
                                                        System.Reflection.PropertyInfo textPatternPropertyInfo = textPatternType.GetProperty("Pattern");
                                                        if (textPatternPropertyInfo != null)
                                                        {
                                                            textPatternProperty = textPatternPropertyInfo.GetValue(null);
                                                        }
                                                        else
                                                        {
                                                            // 如果属性获取失败，尝试获取字段
                                                            System.Reflection.FieldInfo textPatternField = textPatternType.GetField("Pattern", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                                                            if (textPatternField != null)
                                                            {
                                                                textPatternProperty = textPatternField.GetValue(null);
                                                            }
                                                        }
                                                        
                                                        if (textPatternProperty != null)
                                                        {
                                                            // 尝试获取TextPattern
                                                            System.Reflection.MethodInfo tryGetCurrentPatternMethod = automationElementType.GetMethod("TryGetCurrentPattern");
                                                            if (tryGetCurrentPatternMethod != null)
                                                            {
                                                                object[] patternParams = new object[2];
                                                                patternParams[0] = textPatternProperty;
                                                                patternParams[1] = default(object);
                                                                bool gotPattern = (bool)tryGetCurrentPatternMethod.Invoke(passwordEdit, patternParams);
                                                                
                                                                if (gotPattern)
                                                                {
                                                                    object textPattern = patternParams[1];
                                                                    if (textPattern != null)
                                                                    {
                                                                        // 获取文档范围
                                                                        System.Reflection.MethodInfo documentRangeMethod = textPatternType.GetMethod("DocumentRange");
                                                                        if (documentRangeMethod != null)
                                                                        {
                                                                            object documentRange = documentRangeMethod.Invoke(textPattern, null);
                                                                            
                                                                            // 获取TextPatternRange类
                                                                            Type textPatternRangeType = uiaClient.GetType("System.Windows.Automation.TextPatternRange");
                                                                            if (textPatternRangeType != null)
                                                                            {
                                                                                // 获取Text属性
                                                                                System.Reflection.MethodInfo getTextMethod = textPatternRangeType.GetMethod("GetText", new Type[] { typeof(int) });
                                                                                if (getTextMethod != null)
                                                                                {
                                                                                    string password = (string)getTextMethod.Invoke(documentRange, new object[] { int.MaxValue });
                                                                                    if (!string.IsNullOrEmpty(password))
                                                                                    {
                                                                                        Logger.Info($"从Qt输入框 #{i} 使用TextPattern获取到密码: {password}");
                                                                                        return password;
                                                                                    }
                                                                                    else
                                                                                    {
                                                                                        Logger.Debug("TextPattern.GetText为空");
                                                                                    }
                                                                                }
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    Logger.Error($"尝试使用TextPattern获取Qt密码时出错: {ex.Message}");
                                                }
                                                
                                                // 尝试直接获取Current.Value属性
                                                try
                                                {
                                                    System.Reflection.PropertyInfo currentProp = passwordEdit.GetType().GetProperty("Current");
                                                    if (currentProp != null)
                                                    {
                                                        object currentObj = currentProp.GetValue(passwordEdit);
                                                        if (currentObj != null)
                                                        {
                                                            System.Reflection.PropertyInfo valueProperty = currentObj.GetType().GetProperty("Value");
                                                            if (valueProperty != null)
                                                            {
                                                                string directValue = (string)valueProperty.GetValue(currentObj);
                                                                if (!string.IsNullOrEmpty(directValue))
                                                                {
                                                                    Logger.Info($"直接从Current.Value获取到Qt【打开文件密码】输入框内容: {directValue}");
                                                                    return directValue;
                                                                }
                                                                else
                                                                {
                                                                    Logger.Debug("Current.Value为空");
                                                                }
                                                            }
                                                            else
                                                            {
                                                                Logger.Debug("未找到Value属性");
                                                            }
                                                        }
                                                        else
                                                        {
                                                            Logger.Debug("Current对象为空");
                                                        }
                                                    }
                                                    else
                                                    {
                                                        Logger.Debug("未找到Current属性");
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    Logger.Error($"尝试直接获取Current.Value时出错: {ex.Message}");
                                                }
                                                
                                                // 尝试获取元素的句柄，然后使用SendMessage获取内容
                                                try
                                                {
                                                    System.Reflection.PropertyInfo nativeWindowHandleProperty = current.GetType().GetProperty("NativeWindowHandle");
                                                    if (nativeWindowHandleProperty != null)
                                                    {
                                                        int handle = (int)nativeWindowHandleProperty.GetValue(current);
                                                        IntPtr hwnd = new IntPtr(handle);
                                                        Logger.Info($"获取到Qt【打开文件密码】输入框句柄: {hwnd}");
                                                        
                                                        // 使用SendMessage获取输入框内容
                                                        string windowText = GetWindowText(hwnd);
                                                        if (!string.IsNullOrEmpty(windowText))
                                                        {
                                                            Logger.Info($"通过SendMessage获取到Qt【打开文件密码】输入框内容: {windowText}");
                                                            return windowText;
                                                        }
                                                        else
                                                        {
                                                            Logger.Debug("SendMessage获取到的内容为空");
                                                        }
                                                    }
                                                    else
                                                    {
                                                        Logger.Debug("未找到NativeWindowHandle属性");
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    Logger.Error($"尝试使用SendMessage获取Qt密码时出错: {ex.Message}");
                                                }
                                            }
                                        }
                                        
                                        // 对于非Qt密码输入框，使用常规方法
                                        // 尝试使用ValuePattern获取密码
                                        try
                                        {
                                            Type valuePatternType = uiaClient.GetType("System.Windows.Automation.ValuePattern");
                                            if (valuePatternType != null)
                                            {
                                                Logger.Debug("尝试使用ValuePattern获取密码");
                                                
                                                // 获取ValuePattern.Pattern
                                                object valuePatternProperty = null;
                                                System.Reflection.PropertyInfo valuePatternPropertyInfo = valuePatternType.GetProperty("Pattern");
                                                if (valuePatternPropertyInfo != null)
                                                {
                                                    valuePatternProperty = valuePatternPropertyInfo.GetValue(null);
                                                }
                                                else
                                                {
                                                    // 如果属性获取失败，尝试获取字段
                                                    System.Reflection.FieldInfo valuePatternField = valuePatternType.GetField("Pattern", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                                                    if (valuePatternField != null)
                                                    {
                                                        valuePatternProperty = valuePatternField.GetValue(null);
                                                    }
                                                }
                                                
                                                if (valuePatternProperty != null)
                                                {
                                                    // 尝试获取ValuePattern
                                                    System.Reflection.MethodInfo tryGetCurrentPatternMethod = automationElementType.GetMethod("TryGetCurrentPattern");
                                                    if (tryGetCurrentPatternMethod != null)
                                                    {
                                                        object[] patternParams = new object[2];
                                                        patternParams[0] = valuePatternProperty;
                                                        patternParams[1] = default(object);
                                                        bool gotPattern = (bool)tryGetCurrentPatternMethod.Invoke(passwordEdit, patternParams);
                                                        
                                                        if (gotPattern)
                                                        {
                                                            object valuePattern = patternParams[1];
                                                            if (valuePattern != null)
                                                            {
                                                                // 获取Value属性
                                                                System.Reflection.PropertyInfo currentProp = valuePattern.GetType().GetProperty("Current");
                                                                if (currentProp != null)
                                                                {
                                                                    object currentObj = currentProp.GetValue(valuePattern);
                                                                    if (currentObj != null)
                                                                    {
                                                                        System.Reflection.PropertyInfo valueProperty = currentObj.GetType().GetProperty("Value");
                                                                        if (valueProperty != null)
                                                                        {
                                                                            string password = (string)valueProperty.GetValue(currentObj);
                                                                            if (!string.IsNullOrEmpty(password))
                                                                            {
                                                                                Logger.Info($"从输入框 #{i} 使用ValuePattern获取到密码: {password}");
                                                                                return password;
                                                                            }
                                                                            else
                                                                            {
                                                                                Logger.Debug("ValuePattern.Value为空");
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.Error($"尝试使用ValuePattern获取密码时出错: {ex.Message}");
                                        }
                                        
                                        // 尝试使用TextPattern获取密码
                                        try
                                        {
                                            Type textPatternType = uiaClient.GetType("System.Windows.Automation.TextPattern");
                                            if (textPatternType != null)
                                            {
                                                Logger.Debug("尝试使用TextPattern获取密码");
                                                
                                                // 获取TextPattern.Pattern
                                                object textPatternProperty = null;
                                                System.Reflection.PropertyInfo textPatternPropertyInfo = textPatternType.GetProperty("Pattern");
                                                if (textPatternPropertyInfo != null)
                                                {
                                                    textPatternProperty = textPatternPropertyInfo.GetValue(null);
                                                }
                                                else
                                                {
                                                    // 如果属性获取失败，尝试获取字段
                                                    System.Reflection.FieldInfo textPatternField = textPatternType.GetField("Pattern", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                                                    if (textPatternField != null)
                                                    {
                                                        textPatternProperty = textPatternField.GetValue(null);
                                                    }
                                                }
                                                
                                                if (textPatternProperty != null)
                                                {
                                                    // 尝试获取TextPattern
                                                    System.Reflection.MethodInfo tryGetCurrentPatternMethod = automationElementType.GetMethod("TryGetCurrentPattern");
                                                    if (tryGetCurrentPatternMethod != null)
                                                    {
                                                        object[] patternParams = new object[2];
                                                        patternParams[0] = textPatternProperty;
                                                        patternParams[1] = default(object);
                                                        bool gotPattern = (bool)tryGetCurrentPatternMethod.Invoke(passwordEdit, patternParams);
                                                        
                                                        if (gotPattern)
                                                        {
                                                            object textPattern = patternParams[1];
                                                            if (textPattern != null)
                                                            {
                                                                // 获取文档范围
                                                                System.Reflection.MethodInfo documentRangeMethod = textPatternType.GetMethod("DocumentRange");
                                                                if (documentRangeMethod != null)
                                                                {
                                                                    object documentRange = documentRangeMethod.Invoke(textPattern, null);
                                                                    
                                                                    // 获取TextPatternRange类
                                                                    Type textPatternRangeType = uiaClient.GetType("System.Windows.Automation.TextPatternRange");
                                                                    if (textPatternRangeType != null)
                                                                    {
                                                                        // 获取Text属性
                                                                        System.Reflection.MethodInfo getTextMethod = textPatternRangeType.GetMethod("GetText", new Type[] { typeof(int) });
                                                                        if (getTextMethod != null)
                                                                        {
                                                                            string password = (string)getTextMethod.Invoke(documentRange, new object[] { int.MaxValue });
                                                                            if (!string.IsNullOrEmpty(password))
                                                                            {
                                                                                Logger.Info($"从输入框 #{i} 使用TextPattern获取到密码: {password}");
                                                                                return password;
                                                                            }
                                                                            else
                                                                            {
                                                                                Logger.Debug("TextPattern.GetText为空");
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.Error($"尝试使用TextPattern获取密码时出错: {ex.Message}");
                                        }
                                        
                                        // 尝试直接获取Current.Value属性
                                        try
                                        {
                                            System.Reflection.PropertyInfo currentProp = passwordEdit.GetType().GetProperty("Current");
                                            if (currentProp != null)
                                            {
                                                object currentObj = currentProp.GetValue(passwordEdit);
                                                if (currentObj != null)
                                                {
                                                    System.Reflection.PropertyInfo valueProperty = currentObj.GetType().GetProperty("Value");
                                                    if (valueProperty != null)
                                                    {
                                                        string directValue = (string)valueProperty.GetValue(currentObj);
                                                        if (!string.IsNullOrEmpty(directValue))
                                                        {
                                                            Logger.Info($"直接从Current.Value获取到【打开文件密码】输入框内容: {directValue}");
                                                            return directValue;
                                                        }
                                                        else
                                                        {
                                                            Logger.Debug("Current.Value为空");
                                                        }
                                                    }
                                                    else
                                                    {
                                                        Logger.Debug("未找到Value属性");
                                                    }
                                                }
                                                else
                                                {
                                                    Logger.Debug("Current对象为空");
                                                }
                                            }
                                            else
                                            {
                                                Logger.Debug("未找到Current属性");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.Error($"尝试直接获取Current.Value时出错: {ex.Message}");
                                        }
                                        
                                        // 尝试获取元素的句柄，然后使用SendMessage获取内容
                                        try
                                        {
                                            System.Reflection.PropertyInfo nativeWindowHandleProperty = current.GetType().GetProperty("NativeWindowHandle");
                                            if (nativeWindowHandleProperty != null)
                                            {
                                                int handle = (int)nativeWindowHandleProperty.GetValue(current);
                                                IntPtr hwnd = new IntPtr(handle);
                                                Logger.Info($"获取到【打开文件密码】输入框句柄: {hwnd}");
                                                
                                                // 使用SendMessage获取输入框内容
                                                string windowText = GetWindowText(hwnd);
                                                if (!string.IsNullOrEmpty(windowText))
                                                {
                                                    Logger.Info($"通过SendMessage获取到【打开文件密码】输入框内容: {windowText}");
                                                    return windowText;
                                                }
                                                else
                                                {
                                                    Logger.Debug("SendMessage获取到的内容为空");
                                                }
                                            }
                                            else
                                            {
                                                Logger.Debug("未找到NativeWindowHandle属性");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.Error($"尝试使用SendMessage获取密码时出错: {ex.Message}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    // 如果没有找到【打开文件密码】输入框，尝试其他输入框
                    for (int i = 0; i < count; i++)
                    {
                        object passwordEdit = getItemMethod.Invoke(editElements, new object[] { i });
                        if (passwordEdit == null)
                        {
                            Logger.Debug($"无法获取编辑控件 #{i}");
                            continue;
                        }
                        
                        // 尝试使用ValuePattern获取密码
                        string password = TryGetValuePattern(passwordEdit, uiaClient, i);
                        if (!string.IsNullOrEmpty(password))
                        {
                            return password;
                        }
                        
                        // 尝试使用TextPattern获取密码
                        password = TryGetTextPattern(passwordEdit, uiaClient, i);
                        if (!string.IsNullOrEmpty(password))
                        {
                            return password;
                        }
                    }
                }
                
                // 尝试通过名称查找密码输入框
                string passwordByName = FindPasswordByName(dialogElement, uiaClient, treeScopeDescendants, propertyConditionType, automationElementType);
                if (!string.IsNullOrEmpty(passwordByName))
                {
                    return passwordByName;
                }
                
                // 尝试通过类名查找Qt密码输入框
                string passwordByClass = FindPasswordByQtClass(dialogElement, uiaClient, treeScopeDescendants, propertyConditionType, automationElementType, findAllMethod);
                if (!string.IsNullOrEmpty(passwordByClass))
                {
                    return passwordByClass;
                }
                
                Logger.Warning("UI Automation未找到密码输入框");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Logger.Error($"使用UI Automation获取密码时出错: {ex.Message}");
                Logger.Error($"异常堆栈: {ex.StackTrace}");
                return string.Empty;
            }
        }
        
        // 尝试使用ValuePattern获取密码
        private static string TryGetValuePattern(object passwordEdit, System.Reflection.Assembly uiaClient, int index)
        {
            try
            {
                Type valuePatternType = uiaClient.GetType("System.Windows.Automation.ValuePattern");
                if (valuePatternType == null)
                {
                    Logger.Warning("无法获取ValuePattern类型");
                    return string.Empty;
                }
                
                // 获取ValuePattern.Pattern
                object valuePatternProperty = null;
                System.Reflection.PropertyInfo valuePatternPropertyInfo = valuePatternType.GetProperty("Pattern");
                if (valuePatternPropertyInfo != null)
                {
                    valuePatternProperty = valuePatternPropertyInfo.GetValue(null);
                    Logger.Debug("成功获取ValuePattern.Pattern属性");
                }
                else
                {
                    // 如果属性获取失败，尝试获取字段
                    System.Reflection.FieldInfo valuePatternField = valuePatternType.GetField("Pattern", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (valuePatternField != null)
                    {
                        valuePatternProperty = valuePatternField.GetValue(null);
                        Logger.Debug("成功获取ValuePattern.Pattern字段");
                    }
                    else
                    {
                        Logger.Warning("无法获取ValuePattern.Pattern");
                        return string.Empty;
                    }
                }
                
                // 尝试获取ValuePattern
                Type automationElementType = uiaClient.GetType("System.Windows.Automation.AutomationElement");
                System.Reflection.MethodInfo tryGetCurrentPatternMethod = automationElementType.GetMethod("TryGetCurrentPattern");
                if (tryGetCurrentPatternMethod == null)
                {
                    Logger.Warning("无法获取TryGetCurrentPattern方法");
                    return string.Empty;
                }
                
                object[] patternParams = new object[2];
                patternParams[0] = valuePatternProperty;
                patternParams[1] = default(object);
                bool gotPattern = (bool)tryGetCurrentPatternMethod.Invoke(passwordEdit, patternParams);
                
                if (gotPattern)
                {
                    object valuePattern = patternParams[1];
                    if (valuePattern == null)
                    {
                        Logger.Debug($"无法获取编辑控件 #{index} 的ValuePattern");
                        return string.Empty;
                    }
                    
                    // 获取Value属性
                    System.Reflection.PropertyInfo currentProperty = valuePattern.GetType().GetProperty("Current");
                    if (currentProperty == null)
                    {
                        Logger.Warning("无法获取ValuePattern.Current属性");
                        return string.Empty;
                    }
                    object current = currentProperty.GetValue(valuePattern);
                    
                    System.Reflection.PropertyInfo valueProperty = current.GetType().GetProperty("Value");
                    if (valueProperty == null)
                    {
                        Logger.Warning("无法获取Value属性");
                        return string.Empty;
                    }
                    
                    string password = (string)valueProperty.GetValue(current);
                    if (!string.IsNullOrEmpty(password))
                    {
                        Logger.Info($"从输入框 #{index} 使用ValuePattern获取到密码: {password}");
                        return password;
                    }
                    else
                    {
                        Logger.Debug($"输入框 #{index} 为空");
                    }
                }
                else
                {
                    Logger.Debug($"无法获取编辑控件 #{index} 的ValuePattern");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"使用ValuePattern获取密码时出错: {ex.Message}");
                Logger.Error($"异常堆栈: {ex.StackTrace}");
            }
            return string.Empty;
        }
        
        // 尝试使用TextPattern获取密码
        private static string TryGetTextPattern(object passwordEdit, System.Reflection.Assembly uiaClient, int index)
        {
            try
            {
                Type textPatternType = uiaClient.GetType("System.Windows.Automation.TextPattern");
                if (textPatternType == null)
                {
                    Logger.Warning("无法获取TextPattern类型");
                    return string.Empty;
                }
                
                // 获取TextPattern.Pattern
                object textPatternProperty = null;
                System.Reflection.PropertyInfo textPatternPropertyInfo = textPatternType.GetProperty("Pattern");
                if (textPatternPropertyInfo != null)
                {
                    textPatternProperty = textPatternPropertyInfo.GetValue(null);
                    Logger.Debug("成功获取TextPattern.Pattern属性");
                }
                else
                {
                    // 如果属性获取失败，尝试获取字段
                    System.Reflection.FieldInfo textPatternField = textPatternType.GetField("Pattern", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (textPatternField != null)
                    {
                        textPatternProperty = textPatternField.GetValue(null);
                        Logger.Debug("成功获取TextPattern.Pattern字段");
                    }
                    else
                    {
                        Logger.Warning("无法获取TextPattern.Pattern");
                        return string.Empty;
                    }
                }
                
                // 尝试获取TextPattern
                Type automationElementType = uiaClient.GetType("System.Windows.Automation.AutomationElement");
                System.Reflection.MethodInfo tryGetCurrentPatternMethod = automationElementType.GetMethod("TryGetCurrentPattern");
                if (tryGetCurrentPatternMethod == null)
                {
                    Logger.Warning("无法获取TryGetCurrentPattern方法");
                    return string.Empty;
                }
                
                object[] patternParams = new object[2];
                patternParams[0] = textPatternProperty;
                patternParams[1] = default(object);
                bool gotPattern = (bool)tryGetCurrentPatternMethod.Invoke(passwordEdit, patternParams);
                
                if (gotPattern)
                {
                    object textPattern = patternParams[1];
                    if (textPattern == null)
                    {
                        Logger.Debug($"无法获取编辑控件 #{index} 的TextPattern");
                        return string.Empty;
                    }
                    
                    // 获取文档范围
                    System.Reflection.MethodInfo documentRangeMethod = textPatternType.GetMethod("DocumentRange");
                    if (documentRangeMethod == null)
                    {
                        Logger.Warning("无法获取DocumentRange方法");
                        return string.Empty;
                    }
                    object documentRange = documentRangeMethod.Invoke(textPattern, null);
                    
                    // 获取TextPatternRange类
                    Type textPatternRangeType = uiaClient.GetType("System.Windows.Automation.TextPatternRange");
                    if (textPatternRangeType == null)
                    {
                        Logger.Warning("无法获取TextPatternRange类型");
                        return string.Empty;
                    }
                    
                    // 获取Text属性
                    System.Reflection.MethodInfo getTextMethod = textPatternRangeType.GetMethod("GetText", new Type[] { typeof(int) });
                    if (getTextMethod == null)
                    {
                        Logger.Warning("无法获取GetText方法");
                        return string.Empty;
                    }
                    
                    string password = (string)getTextMethod.Invoke(documentRange, new object[] { int.MaxValue });
                    if (!string.IsNullOrEmpty(password))
                    {
                        Logger.Info($"从输入框 #{index} 使用TextPattern获取到密码: {password}");
                        return password;
                    }
                    else
                    {
                        Logger.Debug($"输入框 #{index} 为空");
                    }
                }
                else
                {
                    Logger.Debug($"无法获取编辑控件 #{index} 的TextPattern");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"使用TextPattern获取密码时出错: {ex.Message}");
                Logger.Error($"异常堆栈: {ex.StackTrace}");
            }
            return string.Empty;
        }
        
        // 通过名称查找密码输入框
        private static string FindPasswordByName(object dialogElement, System.Reflection.Assembly uiaClient, object treeScopeDescendants, Type propertyConditionType, Type automationElementType)
        {
            try
            {
                // 获取AutomationElement.NameProperty
                object nameProperty = null;
                System.Reflection.PropertyInfo namePropertyInfo = automationElementType.GetProperty("NameProperty");
                if (namePropertyInfo != null)
                {
                    nameProperty = namePropertyInfo.GetValue(null);
                }
                else
                {
                    // 如果属性获取失败，尝试获取字段
                    System.Reflection.FieldInfo namePropertyField = automationElementType.GetField("NameProperty", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (namePropertyField != null)
                    {
                        nameProperty = namePropertyField.GetValue(null);
                    }
                    else
                    {
                        Logger.Warning("无法获取NameProperty");
                        return string.Empty;
                    }
                }
                
                // 尝试不同的名称变体
                string[] passwordLabels = { "打开文件密码(O)", "打开文件密码", "文件密码", "密码", "Password" };
                
                foreach (string label in passwordLabels)
                {
                    Logger.Debug($"尝试通过标签查找: {label}");
                    object passwordCondition = Activator.CreateInstance(propertyConditionType, new object[] { nameProperty, label });
                    if (passwordCondition == null)
                    {
                        Logger.Debug($"无法创建标签条件: {label}");
                        continue;
                    }
                    
                    System.Reflection.MethodInfo findFirstMethod = automationElementType.GetMethod("FindFirst");
                    if (findFirstMethod == null)
                    {
                        Logger.Warning("无法获取FindFirst方法");
                        return string.Empty;
                    }
                    object passwordElement = findFirstMethod.Invoke(dialogElement, new object[] { treeScopeDescendants, passwordCondition });
                    
                    if (passwordElement != null)
                    {
                        Logger.Debug($"找到标签元素: {label}");
                        
                        // 查找密码输入框（通常是标签旁边的编辑控件）
                        System.Reflection.PropertyInfo nextProperty = automationElementType.GetProperty("Next");
                        if (nextProperty != null)
                        {
                            object siblingElement = nextProperty.GetValue(passwordElement);
                            
                            if (siblingElement != null && siblingElement != DBNull.Value)
                            {
                                // 尝试使用ValuePattern获取密码
                                string password = TryGetValuePattern(siblingElement, uiaClient, 0);
                                if (!string.IsNullOrEmpty(password))
                                {
                                    return password;
                                }
                                
                                // 尝试使用TextPattern获取密码
                                password = TryGetTextPattern(siblingElement, uiaClient, 0);
                                if (!string.IsNullOrEmpty(password))
                                {
                                    return password;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"通过名称查找密码时出错: {ex.Message}");
            }
            return string.Empty;
        }
        
        // 通过Qt类名查找密码输入框
        private static string FindPasswordByQtClass(object dialogElement, System.Reflection.Assembly uiaClient, object treeScopeDescendants, Type propertyConditionType, Type automationElementType, System.Reflection.MethodInfo findAllMethod)
        {
            try
            {
                // 获取AutomationElement.ClassNameProperty
                System.Reflection.PropertyInfo classNamePropertyInfo = automationElementType.GetProperty("ClassNameProperty");
                if (classNamePropertyInfo == null)
                {
                    Logger.Warning("无法获取ClassNameProperty");
                    return string.Empty;
                }
                object classNameProperty = classNamePropertyInfo.GetValue(null);
                
                // 尝试不同的Qt密码输入框类名
                string[] qtPasswordClasses = { "kd::expand::KDPwdLineEditReveal", "KDPwdLineEditReveal", "QLineEdit" };
                
                foreach (string qtClass in qtPasswordClasses)
                {
                    Logger.Debug($"尝试通过Qt类名查找: {qtClass}");
                    object classCondition = Activator.CreateInstance(propertyConditionType, new object[] { classNameProperty, qtClass });
                    if (classCondition == null)
                    {
                        Logger.Debug($"无法创建类条件: {qtClass}");
                        continue;
                    }
                    
                    object qtElements = findAllMethod.Invoke(dialogElement, new object[] { treeScopeDescendants, classCondition });
                    if (qtElements == null)
                    {
                        Logger.Debug($"未找到类为 {qtClass} 的元素");
                        continue;
                    }
                    
                    // 获取元素数量
                    System.Reflection.PropertyInfo countProperty = qtElements.GetType().GetProperty("Count");
                    if (countProperty == null)
                    {
                        Logger.Warning("无法获取元素数量");
                        continue;
                    }
                    int qtCount = (int)countProperty.GetValue(qtElements);
                    
                    if (qtCount > 0)
                    {
                        Logger.Debug($"找到 {qtCount} 个Qt密码输入框");
                        
                        // 获取第一个Qt密码输入框
                        System.Reflection.MethodInfo getItemMethod = qtElements.GetType().GetMethod("get_Item");
                        if (getItemMethod == null)
                        {
                            Logger.Warning("无法获取get_Item方法");
                            continue;
                        }
                        object qtPasswordEdit = getItemMethod.Invoke(qtElements, new object[] { 0 });
                        
                        // 尝试使用ValuePattern获取密码
                        string password = TryGetValuePattern(qtPasswordEdit, uiaClient, 0);
                        if (!string.IsNullOrEmpty(password))
                        {
                            return password;
                        }
                        
                        // 尝试使用TextPattern获取密码
                        password = TryGetTextPattern(qtPasswordEdit, uiaClient, 0);
                        if (!string.IsNullOrEmpty(password))
                        {
                            return password;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"通过Qt类名查找密码时出错: {ex.Message}");
            }
            return string.Empty;
        }
        
        // 查找密码对话框中的密码输入框
        private static IntPtr FindPasswordEditInDialog(IntPtr dialogHandle)
        {
            try
            {
                Logger.Debug("开始查找密码输入框");
                
                // 定义委托和变量
                IntPtr foundEdit = IntPtr.Zero;
                int editCount = 0;
                
                // 枚举所有子窗口
                EnumChildWindows(dialogHandle, (hwnd, lParam) =>
                {
                    // 获取窗口类名
                    StringBuilder className = new StringBuilder(256);
                    GetClassName(hwnd, className, className.Capacity);
                    string classNameStr = className.ToString();
                    
                    // 获取窗口文本
                    StringBuilder windowText = new StringBuilder(256);
                    GetWindowText(hwnd, windowText, windowText.Capacity);
                    string windowTextStr = windowText.ToString();
                    
                    Logger.Debug($"检查窗口: 句柄={hwnd}, 类名={classNameStr}, 文本={windowTextStr}");
                    
                    // 检查是否为编辑控件，特别关注Qt密码输入框类
                    if (IsEditControl(classNameStr))
                    {
                        editCount++;
                        Logger.Debug($"找到编辑控件 #{editCount}: 句柄={hwnd}, 类名={classNameStr}");
                        
                        // 返回第一个编辑控件
                        if (editCount == 1)
                        {
                            foundEdit = hwnd;
                            return false; // 停止枚举
                        }
                    }
                    
                    // 递归查找子窗口
                    IntPtr childEdit = FindPasswordEditInDialog(hwnd);
                    if (childEdit != IntPtr.Zero)
                    {
                        foundEdit = childEdit;
                        return false; // 停止枚举
                    }
                    
                    return true; // 继续枚举
                }, IntPtr.Zero);
                
                return foundEdit;
            }
            catch (Exception ex)
            {
                Logger.Error($"查找密码输入框时出错: {ex.Message}");
                return IntPtr.Zero;
            }
        }
        
        // 检查是否为编辑控件
        private static bool IsEditControl(string className)
        {
            string[] editControlClasses = {
                "Edit", "TextBox", "RichEdit", "RichEdit20W", "RichEdit50W",
                "QLineEdit", "QTextEdit", "QPlainTextEdit", "LineEdit", "TextEdit",
                "INPUT", "edit", "text", "Text", "Edit", "qt", "Qt",
                "QWidget", "QDialog", "QMainWindow", "QFrame",
                "KDPwdLineEditReveal", "kd::expand::KDPwdLineEditReveal" // Qt密码输入框类
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
        
        // 获取窗口文本
        private static string GetWindowText(IntPtr hWnd)
        {
            // 首先尝试使用GetWindowText
            StringBuilder sb = new StringBuilder(256);
            int length = GetWindowText(hWnd, sb, sb.Capacity);
            if (length > 0)
            {
                return sb.ToString();
            }
            
            // 如果GetWindowText失败，尝试使用SendMessage WM_GETTEXT
            const uint WM_GETTEXT = 0x000D;
            StringBuilder sb2 = new StringBuilder(256);
            // 使用正确的SendMessage重载
            SendMessageText(hWnd, WM_GETTEXT, (IntPtr)256, sb2);
            return sb2.ToString();
        }
        
        // SendMessage重载，用于获取文本
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessageText(IntPtr hWnd, uint Msg, IntPtr wParam, StringBuilder lParam);
    }
}
