// Johnny – Startbild mit Widmung (wie im Original, dort für Kater Guido). GPLv3, siehe LICENSE.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace Johnny
{
    /// <summary>Angaben zum Projekt, die im Startbild und im Info-Fenster stehen.</summary>
    public static class AppInfo
    {
        /// <summary>Adresse des eigenen GitHub-Repositorys (leer = wird nicht angezeigt).</summary>
        public const string RepositoryUrl = "https://github.com/PixelMatrix-Error/johnny-simulator-3.0";
        public static string Dedication { get { return L.S("Dedicated to my cat Lulu.", "Gewidmet meiner Katze Lulu."); } }

        public static string RepositoryText
        {
            get { return RepositoryUrl.Replace("https://", "").Replace("http://", "").TrimEnd('/'); }
        }

        static Image lulu;
        /// <summary>Foto von Lulu (in die .exe eingebettet).</summary>
        public static Image Lulu
        {
            get
            {
                if (lulu != null) return lulu;
                try
                {
                    using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("lulu.png"))
                        if (s != null) lulu = new Bitmap(Image.FromStream(s));
                }
                catch (Exception) { }
                return lulu;
            }
        }
    }

    public sealed class SplashForm : Form
    {
        readonly float dpi;
        readonly Timer timer = new Timer();

        public SplashForm(bool autoClose)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            using (Graphics g = CreateGraphics()) dpi = g.DpiX / 96f;
            ClientSize = new Size(S(700), S(440));
            Text = "Johnny";
            Cursor = Cursors.Hand;
            Click += delegate { Close(); };
            KeyDown += delegate { Close(); };
            if (autoClose) { timer.Interval = 7000; timer.Tick += delegate { timer.Stop(); Close(); }; timer.Start(); }
        }

        int S(float px) { return (int)Math.Round(px * dpi); }
        float F(float px) { return px * dpi; }

        /// <summary>Mittig über dem Hauptfenster anzeigen.</summary>
        public void ShowOver(Form owner)
        {
            Rectangle o = owner.Bounds;
            Location = new Point(o.Left + (o.Width - Width) / 2, o.Top + (o.Height - Height) / 2);
            Show(owner);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            Rectangle all = ClientRectangle;
            using (SolidBrush b = new SolidBrush(Color.FromArgb(214, 218, 224))) g.FillRectangle(b, all);
            Rectangle inner = Rectangle.Inflate(all, -S(8), -S(8));
            using (LinearGradientBrush b = new LinearGradientBrush(inner, Color.FromArgb(32, 48, 112), Color.FromArgb(16, 26, 70), LinearGradientMode.Vertical))
                g.FillRectangle(b, inner);
            using (Pen p = new Pen(Color.FromArgb(70, 88, 150), 1f)) g.DrawRectangle(p, inner.X, inner.Y, inner.Width - 1, inner.Height - 1);

            // Foto von Lulu rechts unten, wie Guido im Original
            Image lulu = AppInfo.Lulu;
            if (lulu != null)
            {
                float h = inner.Height - F(14), w = h * lulu.Width / lulu.Height;
                RectangleF dst = new RectangleF(inner.Right - w, inner.Bottom - h, w, h);
                g.SetClip(inner);
                g.DrawImage(lulu, dst);
                g.ResetClip();
            }

            string ui = Font.FontFamily.Name;
            float x = inner.Left + F(32);
            using (Font fTitle = new Font(ui, F(58), FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font fSub = new Font(ui, F(23), FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font fText = new Font(ui, F(15), FontStyle.Regular, GraphicsUnit.Pixel))
            using (Font fSmall = new Font(ui, F(12.5f), FontStyle.Regular, GraphicsUnit.Pixel))
            using (Font fDed = new Font(ui, F(19), FontStyle.Italic | FontStyle.Bold, GraphicsUnit.Pixel))
            {
                Color yellow = Color.FromArgb(255, 211, 77), white = Color.White, soft = Color.FromArgb(190, 202, 235);
                // Schatten unter dem Titel wie im Original
                TextRenderer.DrawText(g, "Johnny", fTitle, new Point((int)x + S(3), inner.Top + S(25)), Color.FromArgb(10, 14, 40), TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, "Johnny", fTitle, new Point((int)x, inner.Top + S(22)), white, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, L.S("Simulation of a simplified\nvon Neumann computer", "Simulation eines einfachen\nvon-Neumann-Rechners"), fSub, new Rectangle((int)x, inner.Top + S(110), S(400), S(70)), yellow, TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);
                TextRenderer.DrawText(g, L.S("Improved edition 2026", "Verbesserte Fassung 2026"), fText, new Point((int)x, inner.Top + S(184)), white, TextFormatFlags.NoPadding);

                int y = inner.Top + S(232);
                string[] lines = {
                    "Original: Peter Dauscher, 2009–2014",
                    L.S("Rewritten in 2026 with Claude (Anthropic)", "Neu geschrieben 2026 mit Claude (Anthropic)"),
                    L.S("Free software · GNU General Public License v3", "Freie Software · GNU General Public License v3") };
                foreach (string l in lines) { TextRenderer.DrawText(g, l, fText, new Point((int)x, y), white, TextFormatFlags.NoPadding); y += S(23); }
                if (AppInfo.RepositoryUrl.Length > 0)
                    TextRenderer.DrawText(g, AppInfo.RepositoryText, fSmall, new Point((int)x, y + S(2)), soft, TextFormatFlags.NoPadding);

                TextRenderer.DrawText(g, AppInfo.Dedication, fDed, new Point((int)x, inner.Bottom - S(78)), yellow, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, L.S("Click or press any key to start", "Klicken oder eine Taste drücken, um zu beginnen"), fSmall, new Point((int)x, inner.Bottom - S(34)), soft, TextFormatFlags.NoPadding);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
