using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using PasswordManager.Utils;
using PasswordManager.UI.Controls;

namespace PasswordManager.UI
{
    /// <summary>
    /// 登录窗口（严格还原原型 app-prototype.html SCREEN 1「登录」）。
    /// 布局基准：窗口 420px 宽、content padding 32px（内容区 356px）、96DPI 逻辑像素。
    /// </summary>
    public class LoginForm : ThemedWindow
    {
        private static int WinWidth => Theme.S(420);       // .window{width:min(420px,94vw)}
        private static int ContentX => Theme.S(32);        // .content padding 32
        private static int ContentWidth => Theme.S(356);   // 420 - 32*2

        // ---------- 登录态控件 ----------
        private Label _headerTitle = null!;   // h1 密码管理 18px/24px bold
        private Label _headerSub = null!;      // p 使用账号登录以继续 13px/18px #606060
        private Label _userLabel = null!;
        private ThemedTextBox _userBox = null!;
        private Label _userError = null!;      // 12px/16px #C42B1C
        private Label _passLabel = null!;
        private ThemedTextBox _passBox = null!;
        private Label _passError = null!;
        private Label _capsHint = null!;       // 大写锁定已开启 12px/16px #878787
        private ExpanderPanel _expander = null!;
        private Label _domainLabel = null!;
        private ThemedTextBox _domainBox = null!;
        private Label _portLabel = null!;
        private ThemedTextBox _portBox = null!;
        private InfoBanner _banner = null!;     // 登录错误横幅 #FDE7E9
        private AppCheckBox _rememberBox = null!;
        private ThemedButton _loginButton = null!;
        private LinkLabelEx _changePwdLink = null!;

        // ---------- 已登录态控件（对应原型 SCREEN 2「账户信息」） ----------
        private Label _homeTitle = null!;      // 你好，{name}
        private Label _homeSub = null!;         // 已登录 · 账号 {account}
        private InfoCard _infoCard = null!;     // 用户名/密码/域名/端口信息卡
        private SignOutCard _signOutCard = null!;  // 注销确认卡（点"注销"后显示）
        private bool _confirmShown;             // 注销确认是否展开
        private ThemedButton _goPwdButton = null!;
        private ThemedButton _logoutButton = null!;

        private bool _initialized;   // 构造期间抑制 CheckedChanged 回写

        /// <summary>
        /// 开发者模式：双击登录页大标题「密码管理」切换，运行时生效、不持久化。
        /// 开启后域名允许显式 http:// 协议（无协议仍自动补 https://）。
        /// </summary>
        public static bool DeveloperMode { get; private set; }

        public LoginForm()
        {
            InitializeComponent();
            LoadSavedInfo();
            _initialized = true;
            UpdateUIState();
        }

        private void LoadSavedInfo()
        {
            // 域名只显示主机地址，不带 http:// / https:// 前缀（协议沿用已保存配置）
            string host = GlobalState.Instance.RawDomain;
            if (string.IsNullOrEmpty(host) && GlobalState.Instance.ServerPort > 0)
            {
                host = GlobalState.Instance.ServerIp;
            }
            if (string.IsNullOrEmpty(host))
            {
                host = "localhost";
            }
            try { host = UrlParser.ExtractHost(host); } catch { }
            _domainBox.Inner.Text = host;
            _portBox.Inner.Text = GlobalState.Instance.ServerPort > 0
                ? GlobalState.Instance.ServerPort.ToString()
                : "8443";

            if (!string.IsNullOrEmpty(GlobalState.Instance.Username))
            {
                _userBox.Inner.Text = GlobalState.Instance.Username;
            }

            // 记住密码：读取本地缓存，勾选并回填
            var (remember, account, password) = StorageManager.LoadLoginCache();
            if (remember)
            {
                _rememberBox.Checked = true;
                if (!string.IsNullOrEmpty(account)) _userBox.Inner.Text = account;
                if (!string.IsNullOrEmpty(password)) _passBox.Inner.Text = password;
            }
        }

        // =====================================================================
        // UI 构建（原型 SCREEN 1 DOM 顺序：header / 用户名 / 密码 / 服务器设置
        // / banner / 记住密码 / 登录按钮 / 修改密码链接）
        // =====================================================================
        private void InitializeComponent()
        {
            Text = "密码管理";
            MaximizeBox = false;

            // --- header ---
            _headerTitle = MakeLabel("密码管理", Theme.H1, Theme.TextPrimary);
            _headerTitle.DoubleClick += HeaderTitle_DoubleClick;   // 双击切换开发者模式
            _headerSub = MakeLabel("请使用域账号登录", Theme.Body, Theme.TextSecondary);

            // --- 用户名 field ---
            _userLabel = MakeLabel("用户名", Theme.Body, Theme.TextSecondary);
            _userBox = new ThemedTextBox(mono: false, password: false, placeholder: "请输入用户名")
            {
                Bounds = new Rectangle(ContentX, 0, ContentWidth, ThemedTextBox.BoxHeight)
            };
            _userError = MakeLabel("请输入用户名", Theme.Small, Theme.Danger);
            _userError.Visible = false;

            // --- 密码 field ---
            _passLabel = MakeLabel("密码", Theme.Body, Theme.TextSecondary);
            _passBox = new ThemedTextBox(mono: false, password: true, placeholder: "请输入密码", eye: true)
            {
                Bounds = new Rectangle(ContentX, 0, ContentWidth, ThemedTextBox.BoxHeight)
            };
            _passError = MakeLabel("请输入密码", Theme.Small, Theme.Danger);
            _passError.Visible = false;
            _capsHint = MakeLabel("大写锁定已开启", Theme.Small, Theme.TextTertiary);
            _capsHint.Visible = false;

            // CapsLock 提示（原型 .caps-hint）
            _passBox.Inner.KeyDown += (_, _) => UpdateCapsHint();
            _passBox.Inner.KeyUp += (_, _) => UpdateCapsHint();
            _passBox.Inner.Leave += (_, _) => { _capsHint.Visible = false; ApplyLoginLayout(); };

            // --- 服务器设置 expander ---
            _expander = new ExpanderPanel
            {
                Text = "服务器设置",
                BodyContentHeight = Theme.S(132)   // 域名(60) + 12 + 端口(60)
            };
            _expander.SummaryValueFunc = () =>
            {
                string host = _domainBox.Inner.Text.Trim();
                try { host = UrlParser.ExtractHost(host); } catch { }
                string port = _portBox.Inner.Text.Trim();
                if (host.Length == 0) return port.Length > 0 ? ":" + port : "";
                return $"{host}:{port}";
            };
            _expander.Toggled += (_, _) => { SetExpanderBodyVisible(); ApplyLayout(); };

            // 域名/端口控件始终 Visible=true：折叠时由 expander 高度（34px）裁剪自然隐藏，
            // 避免切换 Visible 的时序问题（与原型 overflow hidden 语义一致）。
            _domainLabel = MakeLabel("域名", Theme.Body, Theme.TextSecondary);
            _domainBox = new ThemedTextBox(mono: true, password: false, placeholder: "10.8.2.83");

            _portLabel = MakeLabel("端口", Theme.Body, Theme.TextSecondary);
            _portBox = new ThemedTextBox(mono: true, password: false, placeholder: "8080");

            _expander.Controls.Add(_domainLabel);
            _expander.Controls.Add(_domainBox);
            _expander.Controls.Add(_portLabel);
            _expander.Controls.Add(_portBox);
            LayoutExpanderBody();

            _domainBox.Inner.TextChanged += (_, _) => _expander.Invalidate();
            _portBox.Inner.TextChanged += (_, _) => _expander.Invalidate();

            // --- 错误横幅（原型 .banner：#FDE7E9、圆角 4、min-height 60） ---
            _banner = new InfoBanner();

            // --- 记住密码（原型 .checkbox 行高 40） ---
            _rememberBox = new AppCheckBox("记住密码");
            _rememberBox.CheckedChanged += RememberBox_CheckedChanged;

            // --- 登录按钮（原型 .btn-primary：高 40、圆角 4、#005FB8） ---
            _loginButton = new ThemedButton("登录", ThemedButtonStyle.Primary);
            _loginButton.Click += LoginButton_Click;

            // --- 修改密码链接（原型 .link-center：13px #005FB8 居中） ---
            _changePwdLink = new LinkLabelEx("修改密码");
            _changePwdLink.Click += ChangePwdButton_Click;

            // --- 已登录态（原型 SCREEN 2「账户信息」） ---
            _homeTitle = MakeLabel("", Theme.H1, Theme.TextPrimary);
            _homeSub = MakeLabel("", Theme.Body, Theme.TextSecondary);
            _infoCard = new InfoCard();
            _signOutCard = new SignOutCard();
            _signOutCard.CancelClicked += (_, _) => { _confirmShown = false; ApplyLayout(); };
            _signOutCard.ConfirmClicked += (_, _) => LogoutButton_Click(this, EventArgs.Empty);
            _goPwdButton = new ThemedButton("修改密码", ThemedButtonStyle.Primary);
            _goPwdButton.Click += ChangePwdButton_Click;
            _logoutButton = new ThemedButton("注销", ThemedButtonStyle.SecondaryDanger);
            _logoutButton.Click += (_, _) => { _confirmShown = true; ApplyLayout(); };

            Controls.AddRange(new Control[]
            {
                _headerTitle, _headerSub,
                _userLabel, _userBox, _userError,
                _passLabel, _passBox, _passError, _capsHint,
                _expander, _banner, _rememberBox, _loginButton, _changePwdLink,
                _homeTitle, _homeSub, _goPwdButton, _logoutButton,
                _infoCard, _signOutCard
            });

            ApplyLayout();
        }

        private static Label MakeLabel(string text, Font font, Color color)
        {
            return new Label
            {
                Text = text,
                Font = font,
                ForeColor = color,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
        }

        // =====================================================================
        // 布局（登录态）：按原型垂直堆叠逐段计算，动态调整窗口高度
        // =====================================================================
        private void LayoutExpanderBody()
        {
            int x = _expander.BodyLeft;
            int w = _expander.BodyWidth;
            // label 行盒 18 → 容器 22（y-2），中心对齐
            _domainLabel.SetBounds(x, ExpanderPanel.BodyTop - Theme.S(2), w, Theme.S(22));
            _domainBox.SetBounds(x, ExpanderPanel.BodyTop + Theme.S(24), w, ThemedTextBox.BoxHeight);
            _portLabel.SetBounds(x, ExpanderPanel.BodyTop + Theme.S(72) - Theme.S(2), w, Theme.S(22));
            _portBox.SetBounds(x, ExpanderPanel.BodyTop + Theme.S(96), w, ThemedTextBox.BoxHeight);
        }

        private void SetExpanderBodyVisible()
        {
            // 域名/端口控件始终可见，折叠时由 expander 高度裁剪隐藏；此方法保留为空以兼容调用点。
        }

        private void UpdateCapsHint()
        {
            bool caps = Control.IsKeyLocked(Keys.CapsLock);
            if (_capsHint.Visible != caps)
            {
                _capsHint.Visible = caps;
                ApplyLoginLayout();
            }
        }

        private void ApplyLayout()
        {
            if (GlobalState.Instance.IsLoggedIn) ApplyHomeLayout();
            else ApplyLoginLayout();
        }

        private void ShowLoginControls(bool show)
        {
            // 注意：错误提示（_userError/_passError/_capsHint/_banner）不在此切换，
            // 它们的可见性由 ClearFieldErrors / 校验分支独立管理。
            foreach (var c in new Control[] { _headerTitle, _headerSub, _userLabel, _userBox,
                                              _passLabel, _passBox,
                                              _expander, _rememberBox, _loginButton, _changePwdLink })
                c.Visible = show;
            SetExpanderBodyVisible();
            foreach (var c in new Control[] { _homeTitle, _homeSub, _goPwdButton, _logoutButton })
                c.Visible = !show;
        }

        private void ApplyLoginLayout()
        {
            ShowLoginControls(true);

            // 已登录态专属控件移出可视区，避免与登录控件重叠
            _infoCard.SetBounds(ContentX, -Theme.S(900), ContentWidth, _infoCard.GetPreferredHeight());
            _signOutCard.SetBounds(ContentX, -Theme.S(900), ContentWidth, _signOutCard.GetPreferredHeight());

            int y = TitleBarHeight + ContentPadding;

            // 文本容器统一比行盒加高（居中对齐保持原行盒位置），避免 GDI 中文行高溢出被裁：
            // h1 行盒 24 → 容器 32（y-4）；13px 行盒 18 → 容器 22（y-2）；12px 行盒 16 → 容器 20（y-2）。
            // header：h1(24) + 4 + p(18) + margin-bottom 24
            _headerTitle.SetBounds(ContentX, y - Theme.S(4), ContentWidth, Theme.S(32)); y += Theme.S(24 + 4);
            _headerSub.SetBounds(ContentX, y - Theme.S(2), ContentWidth, Theme.S(22)); y += Theme.S(18 + 24);

            // 用户名 field：label(18) + 6 + box(36) [+error 4+16] + margin-bottom 16
            // 错误标签无条件定位（Visible getter 在窗体显示前不可靠），占位用 HasError 状态判断
            _userLabel.SetBounds(ContentX, y - Theme.S(2), ContentWidth, Theme.S(22)); y += Theme.S(18 + 6);
            _userBox.SetBounds(ContentX, y, ContentWidth, ThemedTextBox.BoxHeight); y += ThemedTextBox.BoxHeight;
            _userError.SetBounds(ContentX, y + Theme.S(2), ContentWidth, Theme.S(20));
            if (_userBox.HasError) y += Theme.S(4 + 16);
            y += Theme.S(16);

            // 密码 field
            _passLabel.SetBounds(ContentX, y - Theme.S(2), ContentWidth, Theme.S(22)); y += Theme.S(18 + 6);
            _passBox.SetBounds(ContentX, y, ContentWidth, ThemedTextBox.BoxHeight); y += ThemedTextBox.BoxHeight;
            _passError.SetBounds(ContentX, y + Theme.S(2), ContentWidth, Theme.S(20));
            if (_passBox.HasError) y += Theme.S(4 + 16);
            _capsHint.SetBounds(ContentX, y + Theme.S(2) + (_passBox.HasError ? Theme.S(20) : 0), ContentWidth, Theme.S(20));
            if (_capsHint.Visible) y += Theme.S(4 + 16) + (_passBox.HasError ? Theme.S(20) : 0);
            y += Theme.S(16);

            // 服务器设置 expander
            int eh = _expander.GetPreferredHeight();
            _expander.SetBounds(ContentX, y, ContentWidth, eh);
            LayoutExpanderBody();   // 以最终宽度重新摆放 body 子控件
            y += eh + Theme.S(16);

            // banner（显示时插入：min-height 60 + margin-bottom 16）
            // 未显示时移出可视区，避免透明控件残留原位遮挡下方控件
            if (_banner.IsShown)
            {
                _banner.SetBounds(ContentX, y, ContentWidth, Theme.S(60));
                int bh = _banner.UpdateHeight();
                _banner.Height = bh;
                y += bh + Theme.S(16);
            }
            else
            {
                _banner.SetBounds(ContentX, -Theme.S(500), ContentWidth, Theme.S(60));
            }

            // 记住密码行：checkbox 40px 高，row margin 0 0 20px
            _rememberBox.SetBounds(ContentX, y, ContentWidth, AppCheckBox.RowHeight); y += Theme.S(40 + 20);

            // 登录按钮
            _loginButton.SetBounds(ContentX, y, ContentWidth, ThemedButton.ButtonHeight); y += Theme.S(40 + 16);

            // 修改密码链接（行盒 18 → 容器 22）
            _changePwdLink.SetBounds(ContentX, y - Theme.S(2), ContentWidth, Theme.S(22)); y += Theme.S(18);

            ClientSize = new Size(WinWidth, y + ContentPadding);
        }

        private void ApplyHomeLayout()
        {
            ShowLoginControls(false);

            int y = TitleBarHeight + ContentPadding;

            // header：h1 + 4 + p，margin-bottom 8（容器加高防裁剪）
            _homeTitle.SetBounds(ContentX, y - Theme.S(4), ContentWidth, Theme.S(32)); y += Theme.S(24 + 4);
            _homeSub.SetBounds(ContentX, y - Theme.S(2), ContentWidth, Theme.S(22)); y += Theme.S(18 + 8);

            // 信息卡（原型 .card：margin 16 0 20）
            y += Theme.S(16);
            RebuildInfoCard();
            _infoCard.SetBounds(ContentX, y, ContentWidth, _infoCard.GetPreferredHeight());
            y += _infoCard.Height + Theme.S(20);

            if (_confirmShown)
            {
                // 注销确认卡（原型 .confirm：margin-bottom 12），此时隐藏动作按钮
                _signOutCard.SetBounds(ContentX, y, ContentWidth, _signOutCard.GetPreferredHeight());
                y += _signOutCard.Height + Theme.S(12);
                _goPwdButton.SetBounds(ContentX, -Theme.S(600), ContentWidth, ThemedButton.ButtonHeight);
                _logoutButton.SetBounds(ContentX, -Theme.S(600), ContentWidth, ThemedButton.ButtonHeight);
            }
            else
            {
                _signOutCard.SetBounds(ContentX, -Theme.S(600), ContentWidth, _signOutCard.GetPreferredHeight());
                // 动作区（原型 .actions：flex-direction:column, gap:10px）
                _goPwdButton.SetBounds(ContentX, y, ContentWidth, ThemedButton.ButtonHeight); y += Theme.S(40 + 10);
                _logoutButton.SetBounds(ContentX, y, ContentWidth, ThemedButton.ButtonHeight); y += Theme.S(40);
            }

            ClientSize = new Size(WinWidth, y + ContentPadding);
        }

        /// <summary>按当前登录状态填充信息卡（原型 .card 的四行）。</summary>
        private void RebuildInfoCard()
        {
            _infoCard.Clear();
            _infoCard.AddRow("用户名", GlobalState.Instance.Username ?? "");

            // 密码：黑点掩码展示，无小眼睛（与原型一致）
            _infoCard.AddRow("密码", "••••••••", masked: true);

            bool hasServer = !string.IsNullOrEmpty(GlobalState.Instance.ServerIp);
            _infoCard.AddRow("域名",
                hasServer ? $"{GlobalState.Instance.Protocol}://{GlobalState.Instance.ServerIp}" : "—",
                mono: true, empty: !hasServer);
            _infoCard.AddRow("端口",
                GlobalState.Instance.ServerPort > 0 ? GlobalState.Instance.ServerPort.ToString() : "—",
                mono: true, empty: GlobalState.Instance.ServerPort <= 0);
            _infoCard.Invalidate();
        }

        // =====================================================================
        // 交互 / 状态
        // =====================================================================
        private void HeaderTitle_DoubleClick(object? sender, EventArgs e)
        {
            DeveloperMode = !DeveloperMode;
            // 开发者模式：开启后输出 DEBUG 日志，关闭恢复 Info
            Logger.SetMinLevel(DeveloperMode ? LogLevel.Debug : LogLevel.Info);
            Logger.Info($"开发者模式已{(DeveloperMode ? "开启" : "关闭")}（日志级别：{Logger.CurrentLevel}）");
            _headerSub.Text = DeveloperMode ? "请使用域账号登录（开发者模式）" : "请使用域账号登录";
        }

        private void RememberBox_CheckedChanged(object? sender, EventArgs e)
        {
            if (!_initialized) return;
            try
            {
                if (_rememberBox.Checked)
                {
                    // 勾选：先记住状态（密码登录成功后补写）
                    StorageManager.SaveLoginCache(true, _userBox.Inner.Text.Trim(), null);
                }
                else
                {
                    // 取消勾选：清除已记住的密码
                    StorageManager.SaveLoginCache(false, "", null);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"保存记住密码状态失败: {ex.Message}");
            }
        }

        private void ClearFieldErrors()
        {
            _userError.Visible = false;
            _userBox.HasError = false;
            _passError.Visible = false;
            _passBox.HasError = false;
        }

        private void SetLoginLoading(bool loading)
        {
            _loginButton.Text = loading ? "正在登录…" : "登录";
            _loginButton.Loading = loading;
            bool enable = !loading;
            _userBox.Enabled = enable;
            _passBox.Enabled = enable;
            _domainBox.Enabled = enable;
            _portBox.Enabled = enable;
            _rememberBox.Enabled = enable;
            _changePwdLink.Enabled = enable;
        }

        /// <summary>外部心跳流程调用的加载状态。</summary>
        public void SetLoading(bool isLoading)
        {
            if (GlobalState.Instance.IsLoggedIn) return;
            SetLoginLoading(isLoading);
        }

        // =====================================================================
        // 登录 / 注销 / 修改密码（业务逻辑与原版一致）
        // =====================================================================
        private async void LoginButton_Click(object? sender, EventArgs e)
        {
            if (GlobalState.Instance.IsLoggedIn)
            {
                LogoutButton_Click(sender, e);
                return;
            }

            ClearFieldErrors();
            if (_banner.IsShown)
            {
                _banner.Hide();
                ApplyLoginLayout();
            }

            string username = _userBox.Inner.Text.Trim();
            string password = _passBox.Inner.Text;
            string domain = _domainBox.Inner.Text.Trim();
            string port = _portBox.Inner.Text.Trim();

            if (string.IsNullOrEmpty(username))
            {
                _userError.Visible = true;
                _userBox.HasError = true;
                ApplyLoginLayout();
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                _passError.Visible = true;
                _passBox.HasError = true;
                ApplyLoginLayout();
                return;
            }

            int serverPort;
            if (string.IsNullOrEmpty(domain) || !int.TryParse(port, out serverPort))
            {
                // 展开服务器设置并给出横幅提示
                if (!_expander.IsOpen) _expander.Toggle();
                SetExpanderBodyVisible();
                string msg = string.IsNullOrEmpty(domain) ? "请输入域名。" :
                    string.IsNullOrEmpty(port) ? "请输入端口。" : "请输入有效的端口号。";
                _banner.Show(msg);
                ApplyLoginLayout();
                return;
            }

            // 域名协议解析：无协议自动补 https://；显式协议仅允许 https（开发者模式额外允许 http）
            if (!UrlParser.TryResolveDomain(domain, allowHttp: DeveloperMode,
                    out string host, out string protocol, out string domainError))
            {
                if (!_expander.IsOpen) _expander.Toggle();
                SetExpanderBodyVisible();
                _banner.Show(domainError);
                ApplyLoginLayout();
                return;
            }

            SetLoginLoading(true);

            try
            {
                GlobalState.Instance.ServerIp = host;
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
                    bool needChangePwd = response.data.TryGetProperty("needChangePwd", out JsonElement ncpElement) && ncpElement.ValueKind == JsonValueKind.True;

                    // 首次登录需强制改密：仅使用登录返回的临时 token 完成改密，
                    // 不存储任何用户信息与 token（不调用 SaveUserInfo / SaveConfig，不设置 IsLoggedIn）。
                    if (needChangePwd)
                    {
                        Logger.Info($"用户 {account} 首次登录需修改密码(needChangePwd=true)，弹出重置密码窗口");
                        SetLoginLoading(false);
                        var changePwdForm = new ChangePasswordForm(account, password, token);
                        changePwdForm.ShowDialog(this);
                        return;
                    }

                    GlobalState.Instance.Username = account;
                    GlobalState.Instance.Name = name;
                    GlobalState.Instance.Role = role;
                    GlobalState.Instance.Token = token;
                    GlobalState.Instance.IsLoggedIn = true;

                    GlobalState.Instance.SaveUserInfo();
                    GlobalState.Instance.SaveConfig();

                    // 登录缓存：密码总是加密保存（供"账户信息"页明文展示），remember 控制启动预填
                    try
                    {
                        StorageManager.SaveLoginCache(_rememberBox.Checked, account, password);
                    }
                    catch (Exception cacheEx)
                    {
                        Logger.Error($"保存记住密码状态失败: {cacheEx.Message}");
                    }

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
                    string errorText = !string.IsNullOrEmpty(errorMessage) ? errorMessage : "账号或密码错误，请检查后重试。";
                    _banner.Show("登录失败：" + errorText);
                    Logger.Error($"登录失败: {errorText}");
                    GlobalState.Instance.IsLoggedIn = false;
                    Logger.Info("程序检测机制已暂停");
                    SetLoginLoading(false);
                    ApplyLoginLayout();
                }
            }
            catch (Exception ex)
            {
                _banner.Show("登录失败：" + ex.Message);
                Logger.Error($"登录失败: {ex.Message}");
                GlobalState.Instance.IsLoggedIn = false;
                Logger.Info("程序检测机制已暂停");
                SetLoginLoading(false);
                ApplyLoginLayout();
            }
        }

        private async void LogoutButton_Click(object? sender, EventArgs e)
        {
            _logoutButton.Enabled = false;

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
                        _logoutButton.Enabled = true;
                        return;
                    }
                }

                GlobalState.Instance.IsLoggedIn = false;

                var (logoutSuccess, errorMessage) = await Logout();

                Logger.Info("程序检测机制已关闭");

                if (logoutSuccess)
                {
                    Logger.Info("程序检测机制已暂停");
                    Logger.Info("用户登出成功，资源已清理");
                    OnLogoutSuccess();
                    UpdateUIState();
                }
                else
                {
                    string errorText = !string.IsNullOrEmpty(errorMessage) ? errorMessage : "登出失败: 系统异常";
                    Logger.Error($"登出失败: {errorText}");
                    Logger.Info("程序检测机制已暂停");
                    OnLogoutSuccess();
                    UpdateUIState();
                }
            }
            catch (Exception ex)
            {
                string errorMessage = ex.Message;
                Logger.Error($"登出失败: {errorMessage}");
                Logger.Info("程序检测机制已暂停");
                OnLogoutSuccess();
                UpdateUIState();
            }
            finally
            {
                _logoutButton.Enabled = true;
            }
        }

        /// <summary>
        /// 点击「修改密码」（登录页链接 / 已登录态按钮）：弹出修改密码弹窗。
        /// </summary>
        private void ChangePwdButton_Click(object? sender, EventArgs e)
        {
            if (GlobalState.Instance.IsLoggedIn)
            {
                string account = GlobalState.Instance.Username ?? "";
                if (string.IsNullOrEmpty(account)) return;
                Logger.Info($"用户 {account} 主动发起修改密码");
                using (var changePwdForm = new ChangePasswordForm(account))
                {
                    if (changePwdForm.ShowDialog(this) == DialogResult.OK)
                    {
                        Logger.Info("修改密码成功，已登录态自动执行注销");
                        LogoutButton_Click(this, EventArgs.Empty);
                    }
                }
                return;
            }

            string loginAccount = _userBox.Inner.Text.Trim();
            if (string.IsNullOrEmpty(loginAccount))
            {
                _userError.Visible = true;
                _userBox.HasError = true;
                ApplyLoginLayout();
                return;
            }
            Logger.Info($"用户 {loginAccount} 主动发起修改密码");
            using (var changePwdForm = new ChangePasswordForm(loginAccount))
            {
                if (changePwdForm.ShowDialog(this) == DialogResult.OK)
                {
                    Logger.Info("修改密码成功（未登录态），停留在登录页");
                }
            }
        }

        public void UpdateUIState()
        {
            Text = "密码管理";

            Logger.Info($"UpdateUIState被调用，当前IsLoggedIn状态: {GlobalState.Instance.IsLoggedIn}");

            if (GlobalState.Instance.IsLoggedIn)
            {
                // 已登录态（原型 SCREEN 2「账户信息」）
                string userName = GlobalState.Instance.Name ?? GlobalState.Instance.Username ?? "";
                _homeTitle.Text = $"你好，{userName}";
                _homeSub.Text = $"已登录 · 账号 {GlobalState.Instance.Username ?? ""}";
                _confirmShown = false;
                _logoutButton.Enabled = true;
                _goPwdButton.Enabled = true;
                ApplyHomeLayout();
            }
            else
            {
                SetLoginLoading(false);
                ApplyLoginLayout();
            }
        }

        // =====================================================================
        // HTTP / 账户接口（与原版一致）
        // =====================================================================
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

                var responseData = new
                {
                    status,
                    message,
                    date,
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
