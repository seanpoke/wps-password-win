using System;
using System.Threading;
using System.Windows.Forms;
using WpsPasswordManager.Utils;

namespace WpsPasswordManager.UI
{
    public class FileRecognitionFailedForm : Form
    {
        private Label _messageLabel;
        private Button _confirmButton;
        private static volatile bool _isShowing = false;
        private static readonly object _showLock = new object();
        private bool _allowClose = false;

        public FileRecognitionFailedForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "文件识别失败";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new System.Drawing.Size(350, 165);
            this.ShowIcon = true;
            this.ShowInTaskbar = true;
            this.TopMost = true;
            this.FormClosing += (sender, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing && !_allowClose)
                {
                    e.Cancel = true;
                }
            };

            _messageLabel = new Label
            {
                Text = "请将在线文档移动到桌面或本地再打开",
                Font = new System.Drawing.Font("SimSun", 10F),
                ForeColor = System.Drawing.Color.Black,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                AutoSize = false,
                Size = new System.Drawing.Size(320, 50),
                Location = new System.Drawing.Point(15, 20)
            };
            this.Controls.Add(_messageLabel);

            _confirmButton = new Button
            {
                Text = "确认",
                Font = new System.Drawing.Font("SimSun", 9F),
                Size = new System.Drawing.Size(80, 30),
                Location = new System.Drawing.Point((this.Width - 80) / 2, 85)
            };
            _confirmButton.Click += (sender, e) =>
            {
                _allowClose = true;
                this.Close();
            };
            this.Controls.Add(_confirmButton);
        }

        public static void ShowDialogIfNeeded(string failedFileName)
        {
            if (string.IsNullOrEmpty(failedFileName))
            {
                Logger.Info("弹窗提示被跳过：文档名为空");
                return;
            }

            lock (_showLock)
            {
                if (_isShowing)
                {
                    Logger.Info("弹窗提示被跳过：已有弹窗正在显示");
                    return;
                }

                string lastFailed = GlobalState.Instance.LastFailedFileName;

                if (!string.IsNullOrEmpty(lastFailed) && 
                    string.Equals(lastFailed, failedFileName, StringComparison.Ordinal))
                {
                    Logger.Info("弹窗提示被跳过：上次失败的文档名相同");
                    return;
                }

                _isShowing = true;
                GlobalState.Instance.LastFailedFileName = failedFileName;
                Logger.Info("准备显示弹窗提示");
            }

            Thread newThread = new Thread(() =>
            {
                try
                {
                    Logger.Info("弹窗线程已启动，准备显示弹窗");
                    using (var form = new FileRecognitionFailedForm())
                    {
                        form.Shown += (sender, e) =>
                        {
                            form.Activate();
                            form.BringToFront();
                        };
                        form.ShowDialog();
                    }
                    Logger.Info("弹窗已关闭");
                }
                finally
                {
                    lock (_showLock)
                    {
                        _isShowing = false;
                    }
                }
            });

            newThread.SetApartmentState(ApartmentState.STA);
            newThread.IsBackground = true;
            newThread.Start();
        }

        public static void ClearFailedRecord()
        {
            GlobalState.Instance.LastFailedFileName = null;
        }
    }
}