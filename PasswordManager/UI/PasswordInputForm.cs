using System;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;

namespace PasswordManager.UI
{
    public class PasswordInputForm : Form
    {
        private Label _promptLabel;
        private TextBox _passwordTextBox;
        private Button _confirmButton;
        private Button _cancelButton;
        private System.Windows.Forms.Timer _topmostTimer;

        public string InputPassword { get; private set; }

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const int HWND_TOPMOST = -1;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;

        public PasswordInputForm(string documentName)
        {
            this.TopMost = true;
            InitializeComponent(documentName);
            InitializeTopmostTimer();
        }

        private void InitializeComponent(string documentName)
        {
            this.Text = "输入密码";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.White;
            this.AutoScaleMode = AutoScaleMode.Font;

            Font labelFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
            Font inputFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
            Font buttonFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);

            int controlWidth = 280;
            int inputHeight = 28;
            int buttonHeight = 32;
            int controlGap = 20;

            int startY = 30;

            _promptLabel = new Label
            {
                Text = $"文件 \"{documentName}\" 需要密码才能打开，请输入密码：",
                Font = labelFont,
                ForeColor = Color.Black,
                AutoSize = true,
                Location = new Point(20, startY)
            };

            startY += _promptLabel.Height + controlGap;

            _passwordTextBox = new TextBox
            {
                Size = new Size(controlWidth, inputHeight),
                Location = new Point(20, startY),
                Font = inputFont,
                PasswordChar = '*',
                PlaceholderText = "请输入密码"
            };
            _passwordTextBox.KeyDown += PasswordTextBox_KeyDown;

            startY += inputHeight + controlGap;

            _confirmButton = new Button
            {
                Text = "确认",
                Size = new Size(130, buttonHeight),
                Location = new Point(20, startY),
                Font = buttonFont,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 122, 204),
                FlatStyle = FlatStyle.Flat
            };
            _confirmButton.FlatAppearance.BorderSize = 0;
            _confirmButton.Click += ConfirmButton_Click;

            _cancelButton = new Button
            {
                Text = "取消",
                Size = new Size(130, buttonHeight),
                Location = new Point(170, startY),
                Font = buttonFont,
                ForeColor = Color.Black,
                BackColor = Color.FromArgb(220, 220, 220),
                FlatStyle = FlatStyle.Flat
            };
            _cancelButton.FlatAppearance.BorderSize = 0;
            _cancelButton.Click += CancelButton_Click;

            startY += buttonHeight + 30;

            this.ClientSize = new Size(controlWidth + 40, startY);

            this.Controls.Add(_promptLabel);
            this.Controls.Add(_passwordTextBox);
            this.Controls.Add(_confirmButton);
            this.Controls.Add(_cancelButton);

            this.AcceptButton = _confirmButton;
            this.CancelButton = _cancelButton;
        }

        private void InitializeTopmostTimer()
        {
            _topmostTimer = new System.Windows.Forms.Timer
            {
                Interval = 200
            };
            _topmostTimer.Tick += (sender, e) =>
            {
                if (!this.IsDisposed && this.Visible)
                {
                    SetWindowPos(this.Handle, (IntPtr)HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
                }
            };
            _topmostTimer.Start();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000008;
                return cp;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _topmostTimer?.Stop();
            _topmostTimer?.Dispose();
            base.OnFormClosing(e);
        }

        private void PasswordTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                ConfirmButton_Click(sender, e);
            }
            else if (e.KeyCode == Keys.Escape)
            {
                CancelButton_Click(sender, e);
            }
        }

        private void ConfirmButton_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_passwordTextBox.Text.Trim()))
            {
                InputPassword = _passwordTextBox.Text.Trim();
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("请输入密码", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _passwordTextBox.Focus();
            }
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            InputPassword = null;
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}