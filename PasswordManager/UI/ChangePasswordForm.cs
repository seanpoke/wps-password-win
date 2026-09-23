using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using PasswordManager.Services.Request;
using PasswordManager.Services.Routing;
using PasswordManager.Utils;

namespace PasswordManager.UI
{
    /// <summary>
    /// 首次登录强制改密弹窗。
    /// 在 LoginForm 判定 needChangePwd=true 时弹出，仅使用登录返回的临时 token，
    /// 不落地任何用户信息与 token；改密成功后关闭，由用户回到登录页用新密码重新登录。
    /// </summary>
    public class ChangePasswordForm : Form
    {
        private readonly string _account;
        private readonly string _oldPassword;
        private readonly string _token;

        private Label _accountLabel;
        private TextBox _newPasswordTextBox;
        private TextBox _confirmPasswordTextBox;
        private Button _saveButton;
        private Label _errorLabel;
        private Label _tipLabel;

        public ChangePasswordForm(string account, string oldPassword, string token)
        {
            _account = account;
            _oldPassword = oldPassword;
            _token = token;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "重置密码";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.White;
            this.ClientSize = new Size(440, 310);

            Font labelFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
            Font inputFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
            Font buttonFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);

            int labelWidth = 90;
            int inputWidth = 270;
            int inputHeight = 28;
            int startY = 50;
            int gap = 42;

            Label titleLabel = new Label
            {
                Text = "首次登录，请重置密码",
                ForeColor = Color.FromArgb(0, 120, 212),
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 12)
            };
            this.Controls.Add(titleLabel);

            Label accountLabel = new Label
            {
                Text = "账号:",
                TextAlign = ContentAlignment.MiddleRight,
                Font = labelFont,
                ForeColor = Color.FromArgb(60, 60, 60),
                Size = new Size(labelWidth, inputHeight),
                Location = new Point(15, startY)
            };
            this.Controls.Add(accountLabel);

            _accountLabel = new Label
            {
                Text = _account ?? "",
                TextAlign = ContentAlignment.MiddleLeft,
                Font = labelFont,
                ForeColor = Color.Black,
                AutoSize = true,
                Location = new Point(15 + labelWidth, startY)
            };
            this.Controls.Add(_accountLabel);

            Label newPwdLabel = new Label
            {
                Text = "新密码:",
                TextAlign = ContentAlignment.MiddleRight,
                Font = labelFont,
                ForeColor = Color.FromArgb(60, 60, 60),
                Size = new Size(labelWidth, inputHeight),
                Location = new Point(15, startY + gap)
            };
            this.Controls.Add(newPwdLabel);

            _newPasswordTextBox = new TextBox
            {
                PasswordChar = '*',
                Font = inputFont,
                BorderStyle = BorderStyle.FixedSingle,
                Size = new Size(inputWidth, inputHeight),
                Location = new Point(15 + labelWidth, startY + gap)
            };
            this.Controls.Add(_newPasswordTextBox);
            var newPwdEye = LoginForm.CreatePasswordEyeLabel(_newPasswordTextBox);
            this.Controls.Add(newPwdEye);
            newPwdEye.BringToFront();

            Label confirmPwdLabel = new Label
            {
                Text = "确认密码:",
                TextAlign = ContentAlignment.MiddleRight,
                Font = labelFont,
                ForeColor = Color.FromArgb(60, 60, 60),
                Size = new Size(labelWidth, inputHeight),
                Location = new Point(15, startY + gap * 2)
            };
            this.Controls.Add(confirmPwdLabel);

            _confirmPasswordTextBox = new TextBox
            {
                PasswordChar = '*',
                Font = inputFont,
                BorderStyle = BorderStyle.FixedSingle,
                Size = new Size(inputWidth, inputHeight),
                Location = new Point(15 + labelWidth, startY + gap * 2)
            };
            this.Controls.Add(_confirmPasswordTextBox);
            var confirmPwdEye = LoginForm.CreatePasswordEyeLabel(_confirmPasswordTextBox);
            this.Controls.Add(confirmPwdEye);
            confirmPwdEye.BringToFront();

            _tipLabel = new Label
            {
                Text = "密码要求：至少8位，且包含大写字母、小写字母和数字",
                ForeColor = Color.Gray,
                Font = new Font("Microsoft YaHei UI", 8F, FontStyle.Regular),
                AutoSize = false,
                Size = new Size(315, 34),
                Location = new Point(15 + labelWidth, startY + gap * 2 + inputHeight + 4)
            };
            this.Controls.Add(_tipLabel);

            _errorLabel = new Label
            {
                Text = "",
                ForeColor = Color.Red,
                Font = labelFont,
                AutoSize = true,
                Location = new Point(15, startY + gap * 3 + 30)
            };
            this.Controls.Add(_errorLabel);

            _saveButton = new Button
            {
                Text = "保存",
                Font = buttonFont,
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Size = new Size(100, 35),
                Location = new Point(this.ClientSize.Width - 120, startY + gap * 3 + 68)
            };
            _saveButton.FlatAppearance.BorderSize = 0;
            _saveButton.Click += SaveButton_Click;
            this.Controls.Add(_saveButton);
        }

        private async void SaveButton_Click(object sender, EventArgs e)
        {
            string newPwd = _newPasswordTextBox.Text;
            string confirmPwd = _confirmPasswordTextBox.Text;

            if (string.IsNullOrEmpty(newPwd))
            {
                _errorLabel.Text = "请输入新密码";
                return;
            }

            if (!IsValidPassword(newPwd))
            {
                _errorLabel.Text = "密码须至少8位，且同时包含大写字母、小写字母和数字";
                return;
            }

            if (newPwd != confirmPwd)
            {
                _errorLabel.Text = "两次输入的密码不一致";
                return;
            }

            _saveButton.Enabled = false;
            _errorLabel.Text = "";

            bool success = await DoChangePasswordAsync(newPwd);

            if (success)
            {
                MessageBox.Show("密码修改成功，请使用新密码重新登录", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                _saveButton.Enabled = true;
            }
        }

        private async Task<bool> DoChangePasswordAsync(string newPassword)
        {
            try
            {
                var httpRequestService = new HttpRequestService();
                var requestData = new
                {
                    oldPassword = _oldPassword,
                    newPassword = newPassword
                };

                // 优先使用登录成功后返回的临时 token；若该 token 为空，则从全局缓存获取
                string effectiveToken = _token ?? GlobalState.Instance.Token;

                var response = await httpRequestService.PostAsync<object>(
                    ApiRoutes.AccountChangePassword,
                    requestData,
                    effectiveToken
                );

                if (response != null && response.status == 200)
                {
                    Logger.Info($"用户 {_account} 修改密码成功");
                    return true;
                }

                string err = response?.message ?? "未知错误";
                Logger.Error($"用户 {_account} 修改密码失败: {err}");
                _errorLabel.Text = "修改失败: " + err;
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"修改密码异常: {ex.Message}");
                _errorLabel.Text = "修改失败: " + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 密码强度校验：至少8位，且同时包含大写字母、小写字母、数字。
        /// </summary>
        private bool IsValidPassword(string pwd)
        {
            if (string.IsNullOrEmpty(pwd) || pwd.Length < 8)
            {
                return false;
            }

            bool hasLower = false;
            bool hasUpper = false;
            bool hasDigit = false;

            foreach (char c in pwd)
            {
                if (char.IsLower(c)) hasLower = true;
                else if (char.IsUpper(c)) hasUpper = true;
                else if (char.IsDigit(c)) hasDigit = true;
            }

            return hasLower && hasUpper && hasDigit;
        }
    }
}
