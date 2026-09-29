using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using PasswordManager.UI.Controls;

namespace PasswordManager.UI.Controls
{
    // =====================================================================
    // 文档权限屏（严格还原原型 app-prototype.html SCREEN 7）自定义控件：
    // 数据模型 / 面板 / 虚拟树 / 已选清单 / 小按钮 / 导航箭头 / 状态旗 / 富文本提示行。
    // 布局与颜色全部取自原型 CSS（96DPI 基准，经 Theme.S 缩放）。
    // =====================================================================

    /// <summary>组织节点（由 LdapNodeDTO 映射，含父/深度/子级，供权限树渲染与授权推导）。</summary>
    public class PermNodeModel
    {
        public long Id { get; set; }
        public bool IsDept { get; set; }              // type==0 部门 / type==1 员工
        public string Name { get; set; } = "";
        public string Account { get; set; } = "";
        public string Dn { get; set; } = "";
        public bool HasAuth { get; set; }
        public PermNodeModel? Parent { get; set; }
        public int Depth { get; set; }
        public List<PermNodeModel> Children { get; } = new();

        /// <summary>稳定键：部门与员工 id 可能数值相同，加类型前缀避免冲突。</summary>
        public string Key => (IsDept ? "d:" : "u:") + Id;
        public bool HasChildren => Children.Count > 0;
    }

    public readonly struct PermRow
    {
        public readonly PermNodeModel Node;
        public readonly int Depth;
        public PermRow(PermNodeModel node, int depth) { Node = node; Depth = depth; }
    }

    public readonly struct SelItem
    {
        public readonly PermNodeModel Node;
        public readonly string Path;
        public SelItem(PermNodeModel node, string path) { Node = node; Path = path; }
    }

    /// <summary>原型 SVG 图标（路径与 viewBox 一一对应）。</summary>
    internal static class PermIcons
    {
        /// <summary>twisty 右向 chevron（viewBox 16: M6 3.5 10.5 8 6 12.5, stroke 1.7）。</summary>
        public static void DrawTwisty(Graphics g, Rectangle r, Color color, bool open)
        {
            float s = r.Width / 16f;
            using var pen = new Pen(color, 1.7f * s)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            var pts = new[]
            {
                new PointF(r.X + 6 * s, r.Y + 3.5f * s),
                new PointF(r.X + 10.5f * s, r.Y + 8 * s),
                new PointF(r.X + 6 * s, r.Y + 12.5f * s)
            };
            if (open)
            {
                // 顺时针旋转 90°（原型 .twisty.open svg{transform:rotate(90deg)}）→ 指向下
                float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
                for (int i = 0; i < pts.Length; i++)
                {
                    float dx = pts[i].X - cx, dy = pts[i].Y - cy;
                    pts[i] = new PointF(cx - dy, cy + dx);
                }
            }
            g.DrawLines(pen, pts);
        }

        /// <summary>部门图标（viewBox 24: 双矩形 + 横线，stroke 1.8）。</summary>
        public static void DrawDept(Graphics g, Rectangle r, Color color)
        {
            float s = r.Width / 24f;
            using var pen = new Pen(color, 1.8f * s);
            g.DrawRectangle(pen, r.X + 3.5f * s, r.Y + 3.5f * s, 10 * s, 17 * s);
            g.DrawLine(pen, r.X + 13.5f * s, r.Y + 9.5f * s, r.X + 20.5f * s, r.Y + 9.5f * s);
            g.DrawLine(pen, r.X + 20.5f * s, r.Y + 9.5f * s, r.X + 20.5f * s, r.Y + 20.5f * s);
            g.DrawLine(pen, r.X + 13.5f * s, r.Y + 20.5f * s, r.X + 20.5f * s, r.Y + 20.5f * s);
            g.DrawLine(pen, r.X + 13.5f * s, r.Y + 9.5f * s, r.X + 13.5f * s, r.Y + 20.5f * s);
            g.DrawLine(pen, r.X + 6.5f * s, r.Y + 7 * s, r.X + 8.5f * s, r.Y + 7 * s);
            g.DrawLine(pen, r.X + 6.5f * s, r.Y + 11 * s, r.X + 8.5f * s, r.Y + 11 * s);
            g.DrawLine(pen, r.X + 6.5f * s, r.Y + 15 * s, r.X + 8.5f * s, r.Y + 15 * s);
            g.DrawLine(pen, r.X + 16.5f * s, r.Y + 13 * s, r.X + 17.5f * s, r.Y + 13 * s);
            g.DrawLine(pen, r.X + 16.5f * s, r.Y + 17 * s, r.X + 17.5f * s, r.Y + 17 * s);
        }

        /// <summary>人员图标（viewBox 24: circle 12,8,3.6 + M5 20c0-3.7…, stroke 1.8）。</summary>
        public static void DrawUser(Graphics g, Rectangle r, Color color)
        {
            float s = r.Width / 24f;
            using var pen = new Pen(color, 1.8f * s);
            g.DrawEllipse(pen, r.X + (12 - 3.6f) * s, r.Y + (8 - 3.6f) * s, 7.2f * s, 7.2f * s);
            var path = new GraphicsPath();
            path.AddBezier(
                r.X + 5 * s, r.Y + 20 * s,
                r.X + 5 * s, r.Y + (20 - 3.7f) * s, r.X + (5 + 3.1f) * s, r.Y + (20 - 5.7f) * s, r.X + 12 * s, r.Y + (20 - 5.7f) * s);
            path.AddBezier(
                r.X + 12 * s, r.Y + (20 - 5.7f) * s,
                r.X + (12 + 7) * s, r.Y + (20 - 5.7f) * s, r.X + 19 * s, r.Y + (20 - 3.7f) * s, r.X + 19 * s, r.Y + 20 * s);
            g.DrawPath(pen, path);
        }

        /// <summary>复选框 mixed 横线（viewBox 24: M6 12h12, stroke 3.2）。</summary>
        public static void DrawMixed(Graphics g, Rectangle r, Color color)
        {
            float s = r.Width / 24f;
            using var pen = new Pen(color, 3.2f * s)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawLine(pen, r.X + 6 * s, r.Y + 12 * s, r.X + 18 * s, r.Y + 12 * s);
        }

        /// <summary>移除 ×（viewBox 24: M6 6l12 12 M18 6 6 18, stroke 2.4）。</summary>
        public static void DrawX(Graphics g, Rectangle r, Color color)
        {
            float s = r.Width / 24f;
            using var pen = new Pen(color, 2.4f * s)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawLine(pen, r.X + 6 * s, r.Y + 6 * s, r.X + 18 * s, r.Y + 18 * s);
            g.DrawLine(pen, r.X + 18 * s, r.Y + 6 * s, r.X + 6 * s, r.Y + 18 * s);
        }
    }

    // =====================================================================
    // 面板：原型 .pane —— 白底、1px divider 圆角 6 边框；头部 34px #FAFAFA + 底部分隔线；
    // 标题 12px bold、计数 mono 12px、规则说明 11px tertiary 右对齐。
    // =====================================================================
    public class PermPane : Control
    {
        public string HeadTitle { get; set; } = "";
        public string HeadCount { get; set; } = "";
        public string HeadRule { get; set; } = "";

        private static readonly Color HeadBg = Color.FromArgb(0xFA, 0xFA, 0xFA);

        public PermPane()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.FieldRest;
        }

        /// <summary>内容区（头部之下、边框之内）。</summary>
        public Rectangle BodyRect => new(
            Theme.S(1), Theme.S(34) + Theme.S(1),
            Math.Max(4, Width - Theme.S(2)), Math.Max(4, Height - Theme.S(34) - Theme.S(2)));

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.HiQuality(g);
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = Theme.RoundedRect(rect, Theme.S(6));

            using (var bg = new SolidBrush(Theme.FieldRest))
                g.FillPath(bg, path);

            int headH = Theme.S(34);
            // 头部背景裁进圆角
            g.SetClip(path);
            using (var head = new SolidBrush(HeadBg))
                g.FillRectangle(head, 0, 0, Width, headH);
            g.ResetClip();

            using (var pen = new Pen(Theme.Divider, 1f))
                g.DrawPath(pen, path);
            g.DrawLine(PensDivier, 0, headH, Width, headH);

            // 标题（12px bold）
            var tf = TitleBoldFont;
            int tx = Theme.S(10);
            TextRenderer.DrawText(g, HeadTitle, tf, new Point(tx, (headH - 17) / 2), Theme.TextPrimary);
            tx += TextRenderer.MeasureText(HeadTitle, tf).Width + Theme.S(6);

            // 计数（mono 12px secondary）
            if (!string.IsNullOrEmpty(HeadCount))
            {
                TextRenderer.DrawText(g, HeadCount, Theme.MonoSmall, new Point(tx, (headH - 15) / 2), Theme.TextSecondary);
            }

            // 规则说明（11px tertiary，右对齐，溢出省略）
            if (!string.IsNullOrEmpty(HeadRule))
            {
                var fmt = new TextFormatFlags { };
                var size = TextRenderer.MeasureText(HeadRule, Theme.Tiny);
                int rw = Math.Min(size.Width + 2, Width - tx - Theme.S(10));
                if (rw > 0)
                {
                    var ruleRect = new Rectangle(Width - Theme.S(10) - rw, 0, rw, headH);
                    TextRenderer.DrawText(g, HeadRule, Theme.Tiny, ruleRect, Theme.TextTertiary,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                }
            }
        }

        private static readonly Pen PensDivier = new(Theme.Divider, 1f);
        private static readonly Font TitleBoldFont = Theme.Px(12, FontStyle.Bold);
    }

    // =====================================================================
    // 组织树：原型 .tnode —— 行高 28、缩进 6+16/级、twisty 20、复选框 20（on/mixed 主色）、
    // 图标 16（部门主色/人员次级）、标签 13px（根级 bold）；被上级覆盖整行置灰；
    // 搜索命中词 sel-bg 高亮、当前命中行 info-bg + 主色描边；键盘焦点 2px #1A1A1A 内框。
    // =====================================================================
    public class PermTreeView : Control
    {
        public Func<List<PermRow>>? GetRows { get; set; }
        public Func<PermNodeModel, string>? GetState { get; set; }          // "on" / "mixed" / "off"
        public Func<PermNodeModel, bool>? GetLocked { get; set; }
        public Func<PermNodeModel, bool>? GetExpanded { get; set; }
        public Func<PermNodeModel, (int start, int len)?>? GetHighlight { get; set; }
        public Action<PermNodeModel>? ToggleRequested { get; set; }         // 点击行 / 空格 / 回车
        public Action<PermNodeModel>? ExpandToggled { get; set; }           // twisty / 左右键

        /// <summary>加载中 / 加载失败提示（显示时占据全部内容区）。</summary>
        public bool ShowStatus { get; set; }
        public string StatusText { get; set; } = "";
        public bool StatusIsError { get; set; }

        public string EmptyTitle { get; set; } = "";
        public string EmptySub { get; set; } = "";

        /// <summary>搜索当前命中（.tnode.is-current），由窗体随定位按钮更新。</summary>
        public string? CurrentKey { get; set; }

        private const string LockedTip = "已包含在上级部门的授权范围内，如需单独调整请先取消上级部门授权";
        private static readonly Color EmptyIconGray = Color.FromArgb(0xC7, 0xC7, 0xC7);
        private static readonly Color FocusOuter = Color.FromArgb(0x1A, 0x1A, 0x1A);

        private static readonly Font LabelBoldFont = Theme.Px(13, FontStyle.Bold);

        private readonly VScrollBar _vscroll = new() { Visible = false };
        private readonly ToolTip _tip = new();
        private int _hoverRow = -1;
        private bool _hoverTwisty;
        private int _focusRow = -1;     // 键盘/点击焦点行
        private bool _kbdFocus;         // 仅键盘路径绘制焦点环（原型 permKbdFocus）

        private int RowH => Theme.S(28);
        private int PadTop => Theme.S(4);
        private int TwistySize => Theme.S(20);
        private int CbxSize => Theme.S(20);
        private int IconSize => Theme.S(16);
        private int CbxGap => Theme.S(6);
        private int IconGap => Theme.S(6);
        private int RowPadBottom => Theme.S(4);

        public PermTreeView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            BackColor = Theme.FieldRest;
            TabStop = true;

            Controls.Add(_vscroll);
            _vscroll.ValueChanged += (_, _) => { HideTip(); Invalidate(); };
            Resize += (_, _) => UpdateScroll();
        }

        private int RowsAreaWidth => Width - (_vscroll.Visible ? _vscroll.Width : 0);
        private int PadLeft(int depth) => Theme.S(6 + depth * 16);
        private int LabelX(int depth) => PadLeft(depth) + TwistySize + CbxSize + CbxGap + IconSize + IconGap;

        private Rectangle TwistyRect(int depth, int y) => new(PadLeft(depth), y + (RowH - TwistySize) / 2, TwistySize, TwistySize);
        private Rectangle CbxRect(int depth, int y) => new(PadLeft(depth) + TwistySize, y + (RowH - CbxSize) / 2, CbxSize, CbxSize);
        private Rectangle IconRect(int depth, int y) => new(PadLeft(depth) + TwistySize + CbxSize + CbxGap, y + (RowH - IconSize) / 2, IconSize, IconSize);
        private Rectangle LabelRect(int depth, int y, int areaW)
        {
            int x = LabelX(depth);
            return new Rectangle(x, y, Math.Max(10, areaW - x - Theme.S(8)), RowH);
        }

        public void SetCurrentKey(string? key)
        {
            CurrentKey = key;
            if (key == null) { Invalidate(); return; }
            var rows = GetRows?.Invoke() ?? new List<PermRow>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Node.Key == key) { EnsureVisible(i); break; }
            }
            Invalidate();
        }

        public void FocusKey(string? key)
        {
            if (key == null) return;
            var rows = GetRows?.Invoke() ?? new List<PermRow>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Node.Key == key) { _focusRow = i; _kbdFocus = false; EnsureVisible(i); Invalidate(); return; }
            }
        }

        private void EnsureVisible(int row)
        {
            UpdateScroll();
            int y0 = PadTop + row * RowH - _vscroll.Value;
            int y1 = y0 + RowH;
            if (y0 < 0) _vscroll.Value = Math.Max(_vscroll.Minimum, _vscroll.Value + y0);
            else if (y1 > Height) _vscroll.Value = Math.Min(_vscroll.Maximum, _vscroll.Value + (y1 - Height));
        }

        private void UpdateScroll()
        {
            var rows = GetRows?.Invoke() ?? new List<PermRow>();
            int totalH = PadTop + rows.Count * RowH + RowPadBottom;
            int viewH = Math.Max(RowH, Height);
            bool need = totalH > viewH;
            if (need != _vscroll.Visible)
            {
                _vscroll.SetBounds(Width - _vscroll.Width, 0, _vscroll.Width, Height);
                _vscroll.Visible = need;
            }
            if (need)
            {
                _vscroll.Minimum = 0;
                _vscroll.Maximum = Math.Max(0, totalH - viewH) + _vscroll.LargeChange - 1;
                _vscroll.LargeChange = Math.Max(RowH, viewH);
                _vscroll.SmallChange = RowH;
                if (_vscroll.Value > _vscroll.Maximum) _vscroll.Value = _vscroll.Maximum;
            }
            else if (_vscroll.Value != 0)
            {
                _vscroll.Value = 0;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.HiQuality(g);

            if (ShowStatus)
            {
                DrawStatus(g);
                return;
            }

            var rows = GetRows?.Invoke() ?? new List<PermRow>();
            UpdateScroll();

            if (rows.Count == 0)
            {
                DrawEmptyState(g);
                return;
            }

            int areaW = RowsAreaWidth;
            int scroll = _vscroll.Value;
            int first = Math.Max(0, (scroll - PadTop) / RowH);
            int visibleCount = Height / RowH + 2;

            for (int i = first; i < rows.Count && i <= first + visibleCount; i++)
            {
                int y = PadTop + i * RowH - scroll;
                if (y + RowH < 0 || y > Height) continue;
                DrawRow(g, rows[i], i, y, areaW);
            }
        }

        private void DrawRow(Graphics g, PermRow row, int index, int y, int areaW)
        {
            var node = row.Node;
            bool locked = GetLocked?.Invoke(node) ?? false;
            string st = GetState?.Invoke(node) ?? "off";

            // 行背景：hover/焦点（置灰行无 hover 底纹）
            bool bg = (!locked) && (index == _hoverRow || (index == _focusRow && !_kbdFocus));
            if (bg) using (var b = new SolidBrush(Theme.FieldHover)) g.FillRectangle(b, 0, y, areaW, RowH);
            // 当前搜索命中：info-bg + 主色 2px 内框
            if (node.Key == CurrentKey)
            {
                using (var b = new SolidBrush(Theme.InfoBg)) g.FillRectangle(b, 0, y, areaW, RowH);
                using (var p = new Pen(Theme.Accent, Theme.S(2)))
                    g.DrawRectangle(p, Theme.S(1), y + Theme.S(1), areaW - Theme.S(2), RowH - Theme.S(2));
            }
            // 键盘焦点环：2px #1A1A1A 内框
            if (_kbdFocus && index == _focusRow)
            {
                using (var p = new Pen(FocusOuter, Theme.S(2)))
                    g.DrawRectangle(p, Theme.S(1), y + Theme.S(1), areaW - Theme.S(2), RowH - Theme.S(2));
            }

            Color iconColor = locked ? Theme.TextTertiary : (node.IsDept ? Theme.Accent : Theme.TextSecondary);
            Color labelColor = locked ? Theme.TextTertiary : Theme.TextPrimary;
            Color cbxBg = locked ? Theme.Divider : (st == "on" || st == "mixed" ? Theme.Accent : Theme.FieldRest);
            Color cbxBorder = locked ? Theme.Divider : (index == _hoverRow ? Theme.StrokeHover : Theme.Stroke);
            Color markColor = locked ? Theme.TextTertiary : Color.White;

            // twisty（叶子隐藏但占位）
            bool hasKids = node.HasChildren;
            if (hasKids)
            {
                var tr = TwistyRect(row.Depth, y);
                if (_hoverTwisty && index == _hoverRow)
                {
                    using var b = new SolidBrush(Theme.TitleBtnHover);
                    g.FillRectangle(b, tr);
                }
                PermIcons.DrawTwisty(g, new Rectangle(tr.X + (tr.Width - Theme.S(12)) / 2, tr.Y + (tr.Height - Theme.S(12)) / 2, Theme.S(12), Theme.S(12)),
                    Theme.TextSecondary, GetExpanded?.Invoke(node) ?? false);
            }

            // 复选框
            var cr = CbxRect(row.Depth, y);
            using (var path = Theme.RoundedRect(cr, Theme.S(4)))
            {
                using (var b = new SolidBrush(cbxBg)) g.FillPath(b, path);
                using (var p = new Pen(cbxBorder, 1f)) g.DrawPath(p, path);
            }
            var markRect = new Rectangle(cr.X + (cr.Width - Theme.S(13)) / 2, cr.Y + (cr.Height - Theme.S(13)) / 2, Theme.S(13), Theme.S(13));
            if (st == "on") Theme.DrawCheck(g, markRect, markColor);
            else if (st == "mixed") PermIcons.DrawMixed(g, markRect, markColor);

            // 图标
            var ir = IconRect(row.Depth, y);
            if (node.IsDept) PermIcons.DrawDept(g, ir, iconColor);
            else PermIcons.DrawUser(g, ir, iconColor);

            // 标签（根级 bold；搜索命中词 sel-bg 高亮 + bold；溢出省略）
            var lr = LabelRect(row.Depth, y, areaW);
            DrawLabel(g, node, lr, labelColor, row.Depth == 0);
        }

        private void DrawLabel(Graphics g, PermNodeModel node, Rectangle rect, Color color, bool bold)
        {
            var hl = GetHighlight?.Invoke(node);
            var font = bold ? LabelBoldFont : Theme.Body;
            const TextFormatFlags tf = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
                                     | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;

            if (hl == null)
            {
                TextRenderer.DrawText(g, node.Name, font, rect, color, tf);
                return;
            }

            // 命中词 sel-bg 高亮加粗：前置 + 高亮 + 后置 三段精确排布（TextRenderer 无边距，
            // 测量与绘制一致，避免 GDI+ MeasureString 内边距导致的漂移/裁切）
            string name = node.Name;
            string before = name.Substring(0, hl.Value.start);
            string match = name.Substring(hl.Value.start, hl.Value.len);
            string after = name.Substring(hl.Value.start + hl.Value.len);
            var fReg = Theme.Body;
            var fBold = LabelBoldFont;

            int total = TextRenderer.MeasureText(before, fReg).Width
                      + TextRenderer.MeasureText(match, fBold).Width
                      + TextRenderer.MeasureText(after, fReg).Width;
            if (total > rect.Width)
            {
                TextRenderer.DrawText(g, name, font, rect, color, tf);
                return;
            }

            int x = rect.X;
            if (before.Length > 0)
            {
                int w = TextRenderer.MeasureText(before, fReg).Width;
                TextRenderer.DrawText(g, before, fReg, new Rectangle(x, rect.Y, w, rect.Height), color, tf);
                x += w;
            }

            var ms = TextRenderer.MeasureText(match, fBold);
            var mr = new Rectangle(x, rect.Y + (rect.Height - ms.Height) / 2, ms.Width, ms.Height);
            using (var path = Theme.RoundedRect(mr, Theme.S(2)))
            using (var b = new SolidBrush(Theme.SelBg))
                g.FillPath(b, path);
            TextRenderer.DrawText(g, match, fBold, mr, color, tf);
            x += ms.Width;

            if (after.Length > 0)
            {
                int w = TextRenderer.MeasureText(after, fReg).Width;
                TextRenderer.DrawText(g, after, fReg, new Rectangle(x, rect.Y, w, rect.Height), color, tf);
            }
        }

        private void DrawStatus(Graphics g)
        {
            Color color = StatusIsError ? Theme.Danger : Theme.TextTertiary;
            TextRenderer.DrawText(g, StatusText, Theme.Body,
                new Rectangle(0, 0, Width, Height), color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        }

        private void DrawEmptyState(Graphics g)
        {
            int isz = Theme.S(28);
            var icon = new Rectangle((Width - isz) / 2, Height / 2 - Theme.S(30), isz, isz);
            Theme.DrawSearchIcon(g, icon, EmptyIconGray);
            TextRenderer.DrawText(g, EmptyTitle, Theme.Body,
                new Rectangle(Theme.S(16), icon.Bottom + Theme.S(6), Width - Theme.S(32), Theme.S(18)),
                Theme.TextSecondary, TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, EmptySub, Theme.Small,
                new Rectangle(Theme.S(16), icon.Bottom + Theme.S(6) + Theme.S(18), Width - Theme.S(32), Theme.S(16)),
                Theme.TextTertiary, TextFormatFlags.HorizontalCenter);
        }

        // ===== 输入 =====

        private List<PermRow> SnapshotRows() => GetRows?.Invoke() ?? new List<PermRow>();

        private int RowAt(int py)
        {
            var rows = SnapshotRows();
            int idx = (py - PadTop + _vscroll.Value) / RowH;
            if (idx < 0 || idx >= rows.Count) return -1;
            int y = PadTop + idx * RowH - _vscroll.Value;
            if (py < y || py > y + RowH) return -1;
            return idx;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int row = RowAt(e.Y);
            bool tw = false;
            if (row >= 0)
            {
                var rows = SnapshotRows();
                var node = rows[row].Node;
                if (node.HasChildren && TwistyRect(rows[row].Depth, PadTop + row * RowH - _vscroll.Value).Contains(e.Location))
                    tw = true;
            }
            if (row != _hoverRow || tw != _hoverTwisty)
            {
                _hoverRow = row;
                _hoverTwisty = tw;
                Invalidate();
            }
            Cursor = row >= 0 ? Cursors.Hand : Cursors.Default;

            // 置灰行悬浮提示（原型 aria title）
            if (row >= 0 && row != _tipRow)
            {
                _tipRow = row;
                var rows = SnapshotRows();
                var node = rows[row].Node;
                HideTip();
                if ((GetLocked?.Invoke(node) ?? false))
                {
                    int y = PadTop + row * RowH - _vscroll.Value;
                    _tip.Show(LockedTip, this, PadLeft(rows[row].Depth) + Theme.S(30), y + RowH + 2, 4000);
                }
            }
            else if (row < 0 && _tipRow >= 0)
            {
                _tipRow = -1;
                HideTip();
            }
        }
        private int _tipRow = -1;

        private void HideTip() { if (_tip.Active) _tip.Hide(this); }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverRow = -1;
            _hoverTwisty = false;
            _tipRow = -1;
            HideTip();
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            int row = RowAt(e.Y);
            if (row < 0) return;
            var rows = SnapshotRows();
            var node = rows[row].Node;
            if (node.HasChildren && TwistyRect(rows[row].Depth, PadTop + row * RowH - _vscroll.Value).Contains(e.Location))
            {
                ExpandToggled?.Invoke(node);
                return;
            }
            _focusRow = row;
            _kbdFocus = false;
            ToggleRequested?.Invoke(node);
        }

        protected override bool IsInputKey(Keys keyData) =>
            keyData is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Space or Keys.Enter
            ? true : base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            var rows = SnapshotRows();
            if (rows.Count == 0) return;

            switch (e.KeyCode)
            {
                case Keys.Down:
                case Keys.Up:
                    _kbdFocus = true;
                    int d = e.KeyCode == Keys.Down ? 1 : -1;
                    if (_focusRow < 0) _focusRow = 0;
                    else _focusRow = Math.Max(0, Math.Min(rows.Count - 1, _focusRow + d));
                    EnsureVisible(_focusRow);
                    Invalidate();
                    e.Handled = true;
                    break;

                case Keys.Left:
                case Keys.Right:
                    if (_focusRow >= 0 && _focusRow < rows.Count)
                    {
                        var node = rows[_focusRow].Node;
                        if (node.HasChildren)
                        {
                            _kbdFocus = true;
                            ExpandToggled?.Invoke(node);
                            e.Handled = true;
                        }
                    }
                    break;

                case Keys.Space:
                case Keys.Enter:
                    if (_focusRow >= 0 && _focusRow < rows.Count)
                    {
                        _kbdFocus = true;
                        ToggleRequested?.Invoke(rows[_focusRow].Node);
                        e.Handled = true;
                    }
                    break;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!_vscroll.Visible) return;
            int delta = e.Delta > 0 ? -RowH * 2 : RowH * 2;
            _vscroll.Value = Math.Max(_vscroll.Minimum, Math.Min(_vscroll.Maximum, _vscroll.Value + delta));
        }
    }

    // =====================================================================
    // 已选清单：原型 .sel-row —— 行高 32（图标 16 + 名称 13px + 路径 12px tertiary）+
    // 移除按钮 24x24 吸附可视区右缘（hover 显形，hover 红）；名称与路径单行完整保留，
    // 超宽走横向滚动；空状态图标 + 两行文案。
    // =====================================================================
    public class PermSelList : Control
    {
        public Func<List<SelItem>>? GetItems { get; set; }
        public Action<PermNodeModel>? RemoveRequested { get; set; }

        public bool DeptKind { get; set; }              // 空状态 / 行图标类型
        public string EmptyTitle { get; set; } = "";
        public string EmptySub { get; set; } = "";

        private static readonly Color EmptyIconGray = Color.FromArgb(0xC7, 0xC7, 0xC7);
        private static readonly Color RemoveHoverBg = Color.FromArgb(0xEB, 0xEB, 0xEB);

        private readonly VScrollBar _vscroll = new() { Visible = false };
        private readonly HScrollBar _hscroll = new() { Visible = false };
        private int _hoverRow = -1;
        private bool _hoverRemove;

        private int RowH => Theme.S(32);

        public PermSelList()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.FieldRest;

            Controls.Add(_vscroll);
            Controls.Add(_hscroll);
            _vscroll.ValueChanged += (_, _) => Invalidate();
            _hscroll.ValueChanged += (_, _) => Invalidate();
            Resize += (_, _) => UpdateScroll();
        }

        private bool VVisible => _vscroll.Visible;
        private int RowsW => Width - (VVisible ? _vscroll.Width : 0);
        private int RowsH => Height - (_hscroll.Visible ? _hscroll.Height : 0);

        private int ContentWidth(List<SelItem> items)
        {
            using var g = CreateGraphics();
            int max = RowsW;
            foreach (var it in items)
            {
                int w = Theme.S(10) + Theme.S(16) + Theme.S(6)
                      + TextRenderer.MeasureText(it.Node.Name, Theme.Body).Width + Theme.S(2)
                      + (string.IsNullOrEmpty(it.Path) ? 0 : TextRenderer.MeasureText(it.Path, Theme.Small).Width + Theme.S(4))
                      + Theme.S(4) + Theme.S(24) + Theme.S(2);
                max = Math.Max(max, w);
            }
            return max;
        }

        private void UpdateScroll()
        {
            var items = GetItems?.Invoke() ?? new List<SelItem>();
            int totalH = items.Count * RowH;
            bool vNeed = totalH > Math.Max(RowH, RowsH);
            int hMax = Math.Max(0, ContentWidth(items) - RowsW);
            bool hNeed = hMax > 0;

            if (vNeed != _vscroll.Visible)
            {
                _vscroll.SetBounds(Width - _vscroll.Width, 0, _vscroll.Width, Height - (_hscroll.Visible ? _hscroll.Height : 0));
                _vscroll.Visible = vNeed;
            }
            if (hNeed != _hscroll.Visible)
            {
                _hscroll.SetBounds(0, Height - _hscroll.Height, RowsW, _hscroll.Height);
                _hscroll.Visible = hNeed;
            }
            if (_vscroll.Visible) _vscroll.SetBounds(_vscroll.Left, 0, _vscroll.Width, RowsH);
            if (_hscroll.Visible) _hscroll.SetBounds(0, Height - _hscroll.Height, RowsW, _hscroll.Height);

            if (vNeed)
            {
                _vscroll.Minimum = 0;
                _vscroll.Maximum = Math.Max(0, totalH - RowsH) + _vscroll.LargeChange - 1;
                _vscroll.LargeChange = Math.Max(RowH, RowsH);
                _vscroll.SmallChange = RowH;
                if (_vscroll.Value > _vscroll.Maximum) _vscroll.Value = _vscroll.Maximum;
            }
            else if (_vscroll.Value != 0) _vscroll.Value = 0;

            if (hNeed)
            {
                _hscroll.Minimum = 0;
                _hscroll.Maximum = hMax + _hscroll.LargeChange - 1;
                _hscroll.LargeChange = Math.Max(10, RowsW);
                _hscroll.SmallChange = Theme.S(16);
            }
            else if (_hscroll.Value != 0) _hscroll.Value = 0;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.HiQuality(g);
            var items = GetItems?.Invoke() ?? new List<SelItem>();
            UpdateScroll();

            if (items.Count == 0)
            {
                DrawEmpty(g);
                return;
            }

            int hx = _hscroll.Value;
            int first = Math.Max(0, _vscroll.Value / RowH);
            int count = RowsH / RowH + 2;

            for (int i = first; i < items.Count && i <= first + count; i++)
            {
                int y = i * RowH - _vscroll.Value;
                if (y + RowH < 0 || y > RowsH) continue;
                var it = items[i];
                var rmRect = RemoveRect(y);
                bool hover = i == _hoverRow;

                if (hover) using (var b = new SolidBrush(Theme.FieldHover)) g.FillRectangle(b, 0, y, RowsW, RowH);

                // 移除按钮：吸附可视区右缘（横向滚动时始终够得着），每行常显
                Color rmFg = _hoverRemove && hover ? Theme.Danger : Theme.TextSecondary;
                if (_hoverRemove && hover)
                    using (var b = new SolidBrush(RemoveHoverBg)) g.FillRectangle(b, rmRect);
                PermIcons.DrawX(g, new Rectangle(rmRect.X + (rmRect.Width - Theme.S(12)) / 2, rmRect.Y + (rmRect.Height - Theme.S(12)) / 2, Theme.S(12), Theme.S(12)), rmFg);

                int x = Theme.S(10) - hx;
                var icon = new Rectangle(x, y + (RowH - Theme.S(16)) / 2, Theme.S(16), Theme.S(16));
                if (it.Node.IsDept) PermIcons.DrawDept(g, icon, Theme.Accent);
                else PermIcons.DrawUser(g, icon, Theme.TextSecondary);
                x += Theme.S(16) + Theme.S(6);

                // 名称与路径均单行完整保留（不做省略号截断，横向滚动查看）
                TextRenderer.DrawText(g, it.Node.Name, Theme.Body, new Point(x, y + (RowH - 18) / 2), Theme.TextPrimary);
                x += TextRenderer.MeasureText(it.Node.Name, Theme.Body).Width + Theme.S(2);

                if (!string.IsNullOrEmpty(it.Path))
                {
                    TextRenderer.DrawText(g, it.Path, Theme.Small, new Point(x, y + (RowH - 16) / 2), Theme.TextTertiary);
                }
            }
        }

        private Rectangle RemoveRect(int rowY) => new(RowsW - Theme.S(2) - Theme.S(24), rowY + (RowH - Theme.S(24)) / 2, Theme.S(24), Theme.S(24));

        private void DrawEmpty(Graphics g)
        {
            int isz = Theme.S(26);
            var icon = new Rectangle((Width - isz) / 2, Height / 2 - Theme.S(26), isz, isz);
            if (DeptKind) PermIcons.DrawDept(g, icon, EmptyIconGray);
            else PermIcons.DrawUser(g, icon, EmptyIconGray);
            TextRenderer.DrawText(g, EmptyTitle, Theme.Small,
                new Rectangle(Theme.S(14), icon.Bottom + Theme.S(6), Width - Theme.S(28), Theme.S(16)),
                Theme.TextTertiary, TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, EmptySub, Theme.Tiny,
                new Rectangle(Theme.S(14), icon.Bottom + Theme.S(6) + Theme.S(16), Width - Theme.S(28), Theme.S(15)),
                Theme.TextTertiary, TextFormatFlags.HorizontalCenter);
        }

        private int RowAt(int py)
        {
            var items = GetItems?.Invoke() ?? new List<SelItem>();
            int idx = (py + _vscroll.Value) / RowH;
            if (idx < 0 || idx >= items.Count) return -1;
            int y = idx * RowH - _vscroll.Value;
            if (py < y || py > y + RowH) return -1;
            return idx;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int row = RowAt(e.Y);
            bool rm = row >= 0 && RemoveRect(row * RowH - _vscroll.Value).Contains(e.Location);
            if (row != _hoverRow || rm != _hoverRemove)
            {
                _hoverRow = row;
                _hoverRemove = rm;
                Invalidate();
            }
            Cursor = row >= 0 ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverRow = -1;
            _hoverRemove = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            int row = RowAt(e.Y);
            if (row < 0) return;
            if (RemoveRect(row * RowH - _vscroll.Value).Contains(e.Location))
            {
                var items = GetItems?.Invoke() ?? new List<SelItem>();
                RemoveRequested?.Invoke(items[row].Node);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_vscroll.Visible)
            {
                int delta = e.Delta > 0 ? -RowH : RowH;
                _vscroll.Value = Math.Max(_vscroll.Minimum, Math.Min(_vscroll.Maximum, _vscroll.Value + delta));
            }
            else if (_hscroll.Visible)
            {
                int delta = e.Delta > 0 ? -Theme.S(32) : Theme.S(32);
                _hscroll.Value = Math.Max(_hscroll.Minimum, Math.Min(_hscroll.Maximum, _hscroll.Value + delta));
            }
        }
    }

    // =====================================================================
    // 小按钮：原型 .btn-sm —— 高 32、min-width 64、圆角 4、13px；primary 主色 /
    // ghost 白底描边；disabled 按原型置灰；保存中带 14px 白色旋转环。
    // =====================================================================
    public enum SmallButtonStyle { Primary, Ghost }

    public class SmallButton : Control
    {
        private readonly System.Windows.Forms.Timer _spinTimer = new() { Interval = 50 };
        private float _ringAngle;
        private bool _hover, _pressed;

        public SmallButtonStyle Style { get; set; }
        public bool Loading { get; set; }

        public SmallButton(string text, SmallButtonStyle style)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            Text = text;
            Style = style;
            Height = Theme.S(32);
            Font = Theme.Body;
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;

            _spinTimer.Tick += (_, _) => { _ringAngle = (_ringAngle + 12f) % 360f; Invalidate(); };
            EnabledChanged += (_, _) => { if (!Enabled) { Loading = false; _spinTimer.Stop(); } Invalidate(); };
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; _pressed = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); } }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _pressed = false; Invalidate(); }

        public void SetLoading(bool loading)
        {
            Loading = loading;
            if (loading) _spinTimer.Start(); else _spinTimer.Stop();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.HiQuality(g);
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            bool active = Enabled && !Loading;

            using var path = Theme.RoundedRect(rect, Theme.S(4));
            if (Style == SmallButtonStyle.Primary)
            {
                Color bg = !active ? Theme.AccentDisabled
                    : _pressed ? Theme.AccentPressed
                    : _hover ? Theme.AccentHover : Theme.Accent;
                using (var b = new SolidBrush(bg)) g.FillPath(b, path);
            }
            else
            {
                Color bg = !active ? Theme.FieldDisabled : (_hover ? Theme.FieldHover : Theme.FieldRest);
                Color border = !active ? Theme.Divider : (_hover ? Theme.StrokeHover : Theme.Stroke);
                using (var b = new SolidBrush(bg)) g.FillPath(b, path);
                using (var p = new Pen(border, 1f)) g.DrawPath(p, path);
            }

            Color fg = Style == SmallButtonStyle.Primary
                ? (active ? Color.White : Color.White)
                : (active ? Theme.TextPrimary : Theme.TextTertiary);

            var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            var ts = g.MeasureString(Text, Font, int.MaxValue, fmt);
            int ringW = Loading ? Theme.S(14) + Theme.S(6) : 0;
            float totalW = ts.Width + ringW;
            float textX = (Width - totalW) / 2f;

            if (Loading)
            {
                int rsz = Theme.S(14);
                var rr = new Rectangle((int)Math.Round(textX), (Height - rsz) / 2, rsz, rsz);
                using (var pen = new Pen(Color.FromArgb(120, Color.White), Theme.S(2)))
                    g.DrawEllipse(pen, rr);
                using (var pen = new Pen(Color.White, Theme.S(2)))
                {
                    pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                    g.DrawArc(pen, rr, _ringAngle, 100f);
                }
            }
            using (var b = new SolidBrush(fg))
                g.DrawString(Text, Font, b, new RectangleF(textX + ringW, 0, ts.Width + 4, Height), fmt);
        }
    }

    // =====================================================================
    // 搜索定位箭头：原型 .nav-arrow —— 32x32 ghost 方钮（◀ / ▶ 14px），禁用置灰。
    // =====================================================================
    public class NavArrow : Control
    {
        public bool Left { get; set; }
        private bool _hover;

        public NavArrow(bool left)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            Left = left;
            Size = new Size(Theme.S(32), Theme.S(32));
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.HiQuality(g);
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color bg = !Enabled ? Theme.FieldDisabled : (_hover ? Theme.FieldHover : Theme.FieldRest);
            Color border = !Enabled ? Theme.Divider : (_hover ? Theme.StrokeHover : Theme.Stroke);
            Color fg = !Enabled ? Theme.TextTertiary : Theme.TextPrimary;

            using (var path = Theme.RoundedRect(rect, Theme.S(4)))
            {
                using (var b = new SolidBrush(bg)) g.FillPath(b, path);
                using (var p = new Pen(border, 1f)) g.DrawPath(p, path);
            }

            int isz = Theme.S(14);
            var icon = new Rectangle((Width - isz) / 2, (Height - isz) / 2, isz, isz);
            if (Left)
            {
                // ◀（viewBox 16: M10 3.5 5.5 8 10 12.5, stroke 1.8）
                float s = isz / 16f;
                using var pen = new Pen(fg, 1.8f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                g.DrawLines(pen, new[]
                {
                    new PointF(icon.X + 10 * s, icon.Y + 3.5f * s),
                    new PointF(icon.X + 5.5f * s, icon.Y + 8 * s),
                    new PointF(icon.X + 10 * s, icon.Y + 12.5f * s)
                });
            }
            else
            {
                // ▶（viewBox 16: M6 3.5 10.5 8 6 12.5）
                float s = isz / 16f;
                using var pen = new Pen(fg, 1.8f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                g.DrawLines(pen, new[]
                {
                    new PointF(icon.X + 6 * s, icon.Y + 3.5f * s),
                    new PointF(icon.X + 10.5f * s, icon.Y + 8 * s),
                    new PointF(icon.X + 6 * s, icon.Y + 12.5f * s)
                });
            }
        }
    }

    // =====================================================================
    // 状态旗：原型 .bar-flag —— dirty（橙点 + 橙字）/ ok（成功圆 + 绿字），互斥展示。
    // =====================================================================
    public class BarFlag : Control
    {
        public bool Ok { get; set; }

        public BarFlag()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            Height = Theme.S(32);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.HiQuality(g);

            // 内容右对齐（紧贴动作按钮）
            var ts = TextRenderer.MeasureText(Text, Theme.Small);
            int textW = ts.Width;
            int iconW = Ok ? Theme.S(14) : Theme.S(6);
            int gap = Theme.S(6);
            int total = iconW + gap + textW;
            int x = Width - total;
            int cy = Height / 2;

            if (Ok)
            {
                var icon = new Rectangle(x, cy - Theme.S(7), Theme.S(14), Theme.S(14));
                Theme.DrawSuccessCircle(g, icon, Theme.Success);
                TextRenderer.DrawText(g, Text, Theme.Small, new Point(x + iconW + gap, cy - ts.Height / 2), Theme.Success);
            }
            else
            {
                using var b = new SolidBrush(Theme.Warn);
                g.FillEllipse(b, x, cy - Theme.S(3), Theme.S(6), Theme.S(6));
                TextRenderer.DrawText(g, Text, Theme.Small, new Point(x + iconW + gap, cy - ts.Height / 2), Theme.Warn);
            }
        }
    }
}
