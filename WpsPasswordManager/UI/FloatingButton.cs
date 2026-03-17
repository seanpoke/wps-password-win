using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WpsPasswordManager.Monitor;
using WpsPasswordManager.Utils;

namespace WpsPasswordManager.UI
{
    public class FloatingButton : Form
    {
        private Button _generateButton;
        private IntPtr _passwordEditHandle;
        private WpsMonitor _monitor;
        private bool _isVisible = false;

        // Win32 API 定义
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        // 常量定义
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        public event EventHandler GeneratePasswordClicked;

        public FloatingButton(WpsMonitor monitor)
        {
            _monitor = monitor;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // 配置表单
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.BackColor = System.Drawing.Color.FromArgb(240, 240, 240);
            this.TransparencyKey = System.Drawing.Color.Magenta;

            // 创建按钮
            _generateButton = new Button
            {
                Text = "生成密码",
                FlatStyle = FlatStyle.Flat,
                BackColor = System.Drawing.Color.FromArgb(0, 120, 212),
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold),
                Size = new System.Drawing.Size(90, 32),
                Location = new System.Drawing.Point(0, 0),
                Cursor = System.Windows.Forms.Cursors.Hand
            };

            _generateButton.FlatAppearance.BorderSize = 0;
            _generateButton.Click += (sender, e) => GeneratePasswordClicked?.Invoke(this, EventArgs.Empty);

            this.Controls.Add(_generateButton);
            this.Size = _generateButton.Size;
        }

        // 重写 Show 方法，确保每次显示时都在最顶层
        public new void Show()
        {
            // 设置窗口为最顶层
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
            base.Show();
            // 只有在按钮首次显示时才设置焦点，避免重复设置焦点导致鼠标自动移动
            if (!_isVisible)
            {
                // 移除焦点设置，避免鼠标自动移动到按钮上
                // _generateButton.Focus();
                _isVisible = true;
            }
        }

        public void ShowAtPasswordBox(IntPtr passwordEditHandle)
        {
            _passwordEditHandle = passwordEditHandle;
            if (passwordEditHandle == IntPtr.Zero)
            {
                Logger.Warning("密码输入框句柄为空，无法显示悬浮按钮");
                this.Hide();
                return;
            }

            // 获取密码框位置
            WpsMonitor.RECT rect = _monitor.GetWindowRect(passwordEditHandle);
            float dpiScale = _monitor.GetDpiScale();

            // 计算按钮位置（密码框右侧5px）
            int x = (int)(rect.Right + 5 * dpiScale);
            int y = (int)(rect.Top * dpiScale);

            // 只有在位置发生变化时才更新位置
            if (this.Location.X != x || this.Location.Y != y)
            {
                Logger.Debug($"显示悬浮按钮，位置: X={x}, Y={y}, DPI缩放: {dpiScale}");
                this.Location = new System.Drawing.Point(x, y);
            }
            Show(); // 调用重写的 Show 方法
        }

        public void ShowAtDialog(IntPtr dialogHandle)
        {
            if (dialogHandle == IntPtr.Zero)
            {
                Logger.Warning("对话框句柄为空，无法显示悬浮按钮");
                this.Hide();
                return;
            }

            // 获取对话框位置
            WpsMonitor.RECT rect = _monitor.GetWindowRect(dialogHandle);
            float dpiScale = _monitor.GetDpiScale();

            // 计算按钮位置（对话框右侧紧靠着窗口，垂直居中）
            int x = (int)(rect.Right - 5 * dpiScale); // 距离窗口右侧5px
            int y = (int)((rect.Top + rect.Bottom) / 2 * dpiScale - this.Height / 2); // 垂直居中

            // 只有在位置发生变化时才更新位置
            if (this.Location.X != x || this.Location.Y != y)
            {
                Logger.Debug($"显示悬浮按钮在对话框右侧，位置: X={x}, Y={y}, DPI缩放: {dpiScale}");
                this.Location = new System.Drawing.Point(x, y);
            }
            Show(); // 调用重写的 Show 方法
        }

        public void HideButton()
        {
            Logger.Debug("隐藏悬浮按钮");
            this.Hide();
            // 重置可见状态
            _isVisible = false;
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            // 失去焦点时隐藏
            // this.Hide();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // 可以添加自定义绘制逻辑
        }
    }
}