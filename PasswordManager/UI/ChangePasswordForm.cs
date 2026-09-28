using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using PasswordManager.Services.Request;
using PasswordManager.Services.Routing;
using PasswordManager.Utils;
using PasswordManager.UI.Controls;

namespace PasswordManager.UI
{
    /// <summary>
    /// 修改/重置密码弹窗（严格还原原型 app-prototype.html SCREEN 3「修改密码」/ SCREEN 3b「重置密码」）：
    /// 1. 主动改密（_requireOldPassword=true）：标题"修改密码"，原密码/新密码/确认密码 + 规则清单 + 取消/保存；
    /// 2. 首次登录强制改密：标题"重置密码"，说明注释 + 账号 + 新密码/确认密码 + 规则清单 + 取消/保存。
    /// 接口约定（/account/change-password）：token 不再放请求头，account 放入请求体。
    /// 布局基准：420px 宽、content padding 32px（内容区 356px）、96DPI 逻辑像素。
    /// </summary>
    public class ChangePasswordForm : ThemedWindow
    {
        private static int WinWidth => Theme.S(420);
        private static int ContentX => Theme.S(32);        // .content padding 32
        private static int ContentWidth => Theme.S(356);

        private readonly string _account;
        private readonly string _oldPassword;
        private readonly bool _requireOldPassword;

        private AcctLine _acctLine = null!;    // "账号：xxx"(pwd) / "账号 + mono 账号名"(reset)
        private InfoNote? _note;               // reset 模式说明注释
        private InfoBanner _banner = null!;

        private Label _oldLabel = null!;       // pwd 模式
        private ThemedTextBox _oldBox = null!;
        private Label _oldError = null!;

        private Label _newLabel = null!;
        private ThemedTextBox _newBox = null!;
        private PasswordChecklist _checklist = null!;
        private Label _newError = null!;
        private Label _newCaps = null!;

        private Label _confirmLabel = null!;
        private ThemedTextBox _confirmBox = null!;
        private Label _confirmError = null!;
        private Label _confirmCaps = null!;

        private ThemedButton _cancelButton = null!;
        private ThemedButton _saveButton = null!;

        private bool _busy;

        /// <summary>首次登录强制改密：原密码由登录时输入的密码提供，无需再次输入。</summary>
        public ChangePasswordForm(string account, string oldPassword, string token)
        {
            _account = account;
            _oldPassword = oldPassword;
            _requireOldPassword = false;
            InitializeComponent();
        }

        /// <summary>登录页主动修改密码：需手动输入原密码。</summary>
        public ChangePasswordForm(string account)
        {
            _account = account;
            _oldPassword = null;
            _requireOldPassword = true;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // 原型两窗标题栏仅有关闭按钮
            ShowMinimizeButton = false;
            ShowMaximizeButton = false;
            Text = _requireOldPassword ? "修改密码" : "重置密码";

            // 文本容器比行盒加高（居中对齐保持原行盒位置），避免雅黑中文行高被裁
            if (_requireOldPassword)
            {
                _acctLine = new AcctLine($"账号：{_account}", null);
                Controls.Add(_acctLine);
            }
            else
            {
                _note = new InfoNote("这是首次登录", "，需先设置新密码才能继续使用。");
                _acctLine = new AcctLine("账号 ", _account);
                Controls.Add(_note);
                Controls.Add(_acctLine);
            }

            _banner = new InfoBanner();   // 加入窗体的时机在所有控件之后（z 序最底，见构造尾部）

            if (_requireOldPassword)
            {
                _oldLabel = MakeLabel("原密码", Theme.Body, Theme.TextSecondary);
                _oldBox = new ThemedTextBox(mono: false, password: true, placeholder: null, eye: true);
                _oldError = MakeLabel("请输入原密码", Theme.Small, Theme.Danger);
                _oldError.Visible = false;
                Controls.Add(_oldLabel);
                Controls.Add(_oldBox);
                Controls.Add(_oldError);
            }

            _newLabel = MakeLabel("新密码", Theme.Body, Theme.TextSecondary);
            _newBox = new ThemedTextBox(mono: false, password: true, placeholder: "请输入新密码", eye: true);
            _checklist = new PasswordChecklist();
            _newError = MakeLabel("新密码不符合要求", Theme.Small, Theme.Danger);
            _newError.Visible = false;
            _newCaps = MakeLabel("大写锁定已开启", Theme.Small, Theme.TextTertiary);
            _newCaps.Visible = false;
            Controls.Add(_newLabel);
            Controls.Add(_newBox);
            Controls.Add(_checklist);
            Controls.Add(_newError);
            Controls.Add(_newCaps);

            _confirmLabel = MakeLabel("确认密码", Theme.Body, Theme.TextSecondary);
            _confirmBox = new ThemedTextBox(mono: false, password: true, placeholder: "请再次输入新密码", eye: true);
            _confirmError = MakeLabel("两次输入的密码不一致", Theme.Small, Theme.Danger);
            _confirmError.Visible = false;
            _confirmCaps = MakeLabel("大写锁定已开启", Theme.Small, Theme.TextTertiary);
            _confirmCaps.Visible = false;
            Controls.Add(_confirmLabel);
            Controls.Add(_confirmBox);
            Controls.Add(_confirmError);
            Controls.Add(_confirmCaps);

            // CapsLock 提示（原型 .caps-hint）
            _newBox.Inner.KeyDown += (_, _) => UpdateCaps(_newCaps);
            _newBox.Inner.KeyUp += (_, _) => UpdateCaps(_newCaps);
            _confirmBox.Inner.KeyDown += (_, _) => UpdateCaps(_confirmCaps);
            _confirmBox.Inner.KeyUp += (_, _) => UpdateCaps(_confirmCaps);

            // 规则清单实时勾选（原型 input 事件）
            _newBox.Inner.TextChanged += (_, _) => _checklist.Update(_newBox.Inner.Text);
            _confirmBox.Inner.TextChanged += (_, _) => { if (_confirmBox.Inner.Text == _newBox.Inner.Text) { _confirmBox.HasError = false; _confirmError.Visible = false; } };

            // 动作条（原型 .dialog-actions：右对齐，按钮 min-width 96、padding 0 20、gap 8、margin-top 20）
            _cancelButton = new ThemedButton("取消", ThemedButtonStyle.Secondary);
            _cancelButton.Click += (_, _) => Close();
            _saveButton = new ThemedButton("保存", ThemedButtonStyle.Primary);
            _saveButton.Click += SaveButton_Click;
            Controls.Add(_cancelButton);
            Controls.Add(_saveButton);
            Controls.Add(_banner);   // z 序最底：横幅不会遮挡任何控件

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

        private void UpdateCaps(Label hint)
        {
            bool caps = Control.IsKeyLocked(Keys.CapsLock);
            if (hint.Visible != caps)
            {
                hint.Visible = caps;
                ApplyLayout();
            }
        }

        private void ClearFieldErrors()
        {
            // 原密码控件仅主动改密模式创建（重置模式为 null）
            if (_requireOldPassword)
            {
                _oldError.Visible = false;
                _oldBox.HasError = false;
            }
            _newError.Visible = false;
            _newBox.HasError = false;
            _confirmError.Visible = false;
            _confirmBox.HasError = false;
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _saveButton.Enabled = !busy;
            _cancelButton.Enabled = !busy;
            if (_requireOldPassword) _oldBox.Enabled = !busy;
            _newBox.Enabled = !busy;
            _confirmBox.Enabled = !busy;
        }

        // =====================================================================
        // 布局：按原型垂直堆叠逐段计算，动态调整窗口高度
        // =====================================================================
        private void ApplyLayout()
        {
            int y = TitleBarHeight + ContentPadding;

            if (_requireOldPassword)
            {
                // p 账号：xxx（行盒 18 → 容器 22）
                _acctLine.SetBounds(ContentX, y - Theme.S(2), ContentWidth, Theme.S(22)); y += Theme.S(18 + 16);
            }
            else
            {
                // note：图标 18 + 文字 13px（行盒 18 → 容器 22）
                _note!.SetBounds(ContentX, y - Theme.S(2), ContentWidth, Theme.S(22)); y += Theme.S(18 + 16);
                // acct-line：账号 + mono 账号名
                _acctLine.SetBounds(ContentX, y - Theme.S(2), ContentWidth, Theme.S(22));
                y += Theme.S(18 + 16);
            }

            // banner（显示时插入：min-height 60 + margin-bottom 16）
            // 未显示时移出可视区：避免透明控件残留原位遮挡下方控件（z 序高的先加入者在上）
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

            // 原密码 field（pwd 模式）
            if (_requireOldPassword)
            {
                _oldLabel.SetBounds(ContentX, y - Theme.S(2), ContentWidth, Theme.S(22)); y += Theme.S(18 + 6);
                _oldBox.SetBounds(ContentX, y, ContentWidth, ThemedTextBox.BoxHeight); y += ThemedTextBox.BoxHeight;
                _oldError.SetBounds(ContentX, y + Theme.S(2), ContentWidth, Theme.S(20));
                if (_oldBox.HasError) y += Theme.S(4 + 16);
                y += Theme.S(16);
            }

            // 新密码 field：label + box + checklist(margin 8, 36) [+error][+caps] + margin-bottom 16
            _newLabel.SetBounds(ContentX, y - Theme.S(2), ContentWidth, Theme.S(22)); y += Theme.S(18 + 6);
            _newBox.SetBounds(ContentX, y, ContentWidth, ThemedTextBox.BoxHeight); y += ThemedTextBox.BoxHeight;
            _checklist.SetBounds(ContentX, y + PasswordChecklist.TopMargin, ContentWidth, PasswordChecklist.ChecklistHeight);
            y += PasswordChecklist.TopMargin + PasswordChecklist.ChecklistHeight;
            _newError.SetBounds(ContentX, y + Theme.S(2), ContentWidth, Theme.S(20));
            if (_newBox.HasError) y += Theme.S(4 + 16);
            _newCaps.SetBounds(ContentX, y + Theme.S(2), ContentWidth, Theme.S(20));
            if (_newCaps.Visible) y += Theme.S(4 + 16);
            y += Theme.S(16);

            // 确认密码 field（margin-bottom 0）
            _confirmLabel.SetBounds(ContentX, y - Theme.S(2), ContentWidth, Theme.S(22)); y += Theme.S(18 + 6);
            _confirmBox.SetBounds(ContentX, y, ContentWidth, ThemedTextBox.BoxHeight); y += ThemedTextBox.BoxHeight;
            _confirmError.SetBounds(ContentX, y + Theme.S(2), ContentWidth, Theme.S(20));
            if (_confirmBox.HasError) y += Theme.S(4 + 16);
            _confirmCaps.SetBounds(ContentX, y + Theme.S(2), ContentWidth, Theme.S(20));
            if (_confirmCaps.Visible) y += Theme.S(4 + 16);

            // 动作条：右对齐 [取消][保存]，min-width 96、gap 8、margin-top 20
            y += Theme.S(20);
            int btnW = Theme.S(96);
            _saveButton.SetBounds(WinWidth - ContentX - btnW, y, btnW, ThemedButton.ButtonHeight);
            _cancelButton.SetBounds(WinWidth - ContentX - btnW - Theme.S(8) - btnW, y, btnW, ThemedButton.ButtonHeight);
            y += ThemedButton.ButtonHeight;

            ClientSize = new Size(WinWidth, y + ContentPadding);
        }

        // =====================================================================
        // 保存 / 校验（校验顺序与原型一致：规则 → 一致 → 原密码）
        // =====================================================================
        private async void SaveButton_Click(object? sender, EventArgs e)
        {
            if (_busy) return;

            ClearFieldErrors();
            if (_banner.IsShown)
            {
                _banner.Hide();
                ApplyLayout();
            }

            string oldPwd = _requireOldPassword ? _oldBox.Inner.Text : _oldPassword ?? "";
            string newPwd = _newBox.Inner.Text;
            string confirmPwd = _confirmBox.Inner.Text;

            if (!_checklist.Update(newPwd))
            {
                _newBox.HasError = true;
                _newError.Visible = true;
                _banner.Show("新密码不符合要求，请按下方规则修改。");
                ApplyLayout();
                return;
            }

            if (newPwd != confirmPwd)
            {
                _confirmBox.HasError = true;
                _confirmError.Visible = true;
                _banner.Show(_requireOldPassword ? "两次输入的密码不一致。" : "两次输入的密码不一致，请重新输入。");
                ApplyLayout();
                return;
            }

            if (_requireOldPassword && string.IsNullOrEmpty(oldPwd))
            {
                _oldBox.HasError = true;
                _oldError.Visible = true;
                _banner.Show("请输入原密码。");
                ApplyLayout();
                return;
            }

            SetBusy(true);

            bool success = await DoChangePasswordAsync(oldPwd, newPwd);

            if (success)
            {
                _banner.Show(
                    _requireOldPassword ? "密码已更新，正在注销并返回登录页…" : "密码已重置，请使用新密码重新登录。",
                    InfoBanner.BannerKind.Success);
                ApplyLayout();
                await Task.Delay(1200);
                this.DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                SetBusy(false);
                ApplyLayout();
            }
        }

        private async Task<bool> DoChangePasswordAsync(string oldPassword, string newPassword)
        {
            try
            {
                var httpRequestService = new HttpRequestService();
                // 接口约定（v1）：token 不再放请求头，account 放入请求体
                var requestData = new
                {
                    account = _account,
                    oldPassword = oldPassword,
                    newPassword = newPassword
                };

                var response = await httpRequestService.PostAsync<object>(
                    ApiRoutes.AccountChangePassword,
                    requestData
                );

                if (response != null && response.status == 200)
                {
                    Logger.Info($"用户 {_account} 修改密码成功");
                    return true;
                }

                string err = response?.message ?? "未知错误";
                Logger.Error($"用户 {_account} 修改密码失败: {err}");
                _banner.Show("修改失败：" + err);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"修改密码异常: {ex.Message}");
                _banner.Show("修改失败：" + ex.Message);
                return false;
            }
        }
    }
}
