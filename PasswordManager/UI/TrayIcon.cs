using System;
using System.Linq;
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

            _notifyIcon.ContextMenuStrip = _contextMenu;

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

        private void ShowMainWindow()
        {
            LoginForm existingForm = Application.OpenForms.OfType<LoginForm>().FirstOrDefault();
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
                _loginForm = new LoginForm();
                _loginForm.StartPosition = FormStartPosition.CenterScreen;
                _loginForm.FormClosed += (sender, e) =>
                {
                    _loginForm = null;
                };
                _loginForm.Show();
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