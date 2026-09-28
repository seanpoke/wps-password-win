using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PasswordManager.UI.Controls
{
    /// <summary>
    /// 无边框窗口基类：还原原型 .window / .titlebar。
    /// —— 圆角 8px（Win11 DWM 圆角，与 border-radius:8px 一致）、标题栏高 34px、
    /// 标题 12px #1B1B1B 左缘 12px、右侧 40x32 窗口按钮（hover rgba(0,0,0,.06)，关闭 hover #C42B1C 白图标）。
    /// </summary>
    public class ThemedWindow : Form
    {
        public static int TitleBarHeight => Theme.S(34);      // .titlebar{height:34px}
        public static int TitleBarButtonWidth => Theme.S(40);  // .controls .btn{width:40px;height:32px}
        public static int TitleBarButtonHeight => Theme.S(32);
        public static int ContentPadding => Theme.S(32);       // .content{padding:clamp(18px,5vw,32px)} → 桌面取 32px

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCHITTEST = 0x84;
        private const int WM_NCLBUTTONDBLCLK = 0xA3;
        private const int HTCLIENT = 1;
        private const int HTCAPTION = 2;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMRIGHT = 16;
        private const int HTBOTTOMLEFT = 17;

        public bool ShowMinimizeButton { get; set; } = true;
        public bool ShowMaximizeButton { get; set; } = true;
        public bool ShowCloseButton { get; set; } = true;

        /// <summary>是否允许鼠标拖拽边缘缩放窗口（无边框窗体默认不可缩放）。</summary>
        public bool Resizable { get; set; }

        private enum TitleButtonType { None, Minimize, Maximize, Close }

        private TitleButtonType _hoverButton = TitleButtonType.None;
        private TitleButtonType _pressButton = TitleButtonType.None;

        public ThemedWindow()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Theme.Canvas;
            Font = Theme.Body;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;   // 逻辑像素基准 96DPI，与原型 px 一一对应
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                // Win11+：8px DWM 圆角 + 系统阴影（对应原型 border-radius:8px / box-shadow）
                int pref = DWMWCP_ROUND;
                DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
            }
            catch { }
        }

        // ===== 标题栏按钮命中 =====
        // 原型 .controls DOM 顺序（从左到右）：最小化、最大化、关闭；右缘 4px padding
        private Rectangle GetButtonRect(TitleButtonType type)
        {
            int x = RightButtonsStart();
            if (type == TitleButtonType.Minimize) { }
            else if (type == TitleButtonType.Maximize)
            {
                if (ShowMinimizeButton) x += TitleBarButtonWidth;
            }
            else if (type == TitleButtonType.Close)
            {
                if (ShowMinimizeButton) x += TitleBarButtonWidth;
                if (ShowMaximizeButton) x += TitleBarButtonWidth;
            }
            else return Rectangle.Empty;
            return new Rectangle(x, (TitleBarHeight - TitleBarButtonHeight) / 2, TitleBarButtonWidth, TitleBarButtonHeight);
        }

        private int RightButtonsStart()
        {
            int w = 0;
            if (ShowCloseButton) w += TitleBarButtonWidth;
            if (ShowMaximizeButton) w += TitleBarButtonWidth;
            if (ShowMinimizeButton) w += TitleBarButtonWidth;
            return ClientSize.Width - 4 - w;
        }

        private TitleButtonType HitTest(Point p)
        {
            foreach (TitleButtonType t in new[] { TitleButtonType.Minimize, TitleButtonType.Maximize, TitleButtonType.Close })
            {
                var rect = GetButtonRect(t);
                if (!rect.IsEmpty && rect.Contains(p))
                {
                    bool visible = t switch
                    {
                        TitleButtonType.Minimize => ShowMinimizeButton,
                        TitleButtonType.Maximize => ShowMaximizeButton,
                        _ => ShowCloseButton
                    };
                    if (visible) return t;
                }
            }
            return TitleButtonType.None;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCLBUTTONDBLCLK)
            {
                m.Result = IntPtr.Zero;   // 固定尺寸窗口：禁止双击标题栏最大化
                return;
            }
            base.WndProc(ref m);
            if (m.Msg == WM_NCHITTEST)
            {
                long lp = m.LParam.ToInt64();
                var screen = new Point(unchecked((short)(lp & 0xFFFF)), unchecked((short)((lp >> 16) & 0xFFFF)));
                Point p = PointToClient(screen);
                if (p.Y < TitleBarHeight && HitTest(p) == TitleButtonType.None)
                {
                    m.Result = (IntPtr)HTCAPTION;   // 标题栏空白区可拖动窗口
                }
                else if (Resizable && (int)m.Result == HTCLIENT)
                {
                    // 可缩放窗口：边缘 4px 返回缩放命中（仅对未被子控件覆盖的窗体表面生效）
                    int edge = Theme.S(4);
                    int w = ClientSize.Width, h = ClientSize.Height;
                    bool left = p.X <= edge, right = p.X >= w - edge, top = p.Y <= edge, bottom = p.Y >= h - edge;
                    int hit = 0;
                    if (top && left) hit = HTTOPLEFT;
                    else if (top && right) hit = HTTOPRIGHT;
                    else if (bottom && left) hit = HTBOTTOMLEFT;
                    else if (bottom && right) hit = HTBOTTOMRIGHT;
                    else if (left) hit = HTLEFT;
                    else if (right) hit = HTRIGHT;
                    else if (top) hit = HTTOP;
                    else if (bottom) hit = HTBOTTOM;
                    if (hit != 0) m.Result = (IntPtr)hit;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.Y < TitleBarHeight)
            {
                var h = HitTest(e.Location);
                if (h != _hoverButton)
                {
                    _hoverButton = h;
                    InvalidateTitleBar();
                }
            }
            else if (_hoverButton != TitleButtonType.None)
            {
                _hoverButton = TitleButtonType.None;
                InvalidateTitleBar();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverButton != TitleButtonType.None)
            {
                _hoverButton = TitleButtonType.None;
                InvalidateTitleBar();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            var t = HitTest(e.Location);
            if (t != TitleButtonType.None) _pressButton = t;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            var t = HitTest(e.Location);
            if (_pressButton != TitleButtonType.None && t == _pressButton)
            {
                switch (t)
                {
                    case TitleButtonType.Minimize:
                        WindowState = FormWindowState.Minimized;
                        break;
                    case TitleButtonType.Maximize:
                        OnMaximizeClicked();
                        break;
                    case TitleButtonType.Close:
                        OnCloseClicked();
                        break;
                }
            }
            _pressButton = TitleButtonType.None;
        }

        /// <summary>最大化按钮：默认无操作（登录等固定尺寸窗口）。子类可按需重写。</summary>
        protected virtual void OnMaximizeClicked() { }

        /// <summary>关闭按钮：默认关闭窗口，子类可重写为返回指定界面。</summary>
        protected virtual void OnCloseClicked()
        {
            Close();
        }

        private void InvalidateTitleBar()
        {
            Invalidate(new Rectangle(0, 0, ClientSize.Width, TitleBarHeight), false);
        }

        // ===== 绘制 =====
        protected void PaintTitleBar(Graphics g)
        {
            Theme.HiQuality(g);

            // 背景 + 底部分隔线
            using (var bg = new SolidBrush(Theme.Canvas))
                g.FillRectangle(bg, 0, 0, ClientSize.Width, TitleBarHeight);
            using (var line = new Pen(Theme.Divider, 1f))
                g.DrawLine(line, 0, TitleBarHeight - 0.5f, ClientSize.Width, TitleBarHeight - 0.5f);

            // 标题：12px #1B1B1B，左缘 12px，垂直居中于 34px
            using (var fr = new StringFormat
            {
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            })
            {
                using var tb = new SolidBrush(Theme.TextPrimary);
                var rect = new Rectangle(Theme.S(12), 0, Math.Max(0, RightButtonsStart() - Theme.S(12)), TitleBarHeight);
                g.DrawString(Text, Theme.Small, tb, rect, fr);
            }

            // 窗口按钮
            DrawTitleButton(g, TitleButtonType.Minimize, ShowMinimizeButton);
            DrawTitleButton(g, TitleButtonType.Maximize, ShowMaximizeButton);
            DrawTitleButton(g, TitleButtonType.Close, ShowCloseButton);
        }

        private void DrawTitleButton(Graphics g, TitleButtonType type, bool visible)
        {
            if (!visible) return;
            var rect = GetButtonRect(type);
            if (rect.IsEmpty) return;

            bool hover = _hoverButton == type;
            bool closeHover = hover && type == TitleButtonType.Close;

            // hover 背景：rgba(0,0,0,.06)；关闭按钮 hover：#C42B1C
            if (hover)
            {
                using var bg = new SolidBrush(closeHover ? Theme.Danger : Theme.TitleBtnHover);
                g.FillRectangle(bg, rect);
            }

            // 图标 10px 居中于按钮（.controls .btn svg 10x10）
            int isz = Theme.S(10);
            var icon = new Rectangle(
                rect.X + (rect.Width - isz) / 2,
                rect.Y + (rect.Height - isz) / 2, isz, isz);
            var color = closeHover ? Color.White : Theme.TextSecondary;
            switch (type)
            {
                case TitleButtonType.Minimize: Theme.DrawMinimizeIcon(g, icon, color); break;
                case TitleButtonType.Maximize: Theme.DrawMaximizeIcon(g, icon, color); break;
                case TitleButtonType.Close: Theme.DrawCloseIcon(g, icon, color); break;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            PaintTitleBar(e.Graphics);
        }
    }
}
