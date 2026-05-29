using System;
using System.IO;
using System.Windows.Forms;
using System.Text;
using PasswordManager.Utils;

namespace PasswordManager.UI
{
    public class LogForm : Form
    {
        private TextBox _logTextBox;
        private Button _closeButton;
        private Button _pauseButton;
        private Button _resumeButton;
        private Button _clearButton;
        private string _logFilePath;
        private bool _isPaused;
        private readonly StringBuilder _pendingContent = new StringBuilder();
        private bool _isUpdating = false;
        private readonly object _updateLock = new object();

        public static LogForm Instance { get; private set; }

        public LogForm()
        {
            _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wps_password_manager.log");
            InitializeComponent();
            LoadLogContent();
            SetupLogCallback();
        }

        private void InitializeComponent()
        {
            this.Text = "密码管理 - 日志";
            this.Size = DpiHelper.ScaleSize(new System.Drawing.Size(800, 600));
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;

            float dpiScale = DpiHelper.GetDpiScale();

            _logTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = DpiHelper.ScaleFont(new System.Drawing.Font("Consolas", 9F)),
                BackColor = System.Drawing.Color.Black,
                ForeColor = System.Drawing.Color.LightGreen,
                ScrollBars = ScrollBars.Vertical,
                Multiline = true,
                WordWrap = false
            };

            _closeButton = new Button 
            { 
                Text = "关闭", 
                Width = DpiHelper.ScaleValue(80), 
                Height = DpiHelper.ScaleValue(35),
                Font = DpiHelper.ScaleFont(new System.Drawing.Font("微软雅黑", 9F))
            };
            _closeButton.Click += (s, e) => this.Hide();

            _pauseButton = new Button 
            { 
                Text = "暂停", 
                Width = DpiHelper.ScaleValue(80), 
                Height = DpiHelper.ScaleValue(35),
                Font = DpiHelper.ScaleFont(new System.Drawing.Font("微软雅黑", 9F))
            };
            _pauseButton.Click += (s, e) => { _isPaused = true; _pauseButton.Enabled = false; _resumeButton.Enabled = true; };

            _clearButton = new Button 
            { 
                Text = "清理", 
                Width = DpiHelper.ScaleValue(80), 
                Height = DpiHelper.ScaleValue(35),
                Font = DpiHelper.ScaleFont(new System.Drawing.Font("微软雅黑", 9F))
            };
            _clearButton.Click += (s, e) => { _logTextBox.Clear(); };

            _resumeButton = new Button 
            { 
                Text = "恢复", 
                Width = DpiHelper.ScaleValue(80), 
                Height = DpiHelper.ScaleValue(35), 
                Enabled = false,
                Font = DpiHelper.ScaleFont(new System.Drawing.Font("微软雅黑", 9F))
            };
            _resumeButton.Click += (s, e) => { _isPaused = false; _pauseButton.Enabled = true; _resumeButton.Enabled = false; };

            FlowLayoutPanel buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = DpiHelper.ScaleValue(45),
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(DpiHelper.ScaleValue(10), DpiHelper.ScaleValue(5), DpiHelper.ScaleValue(10), DpiHelper.ScaleValue(5)),
                Margin = new Padding(0),
                AutoSize = false
            };
            _closeButton.Margin = new Padding(DpiHelper.ScaleValue(10), 0, 0, 0);
            _resumeButton.Margin = new Padding(DpiHelper.ScaleValue(10), 0, 0, 0);
            _pauseButton.Margin = new Padding(DpiHelper.ScaleValue(10), 0, 0, 0);
            _clearButton.Margin = new Padding(0);
            buttonPanel.Controls.Add(_closeButton);
            buttonPanel.Controls.Add(_resumeButton);
            buttonPanel.Controls.Add(_pauseButton);
            buttonPanel.Controls.Add(_clearButton);

            this.Controls.Add(_logTextBox);
            this.Controls.Add(buttonPanel);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            float dpiScale = DpiHelper.GetDpiScaleForWindow(this.Handle);
            DpiHelper.ApplyDpiScale(this);
        }

        private void LoadLogContent()
        {
            if (!File.Exists(_logFilePath)) return;

            Task.Run(() =>
            {
                try
                {
                    const int maxLinesToShow = 500;
                    string recentContent = ReadLastLines(_logFilePath, maxLinesToShow);

                    this.BeginInvoke((Action)(() =>
                    {
                        if (!this.IsDisposed)
                        {
                            _logTextBox.Text = recentContent;
                            _logTextBox.SelectionStart = _logTextBox.TextLength;
                            _logTextBox.ScrollToCaret();
                        }
                    }));
                }
                catch { }
            });
        }

        private string ReadLastLines(string filePath, int maxLines)
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long position = fs.Length;
                int linesRead = 0;
                byte[] buffer = new byte[1024];
                int bytesRead;

                while (position > 0 && linesRead < maxLines)
                {
                    int bytesToRead = (int)Math.Min(buffer.Length, position);
                    position -= bytesToRead;
                    fs.Position = position;
                    bytesRead = fs.Read(buffer, 0, bytesToRead);

                    for (int i = bytesRead - 1; i >= 0 && linesRead < maxLines; i--)
                    {
                        if (buffer[i] == '\n') linesRead++;
                    }
                }

                fs.Position = position;
                using (var reader = new StreamReader(fs, System.Text.Encoding.UTF8))
                {
                    string content = reader.ReadToEnd();
                    string[] lines = content.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length > maxLines)
                    {
                        string[] recentLines = new string[maxLines];
                        Array.Copy(lines, lines.Length - maxLines, recentLines, 0, maxLines);
                        return string.Join(Environment.NewLine, recentLines);
                    }
                    return content;
                }
            }
        }

        private void SetupLogCallback()
        {
            Logger.SetLogWindowCallback(OnLogReceived);
        }

        private void OnLogReceived(string logContent)
        {
            if (_isPaused) return;

            lock (_pendingContent)
            {
                _pendingContent.Append(logContent);
            }

            bool shouldInvoke = false;
            lock (_updateLock)
            {
                if (!_isUpdating)
                {
                    _isUpdating = true;
                    shouldInvoke = true;
                }
            }

            if (shouldInvoke && !this.IsDisposed && this.IsHandleCreated)
            {
                this.BeginInvoke((Action)(() => UpdateLogDisplay()));
            }
        }

        private void UpdateLogDisplay()
        {
            try
            {
                string content;
                lock (_pendingContent)
                {
                    content = _pendingContent.ToString();
                    _pendingContent.Clear();
                }

                if (!string.IsNullOrEmpty(content))
                {
                    bool shouldScroll = _logTextBox.SelectionStart == _logTextBox.TextLength;

                    const int maxLines = 2000;
                    if (_logTextBox.Lines.Length > maxLines)
                    {
                        string[] lines = _logTextBox.Lines;
                        string[] newLines = new string[maxLines];
                        Array.Copy(lines, lines.Length - maxLines, newLines, 0, maxLines);
                        _logTextBox.Lines = newLines;
                    }

                    _logTextBox.AppendText(content);

                    if (shouldScroll)
                    {
                        _logTextBox.SelectionStart = _logTextBox.TextLength;
                        _logTextBox.ScrollToCaret();
                    }
                }
            }
            catch { }
            finally
            {
                bool hasPendingContent = false;
                lock (_pendingContent)
                {
                    hasPendingContent = _pendingContent.Length > 0;
                }
                
                lock (_updateLock) { _isUpdating = false; }
                
                if (hasPendingContent && !this.IsDisposed && this.IsHandleCreated)
                {
                    this.BeginInvoke((Action)(() => UpdateLogDisplay()));
                }
            }
        }

        public static void ShowLogWindow()
        {
            if (Instance == null || Instance.IsDisposed)
            {
                Instance = new LogForm();
            }
            else
            {
                Instance.SetupLogCallback();
            }

            if (Instance.Visible)
            {
                Instance.Activate();
                if (Instance.WindowState == FormWindowState.Minimized)
                {
                    Instance.WindowState = FormWindowState.Normal;
                }
            }
            else
            {
                Instance.Show();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                Logger.SetLogWindowCallback(null);
                _pendingContent.Clear();
                e.Cancel = true;
                this.Hide();
                return;
            }

            Logger.SetLogWindowCallback(null);
            _pendingContent.Clear();
            base.OnFormClosing(e);
        }
    }
}
