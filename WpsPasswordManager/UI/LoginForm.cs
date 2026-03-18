using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using WpsPasswordManager.Utils;

namespace WpsPasswordManager.UI
{
    public class LoginForm : Form
    {
        private const string LoginCacheFile = "login_cache.json";
        
        private Label _usernameLabel;
        private TextBox _usernameTextBox;
        private Label _passwordLabel;
        private TextBox _passwordTextBox;
        private Button _loginButton;
        private Label _errorLabel;
        
        public LoginForm()
        {
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            // 配置表单
            this.Text = "登录";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new System.Drawing.Size(300, 200);
            
            // 用户名标签
            _usernameLabel = new Label
            {
                Text = "用户名:",
                Location = new System.Drawing.Point(20, 30),
                Size = new System.Drawing.Size(60, 20)
            };
            
            // 用户名输入框
            _usernameTextBox = new TextBox
            {
                Location = new System.Drawing.Point(80, 28),
                Size = new System.Drawing.Size(180, 20)
            };
            
            // 密码标签
            _passwordLabel = new Label
            {
                Text = "密码:",
                Location = new System.Drawing.Point(20, 60),
                Size = new System.Drawing.Size(60, 20)
            };
            
            // 密码输入框
            _passwordTextBox = new TextBox
            {
                Location = new System.Drawing.Point(80, 58),
                Size = new System.Drawing.Size(180, 20),
                PasswordChar = '*'
            };
            
            // 错误提示标签
            _errorLabel = new Label
            {
                Text = "",
                ForeColor = System.Drawing.Color.Red,
                Location = new System.Drawing.Point(80, 90),
                Size = new System.Drawing.Size(180, 20),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            
            // 登录按钮
            _loginButton = new Button
            {
                Text = "登录",
                Location = new System.Drawing.Point(210, 120),
                Size = new System.Drawing.Size(50, 25)
            };
            _loginButton.Click += LoginButton_Click;
            
            // 添加控件
            this.Controls.Add(_usernameLabel);
            this.Controls.Add(_usernameTextBox);
            this.Controls.Add(_passwordLabel);
            this.Controls.Add(_passwordTextBox);
            this.Controls.Add(_errorLabel);
            this.Controls.Add(_loginButton);
        }
        
        private void LoginButton_Click(object sender, EventArgs e)
        {
            string username = _usernameTextBox.Text.Trim();
            string password = _passwordTextBox.Text;
            
            // 验证密码
            if (password == "123")
            {
                // 保存登录信息到本地缓存
                SaveLoginInfo(username);
                Logger.Info($"用户 {username} 登录成功");
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                // 显示错误信息
                _errorLabel.Text = "用户名密码不对";
                Logger.Warning("登录失败: 密码错误");
            }
        }
        
        private void SaveLoginInfo(string username)
        {
            try
            {
                var loginInfo = new { Username = username, LoggedIn = true };
                string json = JsonSerializer.Serialize(loginInfo, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(LoginCacheFile, json);
                Logger.Info("登录信息已保存到本地缓存");
            }
            catch (Exception ex)
            {
                Logger.Error($"保存登录信息失败: {ex.Message}");
            }
        }
        
        public static bool IsLoggedIn()
        {
            try
            {
                if (File.Exists(LoginCacheFile))
                {
                    string json = File.ReadAllText(LoginCacheFile);
                    var loginInfo = JsonSerializer.Deserialize<dynamic>(json);
                    return loginInfo?.LoggedIn == true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"检查登录状态失败: {ex.Message}");
            }
            return false;
        }
        
        public static string GetUsername()
        {
            try
            {
                if (File.Exists(LoginCacheFile))
                {
                    string json = File.ReadAllText(LoginCacheFile);
                    var loginInfo = JsonSerializer.Deserialize<dynamic>(json);
                    return loginInfo?.Username?.ToString() ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"获取用户名失败: {ex.Message}");
            }
            return string.Empty;
        }
    }
}