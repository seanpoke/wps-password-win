using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PasswordManager.Utils;

namespace PasswordManager.UI
{
    public class TrayIcon
    {
        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _contextMenu;
        private LoginForm _loginForm;
        private MetaQueryForm _metaQueryForm;

        public event EventHandler ExitClicked;
        public event EventHandler OpenFolderClicked;
        public event EventHandler ShowLogClicked;

        public void Initialize()
        {
            try
            {
                _notifyIcon = new NotifyIcon
                {
                    Icon = GetAppIcon(),
                    Text = "密码管理插件",
                    Visible = true
                };
                System.Diagnostics.Debug.WriteLine("托盘图标初始化成功");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"托盘图标初始化失败: {ex.Message}");
                throw;
            }

            _contextMenu = new ContextMenuStrip();

            ToolStripMenuItem homeItem = new ToolStripMenuItem("主页");
            homeItem.Click += (sender, e) => ShowMainWindow();

            ToolStripMenuItem showLogItem = new ToolStripMenuItem("显示日志");
            showLogItem.Click += (sender, e) => ShowLogClicked?.Invoke(this, EventArgs.Empty);

            ToolStripMenuItem openFolderItem = new ToolStripMenuItem("打开安装目录");
            openFolderItem.Click += (sender, e) => OpenFolderClicked?.Invoke(this, EventArgs.Empty);

            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += (sender, e) => ExitClicked?.Invoke(this, EventArgs.Empty);

            _contextMenu.Items.Add(homeItem);
            _contextMenu.Items.Add(showLogItem);
            
            if (IsAdminUser())
            {
                ToolStripMenuItem queryMetaItem = new ToolStripMenuItem("查询元数据");
                queryMetaItem.Click += (sender, e) => ShowMetaQueryWindow();
                _contextMenu.Items.Add(queryMetaItem);
            }
            
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(openFolderItem);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(exitItem);

            _notifyIcon.MouseUp += (sender, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    Form hiddenForm = new Form
                    {
                        Size = new System.Drawing.Size(1, 1),
                        ShowInTaskbar = false,
                        FormBorderStyle = FormBorderStyle.None,
                        Opacity = 0,
                        TopMost = true
                    };
                    hiddenForm.Show();
                    SetForegroundWindow(hiddenForm.Handle);

                    _contextMenu.Closed += (s, args) =>
                    {
                        hiddenForm.Close();
                        hiddenForm.Dispose();
                    };

                    _contextMenu.Show(Cursor.Position);
                }
            };

            _notifyIcon.DoubleClick += (sender, e) =>
            {
                ShowMainWindow();
            };
        }

        private bool IsAdminUser()
        {
            try
            {
                string role = PasswordManager.Utils.GlobalState.Instance.Role;
                return !string.IsNullOrEmpty(role) && role.Equals("admin", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private void ShowMetaQueryWindow()
        {
            MetaQueryForm existingForm = Application.OpenForms.OfType<MetaQueryForm>().FirstOrDefault();
            if (existingForm != null && !existingForm.IsDisposed)
            {
                existingForm.Activate();
                if (existingForm.WindowState == FormWindowState.Minimized)
                {
                    existingForm.WindowState = FormWindowState.Normal;
                }
            }
            else
            {
                _metaQueryForm = new MetaQueryForm();
                _metaQueryForm.StartPosition = FormStartPosition.CenterScreen;
                _metaQueryForm.FormClosed += (sender, e) =>
                {
                    _metaQueryForm = null;
                };
                _metaQueryForm.Show();
            }
        }

        private System.Drawing.Icon GetAppIcon()
        {
            try
            {
                return System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
            }
            catch
            {
                return System.Drawing.SystemIcons.Application;
            }
        }

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);
        
        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;

        private void ShowMainWindow()
        {
            try
            {
                LoginForm existingForm = null;
                
                // 首先尝试通过 _loginForm 引用查找
                if (_loginForm != null && !_loginForm.IsDisposed)
                {
                    existingForm = _loginForm;
                }
                
                // 如果 _loginForm 无效，尝试从 Application.OpenForms 查找
                if (existingForm == null || existingForm.IsDisposed)
                {
                    existingForm = Application.OpenForms.OfType<LoginForm>().FirstOrDefault();
                }
                
                bool isFormValid = existingForm != null && !existingForm.IsDisposed;
                
                if (isFormValid)
                {
                    try
                    {
                        Logger.Info($"找到现有窗体，尝试激活");
                        
                        // 使用 Win32 API 确保窗体显示和激活
                        IntPtr handle = existingForm.Handle;
                        
                        // 如果窗体是最小化的，先恢复
                        if (IsIconic(handle))
                        {
                            ShowWindow(handle, SW_RESTORE);
                        }
                        
                        // 确保窗体可见
                        existingForm.Visible = true;
                        existingForm.Show();
                        
                        // 激活并置顶
                        SetForegroundWindow(handle);
                        existingForm.Activate();
                        existingForm.BringToFront();
                        
                        // 恢复正常窗口状态
                        if (existingForm.WindowState == FormWindowState.Minimized)
                        {
                            existingForm.WindowState = FormWindowState.Normal;
                        }
                        
                        Logger.Info("窗体激活成功");
                        return;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"激活现有窗口失败: {ex.Message}");
                        isFormValid = false;
                    }
                }
                
                // 清理旧窗体
                if (existingForm != null && !existingForm.IsDisposed)
                {
                    try
                    {
                        Logger.Info("清理旧窗体");
                        existingForm.Close();
                        existingForm.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"清理旧窗体失败: {ex.Message}");
                    }
                }
                
                // 创建新窗体
                Logger.Info("创建新窗体");
                _loginForm = new LoginForm();
                _loginForm.StartPosition = FormStartPosition.CenterScreen;
                _loginForm.FormClosed += (sender, e) =>
                {
                    Logger.Info("窗体已关闭");
                    _loginForm = null;
                };
                _loginForm.Show();
                _loginForm.Activate();
                Logger.Info("新窗体已显示");
            }
            catch (Exception ex)
            {
                Logger.Error($"显示主窗口失败: {ex.Message}");
                // 发生严重错误时尝试直接创建新窗口
                try
                {
                    _loginForm = new LoginForm();
                    _loginForm.StartPosition = FormStartPosition.CenterScreen;
                    _loginForm.Show();
                }
                catch (Exception ex2)
                {
                    Logger.Error($"创建新窗口也失败: {ex2.Message}");
                }
            }
        }

        public void ShowBalloonTip(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
        {
            _notifyIcon.ShowBalloonTip(3000, title, message, icon);
        }

        public void UpdateMenuItems()
        {
            try
            {
                Logger.Info("开始更新托盘菜单");
                
                ToolStripMenuItem existingMetaItem = _contextMenu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(item => item.Text == "查询元数据");
                
                bool isAdmin = IsAdminUser();
                Logger.Info($"当前用户角色检查结果: IsAdmin={isAdmin}, Role={GlobalState.Instance.Role}");
                
                if (isAdmin && existingMetaItem == null)
                {
                    Logger.Info("用户是管理员，添加查询元数据菜单");
                    ToolStripMenuItem queryMetaItem = new ToolStripMenuItem("查询元数据");
                    queryMetaItem.Click += (sender, e) => ShowMetaQueryWindow();
                    
                    int insertIndex = _contextMenu.Items.IndexOf(_contextMenu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(item => item.Text == "显示日志")) + 1;
                    _contextMenu.Items.Insert(insertIndex, queryMetaItem);
                    Logger.Info("查询元数据菜单已添加");
                }
                else if (!isAdmin && existingMetaItem != null)
                {
                    Logger.Info("用户非管理员，移除查询元数据菜单");
                    _contextMenu.Items.Remove(existingMetaItem);
                    Logger.Info("查询元数据菜单已移除");
                }
                else
                {
                    Logger.Info("菜单状态无需变更");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"更新菜单失败: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _contextMenu.Dispose();
        }
    }
}
