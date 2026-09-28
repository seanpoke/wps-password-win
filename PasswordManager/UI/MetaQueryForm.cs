using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using PasswordManager.Business;
using PasswordManager.Services.Request;
using PasswordManager.Services.Routing;
using PasswordManager.Utils;
using PasswordManager.UI.Controls;

namespace PasswordManager.UI
{
    /// <summary>
    /// 查询元数据窗口（严格还原原型 app-prototype.html SCREEN 6「查询元数据」）：
    /// 选择文件行（mono 只读路径框 + 「选择文件」/「查询」按钮，支持文件拖拽）+
    /// 错误横幅 + 元数据信息卡（uid/EncodePassword/keyVersion/DecodePassword，等宽省略 + 悬停完整 + 复制按钮）+
    /// 提示行 + 右对齐关闭按钮。
    /// 业务逻辑保留：FileMetaManager 读取 + /doc/password 解密接口。
    /// 布局基准：窗口 480px 宽、content padding 32px（内容区 416px）。
    /// </summary>
    public class MetaQueryForm : ThemedWindow
    {
        private static int WinWidth => Theme.S(480);
        private static int ContentX => Theme.S(32);      // .content padding 32
        private static int ContentWidth => Theme.S(416); // 480 - 32*2

        private Label _pathLabel = null!;
        private ThemedTextBox _pathBox = null!;
        private ThemedButton _browseButton = null!;
        private ThemedButton _queryButton = null!;
        private Label _pathError = null!;
        private InfoBanner _banner = null!;
        private InfoCard _metaCard = null!;
        private Label _hintLine = null!;
        private ThemedButton _closeButton = null!;

        private readonly FileMetaManager _fileMetaManager;
        private bool _busy;

        public MetaQueryForm()
        {
            _fileMetaManager = new FileMetaManager();
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "查询元数据";
            ShowMinimizeButton = false;
            ShowMaximizeButton = false;
            AllowDrop = true;

            // 文件拖拽（原型：把文件拖拽到上方输入框即可查询）
            DragEnter += (_, e) =>
            {
                if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                {
                    e.Effect = DragDropEffects.Copy;
                    _pathBox.DropHighlight = true;
                    _pathBox.Invalidate();
                }
            };
            DragLeave += (_, _) => { _pathBox.DropHighlight = false; _pathBox.Invalidate(); };
            DragDrop += (_, e) =>
            {
                _pathBox.DropHighlight = false;
                _pathBox.Invalidate();
                if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                    SetFilePath(files[0]);
            };

            // ---- 选择文件 field ----
            _pathLabel = MakeLabel("选择文件", Theme.Body, Theme.TextSecondary);

            _pathBox = new ThemedTextBox(mono: true, password: false, placeholder: "未选择文件")
            {
                ReadOnly = true
            };
            _pathError = MakeLabel("请先选择要查询的文件", Theme.Small, Theme.Danger);
            _pathError.Visible = false;

            _browseButton = new ThemedButton("选择文件", ThemedButtonStyle.Secondary);
            _browseButton.Click += (_, _) =>
            {
                using var dialog = new OpenFileDialog
                {
                    Filter = "WPS 文档 (*.docx;*.xlsx;*.pptx)|*.docx;*.xlsx;*.pptx|所有文件 (*.*)|*.*"
                };
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    SetFilePath(dialog.FileName);
            };

            _queryButton = new ThemedButton("查询", ThemedButtonStyle.Primary);
            _queryButton.Click += QueryButton_Click;

            // ---- 错误横幅 ----
            _banner = new InfoBanner();

            // ---- 元数据信息卡（原型 .card.tight：uid/EncodePassword/keyVersion/DecodePassword） ----
            _metaCard = new InfoCard { KeyWidth = Theme.S(132) };
            _metaCard.AddRow("uid", "", mono: true, empty: true, hasCopy: true);
            _metaCard.AddRow("EncodePassword", "", mono: true, empty: true, hasCopy: true);
            _metaCard.AddRow("keyVersion", "", mono: true, empty: true, hasCopy: true);
            _metaCard.AddRow("DecodePassword", "", mono: true, empty: true, hasCopy: true);

            // ---- 提示行（原型 .hint-line） ----
            _hintLine = new Label
            {
                Text = "值过长时单行省略，悬停可见完整内容；点击「选择文件」或把文件拖拽到上方输入框即可查询。",
                Font = Theme.Small,
                ForeColor = Theme.TextTertiary,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            // ---- 关闭按钮（原型 .dialog-actions 右对齐） ----
            _closeButton = new ThemedButton("关闭", ThemedButtonStyle.Primary);
            _closeButton.Click += (_, _) => Close();

            Controls.AddRange(new Control[]
            {
                _pathLabel, _pathBox, _pathError, _browseButton, _queryButton,
                _banner, _metaCard, _hintLine, _closeButton
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

        private void SetFilePath(string path)
        {
            _pathBox.Inner.Text = path;
            _pathError.Visible = false;
            _pathBox.HasError = false;
            ApplyLayout();
        }

        private void ApplyLayout()
        {
            int y = TitleBarHeight + ContentPadding;

            // 选择文件 field：label(18)+6 → query-row(36) [+error 4+16] + margin-bottom 16
            _pathLabel.SetBounds(ContentX, y - Theme.S(2), ContentWidth, Theme.S(22)); y += Theme.S(18 + 6);
            int rowH = ThemedTextBox.BoxHeight;
            int btnW = Theme.S(96);
            int gap = Theme.S(8);
            int pathW = ContentWidth - btnW * 2 - gap * 2;
            _pathBox.SetBounds(ContentX, y, pathW, rowH);
            _browseButton.SetBounds(ContentX + pathW + gap, y, btnW, rowH);
            _queryButton.SetBounds(ContentX + pathW + gap + btnW + gap, y, btnW, rowH);
            y += rowH;
            _pathError.SetBounds(ContentX, y + Theme.S(2), ContentWidth, Theme.S(20));
            if (_pathBox.HasError) y += Theme.S(4 + 16);
            y += Theme.S(16);

            // banner（显示时插入：min-height 60 + margin-bottom 16）
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

            // 信息卡（原型 .card.tight：margin-top 4）
            _metaCard.SetBounds(ContentX, y + Theme.S(4), ContentWidth, _metaCard.GetPreferredHeight());
            y += Theme.S(4) + _metaCard.Height;

            // 提示行（原型 .hint-line：margin 10 0 0）
            y += Theme.S(10);
            _hintLine.SetBounds(ContentX, y, ContentWidth, Theme.S(32));
            y += Theme.S(32);

            // 关闭按钮（原型 .dialog-actions：右对齐、margin-top 20）
            y += Theme.S(20);
            _closeButton.SetBounds(WinWidth - ContentX - Theme.S(96), y, Theme.S(96), ThemedButton.ButtonHeight);
            y += ThemedButton.ButtonHeight;

            ClientSize = new Size(WinWidth, y + ContentPadding);
        }

        // =====================================================================
        // 查询 / 解密（业务逻辑与原版一致）
        // =====================================================================
        private async void QueryButton_Click(object? sender, EventArgs e)
        {
            if (_busy) return;

            string filePath = _pathBox.Inner.Text.Trim();
            if (string.IsNullOrEmpty(filePath))
            {
                _pathBox.HasError = true;
                _pathError.Visible = true;
                ApplyLayout();
                return;
            }

            ClearResults();
            SetBusy(true);
            ApplyLayout();

            await QueryMetadataAsync(filePath);

            SetBusy(false);
            ApplyLayout();
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _queryButton.Text = busy ? "查询中" : "查询";
            _queryButton.Loading = busy;
            _pathBox.Enabled = !busy;
            _browseButton.Enabled = !busy;
        }

        private async Task QueryMetadataAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _banner.Show("文件路径不存在或无法找到文件");
                    return;
                }

                string extension = Path.GetExtension(filePath).ToLower();
                if (extension != ".docx" && extension != ".xlsx" && extension != ".pptx")
                {
                    _banner.Show("不支持的文件格式，请选择 .docx, .xlsx 或 .pptx 文件");
                    return;
                }

                string uid = null;
                string encodePassword = null;
                string keyVersion = null;
                string decodePassword = null;
                string errorMessage = null;

                try
                {
                    uid = _fileMetaManager.ReadUidFromFile(filePath);
                }
                catch (Exception ex)
                {
                    Logger.Error($"读取UID失败: {ex.Message}");
                    errorMessage = AppendError(errorMessage, "读取UID失败: " + ex.Message);
                }

                try
                {
                    encodePassword = _fileMetaManager.ReadPasswordFromFile(filePath);
                }
                catch (Exception ex)
                {
                    Logger.Error($"读取加密密码失败: {ex.Message}");
                    errorMessage = AppendError(errorMessage, "读取加密密码失败: " + ex.Message);
                }

                try
                {
                    keyVersion = _fileMetaManager.ReadKeyVersionFromFile(filePath);
                }
                catch (Exception ex)
                {
                    Logger.Error($"读取keyVersion失败: {ex.Message}");
                    errorMessage = AppendError(errorMessage, "读取keyVersion失败: " + ex.Message);
                }

                if (!string.IsNullOrEmpty(uid) && !string.IsNullOrEmpty(encodePassword))
                {
                    try
                    {
                        decodePassword = await DecryptPasswordAsync(uid, encodePassword, keyVersion);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"解密密码失败: {ex.Message}");
                        errorMessage = AppendError(errorMessage, "解密密码失败: " + ex.Message);
                    }
                }

                UpdateMetaCard("uid", uid, "EncodePassword", encodePassword, "keyVersion", keyVersion, "DecodePassword", decodePassword);

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    _banner.Show(errorMessage);
                }

                Logger.Info("元数据查询完成");
            }
            catch (Exception ex)
            {
                Logger.Error($"查询元数据时发生异常: {ex.Message}");
                _banner.Show("查询元数据时发生异常: " + ex.Message);
            }
        }

        /// <summary>把查询结果填入信息卡（有值显示等宽内容，无值显示 "—"）。</summary>
        private void UpdateMetaCard(string k1, string? v1, string k2, string? v2, string k3, string? v3, string k4, string? v4)
        {
            _metaCard.Clear();
            _metaCard.AddRow(k1, v1 ?? "", mono: true, empty: string.IsNullOrEmpty(v1), hasCopy: !string.IsNullOrEmpty(v1));
            _metaCard.AddRow(k2, v2 ?? "", mono: true, empty: string.IsNullOrEmpty(v2), hasCopy: !string.IsNullOrEmpty(v2));
            _metaCard.AddRow(k3, v3 ?? "", mono: true, empty: string.IsNullOrEmpty(v3), hasCopy: !string.IsNullOrEmpty(v3));
            _metaCard.AddRow(k4, v4 ?? "", mono: true, empty: string.IsNullOrEmpty(v4), hasCopy: !string.IsNullOrEmpty(v4));
            _metaCard.Invalidate();
        }

        private async Task<string> DecryptPasswordAsync(string uid, string encryPassword, string keyVersion)
        {
            if (!GlobalState.Instance.IsLoggedIn)
            {
                throw new Exception("用户未登录，无法解密密码");
            }

            if (string.IsNullOrEmpty(GlobalState.Instance.Token))
            {
                throw new Exception("用户身份验证token失效或未授权");
            }

            try
            {
                var httpRequestService = RequestFactory.GetHttpRequestService();

                var requestData = new
                {
                    docId = uid,
                    encryPassword = encryPassword,
                    keyVersion = keyVersion ?? "default",
                    isTemp = true
                };

                var response = await httpRequestService.PostAsync<PasswordDecryptResponse>(
                    ApiRoutes.DocPassword,
                    requestData,
                    GlobalState.Instance.Token
                );

                if (response != null && response.status == 200 && response.data != null)
                {
                    return response.data.password;
                }
                else
                {
                    throw new Exception(response?.message ?? "后端接口请求失败");
                }
            }
            catch (HttpRequestException ex)
            {
                throw new Exception("后端接口请求失败或超时: " + ex.Message);
            }
            catch (Exception ex)
            {
                throw new Exception("解密密码过程中发生错误: " + ex.Message);
            }
        }

        private void ClearResults()
        {
            _banner.Hide();
            _pathError.Visible = false;
            _pathBox.HasError = false;
            UpdateMetaCard("uid", null, "EncodePassword", null, "keyVersion", null, "DecodePassword", null);
        }

        private string AppendError(string? existing, string newError)
        {
            if (string.IsNullOrEmpty(existing))
            {
                return newError;
            }
            return existing + "\n" + newError;
        }

        public class PasswordDecryptResponse
        {
            public string password { get; set; }
        }
    }
}
