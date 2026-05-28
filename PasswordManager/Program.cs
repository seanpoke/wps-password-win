using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Collections.Concurrent;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using PasswordManager.Monitor;
using PasswordManager.Business;
using PasswordManager.UI;
using PasswordManager.Utils;
using PasswordManager.Services.Request;
using PasswordManager.Services.Routing;
using PasswordManager.Services.Report;
using PasswordManager.Filler;
using PasswordManager.Locator;

#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8605, CS8618, CS8625

namespace PasswordManager
{
    internal static class Program
    {
        // Win32 API 定义
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetFocus();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetParent(IntPtr hWnd);

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

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        private static readonly BlockingCollection<string> _filePathQueue = new BlockingCollection<string>();

        public static BlockingCollection<string> GetFilePathQueue()
        {
            return _filePathQueue;
        }
        private static Thread _filePathConsumerThread;
        private static volatile bool _isConsumerRunning = false;
        private static volatile string _lastPostedFilePath = null;

        // 常量定义
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;

        // 结构体定义
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

        // 监控文档关闭事件的线程
        private static Thread documentCloseMonitorThread;

        [STAThread]
        static void Main()
        {
            DpiHelper.InitializeDpiAwareness();

            if (!IsRunningAsAdmin())
            {
                RestartAsAdmin();
                return;
            }

            MainAsync().GetAwaiter().GetResult();
        }

        private static bool IsRunningAsAdmin()
        {
            using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
            {
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
        }

        private static void RestartAsAdmin()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = Process.GetCurrentProcess().MainModule.FileName;
            startInfo.Arguments = string.Join(" ", Environment.GetCommandLineArgs().Skip(1));
            startInfo.Verb = "runas";
            startInfo.UseShellExecute = true;

            try
            {
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"无法以管理员权限启动程序: {ex.Message}\n\n请右键点击程序图标并选择\"以管理员身份运行\"",
                    "权限不足",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        
        static async Task MainAsync()
        {
            // WPS进程检测阶段：检查是否有WPS相关进程正在运行
            if (IsWpsProcessRunning())
            {
                // 检测到WPS进程，显示模态对话框
                MessageBox.Show(
                    "请先关闭wps应用，再启动本程序",
                    "提示",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                
                // 立即终止程序，确保不残留任何后台进程
                Environment.Exit(0);
                return;
            }

            // 检查进程唯一性
            bool isNewInstance;
            using (System.Threading.Mutex mutex = new System.Threading.Mutex(true, "PasswordManagerMutex", out isNewInstance))
            {
                if (!isNewInstance)
                {
                    // 已存在实例，显示提示信息
                    System.Windows.Forms.MessageBox.Show(
                        "密码管理插件已在运行中",
                        "提示",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Logger.Info("密码管理插件启动");

                // 先初始化系统托盘图标
                TrayIcon trayIcon = new TrayIcon();
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
                    try
                    {
                        System.Diagnostics.Process.Start("explorer.exe", Environment.CurrentDirectory);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"打开文件夹失败: {ex.Message}");
                    }
                };
                trayIcon.ShowLogClicked += (sender, e) =>
                {
                    Logger.Info("用户点击显示日志");
                    PasswordManager.UI.LogForm.ShowLogWindow();
                };

                // 创建登录窗口
                PasswordManager.UI.LoginForm loginForm = new PasswordManager.UI.LoginForm();
                
                if (PasswordManager.UI.LoginForm.IsLoggedIn())
                {
                    // 已登录状态，弹出注销页面并验证token
                    Logger.Info("检测到登录状态，显示注销页面并验证token");
                    
                    // 显示窗口并设置加载状态
                    loginForm.Show();
                    loginForm.SetLoading(true);
                    
                    // 异步执行心跳检测，不阻塞UI线程
                    Task.Run(async () => {
                        try
                        {
                            // 检查服务器配置
                            string serverAddress = GlobalState.Instance.GetServerAddress();
                            
                            // 执行心跳检测
                            Logger.Info("执行心跳检测");
                            bool result = await PasswordManager.UI.LoginForm.Heartbeat();
                            Logger.Info($"心跳检测完成，结果: {result}");
                            
                            // 心跳检测完成，更新UI状态
                            loginForm.Invoke((Action)(() => {
                                if (result)
                                {
                                    // 心跳成功，更新界面并关闭窗口
                                    loginForm.SetLoading(false);
                                    loginForm.UpdateUIState();
                                    string username = PasswordManager.UI.LoginForm.GetUsername();
                                    Logger.Info($"用户 {username} 已登录，心跳检测成功");
                                    // 等待用户查看后关闭窗口
                                    Task.Delay(1000).ContinueWith(_ => {
                                        loginForm.Invoke((Action)(() => {
                                            loginForm.Close();
                                        }));
                                    });
                                    GlobalState.Instance.IsLoggedIn = true;
                                }
                                else
                                {
                                    // token无效或服务器未配置，需要重新登录
                                    Logger.Info("需要重新登录");
                                    // 停止程序检测
                                    GlobalState.Instance.IsLoggedIn = false;
                                    Logger.Info("程序检测机制已停止");
                                    // 重置登录状态（不调用Logout，避免再次尝试请求接口）
                                    GlobalState.Instance.Reset();
                                    GlobalState.Instance.ClearUserInfo();
                                    Logger.Info("登录状态已重置");
                                    loginForm.SetLoading(false);
                                    loginForm.UpdateUIState();
                                    Logger.Info("UI状态已更新，准备显示登录窗口");
                                    // 显示登录窗口
                                    Logger.Info("显示登录窗口");
                                    if (loginForm.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                                    {
                                        Logger.Info("用户登录成功");
                                        GlobalState.Instance.IsLoggedIn = true;
                                    }
                                    else
                                    {
                                        Logger.Info("用户取消登录，退出程序");
                                        Application.Exit();
                                    }
                                }
                            }));
                        }
                        catch (InvalidOperationException ex) when (ex.Message == "服务器IP和端口未设置")
                        {
                            // 服务器配置未设置，直接需要重新登录
                            Logger.Info("服务器配置未设置，需要重新登录");
                            loginForm.Invoke((Action)(() => {
                                // 停止程序检测
                                GlobalState.Instance.IsLoggedIn = false;
                                Logger.Info("程序检测机制已停止");
                                // 重置登录状态
                                GlobalState.Instance.Reset();
                                GlobalState.Instance.ClearUserInfo();
                                Logger.Info("登录状态已重置");
                                loginForm.SetLoading(false);
                                loginForm.UpdateUIState();
                                Logger.Info("UI状态已更新，准备显示登录窗口");
                                // 显示登录窗口
                                Logger.Info("显示登录窗口");
                                if (loginForm.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                                {
                                    Logger.Info("用户登录成功");
                                    GlobalState.Instance.IsLoggedIn = true;
                                }
                                else
                                {
                                    Logger.Info("用户取消登录，退出程序");
                                    Application.Exit();
                                }
                            }));
                        }
                        catch (Exception ex)
                        {
                            // 其他错误
                            Logger.Error($"验证登录状态时出错: {ex.Message}");
                            loginForm.Invoke((Action)(() => {
                                // 停止程序检测
                                GlobalState.Instance.IsLoggedIn = false;
                                Logger.Info("程序检测机制已停止");
                                // 重置登录状态
                                GlobalState.Instance.Reset();
                                GlobalState.Instance.ClearUserInfo();
                                Logger.Info("登录状态已重置");
                                loginForm.SetLoading(false);
                                loginForm.UpdateUIState();
                                Logger.Info("UI状态已更新，准备显示登录窗口");
                                // 显示登录窗口
                                Logger.Info("显示登录窗口");
                                if (loginForm.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                                {
                                    Logger.Info("用户登录成功");
                                    GlobalState.Instance.IsLoggedIn = true;
                                }
                                else
                                {
                                    Logger.Info("用户取消登录，退出程序");
                                    Application.Exit();
                                }
                            }));
                        }
                    });
                }
                else
                {
                    // 未登录状态，显示登录弹框
                    Logger.Info("未登录状态，显示登录弹框");
                    
                    // 检查是否有token，如果有则尝试心跳检测
                    if (!string.IsNullOrEmpty(GlobalState.Instance.Token))
                    {
                        // 显示加载状态
                        loginForm.Show();
                        loginForm.SetLoading(true);
                        
                        // 异步执行心跳检测，不阻塞UI线程
                        Task.Run(async () => {
                            try
                            {
                                // 检查服务器配置
                                string serverAddress = GlobalState.Instance.GetServerAddress();
                                
                                // 尝试心跳检测
                                bool result = await PasswordManager.UI.LoginForm.Heartbeat();
                                
                                // 心跳完成，更新UI状态
                                loginForm.Invoke((Action)(() => {
                                    if (result)
                                    {
                                        Logger.Info("心跳检测成功，用户已登录");
                                        loginForm.SetLoading(false);
                                        loginForm.UpdateUIState();
                                        GlobalState.Instance.IsLoggedIn = true;
                                    }
                                    else
                                    {
                                        Logger.Info("心跳检测失败，需要重新登录");
                                        loginForm.SetLoading(false);
                                        // 显示登录窗口
                                        if (loginForm.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                                        {
                                            Logger.Info("用户取消登录，退出程序");
                                            Application.Exit();
                                        }
                                        GlobalState.Instance.IsLoggedIn = true;
                                    }
                                }));
                            }
                            catch (InvalidOperationException ex) when (ex.Message == "服务器IP和端口未设置")
                            {
                                // 服务器配置未设置，直接需要重新登录
                                Logger.Info("服务器配置未设置，需要重新登录");
                                loginForm.Invoke((Action)(() => {
                                    // 重置登录状态
                                    GlobalState.Instance.Reset();
                                    GlobalState.Instance.ClearUserInfo();
                                    loginForm.SetLoading(false);
                                    // 显示登录窗口
                                    if (loginForm.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                                    {
                                        Logger.Info("用户取消登录，退出程序");
                                        Application.Exit();
                                    }
                                    GlobalState.Instance.IsLoggedIn = true;
                                }));
                            }
                            catch (Exception ex)
                            {
                                // 其他错误
                                Logger.Error($"验证登录状态时出错: {ex.Message}");
                                loginForm.Invoke((Action)(() => {
                                    // 重置登录状态
                                    GlobalState.Instance.Reset();
                                    GlobalState.Instance.ClearUserInfo();
                                    loginForm.SetLoading(false);
                                    // 显示登录窗口
                                    if (loginForm.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                                    {
                                        Logger.Info("用户取消登录，退出程序");
                                        Application.Exit();
                                    }
                                    GlobalState.Instance.IsLoggedIn = true;
                                }));
                            }
                        });
                    }
                    else
                    {
                        // 没有token，直接显示登录窗口
                        if (loginForm.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                        {
                            Logger.Info("用户取消登录，程序继续在后台运行");
                            // 不退出程序，继续在后台运行
                        }
                    }
                }
                
                // 如果未登录，继续运行但处于未登录状态
                if (!GlobalState.Instance.IsLoggedIn)
                {
                    Logger.Info("程序在未登录状态下继续运行");
                }

                // 调用 /config/latest-key 接口获取公钥和keyVersion
                Logger.Info("调用 /config/latest-key 接口获取公钥和keyVersion");
                Task.Run(async () =>
                {
                    try
                    {
                        var httpRequestService = RequestFactory.GetHttpRequestService();
                        var response = await httpRequestService.GetAsync<LatestKeyInfo>(ApiRoutes.ConfigLatestKey);
                        
                        if (response != null && response.data != null)
                        {
                            GlobalState.Instance.PublicKey = response.data.publicKey;
                            GlobalState.Instance.KeyVersion = response.data.keyVersion;
                            GlobalState.Instance.SaveKeyInfo();
                            Logger.Info($"获取公钥和keyVersion成功: keyVersion={response.data.keyVersion}");
                        }
                        else
                        {
                            Logger.Warning("获取公钥和keyVersion失败，使用默认值");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"调用 /config/latest-key 接口失败: {ex.Message}，使用默认值");
                    }
                });

                // 初始化各个模块
                Logger.Info("初始化各个模块");
                WpsMonitor monitor = new WpsMonitor();
                PasswordGenerator passwordGenerator = new PasswordGenerator();
                PasswordAutoFiller autoFiller = new PasswordAutoFiller();
                FloatingButton floatingButton = new FloatingButton(monitor);

                // 悬浮按钮事件
                floatingButton.GeneratePasswordClicked += (sender, e) =>
                {
                    Logger.Info("用户点击生成密码按钮");

                    string password = passwordGenerator.GeneratePassword();
                    Logger.Debug($"生成密码: {password}");

                    if (autoFiller.FillEncryptPassword(password))
                    {
                        Logger.Info("密码填充成功");
                    }
                    else
                    {
                        Logger.Warning("密码填充失败");
                    }
                };

                // 获取主线程的同步上下文
                var mainThreadSyncContext = SynchronizationContext.Current;

                // 创建并启动悬浮按钮管理器
                FloatingButtonManager buttonManager = new FloatingButtonManager(floatingButton, mainThreadSyncContext);
                buttonManager.Start();


                // 程序退出时清理资源
                Application.ApplicationExit += (sender, e) =>
                {
                    Logger.Info("应用程序退出，清理悬浮按钮管理器");
                    buttonManager.Dispose();
                };

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
                            // 检查是否处于未登录状态
                            if (!GlobalState.Instance.IsLoggedIn)
                            {
                                Logger.Info("程序检测机制处于未登录状态，每秒检查一次");
                                GlobalState.Instance.ClearAllResources();
                                Thread.Sleep(1000); // 暂停时每秒检查一次
                                continue;
                            }
                            
                            // Logger.Info("程序检测机制开始执行监控逻辑");
                            
                            long startTime = DateTime.Now.Ticks;

                            // 检查WPS是否运行
                            long checkWpsStart = DateTime.Now.Ticks;
                            bool wpsRunning = monitor.IsWpsRunning();
                            long checkWpsEnd = DateTime.Now.Ticks;
                            
                            // Logger.Info($"WPS运行状态检查结果: {wpsRunning}");
                            
                            if (wpsRunning)
                            {
                                // 如果文档权限窗口已打开，跳过所有识别行为
                                if (AuthTreeForm.IsOpen)
                                {
                                    Thread.Sleep(1000);
                                    continue;
                                }
                                
                                // 获取当前文档路径并设置文件监控
                                try
                                {
                                    // 获取主窗口文档路径
                                    string documentPath = monitor.GetDocumentPath(IntPtr.Zero);
                                    if (!string.IsNullOrEmpty(documentPath))
                                    {
                                        // 只有文档真正打开后才初始化元数据和启动文件监控
                                        if (IsDocumentOpen(documentPath))
                                        {
                                            Logger.Info($"获取到文档路径: {documentPath}");
                                            
                                            // 在文档真正打开后才初始化文件元数据
                                            TryInitializeFileMeta(documentPath);
                                            
                                            FileMonitor.StartWatchingFile(documentPath);
                                        }
                                    }
                                    else
                                    {
                                        Logger.Warning("未能获取到文档路径");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error($"设置文件监控时出错: {ex.Message}");
                                }
                                
                                // 获取当前文档路径，用于检查文件格式
                                string currentDocumentPath = monitor.GetDocumentPath(IntPtr.Zero);
                                
                                // 检查文件格式是否支持
                                bool isSupportedFormat = false;
                                if (!string.IsNullOrEmpty(currentDocumentPath))
                                {
                                    string extension = System.IO.Path.GetExtension(currentDocumentPath).ToLower();
                                    isSupportedFormat = (extension == ".docx" || extension == ".xlsx" || extension == ".pptx");
                                }
                                
                                // 检查加密对话框（仅处理支持的文件格式）
                                long findDialogStart = DateTime.Now.Ticks;
                                IntPtr encryptDialog = isSupportedFormat ? monitor.FindPasswordDialog() : IntPtr.Zero;
                                long findDialogEnd = DateTime.Now.Ticks;
                                
                                // 如果文档权限窗口打开，隐藏悬浮按钮并跳过对话框处理
                                if (AuthTreeForm.IsOpen)
                                {
                                    buttonManager.HideButton();
                                }
                                else if (encryptDialog != IntPtr.Zero)
                                {
                                    // 获取对话框标题
                                    StringBuilder dialogTitle = new StringBuilder(256);
                                    GetWindowText(encryptDialog, dialogTitle, dialogTitle.Capacity);
                                    string title = dialogTitle.ToString();

                                    Logger.Debug($"找到对话框: {encryptDialog}, 标题: {title}");

                                    // 只有在加密窗口中显示悬浮按钮，解密窗口不显示
                                    if (title == "密码加密")
                                    {
                                        Logger.Debug($"找到加密对话框: {encryptDialog}, 标题: {title}");
                                        
                                        FileMeta currentFileMeta = FileMetaFactory.Instance.GetFileMeta(currentDocumentPath);
                                        Logger.Info($"获取到文件元数据: Uid={currentFileMeta?.Uid}, WriteAuth={currentFileMeta?.WriteAuth}");
                                        
                                        // 使用按钮管理器显示悬浮按钮
                                        buttonManager.ShowButton(encryptDialog, currentFileMeta);

                                        // 如果是密码加密窗口，记录密码
                                        if (title == "密码加密")
                                        {
                                            Logger.Info("找到密码加密窗口，开始处理");

                                            // 记录窗口信息，无论是否找到密码输入框
                                            lastPasswordEncryptDialog = encryptDialog;
                                            lastDialogTitle = title;

                                            // 尝试获取文档路径并获取UID
                                            string documentPath = monitor.GetDocumentPath(IntPtr.Zero);
                                            if (!string.IsNullOrEmpty(documentPath))
                                            {
                                                // 优先从FileMetaFactory缓存获取UID
                                                FileMeta cachedFileMeta = FileMetaFactory.Instance.GetFileMeta(documentPath);
                                                string uid = cachedFileMeta?.Uid;
                                                
                                                // 如果缓存中没有，才从文件读取并生成
                                                if (string.IsNullOrEmpty(uid))
                                                {
                                                    FileMetaManager fileMetaManager = new FileMetaManager();
                                                    uid = fileMetaManager.GetDocumentUid(documentPath);
                                                    Logger.Info($"从文件读取到文档UID: {uid}");
                                                }
                                                else
                                                {
                                                    Logger.Info($"从缓存获取到文档UID: {uid}");
                                                }
                                            }

                                            // 使用 UI Automation 获取密码
                                            long getUiaPasswordStart = DateTime.Now.Ticks;
                                            string uiaPassword = GetPasswordFromDialog(encryptDialog);
                                            long getUiaPasswordEnd = DateTime.Now.Ticks;
                                            Logger.Debug($"通过UI Automation获取密码耗时: {(getUiaPasswordEnd - getUiaPasswordStart) / 10000}ms");

                                            if (!string.IsNullOrEmpty(uiaPassword))
                                            {
                                                lastPassword = uiaPassword;
                                                Logger.Info($"获取到密码: {uiaPassword}");
                                            }
                                            else
                                            {
                                                Logger.Warning("未能通过UI Automation获取密码");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Logger.Debug($"找到解密对话框: {encryptDialog}, 标题: {title}，不显示悬浮按钮");
                                        // 隐藏悬浮按钮
                                        buttonManager.HideButton();
                                    }
                                }
                                else
                                {
                                    // 隐藏悬浮按钮
                                    buttonManager.HideButton();
                                    // 重置显示状态
                                    lastShownDialog = IntPtr.Zero;

                                    // 检查是否密码加密窗口刚关闭
                                    if (lastPasswordEncryptDialog != IntPtr.Zero && lastDialogTitle == "密码加密")
                                    {
                                        Logger.Info("密码加密窗口已关闭");

                                        // 如果 lastPassword 不为空，将其存储到 FileMeta 对象中
                                        if (!string.IsNullOrEmpty(lastPassword))
                                        {
                                            try
                                            {
                                                string documentPath = monitor.GetDocumentPath(IntPtr.Zero);
                                                if (!string.IsNullOrEmpty(documentPath))
                                                {
                                                    if (FileMetaFactory.Instance.HasFileMeta(documentPath))
                                                    {
                                                        FileMetaFactory.Instance.UpdatePendingPassword(documentPath, lastPassword);
                                                        Logger.Info($"密码加密窗口关闭，已将密码存储到文件元数据: {documentPath}");
                                                    }
                                                    else
                                                    {
                                                        Logger.Warning($"文件元数据不存在，无法存储密码: {documentPath}");
                                                    }
                                                }
                                                else
                                                {
                                                    Logger.Warning("无法获取文档路径，无法存储密码");
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                Logger.Error($"存储密码到文件元数据时出错: {ex.Message}");
                                            }
                                        }

                                        // 重置记录
                                        lastPasswordEncryptDialog = IntPtr.Zero;
                                        lastPassword = string.Empty;
                                        lastDialogTitle = string.Empty;
                                        Logger.Info("重置窗口记录");
                                    }
                                }

                                // 检查解密对话框（仅处理支持的文件格式）
                                IntPtr decryptDialog = isSupportedFormat ? monitor.FindPasswordDialog() : IntPtr.Zero;
                                if (decryptDialog != IntPtr.Zero)
                                {
                                    StringBuilder dialogTitle = new StringBuilder(256);
                                    GetWindowText(decryptDialog, dialogTitle, dialogTitle.Capacity);
                                    string title = dialogTitle.ToString();

                                    if (title == "文档已加密")
                                    {
                                        Logger.Info($"找到解密对话框: {decryptDialog}, 标题: {title}");

                                        string documentPath = monitor.GetDocumentPath(decryptDialog);
                                        if (!string.IsNullOrEmpty(documentPath))
                                        {
                                            // 检查文件格式是否支持
                                            string extension = System.IO.Path.GetExtension(documentPath).ToLower();
                                            if (extension != ".docx" && extension != ".xlsx" && extension != ".pptx")
                                            {
                                                Logger.Debug($"不支持的文件格式: {extension}，跳过自动填充");
                                                continue;
                                            }

                                            if (!AutoFillAttemptManager.Instance.HasAttempted(documentPath))
                                            {
                                                AutoFillAttemptManager.Instance.MarkAttempted(documentPath);
                                                
                                                // 等待元数据初始化完成（最多等待5秒）
                                                FileMetaFactory.Instance.WaitForInit(documentPath);
                                                
                                                // 获取元数据
                                                var fileMeta = FileMetaFactory.Instance.GetFileMeta(documentPath);
                                                if (fileMeta != null && !string.IsNullOrEmpty(fileMeta.CurrentPassword))
                                                {
                                                    string password = fileMeta.CurrentPassword;
                                                    Logger.Info($"从FileMetaFactory中获取到密码,password={password}");

                                                    bool success = autoFiller.FillDecryptPassword(password);
                                                    if (success)
                                                    {
                                                        Logger.Info("解密密码自动填充成功");
                                                    }
                                                    else
                                                    {
                                                        Logger.Warning("自动填充失败");
                                                    }
                                                }
                                                else
                                                {
                                                    Logger.Warning("未找到文件元数据或密码为空");
                                                }
                                                
                                                // 等待对话框关闭，只有当对话框真正关闭时才重置尝试记录
                                                bool dialogClosed = WaitForDialogClose(decryptDialog, 2500);
                                                if (dialogClosed)
                                                {
                                                    Logger.Info("对话框已关闭，重置自动填充尝试记录");
                                                    AutoFillAttemptManager.Instance.ResetAttempt(documentPath);
                                                }
                                                else
                                                {
                                                    Logger.Warning("对话框未关闭（可能密码错误），保留自动填充尝试记录");
                                                }
                                            }
                                            else
                                            {
                                                Logger.Info($"文档 {documentPath} 已经尝试过自动填充密码，跳过");
                                            }
                                        }
                                        else
                                        {
                                            // 尝试获取文档名称
                                            string docName = monitor.GetDocumentName(decryptDialog);
                                            if (!string.IsNullOrEmpty(docName))
                                            {
                                                Logger.Warning($"未能获取到文档路径，但识别到文档名称: {docName}");
                                                
                                                // 检查是否已显示过提示
                                                if (!AutoFillAttemptManager.Instance.HasAttempted(docName))
                                                {
                                                    AutoFillAttemptManager.Instance.MarkAttempted(docName);
                                                    System.Windows.Forms.MessageBox.Show($"文件 \"{docName}\" 在本地未找到，请将文件保存到本地后再打开", "提示", 
                                                        System.Windows.Forms.MessageBoxButtons.OK, 
                                                        System.Windows.Forms.MessageBoxIcon.Warning, 
                                                        System.Windows.Forms.MessageBoxDefaultButton.Button1, 
                                                        System.Windows.Forms.MessageBoxOptions.DefaultDesktopOnly);
                                                }
                                            }
                                            else
                                            {
                                                Logger.Warning("未能获取到文档路径");
                                            }
                                        }
                                    }

                                }
                            }
                            else
                            {
                                // 隐藏悬浮按钮
                                buttonManager.HideButton();
                                Logger.Debug("WPS 未运行，隐藏悬浮按钮");

                                // 重置记录
                                lastPasswordEncryptDialog = IntPtr.Zero;
                                lastPassword = string.Empty;
                                lastDialogTitle = string.Empty;
                            }

                            long endTime = DateTime.Now.Ticks;
                            // Logger.Debug($"循环结束，总耗时: {(endTime - startTime) / 10000}ms");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"监控线程错误: {ex.Message}");
                        }

                        Thread.Sleep(500); // 500毫秒扫描一次，提高响应速度
                    }
                });

                monitorThread.IsBackground = true;
                monitorThread.SetApartmentState(ApartmentState.STA);
                monitorThread.Start();

                // 显示启动提示
                Logger.Info("插件已启动，正在监控WPS进程");
                trayIcon.ShowBalloonTip("密码管理插件", "插件已启动，正在监控WPS进程...");

                // 启动文档关闭监控线程
                documentCloseMonitorThread = new Thread(() =>
                {
                    while (true)
                    {
                        try
                        {
                            List<string> watchedFiles = FileMonitor.GetWatchedFiles();
                            
                            if (watchedFiles.Count > 0)
                            {
                                List<string> docsToStopWatching = new List<string>();

                                foreach (string documentPath in watchedFiles)
                                {
                                    // 1. 判断文件是否已关闭（使用新的检测逻辑）
                                    if (IsDocumentClosed(documentPath, enableLogging: true))
                                    {
                                        // 2. 已关闭则清理AutoFillAttemptManager中的记录
                                        AutoFillAttemptManager.Instance.OnDocumentClosed(documentPath);

                                        // 3. 从FileMetaFactory获取元数据
                                        FileMeta fileMeta = FileMetaFactory.Instance.GetFileMeta(documentPath);

                                        if (fileMeta != null)
                                        {
                                            // 5. 判断元数据是否正在执行写操作
                                            if (fileMeta.IsWriting)
                                            {
                                                Logger.Info($"文档 {documentPath} 正在写入中，跳过本次写入任务");
                                                continue;
                                            }

                                            // 6. 判断是否需要执行元数据写入
                                            bool needWriteMetadata = false;
                                            bool isUidOnlyWrite = false;

                                            // 触发条件1：元数据已修改
                                            if (fileMeta.IsModify)
                                            {
                                                needWriteMetadata = true;
                                                Logger.Info($"文档 {documentPath} 的元数据已修改，需要执行元数据写入");
                                            }
                                            else
                                            {
                                                // 触发条件2：元数据未修改，但检测文档尾部是否存在UID元数据
                                                FileMetaManager checkManager = new FileMetaManager();
                                                bool hasUidMeta = checkManager.HasUidMetadata(documentPath);
                                                
                                                if (!hasUidMeta)
                                                {
                                                    needWriteMetadata = true;
                                                    isUidOnlyWrite = true;
                                                    Logger.Info($"文档 {documentPath} 的元数据未修改，但文档尾部不存在UID元数据，需要执行UID写入");
                                                }
                                                else
                                                {
                                                    Logger.Info($"文档 {documentPath} 的元数据未修改且已存在UID元数据，无需执行写入");
                                                }
                                            }

                                            if (needWriteMetadata)
                                            {
                                                // 7. 更新元数据的写变量（volatile保证可见性）
                                                fileMeta.IsWriting = true;

                                                string pathCopy = documentPath;

                                                string beforePassword = fileMeta.CurrentPassword;
                                                string writePassword = isUidOnlyWrite ? null : FileMetaFactory.Instance.GetWritePassword(pathCopy);
                                                SortedSet<string> pendingPasswords = isUidOnlyWrite ? null : (fileMeta.PendingPasswordList != null ? new SortedSet<string>(fileMeta.PendingPasswordList) : null);
                                                string fileUid = fileMeta.Uid;
                                                string keyVersion = isUidOnlyWrite ? null : GlobalState.Instance.KeyVersion;

                                                FileMeta asyncFileMeta = new FileMeta(pathCopy)
                                                {
                                                    CurrentPassword = isUidOnlyWrite ? null : beforePassword,
                                                    AfterPassword = writePassword,
                                                    Uid = fileUid,
                                                    CurrentKeyVersion = keyVersion,
                                                    PendingPasswordList = pendingPasswords,
                                                };

                                                Logger.Info($"捕获信息用于写入: isUidOnlyWrite={isUidOnlyWrite}, beforePassword={(string.IsNullOrEmpty(asyncFileMeta.CurrentPassword) ? "空" : "有值")}, afterPassword={(string.IsNullOrEmpty(asyncFileMeta.AfterPassword) ? "空" : "有值")}, fileUid={asyncFileMeta.Uid}");

                                                // 8. 执行写元数据的异步操作
                                                Task.Run(async () =>
                                                {
                                                    try
                                                    {
                                                        Logger.Info($"等待1秒，确保文件 {pathCopy} 已完全关闭...");
                                                        await Task.Delay(1000);

                                                        FileMetaManager fileMetaManager = new FileMetaManager();
                                                        bool success = fileMetaManager.WriteMetaDataToFile(asyncFileMeta);
                                                        if (success)
                                                        {
                                                            Logger.Info($"文档 {pathCopy} 的元数据写入成功");
                                                        }
                                                        else
                                                        {
                                                            Logger.Error($"文档 {pathCopy} 的元数据写入失败");
                                                        }

                                                        // 仅在非UID-only写入场景下才上报密码信息
                                                        if (!isUidOnlyWrite)
                                                        {
                                                            var reportService = PasswordReportService.Instance;

                                                            bool hasPasswordInfo = !string.IsNullOrEmpty(asyncFileMeta.CurrentPassword) ||
                                                                                    !string.IsNullOrEmpty(asyncFileMeta.AfterPassword) ||
                                                                                    (asyncFileMeta.PendingPasswordList != null && asyncFileMeta.PendingPasswordList.Count > 0);

                                                            if (hasPasswordInfo)
                                                            {
                                                                bool reportSuccess = await reportService.ReportSaveLogWithPasswords(asyncFileMeta);

                                                                if (reportSuccess)
                                                                {
                                                                    Logger.Info($"文档 {pathCopy} 的密码保存记录上报成功");
                                                                }
                                                                else
                                                                {
                                                                    Logger.Warning($"文档 {pathCopy} 的密码保存记录上报失败");
                                                                }
                                                            }
                                                            else
                                                            {
                                                                Logger.Info($"文档 {pathCopy} 无密码信息，跳过保存记录上报");
                                                            }
                                                        }
                                                        else
                                                        {
                                                            Logger.Info($"文档 {pathCopy} 为仅写入UID场景，跳过密码信息上报");
                                                        }

                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        Logger.Error($"异步写入文档 {pathCopy} 元数据时发生异常: {ex.Message}");
                                                    }
                                                });
                                            }
                                        }
                                        // 清理元数据
                                        FileMetaFactory.Instance.CleanupFileMeta(documentPath);
                                        Logger.Info($"已从FileMetaFactory中移除文档: {documentPath}");
                                        // 标记需要停止监听
                                        docsToStopWatching.Add(documentPath);
                                    }
                                }

                                // 停止监听已关闭的文件
                                foreach (string documentPath in docsToStopWatching)
                                {
                                    FileMonitor.StopWatchingFile(documentPath);
                                }
                            }


                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"文档关闭监控线程错误: {ex.Message}");
                        }

                        Thread.Sleep(500); // 500ms检查一次
                    }
                });

                documentCloseMonitorThread.IsBackground = true;
                documentCloseMonitorThread.Start();
                Logger.Info("文档关闭监控线程已启动");
                
                // 启动心跳检测线程
                Thread heartbeatThread = new Thread(async () =>
                {
                    while (true)
                    {
                        try
                        {
                            // 只有在登录状态下才进行心跳检测
                            if (GlobalState.Instance.IsLoggedIn)
                            {
                                // 调用心跳接口
                                await PasswordManager.UI.LoginForm.Heartbeat();
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"心跳检测线程错误: {ex.Message}");
                        }
                        
                        // 每30分钟进行一次心跳检测
                        Thread.Sleep(1800000);
                    }
                });
                
                heartbeatThread.IsBackground = true;
                heartbeatThread.Start();
                Logger.Info("心跳检测线程已启动");

                // 启动内核文件监视器线程
                Thread kernelMonitorThread = new Thread(() =>
                {
                    StartKernelFileListening();
                });
                kernelMonitorThread.IsBackground = true;
                kernelMonitorThread.Start();
                Logger.Info("内核文件监视器线程已启动");

                StartFilePathConsumer();

                // 运行应用程序
                Application.Run();
            }
        }

        // 检查WPS进程是否正在运行（仅检测有可见窗口的进程）
        private static bool IsWpsProcessRunning()
        {
            try
            {
                Process[] processes = Process.GetProcessesByName("wps");
                foreach (Process process in processes)
                {
                    try
                    {
                        // 检查进程是否有主窗口且窗口可见
                        if (process.MainWindowHandle != IntPtr.Zero && IsWindowVisible(process.MainWindowHandle))
                        {
                            Logger.Info("检测到WPS窗口进程正在运行");
                            return true;
                        }
                    }
                    catch
                    {
                        // 进程可能已退出，忽略异常
                    }
                }
                
                Logger.Info("未检测到WPS窗口进程");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"检测WPS进程时发生错误: {ex.Message}");
                return false;
            }
        }

        // 检查窗口是否可见
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        // 隐藏控制台窗口
        

        // 检查文档是否打开（保留原有逻辑，简单的文件锁定检测）
        private static bool IsDocumentOpen(string documentPath, bool enableLogging = false)
        {
            if (string.IsNullOrEmpty(documentPath) || !System.IO.File.Exists(documentPath))
            {
                if (enableLogging)
                {
                    Logger.Info($"检测文档打开: 文档路径不存在 {documentPath}");
                }
                return false;
            }

            try
            {
                using (System.IO.FileStream fs = System.IO.File.Open(documentPath, System.IO.FileMode.Open, System.IO.FileAccess.ReadWrite, System.IO.FileShare.None))
                {
                    if (enableLogging)
                    {
                        Logger.Info($"检测文档打开: {documentPath} 未被其他进程锁定");
                    }
                    return false;
                }
            }
            catch (System.IO.IOException)
            {
                return true;
            }
            catch (Exception ex)
            {
                if (enableLogging)
                {
                    Logger.Error($"检测文档打开: {documentPath} 错误信息: {ex.Message}，异常类型: {ex.GetType().Name}");
                }
                return false;
            }
        }

        // 检查文档是否已关闭（新的检测逻辑，包含多重验证）
        private static bool IsDocumentClosed(string documentPath, bool enableLogging = false)
        {
            if (string.IsNullOrEmpty(documentPath) || !System.IO.File.Exists(documentPath))
            {
                if (enableLogging)
                {
                    Logger.Info($"检测文档关闭触发: 文档路径不存在 {documentPath}");
                }
                return true;
            }

            if (HasWpsTempFile(documentPath, enableLogging))
            {
                return false;
            }

            if (!IsWpsProcessRunning())
            {
                if (enableLogging)
                {
                    Logger.Info($"检测文档关闭触发: {documentPath} WPS进程已退出");
                }
                return true;
            }

            const int checkCount = 3;
            const int checkIntervalMs = 200;

            int unlockedCount = 0;

            for (int i = 0; i < checkCount; i++)
            {
                try
                {
                    using (System.IO.FileStream fs = System.IO.File.Open(documentPath, System.IO.FileMode.Open, System.IO.FileAccess.ReadWrite, System.IO.FileShare.None))
                    {
                        unlockedCount++;
                        if (enableLogging)
                        {
                            Logger.Info($"检测文档关闭触发: {documentPath} 第{i + 1}次检测未被锁定");
                        }
                    }
                }
                catch (System.IO.IOException)
                {
                    if (enableLogging)
                    {
                        Logger.Info($"检测文档关闭触发: {documentPath} 第{i + 1}次检测被锁定");
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    if (enableLogging)
                    {
                        Logger.Error($"检测文档关闭触发: {documentPath} 第{i + 1}次检测错误: {ex.Message}");
                    }
                    return true;
                }

                if (i < checkCount - 1)
                {
                    System.Threading.Thread.Sleep(checkIntervalMs);
                }
            }

            if (enableLogging)
            {
                Logger.Info($"检测文档关闭触发: {documentPath} 连续{checkCount}次检测未被锁定，且无临时文件，判定为已关闭");
            }

            return true;
        }

        // 检查是否存在WPS临时文件（仅检测 ~$ 开头的临时文件）
        private static bool HasWpsTempFile(string documentPath, bool enableLogging = false)
        {
            string directory = System.IO.Path.GetDirectoryName(documentPath);
            string fileName = System.IO.Path.GetFileName(documentPath);

            // 临时文件命名模式：最多去掉两个字符
            // 例如：原文件 123456789.docx，临时文件可能是 ~$123456789.docx、~$23456789.docx、~$3456789.docx
            string[] tempFilePatterns = new string[]
            {
                // 模式1: ~$ + 完整文件名（标准WPS临时文件）
                "~$" + fileName,
                // 模式2: ~$ + 文件名去掉第一个字符
                fileName.Length > 1 ? "~$" + fileName.Substring(1) : null,
                // 模式3: ~$ + 文件名去掉前两个字符
                fileName.Length > 2 ? "~$" + fileName.Substring(2) : null
            };

            foreach (string pattern in tempFilePatterns)
            {
                if (string.IsNullOrEmpty(pattern))
                {
                    continue;
                }

                string tempFilePath = System.IO.Path.Combine(directory, pattern);
                if (System.IO.File.Exists(tempFilePath))
                {
                    if (enableLogging)
                    {
                        Logger.Info($"检测文档关闭触发: {documentPath} 存在临时文件 {tempFilePath}");
                    }
                    return true;
                }
            }

            if (enableLogging)
            {
                Logger.Info($"检测文档关闭触发: {documentPath} 未找到匹配的临时文件");
            }
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

                try
                {
                    System.Reflection.PropertyInfo currentProperty = passwordEdit.GetType().GetProperty("Current");
                    if (currentProperty == null)
                        return false;

                    object current = currentProperty.GetValue(passwordEdit);
                    if (current == null)
                        return false;

                    System.Reflection.PropertyInfo boundingRectangleProperty = current.GetType().GetProperty("BoundingRectangle");
                    if (boundingRectangleProperty == null)
                        return false;

                    object boundingRectangle = boundingRectangleProperty.GetValue(current);
                    if (boundingRectangle == null)
                        return false;

                    System.Reflection.PropertyInfo leftProperty = boundingRectangle.GetType().GetProperty("Left");
                    System.Reflection.PropertyInfo topProperty = boundingRectangle.GetType().GetProperty("Top");
                    System.Reflection.PropertyInfo rightProperty = boundingRectangle.GetType().GetProperty("Right");
                    System.Reflection.PropertyInfo bottomProperty = boundingRectangle.GetType().GetProperty("Bottom");

                    if (leftProperty == null || topProperty == null || rightProperty == null || bottomProperty == null)
                        return false;

                    int left = Convert.ToInt32(leftProperty.GetValue(boundingRectangle));
                    int top = Convert.ToInt32(topProperty.GetValue(boundingRectangle));
                    int right = Convert.ToInt32(rightProperty.GetValue(boundingRectangle));
                    int bottom = Convert.ToInt32(bottomProperty.GetValue(boundingRectangle));

                    int eyeButtonX = right - 25;
                    int eyeButtonY = (top + bottom) / 2;

                    Logger.Debug($"计算小眼睛按钮位置: ({eyeButtonX}, {eyeButtonY})");

                    POINT point = new POINT { X = eyeButtonX, Y = eyeButtonY };
                    SimulateMouseClick(point);
                    Logger.Info("通过位置估算点击小眼睛按钮区域");

                    Thread.Sleep(100);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error($"尝试点击小眼睛按钮时出错: {ex.Message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"尝试显示密码时出错: {ex.Message}");
                return false;
            }
        }

        // 尝试初始化文件元数据（如果尚未初始化）
        private static void TryInitializeFileMeta(string documentPath)
        {
            if (string.IsNullOrEmpty(documentPath))
            {
                return;
            }

            // 检查文件元数据是否已存在
            if (FileMetaFactory.Instance.HasFileMeta(documentPath))
            {
                return;
            }

            Logger.Info($"开始初始化文件元数据: {documentPath}");

            try
            {
                // 初始化文件元数据
                FileMetaManager fileMetaManager = new FileMetaManager();
                
                // 读取文件尾部的uid信息
                string uid = fileMetaManager.ReadUidFromFile(documentPath);
                // 如果uid不存在，创建新的uid
                if (string.IsNullOrEmpty(uid))
                {
                    uid = FileMetaFactory.Instance.CreateUid();
                }
                
                // 调用 /doc/owner 接口获取文档信息
                string ownerAccount = null;
                string ownerName = null;
                bool readAuth = false;
                bool writeAuth = false;
                
                try
                {
                    if (GlobalState.Instance.IsLoggedIn && !string.IsNullOrEmpty(GlobalState.Instance.Token))
                    {
                        var httpRequestService = RequestFactory.GetHttpRequestService();
                        string fileName = System.IO.Path.GetFileName(documentPath);
                        var requestData = new { docId = uid, fileName = fileName };
                        var response = httpRequestService.PostAsync<DocOwnerInfo>(ApiRoutes.DocOwner, requestData, GlobalState.Instance.Token).GetAwaiter().GetResult();
                        
                        if (response != null && response.data != null)
                        {
                            ownerAccount = response.data.ownerAccount;
                            ownerName = response.data.ownerName;
                            readAuth = response.data.readAuth;
                            writeAuth = response.data.writeAuth;
                            Logger.Info($"获取文档信息成功: 所有者={ownerName}({ownerAccount}), 读权限={readAuth}, 写权限={writeAuth}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"获取文档信息失败: {ex.Message}");
                }
                
                // 读取文件尾部的密码信息
                string password = fileMetaManager.ReadPasswordFromFile(documentPath);
                
                // 读取文件尾部的keyVersion信息
                string keyVersion = fileMetaManager.ReadKeyVersionFromFile(documentPath);
                if (string.IsNullOrEmpty(keyVersion))
                {
                    keyVersion = "default";
                }
                
                // 如果有读权限且密码不为空，调用 /doc/password 接口获取解密后的密码
                if ((readAuth || writeAuth) && !string.IsNullOrEmpty(password))
                {
                    try
                    {
                        if (GlobalState.Instance.IsLoggedIn && !string.IsNullOrEmpty(GlobalState.Instance.Token))
                        {
                            var httpRequestService = RequestFactory.GetHttpRequestService();
                            var requestData = new { docId = uid, encryPassword = password, keyVersion = keyVersion };
                            var response = httpRequestService.PostAsync<DocPasswordInfo>(ApiRoutes.DocPassword, requestData, GlobalState.Instance.Token).GetAwaiter().GetResult();
                            
                            if (response != null && response.status == 200 && response.data != null && !string.IsNullOrEmpty(response.data.password))
                            {
                                password = response.data.password;
                                Logger.Info($"获取解密后的密码成功");
                            }
                            else
                            {
                                password = null;
                                string errorMsg = response?.message ?? "未知错误";
                                Logger.Warning($"获取解密后的密码失败: {errorMsg}");
                                System.Windows.Forms.MessageBox.Show($"填充密码失败：{errorMsg}", "提示", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning, System.Windows.Forms.MessageBoxDefaultButton.Button1, System.Windows.Forms.MessageBoxOptions.DefaultDesktopOnly);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        password = null;
                        string errorMsg = ex.Message;
                        Logger.Error($"获取解密后的密码失败: {errorMsg}");
                        System.Windows.Forms.MessageBox.Show($"填充密码失败：{errorMsg}", "提示", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning, System.Windows.Forms.MessageBoxDefaultButton.Button1, System.Windows.Forms.MessageBoxOptions.DefaultDesktopOnly);
                    }
                }

                // 创建并初始化FileMeta对象
                FileMeta fileMeta = new FileMeta(documentPath)
                {
                    Uid = uid,
                    CurrentPassword = password,
                    OwnerAccount = ownerAccount,
                    OwnerName = ownerName,
                    ReadAuth = readAuth,
                    WriteAuth = writeAuth,
                    CurrentKeyVersion = keyVersion
                };
                
                // 添加到FileMetaFactory
                FileMetaFactory.Instance.AddFileMeta(fileMeta);
                // 发出元数据初始化完成的信号
                FileMetaFactory.Instance.SignalInitComplete(documentPath);
                Logger.Info($"文件元数据初始化完成: {documentPath}");
            }
            catch (Exception ex)
            {
                Logger.Error($"文件元数据初始化失败: {ex.Message}");
            }
        }

        // 从密码对话框获取密码（仅使用UI Automation）
        public static string GetPasswordFromDialog(IntPtr dialogHandle)
        {
            try
            {
                Logger.Debug($"[GetPasswordFromDialog] 开始尝试从密码对话框获取密码，对话框句柄: {dialogHandle}");

                if (dialogHandle == IntPtr.Zero)
                {
                    Logger.Error("[GetPasswordFromDialog] 对话框句柄为空");
                    return string.Empty;
                }

                if (!IsWindow(dialogHandle))
                {
                    Logger.Error("[GetPasswordFromDialog] 对话框句柄无效或窗口已关闭");
                    return string.Empty;
                }

                string password = GetPasswordUsingUIAutomation(dialogHandle);
                if (!string.IsNullOrEmpty(password))
                {
                    return password;
                }

                Logger.Warning("[GetPasswordFromDialog] UI Automation获取密码失败");
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

                                        // 检查是否为Qt密码输入框
                                        System.Reflection.PropertyInfo classNameProperty = current.GetType().GetProperty("ClassName");
                                        if (classNameProperty != null)
                                        {
                                            string className = (string)classNameProperty.GetValue(current);
                                            Logger.Debug($"编辑控件 #{i} 类名: {className}");

                                            // 特别处理Qt密码输入框
                                            if (className.Contains("KDPwdLineEditReveal"))
                                            {

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
                    }
                    else
                    {
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
                    }
                    else
                    {
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
                    object passwordCondition = Activator.CreateInstance(propertyConditionType, new object[] { nameProperty, label });
                    if (passwordCondition == null)
                    {
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

        // 获取窗口文本
        private static string GetWindowText(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                Logger.Debug("GetWindowText: 句柄为空");
                return string.Empty;
            }

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
            SendMessage(hWnd, WM_GETTEXT, (IntPtr)256, sb2);
            return sb2.ToString();
        }

        // SendMessage重载，用于获取文本
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, StringBuilder lParam);

        // 等待对话框关闭
        private static bool WaitForDialogClose(IntPtr dialogHandle, int maxWaitMs)
        {
            int waitStep = 500;
            int totalWaited = 0;

            while (totalWaited < maxWaitMs)
            {
                Thread.Sleep(waitStep);
                totalWaited += waitStep;

                if (!IsWindow(dialogHandle))
                {
                    return true;
                }
            }

            return false;
        }

        // 检查是否为密码错误对话框
        private static bool IsPasswordErrorDialog(IntPtr dialogHandle)
        {
            StringBuilder text = new StringBuilder(1024);
            GetWindowText(dialogHandle, text, text.Capacity);

            string dialogText = text.ToString();
            return dialogText.Contains("密码错误") ||
                   dialogText.Contains("错误") ||
                   dialogText.Contains("不正确") ||
                   dialogText.Contains("失败");
        }

        // 启动内核文件监视器
        private static void StartKernelFileListening()
        {
            const string SessionName = "WpsKernelFileMonitorSession";

            try
            {
                Logger.Info("【内核级拦截】正在启动 Windows 内核文件监视器...");

                using (var session = new TraceEventSession(SessionName, null))
                {
                    session.StopOnDispose = true;

                    session.EnableKernelProvider(KernelTraceEventParser.Keywords.FileIOInit | KernelTraceEventParser.Keywords.FileIO);

                    session.Source.Kernel.All += (TraceEvent data) =>
                        {
                            try
                            {
                                string processName = data.ProcessName;
                                string eventName = data.EventName;
                                
                                if (processName != null && (processName.Equals("wps", StringComparison.OrdinalIgnoreCase) || 
                                                           processName.Equals("wps.exe", StringComparison.OrdinalIgnoreCase)))
                                {
                                    string filePath = null;
                                    string fileNamePayload = data.PayloadByName("FileName")?.ToString();
                                    string pathPayload = data.PayloadByName("Path")?.ToString();
                                    
                                    if (!string.IsNullOrEmpty(fileNamePayload))
                                    {
                                        filePath = fileNamePayload;
                                    }
                                    else if (!string.IsNullOrEmpty(pathPayload))
                                    {
                                        filePath = pathPayload;
                                    }
                                    
                                    if (!string.IsNullOrEmpty(filePath))
                                    {
                                        bool isTargetExt = IsTargetExtension(filePath);
                                        bool isOpenOp = IsFileOpenOperation(eventName);
                                        
                                        if (isTargetExt && isOpenOp)
                                        {
                                            string normalPath = ConvertDevicePathToDriveLetter(filePath);
                                            
                                            if (!string.Equals(_lastPostedFilePath, normalPath, StringComparison.OrdinalIgnoreCase))
                                            {
                                                _lastPostedFilePath = normalPath;
                                                Console.WriteLine($"[打开文件] {normalPath}");
                                                _filePathQueue.Add(normalPath);
                                                Logger.Info($"[文件识别] WPS打开文档: {normalPath}");
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"内核事件处理异常: {ex.Message}");
                            }
                        };

                    session.Source.Process();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"内核文件监视器异常: {ex.Message}");
                Logger.Error($"异常堆栈: {ex.StackTrace}");
            }
        }

        private static bool IsFileOpenOperation(string eventName)
        {
            if (string.IsNullOrEmpty(eventName))
                return false;
            
            return eventName.IndexOf("Create", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   eventName.IndexOf("Open", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsTargetExtension(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path).ToLower();
            return ext == ".docx" || ext == ".doc" || ext == ".xlsx" || ext == ".xls" || ext == ".pptx" || ext == ".ppt";
        }

        private static void StartFilePathConsumer()
        {
            _isConsumerRunning = true;
            _filePathConsumerThread = new Thread(FileConsumerWorker)
            {
                IsBackground = true,
                Name = "FilePathConsumerThread"
            };
            _filePathConsumerThread.Start();
            Logger.Info("文件识别消费者线程已启动");
        }

        private static void FileConsumerWorker()
        {
            try
            {
                while (_isConsumerRunning)
                {
                    string filePath = _filePathQueue.Take();
                    
                    if (string.IsNullOrEmpty(filePath))
                    {
                        continue;
                    }

                    string ext = Path.GetExtension(filePath).ToLower();

                    if (ext != ".docx" && ext != ".xlsx" && ext != ".pptx")
                    {
                        continue;
                    }

                    GlobalState.Instance.AddPossiblePath(filePath);
                    Logger.Info($"[文件识别成功] 文档路径已投递到possiblePaths: {filePath}");
                }
            }
            catch (InvalidOperationException)
            {
                Logger.Info("文件路径队列已关闭，消费者线程退出");
            }
            catch (Exception ex)
            {
                Logger.Error($"文件消费者工作线程异常: {ex.Message}");
            }
        }

        private static void StopFilePathConsumer()
        {
            _isConsumerRunning = false;
            _filePathQueue.CompleteAdding();
            
            if (_filePathConsumerThread != null && _filePathConsumerThread.IsAlive)
            {
                _filePathConsumerThread.Join(5000);
            }
            
            Logger.Info("文件识别消费者线程已停止");
        }

        private static string ConvertDevicePathToDriveLetter(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath)) return devicePath;

            if (devicePath.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase))
            {
                return devicePath;
            }

            if (devicePath.StartsWith(@"\Device\HarddiskVolume", StringComparison.OrdinalIgnoreCase))
            {
                string volumeNumber = devicePath.Substring(@"\\Device\HarddiskVolume".Length);
                int volumeIndex;
                if (int.TryParse(volumeNumber, out volumeIndex))
                {
                    for (char drive = 'A'; drive <= 'Z'; drive++)
                    {
                        string drivePath = $"{drive}:\\";
                        try
                        {
                            string deviceName = QueryDosDevice(drivePath);
                            if (deviceName != null && devicePath.StartsWith(deviceName.TrimEnd('\0')))
                            {
                                return devicePath.Replace(deviceName.TrimEnd('\0'), drivePath);
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }

            return devicePath;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern uint QueryDosDevice(string lpDeviceName, StringBuilder lpTargetPath, uint ucchMax);

        private static string QueryDosDevice(string drivePath)
        {
            StringBuilder sb = new StringBuilder(512);
            uint result = QueryDosDevice(drivePath, sb, (uint)sb.Capacity);
            if (result == 0)
            {
                return null;
            }
            return sb.ToString();
        }
    }
}
