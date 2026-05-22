using System;
using System.IO;
using System.Timers;
using System.Windows.Forms;

namespace WpsPasswordManager.UI
{
    public class LogForm : Form
    {
        private RichTextBox _logTextBox;
        private Button _closeButton;
        private System.Timers.Timer _refreshTimer;
        private string _logFilePath;
        private long _lastFileSize;

        public static LogForm Instance { get; private set; }

        public LogForm()
        {
            _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wps_password_manager.log");
            _lastFileSize = 0;
            InitializeComponent();
            LoadLogContent();
            StartLogMonitoring();
        }

        private void InitializeComponent()
        {
            this.Text = "WPS密码管理 - 日志";
            this.Size = new System.Drawing.Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;

            _logTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new System.Drawing.Font("Consolas", 9F),
                BackColor = System.Drawing.Color.Black,
                ForeColor = System.Drawing.Color.LightGreen,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = false
            };

            _closeButton = new Button
            {
                Text = "关闭",
                Dock = DockStyle.Bottom,
                Height = 35
            };
            _closeButton.Click += CloseButton_Click;

            this.Controls.Add(_logTextBox);
            this.Controls.Add(_closeButton);

            this.FormClosing += LogForm_FormClosing;
        }

        private void LoadLogContent()
        {
            try
            {
                if (File.Exists(_logFilePath))
                {
                    FileInfo fileInfo = new FileInfo(_logFilePath);
                    _lastFileSize = fileInfo.Length;
                    
                    const int maxLinesToShow = 100;
                    using (StreamReader reader = new StreamReader(_logFilePath, System.Text.Encoding.UTF8))
                    {
                        string[] allLines = reader.ReadToEnd().Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                        if (allLines.Length > maxLinesToShow)
                        {
                            string[] recentLines = new string[maxLinesToShow];
                            Array.Copy(allLines, allLines.Length - maxLinesToShow, recentLines, 0, maxLinesToShow);
                            _logTextBox.Text = string.Join(Environment.NewLine, recentLines);
                        }
                        else
                        {
                            _logTextBox.Text = string.Join(Environment.NewLine, allLines);
                        }
                    }
                    _logTextBox.SelectionStart = _logTextBox.TextLength;
                    _logTextBox.ScrollToCaret();
                }
            }
            catch { }
        }

        private void StartLogMonitoring()
        {
            _refreshTimer = new System.Timers.Timer(1000);
            _refreshTimer.Elapsed += RefreshTimer_Elapsed;
            _refreshTimer.Start();
        }

        private void RefreshTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (File.Exists(_logFilePath))
                {
                    FileInfo fileInfo = new FileInfo(_logFilePath);
                    if (fileInfo.Length > _lastFileSize)
                    {
                        using (FileStream fs = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (StreamReader reader = new StreamReader(fs))
                        {
                            fs.Seek(_lastFileSize, SeekOrigin.Begin);
                            string newContent = reader.ReadToEnd();
                            if (!string.IsNullOrEmpty(newContent))
                            {
                                this.Invoke((Action)(() =>
                                {
                                    _logTextBox.AppendText(newContent);
                                    _logTextBox.SelectionStart = _logTextBox.TextLength;
                                    _logTextBox.ScrollToCaret();
                                }));
                            }
                        }
                        _lastFileSize = fileInfo.Length;
                    }
                }
            }
            catch { }
        }

        private void CloseButton_Click(object sender, EventArgs e)
        {
            this.Hide();
        }

        private void LogForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
            }
        }

        public static void ShowLogWindow()
        {
            if (Instance == null || Instance.IsDisposed)
            {
                Instance = new LogForm();
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _refreshTimer?.Stop();
                _refreshTimer?.Dispose();
                _closeButton?.Dispose();
                _logTextBox?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}