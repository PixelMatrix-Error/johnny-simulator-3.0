// Johnny – Schaltbild des Rechners im Aufbau des Original-Johnny:
// Adressbus oben (blau), Datenbus unten (grün), Arbeitsspeicher links, Steuerwerk in der Mitte,
// Rechenwerk rechts. Alles skaliert mit der Fenstergröße. GPLv3, siehe LICENSE.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Johnny
{
    public sealed class SchematicView : Control
    {
        // ---------------------------------------------------------------- Zustand (vom Hauptfenster gesetzt)
        public Cpu Cpu;
        public Snapshot Prev;                         // Zustand vor dem letzten Schritt: geänderte Werte werden rot
        public int[] PrevRam;
        public readonly HashSet<int> Active = new HashSet<int>();   // zuletzt ausgeführte Mikrobefehle
        public bool HideControlUnit, Bonsai, Running;
        public string RecordingText;                  // null = keine Aufnahme
        public int LastAluOp = -1;

        public event Action<int> MicroOpClicked;
        public event Action<string> DataBusEntered;
        public event Action<string> AddressBusEntered;
        public event Action ControlUnitClicked;

        // ---------------------------------------------------------------- Layout in logischen Einheiten
        const float LW = 1000f, LH = 650f;
        const float Sep1 = 335f, Sep2 = 705f;
        const float AbTop = 34f, AbBot = 78f, DbTop = 590f, DbBot = 636f;
        const float RamL = 40f, RamR = 318f, RamT = 98f, RamB = 548f;
        const float CuL = 345f, CuR = 695f, CuT = 98f, CuB = 580f;

        float sc = 1f, ox, oy, fontScale = -1f, gridScale = -1f;
        Font fHead, fBus, fLabel, fBtn, fValue, fValueBig, fMono, fSmall, fBig;
        readonly List<Hit> hits = new List<Hit>();
        int hover = -1, pressed = -1;
        readonly ToolTip tip = new ToolTip();
        string tipShown;
        readonly string monoFamily;
        readonly TextBox abInput = new TextBox(), dbInput = new TextBox();
        DataGridView grid;

        sealed class Hit { public RectangleF R; public int Op = -1; public string Kind; public string Tip; public bool Enabled = true; }

        // ---------------------------------------------------------------- Farben (wie im Original: Adressen blau, Daten grün)
        static readonly Color CCanvas = Color.FromArgb(234, 236, 240);
        static readonly Color CSep = Color.FromArgb(176, 182, 190);
        static readonly Color CHeadText = Color.FromArgb(120, 127, 136);
        static readonly Color CText = Color.FromArgb(28, 28, 30);
        static readonly Color CMuted = Color.FromArgb(96, 102, 110);
        static readonly Color CBlue = Color.FromArgb(166, 206, 245), CBlueAct = Color.FromArgb(86, 156, 230), CBlueLine = Color.FromArgb(80, 132, 184), CBlueDark = Color.FromArgb(24, 72, 128);
        static readonly Color CGreen = Color.FromArgb(124, 224, 166), CGreenAct = Color.FromArgb(38, 186, 104), CGreenLine = Color.FromArgb(52, 150, 96), CGreenDark = Color.FromArgb(16, 82, 44);
        static readonly Color COrange = Color.FromArgb(248, 196, 140), COrangeAct = Color.FromArgb(238, 146, 48), COrangeLine = Color.FromArgb(196, 122, 40);
        static readonly Color CChanged = Color.FromArgb(200, 30, 20);
        static readonly Color COpcode = Color.FromArgb(190, 20, 20), CAddress = Color.FromArgb(20, 70, 190);
        static readonly Color CNext = Color.FromArgb(255, 241, 168), CNextBorder = Color.FromArgb(190, 150, 30), CArrow = Color.FromArgb(255, 204, 0);
        static readonly Color CBtn = Color.FromArgb(250, 250, 250), CBtnBorder = Color.FromArgb(150, 150, 150);
        static readonly Color CBtnHot = Color.FromArgb(229, 241, 251), CBtnPressed = Color.FromArgb(204, 228, 247), CAccent = Color.FromArgb(0, 99, 177);

        public SchematicView(string monoFamily)
        {
            this.monoFamily = monoFamily;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = CCanvas;
            tip.InitialDelay = 500; tip.ReshowDelay = 200; tip.AutoPopDelay = 12000;
            foreach (TextBox tb in new[] { abInput, dbInput })
            {
                tb.BorderStyle = BorderStyle.FixedSingle; tb.TextAlign = HorizontalAlignment.Right; tb.Text = "0";
                Controls.Add(tb);
            }
            ApplyLanguage();
            abInput.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; if (AddressBusEntered != null) AddressBusEntered(abInput.Text); } };
            dbInput.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; if (DataBusEntered != null) DataBusEntered(dbInput.Text); } };
        }

        /// <summary>Die Speichertabelle wird in den Arbeitsspeicher-Kasten eingebettet.</summary>
        public void AttachGrid(DataGridView g) { grid = g; Controls.Add(g); PerformLayout(); }

        /// <summary>Texte, die nicht beim Zeichnen entstehen, an die Sprache anpassen.</summary>
        public void ApplyLanguage()
        {
            tip.SetToolTip(abInput, L.S("Enter an address by hand (0 to 999), then press Enter or click the arrow.", "Adresse von Hand eingeben (0 bis 999), dann Enter oder den Pfeil anklicken."));
            tip.SetToolTip(dbInput, L.S("Enter a value by hand (0 to 19999 or e.g. TAKE 10), then press Enter or click the arrow.", "Wert von Hand eingeben (0 bis 19999 oder z. B. TAKE 10), dann Enter oder den Pfeil anklicken."));
            tipShown = null;
            Invalidate();
        }

        public void SetInputsEnabled(bool on) { abInput.Enabled = dbInput.Enabled = on; }

        /// <summary>Schrift der Speichertabelle (wird mit dem Schaltbild skaliert).</summary>
        public Font GridFont { get { return grid != null ? grid.DefaultCellStyle.Font : Font; } }

        // ---------------------------------------------------------------- Hilfsfunktionen
        RectangleF D(float x, float y, float w, float h) { return new RectangleF(ox + x * sc, oy + y * sc, w * sc, h * sc); }
        Rectangle Px(RectangleF r) { return Rectangle.Round(r); }
        float X(float x) { return ox + x * sc; }
        float Y(float y) { return oy + y * sc; }

        void ComputeScale()
        {
            sc = Math.Min(ClientSize.Width / LW, ClientSize.Height / LH);
            if (sc < 0.45f) sc = 0.45f;
            ox = (ClientSize.Width - LW * sc) / 2f;
            oy = (ClientSize.Height - LH * sc) / 2f;
        }

        void EnsureFonts()
        {
            if (Math.Abs(fontScale - sc) < 0.001f) return;
            fontScale = sc;
            Font[] old = { fHead, fBus, fLabel, fBtn, fValue, fValueBig, fMono, fSmall, fBig };
            foreach (Font f in old) if (f != null) f.Dispose();
            string ui = Font.FontFamily.Name;
            fHead = new Font(ui, 13f * sc, FontStyle.Bold, GraphicsUnit.Pixel);
            fBus = new Font(ui, 24f * sc, FontStyle.Bold, GraphicsUnit.Pixel);
            fBig = new Font(ui, 30f * sc, FontStyle.Bold, GraphicsUnit.Pixel);
            fLabel = new Font(ui, 12.5f * sc, FontStyle.Regular, GraphicsUnit.Pixel);
            fBtn = new Font(monoFamily, 12.5f * sc, FontStyle.Regular, GraphicsUnit.Pixel);
            fSmall = new Font(ui, 11f * sc, FontStyle.Regular, GraphicsUnit.Pixel);
            fValue = new Font(monoFamily, 21f * sc, FontStyle.Regular, GraphicsUnit.Pixel);
            fValueBig = new Font(monoFamily, 27f * sc, FontStyle.Regular, GraphicsUnit.Pixel);
            fMono = new Font(monoFamily, 13f * sc, FontStyle.Regular, GraphicsUnit.Pixel);
        }

        void Str(Graphics g, string s, Font f, Color c, RectangleF r, TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, s, f, Px(r), c, flags | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }
        const TextFormatFlags Center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
        const TextFormatFlags LeftMid = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;

        static void Rect(Graphics g, Color c, Rectangle r, float width)
        {
            using (Pen p = new Pen(c, width)) { p.Alignment = PenAlignment.Inset; g.DrawRectangle(p, r.X, r.Y, r.Width - 1, r.Height - 1); }
        }
        static void Fill(Graphics g, Color c, Rectangle r) { using (SolidBrush b = new SolidBrush(c)) g.FillRectangle(b, r); }

        bool Changed(Func<Snapshot, int> pick, int now) { return Prev != null && pick(Prev) != now; }
        bool Act(params int[] ops) { foreach (int o in ops) if (Active.Contains(o)) return true; return false; }

        // ---------------------------------------------------------------- Layout der eingebetteten Steuerelemente
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            ComputeScale();
            Font inputFont = new Font(monoFamily, Math.Max(8f, 16f * sc), FontStyle.Regular, GraphicsUnit.Pixel);
            foreach (TextBox tb in new[] { abInput, dbInput }) { Font old = tb.Font; tb.Font = inputFont; }
            abInput.Bounds = Px(D(14, AbTop + 6, 86, 32));
            dbInput.Bounds = Px(D(14, DbTop + 7, 86, 32));
            if (grid != null)
            {
                Rectangle gb = Px(D(RamL + 6, RamT + 34, RamR - RamL - 12, RamB - RamT - 34 - 44));
                grid.Bounds = gb;
                if (Math.Abs(gridScale - sc) > 0.02f)
                {
                    gridScale = sc;
                    int cur = grid.CurrentCell != null ? grid.CurrentCell.RowIndex : 0;
                    int first = grid.FirstDisplayedScrollingRowIndex;
                    Font cf = new Font(monoFamily, Math.Max(8f, 14f * sc), FontStyle.Regular, GraphicsUnit.Pixel);
                    grid.DefaultCellStyle.Font = cf;
                    grid.ColumnHeadersDefaultCellStyle.Font = new Font(Font.FontFamily, Math.Max(8f, 12f * sc), FontStyle.Regular, GraphicsUnit.Pixel);
                    grid.ColumnHeadersHeight = Math.Max(18, (int)(26 * sc));
                    grid.RowHeadersWidth = Math.Max(18, (int)(24 * sc));
                    grid.RowTemplate.Height = Math.Max(16, TextRenderer.MeasureText("0", cf).Height + (int)(6 * sc));
                    grid.RowCount = 0; grid.RowCount = Cpu.RamSize;
                    try
                    {
                        grid.CurrentCell = grid[3, Math.Max(0, cur)];
                        if (first > 0) grid.FirstDisplayedScrollingRowIndex = first;
                    }
                    catch (InvalidOperationException) { }
                }
                int w = grid.ClientSize.Width - grid.RowHeadersWidth - SystemInformation.VerticalScrollBarWidth;
                if (w > 50)
                {
                    grid.Columns[0].Width = (int)(w * 0.20f); grid.Columns[1].Width = (int)(w * 0.16f); grid.Columns[2].Width = (int)(w * 0.20f);
                }
            }
            Invalidate();
        }

        // ---------------------------------------------------------------- Zeichnen
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(CCanvas);
            if (Cpu == null) return;
            ComputeScale();
            EnsureFonts();
            hits.Clear();
            g.SmoothingMode = SmoothingMode.None;
            int next = Running ? -1 : Cpu.NextMicroOp;
            bool cu = !HideControlUnit;

            // Spaltenüberschriften und gestrichelte Trennlinien wie im Original
            using (Pen p = new Pen(CSep, Math.Max(1f, 2f * sc)))
            {
                p.DashStyle = DashStyle.Dash;
                g.DrawLine(p, X(Sep1), Y(6), X(Sep1), Y(LH - 4));
                g.DrawLine(p, X(Sep2), Y(6), X(Sep2), Y(LH - 4));
            }
            Str(g, L.S("MEMORY", "ARBEITSSPEICHER"), fHead, CHeadText, D(0, 4, Sep1, 24), Center);
            Str(g, L.S("CONTROL UNIT", "STEUERWERK"), fHead, CHeadText, D(Sep1, 4, Sep2 - Sep1, 24), Center);
            Str(g, L.S("ARITHMETIC LOGIC UNIT", "RECHENWERK"), fHead, CHeadText, D(Sep2, 4, LW - Sep2, 24), Center);

            DrawBuses(g, cu);
            DrawRam(g);
            if (cu) DrawControlUnit(g, next); else DrawHiddenControlUnit(g);
            DrawAlu(g, cu);

            if (RecordingText != null)
            {
                RectangleF rr = D(CuL + 6, CuT - 2, CuR - CuL - 12, 22);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush b = new SolidBrush(CChanged)) g.FillEllipse(b, rr.X, rr.Y + rr.Height / 2f - 5 * sc, 10 * sc, 10 * sc);
                g.SmoothingMode = SmoothingMode.None;
                Str(g, RecordingText, fSmall, CChanged, new RectangleF(rr.X + 16 * sc, rr.Y, rr.Width - 16 * sc, rr.Height), LeftMid | TextFormatFlags.EndEllipsis);
            }

            for (int i = 0; i < hits.Count; i++) if (hits[i].Kind == "op") DrawButton(g, hits[i], i, next);
        }

        // ---------------------------------------------------------------- Pfeile
        void Poly(Graphics g, PointF[] pts, Color fill, Color line, bool active)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush b = new SolidBrush(fill)) g.FillPolygon(b, pts);
            using (Pen p = new Pen(line, active ? Math.Max(1.5f, 2.2f * sc) : Math.Max(1f, 1.1f * sc))) { p.LineJoin = LineJoin.Miter; g.DrawPolygon(p, pts); }
            g.SmoothingMode = SmoothingMode.None;
        }

        /// <summary>Senkrechter Blockpfeil von y1 nach y2 (Spitze bei y2).</summary>
        void ArrowV(Graphics g, float cx, float y1, float y2, float shaft, Color fill, Color line, bool active)
        {
            float dir = y2 > y1 ? 1 : -1, hl = Math.Min(16f, Math.Abs(y2 - y1) * 0.5f), hw = shaft * 0.95f, sw = shaft / 2f;
            float yb = y2 - dir * hl;
            PointF[] pts = {
                new PointF(X(cx - sw), Y(y1)), new PointF(X(cx + sw), Y(y1)), new PointF(X(cx + sw), Y(yb)), new PointF(X(cx + hw), Y(yb)),
                new PointF(X(cx), Y(y2)), new PointF(X(cx - hw), Y(yb)), new PointF(X(cx - sw), Y(yb)) };
            Poly(g, pts, fill, line, active);
        }

        /// <summary>Waagerechter Blockpfeil von x1 nach x2 (Spitze bei x2).</summary>
        void ArrowH(Graphics g, float cy, float x1, float x2, float shaft, Color fill, Color line, bool active)
        {
            float dir = x2 > x1 ? 1 : -1, hl = Math.Min(16f, Math.Abs(x2 - x1) * 0.5f), hw = shaft * 0.95f, sw = shaft / 2f;
            float xb = x2 - dir * hl;
            PointF[] pts = {
                new PointF(X(x1), Y(cy - sw)), new PointF(X(xb), Y(cy - sw)), new PointF(X(xb), Y(cy - hw)), new PointF(X(x2), Y(cy)),
                new PointF(X(xb), Y(cy + hw)), new PointF(X(xb), Y(cy + sw)), new PointF(X(x1), Y(cy + sw)) };
            Poly(g, pts, fill, line, active);
        }

        /// <summary>Winkelpfeil: von unten (Bus) nach oben bis cy, dann nach rechts bis x2.</summary>
        void ArrowUpRight(Graphics g, float cx, float yBottom, float cy, float x2, float shaft, Color fill, Color line, bool active)
        {
            float sw = shaft / 2f, hw = shaft * 0.95f, hl = 16f, xb = x2 - hl;
            PointF[] pts = {
                new PointF(X(cx - sw), Y(yBottom)), new PointF(X(cx - sw), Y(cy - sw)), new PointF(X(xb), Y(cy - sw)), new PointF(X(xb), Y(cy - hw)),
                new PointF(X(x2), Y(cy)), new PointF(X(xb), Y(cy + hw)), new PointF(X(xb), Y(cy + sw)), new PointF(X(cx + sw), Y(cy + sw)),
                new PointF(X(cx + sw), Y(yBottom)) };
            Poly(g, pts, fill, line, active);
        }

        // ---------------------------------------------------------------- Busse
        void DrawBuses(Graphics g, bool cu)
        {
            bool abA = Act(1, 2, 4, 8), dbA = Act(1, 2, 3, 13, 14, 15, 18);
            // Adressbus
            Rectangle ab = Px(D(150, AbTop, CuR - 150, AbBot - AbTop));
            Fill(g, abA ? CBlueAct : CBlue, ab); Rect(g, abA ? CBlueDark : CBlueLine, ab, abA ? Math.Max(1.5f, 2f * sc) : 1f);
            Str(g, L.S("Address Bus", "Adressbus"), fBus, CBlueDark, D(166, AbTop, 200, AbBot - AbTop), LeftMid);
            RectangleF abv = D(400, AbTop + 6, 76, AbBot - AbTop - 12);
            ValueBox(g, abv, Cpu.Ab.ToString("000"), null, Changed(s => s.Ab, Cpu.Ab), fValue);
            ArrowH(g, (AbTop + AbBot) / 2f, 102, 150, 22, CBlue, CBlueLine, false);
            AddHit(D(102, AbTop + 4, 48, AbBot - AbTop - 8), -1, "abpush", L.S("Put the entered address on the address bus", "Eingegebene Adresse auf den Adressbus legen"), !Running);

            // Datenbus
            Rectangle db = Px(D(150, DbTop, LW - 12 - 150, DbBot - DbTop));
            Fill(g, dbA ? CGreenAct : CGreen, db); Rect(g, dbA ? CGreenDark : CGreenLine, db, dbA ? Math.Max(1.5f, 2f * sc) : 1f);
            Str(g, L.S("Data Bus", "Datenbus"), fBus, CGreenDark, D(166, DbTop, 200, DbBot - DbTop), LeftMid);
            RectangleF dbv = D(400, DbTop + 6, 124, DbBot - DbTop - 12);
            ValueBox(g, dbv, Cpu.Hi(Cpu.Db).ToString("00"), Cpu.Lo(Cpu.Db).ToString("000"), Changed(s => s.Db, Cpu.Db), fValue);
            string dt = Cpu.CellText(Cpu.Db);
            Str(g, "= " + Cpu.Db + (dt.Length > 0 ? "   (" + dt + ")" : ""), fLabel, CGreenDark, D(536, DbTop, 300, DbBot - DbTop), LeftMid);
            ArrowH(g, (DbTop + DbBot) / 2f, 102, 150, 22, CGreen, CGreenLine, false);
            AddHit(D(102, DbTop + 4, 48, DbBot - DbTop - 8), -1, "dbpush", L.S("Put the entered value on the data bus", "Eingegebenen Wert auf den Datenbus legen"), !Running);
        }

        void ValueBox(Graphics g, RectangleF rf, string a, string b, bool changed, Font f)
        {
            Rectangle r = Px(rf);
            Fill(g, Color.White, r);
            Rect(g, Color.FromArgb(70, 70, 70), r, 1f);
            Color tc = changed ? CChanged : CText;
            if (b == null) { Str(g, a, f, tc, rf, Center); return; }
            float hw = rf.Width * 0.38f;
            Str(g, a, f, tc, new RectangleF(rf.X, rf.Y, hw, rf.Height), Center);
            Str(g, b, f, tc, new RectangleF(rf.X + hw, rf.Y, rf.Width - hw, rf.Height), Center);
        }

        // ---------------------------------------------------------------- Arbeitsspeicher
        void DrawRam(Graphics g)
        {
            bool ramA = Act(1, 2);
            // Adresse vom Bus in den Speicher
            ArrowV(g, 190, AbBot, RamT, 26, ramA ? CBlueAct : CBlue, ramA ? CBlueDark : CBlueLine, ramA);
            Rectangle box = Px(D(RamL, RamT, RamR - RamL, RamB - RamT));
            Fill(g, Color.FromArgb(214, 218, 223), box);
            Rect(g, Color.FromArgb(110, 116, 124), box, 1f);
            Str(g, "RAM", fHead, CMuted, D(RamL + 8, RamT, 60, 34), LeftMid);
            ValueBox(g, D(RamL + 100, RamT + 4, 78, 26), Cpu.Ab.ToString("000"), null, Changed(s => s.Ab, Cpu.Ab), fMono);
            Str(g, L.S("address", "Adresse"), fSmall, CMuted, D(RamL + 186, RamT, 80, 34), LeftMid);
            // Inhalt der adressierten Zelle
            int v = Cpu.Ram[Cpu.Ab];
            bool ch = PrevRam != null && PrevRam[Cpu.Ab] != v;
            ValueBox(g, D(RamL + 70, RamB - 38, 130, 32), Cpu.Hi(v).ToString("00"), Cpu.Lo(v).ToString("000"), ch, fValue);
            Str(g, L.S("content", "Inhalt"), fSmall, CMuted, D(RamL + 206, RamB - 38, 70, 32), LeftMid);

            // db->ram (hoch) und ram->db (runter)
            ArrowV(g, 120, DbTop, RamB, 30, Act(1) ? CGreenAct : CGreen, Act(1) ? CGreenDark : CGreenLine, Act(1));
            ArrowV(g, 236, RamB, DbTop, 30, Act(2) ? CGreenAct : CGreen, Act(2) ? CGreenDark : CGreenLine, Act(2));
            OpButton(120 - 37, 557, 74, 1);
            OpButton(236 - 37, 557, 74, 2);
        }

        // ---------------------------------------------------------------- Steuerwerk
        void DrawHiddenControlUnit(Graphics g)
        {
            RectangleF rf = D(CuL + 8, CuT, CuR - CuL - 16, CuB - CuT - 4);
            Rectangle r = Px(rf);
            Fill(g, Color.FromArgb(206, 210, 216), r);
            Rect(g, Color.FromArgb(150, 156, 164), r, 1f);
            Str(g, L.S("Control Unit", "Steuerwerk"), fBig, Color.FromArgb(128, 134, 142), new RectangleF(rf.X, rf.Y, rf.Width, rf.Height - 50 * sc), Center);
            Str(g, L.S("hidden – click here to show it", "ausgeblendet – zum Einblenden hier klicken"), fLabel, Color.FromArgb(110, 116, 124), new RectangleF(rf.X, rf.Y + rf.Height / 2f, rf.Width, 40 * sc), Center);
            AddHit(rf, -1, "cu", L.S("Show control unit", "Steuerwerk einblenden"), true);
        }

        void DrawControlUnit(Graphics g, int next)
        {
            // Pfeile zum Adressbus
            ArrowV(g, 452, 200, AbBot, 26, Act(4) ? CBlueAct : CBlue, Act(4) ? CBlueDark : CBlueLine, Act(4));
            ArrowV(g, 640, 200, AbBot, 26, Act(8) ? CBlueAct : CBlue, Act(8) ? CBlueDark : CBlueLine, Act(8));
            OpButton(452 - 37, 112, 74, 4);
            OpButton(640 - 37, 112, 74, 8);
            // ins->pc
            ArrowH(g, 220, 556, 598, 20, Act(11) ? CBlueAct : CBlue, Act(11) ? CBlueDark : CBlueLine, Act(11));
            OpButton(540, 160, 74, 11);
            // db->ins: vom Datenbus hoch und nach rechts ins Befehlsregister
            ArrowUpRight(g, 368, DbTop, 220, 420, 26, Act(3) ? CGreenAct : CGreen, Act(3) ? CGreenDark : CGreenLine, Act(3));
            OpButton(368 - 34, 452, 68, 3);
            // ins->mc
            ArrowV(g, 452, 240, 300, 24, Act(5) ? COrangeAct : COrange, COrangeLine, Act(5));
            OpButton(452 - 37, 258, 74, 5);

            // Befehlsregister
            bool insCh = Prev != null && (Prev.InsOp != Cpu.InsOp || Prev.InsAd != Cpu.InsAd);
            RectangleF ir = D(420, 200, 136, 40);
            Rectangle irr = Px(ir);
            Fill(g, Color.White, irr); Rect(g, Act(3, 4, 5, 11) ? CAccent : Color.FromArgb(70, 70, 70), irr, Act(3, 4, 5, 11) ? Math.Max(1.5f, 2f * sc) : 1f);
            float hw = ir.Width * 0.38f;
            Str(g, Cpu.InsOp.ToString("00"), fValue, insCh ? CChanged : COpcode, new RectangleF(ir.X, ir.Y, hw, ir.Height), Center);
            Str(g, Cpu.InsAd.ToString("000"), fValue, insCh ? CChanged : CAddress, new RectangleF(ir.X + hw, ir.Y, ir.Width - hw, ir.Height), Center);
            Str(g, L.S("Instruction reg.", "Befehlsregister"), fSmall, CMuted, D(494, 242, 100, 18), LeftMid);
            Str(g, Cpu.InsOp > 0 ? Cpu.CellText(Cpu.InsOp * 1000 + Cpu.InsAd) : "", fMono, CText, D(494, 260, 100, 20), LeftMid);

            // Programmzähler
            RectangleF pc = D(600, 200, 84, 40);
            Rectangle pcr = Px(pc);
            Fill(g, Color.White, pcr); Rect(g, Act(8, 9, 10, 11) ? CAccent : Color.FromArgb(70, 70, 70), pcr, Act(8, 9, 10, 11) ? Math.Max(1.5f, 2f * sc) : 1f);
            Str(g, Cpu.Pc.ToString("000"), fValue, Changed(s => s.Pc, Cpu.Pc) ? CChanged : CText, pc, Center);
            Str(g, L.S("Program counter", "Programmzähler"), fSmall, CMuted, D(596, 242, 100, 18), LeftMid);
            OpButton(606, 266, 84, 10);
            OpButton(606, 296, 84, 9);

            // Mikroprogramm: Zähler, Speicher, nächster Mikrobefehl
            RectangleF mb = D(404, 300, 196, 270);
            Rectangle mbr = Px(mb);
            Fill(g, Color.FromArgb(214, 218, 223), mbr); Rect(g, Color.FromArgb(110, 116, 124), mbr, 1f);
            bool mpCh = Prev != null && (Prev.MOp != Cpu.MOp || Prev.MMc != Cpu.MMc);
            RectangleF mpc = D(412, 306, 92, 30);
            Rectangle mpcr = Px(mpc);
            Fill(g, Color.White, mpcr); Rect(g, Act(5, 6, 7, 19) ? CAccent : Color.FromArgb(70, 70, 70), mpcr, Act(5, 6, 7, 19) ? Math.Max(1.5f, 2f * sc) : 1f);
            Str(g, Cpu.MOp.ToString("00") + "  " + Cpu.MMc, fValue, mpCh ? CChanged : CText, mpc, Center);
            Str(g, L.S("Micro program\ncounter", "Mikroprogramm-\nzähler"), fSmall, CMuted, D(510, 304, 90, 34), TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            RectangleF list = D(412, 342, 180, 186);
            Fill(g, Color.White, Px(list)); Rect(g, Color.FromArgb(150, 150, 150), Px(list), 1f);
            float rowH = 18.4f;
            for (int k = 0; k < 10; k++)
            {
                int addr = Cpu.MOp * 10 + k, n = Cpu.Mpm[addr];
                RectangleF row = D(413, 343 + k * rowH, 178, rowH);
                bool cur = k == Cpu.MMc;
                if (cur) Fill(g, CNext, Px(row));
                string a = addr.ToString("000") + (k == 0 && Cpu.MOp == 0 ? ":fetch" : "");
                Str(g, a, fMono, CMuted, new RectangleF(row.X + 16 * sc, row.Y, 70 * sc, row.Height), LeftMid);
                Str(g, Micro.Ops[n].Name, fMono, n == 0 ? Color.FromArgb(160, 160, 160) : CText, new RectangleF(row.X + 88 * sc, row.Y, row.Width - 90 * sc, row.Height), LeftMid);
                if (cur) SmallArrow(g, row.X + 4 * sc, row.Y + row.Height / 2f, 5f * sc);
            }
            string nx = next >= 0 ? Micro.Ops[next].Name : "–";
            Str(g, Running ? "" : L.S("next: ", "nächster: ") + nx, fMono, CText, D(412, 532, 180, 32), Center);
            OpButton(608, 400, 82, 6);
            OpButton(608, 430, 82, 7);
            OpButton(608, 540, 82, 19);
        }

        void SmallArrow(Graphics g, float x, float cy, float s)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            PointF[] a = { new PointF(x + s * 1.3f, cy), new PointF(x, cy - s), new PointF(x, cy + s) };
            using (SolidBrush b = new SolidBrush(CArrow)) g.FillPolygon(b, a);
            using (Pen p = new Pen(Color.FromArgb(128, 96, 0), Math.Max(1f, 0.8f * sc))) g.DrawPolygon(p, a);
            g.SmoothingMode = SmoothingMode.None;
        }

        // ---------------------------------------------------------------- Rechenwerk
        void DrawAlu(Graphics g, bool cu)
        {
            OpButton(872, 98, 100, 12);
            OpButton(872, 132, 100, 16);
            OpButton(872, 166, 100, 17);

            // Akkumulator
            bool accA = Act(10, 12, 13, 14, 15, 16, 17, 18);
            Str(g, L.S("Accumulator", "Akkumulator"), fLabel, CText, D(850, 250, 136, 22), LeftMid);
            RectangleF acc = D(850, 274, 136, 70);
            Rectangle ar = Px(acc);
            Fill(g, Color.White, ar); Rect(g, accA ? CAccent : Color.FromArgb(70, 70, 70), ar, accA ? Math.Max(2f, 2.5f * sc) : 1.2f);
            bool ach = Changed(s => s.Acc, Cpu.Acc);
            float hw = acc.Width * 0.38f;
            Str(g, Cpu.Hi(Cpu.Acc).ToString("00"), fValueBig, ach ? CChanged : CText, new RectangleF(acc.X, acc.Y, hw, acc.Height), Center);
            Str(g, Cpu.Lo(Cpu.Acc).ToString("000"), fValueBig, ach ? CChanged : CText, new RectangleF(acc.X + hw, acc.Y, acc.Width - hw, acc.Height), Center);
            Str(g, "= " + Cpu.Acc, fSmall, CMuted, D(850, 348, 60, 18), LeftMid);

            // "= 0 ?"-Lampe, im Original gelb
            bool zero = Cpu.Acc == 0;
            Str(g, "= 0 ?", fLabel, CText, D(748, 236, 60, 20), Center);
            RectangleF lamp = D(760, 260, 36, 28);
            Rectangle lr = Px(lamp);
            Fill(g, zero ? Color.FromArgb(255, 230, 0) : Color.FromArgb(196, 198, 200), lr);
            Rect(g, Color.FromArgb(50, 50, 50), lr, Math.Max(1.5f, 2f * sc));
            if (cu)
            {
                bool la = Act(10);
                using (Pen p = new Pen(la ? CAccent : Color.FromArgb(90, 90, 90), la ? Math.Max(1.5f, 2f * sc) : Math.Max(1f, 1.2f * sc)))
                    g.DrawLine(p, X(760), Y(274), X(698), Y(274));
                g.SmoothingMode = SmoothingMode.AntiAlias;
                PointF[] a = { new PointF(X(691), Y(278)), new PointF(X(700), Y(272.5f)), new PointF(X(700), Y(283.5f)) };
                using (SolidBrush b = new SolidBrush(la ? CAccent : Color.FromArgb(90, 90, 90))) g.FillPolygon(b, new[] { new PointF(X(691), Y(274)), new PointF(X(700), Y(269)), new PointF(X(700), Y(279)) });
                g.SmoothingMode = SmoothingMode.None;
            }

            // db->acc (mit plus/minus) und acc->db
            bool inA = Act(13, 14, 18);
            ArrowUpRight(g, 810, DbTop, 309, 850, 28, inA ? CGreenAct : CGreen, inA ? CGreenDark : CGreenLine, inA);
            ArrowV(g, 930, 344, DbTop, 28, Act(15) ? CGreenAct : CGreen, Act(15) ? CGreenDark : CGreenLine, Act(15));
            OpButton(810 - 38, 410, 76, 13);
            OpButton(810 - 38, 442, 76, 14);
            OpButton(810 - 38, 490, 76, 18);
            OpButton(930 - 38, 490, 76, 15);
        }

        // ---------------------------------------------------------------- Knöpfe
        void AddHit(RectangleF r, int op, string kind, string tipText, bool enabled)
        {
            Hit h = new Hit(); h.R = r; h.Op = op; h.Kind = kind; h.Enabled = enabled;
            h.Tip = tipText ?? (op >= 0 ? Micro.Ops[op].Name + ": " + Micro.Ops[op].Description : null);
            hits.Add(h);
        }

        void OpButton(float x, float y, float w, int op)
        {
            if (Bonsai && (op == 12 || op == 13 || op == 14)) return;
            AddHit(D(x, y, w, 26), op, "op", null, !Running);
        }

        void DrawButton(Graphics g, Hit h, int index, int next)
        {
            Rectangle r = Px(h.R);
            bool isNext = h.Op == next && next > 0;
            bool hot = index == hover && h.Enabled, down = index == pressed && h.Enabled;
            Color fill = !h.Enabled ? Color.FromArgb(238, 238, 238) : down ? CBtnPressed : hot ? CBtnHot : isNext ? CNext : CBtn;
            Color border = !h.Enabled ? Color.FromArgb(200, 200, 200) : (hot || down) ? CAccent : isNext ? CNextBorder : CBtnBorder;
            Fill(g, fill, r);
            Rect(g, border, r, 1);
            if (Active.Contains(h.Op) && !Running) Rect(g, CAccent, Rectangle.Inflate(r, -1, -1), 1);
            RectangleF tr = h.R;
            if (isNext) { tr.X += 7 * sc; tr.Width -= 7 * sc; }
            Str(g, Micro.Ops[h.Op].Name, fBtn, h.Enabled ? CText : Color.FromArgb(160, 160, 160), tr, Center);
            if (isNext) SmallArrow(g, h.R.Left + 4 * sc, h.R.Top + h.R.Height / 2f, 5.5f * sc);
        }

        // ---------------------------------------------------------------- Maus
        int HitIndex(Point p)
        {
            for (int i = hits.Count - 1; i >= 0; i--) if (hits[i].R.Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = HitIndex(e.Location);
            if (i != hover) { hover = i; Invalidate(); }
            Cursor = (i >= 0 && hits[i].Enabled) ? Cursors.Hand : Cursors.Default;
            string t = i >= 0 ? hits[i].Tip : null;
            if (t != tipShown) { tipShown = t; tip.SetToolTip(this, t ?? ""); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hover != -1 || pressed != -1) { hover = -1; pressed = -1; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            pressed = HitIndex(e.Location);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            int i = HitIndex(e.Location);
            int p = pressed; pressed = -1;
            Invalidate();
            if (i < 0 || i != p || !hits[i].Enabled) return;
            Hit h = hits[i];
            if (h.Kind == "op" && MicroOpClicked != null) MicroOpClicked(h.Op);
            else if (h.Kind == "dbpush" && DataBusEntered != null) DataBusEntered(dbInput.Text);
            else if (h.Kind == "abpush" && AddressBusEntered != null) AddressBusEntered(abInput.Text);
            else if (h.Kind == "cu" && ControlUnitClicked != null) ControlUnitClicked();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tip.Dispose();
                Font[] all = { fHead, fBus, fLabel, fBtn, fValue, fValueBig, fMono, fSmall, fBig };
                foreach (Font f in all) if (f != null) f.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
