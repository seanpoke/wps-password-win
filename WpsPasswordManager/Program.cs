using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Text;
using WpsPasswordManager.Monitor;
using WpsPasswordManager.Business;
using WpsPasswordManager.Simulator;
using WpsPasswordManager.UI;
using WpsPasswordManager.Utils;

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

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Logger.Info("WPS 密码自动填充插件启动");

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

            // 启动监控线程
            Thread monitorThread = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        // 检查WPS是否运行
                        if (monitor.IsWpsRunning())
                        {
                            Logger.Debug("WPS 正在运行");
                            // 检查加密对话框
                            IntPtr encryptDialog = monitor.FindPasswordDialog();
                            if (encryptDialog != IntPtr.Zero)
                            {
                                Logger.Debug($"找到加密对话框: {encryptDialog}");
                                // 直接在对话框位置显示悬浮按钮（优先显示在打开文件密码标签旁边）
                            Logger.Debug("在对话框位置显示悬浮按钮");
                            Application.DoEvents();
                            floatingButton.ShowAtDialog(encryptDialog);
                            Logger.Info("悬浮按钮已显示");
                            }
                            else
                            {
                                // 隐藏悬浮按钮
                                floatingButton.HideButton();
                                Logger.Debug("未找到加密对话框，隐藏悬浮按钮");
                            }

                            // 检查解密对话框
                            IntPtr decryptDialog = monitor.FindPasswordDialog();
                            if (decryptDialog != IntPtr.Zero)
                            {
                                Logger.Debug($"找到解密对话框: {decryptDialog}");
                                // 尝试找到密码输入框
                                IntPtr passwordEdit = monitor.FindPasswordEdit(decryptDialog);
                                if (passwordEdit != IntPtr.Zero)
                                {
                                    Logger.Debug($"找到密码输入框: {passwordEdit}");
                                    // 写死密码为 z0rfi7llkdc
                                    string password = "z0rfi7llkdc";
                                    Logger.Debug($"填充密码: {password}");
                                    // 先清空输入框
                                    simulator.ClearInput(passwordEdit);
                                    Logger.Debug("已清空密码输入框");
                                    // 填充密码
                                    simulator.SimulatePasswordInput(passwordEdit, password);
                                    Logger.Info("密码已填充到解密输入框");
                                    
                                    // 尝试找到确定按钮并点击
                                    IntPtr okButton = monitor.FindOKButton(decryptDialog);
                                    if (okButton != IntPtr.Zero)
                                    {
                                        Logger.Debug($"找到确定按钮: {okButton}");
                                        // 点击确定按钮
                                        simulator.SimulateButtonClick(okButton);
                                        Logger.Info("已点击确定按钮");
                                    }
                                    else
                                    {
                                        Logger.Warning("未找到确定按钮");
                                    }
                                }
                                else
                                {
                                    Logger.Warning("未找到密码输入框");
                                }
                            }
                        }
                        else
                        {
                            // 隐藏悬浮按钮
                            floatingButton.HideButton();
                            Logger.Debug("WPS 未运行，隐藏悬浮按钮");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"监控线程错误: {ex.Message}");
                    }

                    Thread.Sleep(1000); // 1秒扫描一次
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
    }
}
