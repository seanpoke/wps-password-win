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
            int h = _force ? Theme.S(260) : Theme.S(330);   // 非强制：底部留出「忽略」按钮
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

            if (!_force)
            {
                // 非强制更新：提供「忽略」，点击后关闭当前窗口（不退出程序）
                var ignoreBtn = new ThemedButton("忽略", ThemedButtonStyle.Secondary)
                {
                    Size = new Size(Theme.S(96), ThemedButton.ButtonHeight),
                    Cursor = Cursors.Hand
                };
                ignoreBtn.Click += (_, _) => Close();
                Controls.Add(ignoreBtn);
                ignoreBtn.Location = new Point(w - pad - ignoreBtn.Width, h - pad - ignoreBtn.Height);
            }

            // FORCE：无任何按钮，关闭窗口（×）即视为放弃更新 → 由调用方在 FormClosing 中退出程序。
        }
    }
}
