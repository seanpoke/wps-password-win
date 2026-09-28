using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace PasswordManager.UI.Controls
{
    /// <summary>
    /// 界面主题：颜色、字体与图标绘制。
    /// 数值全部来自甲方确认版原型 app-prototype.html（:root 变量表），96DPI 基准：1px = 0.75pt。
    /// </summary>
    internal static class Theme
    {
        // ===== DPI 等比缩放 =====
        // 原型所有像素基于 96DPI；高 DPI 下字体（pt）自动放大，布局像素必须等比缩放才能保持
        // 文字/容器比例与原型一致（否则文字溢出容器被裁）。字体保持 pt 不动，布局一律经 S() 缩放。
        private static float? _scale;

        /// <summary>系统 DPI 相对 96 的缩放系数（96→1.0、120→1.25、144→1.5）。</summary>
        public static float Scale
        {
            get
            {
                if (!_scale.HasValue)
                {
                    try
                    {
                        using var g = Graphics.FromHwnd(IntPtr.Zero);
                        _scale = g.DpiX / 96f;
                    }
                    catch { _scale = 1f; }
                }
                return _scale.Value;
            }
        }

        /// <summary>96DPI 基准像素 → 当前 DPI 实际像素。</summary>
        public static int S(int px) => (int)Math.Round(px * Scale);

        // ===== 颜色（与原型 :root 一一对应） =====
        public static readonly Color Canvas = Color.FromArgb(0xF3, 0xF3, 0xF3);          // --canvas
        public static readonly Color FieldRest = Color.White;                             // --field-rest
        public static readonly Color FieldHover = Color.FromArgb(0xF5, 0xF5, 0xF5);      // --field-hover
        public static readonly Color FieldDisabled = Color.FromArgb(0xF5, 0xF5, 0xF5);  // --field-disabled
        public static readonly Color Stroke = Color.FromArgb(0x8A, 0x8A, 0x8A);         // --stroke
        public static readonly Color StrokeHover = Color.FromArgb(0x61, 0x61, 0x61);     // --stroke-hover
        public static readonly Color Accent = Color.FromArgb(0x00, 0x5F, 0xB8);         // --accent
        public static readonly Color AccentHover = Color.FromArgb(0x00, 0x67, 0xC0);     // --accent-hover
        public static readonly Color AccentPressed = Color.FromArgb(0x19, 0x75, 0xC5);   // --accent-pressed
        public static readonly Color AccentDisabled = Color.FromArgb(0xC5, 0xC5, 0xC5); // --accent-disabled
        public static readonly Color TextPrimary = Color.FromArgb(0x1B, 0x1B, 0x1B);    // --text-primary
        public static readonly Color TextSecondary = Color.FromArgb(0x60, 0x60, 0x60);   // --text-secondary
        public static readonly Color TextTertiary = Color.FromArgb(0x87, 0x87, 0x87);   // --text-tertiary
        public static readonly Color TextLink = Color.FromArgb(0x00, 0x5F, 0xB8);       // --text-link
        public static readonly Color Danger = Color.FromArgb(0xC4, 0x2B, 0x1C);         // --danger
        public static readonly Color DangerHover = Color.FromArgb(0xA8, 0x23, 0x1A);    // --danger-hover
        public static readonly Color DangerBg = Color.FromArgb(0xFD, 0xE7, 0xE9);       // --danger-bg
        public static readonly Color Warn = Color.FromArgb(0xC7, 0x5B, 0x12);           // --warn（橙色，与 info 蓝明显区分）
        public static readonly Color WarnBg = Color.FromArgb(0xFF, 0xEF, 0xDC);          // --warn-bg（浅橙底）
        public static readonly Color InfoBg = Color.FromArgb(0xE5, 0xF1, 0xFB);          // --info-bg
        public static readonly Color Success = Color.FromArgb(0x0F, 0x7B, 0x0F);        // --success
        public static readonly Color SuccessBg = Color.FromArgb(0xEA, 0xF6, 0xEA);      // --success-bg
        public static readonly Color Divider = Color.FromArgb(0xE0, 0xE0, 0xE0);        // --divider
        public static readonly Color SelBg = Color.FromArgb(0xFF, 0xF1, 0xA8);          // --sel-bg（关键词高亮）
        public static readonly Color FocusOuter = Color.FromArgb(0x1A, 0x1A, 0x1A);     // --focus-outer
        public static readonly Color TitleBtnHover = Color.FromArgb(15, 0, 0, 6);       // rgba(0,0,0,.06)
        public static readonly Color LogInfoFg = Color.FromArgb(0x00, 0x47, 0x7F);      // 日志 INFO 前景

        // ===== 字体（原型 px → pt：18/16/14/13/12/11 px） =====
        private const string YaHei = "Microsoft YaHei UI";
        private const string YaHeiFallback = "Microsoft YaHei";

        private static string? _monoFamily;
        /// <summary>等宽字体族：优先 Cascadia Mono，缺失时退回 Consolas（与原型 --font-mono 一致）。</summary>
        public static string MonoFamily
        {
            get
            {
                if (_monoFamily == null)
                {
                    _monoFamily = "Consolas";
                    try
                    {
                        using var fonts = new InstalledFontCollection();
                        foreach (var f in fonts.Families)
                        {
                            if (f.Name == "Cascadia Mono") { _monoFamily = "Cascadia Mono"; break; }
                        }
                    }
                    catch { }
                }
                return _monoFamily;
            }
        }

        private static string YaHeiFamily
        {
            get
            {
                try
                {
                    using var fonts = new InstalledFontCollection();
                    foreach (var f in fonts.Families)
                        if (f.Name == YaHei) return YaHei;
                }
                catch { }
                return YaHeiFallback;
            }
        }

        private static Font? _h1;    // 18px / 700
        private static Font? _input;  // 14px
        private static Font? _body;   // 13px
        private static Font? _small;  // 12px
        private static Font? _tiny;   // 11px
        private static Font? _monoSmall;   // mono 12px
        private static Font? _monoBody;    // mono 13px

        public static Font H1 => _h1 ??= new Font(YaHeiFamily, 13.5f, FontStyle.Bold, GraphicsUnit.Point);
        public static Font Input => _input ??= new Font(YaHeiFamily, 10.5f, FontStyle.Regular, GraphicsUnit.Point);
        public static Font Body => _body ??= new Font(YaHeiFamily, 9.75f, FontStyle.Regular, GraphicsUnit.Point);
        public static Font Small => _small ??= new Font(YaHeiFamily, 9f, FontStyle.Regular, GraphicsUnit.Point);
        public static Font Tiny => _tiny ??= new Font(YaHeiFamily, 8.25f, FontStyle.Regular, GraphicsUnit.Point);
        public static Font MonoSmall => _monoSmall ??= new Font(MonoFamily, 9f, FontStyle.Regular, GraphicsUnit.Point);
        public static Font MonoBody => _monoBody ??= new Font(MonoFamily, 9.75f, FontStyle.Regular, GraphicsUnit.Point);
        private static Font? _monoTinyBold;
        public static Font MonoTinyBold => _monoTinyBold ??= new Font(MonoFamily, 8.25f, FontStyle.Bold, GraphicsUnit.Point);   // 日志等级徽章 11px bold

        /// <summary>创建按像素值换算的 YaHei 字体（供个别场景使用）。</summary>
        public static Font Px(float px, FontStyle style = FontStyle.Regular) =>
            new Font(YaHeiFamily, px * 0.75f, style, GraphicsUnit.Point);

        /// <summary>等宽字体（按像素值）。</summary>
        public static Font MonoPx(float px) =>
            new Font(MonoFamily, px * 0.75f, FontStyle.Regular, GraphicsUnit.Point);

        // ===== 几何 =====
        /// <summary>圆角矩形路径（半径为像素，与原型 border-radius 一致）。</summary>
        public static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            if (radius < 1 || r.Width < 2 || r.Height < 2) { path.AddRectangle(r); return path; }
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>高质量绘制设置（抗锯齿，模拟浏览器矢量渲染）。</summary>
        public static void HiQuality(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        }

        // ===== 图标（矢量参数与原型 SVG path 一致） =====

        /// <summary>眼睛（18px，viewBox 24）。off=true 时加斜线。</summary>
        public static void DrawEye(Graphics g, Rectangle r, Color color, bool off)
        {
            float s = r.Width / 24f;
            using var pen = new Pen(color, 2f * s);
            g.DrawPath(pen, EyePath(r.X, r.Y, s));
            using var pupil = new Pen(color, 2f * s);
            g.DrawEllipse(pupil, r.X + (12 - 3) * s, r.Y + (12 - 3) * s, 6 * s, 6 * s);
            if (off)
            {
                using var p2 = new Pen(color, 2f * s);
                g.DrawLine(p2, r.X + 4 * s, r.Y + 4 * s, r.X + 20 * s, r.Y + 20 * s);
            }
        }

        private static GraphicsPath EyePath(float ox, float oy, float s)
        {
            // path: M2 12 s3.5-7 10-7 10 7 10 7 -3.5 7 -10 7 -10-7 -10-7 Z
            var path = new GraphicsPath();
            path.AddBezier(ox + 2 * s, oy + 12 * s, ox + 3.5f * s, oy + 5 * s, ox + 8 * s, oy + 5 * s, ox + 12 * s, oy + 5 * s);
            path.AddBezier(ox + 12 * s, oy + 5 * s, ox + 16 * s, oy + 5 * s, ox + 20.5f * s, oy + 5 * s, ox + 22 * s, oy + 12 * s);
            path.AddBezier(ox + 22 * s, oy + 12 * s, ox + 20.5f * s, oy + 19 * s, ox + 16 * s, oy + 19 * s, ox + 12 * s, oy + 19 * s);
            path.AddBezier(ox + 12 * s, oy + 19 * s, ox + 8 * s, oy + 19 * s, ox + 3.5f * s, oy + 19 * s, ox + 2 * s, oy + 12 * s);
            return path;
        }

        /// <summary>复选框对勾（viewBox 24, path M5 12l5 5 9-10, stroke 3）。</summary>
        public static void DrawCheck(Graphics g, Rectangle r, Color color, float strokeWidth = 3f)
        {
            float s = r.Width / 24f;
            using var pen = new Pen(color, strokeWidth * s)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            g.DrawLine(pen, r.X + 5 * s, r.Y + 12 * s, r.X + 10 * s, r.Y + 17 * s);
            g.DrawLine(pen, r.X + 10 * s, r.Y + 17 * s, r.X + 19 * s, r.Y + 7 * s);
        }

        /// <summary>下箭头 chevron（14px, viewBox 14, path M3 5l4 4 4-4, stroke 1.6）。</summary>
        public static void DrawChevron(Graphics g, Rectangle r, Color color, bool up)
        {
            float s = r.Width / 14f;
            using var pen = new Pen(color, 1.6f * s)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            var pts = new[]
            {
                new PointF(r.X + 3 * s, r.Y + 5 * s),
                new PointF(r.X + 7 * s, r.Y + 9 * s),
                new PointF(r.X + 11 * s, r.Y + 5 * s)
            };
            if (up)
            {
                for (int i = 0; i < pts.Length; i++) pts[i].Y = r.Bottom - pts[i].Y + 2 * r.Top;
            }
            g.DrawLines(pen, pts);
        }

        /// <summary>信息圆（18px, viewBox 24: circle r9 + M12 7v6 + dot 16.5）。</summary>
        public static void DrawInfoCircle(Graphics g, Rectangle r, Color color)
        {
            float s = r.Width / 24f;
            using var pen = new Pen(color, 2f * s);
            g.DrawEllipse(pen, r.X + 3 * s, r.Y + 3 * s, 18 * s, 18 * s);
            g.DrawLine(pen, r.X + 12 * s, r.Y + 7 * s, r.X + 12 * s, r.Y + 13 * s);
            float d = 2f * s;
            g.FillEllipse(new SolidBrush(color), r.X + 12 * s - d / 2, r.Y + 16.5f * s - d / 2, d, d);
        }

        /// <summary>警告三角（viewBox 24: M12 3.5 21 19H3l9-15.5Z + M12 10v4 + dot 16.6）。</summary>
        public static void DrawWarningTriangle(Graphics g, Rectangle r, Color color)
        {
            float s = r.Width / 24f;
            using var fill = new SolidBrush(color);
            var path = new GraphicsPath();
            path.AddPolygon(new[]
            {
                new PointF(r.X + 12 * s, r.Y + 3.5f * s),
                new PointF(r.X + 21 * s, r.Y + 19 * s),
                new PointF(r.X + 3 * s, r.Y + 19 * s)
            });
            g.FillPath(fill, path);
            // 挖出白色竖线与点（用背景色画）
            using var cut = new Pen(Color.White, 2f * s);
            g.DrawLine(cut, r.X + 12 * s, r.Y + 10 * s, r.X + 12 * s, r.Y + 14 * s);
            float d = 2f * s;
            g.FillEllipse(new SolidBrush(Color.White), r.X + 12 * s - d / 2, r.Y + 16.6f * s - d / 2, d, d);
        }

        /// <summary>成功圆（18px, viewBox 24: circle r9 + M7.5 12l3 3 6-6）。</summary>
        public static void DrawSuccessCircle(Graphics g, Rectangle r, Color color)
        {
            float s = r.Width / 24f;
            using var pen = new Pen(color, 2f * s)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            g.DrawEllipse(pen, r.X + 3 * s, r.Y + 3 * s, 18 * s, 18 * s);
            g.DrawLine(pen, r.X + 7.5f * s, r.Y + 12 * s, r.X + 10.5f * s, r.Y + 15 * s);
            g.DrawLine(pen, r.X + 10.5f * s, r.Y + 15 * s, r.X + 16.5f * s, r.Y + 9 * s);
        }

        /// <summary>房子（托盘菜单"主页"，viewBox 24: M4 11 12 4l8 7 + M6 10v9h12v-9）。</summary>
        public static void DrawHouse(Graphics g, Rectangle r, Color color)
        {
            float s = r.Width / 24f;
            using var pen = new Pen(color, 2f * s)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            g.DrawLines(pen, new[] { new PointF(r.X + 4 * s, r.Y + 11 * s), new PointF(r.X + 12 * s, r.Y + 4 * s), new PointF(r.X + 20 * s, r.Y + 11 * s) });
            g.DrawLines(pen, new[] { new PointF(r.X + 6 * s, r.Y + 10 * s), new PointF(r.X + 6 * s, r.Y + 19 * s), new PointF(r.X + 18 * s, r.Y + 19 * s), new PointF(r.X + 18 * s, r.Y + 10 * s) });
        }

        /// <summary>文档（托盘菜单"显示日志"，viewBox 24: rect 5,4,14,16 + 三条横线）。</summary>
        public static void DrawDocument(Graphics g, Rectangle r, Color color)
        {
            float s = r.Width / 24f;
            using var pen = new Pen(color, 2f * s)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            g.DrawRectangle(pen, r.X + 5 * s, r.Y + 4 * s, 14 * s, 16 * s);
            g.DrawLine(pen, r.X + 8 * s, r.Y + 9 * s, r.X + 16 * s, r.Y + 9 * s);
            g.DrawLine(pen, r.X + 8 * s, r.Y + 13 * s, r.X + 16 * s, r.Y + 13 * s);
            g.DrawLine(pen, r.X + 8 * s, r.Y + 17 * s, r.X + 13 * s, r.Y + 17 * s);
        }

        /// <summary>放大镜（托盘菜单"查询元数据"，viewBox 24: circle 11,11,6 + M15.5 15.5 20 20）。</summary>
        public static void DrawSearchIcon(Graphics g, Rectangle r, Color color)
        {
            float s = r.Width / 24f;
            using var pen = new Pen(color, 2f * s)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawEllipse(pen, r.X + 5 * s, r.Y + 5 * s, 12 * s, 12 * s);
            g.DrawLine(pen, r.X + 15.5f * s, r.Y + 15.5f * s, r.X + 20 * s, r.Y + 20 * s);
        }

        /// <summary>文件夹（托盘菜单"打开安装目录"，viewBox 24: M3 7V5h6l2 2h10v12H3z）。</summary>
        public static void DrawFolderIcon(Graphics g, Rectangle r, Color color)
        {
            float s = r.Width / 24f;
            using var pen = new Pen(color, 2f * s)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            g.DrawLines(pen, new[]
            {
                new PointF(r.X + 3 * s, r.Y + 19 * s),
                new PointF(r.X + 3 * s, r.Y + 5 * s),
                new PointF(r.X + 9 * s, r.Y + 5 * s),
                new PointF(r.X + 11 * s, r.Y + 7 * s),
                new PointF(r.X + 21 * s, r.Y + 7 * s),
                new PointF(r.X + 21 * s, r.Y + 19 * s),
                new PointF(r.X + 3 * s, r.Y + 19 * s)
            });
        }

        /// <summary>复制（viewBox 24: rect 9,9,11,11 rx2 + M5 15V6a2 2 0 0 1 2-2h9）。</summary>
        public static void DrawCopyIcon(Graphics g, Rectangle r, Color color)
        {
            float s = r.Width / 24f;
            using var pen = new Pen(color, 2f * s)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            g.DrawRectangle(pen, r.X + 9 * s, r.Y + 9 * s, 11 * s, 11 * s);
            g.DrawLines(pen, new[]
            {
                new PointF(r.X + 5 * s, r.Y + 15 * s),
                new PointF(r.X + 5 * s, r.Y + 6 * s),
                new PointF(r.X + 14 * s, r.Y + 6 * s)
            });
        }

        /// <summary>电源（托盘菜单"退出"，viewBox 24: M12 4v8 + 弧 M7 7a7 7 0 1 0 10 0）。</summary>
        public static void DrawPowerIcon(Graphics g, Rectangle r, Color color)
        {
            float s = r.Width / 24f;
            using var pen = new Pen(color, 2f * s)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawLine(pen, r.X + 12 * s, r.Y + 4 * s, r.X + 12 * s, r.Y + 12 * s);
            // 圆弧：圆心 (12,14) 半径 7，从右上经底部到左上（缺顶部分口）
            g.DrawArc(pen, r.X + 5 * s, r.Y + 7 * s, 14 * s, 14 * s, 306, 288);
        }

        /// <summary>标题栏最小化图标（10px, viewBox 10: rect 1,8,8,1.5）。</summary>
        public static void DrawMinimizeIcon(Graphics g, Rectangle r, Color color)
        {
            float s = r.Width / 10f;
            using var pen = new Pen(color, 1.5f * s);
            g.DrawLine(pen, r.X + 1 * s, r.Y + 8.75f * s, r.X + 9 * s, r.Y + 8.75f * s);
        }

        /// <summary>标题栏最大化图标（10px, viewBox 10: rect 1,1,8,8 stroke 1.2）。</summary>
        public static void DrawMaximizeIcon(Graphics g, Rectangle r, Color color)
        {
            float s = r.Width / 10f;
            using var pen = new Pen(color, 1.2f * s);
            g.DrawRectangle(pen, r.X + 1.6f * s, r.Y + 1.6f * s, 6.8f * s, 6.8f * s);
        }

        /// <summary>标题栏关闭图标（10px, viewBox 10: M1 1L9 9 M9 1L1 9 stroke 1.2）。</summary>
        public static void DrawCloseIcon(Graphics g, Rectangle r, Color color)
        {
            float s = r.Width / 10f;
            using var pen = new Pen(color, 1.2f * s)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawLine(pen, r.X + 1.4f * s, r.Y + 1.4f * s, r.X + 8.6f * s, r.Y + 8.6f * s);
            g.DrawLine(pen, r.X + 8.6f * s, r.Y + 1.4f * s, r.X + 1.4f * s, r.Y + 8.6f * s);
        }
    }
}
