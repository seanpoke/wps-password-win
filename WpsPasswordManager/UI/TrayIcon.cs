using System;
using System.Windows.Forms;

namespace WpsPasswordManager.UI
{
    public class TrayIcon
    {
        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _contextMenu;
        private LoginForm _loginForm;

        public event EventHandler ExitClicked;
        public event EventHandler OpenFolderClicked;

        public void Initialize()
        {
            // 创建系统托盘图标
            _notifyIcon = new NotifyIcon
            {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location),
                Text = "WPS 密码自动填充插件",
                Visible = true
            };

            // 创建上下文菜单
            _contextMenu = new ContextMenuStrip();
            
            // 添加菜单项
            ToolStripMenuItem homeItem = new ToolStripMenuItem("主页");
            homeItem.Click += (sender, e) => ShowMainWindow();
            
            ToolStripMenuItem openFolderItem = new ToolStripMenuItem("打开安装目录");
            openFolderItem.Click += (sender, e) => OpenFolderClicked?.Invoke(this, EventArgs.Empty);
            
            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += (sender, e) => ExitClicked?.Invoke(this, EventArgs.Empty);

            _contextMenu.Items.Add(homeItem);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(openFolderItem);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = _contextMenu;

            // 双击图标事件
            _notifyIcon.DoubleClick += (sender, e) =>
            {
                ShowMainWindow();
            };
        }
        
        private void ShowMainWindow()
        {
            // 检查登录窗口是否已经存在
            if (_loginForm != null && !_loginForm.IsDisposed)
            {
                // 如果窗口存在且未被销毁，激活它
                _loginForm.Activate();
                if (_loginForm.WindowState == FormWindowState.Minimized)
                {
                    _loginForm.WindowState = FormWindowState.Normal;
                }
            }
            else
            {
                // 创建并显示登录窗口
                _loginForm = new LoginForm();
                _loginForm.StartPosition = FormStartPosition.CenterScreen;
                _loginForm.FormClosed += (sender, e) =>
                {
                    // 窗口关闭时重置引用
                    _loginForm = null;
                };
                _loginForm.Show();
            }
        }

        public void ShowBalloonTip(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
        {
            _notifyIcon.ShowBalloonTip(3000, title, message, icon);
        }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _contextMenu.Dispose();
        }
    }
}