using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
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
            int h = Theme.S(330);   // 底部留出「下载安装包」/「忽略」按钮
            ClientSize = new Size(w, h);

            int pad = ContentPadding;
            int top = TitleBarHeight + Theme.S(24);

            var tip = new Label
            {
                Text = _force
                    ? "当前版本已停止维护，请下载最新版本"
                    : "已检测到软件新版本，建议升级",
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

            // 下载按钮（OPTIONAL / FORCE 均提供）：保存新版本安装包，无需 token
            var downloadBtn = new ThemedButton("下载安装包", ThemedButtonStyle.Primary)
            {
                Size = new Size(Theme.S(110), ThemedButton.ButtonHeight),
                Cursor = Cursors.Hand
            };
            downloadBtn.Click += (_, _) => DownloadAsync(downloadBtn);
            Controls.Add(downloadBtn);
            downloadBtn.Location = new Point(w - pad - downloadBtn.Width, h - pad - downloadBtn.Height);

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
                ignoreBtn.Location = new Point(downloadBtn.Left - Theme.S(12) - ignoreBtn.Width, h - pad - ignoreBtn.Height);
            }

            // FORCE：仅保留下载按钮；关闭窗口（×）即视为放弃更新 → 由调用方在 FormClosing 中退出程序。
        }

        /// <summary>下载地址：服务端返回相对路径时拼接当前服务器地址（ip:端口），无需 token。</summary>
        private string? ResolveDownloadUrl()
        {
            var du = _info.downloadUrl;
            if (string.IsNullOrWhiteSpace(du)) return null;
            if (du.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                du.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return du;

            string server = GlobalState.Instance.GetServerAddress();   // 如 http://10.8.2.83:8081
            return server.TrimEnd('/') + "/" + du.TrimStart('/');
        }

        /// <summary>下载新版本安装包：弹保存位置 → 流式写盘 → 按钮显示进度 → 完成后打开所在目录。</summary>
        private async void DownloadAsync(ThemedButton btn)
        {
            try
            {
                string? url = ResolveDownloadUrl();
                if (string.IsNullOrEmpty(url))
                {
                    MessageBox.Show("下载地址无效，请联系管理员获取最新版本", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string fileName;
                try { fileName = Path.GetFileName(new Uri(url).LocalPath); }
                catch { fileName = ""; }
                if (string.IsNullOrWhiteSpace(fileName))
                    fileName = $"PasswordManager-{_info.latestVersion}.zip";

                using var dialog = new SaveFileDialog
                {
                    FileName = fileName,
                    Filter = "压缩文件 (*.zip)|*.zip|所有文件 (*.*)|*.*"
                };
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string savePath = dialog.FileName;

                btn.Enabled = false;
                string originalText = btn.Text;
                btn.Text = "下载中 0%";
                Logger.Info($"开始下载新版本安装包: {url} → {savePath}");

                // 无 token 直连（服务端下载接口免鉴权）；流式写盘并回报进度
                using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
                using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                resp.EnsureSuccessStatusCode();

                long? total = resp.Content.Headers.ContentLength;
                using var src = await resp.Content.ReadAsStreamAsync();
                await using var fs = new FileStream(savePath, FileMode.Create, FileAccess.Write);

                var buffer = new byte[81920];
                long downloaded = 0;
                int read;
                var progress = new Progress<int>(p => btn.Text = $"下载中 {p}%");
                while ((read = await src.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, read));
                    downloaded += read;
                    if (total > 0 && total.Value > 0)
                        ((IProgress<int>)progress).Report((int)(downloaded * 100 / total.Value));
                }

                Logger.Info($"新版本安装包下载完成: {savePath}");
                // 提示下载完成，用户确认后关闭弹窗并退出进程（释放文件占用，便于安装新版本）
                MessageBox.Show($"下载完成：{savePath}", "下载完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                Logger.Info("用户确认下载完成，关闭更新弹窗并退出程序");
                Close();
                Application.Exit();
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Logger.Error($"下载新版本安装包失败: {ex.Message}");
                MessageBox.Show($"下载失败: {ex.Message}", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                btn.Enabled = true;
                btn.Text = "下载安装包";
            }
        }
    }
}
