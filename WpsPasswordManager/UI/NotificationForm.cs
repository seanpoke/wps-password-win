using System;
using System.Windows.Forms;

namespace WpsPasswordManager.UI
{
    public class NotificationForm : Form
    {
        private Label _messageLabel;
        private System.Windows.Forms.Timer _closeTimer;

        public NotificationForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // 配置表单
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.BackColor = System.Drawing.Color.FromArgb(240, 240, 240);
            this.Size = new System.Drawing.Size(300, 100);
            this.StartPosition = FormStartPosition.Manual;

            // 创建消息标签
            _messageLabel = new Label
            {
                Text = "提示信息",
                Font = new System.Drawing.Font("微软雅黑", 10F),
                ForeColor = System.Drawing.Color.Black,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };

            this.Controls.Add(_messageLabel);

            // 创建定时器，10秒后自动关闭
            _closeTimer = new System.Windows.Forms.Timer
            {
                Interval = 10000
            };
            _closeTimer.Tick += (sender, e) =>
            {
                _closeTimer.Stop();
                this.Close();
            };
        }

        public void ShowNotification(string message)
        {
            _messageLabel.Text = message;

            this.Show();
            
            // 定位到屏幕中间
            System.Drawing.Rectangle screenBounds = Screen.GetWorkingArea(this);
            int x = (screenBounds.Width - this.Width) / 2 + screenBounds.Left;
            int y = (screenBounds.Height - this.Height) / 2 + screenBounds.Top;
            
            // 确保坐标不为负数
            x = Math.Max(0, x);
            y = Math.Max(0, y);
            
            this.Location = new System.Drawing.Point(x, y);
            
            _closeTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // 绘制边框
            using (System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(200, 200, 200), 1))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
            }
        }
    }
}