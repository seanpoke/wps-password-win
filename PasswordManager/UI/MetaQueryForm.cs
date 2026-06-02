using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using PasswordManager.Business;
using PasswordManager.Services.Request;
using PasswordManager.Services.Routing;
using PasswordManager.Utils;

namespace PasswordManager.UI
{
    public class MetaQueryForm : Form
    {
        private TextBox _filePathTextBox;
        private Button _queryButton;
        private TextBox _uidTextBox;
        private TextBox _encodePasswordTextBox;
        private TextBox _keyVersionTextBox;
        private TextBox _decodePasswordTextBox;
        private Label _errorLabel;
        private Label _loadingLabel;
        private Panel _resultPanel;

        private FileMetaManager _fileMetaManager;

        public MetaQueryForm()
        {
            _fileMetaManager = new FileMetaManager();
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "请输入文件路径";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(700, 320);

            Font labelFont = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Regular);
            Font inputFont = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Regular);
            Font buttonFont = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Bold);

            int labelGap = 15;
            int controlGap = 28;
            int startY = 20;
            int inputWidth = 450;
            int buttonWidth = 80;
            int labelWidth = 70;

            Label filePathLabel = new Label
            {
                Text = "文件路径:",
                TextAlign = System.Drawing.ContentAlignment.MiddleRight,
                Font = labelFont,
                ForeColor = System.Drawing.Color.FromArgb(60, 60, 60),
                Size = new System.Drawing.Size(labelWidth, 26),
                Location = new System.Drawing.Point(labelGap, startY)
            };
            this.Controls.Add(filePathLabel);

            _filePathTextBox = new TextBox
            {
                Font = inputFont,
                BorderStyle = BorderStyle.FixedSingle,
                Size = new System.Drawing.Size(inputWidth, 26),
                Location = new System.Drawing.Point(labelWidth + labelGap * 2, startY),
                PlaceholderText = "请输入文件路径"
            };
            this.Controls.Add(_filePathTextBox);

            _queryButton = new Button
            {
                Text = "查询",
                Font = buttonFont,
                BackColor = System.Drawing.Color.FromArgb(0, 120, 212),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Size = new System.Drawing.Size(buttonWidth, 26),
                Location = new System.Drawing.Point(labelWidth + labelGap * 2 + inputWidth + labelGap, startY)
            };
            _queryButton.FlatAppearance.BorderSize = 0;
            _queryButton.MouseEnter += (sender, e) => _queryButton.BackColor = System.Drawing.Color.FromArgb(26, 115, 232);
            _queryButton.MouseLeave += (sender, e) => _queryButton.BackColor = System.Drawing.Color.FromArgb(0, 120, 212);
            _queryButton.MouseDown += (sender, e) => _queryButton.BackColor = System.Drawing.Color.FromArgb(0, 90, 170);
            _queryButton.MouseUp += (sender, e) => _queryButton.BackColor = System.Drawing.Color.FromArgb(26, 115, 232);
            _queryButton.Click += QueryButton_Click;
            this.Controls.Add(_queryButton);

            _loadingLabel = new Label
            {
                Text = "查询中...",
                ForeColor = System.Drawing.Color.Blue,
                Font = labelFont,
                AutoSize = true,
                Visible = false,
                Location = new System.Drawing.Point(labelGap, startY + controlGap)
            };
            this.Controls.Add(_loadingLabel);

            _resultPanel = new Panel
            {
                Size = new System.Drawing.Size(670, 180),
                Location = new System.Drawing.Point(labelGap, startY + controlGap),
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(_resultPanel);

            int fieldGap = 42;
            int fieldStartY = 10;
            int fieldLabelWidth = 130;
            int fieldValueWidth = 520;
            int fieldValueHeight = 38;

            Label uidFieldLabel = new Label
            {
                Text = "uid:",
                TextAlign = System.Drawing.ContentAlignment.MiddleRight,
                Font = labelFont,
                ForeColor = System.Drawing.Color.FromArgb(60, 60, 60),
                Size = new System.Drawing.Size(fieldLabelWidth, fieldValueHeight),
                Location = new System.Drawing.Point(5, fieldStartY)
            };
            _resultPanel.Controls.Add(uidFieldLabel);

            _uidTextBox = new TextBox
            {
                Text = "",
                Font = inputFont,
                ForeColor = System.Drawing.Color.Black,
                Size = new System.Drawing.Size(fieldValueWidth, fieldValueHeight),
                Location = new System.Drawing.Point(fieldLabelWidth + 5, fieldStartY),
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = true,
                Multiline = true,
                WordWrap = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = System.Drawing.Color.White
            };
            _resultPanel.Controls.Add(_uidTextBox);

            Label encodePasswordFieldLabel = new Label
            {
                Text = "EncodePassword:",
                TextAlign = System.Drawing.ContentAlignment.MiddleRight,
                Font = labelFont,
                ForeColor = System.Drawing.Color.FromArgb(60, 60, 60),
                Size = new System.Drawing.Size(fieldLabelWidth, fieldValueHeight),
                Location = new System.Drawing.Point(5, fieldStartY + fieldGap)
            };
            _resultPanel.Controls.Add(encodePasswordFieldLabel);

            _encodePasswordTextBox = new TextBox
            {
                Text = "",
                Font = inputFont,
                ForeColor = System.Drawing.Color.Black,
                Size = new System.Drawing.Size(fieldValueWidth, fieldValueHeight),
                Location = new System.Drawing.Point(fieldLabelWidth + 5, fieldStartY + fieldGap),
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = true,
                Multiline = true,
                WordWrap = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = System.Drawing.Color.White
            };
            _resultPanel.Controls.Add(_encodePasswordTextBox);

            Label keyVersionFieldLabel = new Label
            {
                Text = "keyVersion:",
                TextAlign = System.Drawing.ContentAlignment.MiddleRight,
                Font = labelFont,
                ForeColor = System.Drawing.Color.FromArgb(60, 60, 60),
                Size = new System.Drawing.Size(fieldLabelWidth, fieldValueHeight),
                Location = new System.Drawing.Point(5, fieldStartY + fieldGap * 2)
            };
            _resultPanel.Controls.Add(keyVersionFieldLabel);

            _keyVersionTextBox = new TextBox
            {
                Text = "",
                Font = inputFont,
                ForeColor = System.Drawing.Color.Black,
                Size = new System.Drawing.Size(fieldValueWidth, fieldValueHeight),
                Location = new System.Drawing.Point(fieldLabelWidth + 5, fieldStartY + fieldGap * 2),
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = true,
                Multiline = true,
                WordWrap = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = System.Drawing.Color.White
            };
            _resultPanel.Controls.Add(_keyVersionTextBox);

            Label decodePasswordFieldLabel = new Label
            {
                Text = "DecodePassword:",
                TextAlign = System.Drawing.ContentAlignment.MiddleRight,
                Font = labelFont,
                ForeColor = System.Drawing.Color.FromArgb(60, 60, 60),
                Size = new System.Drawing.Size(fieldLabelWidth, fieldValueHeight),
                Location = new System.Drawing.Point(5, fieldStartY + fieldGap * 3)
            };
            _resultPanel.Controls.Add(decodePasswordFieldLabel);

            _decodePasswordTextBox = new TextBox
            {
                Text = "",
                Font = inputFont,
                ForeColor = System.Drawing.Color.Black,
                Size = new System.Drawing.Size(fieldValueWidth, fieldValueHeight),
                Location = new System.Drawing.Point(fieldLabelWidth + 5, fieldStartY + fieldGap * 3),
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = true,
                Multiline = true,
                WordWrap = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = System.Drawing.Color.White
            };
            _resultPanel.Controls.Add(_decodePasswordTextBox);

            _errorLabel = new Label
            {
                Text = "",
                ForeColor = System.Drawing.Color.Red,
                Font = labelFont,
                Size = new System.Drawing.Size(670, 25),
                AutoSize = false,
                Location = new System.Drawing.Point(labelGap, startY + controlGap + 190)
            };
            this.Controls.Add(_errorLabel);

            _filePathTextBox.KeyDown += (sender, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    _queryButton.PerformClick();
                }
            };
        }

        private async void QueryButton_Click(object sender, EventArgs e)
        {
            string filePath = _filePathTextBox.Text.Trim();

            if (string.IsNullOrEmpty(filePath))
            {
                ShowError("请输入文件路径");
                return;
            }

            ClearResults();
            ShowLoading(true);

            await QueryMetadataAsync(filePath);

            ShowLoading(false);
        }

        private async Task QueryMetadataAsync(string filePath)
        {
            try
            {
                ShowError("");

                if (!File.Exists(filePath))
                {
                    ShowError("文件路径不存在或无法找到文件");
                    return;
                }

                string extension = Path.GetExtension(filePath).ToLower();
                if (extension != ".docx" && extension != ".xlsx" && extension != ".pptx")
                {
                    ShowError("不支持的文件格式，请选择 .docx, .xlsx 或 .pptx 文件");
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

                _uidTextBox.Text = uid ?? "";
                _encodePasswordTextBox.Text = encodePassword ?? "";
                _keyVersionTextBox.Text = keyVersion ?? "";
                _decodePasswordTextBox.Text = decodePassword ?? "";

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    ShowError(errorMessage);
                }

                Logger.Info("元数据查询完成");
            }
            catch (Exception ex)
            {
                Logger.Error($"查询元数据时发生异常: {ex.Message}");
                ShowError("查询元数据时发生异常: " + ex.Message);
            }
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

        private void ShowLoading(bool isLoading)
        {
            _loadingLabel.Visible = isLoading;
            _resultPanel.Visible = !isLoading;
            _queryButton.Enabled = !isLoading;
            _filePathTextBox.Enabled = !isLoading;
        }

        private void ShowError(string message)
        {
            _errorLabel.Text = message;
        }

        private void ClearResults()
        {
            _uidTextBox.Text = "";
            _encodePasswordTextBox.Text = "";
            _keyVersionTextBox.Text = "";
            _decodePasswordTextBox.Text = "";
            _errorLabel.Text = "";
        }

        private string AppendError(string existing, string newError)
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