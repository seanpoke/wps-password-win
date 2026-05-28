using System;
using System.Linq;
using System.Windows.Forms;

namespace PasswordManager.UI
{
    public class TrayIcon
    {
        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _contextMenu;
        private LoginForm _loginForm;

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

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _contextMenu.Dispose();
        }
    }
}