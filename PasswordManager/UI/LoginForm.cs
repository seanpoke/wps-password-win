using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using PasswordManager.Utils;

namespace PasswordManager.UI
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
        private Label _userInfoLabel;
        private Label _errorLabel;
        private Label _loadingLabel;
        private Panel _loadingOverlay;
        
        public LoginForm()
        {
            InitializeComponent();
            // 填充已有信息
            LoadSavedInfo();
        }
        
        private void LoadSavedInfo()
        {
            // 填充服务器信息
            if (!string.IsNullOrEmpty(GlobalState.Instance.ServerIp))
            {
                _domainTextBox.Text = GlobalState.Instance.ServerIp;
            }
            if (GlobalState.Instance.ServerPort > 0)
            {
                _portTextBox.Text = GlobalState.Instance.ServerPort.ToString();
            }
            
            // 填充用户信息
            if (!string.IsNullOrEmpty(GlobalState.Instance.Username))
            {
                _usernameTextBox.Text = GlobalState.Instance.Username;
            }
            
            // 更新界面状态
            UpdateUIState();
        }
        
        private void InitializeComponent()
        {
            this.Text = "密码管理";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new System.Drawing.Size(340, 360);
            this.BackColor = System.Drawing.Color.White;
            
            this.Resize += LoginForm_Resize;
            
            Font labelFont = new System.Drawing.Font("微软雅黑", 9F);
            Font inputFont = new System.Drawing.Font("微软雅黑", 9F);
            
            _userInfoLabel = new Label
            {
                Text = "",
                ForeColor = System.Drawing.Color.Green,
                Location = new System.Drawing.Point(180, 15),
                Size = new System.Drawing.Size(130, 20),
                TextAlign = System.Drawing.ContentAlignment.MiddleRight,
                Visible = false,
                Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold)
            };
            this.Controls.Add(_userInfoLabel);
            
            _usernameLabel = new Label
            {
                Text = "用户名:",
                Location = new System.Drawing.Point(25, 50),
                Size = new System.Drawing.Size(60, 20),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Font = labelFont,
                ForeColor = System.Drawing.Color.FromArgb(60, 60, 60)
            };
            this.Controls.Add(_usernameLabel);
            
            _usernameTextBox = new TextBox
            {
                Location = new System.Drawing.Point(90, 48),
                Size = new System.Drawing.Size(160, 24),
                Font = inputFont,
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(_usernameTextBox);
            
            _passwordLabel = new Label
            {
                Text = "密码:",
                Location = new System.Drawing.Point(25, 85),
                Size = new System.Drawing.Size(60, 20),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Font = labelFont,
                ForeColor = System.Drawing.Color.FromArgb(60, 60, 60)
            };
            this.Controls.Add(_passwordLabel);
            
            _passwordTextBox = new TextBox
            {
                Location = new System.Drawing.Point(90, 83),
                Size = new System.Drawing.Size(160, 24),
                PasswordChar = '*',
                Font = inputFont,
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(_passwordTextBox);
            
            _domainLabel = new Label
            {
                Text = "域名:",
                Location = new System.Drawing.Point(25, 120),
                Size = new System.Drawing.Size(60, 20),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Font = labelFont,
                ForeColor = System.Drawing.Color.FromArgb(60, 60, 60)
            };
            this.Controls.Add(_domainLabel);
            
            _domainTextBox = new TextBox
            {
                Location = new System.Drawing.Point(90, 118),
                Size = new System.Drawing.Size(160, 24),
                Text = "localhost",
                Font = inputFont,
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(_domainTextBox);
            
            _portLabel = new Label
            {
                Text = "端口:",
                Location = new System.Drawing.Point(25, 155),
                Size = new System.Drawing.Size(60, 20),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Font = labelFont,
                ForeColor = System.Drawing.Color.FromArgb(60, 60, 60)
            };
            this.Controls.Add(_portLabel);
            
            _portTextBox = new TextBox
            {
                Location = new System.Drawing.Point(90, 153),
                Size = new System.Drawing.Size(100, 24),
                Text = "8081",
                Font = inputFont,
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(_portTextBox);
            
            _errorLabel = new Label
            {
                Text = "",
                ForeColor = System.Drawing.Color.Red,
                Location = new System.Drawing.Point(25, 190),
                Size = new System.Drawing.Size(280, 40),
                AutoSize = false,
                TextAlign = System.Drawing.ContentAlignment.TopLeft,
                Font = new System.Drawing.Font("微软雅黑", 9F)
            };
            this.Controls.Add(_errorLabel);
            
            _loadingLabel = new Label
            {
                Text = "正在验证身份...",
                ForeColor = System.Drawing.Color.Blue,
                Location = new System.Drawing.Point(25, 190),
                Size = new System.Drawing.Size(280, 40),
                AutoSize = false,
                TextAlign = System.Drawing.ContentAlignment.TopLeft,
                Visible = false,
                Font = new System.Drawing.Font("微软雅黑", 9F)
            };
            this.Controls.Add(_loadingLabel);
            
            _loginButton = new Button
            {
                Text = "登录",
                Location = new System.Drawing.Point(230, 290),
                Size = new System.Drawing.Size(80, 28),
                Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold),
                BackColor = System.Drawing.Color.FromArgb(0, 120, 212),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _loginButton.Click += LoginButton_Click;
            _loginButton.FlatAppearance.BorderSize = 0;
            _loginButton.MouseEnter += (sender, e) => _loginButton.BackColor = System.Drawing.Color.FromArgb(26, 115, 232);
            _loginButton.MouseLeave += (sender, e) => _loginButton.BackColor = System.Drawing.Color.FromArgb(0, 120, 212);
            _loginButton.MouseDown += (sender, e) => _loginButton.BackColor = System.Drawing.Color.FromArgb(0, 90, 170);
            _loginButton.MouseUp += (sender, e) => _loginButton.BackColor = System.Drawing.Color.FromArgb(26, 115, 232);
            this.Controls.Add(_loginButton);
            
            _loadingOverlay = new Panel
            {
                BackColor = System.Drawing.Color.FromArgb(128, 0, 0, 0),
                Size = this.Size,
                Location = new System.Drawing.Point(0, 0),
                Visible = false
            };
            
            Label overlayLoadingLabel = new Label
            {
                Text = "正在登录",
                ForeColor = System.Drawing.Color.White,
                AutoSize = true,
                Font = new System.Drawing.Font("微软雅黑", 12F)
            };
            _loadingOverlay.Controls.Add(overlayLoadingLabel);
            overlayLoadingLabel.Location = new System.Drawing.Point(
                (this.ClientSize.Width - overlayLoadingLabel.Width) / 2,
                (this.ClientSize.Height - overlayLoadingLabel.Height) / 2
            );
            this.Controls.Add(_loadingOverlay);
            
            UpdateUIState();
        }
        
        private async void LoginButton_Click(object sender, EventArgs e)
        {
            // 检查当前是否已登录
            if (GlobalState.Instance.IsLoggedIn)
            {
                // 已登录状态，执行注销操作
                LogoutButton_Click(sender, e);
                return;
            }
            
            // 未登录状态，执行登录操作
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
            
            // 禁用登录按钮，防止重复点击
            _loginButton.Enabled = false;
            
            try
            {
                // 先设置服务器地址到全局状态
                GlobalState.Instance.ServerIp = domain;
                GlobalState.Instance.ServerPort = int.Parse(port);
                
                // 构建登录请求参数
                var loginData = new { account = username, password = password };
                string jsonContent = JsonSerializer.Serialize(loginData);
                
                // 调用登录接口
                var (response, errorMessage) = await requestHandler(HttpMethod.Post, "/account/login", jsonContent);
                
                if (response != null)
                {
                    // 解析响应数据
                    string token = response.data.GetProperty("token").GetString();
                    string account = response.data.GetProperty("account").GetString();
                    string name = response.data.GetProperty("name").GetString();
                    
                    // 存储登录信息到全局状态
                    GlobalState.Instance.Username = account;
                    GlobalState.Instance.Name = name;
                    GlobalState.Instance.Token = token;
                    GlobalState.Instance.IsLoggedIn = true;
                    
                    // 保存用户信息到本地存储
                    GlobalState.Instance.SaveUserInfo();
                    // 保存配置信息到本地存储
                    GlobalState.Instance.SaveConfig();
                    
                    // 记录登录成功日志
                    Logger.Info($"用户 {account} 登录成功，token: {token}");
                    
                    // 登录成功，设置IsLoggedIn=true（程序检测机制运行）
                    Logger.Info($"登录成功前IsLoggedIn状态: {GlobalState.Instance.IsLoggedIn}");
                    GlobalState.Instance.IsLoggedIn = true;
                    Logger.Info($"登录成功后IsLoggedIn状态: {GlobalState.Instance.IsLoggedIn}");
                    Logger.Info("用户登录成功，程序检测机制已开始运行");
                    
                    // 设置DialogResult为OK并关闭对话框
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    // 服务器返回错误或网络请求失败，优先使用服务端返回的错误信息
                    string errorText = !string.IsNullOrEmpty(errorMessage) ? errorMessage : "服务器返回错误或网络连接失败";
                    _errorLabel.Text = "登录失败: " + errorText;
                    Logger.Error($"登录失败: {errorText}");
                    // 登录失败，设置IsPaused=true（程序检测机制暂停）
                    GlobalState.Instance.IsLoggedIn = false;
                    Logger.Info("程序检测机制已暂停");
                    // 启用登录按钮
                    _loginButton.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                _errorLabel.Text = "登录失败: " + ex.Message;
                Logger.Error($"登录失败: {ex.Message}");
                // 登录失败，设置IsPaused=true（程序检测机制暂停）
                GlobalState.Instance.IsLoggedIn = false;
                Logger.Info("程序检测机制已暂停");
                // 启用登录按钮
                _loginButton.Enabled = true;
            }
        }
        
        private async void LogoutButton_Click(object sender, EventArgs e)
        {
            // 禁用注销按钮，防止重复点击
            _loginButton.Enabled = false;
            
            try
            {
                GlobalState.Instance.IsLoggedIn = false;
                
                // 2. 调用登出接口
                var (logoutSuccess, errorMessage) = await Logout();
            
                Logger.Info("程序检测机制已关闭");

                if (logoutSuccess)
                {
                    Logger.Info("程序检测机制已暂停");
                    // 5. 更新界面状态，切换到未登录状态
                    UpdateUIState();
                    Logger.Info("用户登出成功，资源已清理");
                }
                else
                {
                    // 登出失败，显示错误信息
                    string errorText = !string.IsNullOrEmpty(errorMessage) ? errorMessage : "登出失败: 系统异常";
                    _errorLabel.Text = "登出失败: " + errorText;
                    Logger.Error($"登出失败: {errorText}");
                    Logger.Info("程序检测机制已暂停");
                    // 即使登出失败，也更新界面状态，切换到未登录状态
                    UpdateUIState();
                }
            }
            catch (Exception ex)
            {
                string errorMessage = ex.Message;
                _errorLabel.Text = "登出失败: " + errorMessage;
                Logger.Error($"登出失败: {errorMessage}");
                Logger.Info("程序检测机制已暂停");
                // 即使发生异常，也更新界面状态，切换到未登录状态
                UpdateUIState();
            }
        }
        
        /// <summary>
        /// 设置加载状态
        /// </summary>
        /// <param name="isLoading">是否处于加载状态</param>
        public void SetLoading(bool isLoading)
        {
            if (isLoading)
            {
                _loadingLabel.Visible = true;
                _errorLabel.Visible = false;
                _loginButton.Enabled = false;
                _loadingOverlay.Visible = true;
                // 确保遮罩在最上层
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
            // 固定窗口标题为【密码管理】
            this.Text = "密码管理";
            
            Logger.Info($"UpdateUIState被调用，当前IsLoggedIn状态: {GlobalState.Instance.IsLoggedIn}");
            
            if (GlobalState.Instance.IsLoggedIn)
            {
                // 已登录状态
                _loginButton.Text = "注销";
                
                // 先移除所有事件处理器，避免重复添加
                _loginButton.Click -= LoginButton_Click;
                _loginButton.Click -= LogoutButton_Click;
                // 只添加注销事件处理器
                _loginButton.Click += LogoutButton_Click;
                
                // 显示用户信息
                string userName = GlobalState.Instance.Name ?? GlobalState.Instance.Username;
                _userInfoLabel.Text = $"你好，{userName}";
                _userInfoLabel.Visible = true;
                
                // 禁用输入框
                _usernameTextBox.Enabled = false;
                _passwordTextBox.Enabled = false;
                _domainTextBox.Enabled = false;
                _portTextBox.Enabled = false;
                
                // 启用注销按钮
                _loginButton.Enabled = true;
                
                _errorLabel.Text = "";
            }
            else
            {
                // 未登录状态
                _loginButton.Text = "登录";
                
                // 先移除所有事件处理器，避免重复添加
                _loginButton.Click -= LoginButton_Click;
                _loginButton.Click -= LogoutButton_Click;
                // 只添加登录事件处理器
                _loginButton.Click += LoginButton_Click;
                
                // 隐藏用户信息
                _userInfoLabel.Visible = false;
                
                // 启用输入框
                _usernameTextBox.Enabled = true;
                _passwordTextBox.Enabled = true;
                _domainTextBox.Enabled = true;
                _portTextBox.Enabled = true;
                
                // 启用登录按钮
                _loginButton.Enabled = true;
                
                _errorLabel.Text = "";
            }
        }
        
        private void LoginForm_Resize(object sender, EventArgs e)
        {
            // 当窗口最小化时，只在非对话框模式下隐藏窗口
            // 避免在ShowDialog模式下最小化后窗口消失的问题
            if (this.WindowState == FormWindowState.Minimized && !this.Modal)
            {
                this.Hide();
                // 确保系统托盘图标显示
                // 这里可以添加系统托盘相关的代码，如果需要的话
            }
        }
        
        /// <summary>
        /// 通用HTTP请求处理方法
        /// </summary>
        /// <param name="method">HTTP请求方法</param>
        /// <param name="url">请求地址（可以是完整URL或相对路径）</param>
        /// <param name="content">请求内容（JSON格式）</param>
        /// <returns>响应数据对象和错误信息</returns>
        private static async Task<(dynamic, string)> requestHandler(HttpMethod method, string url, string content = null)
        {
            // 创建新的HttpClient实例
            using var httpClient = new HttpClient();
            
            try
            {
                Logger.Info($"开始处理请求: {method} {url}");
                
                // 构建完整的请求地址
                string fullUrl = url;
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    // 使用全局状态中的服务器地址
                    Logger.Info("构建服务器地址");
                    string serverAddress = GlobalState.Instance.GetServerAddress();
                    fullUrl = $"{serverAddress}{url}";
                    Logger.Info($"完整请求地址: {fullUrl}");
                }
                
                // 设置请求超时时间为5秒
                httpClient.Timeout = TimeSpan.FromSeconds(5);
                Logger.Info("设置请求超时时间为5秒");
                
                // 创建请求消息
                var request = new HttpRequestMessage(method, fullUrl);
                Logger.Info("创建请求消息");
                
                // 设置请求头
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                Logger.Info("设置请求头");
                
                // 添加token请求头（如果有token）
                string token = GlobalState.Instance.Token;
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Add("token", token);
                    Logger.Info("添加token请求头");
                }
                
                // 添加请求内容（如果有）
                if (!string.IsNullOrEmpty(content))
                {
                    request.Content = new StringContent(content, Encoding.UTF8, "application/json");
                    Logger.Info("添加请求内容");
                }
                
                // 发送请求并获取响应
                Logger.Info("发送请求");
                var response = await httpClient.SendAsync(request);
                Logger.Info($"收到响应，状态码: {response.StatusCode}");
                
                // 读取响应内容
                string responseContent = await response.Content.ReadAsStringAsync();
                Logger.Info($"响应内容: {responseContent}");
                
                // 解析响应JSON
                var jsonDocument = JsonDocument.Parse(responseContent);
                var root = jsonDocument.RootElement;
                Logger.Info("解析响应JSON");
                
                // 处理HTTP状态码
                if (!response.IsSuccessStatusCode)
                {
                    string errorMessage = root.TryGetProperty("message", out var errorMessageElement) ? errorMessageElement.GetString() : "请求失败";
                    Logger.Error($"HTTP请求失败: {errorMessage}");
                    return (null, errorMessage);
                }
                
                // 处理业务逻辑状态码
                int status = root.TryGetProperty("status", out var statusElement) ? statusElement.GetInt32() : 0;
                if (status != 200)
                {
                    string errorMessage = root.TryGetProperty("message", out var errorMessageElement) ? errorMessageElement.GetString() : "操作失败";
                    Logger.Error($"业务逻辑失败: {errorMessage}");
                    return (null, errorMessage);
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
                
                Logger.Info("请求处理完成");
                return (responseData, null);
            }
            catch (InvalidOperationException ex) when (ex.Message == "服务器IP和端口未设置")
            {
                // 服务器IP和端口未设置，重定向到登录流程
                Logger.Error($"请求失败: {ex.Message}");
                throw;
            }
            catch (HttpRequestException ex)
            {
                // 网络错误处理
                Logger.Error($"网络请求失败: {ex.Message}");
                return (null, ex.Message);
            }
            catch (Exception ex)
            {
                // 其他错误处理
                Logger.Error($"请求处理失败: {ex.Message}");
                return (null, ex.Message);
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
        
        /// <summary>
        /// 登出方法
        /// </summary>
        public static async Task<(bool, string)> Logout()
        {
            try
            {
                // 调用登出接口
                var (response, errorMessage) = await requestHandler(HttpMethod.Post, "/account/logout");
                
                if (response != null)
                {
                    // 清除登录状态
                    GlobalState.Instance.Reset();
                    // 清除本地存储中的用户信息
                    GlobalState.Instance.ClearUserInfo();
                    return (true, null);
                }
                return (false, errorMessage);
            }
            catch (Exception ex)
            {
                string errorMessage = ex.Message;
                Logger.Error($"登出失败: {errorMessage}");
                // 即使接口调用失败，也清除本地状态
                GlobalState.Instance.Reset();
                // 清除本地存储中的用户信息
                GlobalState.Instance.ClearUserInfo();
                return (false, errorMessage);
            }
        }
        
        /// <summary>
        /// 刷新token方法
        /// </summary>
        public static async Task<bool> RefreshToken()
        {
            try
            {
                // 调用刷新token接口
                var (response, errorMessage) = await requestHandler(HttpMethod.Post, "/account/refresh-token");
                
                if (response != null)
                {
                    // 解析响应数据
                    string token = response.data.GetProperty("token").GetString();
                    string account = response.data.GetProperty("account").GetString();
                    string name = response.data.GetProperty("name").GetString();
                    
                    // 更新token和用户信息
                    GlobalState.Instance.Username = account;
                    GlobalState.Instance.Name = name;
                    GlobalState.Instance.Token = token;
                    Logger.Info($"Token刷新成功，用户: {account}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Token刷新失败: {ex.Message}");
                // 刷新失败，清除登录状态
                GlobalState.Instance.Reset();
                return false;
            }
        }
        
        /// <summary>
        /// 心跳检测方法
        /// </summary>
        public static async Task<bool> Heartbeat()
        {
            try
            {
                // 调用心跳保活接口
                var (response, errorMessage) = await requestHandler(HttpMethod.Post, "/account/refresh-token");
                
                if (response != null)
                {
                    // 解析响应数据
                    string token = response.data.GetProperty("token").GetString();
                    string account = response.data.GetProperty("account").GetString();
                    string name = response.data.GetProperty("name").GetString();
                    
                    // 更新token和用户信息
                    GlobalState.Instance.Username = account;
                    GlobalState.Instance.Name = name;
                    GlobalState.Instance.Token = token;
                    
                    Logger.Info("心跳检测成功");
                    // 心跳成功，设置IsPaused=false（程序检测机制运行）
                    GlobalState.Instance.IsLoggedIn = true;
                    Logger.Info("程序检测机制已开始运行");
                    return true;
                }
                else
                {
                    Logger.Info("心跳检测失败");
                    // 心跳失败，设置IsPaused=true（程序检测机制暂停）
                    GlobalState.Instance.IsLoggedIn = false;
                    Logger.Info("程序检测机制已暂停");
                    // 清除登录状态
                    GlobalState.Instance.Reset();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"心跳检测异常: {ex.Message}");
                // 心跳异常，设置IsPaused=true（程序检测机制暂停）
                GlobalState.Instance.IsLoggedIn = false;
                Logger.Info("程序检测机制已暂停");
                // 清除登录状态
                GlobalState.Instance.Reset();
                return false;
            }
        }
    }
}