// Johnny – Symbole für Menü und Symbolleiste.
// Unter Windows 10/11 werden die Systemsymbole (Segoe Fluent Icons / Segoe MDL2 Assets) verwendet,
// sonst einfache gezeichnete Formen. GPLv3, siehe LICENSE.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Johnny
{
    public static class Icons
    {
        static readonly string glyphFamily = FindFamily("Segoe Fluent Icons", "Segoe MDL2 Assets");

        static string FindFamily(params string[] names)
        {
            foreach (string n in names)
            {
                try { using (Font f = new Font(n, 10f)) if (string.Equals(f.Name, n, StringComparison.OrdinalIgnoreCase)) return n; }
                catch (ArgumentException) { }
            }
            return null;
        }

        public static readonly Color Neutral = Color.FromArgb(50, 50, 50);
        public static readonly Color Green = Color.FromArgb(56, 138, 52);
        public static readonly Color Blue = Color.FromArgb(0, 99, 177);
        public static readonly Color Red = Color.FromArgb(196, 43, 28);

        /// <summary>Erzeugt ein Symbol der Größe size×size.</summary>
        public static Bitmap Get(string key, int size)
        {
            Color c = Neutral;
            char glyph = '\0';
            switch (key)
            {
                case "new": glyph = ''; break;
                case "open": glyph = ''; break;
                case "save": glyph = ''; break;
                case "undo": glyph = ''; break;
                case "reset": glyph = ''; break;
                case "micro": glyph = ''; c = Blue; break;
                case "macro": glyph = ''; c = Blue; break;
                case "run": glyph = ''; c = Green; break;
                case "pause": glyph = ''; c = Blue; break;
                case "help": glyph = ''; break;
                case "code": glyph = ''; break;
                case "record": c = Red; break;
                case "breakpoint": c = Red; break;
            }
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                if (glyph != '\0' && glyphFamily != null)
                {
                    using (Font f = new Font(glyphFamily, size * 0.78f, GraphicsUnit.Pixel))
                    using (SolidBrush b = new SolidBrush(c))
                    using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic))
                    {
                        sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
                        g.DrawString(glyph.ToString(), f, b, new RectangleF(0, 0, size, size + size * 0.04f), sf);
                    }
                }
                else DrawFallback(g, key, size, c);
            }
            return bmp;
        }

        static void DrawFallback(Graphics g, string key, int s, Color c)
        {
            float u = s / 16f;
            using (SolidBrush b = new SolidBrush(c))
            using (Pen p = new Pen(c, Math.Max(1f, 1.3f * u)))
            {
                switch (key)
                {
                    case "run": g.FillPolygon(b, new[] { new PointF(4 * u, 2 * u), new PointF(13 * u, 8 * u), new PointF(4 * u, 14 * u) }); break;
                    case "pause": g.FillRectangle(b, 4 * u, 3 * u, 3 * u, 10 * u); g.FillRectangle(b, 9 * u, 3 * u, 3 * u, 10 * u); break;
                    case "micro": g.DrawLines(p, new[] { new PointF(6 * u, 3 * u), new PointF(11 * u, 8 * u), new PointF(6 * u, 13 * u) }); break;
                    case "macro": g.FillPolygon(b, new[] { new PointF(3 * u, 3 * u), new PointF(10 * u, 8 * u), new PointF(3 * u, 13 * u) }); g.FillRectangle(b, 11 * u, 3 * u, 2 * u, 10 * u); break;
                    case "reset": g.DrawArc(p, 3 * u, 3 * u, 10 * u, 10 * u, 300, 300); g.FillPolygon(b, new[] { new PointF(11 * u, 1 * u), new PointF(14 * u, 5 * u), new PointF(9.5f * u, 5.5f * u) }); break;
                    case "undo": g.DrawArc(p, 4 * u, 4 * u, 9 * u, 8 * u, 180, 200); g.FillPolygon(b, new[] { new PointF(1.5f * u, 8 * u), new PointF(6.5f * u, 8 * u), new PointF(4 * u, 4 * u) }); break;
                    case "new": g.DrawPolygon(p, new[] { new PointF(4 * u, 2 * u), new PointF(10 * u, 2 * u), new PointF(13 * u, 5 * u), new PointF(13 * u, 14 * u), new PointF(4 * u, 14 * u) }); break;
                    case "open": g.DrawPolygon(p, new[] { new PointF(2 * u, 4 * u), new PointF(6 * u, 4 * u), new PointF(7.5f * u, 5.5f * u), new PointF(14 * u, 5.5f * u), new PointF(14 * u, 13 * u), new PointF(2 * u, 13 * u) }); break;
                    case "save": g.DrawRectangle(p, 3 * u, 3 * u, 10 * u, 10 * u); g.FillRectangle(b, 5.5f * u, 3 * u, 5 * u, 3.5f * u); break;
                    case "help": using (Font f = new Font(FontFamily.GenericSansSerif, 11 * u, FontStyle.Bold, GraphicsUnit.Pixel)) g.DrawString("?", f, b, 3.5f * u, 0.5f * u); break;
                    case "code": g.DrawLines(p, new[] { new PointF(6 * u, 4 * u), new PointF(2 * u, 8 * u), new PointF(6 * u, 12 * u) }); g.DrawLines(p, new[] { new PointF(10 * u, 4 * u), new PointF(14 * u, 8 * u), new PointF(10 * u, 12 * u) }); break;
                    case "record":
                    case "breakpoint": g.FillEllipse(b, 3 * u, 3 * u, 10 * u, 10 * u); break;
                }
            }
        }

        /// <summary>Programmsymbol (Prozessor mit „J“), wird auch für die .ico-Datei verwendet.</summary>
        public static Bitmap AppIcon(int size)
        {
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                float u = size / 32f;
                Color body = Color.FromArgb(31, 78, 140), pin = Color.FromArgb(120, 130, 145);
                using (SolidBrush pb = new SolidBrush(pin))
                    for (int i = 0; i < 4; i++)
                    {
                        float o = (7.5f + i * 5f) * u;
                        g.FillRectangle(pb, o, 1 * u, 2 * u, 5 * u);
                        g.FillRectangle(pb, o, 26 * u, 2 * u, 5 * u);
                        g.FillRectangle(pb, 1 * u, o, 5 * u, 2 * u);
                        g.FillRectangle(pb, 26 * u, o, 5 * u, 2 * u);
                    }
                using (GraphicsPath path = RoundRect(new RectangleF(5 * u, 5 * u, 22 * u, 22 * u), 3 * u))
                using (SolidBrush bb = new SolidBrush(body))
                    g.FillPath(bb, path);
                using (Font f = new Font("Segoe UI", 15 * u, FontStyle.Bold, GraphicsUnit.Pixel))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
                    g.DrawString("J", f, Brushes.White, new RectangleF(5 * u, 5.5f * u, 22 * u, 22 * u), sf);
                }
            }
            return bmp;
        }

        static GraphicsPath RoundRect(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
