using System;
using System.Drawing;
using System.Windows.Forms;
using PasswordManager.Services.Routing;
using PasswordManager.UI.Controls;
using PasswordManager.Utils;

namespace PasswordManager.UI
{
    public class UpdateDialog : ThemedWindow
    {
        private readonly VersionCheckInfo _info;
        private readonly bool _force;

        public UpdateDialog(VersionCheckInfo info, bool force)
        {
            _info = info;
            _force = force;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = _force ? "强制更新" : "发现新版本";
            ShowMinimizeButton = false;
            ShowMaximizeButton = false;
            Resizable = false;
            TopMost = true;   // 始终置顶，不可被其它应用覆盖

            int w = Theme.S(420);
            int h = Theme.S(260);
            ClientSize = new Size(w, h);

            int pad = ContentPadding;
            int top = TitleBarHeight + Theme.S(24);

            var tip = new Label
            {
                Text = _force
                    ? "当前版本已停止支持，请更新后再继续使用，请联系管理员获取最新版本"
                    : "有可用的新版本，建议更新，请联系管理员获取最新版本",
                Font = Theme.Body,
                ForeColor = Theme.TextPrimary,
                Location = new Point(pad, top),
                AutoSize = true,
                MaximumSize = new Size(w - pad * 2, 0)
            };
            Controls.Add(tip);

            var ver = new Label
            {
                Text = $"当前版本 {_info.currentVersion} → 最新版本 {_info.latestVersion}",
                Font = Theme.Small,
                ForeColor = Theme.TextSecondary,
                Location = new Point(pad, top + Theme.S(48)),
                AutoSize = true
            };
            Controls.Add(ver);

            var release = new Label
            {
                Text = string.IsNullOrEmpty(_info.releaseTime) ? "" : $"发布时间：{_info.releaseTime}",
                Font = Theme.Small,
                ForeColor = Theme.TextSecondary,
                Location = new Point(pad, top + Theme.S(72)),
                AutoSize = true
            };
            Controls.Add(release);

            var logLabel = new Label
            {
                Text = "更新说明：" + (string.IsNullOrEmpty(_info.changelog) ? "（无）" : _info.changelog),
                Font = Theme.Small,
                ForeColor = Theme.TextSecondary,
                Location = new Point(pad, top + Theme.S(100)),
                AutoSize = true,
                MaximumSize = new Size(w - pad * 2, Theme.S(96))
            };
            Controls.Add(logLabel);

            // 无按钮：FORCE 关闭窗口即视为放弃更新 → 由调用方在 FormClosing 中退出程序；
            // OPTIONAL 关闭窗口即“暂不更新”。
        }
    }
}
