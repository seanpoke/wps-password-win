using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using PasswordManager.Utils;
using PasswordManager.UI.Controls;

namespace PasswordManager.UI
{
    /// <summary>
    /// 日志窗口（严格还原原型 app-prototype.html SCREEN 5「日志」）：
    /// 工具条（搜索框 / 等级分段筛选 / 自动滚动开关 / 清空）+ 富文本日志列表
    /// （日期分组、mono 时间、等级徽章、[来源] 灰显、关键词黄底高亮、暂停提示条、空状态）。
    /// 布局基准：窗口 720px 宽、96DPI 逻辑像素经 Theme.S 缩放。
    /// </summary>
    public class LogForm : ThemedWindow
    {
        private class LogEntry
        {
            public string Date = "";
            public string Time = "";
            public string Level = "INFO";
            public string Msg = "";
        }

        private class DisplayLine
        {
            public bool IsDate;
            public string Text = "";        // 日期行文本
            public string Date = "";        // 日志行所属日期
            public string Time = "";        // 日志行时间
            public string Level = "";       // 日志行等级
            public string Msg = "";         // 日志行消息
            public List<(int start, int len)> Marks = new();
            public List<(int start, int len)> TimeMarks = new();   // 时间列关键词高亮

            public string ToFullLine() => $"[{Time}] [{Level}] {Msg}";
        }

        private static readonly Regex LineRegex =
            new(@"^\[([0-9]{4}-[0-9]{2}-[0-9]{2}) ([0-9:.]+)\]\s*\[(\w+)\]\s?(.*)$", RegexOptions.Compiled);

        private readonly List<LogEntry> _entries = new();
        private readonly object _lock = new();
        private List<DisplayLine> _lines = new();

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHParseDisplayName(string name, IntPtr bindingContext, out IntPtr pidl, uint sfgaoIn, out uint sfgaoOut);

        [DllImport("shell32.dll")]
        private static extern int SHOpenFolderAndSelectItems(IntPtr pidlFolder, uint cidl, IntPtr[]? apidl, uint flags);

        [DllImport("shell32.dll")]
        private static extern void ILFree(IntPtr pidl);

        private SearchBox _searchBox = null!;
        private SegmentedControl _seg = null!;
        private ToggleSwitch _switch = null!;
        private Label _switchLabel = null!;
        private MiniButton _clearButton = null!;
        private MiniButton _openFolderButton = null!;
        private LogView _logView = null!;
        private VScrollBar _scrollBar = null!;

        private int _levelFilter;        // 0 全部 1 信息 2 警告 3 错误
        private bool _cleared;           // 清屏后显示"暂无日志记录"；新日志到来即恢复正常展示
        private string _logDirectory = "";

        public static LogForm Instance { get; private set; }

        public LogForm()
        {
            // 日志目录（日志按天分文件：log_yyyy-MM-dd.log）
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log");

            InitializeComponent();
            // 打开时从空白开始：不加载文件历史，仅实时显示打开之后新产生的日志
            Logger.SetLogWindowCallback(OnLogReceived);
        }

        private void InitializeComponent()
        {
            Text = "密码管理 · 日志";
            Resizable = true;   // 允许拖拽边缘缩放
            // 初始宽度保证工具条单行摆放（单行需约 714 逻辑宽）
            ClientSize = new Size(Theme.S(730), TitleBarHeight + Theme.S(52) + Theme.S(340));

            // ---- 工具条 ----
            _searchBox = new SearchBox("搜索日志内容");
            _searchBox.TextChanged += (_, _) => { RebuildLines(); ApplyLayout(); };

            _seg = new SegmentedControl(new[] { "全部", "信息", "警告", "错误" });
            _seg.SelectedChanged += (_, _) => { _levelFilter = _seg.Selected; RebuildLines(); };

            _switch = new ToggleSwitch();
            _switch.CheckedChanged += (_, _) => { _logView.AutoScroll = _switch.Checked; RebuildLines(); };

            _switchLabel = new Label
            {
                Text = "自动滚动",
                Font = Theme.Small,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _clearButton = new MiniButton("清空");
            _clearButton.Click += (_, _) =>
            {
                // 清屏：清掉当前内容；后续新日志继续实时展示
                lock (_lock) { _entries.Clear(); }
                _cleared = true;
                RebuildLines();
            };

            _openFolderButton = new MiniButton("打开目录");
            _openFolderButton.Click += (_, _) =>
            {
                // 管理员进程同步调用 explorer.exe 会因 DDE 会话阻塞 UI 线程（表现为点击无反应），
                // 放到后台线程执行：UI 立即响应，目录由系统延迟打开
                if (string.IsNullOrEmpty(_logDirectory)) return;
                if (!Directory.Exists(_logDirectory)) Directory.CreateDirectory(_logDirectory);
                Task.Run(() =>
                {
                    try
                    {
                        Logger.Info("点击打开日志目录");
                        System.Diagnostics.Process.Start("explorer.exe", _logDirectory);
                        Logger.Info("已发起打开日志目录");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"打开日志目录失败: {ex.Message}");
                    }
                });
            };

            // ---- 日志列表 ----
            _logView = new LogView();
            _logView.ResumeRequested += (_, _) => _switch.Checked = true;
            _scrollBar = new VScrollBar();

            Controls.AddRange(new Control[] { _searchBox, _seg, _switch, _switchLabel, _clearButton, _openFolderButton, _logView, _scrollBar });

            _scrollBar.ValueChanged += (_, _) => { _logView.ScrollOffset = _scrollBar.Value; _logView.Invalidate(); };
            _logView.ScrollChanged += offset =>
            {
                if (_scrollBar.Value != offset) _scrollBar.Value = Math.Max(_scrollBar.Minimum, Math.Min(_scrollBar.Maximum, offset));
            };

            ApplyLayout();
        }

        protected override void OnMaximizeClicked()
        {
            // 日志窗口支持最大化/还原
            WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        }

        private int _barHeight;   // 工具条当前高度（随换行布局变化）

        private void ApplyLayout()
        {
            int edge = Theme.S(4);   // 窗体边缘留白：供鼠标拖拽缩放命中（背景与窗体同色，视觉无边）
            int barTop = TitleBarHeight;
            int searchW = Theme.S(210);
            int segW = Theme.S(210);
            int clearW = Theme.S(64);
            int folderW = Theme.S(76);
            int searchY = barTop + Theme.S(10);
            int clearRight = ClientSize.Width - edge - Theme.S(12);

            // 原型 .logbar{flex-wrap:wrap}：宽度不足以单行摆放时换行——
            // 行1：搜索框 + 等级筛选；行2（右对齐）：自动滚动开关 + 打开目录 + 清空
            int singleLineWidth = Theme.S(714);   // 单行布局所需宽度（各元素+间距累加）
            bool wrap = ClientSize.Width < singleLineWidth;

            _searchBox.SetBounds(edge + Theme.S(12), searchY, searchW, Theme.S(32));
            _seg.SetBounds(_searchBox.Right + Theme.S(10), searchY + Theme.S(1), segW, Theme.S(30));

            if (!wrap)
            {
                _clearButton.SetBounds(clearRight - clearW, searchY, clearW, Theme.S(32));
                _openFolderButton.SetBounds(_clearButton.Left - Theme.S(8) - folderW, searchY, folderW, Theme.S(32));
                _switchLabel.SetBounds(_openFolderButton.Left - Theme.S(8) - Theme.S(64), searchY, Theme.S(64), Theme.S(32));
                _switch.SetBounds(_switchLabel.Left - Theme.S(8) - Theme.S(36), barTop + Theme.S(16), Theme.S(36), Theme.S(20));
                _barHeight = Theme.S(52);
            }
            else
            {
                int row2Y = searchY + Theme.S(32) + Theme.S(10);
                _clearButton.SetBounds(clearRight - clearW, row2Y, clearW, Theme.S(32));
                _openFolderButton.SetBounds(_clearButton.Left - Theme.S(8) - folderW, row2Y, folderW, Theme.S(32));
                _switchLabel.SetBounds(_openFolderButton.Left - Theme.S(8) - Theme.S(64), row2Y, Theme.S(64), Theme.S(32));
                _switch.SetBounds(_switchLabel.Left - Theme.S(8) - Theme.S(36), row2Y + Theme.S(6), Theme.S(36), Theme.S(20));
                _barHeight = Theme.S(10) + Theme.S(32) + Theme.S(10) + Theme.S(32) + Theme.S(10);
            }

            int viewTop = barTop + _barHeight;
            _logView.SetBounds(edge, viewTop, ClientSize.Width - edge * 2 - Theme.S(10), ClientSize.Height - viewTop - edge);
            _scrollBar.SetBounds(ClientSize.Width - edge - Theme.S(10), viewTop, Theme.S(10), ClientSize.Height - viewTop - edge);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_logView != null) ApplyLayout();
        }

        // =====================================================================
        // 日志数据：实时回调 / 过滤渲染（窗口打开时从空白开始，不加载文件历史）
        // =====================================================================
        /// <summary>等级归一化：WARNING→WARN、ERROR/ERR→ERROR、INFORMATION→INFO。</summary>
        private static string NormalizeLevel(string level) => level switch
        {
            "WARNING" or "WARN" => "WARN",
            "ERROR" or "ERR" => "ERROR",
            "INFORMATION" or "INFO" => "INFO",
            _ => level
        };

        private static LogEntry? ParseLine(string line)
        {
            var m = LineRegex.Match(line);
            if (m.Success)
            {
                return new LogEntry { Date = m.Groups[1].Value, Time = m.Groups[2].Value, Level = NormalizeLevel(m.Groups[3].Value.ToUpperInvariant()), Msg = m.Groups[4].Value };
            }
            return new LogEntry { Time = "", Level = "INFO", Msg = line };   // 堆栈等续行
        }

        private void OnLogReceived(string logContent)
        {
            // 自动滚动关闭 = 暂停接收：期间新日志不打印，点"恢复跟随"后继续
            if (IsDisposed || Disposing || !_switch.Checked) return;
            var parsed = new List<LogEntry>();
            foreach (var line in logContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var entry = ParseLine(line);
                if (entry != null) parsed.Add(entry);
            }

            bool added = false;
            lock (_lock)
            {
                _entries.AddRange(parsed);
                if (_entries.Count > 2000)
                    _entries.RemoveRange(0, _entries.Count - 2000);
                added = parsed.Count > 0;
                if (added) _cleared = false;   // 新日志到来，退出清屏空状态
            }

            if (added && IsHandleCreated)
            {
                try { BeginInvoke((Action)RebuildLines); } catch { }
            }
        }

        private static string LevelGroup(string level) =>
            level == "WARN" ? "warn" : level == "ERROR" ? "error" : "info";

        /// <summary>重建过滤后的渲染行（日期分组 + 关键词高亮标记）。</summary>
        private void RebuildLines()
        {
            var lines = new List<DisplayLine>();
            string keyword = (_searchBox.Inner.Text ?? "").Trim();
            List<LogEntry> list;
            lock (_lock) { list = _entries.ToList(); }

            foreach (var e in list)
            {
                string group = LevelGroup(e.Level);
                if (_levelFilter == 1 && group != "info") continue;
                if (_levelFilter == 2 && group != "warn") continue;
                if (_levelFilter == 3 && group != "error") continue;
                if (!string.IsNullOrEmpty(keyword))
                {
                    string fullTime = string.IsNullOrEmpty(e.Date) ? e.Time : $"{e.Date} {e.Time}";
                    if (e.Msg.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0 &&
                        fullTime.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0) continue;
                }

                // 每行直接展示完整时间：yyyy-MM-dd HH:mm:ss.fff（不做日期分组行）
                var dl = new DisplayLine { Date = e.Date, Time = string.IsNullOrEmpty(e.Date) ? e.Time : $"{e.Date} {e.Time}", Level = e.Level, Msg = e.Msg };
                if (!string.IsNullOrEmpty(keyword))
                {
                    // 搜索同时匹配消息与完整时间（如搜"36"命中秒/毫秒含 36 的行）
                    int idx = 0;
                    while ((idx = e.Msg.IndexOf(keyword, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
                    {
                        dl.Marks.Add((idx, keyword.Length));
                        idx += keyword.Length;
                    }
                    idx = 0;
                    while ((idx = dl.Time.IndexOf(keyword, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
                    {
                        dl.TimeMarks.Add((idx, keyword.Length));
                        idx += keyword.Length;
                    }
                }
                lines.Add(dl);
            }

            _lines = lines;
            _logView.SetLines(_lines, keyword, _cleared);
            UpdateScrollBar();
            if (_switch.Checked) ScrollToEnd();
        }

        private void UpdateScrollBar()
        {
            int contentH = _logView.ContentHeight;
            int viewH = _logView.Height;
            if (contentH <= viewH || viewH <= 0)
            {
                _scrollBar.Enabled = false;
                _scrollBar.Maximum = 0;
            }
            else
            {
                _scrollBar.Enabled = true;
                _scrollBar.Minimum = 0;
                _scrollBar.Maximum = Math.Max(0, contentH - viewH);
                _scrollBar.LargeChange = Math.Max(1, viewH);
                _scrollBar.SmallChange = Theme.S(28);
            }
        }

        private void ScrollToEnd()
        {
            _scrollBar.Value = _scrollBar.Maximum;
            _logView.ScrollOffset = _scrollBar.Maximum;
            _logView.Invalidate();
        }

        public static void ShowLogWindow()
        {
            // 可见则激活；已关闭则销毁重建——重新加载文件日志，不保留上次屏幕打印的内容
            if (Instance != null && !Instance.IsDisposed && Instance.Visible)
            {
                Instance.Activate();
                if (Instance.WindowState == FormWindowState.Minimized)
                    Instance.WindowState = FormWindowState.Normal;
                return;
            }

            if (Instance != null && !Instance.IsDisposed)
                Instance.Dispose();
            Instance = new LogForm();
            Instance.Show();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                Logger.SetLogWindowCallback(null);
                e.Cancel = true;
                Hide();
                return;
            }
            Logger.SetLogWindowCallback(null);
            base.OnFormClosing(e);
        }

        // =====================================================================
        // 日志列表视图：白底、日期分组行、mono 时间、等级徽章、[来源] 灰显、
        // 关键词黄底高亮、底部暂停提示条（深底圆角 + "恢复跟随"）、空状态。
        // =====================================================================
        private class LogView : Control
        {
            private List<DisplayLine> _lines = new();
            private string _keyword = "";
            private bool _emptied;
            private bool _autoScroll = true;
            private int _scrollOffset;
            private Rectangle _resumeRect = Rectangle.Empty;
            private int _selected = -1;                 // 选中的日志行索引
            private ContextMenuStrip? _contextMenu;

            public event EventHandler? ResumeRequested;
            public event Action<int>? ScrollChanged;

            public bool AutoScroll { get => _autoScroll; set => _autoScroll = value; }
            public int ScrollOffset { get => _scrollOffset; set => _scrollOffset = Math.Max(0, value); }
            public int ContentHeight
            {
                get
                {
                    int h = Theme.S(12);
                    foreach (var l in _lines)
                        h += l.IsDate ? Theme.S(32) : Theme.S(28);
                    return h + Theme.S(12);
                }
            }

            public LogView()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                         ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
                BackColor = Theme.FieldRest;
                TabStop = true;
            }

            public void SetLines(List<DisplayLine> lines, string keyword, bool emptied)
            {
                _lines = lines;
                _keyword = keyword;
                _emptied = emptied;
                if (_selected >= _lines.Count) _selected = -1;
                Invalidate();
            }

            protected override bool IsInputKey(Keys keyData) => true;

            protected override void OnKeyDown(KeyEventArgs e)
            {
                base.OnKeyDown(e);
                if (e.Control && e.KeyCode == Keys.C) { CopySelected(); e.Handled = true; }
            }

            private void CopySelected()
            {
                if (_selected < 0 || _selected >= _lines.Count) return;
                var l = _lines[_selected];
                if (l.IsDate) return;
                try { Clipboard.SetText(l.ToFullLine()); } catch { }
            }

            private void ShowContextMenu(Point location)
            {
                if (_selected < 0 || _selected >= _lines.Count) return;
                var l = _lines[_selected];
                if (l.IsDate) return;

                _contextMenu ??= new ContextMenuStrip
                {
                    Renderer = new PrototypeMenuRenderer(),
                    ShowImageMargin = false,
                    ShowCheckMargin = false
                };
                _contextMenu.Padding = new Padding(Theme.S(4));
                _contextMenu.Items.Clear();

                var copyLine = new PrototypeMenuItem("复制此行", "copy");
                copyLine.Click += (_, _) => { try { Clipboard.SetText(l.ToFullLine()); } catch { } };
                var copyMsg = new PrototypeMenuItem("复制消息内容", "");
                copyMsg.Click += (_, _) => { try { Clipboard.SetText(l.Msg); } catch { } };
                var copyAll = new PrototypeMenuItem("复制全部日志", "");
                copyAll.Click += (_, _) =>
                {
                    try
                    {
                        var sb = new System.Text.StringBuilder();
                        foreach (var line in _lines)
                            sb.AppendLine(line.IsDate ? line.Text : line.ToFullLine());
                        Clipboard.SetText(sb.ToString());
                    }
                    catch { }
                };
                _contextMenu.Items.AddRange(new ToolStripItem[] { copyLine, copyMsg, copyAll });
                MenuDwm.ApplyRound(_contextMenu.Handle);
                _contextMenu.Show(this, location);
            }

            /// <summary>命中测试：返回 (行索引, 行矩形)；未命中返回 (-1, Empty)。</summary>
            private (int index, Rectangle rect) HitTest(Point p)
            {
                int y = Theme.S(12) - _scrollOffset;
                for (int i = 0; i < _lines.Count; i++)
                {
                    int rowH = _lines[i].IsDate ? Theme.S(32) : Theme.S(28);
                    var rect = new Rectangle(0, y, Width, rowH);
                    if (p.Y >= rect.Top && p.Y < rect.Bottom) return (i, rect);
                    y += rowH;
                }
                return (-1, Rectangle.Empty);
            }

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                base.OnMouseWheel(e);
                ScrollOffset -= Math.Sign(e.Delta) * Theme.S(56);
                ScrollChanged?.Invoke(_scrollOffset);
                Invalidate();
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                base.OnMouseClick(e);
                if (_resumeRect.Contains(e.Location) && !_autoScroll)
                {
                    ResumeRequested?.Invoke(this, EventArgs.Empty);
                    return;
                }

                var (index, _) = HitTest(e.Location);
                if (index >= 0 && !_lines[index].IsDate)
                {
                    _selected = index;
                    Focus();
                }
                else
                {
                    _selected = -1;
                }
                Invalidate();
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                if (e.Button == MouseButtons.Right)
                {
                    var (index, rect) = HitTest(e.Location);
                    if (index >= 0 && !_lines[index].IsDate)
                    {
                        _selected = index;
                        Invalidate();
                        ShowContextMenu(new Point(rect.Left + Theme.S(60), rect.Bottom));
                    }
                }
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                Cursor = _resumeRect.Contains(e.Location) && !_autoScroll ? Cursors.Hand : Cursors.Default;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                Theme.HiQuality(g);
                using (var bg = new SolidBrush(Theme.FieldRest))
                    g.FillRectangle(bg, ClientRectangle);

                if (_lines.Count == 0)
                {
                    DrawEmpty(g);
                    return;
                }

                int y = Theme.S(12) - _scrollOffset;
                int timeW = Theme.S(180);   // "yyyy-MM-dd HH:mm:ss.fff" 完整时间列宽（含毫秒）
                for (int i = 0; i < _lines.Count; i++)
                {
                    var l = _lines[i];
                    int rowH = Theme.S(28);
                    if (y + rowH < 0) { y += rowH; continue; }
                    if (y > Height) break;

                    var rowRect = new Rectangle(0, y, Width, rowH);
                    // 选中行：淡蓝背景（区别于悬停灰）
                    if (i == _selected)
                    {
                        using var sb = new SolidBrush(Theme.InfoBg);
                        g.FillRectangle(sb, rowRect);
                    }
                    else if (rowRect.Contains(PointToClient(Cursor.Position)))
                    {
                        using var hb = new SolidBrush(Theme.FieldHover);
                        g.FillRectangle(hb, rowRect);
                    }

                    // 时间（mono 12px #878787，关键词黄底高亮）——共享字体严禁 dispose
                    var tf = Theme.MonoSmall;
                    using (var tb = new SolidBrush(Theme.TextTertiary))
                    {
                        if (l.TimeMarks.Count > 0)
                        {
                            float dx = Theme.S(12);
                            int p = 0;
                            foreach (var (s, len) in l.TimeMarks)
                            {
                                if (s > p)
                                {
                                    string seg = l.Time.Substring(p, s - p);
                                    SizeF ts = g.MeasureString(seg, tf, Theme.S(220), fmt(seg));
                                    g.DrawString(seg, tf, tb, new RectangleF(dx, y, ts.Width + 8, rowH), fmt(seg));
                                    dx += ts.Width - 6;
                                }
                                string mk = l.Time.Substring(s, len);
                                SizeF ms = g.MeasureString(mk, tf, Theme.S(220), fmt(mk));
                                using var mb = new SolidBrush(Theme.SelBg);
                                g.FillRectangle(mb, dx, y + Theme.S(2), ms.Width, rowH - Theme.S(4));
                                using var mb2 = new SolidBrush(Theme.TextPrimary);
                                g.DrawString(mk, tf, mb2, new RectangleF(dx, y, ms.Width + 8, rowH), fmt(mk));
                                dx += ms.Width - 6;
                                p = s + len;
                            }
                            if (p < l.Time.Length)
                            {
                                string seg = l.Time.Substring(p);
                                SizeF ts = g.MeasureString(seg, tf, Theme.S(220), fmt(seg));
                                g.DrawString(seg, tf, tb, new RectangleF(dx, y, ts.Width + 8, rowH), fmt(seg));
                            }
                        }
                        else
                        {
                            g.DrawString(l.Time, tf, tb, new RectangleF(Theme.S(12), y, timeW, rowH), fmt(l.Time));
                        }
                    }

                    // 等级徽章（mono 11px bold、圆角 9）
                    DrawBadge(g, l.Level, new Rectangle(Theme.S(12) + timeW + Theme.S(10), y + (rowH - Theme.S(18)) / 2, Theme.S(52), Theme.S(18)));

                    // 消息（[来源] 灰显 + 关键词黄底）
                    DrawMessage(g, l.Msg, l.Marks, new RectangleF(Theme.S(12) + timeW + Theme.S(10) + Theme.S(52) + Theme.S(10), y, Width - Theme.S(12) - (Theme.S(12) + timeW + Theme.S(10) + Theme.S(52) + Theme.S(10)), rowH));

                    y += rowH;
                }

                // 暂停提示条（原型 .log-hint：sticky 底部、#2B2B2B 圆角 6、高 36）
                if (!_autoScroll)
                {
                    int hw = Width - Theme.S(20);
                    _resumeRect = new Rectangle(Theme.S(10), Height - Theme.S(10) - Theme.S(36), hw, Theme.S(36));
                    using var path = Theme.RoundedRect(_resumeRect, Theme.S(6));
                    using (var bg = new SolidBrush(Color.FromArgb(0x2B, 0x2B, 0x2B)))
                        g.FillPath(bg, path);
                    using var fmt = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
                    using (var b = new SolidBrush(Color.White))
                        g.DrawString("已暂停自动滚动", Theme.Small, b, new RectangleF(_resumeRect.X + Theme.S(12), _resumeRect.Y, 300, _resumeRect.Height), fmt);
                    SizeF ts = g.MeasureString("恢复跟随", Theme.Small);
                    using (var b = new SolidBrush(Color.FromArgb(0x8C, 0xC8, 0xFF)))
                        g.DrawString("恢复跟随", Theme.Small, b, new RectangleF(_resumeRect.Right - Theme.S(12) - ts.Width, _resumeRect.Y, ts.Width + 8, _resumeRect.Height), fmt);
                }
                else
                {
                    _resumeRect = Rectangle.Empty;
                }
            }

            private static StringFormat fmt(string s) => new()
            {
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap,
                Trimming = StringTrimming.EllipsisCharacter
            };

            private void DrawBadge(Graphics g, string level, Rectangle r)
            {
                Color bg = level == "WARN" ? Theme.WarnBg : level == "ERROR" ? Theme.DangerBg : Theme.InfoBg;
                Color fg = level == "WARN" ? Theme.Warn : level == "ERROR" ? Theme.Danger : Theme.LogInfoFg;
                using (var path = Theme.RoundedRect(r, Theme.S(9)))
                using (var b = new SolidBrush(bg))
                    g.FillPath(b, path);
                using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
                using var b2 = new SolidBrush(fg);
                g.DrawString(level, Theme.MonoTinyBold, b2, r, fmt);
            }

            private void DrawMessage(Graphics g, string msg, List<(int start, int len)> marks, RectangleF rect)
            {
                // 拆 [来源] 段（.src 灰显），再在段内叠加关键词黄底
                var srcSegs = new List<(string text, bool src)>();
                int pos = 0;
                var rx = new Regex(@"\[[^\]]+\]");
                foreach (Match m in rx.Matches(msg))
                {
                    if (m.Index > pos) srcSegs.Add((msg.Substring(pos, m.Index - pos), false));
                    srcSegs.Add((m.Value, true));
                    pos = m.Index + m.Length;
                }
                if (pos < msg.Length) srcSegs.Add((msg.Substring(pos), false));

                using var fmt = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter };
                float x = rect.X;
                using var fp = new SolidBrush(Theme.TextPrimary);
                using var sp = new SolidBrush(Theme.TextSecondary);
                foreach (var (text, isSrc) in srcSegs)
                {
                    if (x > rect.Right) break;
                    // 关键词 mark 段拆分
                    var pieces = new List<(string text, bool mark)>();
                    if (_keyword.Length > 0)
                    {
                        int p = 0;
                        int idx;
                        while ((idx = text.IndexOf(_keyword, p, StringComparison.OrdinalIgnoreCase)) >= 0)
                        {
                            if (idx > p) pieces.Add((text.Substring(p, idx - p), false));
                            pieces.Add((text.Substring(idx, _keyword.Length), true));
                            p = idx + _keyword.Length;
                        }
                        if (p < text.Length) pieces.Add((text.Substring(p), false));
                        if (pieces.Count == 0) pieces.Add((text, false));
                    }
                    else pieces.Add((text, false));

                    foreach (var (piece, isMark) in pieces)
                    {
                        if (string.IsNullOrEmpty(piece)) continue;
                        SizeF ts = g.MeasureString(piece, Theme.Input, (int)Math.Max(10, rect.Right - x), fmt);
                        if (isMark)
                        {
                            using var mb = new SolidBrush(Theme.SelBg);
                            g.FillRectangle(mb, x, rect.Y + Theme.S(2), ts.Width, rect.Height - Theme.S(4));
                        }
                        using var b = new SolidBrush(isSrc && !isMark ? Theme.TextSecondary : Theme.TextPrimary);
                        g.DrawString(piece, Theme.Input, b, new RectangleF(x, rect.Y, ts.Width + 8, rect.Height), fmt);
                        x += ts.Width - 6;
                    }
                }
            }

            private void DrawEmpty(Graphics g)
            {
                string title = _emptied ? "暂无日志记录" : "未找到匹配的日志";
                string sub = _emptied ? "程序运行后的操作与异常会记录在这里。" : "试试更换关键词，或把等级筛选切回「全部」。";

                int isz = Theme.S(32);
                var icon = new Rectangle((Width - isz) / 2, Height / 2 - Theme.S(50), isz, isz);
                Theme.DrawDocument(g, icon, Color.FromArgb(0xC7, 0xC7, 0xC7));

                using var fmt = new StringFormat { Alignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
                using (var b = new SolidBrush(Theme.TextSecondary))
                    g.DrawString(title, Theme.Input, b, new RectangleF(0, Height / 2 - Theme.S(4), Width, Theme.S(20)), fmt);
                using (var b = new SolidBrush(Theme.TextTertiary))
                    g.DrawString(sub, Theme.Small, b, new RectangleF(0, Height / 2 + Theme.S(18), Width, Theme.S(16)), fmt);
            }
        }
    }
}
