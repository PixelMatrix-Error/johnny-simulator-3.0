// Johnny – Hauptfenster. GPLv3, siehe LICENSE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Windows.Forms;

namespace Johnny
{
    /// <summary>Flache, helle Farben für Menü, Symbolleiste und Statusleiste.</summary>
    sealed class FlatColors : ProfessionalColorTable
    {
        static readonly Color Bar = Color.FromArgb(245, 246, 247), Hot = Color.FromArgb(229, 241, 251), HotBorder = Color.FromArgb(204, 228, 247),
            Down = Color.FromArgb(204, 228, 247), Line = Color.FromArgb(218, 220, 224);
        public override Color ToolStripGradientBegin { get { return Bar; } }
        public override Color ToolStripGradientMiddle { get { return Bar; } }
        public override Color ToolStripGradientEnd { get { return Bar; } }
        public override Color ToolStripBorder { get { return Bar; } }
        public override Color MenuStripGradientBegin { get { return Color.White; } }
        public override Color MenuStripGradientEnd { get { return Color.White; } }
        public override Color MenuItemSelected { get { return Hot; } }
        public override Color MenuItemBorder { get { return HotBorder; } }
        public override Color MenuItemSelectedGradientBegin { get { return Hot; } }
        public override Color MenuItemSelectedGradientEnd { get { return Hot; } }
        public override Color MenuItemPressedGradientBegin { get { return Color.FromArgb(240, 240, 240); } }
        public override Color MenuItemPressedGradientMiddle { get { return Color.FromArgb(240, 240, 240); } }
        public override Color MenuItemPressedGradientEnd { get { return Color.FromArgb(240, 240, 240); } }
        public override Color MenuBorder { get { return Color.FromArgb(204, 206, 210); } }
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(252, 252, 252); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(252, 252, 252); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(252, 252, 252); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(252, 252, 252); } }
        public override Color ButtonSelectedHighlight { get { return Hot; } }
        public override Color ButtonSelectedBorder { get { return HotBorder; } }
        public override Color ButtonSelectedGradientBegin { get { return Hot; } }
        public override Color ButtonSelectedGradientMiddle { get { return Hot; } }
        public override Color ButtonSelectedGradientEnd { get { return Hot; } }
        public override Color ButtonPressedGradientBegin { get { return Down; } }
        public override Color ButtonPressedGradientMiddle { get { return Down; } }
        public override Color ButtonPressedGradientEnd { get { return Down; } }
        public override Color ButtonPressedBorder { get { return HotBorder; } }
        public override Color ButtonCheckedGradientBegin { get { return Down; } }
        public override Color ButtonCheckedGradientMiddle { get { return Down; } }
        public override Color ButtonCheckedGradientEnd { get { return Down; } }
        public override Color CheckBackground { get { return Down; } }
        public override Color CheckSelectedBackground { get { return Hot; } }
        public override Color SeparatorDark { get { return Line; } }
        public override Color SeparatorLight { get { return Bar; } }
        public override Color StatusStripGradientBegin { get { return Color.FromArgb(240, 241, 243); } }
        public override Color StatusStripGradientEnd { get { return Color.FromArgb(240, 241, 243); } }
        public override Color GripDark { get { return Bar; } }
        public override Color GripLight { get { return Bar; } }
    }

    public sealed class MainForm : Form
    {
        static readonly int[] Delays = { 0, 1000, 700, 500, 350, 250, 150, 100, 50, 20, 0 };
        static readonly string[] SpeedNamesDe = { "", "sehr langsam", "langsam", "gemächlich", "mittel", "normal", "zügig", "schnell", "sehr schnell", "rasant", "ohne Anzeige (Turbo)" };
        static readonly string[] SpeedNamesEn = { "", "very slow", "slow", "leisurely", "medium", "normal", "brisk", "fast", "very fast", "rapid", "no display (turbo)" };
        static string[] SpeedNames { get { return L.De ? SpeedNamesDe : SpeedNamesEn; } }

        readonly Cpu cpu = new Cpu();
        string setKey = "standard";
        readonly List<Snapshot> history = new List<Snapshot>();
        readonly HashSet<int> breakpoints = new HashSet<int>();
        bool running, halted, dirty, firstTick, logDirty, forcingCell, closingConfirmed;
        string filePath, docName;
        string DocTitle { get { return docName ?? L.S("Untitled", "Unbenannt"); } }
        int recOp = -1, recIdx;
        int[] prevRam;
        readonly Settings settings;
        readonly List<string[]> log = new List<string[]>();
        long logNo;
        readonly float dpi;
        readonly string mono;
        Font monoFont, monoBold;
        public bool TestMode, TestHideCu, TestShowCu;
        public string OpenOnStart;

        MenuStrip menu; ToolStrip tools; StatusStrip status;
        SplitContainer split;
        DataGridView grid; SchematicView view; TabControl tabs; TabPage tpAsm, tpLog;
        TextBox asm; ListBox asmErrors; ListView logView;
        readonly Timer timer = new Timer();
        readonly ToolTip gridTip = new ToolTip();
        ToolStripMenuItem miUndo, miMicro, miMacro, miRun, miReset, miMicroRun, miFollow, miAdjust, miHideCu, miRecord, miSide;
        readonly List<ToolStripMenuItem> miSets = new List<ToolStripMenuItem>();
        readonly List<ToolStripMenuItem> miSpeeds = new List<ToolStripMenuItem>();
        readonly List<ToolStripMenuItem> miFonts = new List<ToolStripMenuItem>();
        ToolStripButton tbUndo, tbMicro, tbMacro, tbRun, tbPause, tbReset, tbSave, tbCu, tbSide, tbRecord;
        ToolStripComboBox cbSet;
        TrackBar speed; ToolStripLabel speedText;
        ToolStripStatusLabel stState, stNext, stMacro, stMicro, stPc;
        bool updatingUi;
        ToolStripButton bAsm, bDis, bLogClear;


        int S(int px) { return (int)Math.Round(px * dpi); }

        static string PickFont(params string[] names)
        {
            foreach (string n in names)
                using (Font f = new Font(n, 10f)) if (string.Equals(f.Name, n, StringComparison.OrdinalIgnoreCase)) return n;
            return FontFamily.GenericMonospace.Name;
        }

        public MainForm()
        {
            settings = Settings.Load();
            L.De = settings.Language == "de";
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.None;
            using (Graphics g = CreateGraphics()) dpi = g.DpiX / 96f;
            mono = PickFont("Consolas", "DejaVu Sans Mono", "Liberation Mono", "Courier New");
            ToolStripManager.Renderer = new ToolStripProfessionalRenderer(new FlatColors()) { RoundedEdges = false };
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }
            Text = "Johnny";
            MinimumSize = new Size(S(980), S(620));
            KeyPreview = true;
            AllowDrop = true;

            BuildMenu();
            BuildToolbar();
            BuildStatus();
            BuildMain();
            UpdateChecks();
            Controls.Add(split);
            Controls.Add(status);
            Controls.Add(tools);
            Controls.Add(menu);
            MainMenuStrip = menu;

            ApplyFontSize();
            timer.Tick += Timer_Tick;
            DragEnter += OnDragEnter; DragDrop += OnDragDrop;

            if (settings.Bounds.Width > 200 && SystemInformation.VirtualScreen.IntersectsWith(settings.Bounds))
            { StartPosition = FormStartPosition.Manual; Bounds = settings.Bounds; }
            else { Size = new Size(S(1280), S(800)); settings.Maximized = true; }
            if (settings.Maximized) WindowState = FormWindowState.Maximized;

            UpdateSetUi();
            RefreshAll(false);
            SetStatus(L.S("Ready. Example programs are under File > Examples.", "Bereit. Beispielprogramme finden Sie unter Datei > Beispiele."));
        }

        // ================================================================ Aufbau
        ToolStripMenuItem Item(string text, Keys keys, EventHandler click, string keyText = null)
        {
            ToolStripMenuItem mi = new ToolStripMenuItem(text, null, click);
            if (keys != Keys.None) mi.ShortcutKeys = keys;
            if (keyText != null) mi.ShortcutKeyDisplayString = keyText;
            return mi;
        }

        void BuildMenu()
        {
            menu = new MenuStrip();
            miSets.Clear(); miSpeeds.Clear(); miFonts.Clear();
            menu.Padding = new Padding(S(4), S(2), 0, S(2));

            ToolStripMenuItem file = new ToolStripMenuItem(L.S("&File", "&Datei"));
            ToolStripMenuItem miNew = Item(L.S("&New", "&Neu"), Keys.Control | Keys.N, delegate { NewDocument(); });
            miNew.Image = Icons.Get("new", S(16));
            ToolStripMenuItem miOpen = Item(L.S("&Open…", "Ö&ffnen…"), Keys.Control | Keys.O, delegate { OpenDialog(); });
            miOpen.Image = Icons.Get("open", S(16));
            ToolStripMenuItem miSave = Item(L.S("&Save", "&Speichern"), Keys.Control | Keys.S, delegate { Save(); });
            miSave.Image = Icons.Get("save", S(16));
            ToolStripMenuItem examples = new ToolStripMenuItem(L.S("&Examples", "&Beispiele"));
            foreach (Example ex in Examples.All)
            {
                Example e2 = ex;
                ToolStripMenuItem mi = new ToolStripMenuItem(ex.Title, null, delegate { LoadExample(e2); });
                mi.ToolTipText = ex.Note;
                examples.DropDownItems.Add(mi);
            }
            file.DropDownItems.AddRange(new ToolStripItem[] {
                miNew, miOpen, miSave, Item(L.S("Save &As…", "Speichern &unter…"), Keys.Control | Keys.Shift | Keys.S, delegate { SaveAs(); }),
                new ToolStripSeparator(), examples, new ToolStripSeparator(),
                Item(L.S("E&xit", "&Beenden"), Keys.Alt | Keys.F4, delegate { Close(); }) });

            ToolStripMenuItem edit = new ToolStripMenuItem(L.S("&Edit", "&Bearbeiten"));
            miUndo = Item(L.S("&Undo", "&Rückgängig"), Keys.Control | Keys.Z, delegate { UndoCommand(); });
            miUndo.Image = Icons.Get("undo", S(16));
            miAdjust = Item(L.S("&Adjust addresses when inserting and deleting rows", "Adressen beim Einfügen und Löschen &anpassen"), Keys.None, delegate { settings.Adjust = !settings.Adjust; miAdjust.Checked = settings.Adjust; });
            miAdjust.Checked = settings.Adjust;
            edit.DropDownItems.AddRange(new ToolStripItem[] {
                miUndo, new ToolStripSeparator(),
                Item(L.S("&Edit cell", "Zelle &bearbeiten"), Keys.None, delegate { EditCell(); }, "F2"),
                Item(L.S("Set cell to &0", "Zelle auf &0 setzen"), Keys.None, delegate { ClearCell(); }, L.S("Del", "Entf")),
                Item(L.S("&Insert row", "Zeile &einfügen"), Keys.None, delegate { InsertRow(); }, L.S("Ins", "Einfg")),
                Item(L.S("&Delete row", "Zeile &löschen"), Keys.None, delegate { DeleteRow(); }, L.S("Shift+Del", "Umschalt+Entf")),
                miAdjust, new ToolStripSeparator(),
                Item(L.S("&Go to address…", "&Gehe zu Adresse…"), Keys.Control | Keys.G, delegate { GotoAddress(); }),
                new ToolStripSeparator(),
                Item(L.S("Toggle &breakpoint", "&Haltepunkt ein/aus"), Keys.F9, delegate { ToggleBreakpoint(CurrentRow()); }),
                Item(L.S("&Remove all breakpoints", "Alle Haltepunkte ent&fernen"), Keys.Control | Keys.Shift | Keys.F9, delegate { breakpoints.Clear(); RefreshAll(false); }),
                new ToolStripSeparator(),
                Item(L.S("&Clear memory…", "&Speicher leeren…"), Keys.None, delegate { ClearMemory(); }) });

            ToolStripMenuItem run = new ToolStripMenuItem(L.S("&Run", "&Ausführen"));
            miMicro = Item(L.S("&Micro step", "&Mikroschritt"), Keys.F7, delegate { MicroStepCommand(); }); miMicro.Image = Icons.Get("micro", S(16));
            miMacro = Item(L.S("M&acro step", "M&akroschritt"), Keys.F8, delegate { MacroStepCommand(); }); miMacro.Image = Icons.Get("macro", S(16));
            miRun = Item("&Start", Keys.F5, delegate { ToggleRun(); }); miRun.Image = Icons.Get("run", S(16));
            miReset = Item("&Reset", Keys.Control | Keys.R, delegate { ResetCpu(); }); miReset.Image = Icons.Get("reset", S(16));
            ToolStripMenuItem speedMenu = new ToolStripMenuItem(L.S("S&peed", "&Geschwindigkeit"));
            for (int i = 1; i <= 10; i++)
            {
                int i2 = i;
                ToolStripMenuItem mi = new ToolStripMenuItem(i + " – " + SpeedNames[i], null, delegate { SetSpeed(i2); });
                miSpeeds.Add(mi); speedMenu.DropDownItems.Add(mi);
            }
            miMicroRun = Item(L.S("Show &micro steps while running", "Beim Ablauf &Mikroschritte zeigen"), Keys.None, delegate { settings.MicroRun = !settings.MicroRun; UpdateChecks(); });
            miFollow = Item(L.S("Memory view &follows the program counter", "Speicheransicht &folgt dem Programmzähler"), Keys.None, delegate { settings.Follow = !settings.Follow; UpdateChecks(); });
            run.DropDownItems.AddRange(new ToolStripItem[] { miMicro, miMacro, miRun, miReset, new ToolStripSeparator(), speedMenu, miMicroRun, miFollow });

            ToolStripMenuItem mc = new ToolStripMenuItem(L.S("&Microcode", "&Mikrocode"));
            ToolStripMenuItem sets = new ToolStripMenuItem(L.S("&Instruction set", "&Befehlssatz"));
            foreach (InstructionSet s in InstructionSets.All)
            {
                string key = s.Key;
                ToolStripMenuItem mi = new ToolStripMenuItem(s.Title, null, delegate { ChangeSet(key); });
                mi.Tag = key; miSets.Add(mi); sets.DropDownItems.Add(mi);
            }
            ToolStripMenuItem miCustom = new ToolStripMenuItem(L.S("Custom microcode", "Eigener Mikrocode"), null, delegate { ChangeSet("custom"); });
            miCustom.Tag = "custom"; miSets.Add(miCustom); sets.DropDownItems.Add(miCustom);
            ToolStripMenuItem miEditor = Item(L.S("Microcode &editor…", "Mikrocode-&Editor…"), Keys.None, delegate { OpenMicrocodeEditor(); });
            miEditor.Image = Icons.Get("code", S(16));
            miRecord = Item(L.S("&Record instruction…", "Befehl &aufnehmen…"), Keys.None, delegate { ToggleRecording(); });
            miRecord.Image = Icons.Get("record", S(16));
            mc.DropDownItems.AddRange(new ToolStripItem[] { sets, new ToolStripSeparator(), miEditor, miRecord, new ToolStripSeparator(),
                Item(L.S("&Open microcode…", "Mikrocode ö&ffnen…"), Keys.None, delegate { OpenMicrocode(); }),
                Item(L.S("&Save microcode…", "Mikrocode &speichern…"), Keys.None, delegate { SaveMicrocode(); }) });

            ToolStripMenuItem viewMenu = new ToolStripMenuItem(L.S("&View", "A&nsicht"));
            miHideCu = Item(L.S("Show &control unit", "&Steuerwerk anzeigen"), Keys.Control | Keys.T, delegate { ToggleControlUnit(); });
            miSide = Item(L.S("Show &assembler and log", "&Assembler und Protokoll anzeigen"), Keys.Control | Keys.L, delegate { ToggleSidePanel(); });
            ToolStripMenuItem fonts = new ToolStripMenuItem(L.S("&Font size", "Schrift&größe"));
            float[] sizes = { 10f, 12f, 14f }; string[] fn = { "Normal", L.S("Large", "Groß"), L.S("Very large (projector)", "Sehr groß (Beamer)") };
            for (int i = 0; i < 3; i++)
            {
                float sz = sizes[i];
                ToolStripMenuItem mi = new ToolStripMenuItem(fn[i], null, delegate { settings.FontSize = sz; ApplyFontSize(); });
                mi.Tag = sz; miFonts.Add(mi); fonts.DropDownItems.Add(mi);
            }
            ToolStripMenuItem miSplashOnStart = null;
            miSplashOnStart = Item(L.S("Show &splash screen at startup", "Startbild beim Programm&start"), Keys.None, delegate { settings.ShowSplash = !settings.ShowSplash; miSplashOnStart.Checked = settings.ShowSplash; });
            miSplashOnStart.Checked = settings.ShowSplash;
            viewMenu.DropDownItems.AddRange(new ToolStripItem[] { miHideCu, miSide, new ToolStripSeparator(), fonts, miSplashOnStart, new ToolStripSeparator(),
                Item(L.S("Clear &log", "&Protokoll leeren"), Keys.None, delegate { ClearLog(); }) });

            ToolStripMenuItem help = new ToolStripMenuItem(L.S("&Help", "&Hilfe"));
            ToolStripMenuItem miHelp = Item(L.S("&Help", "&Hilfe"), Keys.F1, delegate { using (HelpForm h = new HelpForm(cpu, InstructionSets.ByKey(setKey))) h.ShowDialog(this); });
            miHelp.Image = Icons.Get("help", S(16));
            help.DropDownItems.AddRange(new ToolStripItem[] { miHelp, new ToolStripSeparator(),
                Item(L.S("Show &splash screen", "&Startbild anzeigen"), Keys.None, delegate { new SplashForm(false).ShowOver(this); }),
                Item(L.S("&About Johnny…", "&Über Johnny…"), Keys.None, delegate { using (AboutForm a = new AboutForm()) a.ShowDialog(this); }) });

            ToolStripMenuItem lang = new ToolStripMenuItem(L.S("&Language", "&Sprache (Language)"));
            ToolStripMenuItem miEn = new ToolStripMenuItem("English", null, delegate { SetLanguage(false); });
            ToolStripMenuItem miDe = new ToolStripMenuItem("Deutsch", null, delegate { SetLanguage(true); });
            miEn.Checked = !L.De; miDe.Checked = L.De;
            lang.DropDownItems.AddRange(new ToolStripItem[] { miEn, miDe });
            menu.Items.AddRange(new ToolStripItem[] { file, edit, run, mc, viewMenu, lang, help });
            UpdateChecks();
        }

        ToolStripButton ToolButton(string key, string text, string tip, bool showText, EventHandler click)
        {
            ToolStripButton b = new ToolStripButton(text, Icons.Get(key, S(16)), click);
            b.DisplayStyle = showText ? ToolStripItemDisplayStyle.ImageAndText : ToolStripItemDisplayStyle.Image;
            b.ToolTipText = tip; b.Padding = new Padding(S(3), S(2), S(3), S(2));
            return b;
        }

        void BuildToolbar()
        {
            tools = new ToolStrip();
            tools.GripStyle = ToolStripGripStyle.Hidden;
            tools.ImageScalingSize = new Size(S(16), S(16));
            tools.Padding = new Padding(S(6), S(3), S(6), S(3));
            tbSave = ToolButton("save", L.S("Save", "Speichern"), L.S("Save (Ctrl+S)", "Speichern (Strg+S)"), false, delegate { Save(); });
            tbUndo = ToolButton("undo", L.S("Undo", "Rückgängig"), L.S("Undo (Ctrl+Z)", "Rückgängig (Strg+Z)"), false, delegate { UndoCommand(); });
            tbReset = ToolButton("reset", "Reset", L.S("Reset: all registers to 0, memory is kept (Ctrl+R)", "Reset: alle Register auf 0, der Speicher bleibt (Strg+R)"), true, delegate { ResetCpu(); });
            tbMicro = ToolButton("micro", L.S("Micro step", "Mikroschritt"), L.S("Execute one micro instruction (F7)", "Einen Mikrobefehl ausführen (F7)"), true, delegate { MicroStepCommand(); });
            tbMacro = ToolButton("macro", L.S("Macro step", "Makroschritt"), L.S("Execute one complete machine instruction (F8)", "Einen ganzen Maschinenbefehl ausführen (F8)"), true, delegate { MacroStepCommand(); });
            tbRun = ToolButton("run", "Start", L.S("Run the program (F5)", "Programm laufen lassen (F5)"), true, delegate { StartRun(); });
            tbPause = ToolButton("pause", "Pause", L.S("Pause the program (F5 or Esc)", "Programm anhalten (F5 oder Esc)"), true, delegate { if (running) PauseRun(false); });
            tbCu = ToolButton("code", L.S("Control unit", "Steuerwerk"), L.S("Show or hide the control unit (Ctrl+T)", "Steuerwerk ein- oder ausblenden (Strg+T)"), true, delegate { ToggleControlUnit(); });
            tbSide = ToolButton("help", L.S("Assembler / Log", "Assembler / Protokoll"), L.S("Show or hide the side panel with assembler and log (Ctrl+L)", "Seitenleiste mit Assembler und Protokoll ein- oder ausblenden (Strg+L)"), true, delegate { ToggleSidePanel(); });
            tbSide.Image = null; tbSide.DisplayStyle = ToolStripItemDisplayStyle.Text; tbSide.Alignment = ToolStripItemAlignment.Right;
            tbRecord = ToolButton("record", L.S("Record", "Aufnehmen"), L.S("Record a new instruction: click micro instructions in the diagram", "Neuen Befehl aufnehmen: Mikrobefehle im Schaltbild anklicken"), false, delegate { ToggleRecording(); });
            ToolStripButton tbEditor = ToolButton("code", L.S("Microcode", "Mikrocode"), L.S("Open the microcode editor", "Mikrocode-Editor öffnen"), false, delegate { OpenMicrocodeEditor(); });

            speed = new TrackBar();
            speed.AutoSize = false; speed.Minimum = 1; speed.Maximum = 10; speed.TickStyle = TickStyle.None;
            speed.Size = new Size(S(120), S(24)); speed.BackColor = Color.FromArgb(245, 246, 247);
            speed.Value = Math.Max(1, Math.Min(10, settings.Speed));
            speed.ValueChanged += delegate { if (!updatingUi) SetSpeed(speed.Value); };
            ToolStripControlHost speedHost = new ToolStripControlHost(speed);
            speedHost.ToolTipText = L.S("Execution speed", "Ablaufgeschwindigkeit"); speedHost.Margin = new Padding(S(2), S(1), S(2), 0);
            speedText = new ToolStripLabel(); speedText.ForeColor = Color.FromArgb(90, 90, 90);

            cbSet = new ToolStripComboBox();
            cbSet.DropDownStyle = ComboBoxStyle.DropDownList; cbSet.FlatStyle = FlatStyle.System;
            cbSet.AutoSize = false; cbSet.Width = S(200);
            foreach (InstructionSet s in InstructionSets.All) cbSet.Items.Add(s.Title);
            cbSet.Items.Add(L.S("Custom microcode", "Eigener Mikrocode"));
            cbSet.SelectedIndexChanged += delegate
            {
                if (updatingUi) return;
                int i = cbSet.SelectedIndex;
                ChangeSet(i < InstructionSets.All.Length ? InstructionSets.All[i].Key : "custom");
            };

            tools.Items.AddRange(new ToolStripItem[] {
                ToolButton("new", L.S("New", "Neu"), L.S("New (Ctrl+N)", "Neu (Strg+N)"), false, delegate { NewDocument(); }),
                ToolButton("open", L.S("Open", "Öffnen"), L.S("Open (Ctrl+O)", "Öffnen (Strg+O)"), false, delegate { OpenDialog(); }),
                tbSave, new ToolStripSeparator(),
                tbRun, tbPause, tbMacro, tbMicro, tbReset, tbUndo, new ToolStripSeparator(),
                new ToolStripLabel(L.S("Speed:", "Tempo:")), speedHost, speedText, new ToolStripSeparator(),
                tbCu, new ToolStripSeparator(),
                new ToolStripLabel(L.S("Instruction set:", "Befehlssatz:")), cbSet, tbEditor, tbRecord, tbSide });
            SetSpeed(speed.Value);
        }

        void BuildStatus()
        {
            status = new StatusStrip();
            status.SizingGrip = true;
            status.Padding = new Padding(S(4), 0, S(12), 0);
            stState = new ToolStripStatusLabel(L.S("Ready", "Bereit"));
            stState.AutoSize = false; stState.Width = S(120); stState.TextAlign = ContentAlignment.MiddleLeft;
            stState.BorderSides = ToolStripStatusLabelBorderSides.Right; stState.BorderStyle = Border3DStyle.Etched;
            stNext = new ToolStripStatusLabel(); stNext.Spring = true; stNext.TextAlign = ContentAlignment.MiddleLeft;
            stPc = new ToolStripStatusLabel(); stPc.BorderSides = ToolStripStatusLabelBorderSides.Left; stPc.BorderStyle = Border3DStyle.Etched;
            stMacro = new ToolStripStatusLabel(); stMacro.BorderSides = ToolStripStatusLabelBorderSides.Left; stMacro.BorderStyle = Border3DStyle.Etched;
            stMicro = new ToolStripStatusLabel(); stMicro.BorderSides = ToolStripStatusLabelBorderSides.Left; stMicro.BorderStyle = Border3DStyle.Etched;
            status.Items.AddRange(new ToolStripItem[] { stState, stNext, stPc, stMacro, stMicro });
        }

        Panel ToolWindowHeader(string title)
        {
            Panel p = new Panel(); p.Dock = DockStyle.Top; p.Height = S(28); p.BackColor = Color.FromArgb(240, 241, 243);
            Label l = new Label(); l.Text = title; l.Dock = DockStyle.Fill; l.TextAlign = ContentAlignment.MiddleLeft;
            l.Padding = new Padding(S(8), 0, 0, 0); l.Font = new Font(Font, FontStyle.Bold); l.ForeColor = Color.FromArgb(50, 50, 50);
            p.Controls.Add(l);
            p.Paint += delegate(object s, PaintEventArgs e) { using (Pen pen = new Pen(Color.FromArgb(214, 217, 221))) e.Graphics.DrawLine(pen, 0, p.Height - 1, p.Width, p.Height - 1); };
            return p;
        }

        void BuildMain()
        {
            split = new SplitContainer();
            split.Dock = DockStyle.Fill; split.FixedPanel = FixedPanel.Panel2; split.SplitterWidth = S(5);
            split.BackColor = Color.FromArgb(214, 217, 221);
            split.Panel1.BackColor = Color.White; split.Panel2.BackColor = Color.White;
            split.Panel2Collapsed = !settings.ShowSide;

            // ---- Arbeitsspeicher
            grid = new DataGridView();
            grid.VirtualMode = true;
            grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.AllowUserToResizeRows = false; grid.AllowUserToOrderColumns = false;
            grid.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing; grid.RowHeadersWidth = S(26);
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing; grid.ColumnHeadersHeight = S(26);
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; grid.MultiSelect = false;
            grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            grid.BorderStyle = BorderStyle.None; grid.BackgroundColor = Color.White;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal; grid.GridColor = Color.FromArgb(236, 237, 239);
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(246, 247, 248);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(70, 70, 70);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(246, 247, 248);
            grid.RowHeadersDefaultCellStyle.BackColor = Color.FromArgb(246, 247, 248);
            grid.RowHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(246, 247, 248);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(204, 232, 255);
            grid.DefaultCellStyle.SelectionForeColor = Color.Black;
            grid.DefaultCellStyle.Padding = new Padding(S(2), 0, 0, 0);
            grid.ShowCellToolTips = false; grid.StandardTab = true;
            string[] heads = { L.S("Addr.", "Adr."), "Hi", "Lo", L.S("Instruction", "Befehl") }; int[] widths = { 52, 36, 46, 110 };
            for (int i = 0; i < 4; i++)
            {
                DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn();
                c.HeaderText = heads[i]; c.Width = S(widths[i]); c.SortMode = DataGridViewColumnSortMode.NotSortable;
                c.ReadOnly = i < 3; c.Resizable = DataGridViewTriState.False;
                if (i == 3) { c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; c.MaxInputLength = 20; }
                grid.Columns.Add(c);
            }
            grid.RowCount = Cpu.RamSize;
            grid.CellValueNeeded += Grid_CellValueNeeded;
            grid.CellValuePushed += Grid_CellValuePushed;
            grid.CellValidating += Grid_CellValidating;
            grid.CellFormatting += Grid_CellFormatting;
            grid.CellPainting += Grid_CellPainting;
            grid.RowPostPaint += Grid_RowPostPaint;
            grid.RowHeaderMouseClick += delegate(object s, DataGridViewCellMouseEventArgs e) { if (e.Button == MouseButtons.Left) ToggleBreakpoint(e.RowIndex); };
            grid.CellMouseDown += delegate(object s, DataGridViewCellMouseEventArgs e)
            {
                if (e.Button == MouseButtons.Right && e.RowIndex >= 0) { grid.CurrentCell = grid[3, e.RowIndex]; }
            };
            grid.CurrentCellChanged += delegate
            {
                if (forcingCell || !IsHandleCreated || grid.CurrentCell == null || grid.CurrentCell.ColumnIndex == 3) return;
                forcingCell = true;
                BeginInvoke((Action)delegate
                {
                    try { if (grid.CurrentCell != null && !grid.IsCurrentCellInEditMode) grid.CurrentCell = grid[3, grid.CurrentCell.RowIndex]; }
                    catch (InvalidOperationException) { }
                    finally { forcingCell = false; }
                });
            };
            grid.KeyDown += Grid_KeyDown;
            grid.EditingControlShowing += delegate(object s, DataGridViewEditingControlShowingEventArgs e)
            {
                TextBox tb = e.Control as TextBox;
                if (tb != null) tb.CharacterCasing = CharacterCasing.Upper;
            };
            grid.AllowDrop = true; grid.DragEnter += OnDragEnter; grid.DragDrop += OnDragDrop;

            grid.ContextMenuStrip = BuildGridMenu();


            // ---- Schaltbild
            view = new SchematicView(mono);
            view.Dock = DockStyle.Fill; view.Cpu = cpu; view.Font = Font;
            view.MicroOpClicked += ManualOp;
            view.DataBusEntered += SetDataBusFromText;
            view.AddressBusEntered += SetAddressBusFromText;
            view.ControlUnitClicked += ToggleControlUnit;
            view.AttachGrid(grid);
            view.AllowDrop = true; view.DragEnter += OnDragEnter; view.DragDrop += OnDragDrop;
            split.Panel1.Controls.Add(view);

            // ---- Seitenleiste: Assembler und Protokoll
            tabs = new TabControl(); tabs.Dock = DockStyle.Fill; tabs.Padding = new Point(S(12), S(4));
            tpAsm = new TabPage("Assembler"); tpLog = new TabPage(L.S("Log", "Protokoll"));
            tpAsm.BackColor = Color.White; tpLog.BackColor = Color.White;

            ToolStrip asmBar = new ToolStrip(); asmBar.GripStyle = ToolStripGripStyle.Hidden; asmBar.ImageScalingSize = new Size(S(16), S(16));
            asmBar.Padding = new Padding(S(4), S(2), S(4), S(2));
            bAsm = new ToolStripButton(L.S("Load into memory", "In den Speicher übertragen"), Icons.Get("macro", S(16)), delegate { AssembleCommand(); });
            bAsm.ToolTipText = L.S("Assemble the text and write it into memory (Ctrl+Enter)", "Text übersetzen und in den Arbeitsspeicher schreiben (Strg+Enter)");
            bDis = new ToolStripButton(L.S("From memory", "Aus dem Speicher"), Icons.Get("undo", S(16)), delegate { DisassembleCommand(); });
            bDis.ToolTipText = L.S("Show the current memory content as text", "Aktuellen Speicherinhalt als Text anzeigen");
            asmBar.Items.AddRange(new ToolStripItem[] { bAsm, new ToolStripSeparator(), bDis });
            asm = new TextBox();
            asm.Multiline = true; asm.AcceptsTab = true; asm.AcceptsReturn = true; asm.WordWrap = false; asm.ScrollBars = ScrollBars.Both;
            asm.Dock = DockStyle.Fill; asm.BorderStyle = BorderStyle.None; asm.MaxLength = 200000;
            asm.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter && e.Control) { e.SuppressKeyPress = true; AssembleCommand(); }
                if (e.KeyCode == Keys.A && e.Control) { asm.SelectAll(); e.SuppressKeyPress = true; }
            };
            Panel asmPad = new Panel(); asmPad.Dock = DockStyle.Fill; asmPad.Padding = new Padding(S(6), S(4), 0, 0); asmPad.BackColor = Color.White;
            asmPad.Controls.Add(asm);
            asmErrors = new ListBox(); asmErrors.Dock = DockStyle.Bottom; asmErrors.Height = S(96); asmErrors.Visible = false;
            asmErrors.ForeColor = Color.FromArgb(160, 30, 20); asmErrors.BorderStyle = BorderStyle.FixedSingle; asmErrors.IntegralHeight = false;
            asmErrors.DoubleClick += delegate { GotoAsmError(); };
            tpAsm.Controls.Add(asmPad); tpAsm.Controls.Add(asmErrors); tpAsm.Controls.Add(asmBar);

            ToolStrip logBar = new ToolStrip(); logBar.GripStyle = ToolStripGripStyle.Hidden; logBar.Padding = new Padding(S(4), S(2), S(4), S(2));
            bLogClear = new ToolStripButton(L.S("Clear log", "Protokoll leeren"), null, delegate { ClearLog(); });
            logBar.Items.Add(bLogClear);
            logView = new ListView();
            logView.View = View.Details; logView.VirtualMode = true; logView.FullRowSelect = true; logView.Dock = DockStyle.Fill;
            logView.BorderStyle = BorderStyle.None; logView.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            logView.Columns.Add(L.S("No.", "Nr."), S(52)); logView.Columns.Add(L.S("Micro addr.", "Mikro-Adr."), S(70)); logView.Columns.Add(L.S("Micro instr.", "Mikrobefehl"), S(80)); logView.Columns.Add(L.S("Effect", "Wirkung"), S(320));
            logView.RetrieveVirtualItem += delegate(object s, RetrieveVirtualItemEventArgs e)
            {
                string[] r = e.ItemIndex < log.Count ? log[e.ItemIndex] : new[] { "", "", "", "" };
                ListViewItem it = new ListViewItem(r);
                if (r[0].Length == 0) it.ForeColor = Color.FromArgb(120, 120, 120);
                else if (r[1] == "Hand" || r[1] == "manual") it.ForeColor = Color.FromArgb(0, 84, 153);
                e.Item = it;
            };
            tpLog.Controls.Add(logView); tpLog.Controls.Add(logBar);

            tabs.TabPages.Add(tpAsm); tabs.TabPages.Add(tpLog);
            tabs.SelectedIndexChanged += delegate { if (tabs.SelectedTab == tpLog) { logDirty = true; RefreshLog(); } };
            split.Panel2.Controls.Add(tabs);
            split.Panel2.Padding = new Padding(0, S(4), S(2), 0);
        }

        ContextMenuStrip BuildGridMenu()
        {
            ContextMenuStrip cm = new ContextMenuStrip();
            cm.Items.AddRange(new ToolStripItem[] {
                Item(L.S("Edit cell", "Zelle bearbeiten"), Keys.None, delegate { EditCell(); }, "F2"),
                Item(L.S("Set cell to 0", "Zelle auf 0 setzen"), Keys.None, delegate { ClearCell(); }, L.S("Del", "Entf")),
                new ToolStripSeparator(),
                Item(L.S("Insert row", "Zeile einfügen"), Keys.None, delegate { InsertRow(); }, L.S("Ins", "Einfg")),
                Item(L.S("Delete row", "Zeile löschen"), Keys.None, delegate { DeleteRow(); }, L.S("Shift+Del", "Umschalt+Entf")),
                new ToolStripSeparator(),
                Item(L.S("Toggle breakpoint", "Haltepunkt ein/aus"), Keys.None, delegate { ToggleBreakpoint(CurrentRow()); }, "F9"),
                Item(L.S("Set program counter here", "Programmzähler hierher setzen"), Keys.None, delegate { SetPcHere(); }) });
            return cm;
        }

        void SetLanguage(bool de)
        {
            if (L.De == de) return;
            L.De = de;
            settings.Language = de ? "de" : "en";
            ApplyLanguage();
        }

        /// <summary>Alle Texte der Oberfläche in der gewählten Sprache neu setzen.</summary>
        void ApplyLanguage()
        {
            SuspendLayout();
            MenuStrip oldMenu = menu; ToolStrip oldTools = tools;
            Controls.Remove(oldMenu); Controls.Remove(oldTools);
            BuildMenu(); BuildToolbar();
            Controls.Add(tools); Controls.Add(menu); MainMenuStrip = menu;
            oldMenu.Dispose(); oldTools.Dispose();
            grid.Columns[0].HeaderText = L.S("Addr.", "Adr.");
            grid.Columns[3].HeaderText = L.S("Instruction", "Befehl");
            tpLog.Text = L.S("Log", "Protokoll");
            bAsm.Text = L.S("Load into memory", "In den Speicher übertragen");
            bAsm.ToolTipText = L.S("Assemble the text and write it into memory (Ctrl+Enter)", "Text übersetzen und in den Arbeitsspeicher schreiben (Strg+Enter)");
            bDis.Text = L.S("From memory", "Aus dem Speicher");
            bDis.ToolTipText = L.S("Show the current memory content as text", "Aktuellen Speicherinhalt als Text anzeigen");
            bLogClear.Text = L.S("Clear log", "Protokoll leeren");
            logView.Columns[0].Text = L.S("No.", "Nr."); logView.Columns[1].Text = L.S("Micro addr.", "Mikro-Adr.");
            logView.Columns[2].Text = L.S("Micro instr.", "Mikrobefehl"); logView.Columns[3].Text = L.S("Effect", "Wirkung");
            ContextMenuStrip oldCm = grid.ContextMenuStrip; grid.ContextMenuStrip = BuildGridMenu(); if (oldCm != null) oldCm.Dispose();
            UpdateChecks(); UpdateSetUi(); SetSpeed(settings.Speed);
            if (recOp >= 0) UpdateRecordingText();
            view.ApplyLanguage();
            ResumeLayout(true);
            SetStatus(L.S("Language: English", "Sprache: Deutsch"));
            RefreshAll(false);
        }

        public void TestSetLanguage(bool de) { SetLanguage(de); }

        void ApplyFontSize()
        {
            if (monoFont != null) { monoFont.Dispose(); monoBold.Dispose(); }
            monoFont = new Font(mono, settings.FontSize);
            monoBold = new Font(mono, settings.FontSize, FontStyle.Bold);
            asm.Font = monoFont;
            asmErrors.Font = new Font(Font.FontFamily, Math.Max(9f, settings.FontSize - 1f));
            logView.Font = new Font(mono, Math.Max(9f, settings.FontSize - 1f));
            foreach (ToolStripMenuItem mi in miFonts) mi.Checked = Math.Abs((float)mi.Tag - settings.FontSize) < 0.1f;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            PlaceSplitter();
            if (grid.Rows.Count > 0) grid.CurrentCell = grid[3, 0];
            grid.Focus();
            if (OpenOnStart != null) OpenFiles(new[] { OpenOnStart });
            else if (settings.ShowSplash && !TestMode) BeginInvoke((Action)delegate { new SplashForm(true).ShowOver(this); });
        }

        // ================================================================ Speichertabelle
        void Grid_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            int i = e.RowIndex; if (i < 0 || i >= Cpu.RamSize) return;
            int v = cpu.Ram[i];
            switch (e.ColumnIndex)
            {
                case 0: e.Value = i.ToString("000"); break;
                case 1: e.Value = Cpu.Hi(v).ToString("00"); break;
                case 2: e.Value = Cpu.Lo(v).ToString("000"); break;
                case 3: e.Value = Cpu.Hi(v) > 0 ? cpu.CellText(v) : (v != 0 ? v.ToString() : ""); break;
            }
        }

        void Grid_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (e.ColumnIndex != 3 || !grid.IsCurrentCellInEditMode) return;
            int v; string err;
            if (!Cpu.ParseCell(Convert.ToString(e.FormattedValue), cpu.Names, out v, out err))
            {
                e.Cancel = true;
                SystemSounds.Beep.Play();
                Rectangle r = grid.GetCellDisplayRectangle(3, e.RowIndex, true);
                gridTip.ToolTipTitle = L.S("Invalid input", "Ungültige Eingabe");
                gridTip.Show(err, grid, r.Left, r.Bottom + S(2), 3500);
            }
        }

        void Grid_CellValuePushed(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.ColumnIndex != 3) return;
            int v; string err;
            if (!Cpu.ParseCell(Convert.ToString(e.Value), cpu.Names, out v, out err)) return;
            if (cpu.Ram[e.RowIndex] == v) return;
            PushHistory();
            prevRam = null; view.PrevRam = null;
            cpu.Ram[e.RowIndex] = v;
            SetDirty();
            RefreshAll(false);
        }

        static readonly Color RowPc = Color.FromArgb(255, 246, 204), RowBp = Color.FromArgb(251, 229, 227);
        void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            int i = e.RowIndex; if (i < 0) return;
            int v = cpu.Ram[i];
            if (breakpoints.Contains(i)) e.CellStyle.BackColor = RowBp;
            else if (i == cpu.Pc) e.CellStyle.BackColor = RowPc;
            bool changed = prevRam != null && prevRam[i] != v;
            switch (e.ColumnIndex)
            {
                case 0:
                    if (i == cpu.Ab) { e.CellStyle.ForeColor = Color.FromArgb(0, 99, 177); e.CellStyle.Font = GridBold(); e.CellStyle.SelectionForeColor = Color.FromArgb(0, 99, 177); }
                    else e.CellStyle.ForeColor = Color.FromArgb(120, 120, 120);
                    break;
                case 1:
                case 2:
                    e.CellStyle.ForeColor = changed ? Color.FromArgb(196, 43, 28) : (v == 0 ? Color.FromArgb(175, 175, 175) : (e.ColumnIndex == 1 ? Color.FromArgb(31, 78, 140) : Color.Black));
                    break;
                case 3:
                    e.CellStyle.ForeColor = changed ? Color.FromArgb(196, 43, 28) : (Cpu.Hi(v) > 0 ? Color.Black : Color.FromArgb(128, 128, 128));
                    break;
            }
        }

        Font gridBold, gridBoldBase;
        Font GridBold()
        {
            Font f = grid.DefaultCellStyle.Font ?? grid.Font;
            if (gridBold == null || gridBoldBase != f) { if (gridBold != null) gridBold.Dispose(); gridBold = new Font(f, FontStyle.Bold); gridBoldBase = f; }
            return gridBold;
        }

        void Grid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.ColumnIndex == -1 && e.RowIndex >= 0)
            {
                // Zeilenkopf: nur Hintergrund, Markierungen kommen in RowPostPaint (statt des Standard-Dreiecks)
                using (SolidBrush b = new SolidBrush(Color.FromArgb(246, 247, 248))) e.Graphics.FillRectangle(b, e.CellBounds);
                using (Pen p = new Pen(Color.FromArgb(226, 228, 231))) e.Graphics.DrawLine(p, e.CellBounds.Right - 1, e.CellBounds.Top, e.CellBounds.Right - 1, e.CellBounds.Bottom);
                e.Handled = true;
            }
        }

        void Grid_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            int i = e.RowIndex;
            bool bp = breakpoints.Contains(i), pc = i == cpu.Pc;
            if (!bp && !pc) return;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle hb = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, grid.RowHeadersWidth, e.RowBounds.Height);
            float d = Math.Min(hb.Height - S(6), S(13));
            if (bp)
            {
                RectangleF c = new RectangleF(hb.Left + (hb.Width - d) / 2f, hb.Top + (hb.Height - d) / 2f, d, d);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(229, 20, 0))) g.FillEllipse(b, c);
                using (Pen p = new Pen(Color.FromArgb(150, 20, 10))) g.DrawEllipse(p, c);
            }
            if (pc)
            {
                float cx = hb.Left + hb.Width / 2f + S(2), cy = hb.Top + hb.Height / 2f, s = d * 0.55f;
                PointF[] a = { new PointF(cx + s * 0.9f, cy), new PointF(cx - s * 0.7f, cy - s), new PointF(cx - s * 0.7f, cy + s) };
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 204, 0))) g.FillPolygon(b, a);
                using (Pen p = new Pen(Color.FromArgb(128, 96, 0))) g.DrawPolygon(p, a);
            }
            g.SmoothingMode = SmoothingMode.None;
        }

        void Grid_KeyDown(object sender, KeyEventArgs e)
        {
            if (grid.IsCurrentCellInEditMode) return;
            if (e.KeyCode == Keys.Delete && e.Shift) { DeleteRow(); e.Handled = true; }
            else if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back) { ClearCell(); e.Handled = true; }
            else if (e.KeyCode == Keys.Insert) { InsertRow(); e.Handled = true; }
        }

        int CurrentRow() { return grid.CurrentCell != null ? grid.CurrentCell.RowIndex : 0; }

        void SelectRow(int i, bool center)
        {
            i = Math.Max(0, Math.Min(Cpu.RamSize - 1, i));
            if (grid.IsCurrentCellInEditMode) grid.EndEdit();
            grid.CurrentCell = grid[3, i];
            if (center)
            {
                int n = Math.Max(1, grid.DisplayedRowCount(false));
                grid.FirstDisplayedScrollingRowIndex = Math.Max(0, i - n / 2);
            }
        }

        void ScrollToTop() { try { if (grid.RowCount > 0) grid.FirstDisplayedScrollingRowIndex = 0; } catch (InvalidOperationException) { } }

        void EnsureRowVisible(int i)
        {
            int first = grid.FirstDisplayedScrollingRowIndex, n = grid.DisplayedRowCount(false);
            if (first < 0 || n <= 0) return;
            if (i < first || i >= first + n) grid.FirstDisplayedScrollingRowIndex = Math.Max(0, Math.Min(Cpu.RamSize - 1, i - n / 3));
        }

        void EditCell() { if (grid.CurrentCell == null) return; grid.CurrentCell = grid[3, CurrentRow()]; grid.BeginEdit(true); }

        void ClearCell()
        {
            int i = CurrentRow();
            if (cpu.Ram[i] == 0) return;
            PushHistory(); prevRam = null; view.PrevRam = null;
            cpu.Ram[i] = 0; SetDirty(); RefreshAll(false);
        }

        void InsertRow()
        {
            int i = CurrentRow(), changed;
            PushHistory();
            string err = RamEdit.InsertRow(cpu.Ram, i, settings.Adjust, cpu.Names, out changed);
            if (err != null) { history.RemoveAt(history.Count - 1); MessageBox.Show(this, err, L.S("Insert row", "Zeile einfügen"), MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            prevRam = null; view.PrevRam = null;
            SetDirty();
            SetStatus(L.S("Row " + i.ToString("000") + " inserted." + (settings.Adjust ? " " + changed + " address reference(s) adjusted." : " Please check that all address references are still correct."),
                          "Zeile " + i.ToString("000") + " eingefügt." + (settings.Adjust ? " " + changed + " Adressangabe(n) angepasst." : " Bitte prüfen, ob alle Adressangaben noch stimmen.")));
            RefreshAll(false);
        }

        void DeleteRow()
        {
            int i = CurrentRow(), changed, dangling;
            PushHistory();
            RamEdit.DeleteRow(cpu.Ram, i, settings.Adjust, cpu.Names, out changed, out dangling);
            prevRam = null; view.PrevRam = null;
            SetDirty();
            string msg = L.S("Row " + i.ToString("000") + " deleted." + (settings.Adjust ? " " + changed + " address reference(s) adjusted." : " Please check that all address references are still correct."),
                             "Zeile " + i.ToString("000") + " gelöscht." + (settings.Adjust ? " " + changed + " Adressangabe(n) angepasst." : " Bitte prüfen, ob alle Adressangaben noch stimmen."));
            if (dangling > 0) msg += L.S(" Warning: " + dangling + " instruction(s) pointed to the deleted cell.", " Achtung: " + dangling + " Befehl(e) zeigten auf die gelöschte Zelle.");
            SetStatus(msg);
            RefreshAll(false);
        }

        void ToggleBreakpoint(int i)
        {
            if (i < 0 || i >= Cpu.RamSize) return;
            if (!breakpoints.Remove(i)) breakpoints.Add(i);
            grid.InvalidateRow(i);
        }

        void SetPcHere()
        {
            if (running) return;
            PushHistory(); BeginStep();
            cpu.Pc = CurrentRow(); cpu.MOp = 0; cpu.MMc = 0; halted = false;
            RefreshAll(false);
        }

        void GotoAddress()
        {
            using (InputDialog d = new InputDialog(L.S("Go to address", "Gehe zu Adresse"), L.S("Address (0 to 999):", "Adresse (0 bis 999):"), CurrentRow().ToString()))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                int a;
                if (!int.TryParse(d.Value.Trim(), out a) || a < 0 || a > 999) { MessageBox.Show(this, L.S("Please enter an address from 0 to 999.", "Bitte eine Adresse von 0 bis 999 eingeben."), L.S("Go to address", "Gehe zu Adresse"), MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                SelectRow(a, true); grid.Focus();
            }
        }

        void ClearMemory()
        {
            if (Cpu.LastUsed(cpu.Ram) < 0) return;
            if (MessageBox.Show(this, L.S("Set the entire memory to 0?\n(Can be undone with \"Undo\".)", "Den gesamten Arbeitsspeicher auf 0 setzen?\n(Mit \"Rückgängig\" wieder herstellbar.)"), L.S("Clear memory", "Speicher leeren"), MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            PauseIfRunning();
            PushHistory(); prevRam = null; view.PrevRam = null;
            cpu.Ram = new int[Cpu.RamSize]; cpu.ResetRegisters(); halted = false;
            SetDirty(); RefreshAll(false);
        }

        // ================================================================ Ausführen
        void PushHistory()
        {
            history.Add(cpu.Save(true));
            if (history.Count > 2000) history.RemoveRange(0, history.Count - 2000);
        }

        void UndoCommand()
        {
            if (asm.Focused) { asm.Undo(); return; }
            if (grid.IsCurrentCellInEditMode) { TextBox tb = grid.EditingControl as TextBox; if (tb != null) tb.Undo(); return; }
            if (history.Count == 0) return;
            PauseIfRunning();
            Snapshot s = history[history.Count - 1];
            history.RemoveAt(history.Count - 1);
            bool ramChanged = !s.Ram.SequenceEqual(cpu.Ram);
            cpu.Restore(s);
            halted = false; view.Prev = null; view.PrevRam = null; prevRam = null; view.Active.Clear();
            if (ramChanged) SetDirty();
            AddLogSeparator(L.S("Undo", "Rückgängig"));
            RefreshAll(true);
        }

        void BeginStep()
        {
            view.Prev = cpu.Save(false);
            prevRam = (int[])cpu.Ram.Clone();
            view.PrevRam = prevRam;
            view.Active.Clear();
        }

        void NoteAlu(int op) { if (op >= 12 && op <= 17 && op != 15) view.LastAluOp = op; }

        void StepMicro()
        {
            BeginStep();
            string at = cpu.MOp.ToString("00") + ":" + cpu.MMc;
            StepResult r = cpu.MicroStep();
            if (r.Error != null) { StopWithError(r.Error); return; }
            view.Active.Add(r.Op); NoteAlu(r.Op);
            AddLog(at, r.Op);
            if (r.Halt) OnHalt();
        }

        void StepMacro()
        {
            BeginStep();
            MacroResult res = cpu.MacroStep(false, delegate(StepResult r)
            {
                view.Active.Add(r.Op); NoteAlu(r.Op);
                AddLog((r.Address / 10).ToString("00") + ":" + (r.Address % 10), r.Op);
            });
            if (res.Error != null) { StopWithError(res.Error); return; }
            if (res.Halted) OnHalt();
        }

        void MicroStepCommand() { if (running) return; CommitEdit(); halted = false; PushHistory(); StepMicro(); RefreshAll(true); }
        void MacroStepCommand() { if (running) return; CommitEdit(); halted = false; PushHistory(); StepMacro(); RefreshAll(true); }

        void CommitEdit() { if (grid.IsCurrentCellInEditMode) grid.EndEdit(); }

        void ToggleRun() { if (running) PauseRun(false); else StartRun(); }

        void StartRun()
        {
            if (running) return;
            if (grid.IsCurrentCellInEditMode && !grid.EndEdit()) return;
            if (recOp >= 0) StopRecording(null);
            running = true; halted = false; firstTick = true;
            PushHistory();
            AddLogSeparator("Start");
            timer.Interval = Math.Max(10, Delays[settings.Speed]);
            timer.Start();
            RefreshAll(false);
        }

        void PauseRun(bool silent)
        {
            running = false; timer.Stop();
            if (!silent) { AddLogSeparator("Pause"); SetStatus(L.S("Paused.", "Angehalten.")); }
            RefreshAll(true);
        }

        void PauseIfRunning() { if (running) PauseRun(true); }

        void Timer_Tick(object sender, EventArgs e)
        {
            if (!running) { timer.Stop(); return; }
            if (settings.Speed >= 10)
            {
                Stopwatch sw = Stopwatch.StartNew();
                view.Active.Clear(); view.Prev = null; view.PrevRam = null; prevRam = null;
                while (running && sw.ElapsedMilliseconds < 40)
                {
                    if (!firstTick && cpu.AtFetchStart && breakpoints.Contains(cpu.Pc)) { HitBreakpoint(); break; }
                    if (EmptyCellAtPc()) break;
                    firstTick = false;
                    MacroResult r = cpu.MacroStep(false);
                    if (r.Error != null) { StopWithError(r.Error); break; }
                    if (r.Halted) { OnHalt(); break; }
                }
                RefreshAll(true);
                return;
            }
            if (!firstTick && cpu.AtFetchStart && breakpoints.Contains(cpu.Pc)) { HitBreakpoint(); return; }
            firstTick = false;
            if (EmptyCellAtPc()) return;
            PushHistory();
            if (settings.MicroRun) StepMicro(); else StepMacro();
            RefreshAll(true);
        }

        bool EmptyCellAtPc()
        {
            if (!cpu.AtFetchStart || Cpu.Hi(cpu.Ram[cpu.Pc]) != 0) return false;
            PauseRun(true);
            AddLogSeparator(L.S("No instruction at address ", "Kein Befehl an Adresse ") + cpu.Pc.ToString("000"));
            SelectRow(cpu.Pc, false);
            MessageBox.Show(this, L.S("There is no instruction at address " + cpu.Pc.ToString("000") + " (opcode 00, content " + cpu.Ram[cpu.Pc] + ").\n\n" +
                "Johnny would fetch this cell forever. Is a HLT missing at the end of the program?",
                "An Adresse " + cpu.Pc.ToString("000") + " steht kein Befehl (Opcode 00, Inhalt " + cpu.Ram[cpu.Pc] + ").\n\n" +
                "Johnny würde diese Zelle endlos holen. Fehlt vielleicht ein HLT am Ende des Programms?"), L.S("Program stopped", "Programm angehalten"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return true;
        }

        void HitBreakpoint()
        {
            PauseRun(true);
            AddLogSeparator("Haltepunkt " + cpu.Pc.ToString("000"));
            SelectRow(cpu.Pc, false);
            SetStatus("Haltepunkt bei Adresse " + cpu.Pc.ToString("000") + " erreicht.");
            SystemSounds.Asterisk.Play();
        }

        void OnHalt()
        {
            halted = true;
            if (running) PauseRun(true);
            AddLogSeparator(L.S("End of program", "Programmende"));
            SetStatus(L.S("Johnny has reached the end of the program.", "Johnny hat das Ende des Programms erreicht."));
            SystemSounds.Asterisk.Play();
        }

        void StopWithError(string msg)
        {
            if (running) PauseRun(true);
            AddLogSeparator(L.S("Error", "Fehler"));
            RefreshAll(true);
            MessageBox.Show(this, msg, L.S("Program stopped", "Programm angehalten"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        void ResetCpu()
        {
            PauseIfRunning();
            PushHistory();
            cpu.ResetRegisters(); halted = false;
            view.Prev = null; view.PrevRam = null; prevRam = null; view.Active.Clear(); view.LastAluOp = -1;
            AddLogSeparator("Reset");
            SelectRow(0, false); ScrollToTop();
            SetStatus(L.S("Reset: all registers are 0. Memory is unchanged.", "Reset: Alle Register stehen auf 0. Der Speicher ist unverändert."));
            RefreshAll(false);
        }

        void SetSpeed(int s)
        {
            settings.Speed = Math.Max(1, Math.Min(10, s));
            updatingUi = true;
            try { if (speed.Value != settings.Speed) speed.Value = settings.Speed; }
            finally { updatingUi = false; }
            speedText.Text = settings.Speed >= 10 ? "Turbo" : SpeedNames[settings.Speed];
            for (int i = 0; i < miSpeeds.Count; i++) miSpeeds[i].Checked = i + 1 == settings.Speed;
            if (running) timer.Interval = Math.Max(10, Delays[settings.Speed]);
        }

        void UpdateChecks()
        {
            if (miMicroRun == null) return;
            miMicroRun.Checked = settings.MicroRun; miFollow.Checked = settings.Follow; miHideCu.Checked = !settings.HideCu; miAdjust.Checked = settings.Adjust;
            if (miSide != null) miSide.Checked = settings.ShowSide;
            if (tbCu != null) tbCu.Checked = !settings.HideCu;
            if (tbSide != null) tbSide.Checked = settings.ShowSide;
        }

        // ---------------------------------------------------------------- Von Hand
        void ManualOp(int op)
        {
            if (running) return;
            CommitEdit();
            PushHistory(); halted = false; BeginStep();
            bool h = cpu.Exec(op);
            view.Active.Add(op); NoteAlu(op);
            AddLog(L.S("manual", "Hand"), op);
            if (recOp >= 0)
            {
                cpu.Mpm[recIdx++] = op;
                MarkCustom();
                if (recIdx >= recOp * 10 + 10) StopRecording(L.S("Recording finished: 10 micro instructions were recorded.", "Aufnahme beendet: 10 Mikrobefehle wurden aufgezeichnet."));
                else UpdateRecordingText();
            }
            if (h) SetStatus(L.S("stopp was executed.", "stopp wurde ausgeführt."));
            RefreshAll(false);
        }

        void SetDataBusFromText(string text)
        {
            if (running) return;
            int v; string err;
            if (!Cpu.ParseCell(text, cpu.Names, out v, out err)) { MessageBox.Show(this, err, L.S("Data bus", "Datenbus"), MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            PushHistory(); BeginStep(); cpu.Db = v;
            AddLogText(L.S("manual", "Hand"), "-> db", L.S("Data bus := ", "Datenbus := ") + Fv(v));
            SetStatus(L.S("Value ", "Wert ") + Fv(v) + L.S(" was put on the data bus by hand.", " von Hand auf den Datenbus gelegt."));
            RefreshAll(false);
        }

        void SetAddressBusFromText(string text)
        {
            if (running) return;
            int a;
            if (!int.TryParse((text ?? "").Trim(), out a) || a < 0 || a > 999) { MessageBox.Show(this, L.S("Please enter an address from 0 to 999.", "Bitte eine Adresse von 0 bis 999 eingeben."), L.S("Address bus", "Adressbus"), MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            PushHistory(); BeginStep(); cpu.Ab = a;
            AddLogText(L.S("manual", "Hand"), "-> ab", L.S("Address bus := ", "Adressbus := ") + a.ToString("000"));
            SetStatus(L.S("Address " + a.ToString("000") + " was put on the address bus by hand.", "Adresse " + a.ToString("000") + " von Hand auf den Adressbus gelegt."));
            RefreshAll(false); EnsureRowVisible(a);
        }

        void ToggleControlUnit()
        {
            settings.HideCu = !settings.HideCu;
            UpdateChecks();
            SetStatus(settings.HideCu ? L.S("Control unit hidden: buses, memory and ALU can be operated by hand.", "Steuerwerk ausgeblendet: Busse, Speicher und Rechenwerk können von Hand bedient werden.") : L.S("Control unit shown.", "Steuerwerk eingeblendet."));
            RefreshAll(false);
        }

        void ToggleSidePanel()
        {
            settings.ShowSide = !settings.ShowSide;
            if (!settings.ShowSide && split.Width > 0) settings.RightWidth = split.Width - split.SplitterDistance;
            split.Panel2Collapsed = !settings.ShowSide;
            if (settings.ShowSide) PlaceSplitter();
            UpdateChecks();
        }

        void PlaceSplitter()
        {
            if (split.Panel2Collapsed) return;
            int right = settings.RightWidth > 0 ? settings.RightWidth : S(340);
            try
            {
                split.Panel2MinSize = S(220); split.Panel1MinSize = S(400);
                split.SplitterDistance = Math.Max(S(400), split.Width - right);
            }
            catch (InvalidOperationException) { }
            catch (ArgumentException) { }
        }

        // ---------------------------------------------------------------- Protokoll
        static string Fv(int v) { return Cpu.Hi(v) > 0 ? Cpu.Hi(v).ToString("00") + " " + Cpu.Lo(v).ToString("000") : v.ToString(); }

        string Effect(int n)
        {
            string ab = cpu.Ab.ToString("000"), pc = cpu.Pc.ToString("000");
            switch (n)
            {
                case 0: return L.S("no operation", "keine Operation");
                case 1: return "RAM[" + ab + "] := " + L.S("data bus", "Datenbus") + " = " + Fv(cpu.Db);
                case 2: return L.S("data bus", "Datenbus") + " := RAM[" + ab + "] = " + Fv(cpu.Db);
                case 3: return L.S("instruction register", "Befehlsregister") + " := " + cpu.InsOp.ToString("00") + " " + cpu.InsAd.ToString("000") + (cpu.InsOp > 0 ? "  (" + cpu.CellText(cpu.InsOp * 1000 + cpu.InsAd) + ")" : "");
                case 4: return L.S("address bus := address part = ", "Adressbus := Adressteil = ") + ab;
                case 5: return L.S("micro program ", "Mikroprogramm ") + cpu.MOp.ToString("00") + " (" + cpu.OpName(cpu.MOp) + L.S(") starts", ") beginnt");
                case 6: return L.S("micro program counter + 1", "Mikroprogrammzähler + 1");
                case 7: return L.S("micro program counter := 0, next instruction", "Mikroprogrammzähler := 0, nächster Befehl");
                case 8: return L.S("address bus := program counter = ", "Adressbus := Programmzähler = ") + ab;
                case 9: return L.S("program counter = ", "Programmzähler = ") + pc;
                case 10: return cpu.Acc == 0 ? L.S("acc is 0: program counter = ", "Akku ist 0: Programmzähler = ") + pc : L.S("acc is not 0: program counter stays ", "Akku ist nicht 0: Programmzähler bleibt ") + pc;
                case 11: return L.S("program counter := ", "Programmzähler := ") + pc + L.S(" (jump)", " (Sprung)");
                case 12: return L.S("acc := 0", "Akku := 0");
                case 13: return L.S("acc := acc + data bus = ", "Akku := Akku + Datenbus = ") + Fv(cpu.Acc);
                case 14: return L.S("acc := acc - data bus = ", "Akku := Akku - Datenbus = ") + Fv(cpu.Acc);
                case 15: return L.S("data bus := acc = ", "Datenbus := Akku = ") + Fv(cpu.Db);
                case 16: case 17: return L.S("acc = ", "Akku = ") + Fv(cpu.Acc);
                case 18: return L.S("acc := data bus = ", "Akku := Datenbus = ") + Fv(cpu.Acc);
                case 19: return L.S("program stopped", "Programm angehalten");
            }
            return "";
        }

        void AddLog(string at, int op) { AddLogText(at, Micro.Ops[op].Name, Effect(op)); }

        void AddLogText(string at, string name, string effect)
        {
            logNo++;
            log.Add(new[] { logNo.ToString(), at, name, effect });
            if (log.Count > 20000) log.RemoveRange(0, 5000);
            logDirty = true;
        }

        void AddLogSeparator(string text)
        {
            log.Add(new[] { "", "", "", "—— " + text + " ——" });
            logDirty = true;
        }

        void ClearLog() { log.Clear(); logNo = 0; logDirty = true; RefreshLog(); }

        void RefreshLog()
        {
            if (!logDirty || tabs.SelectedTab != tpLog) return;
            logDirty = false;
            logView.VirtualListSize = log.Count;
            if (log.Count > 0) logView.EnsureVisible(log.Count - 1);
            logView.Invalidate();
        }

        // ================================================================ Anzeige aktualisieren
        void RefreshAll(bool follow)
        {
            view.Running = running;
            view.HideControlUnit = settings.HideCu;
            view.Bonsai = setKey == "bonsai";
            view.Invalidate();
            grid.Invalidate();
            if (follow && settings.Follow) EnsureRowVisible(cpu.Pc);

            stMacro.Text = L.S("Instructions: ", "Befehle: ") + cpu.MacroCount;
            stMicro.Text = L.S("Cycles: ", "Takte: ") + cpu.MicroCount;
            stPc.Text = "PC: " + cpu.Pc.ToString("000");
            stState.Text = recOp >= 0 ? L.S("Recording", "Aufnahme") : running ? L.S("Running", "Läuft") : halted ? L.S("End of program", "Programmende") : L.S("Ready", "Bereit");
            stState.ForeColor = recOp >= 0 ? Color.FromArgb(196, 43, 28) : running ? Color.FromArgb(16, 124, 16) : Color.Black;
            if (!running)
            {
                int n = cpu.NextMicroOp;
                if (statusMessage != null) { stNext.Text = statusMessage; statusMessage = null; }
                else stNext.Text = n >= 0 ? L.S("Next micro instruction:  ", "Nächster Mikrobefehl:  ") + Micro.Ops[n].Name + "  –  " + Micro.Ops[n].Description
                                          : L.S("The micro program does not end with \"mc:=0\". Please reset or check the microcode.", "Das Mikroprogramm endet ohne \"mc:=0\". Bitte Reset ausführen oder den Mikrocode prüfen.");
            }
            else stNext.Text = settings.Speed >= 10 ? L.S("Program running in turbo mode …", "Programm läuft im Turbo-Modus …") : L.S("Program running …", "Programm läuft …");

            tbMicro.Enabled = tbMacro.Enabled = miMicro.Enabled = miMacro.Enabled = tbRun.Enabled = !running;
            tbPause.Enabled = running;
            view.SetInputsEnabled(!running);
            tbRecord.Checked = recOp >= 0;
            miRun.Text = running ? "&Pause" : "&Start";
            tbUndo.Enabled = miUndo.Enabled = history.Count > 0 || asm.Focused;
            miRecord.Text = recOp >= 0 ? L.S("&Stop recording", "&Aufnahme beenden") : L.S("&Record instruction…", "Befehl &aufnehmen…");
            RefreshLog();
            UpdateTitle();
        }

        string statusMessage;
        void SetStatus(string s) { statusMessage = s; if (stNext != null) stNext.Text = s; }

        void SetDirty() { if (!dirty) { dirty = true; UpdateTitle(); } }

        void UpdateTitle()
        {
            string t = DocTitle + (dirty ? " *" : "") + " – Johnny";
            if (Text != t) Text = t;
        }

        // ================================================================ Befehlssatz und Mikrocode
        void ChangeSet(string key)
        {
            if (key == setKey) { UpdateSetUi(); return; }
            if (key == "custom") { MarkCustom(); OpenMicrocodeEditor(); return; }
            if (setKey == "custom" && MessageBox.Show(this, L.S("The custom microcode will be replaced by the instruction set \"" + InstructionSets.ByKey(key).Title + "\".\n\nContinue?",
                "Der eigene Mikrocode wird durch den Befehlssatz \"" + InstructionSets.ByKey(key).Title + "\" ersetzt.\n\nFortfahren?"),
                L.S("Change instruction set", "Befehlssatz wechseln"), MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) { UpdateSetUi(); return; }
            PauseIfRunning();
            LoadSet(key);
            SetStatus(L.S("Instruction set: ", "Befehlssatz: ") + InstructionSets.ByKey(key).Title + L.S(". The memory content is kept.", ". Der Speicherinhalt bleibt erhalten."));
            RefreshAll(false);
        }

        void LoadSet(string key)
        {
            InstructionSet s = InstructionSets.ByKey(key);
            if (s == null) return;
            if (recOp >= 0) StopRecording(null);
            cpu.LoadSet(s); setKey = key;
            UpdateSetUi();
            grid.Invalidate();
        }

        void MarkCustom() { if (setKey != "custom") { setKey = "custom"; UpdateSetUi(); } }

        void UpdateSetUi()
        {
            updatingUi = true;
            try
            {
                int idx = InstructionSets.All.Length;
                for (int i = 0; i < InstructionSets.All.Length; i++) if (InstructionSets.All[i].Key == setKey) idx = i;
                if (cbSet.SelectedIndex != idx) cbSet.SelectedIndex = idx;
                foreach (ToolStripMenuItem mi in miSets) mi.Checked = (string)mi.Tag == setKey;
            }
            finally { updatingUi = false; }
        }

        void OpenMicrocodeEditor()
        {
            PauseIfRunning();
            using (MicrocodeEditor ed = new MicrocodeEditor(cpu, mono))
            {
                if (ed.ShowDialog(this) != DialogResult.OK) { UpdateSetUi(); return; }
                bool same = ed.Mpm.SequenceEqual(cpu.Mpm) && ed.Names.SequenceEqual(cpu.Names);
                if (same) { UpdateSetUi(); return; }
                Array.Copy(ed.Mpm, cpu.Mpm, 200); cpu.Names = Cpu.FixNames(ed.Names);
                setKey = "custom";
                foreach (InstructionSet s in InstructionSets.All)
                    if (s.Mpm.SequenceEqual(cpu.Mpm) && s.Names.SequenceEqual(cpu.Names)) setKey = s.Key;
                UpdateSetUi();
                SetStatus(L.S("Microcode applied.", "Mikrocode übernommen."));
                RefreshAll(false);
            }
        }

        void ToggleRecording()
        {
            if (recOp >= 0) { StopRecording(L.S("Recording finished.", "Aufnahme beendet.")); return; }
            PauseIfRunning();
            using (RecordDialog d = new RecordDialog(cpu))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                recOp = d.Opcode; recIdx = recOp * 10;
                cpu.Names[recOp] = d.InstructionName;
                for (int k = 0; k < 10; k++) cpu.Mpm[recOp * 10 + k] = 0;
                MarkCustom();
                UpdateRecordingText();
                SetStatus(L.S("Recording: click the micro instructions in the diagram in the desired order.", "Aufnahme läuft: Mikrobefehle im Schaltbild in der gewünschten Reihenfolge anklicken."));
                RefreshAll(false);
            }
        }

        void UpdateRecordingText()
        {
            view.RecordingText = L.S("Recording opcode ", "Aufnahme Opcode ") + recOp.ToString("00") + (cpu.Names[recOp].Length > 0 ? " (" + cpu.Names[recOp] + ")" : "") +
                ": " + (recIdx - recOp * 10) + L.S(" of 10 steps. Click micro instructions.", " von 10 Schritten. Mikrobefehle anklicken.");
        }

        void StopRecording(string msg)
        {
            recOp = -1; view.RecordingText = null;
            if (msg != null) SetStatus(msg);
            RefreshAll(false);
        }

        void OpenMicrocode()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = L.S("Open microcode", "Mikrocode öffnen"); d.Filter = L.S("Johnny microcode (*.mpc)|*.mpc|Instruction names (*.nam)|*.nam", "Johnny-Mikrocode (*.mpc)|*.mpc|Befehlsnamen (*.nam)|*.nam");
                d.Multiselect = true; if (Directory.Exists(settings.LastDir)) d.InitialDirectory = settings.LastDir;
                if (d.ShowDialog(this) == DialogResult.OK) OpenFiles(d.FileNames);
            }
        }

        void SaveMicrocode()
        {
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Title = L.S("Save microcode", "Mikrocode speichern"); d.Filter = L.S("Johnny microcode (*.mpc)|*.mpc", "Johnny-Mikrocode (*.mpc)|*.mpc"); d.FileName = L.S("microcode.mpc", "mikrocode.mpc");
                if (Directory.Exists(settings.LastDir)) d.InitialDirectory = settings.LastDir;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    Formats.WriteText(d.FileName, Formats.SerializeMpc(cpu.Mpm), false);
                    Formats.WriteText(Path.ChangeExtension(d.FileName, ".nam"), Formats.SerializeNam(cpu.Names), false);
                    SetStatus(L.S("Microcode saved: ", "Mikrocode gespeichert: ") + Path.GetFileName(d.FileName) + L.S(" and ", " und ") + Path.GetFileName(Path.ChangeExtension(d.FileName, ".nam")));
                    RefreshAll(false);
                }
                catch (Exception ex) { ShowIoError(ex); }
            }
        }

        // ================================================================ Assembler
        void AssembleCommand()
        {
            PauseIfRunning();
            AsmResult res = Assembler.Assemble(asm.Text, cpu.Names);
            asmErrors.Items.Clear();
            if (res.Errors.Count > 0)
            {
                foreach (AsmError e in res.Errors) asmErrors.Items.Add(e);
                asmErrors.Visible = true;
                if (!settings.ShowSide) ToggleSidePanel();
                tabs.SelectedTab = tpAsm;
                SetStatus(res.Errors.Count + L.S(" error(s) in the assembler text. Double-click an error message to jump to the line.", " Fehler im Assembler-Text. Doppelklick auf eine Fehlermeldung springt zur Zeile."));
                SystemSounds.Beep.Play();
                RefreshAll(false);
                return;
            }
            asmErrors.Visible = false;
            PushHistory();
            cpu.Ram = res.Ram; cpu.ResetRegisters(); halted = false;
            view.Prev = null; view.PrevRam = null; prevRam = null; view.Active.Clear();
            SetDirty();
            AddLogSeparator(L.S("Assembler text loaded", "Assembler-Text übertragen"));
            SelectRow(0, false); ScrollToTop();
            SetStatus(res.Count + L.S(" memory cells written to memory. Registers were reset.", " Speicherzellen in den Arbeitsspeicher geschrieben. Register wurden zurückgesetzt."));
            RefreshAll(false);
        }

        void DisassembleCommand()
        {
            if (asm.Text.Trim().Length > 0 && MessageBox.Show(this, L.S("The text in the assembler window will be replaced by the current memory content.", "Der Text im Assembler-Fenster wird durch den aktuellen Speicherinhalt ersetzt."), L.S("Get from memory", "Aus dem Speicher holen"),
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            asm.Text = Assembler.Disassemble(cpu.Ram, cpu.Names);
            asmErrors.Visible = false;
        }

        void GotoAsmError()
        {
            AsmError e = asmErrors.SelectedItem as AsmError;
            if (e == null) return;
            int start = asm.GetFirstCharIndexFromLine(e.Line - 1);
            if (start < 0) return;
            int end = asm.Text.IndexOf('\n', start); if (end < 0) end = asm.TextLength;
            asm.Focus(); asm.Select(start, Math.Max(0, end - start - (end > 0 && end <= asm.TextLength && asm.Text[end - 1] == '\r' ? 1 : 0)));
            asm.ScrollToCaret();
        }

        // ================================================================ Dateien
        bool ConfirmDiscard()
        {
            if (!dirty || TestMode) return true;
            DialogResult r = MessageBox.Show(this, L.S("Do you want to save the changes to \"" + DocTitle + "\"?", "Möchten Sie die Änderungen an \"" + DocTitle + "\" speichern?"), "Johnny", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (r == DialogResult.Cancel) return false;
            if (r == DialogResult.Yes) return Save();
            return true;
        }

        void ResetDocumentState(string path, string name)
        {
            cpu.ResetRegisters(); halted = false;
            history.Clear(); breakpoints.Clear();
            view.Prev = null; view.PrevRam = null; prevRam = null; view.Active.Clear(); view.LastAluOp = -1;
            filePath = path; docName = name; dirty = false;
            SelectRow(0, false); ScrollToTop();
        }

        void NewDocument()
        {
            PauseIfRunning();
            if (!ConfirmDiscard()) return;
            cpu.Ram = new int[Cpu.RamSize];
            asm.Clear(); asmErrors.Visible = false;
            ResetDocumentState(null, null);
            AddLogSeparator(L.S("New", "Neu"));
            SetStatus(L.S("New program.", "Neues Programm."));
            RefreshAll(false);
        }

        void OpenDialog()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = L.S("Open", "Öffnen");
                d.Filter = L.S("All Johnny files (*.ram;*.bma;*.bij;*.asm;*.mpc;*.nam)|*.ram;*.bma;*.bij;*.asm;*.mpc;*.nam|" +
                               "Johnny program (*.ram)|*.ram|Bonsai program (*.bma;*.bij)|*.bma;*.bij|Assembler text (*.asm)|*.asm|" +
                               "Microcode (*.mpc;*.nam)|*.mpc;*.nam|All files (*.*)|*.*",
                               "Alle Johnny-Dateien (*.ram;*.bma;*.bij;*.asm;*.mpc;*.nam)|*.ram;*.bma;*.bij;*.asm;*.mpc;*.nam|" +
                               "Johnny-Programm (*.ram)|*.ram|Bonsai-Programm (*.bma;*.bij)|*.bma;*.bij|Assembler-Text (*.asm)|*.asm|" +
                               "Mikrocode (*.mpc;*.nam)|*.mpc;*.nam|Alle Dateien (*.*)|*.*");
                d.Multiselect = true;
                if (Directory.Exists(settings.LastDir)) d.InitialDirectory = settings.LastDir;
                if (d.ShowDialog(this) == DialogResult.OK) OpenFiles(d.FileNames);
            }
        }

        static string Ext(string p) { return Path.GetExtension(p).ToLowerInvariant(); }

        public void OpenFiles(string[] paths)
        {
            if (paths == null || paths.Length == 0) return;
            PauseIfRunning();
            try
            {
                settings.LastDir = Path.GetDirectoryName(Path.GetFullPath(paths[0]));
                string mpc = paths.FirstOrDefault(p => Ext(p) == ".mpc"), nam = paths.FirstOrDefault(p => Ext(p) == ".nam");
                if (mpc != null || nam != null)
                {
                    if (mpc != null)
                    {
                        Array.Copy(Formats.ParseMpc(Formats.ReadText(mpc)), cpu.Mpm, 200);
                        string side = Path.ChangeExtension(mpc, ".nam");
                        if (nam == null && File.Exists(side)) nam = side;
                    }
                    if (nam != null) cpu.Names = Formats.ParseNam(Formats.ReadText(nam));
                    string bn = Path.GetFileNameWithoutExtension(mpc ?? nam).ToLowerInvariant();
                    setKey = "custom";
                    foreach (InstructionSet s in InstructionSets.All)
                        if (s.Mpm.SequenceEqual(cpu.Mpm) && s.Names.SequenceEqual(cpu.Names)) setKey = s.Key;
                    if (setKey == "custom" && bn == "standard_bonsai") setKey = "bonsai";
                    UpdateSetUi();
                    SetStatus(L.S("Microcode loaded: ", "Mikrocode geladen: ") + Path.GetFileName(mpc ?? nam));
                }
                string doc = paths.FirstOrDefault(p => Ext(p) != ".mpc" && Ext(p) != ".nam");
                if (doc != null)
                {
                    if (!ConfirmDiscard()) { RefreshAll(false); return; }
                    string ext = Ext(doc), text = Formats.ReadText(doc), name = Path.GetFileName(doc);
                    if (ext == ".bma" || ext == ".bij")
                    {
                        if (setKey != "bonsai") LoadSet("bonsai");
                        cpu.Ram = ext == ".bma" ? Formats.ParseBma(text) : Formats.ParseRam(text);
                        ResetDocumentState(ext == ".bma" ? doc : null, name);
                        SetStatus(L.S("Opened: ", "Geöffnet: ") + name + L.S(" (instruction set Bonsai)", " (Befehlssatz Bonsai)"));
                    }
                    else if (ext == ".ram" || (ext != ".asm" && Formats.IsNumberFile(text)))
                    {
                        cpu.Ram = Formats.ParseRam(text);
                        ResetDocumentState(ext == ".ram" ? doc : null, name);
                        SetStatus(L.S("Opened: ", "Geöffnet: ") + name);
                    }
                    else
                    {
                        asm.Text = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
                        AsmResult res = Assembler.Assemble(text, cpu.Names);
                        asmErrors.Items.Clear();
                        tabs.SelectedTab = tpAsm;
                        if (res.Errors.Count > 0)
                        {
                            foreach (AsmError e in res.Errors) asmErrors.Items.Add(e);
                            asmErrors.Visible = true;
                            if (!settings.ShowSide) ToggleSidePanel();
                            ResetDocumentState(ext == ".asm" ? doc : null, name);
                            SetStatus(L.S("The assembler text contains ", "Der Assembler-Text enthält ") + res.Errors.Count + L.S(" error(s) and was not loaded into memory.", " Fehler und wurde nicht in den Speicher übertragen."));
                        }
                        else
                        {
                            asmErrors.Visible = false;
                            cpu.Ram = res.Ram;
                            ResetDocumentState(ext == ".asm" ? doc : null, name);
                            SetStatus(L.S("Opened: ", "Geöffnet: ") + name + " (" + res.Count + L.S(" memory cells)", " Speicherzellen)"));
                        }
                    }
                    AddLogSeparator(L.S("Opened: ", "Geöffnet: ") + name);
                }
            }
            catch (Exception ex) { ShowIoError(ex); }
            RefreshAll(false);
        }

        bool Save()
        {
            if (filePath == null) return SaveAs();
            string ext = Ext(filePath);
            if (ext != ".ram" && ext != ".bma" && ext != ".asm") return SaveAs();
            return WriteDocument(filePath);
        }

        bool SaveAs()
        {
            CommitEdit();
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Title = L.S("Save As", "Speichern unter");
                d.Filter = L.S("Johnny program (*.ram)|*.ram|Bonsai program (*.bma)|*.bma|Assembler text (*.asm)|*.asm", "Johnny-Programm (*.ram)|*.ram|Bonsai-Programm (*.bma)|*.bma|Assembler-Text (*.asm)|*.asm");
                string baseName = docName == null ? L.S("program", "programm") : Path.GetFileNameWithoutExtension(docName);
                d.FileName = baseName;
                d.FilterIndex = setKey == "bonsai" ? 2 : 1;
                if (Directory.Exists(settings.LastDir)) d.InitialDirectory = settings.LastDir;
                if (d.ShowDialog(this) != DialogResult.OK) return false;
                return WriteDocument(d.FileName);
            }
        }

        bool WriteDocument(string path)
        {
            CommitEdit();
            string ext = Ext(path), text;
            if (ext == ".bma")
            {
                if (setKey != "bonsai")
                {
                    MessageBox.Show(this, L.S("Only programs in the Bonsai instruction set can be saved as Bonsai files.", "Nur Programme im Befehlssatz Bonsai können als Bonsai-Datei gespeichert werden."), L.S("Save", "Speichern"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
                List<string> pr = Formats.CheckBonsai(cpu.Ram);
                if (pr.Count > 0 && MessageBox.Show(this, string.Join("\n", pr.ToArray()) + L.S("\n\nSave anyway?", "\n\nTrotzdem speichern?"), L.S("Save", "Speichern"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return false;
                text = Formats.SerializeBma(cpu.Ram);
            }
            else if (ext == ".asm")
            {
                AsmResult check = Assembler.Assemble(asm.Text, cpu.Names);
                bool editorMatches = asm.Text.Trim().Length > 0 && check.Errors.Count == 0 && check.Ram.SequenceEqual(cpu.Ram);
                if (!editorMatches) asm.Text = Assembler.Disassemble(cpu.Ram, cpu.Names);
                text = asm.Text.Replace("\r\n", "\n").Replace("\n", "\r\n");
            }
            else text = Formats.SerializeRam(cpu.Ram);
            try
            {
                Formats.WriteText(path, text, ext == ".asm");
                filePath = path; docName = Path.GetFileName(path); dirty = false;
                settings.LastDir = Path.GetDirectoryName(path);
                SetStatus(L.S("Saved: ", "Gespeichert: ") + docName);
                RefreshAll(false);
                return true;
            }
            catch (Exception ex) { ShowIoError(ex); return false; }
        }

        void ShowIoError(Exception ex)
        {
            MessageBox.Show(this, L.S("The file could not be read or written.\n\n", "Die Datei konnte nicht gelesen oder geschrieben werden.\n\n") + ex.Message, "Johnny", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        void LoadExample(Example ex)
        {
            PauseIfRunning();
            if (!ConfirmDiscard()) return;
            if (setKey == "custom" && MessageBox.Show(this, L.S("The example needs the instruction set \"" + InstructionSets.ByKey(ex.SetKey).Title + "\". The custom microcode will be replaced.\n\nContinue?",
                "Das Beispiel braucht den Befehlssatz \"" + InstructionSets.ByKey(ex.SetKey).Title + "\". Der eigene Mikrocode wird ersetzt.\n\nFortfahren?"),
                L.S("Load example", "Beispiel laden"), MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            if (setKey != ex.SetKey) LoadSet(ex.SetKey);
            asm.Text = ex.Source.Replace("\r\n", "\n").Replace("\n", "\r\n");
            asmErrors.Visible = false;
            cpu.Ram = Assembler.Assemble(ex.Source, cpu.Names).Ram;
            ResetDocumentState(null, ex.FileName + (ex.SetKey == "bonsai" ? ".bma" : ".ram"));
            AddLogSeparator(L.S("Example: ", "Beispiel: ") + ex.Title);
            SetStatus(L.S("Example \"" + ex.Title + "\" loaded. F8 = one instruction, F7 = one micro instruction, F5 = start.", "Beispiel \"" + ex.Title + "\" geladen. F8 = ein Befehl, F7 = ein Mikrobefehl, F5 = Start."));
            RefreshAll(false);
        }

        void OnDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        void OnDragDrop(object sender, DragEventArgs e)
        {
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null) BeginInvoke((Action)delegate { OpenFiles(files); });
        }

        // ================================================================ Tastatur, Schließen
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && running) { PauseRun(false); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!TestMode && !closingConfirmed)
            {
                PauseIfRunning();
                if (!ConfirmDiscard()) { e.Cancel = true; return; }
                closingConfirmed = true;
            }
            if (!TestMode)
            {
                settings.Maximized = WindowState == FormWindowState.Maximized;
                settings.Bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                if (!split.Panel2Collapsed) settings.RightWidth = split.Width - split.SplitterDistance;
                settings.Save();
            }
            base.OnFormClosing(e);
        }

        // ================================================================ Selbsttest der Bedienung (nur zur Prüfung)
        bool Pump(int maxMs)
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (running && sw.ElapsedMilliseconds < maxMs) { Application.DoEvents(); System.Threading.Thread.Sleep(1); }
            return !running;
        }

        public string SelfTest()
        {
            StringBuilder r = new StringBuilder();
            int fails = 0;
            Action<bool, string> ok = delegate(bool c, string m) { r.AppendLine((c ? "OK     " : "FEHLER ") + m); if (!c) fails++; };
            LoadExample(Examples.All[0]);
            SetSpeed(10); StartRun();
            ok(Pump(10000) && halted && cpu.Ram[12] == 15, "Turbo-Lauf Multiplikation, RAM[12]=" + cpu.Ram[12]);
            ResetCpu(); cpu.Ram[11] = 3; cpu.Ram[12] = 0;
            SetSpeed(9); settings.MicroRun = true; StartRun();
            ok(Pump(20000) && halted && cpu.Ram[12] == 15, "Lauf in Mikroschritten, RAM[12]=" + cpu.Ram[12] + ", Takte " + cpu.MicroCount);
            ResetCpu(); cpu.Ram[11] = 3; cpu.Ram[12] = 0;
            settings.MicroRun = false; StartRun();
            ok(Pump(20000) && halted && cpu.Ram[12] == 15, "Lauf in Makroschritten, Befehle " + cpu.MacroCount);
            LoadExample(Examples.All[0]);
            breakpoints.Add(4); SetSpeed(10); StartRun();
            ok(Pump(5000) && !halted && cpu.Pc == 4 && cpu.AtFetchStart, "Haltepunkt bei 004, PC=" + cpu.Pc);
            breakpoints.Clear();
            SelectRow(15, false); grid.BeginEdit(true);
            grid.EditingControl.Text = "take 10"; grid.EndEdit();
            ok(cpu.Ram[15] == 1010, "Zelle 015 bearbeitet: " + cpu.Ram[15]);
            ok(dirty, "Dokument als geändert markiert");
            grid.Focus(); UndoCommand();
            ok(cpu.Ram[15] == 0, "Rückgängig: " + cpu.Ram[15]);
            SelectRow(1, false); InsertRow();
            ok(cpu.Ram[7] == 5002 && cpu.Ram[1] == 0 && cpu.Ram[2] == 1012, "Zeile eingefügt, JMP angepasst: " + cpu.Ram[7]);
            UndoCommand();
            ok(cpu.Ram[6] == 5001, "Einfügen rückgängig");
            asm.Text = Examples.All[2].Source; AssembleCommand();
            ok(cpu.Ram[0] == 1020 && cpu.Ram[4] == 5007 && !asmErrors.Visible, "Assembler mit Sprungmarken");
            asm.Text = "TAKE 10" + Environment.NewLine + "FOO 3"; AssembleCommand();
            ok(asmErrors.Visible && asmErrors.Items.Count == 1 && cpu.Ram[0] == 1020, "Assembler-Fehler werden angezeigt");
            LoadExample(Examples.All[5]);
            ok(setKey == "bonsai", "Bonsai-Beispiel schaltet Befehlssatz um");
            SetSpeed(10); StartRun();
            ok(Pump(5000) && halted && cpu.Ram[8] == 5, "Bonsai-Addition, RAM[8]=" + cpu.Ram[8]);
            ManualOp(12);
            ok(cpu.Acc == 0 && log.Count > 0, "Mikrobefehl von Hand");
            ok(log.Count > 0, "Protokoll hat " + log.Count + " Einträge");
            r.AppendLine(fails == 0 ? "ALLE TESTS BESTANDEN" : fails + " FEHLER");
            return r.ToString();
        }

        // ================================================================ für Bildschirmfotos (Prüfung)
        public void TestLoadExample(int i, int macroSteps, int microSteps)
        {
            if (TestHideCu) { settings.HideCu = true; UpdateChecks(); }
            if (TestShowCu) { settings.HideCu = false; UpdateChecks(); }
            LoadExample(Examples.All[i]);
            for (int k = 0; k < macroSteps; k++) MacroStepCommand();
            for (int k = 0; k < microSteps; k++) MicroStepCommand();
            if (breakpoints.Count == 0) breakpoints.Add(5);
            RefreshAll(true);
        }
    }
}
