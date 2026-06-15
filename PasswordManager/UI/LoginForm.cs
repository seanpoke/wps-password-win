using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using PasswordManager.Utils;

namespace PasswordManager.UI
{
    public class LoginForm : Form
    {
        private const string LoginCacheFile = "login_cache.json";
        
        private Label _userInfoLabel;
        private TextBox _usernameTextBox;
        private TextBox _passwordTextBox;
        private TextBox _domainTextBox;
        private TextBox _portTextBox;
        private Button _loginButton;
        private Label _errorLabel;
        private Label _loadingLabel;
        private Panel _loadingOverlay;
        
        public LoginForm()
        {
            InitializeComponent();
            LoadSavedInfo();
        }
        
        private void LoadSavedInfo()
        {
            if (!string.IsNullOrEmpty(GlobalState.Instance.RawDomain))
            {
                _domainTextBox.Text = GlobalState.Instance.RawDomain;
            }
            else if (!string.IsNullOrEmpty(GlobalState.Instance.ServerIp))
            {
                string protocol = GlobalState.Instance.Protocol;
                _domainTextBox.Text = $"{protocol}://{GlobalState.Instance.ServerIp}";
            }
            if (GlobalState.Instance.ServerPort > 0)
            {
                _portTextBox.Text = GlobalState.Instance.ServerPort.ToString();
            }
            
            if (!string.IsNullOrEmpty(GlobalState.Instance.Username))
            {
                _usernameTextBox.Text = GlobalState.Instance.Username;
            }
            
            UpdateUIState();
        }
        
        private void InitializeComponent()
        {
            this.Text = "密码管理";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.White;
            this.MinimumSize = new Size(450, 400);
            
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Font;
            
            this.Resize += LoginForm_Resize;
            
            Font labelFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
            Font inputFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
            Font buttonFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            Font userInfoFont = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            
            int labelWidth = 70;
            int inputWidth = 280;
            int inputHeight = 28;
            int controlGap = 45;
            int labelGap = 15;
            
            int startY = 70;
            
            int totalWidth = labelWidth + inputWidth + labelGap * 4;
            int totalHeight = startY + controlGap * 6 + 50;
            
            this.ClientSize = new Size(totalWidth, totalHeight);
            
            _userInfoLabel = new Label
            {
                Text = "",
                ForeColor = Color.Green,
                TextAlign = ContentAlignment.MiddleRight,
                Visible = false,
                Font = userInfoFont,
                AutoSize = true,
                Location = new Point(this.ClientSize.Width - 160, 20)
            };
            this.Controls.Add(_userInfoLabel);
            
            Label usernameLabel = new Label
            {
                Text = "用户名:",
                TextAlign = ContentAlignment.MiddleRight,
                Font = labelFont,
                ForeColor = Color.FromArgb(60, 60, 60),
                Size = new Size(labelWidth, inputHeight),
                Location = new Point(labelGap, startY)
            };
            this.Controls.Add(usernameLabel);
            
            _usernameTextBox = new TextBox
            {
                Font = inputFont,
                BorderStyle = BorderStyle.FixedSingle,
                Size = new Size(inputWidth, inputHeight),
                Location = new Point(labelWidth + labelGap * 2, startY)
            };
            this.Controls.Add(_usernameTextBox);
            
            Label passwordLabel = new Label
            {
                Text = "密码:",
                TextAlign = ContentAlignment.MiddleRight,
                Font = labelFont,
                ForeColor = Color.FromArgb(60, 60, 60),
                Size = new Size(labelWidth, inputHeight),
                Location = new Point(labelGap, startY + controlGap)
            };
            this.Controls.Add(passwordLabel);
            
            _passwordTextBox = new TextBox
            {
                PasswordChar = '*',
                Font = inputFont,
                BorderStyle = BorderStyle.FixedSingle,
                Size = new Size(inputWidth, inputHeight),
                Location = new Point(labelWidth + labelGap * 2, startY + controlGap)
            };
            this.Controls.Add(_passwordTextBox);
            
            Label domainLabel = new Label
            {
                Text = "域名:",
                TextAlign = ContentAlignment.MiddleRight,
                Font = labelFont,
                ForeColor = Color.FromArgb(60, 60, 60),
                Size = new Size(labelWidth, inputHeight),
                Location = new Point(labelGap, startY + controlGap * 2)
            };
            this.Controls.Add(domainLabel);
            
            _domainTextBox = new TextBox
            {
                Text = "localhost",
                Font = inputFont,
                BorderStyle = BorderStyle.FixedSingle,
                Size = new Size(inputWidth, inputHeight),
                Location = new Point(labelWidth + labelGap * 2, startY + controlGap * 2)
            };
            this.Controls.Add(_domainTextBox);
            
            Label portLabel = new Label
            {
                Text = "端口:",
                TextAlign = ContentAlignment.MiddleRight,
                Font = labelFont,
                ForeColor = Color.FromArgb(60, 60, 60),
                Size = new Size(labelWidth, inputHeight),
                Location = new Point(labelGap, startY + controlGap * 3)
            };
            this.Controls.Add(portLabel);
            
            _portTextBox = new TextBox
            {
                Text = "8443",
                Font = inputFont,
                BorderStyle = BorderStyle.FixedSingle,
                Size = new Size(120, inputHeight),
                Location = new Point(labelWidth + labelGap * 2, startY + controlGap * 3)
            };
            this.Controls.Add(_portTextBox);
            
            _errorLabel = new Label
            {
                Text = "",
                ForeColor = Color.Red,
                Size = new Size(inputWidth + labelWidth + labelGap * 3, 45),
                AutoSize = false,
                TextAlign = ContentAlignment.TopLeft,
                Font = labelFont,
                Location = new Point(labelGap, startY + controlGap * 4)
            };
            this.Controls.Add(_errorLabel);
            
            _loadingLabel = new Label
            {
                Text = "正在验证身份...",
                ForeColor = Color.Blue,
                Size = new Size(inputWidth + labelWidth + labelGap * 3, 45),
                AutoSize = false,
                TextAlign = ContentAlignment.TopLeft,
                Visible = false,
                Font = labelFont,
                Location = new Point(labelGap, startY + controlGap * 4)
            };
            this.Controls.Add(_loadingLabel);
            
            _loginButton = new Button
            {
                Text = "登录",
                Font = buttonFont,
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Size = new Size(100, 35),
                Location = new Point(this.ClientSize.Width - 120, startY + controlGap * 5)
            };
            _loginButton.FlatAppearance.BorderSize = 0;
            _loginButton.MouseEnter += (sender, e) => _loginButton.BackColor = Color.FromArgb(26, 115, 232);
            _loginButton.MouseLeave += (sender, e) => _loginButton.BackColor = Color.FromArgb(0, 120, 212);
            _loginButton.MouseDown += (sender, e) => _loginButton.BackColor = Color.FromArgb(0, 90, 170);
            _loginButton.MouseUp += (sender, e) => _loginButton.BackColor = Color.FromArgb(26, 115, 232);
            _loginButton.Click += LoginButton_Click;
            this.Controls.Add(_loginButton);
            
            _loadingOverlay = new Panel
            {
                BackColor = Color.FromArgb(128, 0, 0, 0),
                Visible = false,
                Size = this.ClientSize,
                Location = new Point(0, 0)
            };
            
            Label overlayLabel = new Label
            {
                Text = "正在登录",
                ForeColor = Color.White,
                Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular),
                AutoSize = true
            };
            overlayLabel.Location = new Point(
                (this.ClientSize.Width - overlayLabel.Width) / 2,
                (this.ClientSize.Height - overlayLabel.Height) / 2
            );
            _loadingOverlay.Controls.Add(overlayLabel);
            this.Controls.Add(_loadingOverlay);
            
            UpdateUIState();
        }
        
        private async void LoginButton_Click(object sender, EventArgs e)
        {
            if (GlobalState.Instance.IsLoggedIn)
            {
                LogoutButton_Click(sender, e);
                return;
            }
            
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
            
            if (string.IsNullOrEmpty(port) || !int.TryParse(port, out int serverPort))
            {
                _errorLabel.Text = "请输入有效的端口号";
                return;
            }
            
            _loginButton.Enabled = false;
            
            try
            {
                string cleanDomain = UrlParser.ExtractHost(domain);

                string protocol = UrlParser.ExtractProtocol(domain);

                GlobalState.Instance.ServerIp = cleanDomain;
                GlobalState.Instance.ServerPort = serverPort;
                GlobalState.Instance.Protocol = protocol;
                GlobalState.Instance.RawDomain = domain;
                
                var loginData = new { account = username, password = password };
                string jsonContent = JsonSerializer.Serialize(loginData);
                
                var (response, errorMessage) = await requestHandler(HttpMethod.Post, "/account/login", jsonContent);
                
                if (response != null)
                {
                    string token = response.data.GetProperty("token").GetString();
                    string account = response.data.GetProperty("account").GetString();
                    string name = response.data.GetProperty("name").GetString();
                    string role = response.data.TryGetProperty("role", out JsonElement roleElement) ? roleElement.GetString() : null;
                    
                    GlobalState.Instance.Username = account;
                    GlobalState.Instance.Name = name;
                    GlobalState.Instance.Role = role;
                    GlobalState.Instance.Token = token;
                    GlobalState.Instance.IsLoggedIn = true;
                    
                    GlobalState.Instance.SaveUserInfo();
                    GlobalState.Instance.SaveConfig();
                    
                    Logger.Info($"用户 {account} 登录成功，token: {token}");
                    Logger.Info($"登录成功前IsLoggedIn状态: {GlobalState.Instance.IsLoggedIn}");
                    GlobalState.Instance.IsLoggedIn = true;
                    Logger.Info($"登录成功后IsLoggedIn状态: {GlobalState.Instance.IsLoggedIn}");
                    Logger.Info($"用户角色: {GlobalState.Instance.Role}");
                    Logger.Info("用户登录成功，程序检测机制已开始运行");
                    
                    OnLoginSuccess();
                    
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    string errorText = !string.IsNullOrEmpty(errorMessage) ? errorMessage : "服务器返回错误或网络连接失败";
                    _errorLabel.Text = "登录失败: " + errorText;
                    Logger.Error($"登录失败: {errorText}");
                    GlobalState.Instance.IsLoggedIn = false;
                    Logger.Info("程序检测机制已暂停");
                    _loginButton.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                _errorLabel.Text = "登录失败: " + ex.Message;
                Logger.Error($"登录失败: {ex.Message}");
                GlobalState.Instance.IsLoggedIn = false;
                Logger.Info("程序检测机制已暂停");
                _loginButton.Enabled = true;
            }
        }
        
        private async void LogoutButton_Click(object sender, EventArgs e)
        {
            _loginButton.Enabled = false;
            
            try
            {
                if (Program.IsWpsProcessRunning())
                {
                    DialogResult result = MessageBox.Show(
                        "检测到wps正在运行，建议先关闭wps再执行注销，否则有丢失文件元数据的风险，是否强制注销",
                        "提示",
                        MessageBoxButtons.OKCancel,
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2);
                    
                    if (result == DialogResult.Cancel)
                    {
                        _loginButton.Enabled = true;
                        return;
                    }
                }
                
                GlobalState.Instance.IsLoggedIn = false;
                
                var (logoutSuccess, errorMessage) = await Logout();
            
                Logger.Info("程序检测机制已关闭");

                if (logoutSuccess)
                {
                    Logger.Info("程序检测机制已暂停");
                    UpdateUIState();
                    Logger.Info("用户登出成功，资源已清理");
                    OnLogoutSuccess();
                }
                else
                {
                    string errorText = !string.IsNullOrEmpty(errorMessage) ? errorMessage : "登出失败: 系统异常";
                    _errorLabel.Text = "登出失败: " + errorText;
                    Logger.Error($"登出失败: {errorText}");
                    Logger.Info("程序检测机制已暂停");
                    UpdateUIState();
                    OnLogoutSuccess();
                }
            }
            catch (Exception ex)
            {
                string errorMessage = ex.Message;
                _errorLabel.Text = "登出失败: " + errorMessage;
                Logger.Error($"登出失败: {errorMessage}");
                Logger.Info("程序检测机制已暂停");
                UpdateUIState();
                OnLogoutSuccess();
            }
        }
        
        public void SetLoading(bool isLoading)
        {
            if (isLoading)
            {
                _loadingOverlay.Size = this.ClientSize;
                _loadingOverlay.Location = new Point(0, 0);
                
                if (_loadingOverlay.Controls.Count > 0 && _loadingOverlay.Controls[0] is Label overlayLabel)
                {
                    overlayLabel.Location = new Point(
                        (this.ClientSize.Width - overlayLabel.Width) / 2,
                        (this.ClientSize.Height - overlayLabel.Height) / 2
                    );
                }
                
                _loadingLabel.Visible = true;
                _errorLabel.Visible = false;
                _loginButton.Enabled = false;
                _loadingOverlay.Visible = true;
                _loadingOverlay.BringToFront();
            }
            else
            {
                _loadingLabel.Visible = false;
                _errorLabel.Visible = true;
                _loginButton.Enabled = true;
                _loadingOverlay.Visible = false;
            }
        }
        
        public void UpdateUIState()
        {
            this.Text = "密码管理";
            
            Logger.Info($"UpdateUIState被调用，当前IsLoggedIn状态: {GlobalState.Instance.IsLoggedIn}");
            
            if (GlobalState.Instance.IsLoggedIn)
            {
                _loginButton.Text = "注销";
                _loginButton.Click -= LoginButton_Click;
                _loginButton.Click -= LogoutButton_Click;
                _loginButton.Click += LogoutButton_Click;
                
                string userName = GlobalState.Instance.Name ?? GlobalState.Instance.Username;
                _userInfoLabel.Text = $"你好，{userName}";
                _userInfoLabel.Visible = true;
                _userInfoLabel.Location = new Point(this.ClientSize.Width - _userInfoLabel.Width - 20, 20);
                
                _usernameTextBox.Enabled = false;
                _passwordTextBox.Enabled = false;
                _domainTextBox.Enabled = false;
                _portTextBox.Enabled = false;
                
                _loginButton.Enabled = true;
                
                _errorLabel.Text = "";
            }
            else
            {
                _loginButton.Text = "登录";
                _loginButton.Click -= LoginButton_Click;
                _loginButton.Click -= LogoutButton_Click;
                _loginButton.Click += LoginButton_Click;
                
                _userInfoLabel.Visible = false;
                
                _usernameTextBox.Enabled = true;
                _passwordTextBox.Enabled = true;
                _domainTextBox.Enabled = true;
                _portTextBox.Enabled = true;
                
                _loginButton.Enabled = true;
                
                _errorLabel.Text = "";
            }
        }
        
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
        }

        private void LoginForm_Resize(object sender, EventArgs e)
        {
            // 移除自动隐藏功能，避免窗体无法显示的问题
            // 改为只保留最小化到任务栏的默认行为
        }
        
        private static async Task<(dynamic, string)> requestHandler(HttpMethod method, string url, string content = null)
        {
            using HttpClient httpClient = DynamicHttpClientManager.CreateClientWithTimeout(TimeSpan.FromSeconds(5));
            
            try
            {
                Logger.Info($"开始处理请求: {method} {url}");
                
                string fullUrl = url;
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    Logger.Info("构建服务器地址");
                    string serverAddress = GlobalState.Instance.GetServerAddress();
                    fullUrl = $"{serverAddress}{url}";
                    Logger.Info($"完整请求地址: {fullUrl}");
                }
                
                Logger.Info("设置请求超时时间为5秒");
                
                var request = new HttpRequestMessage(method, fullUrl);
                Logger.Info("创建请求消息");
                
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                Logger.Info("设置请求头");
                
                string token = GlobalState.Instance.Token;
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Add("token", token);
                    Logger.Info("添加token请求头");
                }
                
                if (!string.IsNullOrEmpty(content))
                {
                    request.Content = new StringContent(content, Encoding.UTF8, "application/json");
                    Logger.Info("添加请求内容");
                }
                
                Logger.Info("发送请求");
                var response = await httpClient.SendAsync(request);
                Logger.Info($"收到响应，状态码: {response.StatusCode}");
                
                string responseContent = await response.Content.ReadAsStringAsync();
                Logger.Info($"响应内容: {responseContent}");
                
                var jsonDocument = JsonDocument.Parse(responseContent);
                var root = jsonDocument.RootElement;
                Logger.Info("解析响应JSON");
                
                if (!response.IsSuccessStatusCode)
                {
                    string errorMessage = root.TryGetProperty("message", out var errorMessageElement) ? errorMessageElement.GetString() : "请求失败";
                    Logger.Error($"HTTP请求失败: {errorMessage}");
                    return (null, errorMessage);
                }
                
                int status = root.TryGetProperty("status", out var statusElement) ? statusElement.GetInt32() : 0;
                if (status != 200)
                {
                    string errorMessage = root.TryGetProperty("message", out var errorMessageElement) ? errorMessageElement.GetString() : "操作失败";
                    Logger.Error($"业务逻辑失败: {errorMessage}");
                    return (null, errorMessage);
                }
                
                string message = root.TryGetProperty("message", out var messageElement) ? messageElement.GetString() : "";
                string date = root.TryGetProperty("date", out var dateElement) ? dateElement.GetString() : "";
                JsonElement dataElement = root.TryGetProperty("data", out var data) ? data : default;
                
                var responseData = new {
                    status = status,
                    message = message,
                    date = date,
                    data = dataElement
                };
                
                Logger.Info("请求处理完成");
                return (responseData, null);
            }
            catch (InvalidOperationException ex) when (ex.Message == "服务器IP和端口未设置")
            {
                Logger.Error($"请求失败: {ex.Message}");
                throw;
            }
            catch (HttpRequestException ex)
            {
                Logger.Error($"网络请求失败: {ex.Message}");
                return (null, ex.Message);
            }
            catch (Exception ex)
            {
                Logger.Error($"请求处理失败: {ex.Message}");
                return (null, ex.Message);
            }
        }
        
        private void SaveLoginInfo(string username, string token)
        {
            try
            {
                Logger.Info("登录信息已保存到内存缓存");
            }
            catch (Exception ex)
            {
                Logger.Error($"保存登录信息失败: {ex.Message}");
            }
        }
        
        public static event EventHandler LoginSuccess;
        public static event EventHandler LogoutSuccess;

        private static void OnLoginSuccess()
        {
            try
            {
                LoginSuccess?.Invoke(null, EventArgs.Empty);
                Logger.Info("登录成功事件已触发");
            }
            catch (Exception ex)
            {
                Logger.Error($"触发登录成功事件失败: {ex.Message}");
            }
        }

        private static void OnLogoutSuccess()
        {
            try
            {
                LogoutSuccess?.Invoke(null, EventArgs.Empty);
                Logger.Info("注销成功事件已触发");
            }
            catch (Exception ex)
            {
                Logger.Error($"触发注销成功事件失败: {ex.Message}");
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
        
        public static async Task<(bool, string)> Logout()
        {
            try
            {
                var (response, errorMessage) = await requestHandler(HttpMethod.Post, "/account/logout");
                
                if (response != null)
                {
                    GlobalState.Instance.Reset();
                    GlobalState.Instance.ClearUserInfo();
                    return (true, null);
                }
                return (false, errorMessage);
            }
            catch (Exception ex)
            {
                string errorMessage = ex.Message;
                Logger.Error($"登出失败: {errorMessage}");
                GlobalState.Instance.Reset();
                GlobalState.Instance.ClearUserInfo();
                return (false, errorMessage);
            }
        }
        
        public static async Task<bool> RefreshToken()
        {
            try
            {
                var (response, errorMessage) = await requestHandler(HttpMethod.Post, "/account/refresh-token");
                
                if (response != null)
                {
                    string token = response.data.GetProperty("token").GetString();
                    string account = response.data.GetProperty("account").GetString();
                    string name = response.data.GetProperty("name").GetString();
                    string role = response.data.TryGetProperty("role", out JsonElement roleElement) ? roleElement.GetString() : null;
                    
                    GlobalState.Instance.Username = account;
                    GlobalState.Instance.Name = name;
                    GlobalState.Instance.Role = role;
                    GlobalState.Instance.Token = token;
                    Logger.Info($"Token刷新成功，用户: {account}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Token刷新失败: {ex.Message}");
                GlobalState.Instance.Reset();
                return false;
            }
        }
        
        public static async Task<bool> Heartbeat()
        {
            try
            {
                var (response, errorMessage) = await requestHandler(HttpMethod.Post, "/account/refresh-token");
                
                if (response != null)
                {
                    string token = response.data.GetProperty("token").GetString();
                    string account = response.data.GetProperty("account").GetString();
                    string name = response.data.GetProperty("name").GetString();
                    string role = response.data.TryGetProperty("role", out JsonElement roleElement) ? roleElement.GetString() : null;
                    
                    GlobalState.Instance.Username = account;
                    GlobalState.Instance.Name = name;
                    GlobalState.Instance.Role = role;
                    GlobalState.Instance.Token = token;
                    
                    Logger.Info("心跳检测成功");
                    GlobalState.Instance.IsLoggedIn = true;
                    Logger.Info("程序检测机制已开始运行");
                    return true;
                }
                else
                {
                    Logger.Info("心跳检测失败");
                    GlobalState.Instance.IsLoggedIn = false;
                    Logger.Info("程序检测机制已暂停");
                    GlobalState.Instance.Reset();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"心跳检测异常: {ex.Message}");
                GlobalState.Instance.IsLoggedIn = false;
                Logger.Info("程序检测机制已暂停");
                GlobalState.Instance.Reset();
                return false;
            }
        }
    }
}
