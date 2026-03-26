using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
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
        private Label _domainLabel;
        private TextBox _domainTextBox;
        private Label _portLabel;
        private TextBox _portTextBox;
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
            this.Size = new System.Drawing.Size(300, 280);
            
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
            
            // 域名标签
            _domainLabel = new Label
            {
                Text = "域名:",
                Location = new System.Drawing.Point(20, 90),
                Size = new System.Drawing.Size(60, 20)
            };
            
            // 域名输入框
            _domainTextBox = new TextBox
            {
                Location = new System.Drawing.Point(80, 88),
                Size = new System.Drawing.Size(180, 20),
                Text = "localhost"
            };
            
            // 端口标签
            _portLabel = new Label
            {
                Text = "端口:",
                Location = new System.Drawing.Point(20, 120),
                Size = new System.Drawing.Size(60, 20)
            };
            
            // 端口输入框
            _portTextBox = new TextBox
            {
                Location = new System.Drawing.Point(80, 118),
                Size = new System.Drawing.Size(180, 20),
                Text = "8080"
            };
            
            // 错误提示标签
            _errorLabel = new Label
            {
                Text = "",
                ForeColor = System.Drawing.Color.Red,
                Location = new System.Drawing.Point(80, 150),
                Size = new System.Drawing.Size(180, 20),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            
            // 登录按钮
            _loginButton = new Button
            {
                Text = "登录",
                Location = new System.Drawing.Point(210, 180),
                Size = new System.Drawing.Size(50, 25)
            };
            _loginButton.Click += LoginButton_Click;
            
            // 添加控件
            this.Controls.Add(_usernameLabel);
            this.Controls.Add(_usernameTextBox);
            this.Controls.Add(_passwordLabel);
            this.Controls.Add(_passwordTextBox);
            this.Controls.Add(_domainLabel);
            this.Controls.Add(_domainTextBox);
            this.Controls.Add(_portLabel);
            this.Controls.Add(_portTextBox);
            this.Controls.Add(_errorLabel);
            this.Controls.Add(_loginButton);
        }
        
        private void LoginButton_Click(object sender, EventArgs e)
        {
            // 表单验证
            string username = _usernameTextBox.Text.Trim();
            string password = _passwordTextBox.Text;
            string domain = _domainTextBox.Text.Trim();
            string port = _portTextBox.Text.Trim();
            
            if (string.IsNullOrEmpty(username))
            {
                _errorLabel.Text = "请输入用户名";
                return;
            }
            
            if (string.IsNullOrEmpty(password))
            {
                _errorLabel.Text = "请输入密码";
                return;
            }
            
            if (string.IsNullOrEmpty(domain))
            {
                _errorLabel.Text = "请输入域名";
                return;
            }
            
            if (string.IsNullOrEmpty(port) || !int.TryParse(port, out _))
            {
                _errorLabel.Text = "请输入有效的端口号";
                return;
            }
            
            try
            {
                // 写死的登录逻辑，直接返回默认token
                string token = "default_token_123456";
                SaveLoginInfo(username, token);
                
                // 存储服务器IP、端口、用户名和token到全局状态
                GlobalState.Instance.ServerIp = domain;
                GlobalState.Instance.ServerPort = int.Parse(port);
                GlobalState.Instance.Username = username;
                GlobalState.Instance.Token = token;
                GlobalState.Instance.IsLoggedIn = true;
                
                // 记录token到日志
                Logger.Info($"用户 {username} 登录成功，token: {token}");
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                _errorLabel.Text = "登录失败: " + ex.Message;
                Logger.Error($"登录失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 通用HTTP请求处理方法
        /// </summary>
        /// <param name="method">HTTP请求方法</param>
        /// <param name="url">请求地址（可以是完整URL或相对路径）</param>
        /// <param name="content">请求内容（JSON格式）</param>
        /// <returns>响应数据对象</returns>
        private async Task<dynamic> requestHandler(HttpMethod method, string url, string content = null)
        {
            // 创建新的HttpClient实例
            using var httpClient = new HttpClient();
            
            try
            {
                // 构建完整的请求地址
                string fullUrl = url;
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    // 使用全局状态中的服务器地址
                    string serverAddress = GlobalState.Instance.GetServerAddress();
                    fullUrl = $"{serverAddress}{url}";
                }
                
                // 设置请求超时时间为30秒
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                
                // 创建请求消息
                var request = new HttpRequestMessage(method, fullUrl);
                
                // 设置请求头
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                
                // 添加Authorization请求头（如果有token）
                string token = GlobalState.Instance.Token;
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
                
                // 添加请求内容（如果有）
                if (!string.IsNullOrEmpty(content))
                {
                    request.Content = new StringContent(content, Encoding.UTF8, "application/json");
                }
                
                // 发送请求并获取响应
                var response = await httpClient.SendAsync(request);
                
                // 读取响应内容
                string responseContent = await response.Content.ReadAsStringAsync();
                
                // 解析响应JSON
                var jsonDocument = JsonDocument.Parse(responseContent);
                var root = jsonDocument.RootElement;
                
                // 处理HTTP状态码
                if (!response.IsSuccessStatusCode)
                {
                    string errorMessage = root.TryGetProperty("message", out var errorMessageElement) ? errorMessageElement.GetString() : "请求失败";
                    MessageBox.Show(errorMessage, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }
                
                // 处理业务逻辑状态码
                int status = root.TryGetProperty("status", out var statusElement) ? statusElement.GetInt32() : 0;
                if (status != 200)
                {
                    string errorMessage = root.TryGetProperty("message", out var errorMessageElement) ? errorMessageElement.GetString() : "操作失败";
                    MessageBox.Show(errorMessage, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }
                
                // 获取响应数据
                string message = root.TryGetProperty("message", out var messageElement) ? messageElement.GetString() : "";
                string date = root.TryGetProperty("date", out var dateElement) ? dateElement.GetString() : "";
                JsonElement dataElement = root.TryGetProperty("data", out var data) ? data : default;
                
                // 构建返回对象
                var responseData = new {
                    status = status,
                    message = message,
                    date = date,
                    data = dataElement
                };
                
                return responseData;
            }
            catch (InvalidOperationException ex) when (ex.Message == "服务器IP和端口未设置")
            {
                // 服务器IP和端口未设置，重定向到登录流程
                MessageBox.Show("请先登录", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.Error($"请求失败: {ex.Message}");
                
                // 打开登录窗口
                var loginForm = new LoginForm();
                loginForm.ShowDialog();
                
                throw;
            }
            catch (HttpRequestException ex)
            {
                // 网络错误处理
                MessageBox.Show("网络连接失败，请检查网络设置", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.Error($"网络请求失败: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                // 其他错误处理
                Logger.Error($"请求处理失败: {ex.Message}");
                throw;
            }
        }
        
        private void SaveLoginInfo(string username, string token)
        {
            try
            {
                // 登录信息已存储在GlobalState中，无需写入文件
                Logger.Info("登录信息已保存到内存缓存");
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
                return GlobalState.Instance.IsLoggedIn;
            }
            catch (Exception ex)
            {
                Logger.Error($"检查登录状态失败: {ex.Message}");
                return false;
            }
        }
        
        public static string GetUsername()
        {
            try
            {
                return GlobalState.Instance.Username;
            }
            catch (Exception ex)
            {
                Logger.Error($"获取用户名失败: {ex.Message}");
                return string.Empty;
            }
        }
        
        public static string GetToken()
        {
            try
            {
                return GlobalState.Instance.Token;
            }
            catch (Exception ex)
            {
                Logger.Error($"获取token失败: {ex.Message}");
                return string.Empty;
            }
        }
    }
}