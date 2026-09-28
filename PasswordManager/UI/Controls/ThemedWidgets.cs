using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PasswordManager.Utils;

namespace PasswordManager.UI.Controls
{
    // =====================================================================
    // 文本框：原型 .textbox input
    // 高 36px、白底、1px #8A8A8A 边框、圆角 4px、内边距 0 12px、字 14px；
    // hover 边框 #616161；focus 边框 #005FB8 + 底部内侧 2px accent 下划线；
    // disabled 背景 #F5F5F5 文字 #878787；has-error 边框 #C42B1C + 底部 2px danger。
    // 密码框右侧 4px 处有 32px 宽"小眼睛"，图标 18px，文本右让 40px。
    // =====================================================================
    public class ThemedTextBox : Panel
    {
        public static int BoxHeight => Theme.S(36);    // .textbox input{height:36px}
        public static int BoxPadding => Theme.S(12);   // padding:0 12px
        public static int EyeZone => Theme.S(40);      // input[type=password]{padding-right:40px}

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
        private const int EM_SETCUEBANNER = 0x1501;

        public TextBox Inner { get; }
        private readonly bool _mono;
        private readonly bool _hasEye;
        private bool _eyeOn;      // true=明文（eye 处于 .on，显示带斜线图标）
        private bool _eyeHover;

        private bool _hover, _hasError;

        /// <summary>眼睛按钮区域（原型 .eye：right:4px、宽 32、上下撑满）。</summary>
        private Rectangle EyeRect => new(Width - Theme.S(4) - Theme.S(32), 0, Theme.S(32), Height);

        public bool HasError
        {
            get => _hasError;
            set { _hasError = value; Invalidate(); }
        }

        public bool Password
        {
            get => Inner.PasswordChar != '\0';
            set => Inner.PasswordChar = value ? '●' : '\0';
        }

        public string Placeholder
        {
            set
            {
                if (Inner.IsHandleCreated) SendMessage(Inner.Handle, EM_SETCUEBANNER, (IntPtr)1, value ?? "");
                else Inner.HandleCreated += (s, e) => SendMessage(Inner.Handle, EM_SETCUEBANNER, (IntPtr)1, value ?? "");
            }
        }

        public new bool Enabled
        {
            get => Inner.Enabled;
            set
            {
                Inner.Enabled = value;
                Invalidate();
            }
        }

        public bool ReadOnly
        {
            get => Inner.ReadOnly;
            set => Inner.ReadOnly = value;
        }

        /// <summary>拖拽文件悬停时的虚线高亮（原型 .textbox.read.drop）。</summary>
        public bool DropHighlight { get; set; }

        public ThemedTextBox(bool mono = false, bool password = false, string? placeholder = null, bool eye = false)
        {
            _mono = mono;
            _hasEye = eye && password;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            Inner = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = mono ? Theme.MonoPx(13) : Theme.Input,
                ForeColor = Theme.TextPrimary,
                BackColor = Theme.FieldRest,
                PasswordChar = password ? '●' : '\0'
            };
            Controls.Add(Inner);

            if (placeholder != null) Placeholder = placeholder;

            Inner.TextChanged += (s, e) => Relayout();
            Inner.EnabledChanged += (s, e) => Invalidate();
            Inner.Enter += (s, e) => { Invalidate(); UpdateHover(); };
            Inner.Leave += (s, e) => { Invalidate(); UpdateHover(); };
            Inner.MouseEnter += (s, e) => UpdateHover();
            Inner.MouseLeave += (s, e) => UpdateHover();
            Inner.MouseMove += (s, e) => UpdateHover();
            MouseEnter += (s, e) => UpdateHover();
            MouseLeave += (s, e) => UpdateHover();
            MouseMove += (s, e) => UpdateHover();
            Resize += (s, e) => Relayout();
            HandleCreated += (s, e) => Relayout();
        }

        private void UpdateHover()
        {
            bool h = ClientRectangle.Contains(PointToClient(Cursor.Position));
            if (_hasEye && h)
            {
                var p = PointToClient(Cursor.Position);
                bool eh = EyeRect.Contains(p);
                Cursor = eh ? Cursors.Hand : Cursors.IBeam;
                if (eh != _eyeHover) { _eyeHover = eh; Invalidate(); }
            }
            else if (_eyeHover)
            {
                _eyeHover = false;
                Cursor = Cursors.IBeam;
                Invalidate();
            }
            if (h != _hover) { _hover = h; Invalidate(); }
        }

        private void Relayout()
        {
            Height = BoxHeight;
            int innerX = BoxPadding;
            int innerW = Width - BoxPadding * 2 - (_hasEye ? EyeZone - BoxPadding : 0);
            // 垂直居中
            int ih = Inner.PreferredHeight;
            Inner.SetBounds(innerX, (BoxHeight - ih) / 2, Math.Max(10, innerW), ih);
            UpdateHover();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.HiQuality(g);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color border =
                _hasError ? Theme.Danger :
                Inner.Focused ? Theme.Accent :
                _hover ? Theme.StrokeHover :
                Theme.Stroke;

            using (var bg = new SolidBrush(Inner.Enabled ? Theme.FieldRest : Theme.FieldDisabled))
                g.FillRectangle(bg, rect);

            using var path = Theme.RoundedRect(rect, Theme.S(4));
            using (var pen = new Pen(border, 1f))
                g.DrawPath(pen, path);

            // 拖拽文件悬停：accent 虚线外框（原型 .textbox.read.drop）
            if (DropHighlight)
            {
                using var dpen = new Pen(Theme.Accent, 1f) { DashStyle = DashStyle.Dash };
                var outer = new Rectangle(1, 1, Width - 3, Height - 3);
                using var opath = Theme.RoundedRect(outer, Theme.S(4));
                g.DrawPath(dpen, opath);
            }

            // focus / error 底部 2px 下划线（inset 0 -2px 0 0）
            Color? under = _hasError ? Theme.Danger : (Inner.Focused ? (Color?)Theme.Accent : null);
            if (under.HasValue)
            {
                using var upen = new Pen(under.Value, Theme.S(2));
                g.DrawLine(upen, Theme.S(2), Height - Theme.S(2) + 0.5f, Width - Theme.S(2), Height - Theme.S(2) + 0.5f);
            }

            Inner.ForeColor = Inner.Enabled ? Theme.TextPrimary : Theme.TextTertiary;
            Inner.BackColor = Inner.Enabled ? Theme.FieldRest : Theme.FieldDisabled;

            // 眼睛图标（原型 .eye：18px，右缘 4px 处 32px 热区；hover 变深；明文时切换为带斜线图标）
            if (_hasEye)
            {
                int isz = Theme.S(18);
                var icon = new Rectangle(EyeRect.X + (EyeRect.Width - isz) / 2, (Height - isz) / 2, isz, isz);
                Theme.DrawEye(g, icon, _eyeHover ? Theme.TextPrimary : Theme.TextSecondary, _eyeOn);
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (_hasEye && e.Button == MouseButtons.Left && EyeRect.Contains(e.Location))
            {
                _eyeOn = !_eyeOn;
                Inner.PasswordChar = _eyeOn ? '\0' : '●';
                Invalidate();
                Inner.Focus();
            }
        }
    }

    // =====================================================================
    // 加载旋转环：原型 .ring —— 16px、2px 边框 rgba(255,255,255,.4)、顶部 1/4 白色弧、1.5s/圈
    // =====================================================================
    public class SpinnerRing : Control
    {
        private readonly System.Windows.Forms.Timer _timer = new() { Interval = 15 };
        private float _angle;

        public SpinnerRing()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(Theme.S(16), Theme.S(16));
            _timer.Tick += (s, e) => { _angle += 3.6f; if (_angle >= 360) _angle -= 360; Invalidate(); };
        }

        public void Start() { _timer.Start(); Visible = true; Invalidate(); }
        public void Stop() { _timer.Stop(); Visible = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.HiQuality(g);
            int m = Theme.S(2);
            var r = new Rectangle(m, m, Width - m * 2, Height - m * 2);
            using (var dim = new Pen(Color.FromArgb(102, 255, 255, 255), Theme.S(2)))
                g.DrawArc(dim, r, 0, 360);
            using (var top = new Pen(Color.White, Theme.S(2)))
                g.DrawArc(top, r, -90 + _angle, 100);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }
    }

    // =====================================================================
    // 按钮：原型 .btn-primary / .btn-secondary
    // 高 40px、圆角 4px、字 14px；primary #005FB8（hover #0067C0 / pressed #1975C5 / disabled #C5C5C5）；
    // secondary 白底 1px #8A8A8A（hover #F5F5F5+#616161）；secondary.danger hover 红字红边红底。
    // Loading：文字前 16px 白色旋转环。
    // =====================================================================
    public enum ThemedButtonStyle { Primary, Secondary, SecondaryDanger, DangerPrimary }

    public class ThemedButton : Control
    {
        public ThemedButtonStyle Style { get; }
        private readonly SpinnerRing _ring = new() { Visible = false };
        private bool _hover, _pressed;

        public bool Loading
        {
            get => _ring.Visible;
            set
            {
                if (value) _ring.Start(); else _ring.Stop();
                Invalidate();
            }
        }

        public static int ButtonHeight => Theme.S(40);   // .btn-primary{height:40px}

        public ThemedButton(string text, ThemedButtonStyle style)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            Text = text;
            Style = style;
            Height = ButtonHeight;
            Cursor = Cursors.Hand;
            Font = Theme.Input;
            Controls.Add(_ring);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; _pressed = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); } }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _pressed = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.HiQuality(g);
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            bool active = Enabled && !Loading;

            if (Style == ThemedButtonStyle.Primary || Style == ThemedButtonStyle.DangerPrimary)
            {
                bool danger = Style == ThemedButtonStyle.DangerPrimary;
                Color rest = danger ? Theme.Danger : Theme.Accent;
                Color hov = danger ? Theme.DangerHover : Theme.AccentHover;
                Color prs = danger ? Theme.DangerHover : Theme.AccentPressed;
                Color bg = !active ? Theme.AccentDisabled
                    : _pressed ? prs
                    : _hover ? hov
                    : rest;
                using var path = Theme.RoundedRect(rect, Theme.S(4));
                using (var brush = new SolidBrush(bg)) g.FillPath(brush, path);
                DrawContent(g, Color.White);
            }
            else
            {
                Color bg = !active ? Theme.FieldRest : (_hover ? Theme.FieldHover : Theme.FieldRest);
                Color border = !active ? Theme.Stroke : (_hover ? Theme.StrokeHover : Theme.Stroke);
                bool dangerHover = Style == ThemedButtonStyle.SecondaryDanger && _hover && active;
                if (dangerHover) border = Theme.Danger;

                using var path = Theme.RoundedRect(rect, Theme.S(4));
                using (var brush = new SolidBrush(dangerHover ? Theme.DangerBg : bg)) g.FillPath(brush, path);
                using (var pen = new Pen(border, 1f)) g.DrawPath(pen, path);

                Color fg = dangerHover ? Theme.Danger : Theme.TextPrimary;
                DrawContent(g, fg);
            }
        }

        private void DrawContent(Graphics g, Color fg)
        {
            var font = Theme.Input;   // 14px，共享缓存字体，严禁 dispose
            var fmt = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap,
                Trimming = StringTrimming.EllipsisCharacter
            };
            SizeF ts = g.MeasureString(Text, font, Width, fmt);
            int ringW = _ring.Visible ? Theme.S(16) + Theme.S(8) : 0;   // gap 8
            float totalW = ts.Width + ringW;
            float textX = (Width - totalW) / 2;

            if (_ring.Visible)
            {
                int rsz = Theme.S(16);
                _ring.SetBounds((int)Math.Round(textX), (Height - rsz) / 2, rsz, rsz);
            }
            using (var b = new SolidBrush(fg))
            {
                var tr = new RectangleF(textX + ringW, 0, ts.Width + 4, Height);
                g.DrawString(Text, font, b, tr, fmt);
            }
            fmt.Dispose();
        }
    }

    // =====================================================================
    // 复选框：原型 .checkbox —— 行高 40px、box 20x20 圆角 4、边框 1px #8A8A8A（hover #616161）、
    // 选中 #005FB8 + 14px 白勾；文字 13px #1B1B1B，box 与文字间距 8px。
    // =====================================================================
    public class AppCheckBox : Control
    {
        public static int RowHeight => Theme.S(40);   // .checkbox{height:40px}

        public event EventHandler? CheckedChanged;

        private bool _checked;
        private bool _hover;

        public bool Checked
        {
            get => _checked;
            set { if (_checked != value) { _checked = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); } }
        }

        public AppCheckBox(string text)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            Text = text;
            Height = RowHeight;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Space) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space) { Checked = !Checked; e.Handled = true; }
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.HiQuality(g);

            int boxSz = Theme.S(20);
            int boxY = (Height - boxSz) / 2;
            var box = new Rectangle(0, boxY, boxSz - 1, boxSz - 1);

            // 填充与描边都用同一圆角路径，避免直角填充在圆角处露出毛刺
            using var path = Theme.RoundedRect(box, Theme.S(4));
            using (var bg = new SolidBrush(_checked ? Theme.Accent : Theme.FieldRest))
                g.FillPath(bg, path);
            using (var pen = new Pen(_checked ? Theme.Accent : (_hover ? Theme.StrokeHover : Theme.Stroke), 1f))
                g.DrawPath(pen, path);

            if (_checked)
            {
                int isz = Theme.S(14);
                var icon = new Rectangle(box.X + (box.Width - isz) / 2, box.Y + (box.Height - isz) / 2, isz, isz);
                Theme.DrawCheck(g, icon, Color.White);
            }

            using var fmt = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            using var brush = new SolidBrush(Theme.TextPrimary);
            int textX = boxSz + Theme.S(8);   // gap 8
            g.DrawString(Text, Theme.Body, brush, new RectangleF(textX, 0, Math.Max(0, Width - textX), Height), fmt);
        }
    }

    // =====================================================================
    // 链接：原型 .link-center —— 13px #005FB8 居中，hover 下划线，行高 18px。
    // =====================================================================
    public class LinkLabelEx : Control
    {
        private bool _hover;

        public LinkLabelEx(string text)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            Text = text;
            Height = Theme.S(18);
            Cursor = Cursors.Hand;
            Font = Theme.Body;
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.HiQuality(g);
            using var fmt = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap
            };
            using var brush = new SolidBrush(Theme.TextLink);
            var rect = new RectangleF(0, 0, Width, Height);
            g.DrawString(Text, Theme.Body, brush, rect, fmt);
            if (_hover)
            {
                SizeF ts = g.MeasureString(Text, Theme.Body, Width, fmt);
                float x = (Width - ts.Width) / 2;
                using var pen = new Pen(Theme.TextLink, 1f);
                float uy = Height - Theme.S(3);
                g.DrawLine(pen, x, uy, x + ts.Width, uy);
            }
        }
    }

    // =====================================================================
    // 信息横幅：原型 .banner —— min-height 60px、圆角 4px、padding 10 12、icon 18px、文字 13px #1B1B1B。
    // danger：背景 #FDE7E9 / icon #C42B1C；success：背景 #EAF6EA / icon #0F7B0F。
    // 控件始终可见，IsShown 控制是否绘制内容（未显示时完全透明），规避 Visible 切换时序问题。
    // =====================================================================
    public class InfoBanner : Control
    {
        public enum BannerKind { Danger, Success }

        public BannerKind Kind { get; set; } = BannerKind.Danger;

        /// <summary>横幅是否处于显示状态。</summary>
        public bool IsShown { get; private set; }

        private string _message = "";

        public InfoBanner()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        public void Show(string message, BannerKind kind = BannerKind.Danger)
        {
            _message = message;
            Kind = kind;
            IsShown = true;
            UpdateHeight();
            Invalidate();
            Refresh();
        }

        public new void Hide()
        {
            IsShown = false;
            Invalidate();
        }

        /// <summary>按当前宽度测量并设置高度（min 60px），返回高度。</summary>
        public int UpdateHeight()
        {
            int textW = Math.Max(10, Width - Theme.S(52));   // 左12 + icon18 + gap10 + 右12
            int lines;
            using (var g = CreateGraphics())
            {
                var size = g.MeasureString(_message, Theme.Body, textW);
                lines = Math.Max(1, (int)Math.Ceiling(size.Height / (18f * Theme.Scale)));
            }
            int textH = lines * Theme.S(18);
            Height = Math.Max(Theme.S(60), textH + Theme.S(20));   // padding 10 上下
            return Height;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateHeight();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!IsShown) return;   // 未显示：完全透明

            var g = e.Graphics;
            Theme.HiQuality(g);
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = Theme.RoundedRect(rect, Theme.S(4));
            using (var bg = new SolidBrush(Kind == BannerKind.Success ? Theme.SuccessBg : Theme.DangerBg))
                g.FillPath(bg, path);

            int isz = Theme.S(18);
            var icon = new Rectangle(Theme.S(12), (Height - isz) / 2, isz, isz);
            if (Kind == BannerKind.Success) Theme.DrawSuccessCircle(g, icon, Theme.Success);
            else Theme.DrawInfoCircle(g, icon, Theme.Danger);

            var textRect = new RectangleF(Theme.S(40), 0, 2000, Height);
            using var fmt = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            using var brush = new SolidBrush(Theme.TextPrimary);
            g.DrawString(_message, Theme.Body, brush, textRect, fmt);
        }
    }

    // =====================================================================
    // 展开器：原型 .expander —— 1px #E0E0E0 圆角 4px 边框；摘要行高 32px（hover #F5F5F5）；
    // label 13px #606060 左缘 12px；value mono 12px #878787（chevron 左 8px）；chevron 14px；
    // 展开时 body 顶部 1px 分隔线 + 12px padding，chevron 150ms 旋转 180°。
    // =====================================================================
    public class ExpanderPanel : Control
    {
        public static int SummaryHeight => Theme.S(32);

        public bool IsOpen { get; private set; }
        public event EventHandler? Toggled;

        public Func<string> SummaryValueFunc { get; set; } = () => "";

        private readonly System.Windows.Forms.Timer _anim = new() { Interval = 15 };
        private float _chevAngle;
        private bool _hoverSummary;
        private int _targetAngle;
        private int _bodyContentHeight = Theme.S(132);   // 登录窗：域名(60)+间距12+端口(60)

        /// <summary>body 区域内容高度（不含上下 padding 12），供高度计算。</summary>
        public int BodyContentHeight
        {
            get => _bodyContentHeight;
            set { _bodyContentHeight = value; Invalidate(); Toggled?.Invoke(this, EventArgs.Empty); }
        }

        public ExpanderPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = SummaryHeight + 2;
            Cursor = Cursors.Hand;
            _anim.Tick += (s, e) =>
            {
                int dir = _targetAngle > _chevAngle ? 1 : -1;
                _chevAngle += dir * 24f;   // 150ms → 180°：每 15ms 转 24°
                if ((_chevAngle >= _targetAngle && dir == 1) || (_chevAngle <= _targetAngle && dir == -1))
                {
                    _chevAngle = _targetAngle;
                    _anim.Stop();
                }
                Invalidate();
            };
        }

        public int GetPreferredHeight() => IsOpen
            ? 1 + SummaryHeight + 1 + Theme.S(12) + _bodyContentHeight + Theme.S(12) + 1   // 边框+摘要+分隔线+padding+内容+padding+边框
            : SummaryHeight + 2;

        /// <summary>body 子控件（12px padding 内）的起始 Y。</summary>
        public static int BodyTop => Theme.S(46);   // 1(border) + 32(summary) + 1(sep) + 12(padding)
        public int BodyLeft => Theme.S(13);         // 1(border) + 12(padding)
        public int BodyWidth => Width - Theme.S(26);

        public void Toggle()
        {
            IsOpen = !IsOpen;
            _targetAngle = IsOpen ? 180 : 0;
            _anim.Start();
            Invalidate();
            Toggled?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool h = e.Y <= SummaryHeight;
            if (h != _hoverSummary) { _hoverSummary = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverSummary) { _hoverSummary = false; Invalidate(); }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (PointToClient(Cursor.Position).Y <= SummaryHeight) Toggle();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.HiQuality(g);
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = Theme.RoundedRect(rect, Theme.S(4));

            using (var bg = new SolidBrush(Theme.FieldRest))
                g.FillPath(bg, path);
            using (var pen = new Pen(Theme.Divider, 1f))
                g.DrawPath(pen, path);

            if (IsOpen && Height > SummaryHeight + 2)
            {
                using (var sep = new Pen(Theme.Divider, 1f))
                    g.DrawLine(sep, 1, SummaryHeight + 1.5f, Width - 1, SummaryHeight + 1.5f);
            }

            // 摘要行 hover
            if (_hoverSummary)
            {
                var clip = g.Clip;
                g.SetClip(new Rectangle(1, 1, Width - 2, SummaryHeight));
                using (var hb = new SolidBrush(Theme.FieldHover))
                    g.FillRectangle(hb, 1, 1, Width - 2, SummaryHeight);
                g.Clip = clip;
            }

            // label
            using var fmt = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            using (var lb = new SolidBrush(Theme.TextSecondary))
                g.DrawString(Text, Theme.Body, lb, new RectangleF(Theme.S(12), 0, Width - Theme.S(12) - Theme.S(90), SummaryHeight), fmt);

            // value（mono 12px #878787）
            string value = SummaryValueFunc();
            if (!string.IsNullOrEmpty(value))
            {
                SizeF vs = g.MeasureString(value, Theme.MonoSmall);
                using (var vb = new SolidBrush(Theme.TextTertiary))
                    g.DrawString(value, Theme.MonoSmall, vb,
                        new RectangleF(Width - Theme.S(12) - Theme.S(14) - Theme.S(8) - vs.Width, 0, vs.Width + 4, SummaryHeight), fmt);
            }

            // chevron
            int csz = Theme.S(14);
            var chev = new Rectangle(Width - Theme.S(12) - csz, (SummaryHeight - csz) / 2, csz, csz);
            DrawChevronRotated(g, chev, Theme.TextSecondary, _chevAngle);
        }

        private static void DrawChevronRotated(Graphics g, Rectangle r, Color color, float angle)
        {
            var state = g.Save();
            try
            {
                float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
                g.TranslateTransform(cx, cy);
                g.RotateTransform(angle);
                g.TranslateTransform(-cx, -cy);
                Theme.DrawChevron(g, r, color, false);
            }
            finally { g.Restore(state); }
        }
    }

    // =====================================================================
    // 密码规则清单：原型 .checklist —— 两列网格（列宽 1fr 1fr，gap 4px 12px）；
    // 每项：14px 圆点（白底 1px #8A8A8A 圆框）+ 6px 间距 + 12px 文字 #878787；
    // 满足时圆点 #0F7B0F 填充 + 白勾、文字变 #0F7B0F。总高 2 行 × 16 + 4 = 36px。
    // =====================================================================
    public class PasswordChecklist : Control
    {
        public static int ChecklistHeight => Theme.S(36);   // 2行×16 + 4
        public static int TopMargin => Theme.S(8);          // .checklist{margin:8px 0 0}

        private static readonly string[] Labels = { "至少 8 位", "包含大写字母", "包含小写字母", "包含数字" };
        private readonly bool[] _ok = new bool[4];

        /// <summary>四条规则是否全部满足。</summary>
        public bool AllOk => _ok[0] && _ok[1] && _ok[2] && _ok[3];

        public PasswordChecklist()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            Height = ChecklistHeight;
            BackColor = Color.Transparent;
        }

        /// <summary>按密码实时刷新四条规则，返回是否全部满足。</summary>
        public bool Update(string? password)
        {
            string p = password ?? "";
            _ok[0] = p.Length >= 8;
            _ok[1] = p.Any(char.IsUpper);
            _ok[2] = p.Any(char.IsLower);
            _ok[3] = p.Any(char.IsDigit);
            Invalidate();
            return AllOk;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.HiQuality(g);

            int gap = Theme.S(12);
            int rowH = Theme.S(16);
            int rowStep = Theme.S(20);   // 行高 16 + 行距 4
            int colW = (Width - gap) / 2;
            int dotSz = Theme.S(13);
            for (int i = 0; i < 4; i++)
            {
                int x = (i % 2) * (colW + gap);
                int y = (i / 2) * rowStep;

                // dot 14px（含 1px 边框）
                var dot = new Rectangle(x, y + Theme.S(1), dotSz, dotSz);
                if (_ok[i])
                {
                    using var bg = new SolidBrush(Theme.Success);
                    g.FillEllipse(bg, dot);
                }
                using var pen = new Pen(_ok[i] ? Theme.Success : Theme.Stroke, 1f);
                g.DrawEllipse(pen, dot);
                if (_ok[i])
                {
                    // 白勾（.dot::after：6x3 折线）
                    using var ck = new Pen(Color.White, 1.5f)
                    {
                        StartCap = LineCap.Round,
                        EndCap = LineCap.Round
                    };
                    float cx = dot.X + dot.Width / 2f, cy = dot.Y + dot.Height / 2f;
                    float hw = dotSz * 3f / 13f;
                    g.DrawLine(ck, cx - hw, cy, cx - hw * 0.17f, cy + hw * 0.83f);
                    g.DrawLine(ck, cx - hw * 0.17f, cy + hw * 0.83f, cx + hw * 1.17f, cy - hw * 0.83f);
                }

                using var fmt = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
                using var tb = new SolidBrush(_ok[i] ? Theme.Success : Theme.TextTertiary);
                g.DrawString(Labels[i], Theme.Small, tb, new RectangleF(x + dotSz + Theme.S(6), y, colW - dotSz - Theme.S(6), rowH), fmt);
            }
        }
    }

    // =====================================================================
    // 说明注释：原型 .note —— 18px accent 色 info 圆图标 + 13px 文字，
    // 文字支持 <b> 粗体前段（boldText 深色 + regularText 次色）。
    // =====================================================================
    public class InfoNote : Control
    {
        private string _boldText = "";
        private string _regularText = "";

        public InfoNote(string boldText, string regularText)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            _boldText = boldText;
            _regularText = regularText;
            Height = Theme.S(22);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.HiQuality(g);

            int isz = Theme.S(18);
            var icon = new Rectangle(0, (Height - isz) / 2, isz, isz);
            Theme.DrawInfoCircle(g, icon, Theme.Accent);

            float x = isz + Theme.S(10);   // gap 10
            using var fmt = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            using (var bb = new SolidBrush(Theme.TextPrimary))
            {
                // layout 宽度给足（2000），避免 GDI 在测量/绘制 rect 内裁剪
                var rect = new RectangleF(x, 0, 2000, Height);
                g.DrawString(_boldText, Theme.Body, bb, rect, fmt);
                float w = g.MeasureString(_boldText, Theme.Body).Width;
                using var rb = new SolidBrush(Theme.TextSecondary);
                g.DrawString(_regularText, Theme.Body, rb, new RectangleF(x + w - 6, 0, 2000, Height), fmt);
            }
        }
    }

    // =====================================================================
    // 账号行：原型 .acct-line —— 13px #606060 前缀 + mono 13px #1B1B1B 账号名（单行自绘）。
    // monoValue 为 null 时仅绘制前缀（如"账号：xiaosn"整段次色）。
    // =====================================================================
    public class AcctLine : Control
    {
        private readonly string _prefix;
        private readonly string? _monoValue;

        public AcctLine(string prefix, string? monoValue)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            _prefix = prefix;
            _monoValue = monoValue;
            Height = Theme.S(22);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.HiQuality(g);

            using var fmt = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            using (var b = new SolidBrush(Theme.TextSecondary))
                g.DrawString(_prefix, Theme.Body, b, new RectangleF(0, 0, 2000, Height), fmt);

            if (!string.IsNullOrEmpty(_monoValue))
            {
                float w = g.MeasureString(_prefix, Theme.Body).Width;
                using var mb = new SolidBrush(Theme.TextPrimary);
                g.DrawString(_monoValue, Theme.MonoBody, mb, new RectangleF(w - 6, 0, 2000, Height), fmt);
            }
        }
    }

    // =====================================================================
    // 信息卡片：原型 .card / .info-row —— 白底 1px #E0E0E0 圆角 4px；
    // 每行 min-height 44px、左右 padding 12px、key 宽 60px 13px #606060、
    // value 14px #1B1B1B（mono 13px）；行间 1px 分隔线（末行无）。
    // 密码行 masked：8 个点、字号 16px、字距 3px，右侧 32px "小眼睛"点击切换明文。
    // =====================================================================
    public class InfoCard : Control
    {
        public class Row
        {
            public string Key = "";
            public string Value = "";
            public bool Mono;
            public bool Masked;
            public bool Empty;
            public bool HasEye;
            public bool HasCopy;
            public bool Revealed;
            public bool Copied;          // 复制成功反馈（图标短暂变勾）
            public string? RevealValue;
        }

        private readonly List<Row> _rows = new();
        private int _hoverEye = -1;
        private int _hoverCopy = -1;
        private ToolTip? _tip;

        /// <summary>key 列宽度（默认 60px；查询元数据卡用 132px）。</summary>
        public int KeyWidth { get; set; } = Theme.S(60);

        public InfoCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Canvas;
        }

        public void Clear() { _rows.Clear(); }

        public Row AddRow(string key, string value, bool mono = false, bool masked = false,
                          bool empty = false, bool hasEye = false, bool hasCopy = false, string? reveal = null)
        {
            var row = new Row { Key = key, Value = value, Mono = mono, Masked = masked, Empty = empty, HasEye = hasEye, HasCopy = hasCopy, RevealValue = reveal };
            _rows.Add(row);
            Height = Theme.S(2) + _rows.Count * Theme.S(44);
            return row;
        }

        public int GetPreferredHeight() => Theme.S(2) + Math.Max(1, _rows.Count) * Theme.S(44);

        private Rectangle RowRect(int i) => new(1, 1 + i * Theme.S(44), Width - 2, Theme.S(44));

        private Rectangle EyeRect(int i)
        {
            var rr = RowRect(i);
            int sz = Theme.S(32);
            return new Rectangle(rr.Right - Theme.S(12) - sz, rr.Y + (rr.Height - sz) / 2, sz, sz);
        }

        private Rectangle CopyRect(int i)
        {
            var rr = RowRect(i);
            int sz = Theme.S(28);
            return new Rectangle(rr.Right - Theme.S(12) - sz, rr.Y + (rr.Height - sz) / 2, sz, sz);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = -1, c = -1;
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].HasEye && EyeRect(i).Contains(e.Location)) { h = i; break; }
                if (_rows[i].HasCopy && CopyRect(i).Contains(e.Location)) { c = i; break; }
            }
            Cursor = h >= 0 || c >= 0 ? Cursors.Hand : Cursors.Default;
            if (h != _hoverEye) { _hoverEye = h; Invalidate(); }
            if (c != _hoverCopy) { _hoverCopy = c; Invalidate(); }

            // 悬停显示完整值（原型：值过长时单行省略，悬停可见完整内容）
            _tip ??= new ToolTip();
            string? tipText = null;
            for (int i = 0; i < _rows.Count; i++)
                if (RowRect(i).Contains(e.Location)) { tipText = _rows[i].Value; break; }
            _tip.SetToolTip(this, tipText ?? "");
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverEye >= 0 || _hoverCopy >= 0) { _hoverEye = -1; _hoverCopy = -1; Cursor = Cursors.Default; Invalidate(); }
            _tip?.SetToolTip(this, "");
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row.HasEye && EyeRect(i).Contains(e.Location))
                {
                    if (row.Revealed || !string.IsNullOrEmpty(row.RevealValue))
                    {
                        row.Revealed = !row.Revealed;
                        Invalidate();
                    }
                    return;
                }
                if (row.HasCopy && CopyRect(i).Contains(e.Location))
                {
                    try { Clipboard.SetText(row.Value ?? ""); } catch { }
                    row.Copied = true;
                    Invalidate();
                    var timer = new System.Windows.Forms.Timer { Interval = 1200 };
                    timer.Tick += (_, _) => { timer.Stop(); timer.Dispose(); row.Copied = false; Invalidate(); };
                    timer.Start();
                    return;
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.HiQuality(g);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = Theme.RoundedRect(rect, Theme.S(4));
            using (var bg = new SolidBrush(Theme.FieldRest))
                g.FillPath(bg, path);
            using (var pen = new Pen(Theme.Divider, 1f))
                g.DrawPath(pen, path);

            int keyW = KeyWidth;
            int padX = Theme.S(12);
            int gap = Theme.S(12);
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                var rr = RowRect(i);

                // 行分隔线（末行无）
                if (i < _rows.Count - 1)
                {
                    using var sp = new Pen(Theme.Divider, 1f);
                    g.DrawLine(sp, 1, rr.Bottom, Width - 1, rr.Bottom);
                }

                using var fmt = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter };
                using (var kb = new SolidBrush(Theme.TextSecondary))
                    g.DrawString(row.Key, Theme.Body, kb, new RectangleF(padX, rr.Y, keyW, rr.Height), fmt);

                float vX = padX + keyW + gap;
                float vW = rr.Width - (vX - rr.X) - (row.HasEye ? Theme.S(12) + Theme.S(32) : 0) - (row.HasCopy ? Theme.S(12) + Theme.S(28) : Theme.S(12));
                if (row.Masked && !(row.Revealed && !string.IsNullOrEmpty(row.RevealValue)))
                {
                    // 8 个点、字号 16px、字距 3px（原型 .v.masked）；置灰呈只读感
                    using var f = Theme.Px(16);
                    using var b = new SolidBrush(Theme.TextTertiary);
                    float dx = vX;
                    float dw = g.MeasureString("•", f).Width - 6;
                    for (int k = 0; k < 8; k++)
                    {
                        g.DrawString("•", f, b, new RectangleF(dx, rr.Y, dw + 6, rr.Height), fmt);
                        dx += dw + Theme.S(3);
                    }
                }
                else if (row.Masked && row.Revealed && row.RevealValue != null)
                {
                    using var b = new SolidBrush(Theme.TextTertiary);
                    g.DrawString(row.RevealValue, Theme.MonoBody, b, new RectangleF(vX, rr.Y, vW, rr.Height), fmt);
                }
                else
                {
                    using var b = new SolidBrush(Theme.TextTertiary);
                    g.DrawString(string.IsNullOrEmpty(row.Value) ? "—" : row.Value,
                        row.Mono ? Theme.MonoBody : Theme.Input, b, new RectangleF(vX, rr.Y, vW, rr.Height), fmt);
                }

                // 复制按钮（原型 .icon-btn：28x28，hover #F5F5F5；复制成功短暂变勾）
                if (row.HasCopy)
                {
                    var cr = CopyRect(i);
                    if (i == _hoverCopy || row.Copied)
                    {
                        using var hb = new SolidBrush(Theme.FieldHover);
                        g.FillRectangle(hb, cr);
                    }
                    int isz = Theme.S(15);
                    var icon = new Rectangle(cr.X + (cr.Width - isz) / 2, cr.Y + (cr.Height - isz) / 2, isz, isz);
                    if (row.Copied)
                        Theme.DrawCheck(g, icon, Theme.Success, 2.6f);
                    else
                        Theme.DrawCopyIcon(g, icon, i == _hoverCopy ? Theme.TextPrimary : Theme.TextSecondary);
                }

                // 小眼睛（.eye-static：32x32、hover #F5F5F5、icon 18px）
                if (row.HasEye)
                {
                    var er = EyeRect(i);
                    if (i == _hoverEye)
                    {
                        using var hb = new SolidBrush(Theme.FieldHover);
                        g.FillRectangle(hb, er);
                    }
                    int isz = Theme.S(18);
                    var icon = new Rectangle(er.X + (er.Width - isz) / 2, er.Y + (er.Height - isz) / 2, isz, isz);
                    Theme.DrawEye(g, icon, i == _hoverEye ? Theme.TextPrimary : Theme.TextSecondary, row.Revealed);
                }
            }
        }
    }

    // =====================================================================
    // 注销确认卡：原型 .confirm —— 白底 1px #E0E0E0 圆角 4px、padding 12；
    // 头部：危险三角 18px + 标题 13px #1B1B1B + 副题 12px #606060；
    // 按钮行：[取消 secondary][确认注销 红色实心] 均分、gap 8、高 40。
    // =====================================================================
    public class SignOutCard : Control
    {
        public event EventHandler? CancelClicked;
        public event EventHandler? ConfirmClicked;

        private readonly ThemedButton _cancel = new("取消", ThemedButtonStyle.Secondary);
        private readonly ThemedButton _confirm = new("确认注销", ThemedButtonStyle.DangerPrimary);

        public SignOutCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Canvas;
            Controls.Add(_cancel);
            Controls.Add(_confirm);
            _cancel.Click += (_, _) => CancelClicked?.Invoke(this, EventArgs.Empty);
            _confirm.Click += (_, _) => ConfirmClicked?.Invoke(this, EventArgs.Empty);
            Height = GetPreferredHeight();
        }

        private int HeadHeight => Theme.S(36);

        public int GetPreferredHeight() => Theme.S(12) + HeadHeight + Theme.S(12) + ThemedButton.ButtonHeight + Theme.S(12);

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int btnW = (Width - Theme.S(24) - Theme.S(8)) / 2;
            int by = Height - Theme.S(12) - ThemedButton.ButtonHeight;
            _cancel.SetBounds(Theme.S(12), by, btnW, ThemedButton.ButtonHeight);
            _confirm.SetBounds(Theme.S(12) + btnW + Theme.S(8), by, btnW, ThemedButton.ButtonHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.HiQuality(g);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = Theme.RoundedRect(rect, Theme.S(4));
            using (var bg = new SolidBrush(Theme.FieldRest))
                g.FillPath(bg, path);
            using (var pen = new Pen(Theme.Divider, 1f))
                g.DrawPath(pen, path);

            int isz = Theme.S(18);
            var icon = new Rectangle(Theme.S(12), Theme.S(12), isz, isz);
            Theme.DrawWarningTriangle(g, icon, Theme.Danger);

            float tx = Theme.S(12) + isz + Theme.S(10);
            using var fmt = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            using (var tb = new SolidBrush(Theme.TextPrimary))
                g.DrawString("确定要注销当前账号吗？", Theme.Body, tb, new RectangleF(tx, Theme.S(12), 2000, Theme.S(18)), fmt);
            using (var sb = new SolidBrush(Theme.TextSecondary))
                g.DrawString("注销后需重新登录才能继续使用。", Theme.Small, sb,
                    new RectangleF(tx, Theme.S(12) + Theme.S(20), 2000, Theme.S(16)), fmt);
        }
    }

    // =====================================================================
    // 托盘菜单项：原型 .mi —— 高 32px、圆角 4px、padding 0 10px、图标 16px #606060、
    // 图标-文字 gap 10px、字 14px #1B1B1B；hover 背景 #F5F5F5；
    // 危险项（退出）hover：背景 #FDE7E9、文字/图标 #C42B1C；
    // ShowCheck 时右缘画 accent 勾（"显示日志"）。
    // =====================================================================
    public class PrototypeMenuItem : ToolStripMenuItem
    {
        public string IconKind = "";
        public bool Danger;
        public bool ShowCheck;

        public PrototypeMenuItem(string text, string iconKind, bool danger = false, bool showCheck = false)
        {
            Text = text;
            IconKind = iconKind;
            Danger = danger;
            ShowCheck = showCheck;
            AutoSize = false;
            Size = new Size(Theme.S(232), Theme.S(32));
            Padding = Padding.Empty;
            Margin = Padding.Empty;
            ForeColor = Theme.TextPrimary;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.HiQuality(g);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            bool hot = Selected || Pressed;
            if (hot)
            {
                using var path = Theme.RoundedRect(rect, Theme.S(4));
                using var bg = new SolidBrush(Danger ? Theme.DangerBg : Theme.FieldHover);
                g.FillPath(bg, path);
            }

            bool dangerHot = Danger && hot;
            Color iconColor = dangerHot ? Theme.Danger : Theme.TextSecondary;
            Color textColor = dangerHot ? Theme.Danger : Theme.TextPrimary;

            bool hasIcon = !string.IsNullOrEmpty(IconKind);
            int isz = Theme.S(16);
            int iconX = Theme.S(10);
            if (hasIcon)
            {
                var iconRect = new Rectangle(iconX, (Height - isz) / 2, isz, isz);
                DrawIcon(g, iconRect, iconColor);
            }

            using var fmt = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            using var tb = new SolidBrush(textColor);
            float tx = hasIcon ? Theme.S(10) + isz + Theme.S(10) : Theme.S(10);
            float tw = Width - tx - (ShowCheck ? Theme.S(10) + Theme.S(16) : Theme.S(10));
            g.DrawString(Text, Theme.Input, tb, new RectangleF(tx, 0, Math.Max(10, tw), Height), fmt);

            if (ShowCheck)
            {
                int csz = Theme.S(16);
                var ck = new Rectangle(Width - Theme.S(10) - csz, (Height - csz) / 2, csz, csz);
                Theme.DrawCheck(g, ck, Theme.Accent, 2.6f);
            }
        }

        private void DrawIcon(Graphics g, Rectangle r, Color color)
        {
            switch (IconKind)
            {
                case "house": Theme.DrawHouse(g, r, color); break;
                case "log": Theme.DrawDocument(g, r, color); break;
                case "meta": Theme.DrawSearchIcon(g, r, color); break;
                case "folder": Theme.DrawFolderIcon(g, r, color); break;
                case "power": Theme.DrawPowerIcon(g, r, color); break;
            }
        }
    }

    // =====================================================================
    // 托盘菜单渲染器：原型 .flyout —— 白底、1px #E0E0E0 圆角 8px 边框、padding 4px；
    // 分隔线 .sep：#E0E0E0、左右留白 4px。
    // =====================================================================
    public class PrototypeMenuRenderer : ToolStripRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var bg = new SolidBrush(Theme.FieldRest);
            e.Graphics.FillRectangle(bg, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            var g = e.Graphics;
            Theme.HiQuality(g);
            var r = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            using var path = Theme.RoundedRect(r, Theme.S(8));
            using var pen = new Pen(Theme.Divider, 1f);
            g.DrawPath(pen, path);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) { }
        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e) { }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            // PrototypeMenuItem 自绘背景；此处仅处理其他类型（无）
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            base.OnRenderItemText(e);   // PrototypeMenuItem 重写 OnPaint 全自绘，不会走到这里
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var g = e.Graphics;
            Theme.HiQuality(g);
            int y = e.Item.Height / 2;
            using var pen = new Pen(Theme.Divider, 1f);
            g.DrawLine(pen, Theme.S(4), y, e.Item.Width - Theme.S(4), y);
        }
    }

    public static class MenuDwm
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        /// <summary>菜单窗口应用 Win11 8px 圆角（对应原型 .flyout border-radius:8px）。</summary>
        public static void ApplyRound(IntPtr hwnd)
        {
            try
            {
                int pref = DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
            }
            catch { }
        }
    }

    // =====================================================================
    // 搜索框：原型 .searchbox —— 高 32px、1px #8A8A8A 圆角 4px、白底、
    // 左侧 14px 放大镜 #878787、文字 13px；focus 边框 #005FB8 + 底部 2px 下划线。
    // =====================================================================
    public class SearchBox : Panel
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
        private const int EM_SETCUEBANNER = 0x1501;

        public TextBox Inner { get; }
        private bool _hover;

        public event EventHandler? TextChanged;

        public SearchBox(string placeholder)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = Theme.S(32);

            Inner = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = Theme.Body,
                ForeColor = Theme.TextPrimary,
                BackColor = Theme.FieldRest
            };
            Controls.Add(Inner);

            if (placeholder != null)
            {
                if (Inner.IsHandleCreated) SendMessage(Inner.Handle, EM_SETCUEBANNER, (IntPtr)1, placeholder);
                else Inner.HandleCreated += (s, e) => SendMessage(Inner.Handle, EM_SETCUEBANNER, (IntPtr)1, placeholder);
            }

            Inner.TextChanged += (_, _) => TextChanged?.Invoke(this, EventArgs.Empty);
            Inner.MouseEnter += (_, _) => UpdateHover();
            Inner.MouseLeave += (_, _) => UpdateHover();
            MouseEnter += (_, _) => UpdateHover();
            MouseLeave += (_, _) => UpdateHover();
            Resize += (_, _) => Relayout();
            HandleCreated += (_, _) => Relayout();
        }

        private void UpdateHover()
        {
            Invalidate();
        }

        private void Relayout()
        {
            Height = Theme.S(32);
            int ih = Inner.PreferredHeight;
            int ix = Theme.S(10) + Theme.S(14) + Theme.S(6);
            Inner.SetBounds(ix, (Height - ih) / 2, Math.Max(10, Width - ix - Theme.S(10)), ih);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.HiQuality(g);
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            bool focused = Inner.Focused;
            Color border = focused ? Theme.Accent : Theme.Stroke;
            using (var bg = new SolidBrush(Theme.FieldRest))
                g.FillRectangle(bg, rect);
            using var path = Theme.RoundedRect(rect, Theme.S(4));
            using (var pen = new Pen(border, 1f))
                g.DrawPath(pen, path);
            if (focused)
            {
                using var upen = new Pen(Theme.Accent, Theme.S(2));
                g.DrawLine(upen, Theme.S(2), Height - Theme.S(2) + 0.5f, Width - Theme.S(2), Height - Theme.S(2) + 0.5f);
            }

            int isz = Theme.S(14);
            var icon = new Rectangle(Theme.S(10), (Height - isz) / 2, isz, isz);
            Theme.DrawSearchIcon(g, icon, Theme.TextTertiary);

            Inner.ForeColor = Theme.TextPrimary;
            Inner.BackColor = Theme.FieldRest;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            Inner.Focus();
        }
    }

    // =====================================================================
    // 分段筛选：原型 .seg —— 1px #E0E0E0 圆角 4px 白底；段按钮 12px 字、
    // 高 30px、padding 0 12、段间 1px 分隔线；选中段 accent 底白字。
    // =====================================================================
    public class SegmentedControl : Control
    {
        private readonly string[] _items;
        private int _selected;

        public event EventHandler? SelectedChanged;

        public int Selected => _selected;

        public SegmentedControl(string[] items, int selected = 0)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            _items = items;
            _selected = selected;
            Height = Theme.S(30);
            BackColor = Theme.Canvas;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int idx = e.X * _items.Length / Math.Max(1, Width);
            if (idx >= 0 && idx < _items.Length && idx != _selected)
            {
                _selected = idx;
                Invalidate();
                SelectedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.HiQuality(g);
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var bg = new SolidBrush(Theme.FieldRest))
                g.FillRectangle(bg, rect);
            using (var pen = new Pen(Theme.Divider, 1f))
                g.DrawRectangle(pen, rect);

            int segW = Width / _items.Length;
            using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            for (int i = 0; i < _items.Length; i++)
            {
                var segRect = new Rectangle(i * segW, 0, i == _items.Length - 1 ? Width - segW * (_items.Length - 1) : segW, Height);
                bool active = i == _selected;
                if (active)
                {
                    using var ab = new SolidBrush(Theme.Accent);
                    g.FillRectangle(ab, segRect);
                }
                else if (i > 0)
                {
                    using var sp = new Pen(Theme.Divider, 1f);
                    g.DrawLine(sp, segRect.Left, 0, segRect.Left, Height);
                }
                using var b = new SolidBrush(active ? Color.White : Theme.TextSecondary);
                g.DrawString(_items[i], Theme.Small, b, segRect, fmt);
            }
        }
    }

    // =====================================================================
    // 开关：原型 .switch —— track 36x20 圆角 10 灰 #C7C7C7，knob 14 白（左 3 / 右 3）；
    // 选中 track 变 accent。文字由外部 Label 配合。
    // =====================================================================
    public class ToggleSwitch : Control
    {
        private bool _checked = true;

        public event EventHandler? CheckedChanged;

        public bool Checked
        {
            get => _checked;
            set { if (_checked != value) { _checked = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); } }
        }

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(Theme.S(36), Theme.S(20));
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.HiQuality(g);
            int w = Theme.S(36), h = Theme.S(20), r = h / 2;
            using (var path = new GraphicsPath())
            {
                path.AddArc(0, 0, h, h, 90, 180);
                path.AddArc(w - h, 0, h, h, -90, 180);
                path.CloseFigure();
                using var bg = new SolidBrush(_checked ? Theme.Accent : Color.FromArgb(0xC7, 0xC7, 0xC7));
                g.FillPath(bg, path);
            }
            int k = Theme.S(14);
            int kx = _checked ? w - Theme.S(3) - k : Theme.S(3);
            using var kb = new SolidBrush(Color.White);
            g.FillEllipse(kb, kx, (h - k) / 2, k, k);
        }
    }

    // =====================================================================
    // 小按钮：原型 .btn-mini —— 12px 字、高 32px、padding 0 12px、
    // 1px #8A8A8A 圆角 4px 白底；hover #F5F5F5 + 边框 #616161。
    // =====================================================================
    public class MiniButton : Control
    {
        private bool _hover;

        public MiniButton(string text)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            Text = text;
            Height = Theme.S(32);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = Theme.Small;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.HiQuality(g);
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = Theme.RoundedRect(rect, Theme.S(4));
            using (var bg = new SolidBrush(_hover ? Theme.FieldHover : Theme.FieldRest))
                g.FillPath(bg, path);
            using (var pen = new Pen(_hover ? Theme.StrokeHover : Theme.Stroke, 1f))
                g.DrawPath(pen, path);

            using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            using var b = new SolidBrush(Theme.TextPrimary);
            g.DrawString(Text, Theme.Small, b, rect, fmt);
        }
    }
}
