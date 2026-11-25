// Johnny – Dialoge: Eingabe, Aufnahme, Mikrocode-Editor, Hilfe, Info. Einstellungen. GPLv3, siehe LICENSE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace Johnny
{
    // ---------------------------------------------------------------- gemeinsame Grundlage
    public class DialogBase : Form
    {
        protected readonly FlowLayoutPanel Buttons;
        protected readonly float Dpi;

        public DialogBase(string title)
        {
            Text = title;
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false; ShowIcon = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.None;
            using (Graphics g = CreateGraphics()) Dpi = g.DpiX / 96f;
            Buttons = new FlowLayoutPanel();
            Buttons.Dock = DockStyle.Bottom; Buttons.FlowDirection = FlowDirection.RightToLeft;
            Buttons.AutoSize = true; Buttons.Padding = new Padding(S(10), S(8), S(10), S(10));
            Buttons.BackColor = Color.FromArgb(240, 240, 240);
            Controls.Add(Buttons);
        }

        protected int S(int px) { return (int)Math.Round(px * Dpi); }

        protected Button AddButton(string text, DialogResult result)
        {
            Button b = new Button();
            b.Text = text; b.DialogResult = result; b.AutoSize = true;
            b.MinimumSize = new Size(S(84), S(26)); b.Margin = new Padding(S(6), 0, 0, 0);
            Buttons.Controls.Add(b);
            if (result == DialogResult.OK) AcceptButton = b;
            if (result == DialogResult.Cancel) CancelButton = b;
            return b;
        }
    }

    // ---------------------------------------------------------------- einfache Eingabe
    public sealed class InputDialog : DialogBase
    {
        readonly TextBox box = new TextBox();
        public string Value { get { return box.Text; } }

        public InputDialog(string title, string prompt, string initial) : base(title)
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill; t.Padding = new Padding(S(14)); t.ColumnCount = 1; t.AutoSize = true;
            Label l = new Label(); l.Text = prompt; l.AutoSize = true; l.MaximumSize = new Size(S(360), 0); l.Margin = new Padding(0, 0, 0, S(8));
            box.Text = initial ?? ""; box.Width = S(360); box.Font = new Font(FontFamily.GenericMonospace, Font.SizeInPoints + 1);
            t.Controls.Add(l); t.Controls.Add(box);
            Controls.Add(t); t.BringToFront();
            AddButton(L.S("Cancel", "Abbrechen"), DialogResult.Cancel);
            AddButton("OK", DialogResult.OK);
            ClientSize = new Size(S(392), S(150));
            AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Shown += delegate { box.SelectAll(); box.Focus(); };
        }
    }

    // ---------------------------------------------------------------- Aufnahme eines Mikroprogramms
    public sealed class RecordDialog : DialogBase
    {
        readonly NumericUpDown op = new NumericUpDown();
        readonly TextBox name = new TextBox();
        public int Opcode { get { return (int)op.Value; } }
        public string InstructionName { get { return name.Text.Trim().ToUpperInvariant(); } }

        public RecordDialog(Cpu cpu) : base(L.S("Record instruction", "Befehl aufnehmen"))
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill; t.Padding = new Padding(S(14)); t.ColumnCount = 2; t.AutoSize = true;
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Label info = new Label();
            info.Text = L.S("After starting, click the micro instructions in the diagram in the desired order. " +
                        "Each click is executed and recorded (at most 10 steps). The last step should be \"mc:=0\".",
                        "Nach dem Start die Mikrobefehle im Schaltbild in der gewünschten Reihenfolge anklicken. " +
                        "Jeder Klick wird ausgeführt und aufgezeichnet (höchstens 10 Schritte). Der letzte Schritt sollte \"mc:=0\" sein.");
            info.AutoSize = true; info.MaximumSize = new Size(S(380), 0); info.Margin = new Padding(0, 0, 0, S(12));
            t.Controls.Add(info, 0, 0); t.SetColumnSpan(info, 2);
            op.Minimum = 1; op.Maximum = 19; op.Width = S(70);
            int free = 1;
            for (int i = 1; i < 20; i++) { bool used = cpu.Names[i].Length > 0; for (int k = 0; k < 10; k++) if (cpu.Mpm[i * 10 + k] != 0) used = true; if (!used) { free = i; break; } }
            op.Value = free;
            name.Width = S(120); name.CharacterCasing = CharacterCasing.Upper; name.MaxLength = 8;
            name.Text = cpu.Names[free];
            op.ValueChanged += delegate { name.Text = cpu.Names[(int)op.Value]; };
            t.Controls.Add(MakeLabel("Opcode:"), 0, 1); t.Controls.Add(op, 1, 1);
            t.Controls.Add(MakeLabel(L.S("Instruction name:", "Befehlsname:")), 0, 2); t.Controls.Add(name, 1, 2);
            Controls.Add(t); t.BringToFront();
            AddButton(L.S("Cancel", "Abbrechen"), DialogResult.Cancel);
            AddButton(L.S("Start recording", "Aufnahme starten"), DialogResult.OK);
            AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
        }

        Label MakeLabel(string s)
        {
            Label l = new Label(); l.Text = s; l.AutoSize = true; l.Anchor = AnchorStyles.Left; l.Margin = new Padding(0, S(6), S(10), S(6));
            return l;
        }
    }

    // ---------------------------------------------------------------- Mikrocode-Editor
    public sealed class MicrocodeEditor : DialogBase
    {
        readonly DataGridView grid = new DataGridView();
        public int[] Mpm = new int[200];
        public string[] Names = new string[20];

        public MicrocodeEditor(Cpu cpu, string monoFamily) : base(L.S("Microcode Editor", "Mikrocode-Editor"))
        {
            FormBorderStyle = FormBorderStyle.Sizable; MinimizeBox = false; MaximizeBox = true;
            Array.Copy(cpu.Mpm, Mpm, 200); Names = (string[])cpu.Names.Clone();

            Label info = new Label();
            info.Text = L.S("Each row is a machine instruction (opcode 00 is the fetch phase). Columns 0 to 9 are the micro instructions " +
                        "that are executed one after the other. Each micro program should end with \"mc:=0\".",
                        "Jede Zeile ist ein Maschinenbefehl (Opcode 00 ist die Holphase). Die Spalten 0 bis 9 sind die Mikrobefehle, " +
                        "die nacheinander ausgeführt werden. Jedes Mikroprogramm sollte mit \"mc:=0\" enden.");
            info.Dock = DockStyle.Top; info.Padding = new Padding(S(12), S(10), S(12), S(8)); info.AutoSize = false; info.Height = S(52);

            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false; grid.SelectionMode = DataGridViewSelectionMode.CellSelect; grid.MultiSelect = false;
            grid.EditMode = DataGridViewEditMode.EditOnEnter; grid.BackgroundColor = SystemColors.Window; grid.BorderStyle = BorderStyle.None;
            grid.GridColor = Color.FromArgb(225, 225, 225);
            grid.DefaultCellStyle.Font = new Font(monoFamily, 9.5f);
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = S(26);
            grid.RowTemplate.Height = S(24);

            DataGridViewTextBoxColumn cOp = new DataGridViewTextBoxColumn();
            cOp.HeaderText = "Opcode"; cOp.ReadOnly = true; cOp.Width = S(64); cOp.DefaultCellStyle.ForeColor = Color.FromArgb(100, 100, 100);
            grid.Columns.Add(cOp);
            DataGridViewTextBoxColumn cName = new DataGridViewTextBoxColumn();
            cName.HeaderText = "Name"; cName.Width = S(92); cName.MaxInputLength = 8;
            grid.Columns.Add(cName);
            string[] opNames = new string[Micro.Ops.Length];
            for (int i = 0; i < opNames.Length; i++) opNames[i] = Micro.Ops[i].Name;
            for (int k = 0; k < 10; k++)
            {
                DataGridViewComboBoxColumn c = new DataGridViewComboBoxColumn();
                c.HeaderText = k.ToString(); c.Items.AddRange(opNames); c.MinimumWidth = S(70);
                c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                c.DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing; c.FlatStyle = FlatStyle.Flat;
                grid.Columns.Add(c);
            }
            Fill();
            grid.CellFormatting += delegate(object s, DataGridViewCellFormattingEventArgs e)
            {
                if (e.ColumnIndex >= 2 && "---".Equals(e.Value)) e.CellStyle.ForeColor = Color.FromArgb(175, 175, 175);
                if (e.RowIndex == 0) { e.CellStyle.BackColor = Color.FromArgb(246, 247, 248); if (e.ColumnIndex == 1) e.CellStyle.ForeColor = Color.FromArgb(110, 110, 110); }
            };
            grid.CellValidating += delegate(object s, DataGridViewCellValidatingEventArgs e)
            {
                if (e.ColumnIndex != 1 || e.RowIndex == 0) return;
                string v = (e.FormattedValue as string ?? "").Trim().ToUpperInvariant();
                if (v.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(v, "^[A-Z_][A-Z0-9_]*$"))
                { MessageBox.Show(this, L.S("Instruction names consist of letters, digits and _ and start with a letter.", "Befehlsnamen bestehen aus Buchstaben, Ziffern und _ und beginnen mit einem Buchstaben."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information); e.Cancel = true; }
            };
            grid.CellEndEdit += delegate(object s, DataGridViewCellEventArgs e)
            {
                if (e.ColumnIndex == 1) grid[1, e.RowIndex].Value = (Convert.ToString(grid[1, e.RowIndex].Value) ?? "").Trim().ToUpperInvariant();
            };

            Controls.Add(grid); Controls.Add(info);
            grid.BringToFront();

            AddButton(L.S("Cancel", "Abbrechen"), DialogResult.Cancel);
            Button ok = AddButton("OK", DialogResult.OK);
            ok.Click += delegate { if (!Collect()) DialogResult = DialogResult.None; };
            Button open = AddButton(L.S("Open…", "Öffnen…"), DialogResult.None);
            open.Click += delegate { OpenFile(); };
            Button saveAs = AddButton(L.S("Save As…", "Speichern unter…"), DialogResult.None);
            saveAs.Click += delegate { if (Collect()) SaveFile(); };
            Button tmpl = AddButton(L.S("Template ▾", "Vorlage ▾"), DialogResult.None);
            ContextMenuStrip cm = new ContextMenuStrip();
            foreach (InstructionSet set in InstructionSets.All)
            {
                InstructionSet s2 = set;
                cm.Items.Add(set.Title, null, delegate { Array.Copy(s2.Mpm, Mpm, 200); Names = Cpu.FixNames(s2.Names); Fill(); });
            }
            tmpl.Click += delegate { cm.Show(tmpl, new Point(0, tmpl.Height)); };
            foreach (Control c in new Control[] { open, saveAs, tmpl }) c.Margin = new Padding(S(6), 0, 0, 0);

            Size = new Size(S(1000), S(680));
            MinimumSize = new Size(S(700), S(400));
        }

        void Fill()
        {
            grid.Rows.Clear();
            for (int op = 0; op < 20; op++)
            {
                object[] row = new object[12];
                row[0] = op.ToString("00"); row[1] = op == 0 ? L.S("Fetch", "Holphase") : Names[op];
                for (int k = 0; k < 10; k++) row[2 + k] = Micro.Ops[Mpm[op * 10 + k]].Name;
                grid.Rows.Add(row);
            }
            grid.Rows[0].Cells[1].ReadOnly = true;
        }

        bool Collect()
        {
            grid.EndEdit();
            int[] m = new int[200]; string[] n = new string[20];
            Dictionary<string, int> seen = new Dictionary<string, int>();
            for (int op = 0; op < 20; op++)
            {
                n[op] = op == 0 ? "" : (Convert.ToString(grid[1, op].Value) ?? "").Trim().ToUpperInvariant();
                if (n[op].Length > 0 && n[op] != "NN")
                {
                    if (seen.ContainsKey(n[op]))
                    {
                        MessageBox.Show(this, L.S("The instruction name \"", "Der Befehlsname \"") + n[op] + L.S("\" occurs twice (opcode ", "\" kommt doppelt vor (Opcode ") + seen[n[op]].ToString("00") + L.S(" and ", " und ") + op.ToString("00") + ").", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return false;
                    }
                    seen[n[op]] = op;
                }
                for (int k = 0; k < 10; k++) m[op * 10 + k] = Micro.IndexOf(Convert.ToString(grid[2 + k, op].Value));
            }
            Mpm = m; Names = n;
            return true;
        }

        void OpenFile()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = L.S("Open microcode", "Mikrocode öffnen"); d.Filter = L.S("Johnny microcode (*.mpc)|*.mpc", "Johnny-Mikrocode (*.mpc)|*.mpc");
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    Mpm = Formats.ParseMpc(Formats.ReadText(d.FileName));
                    string nam = Path.ChangeExtension(d.FileName, ".nam");
                    if (File.Exists(nam)) Names = Formats.ParseNam(Formats.ReadText(nam));
                    Fill();
                }
                catch (Exception ex) { MessageBox.Show(this, L.S("The file could not be read:\n", "Die Datei konnte nicht gelesen werden:\n") + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        void SaveFile()
        {
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Title = L.S("Save microcode", "Mikrocode speichern"); d.Filter = L.S("Johnny microcode (*.mpc)|*.mpc", "Johnny-Mikrocode (*.mpc)|*.mpc"); d.FileName = L.S("microcode.mpc", "mikrocode.mpc");
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    Formats.WriteText(d.FileName, Formats.SerializeMpc(Mpm), false);
                    Formats.WriteText(Path.ChangeExtension(d.FileName, ".nam"), Formats.SerializeNam(Names), false);
                }
                catch (Exception ex) { MessageBox.Show(this, L.S("The file could not be saved:\n", "Die Datei konnte nicht gespeichert werden:\n") + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }
    }

    // ---------------------------------------------------------------- Hilfe
    public sealed class HelpForm : DialogBase
    {
        public HelpForm(Cpu cpu, InstructionSet set) : base(L.S("Johnny – Help", "Johnny – Hilfe"))
        {
            FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = true;
            TabControl tabs = new TabControl(); tabs.Dock = DockStyle.Fill; tabs.Padding = new Point(S(12), S(4));

            TabPage basics = new TabPage(L.S("Basics", "Grundlagen"));
            TextBox tb = new TextBox();
            tb.Multiline = true; tb.ReadOnly = true; tb.Dock = DockStyle.Fill; tb.BorderStyle = BorderStyle.None; tb.BackColor = SystemColors.Window;
            tb.ScrollBars = ScrollBars.Vertical; tb.Font = new Font(Font.FontFamily, 10f);
            tb.Text = L.S(
                "Johnny is a simplified von Neumann computer. Program and data are stored together in memory (1000 cells, addresses 000 to 999).\r\n\r\n" +
                "Each memory cell holds a number from 0 to 19999. For an instruction, the hi part (the thousands) is the opcode and the lo part (the last three digits) is the address. Example: 1012 means TAKE 012.\r\n\r\n" +
                "The control unit fetches one instruction after another from memory (fetch phase) and executes it. Each machine instruction consists of small micro instructions that move values over the data bus and the address bus. The arithmetic logic unit calculates with the accumulator.\r\n\r\n" +
                "Operation:\r\n" +
                "  • Micro step (F7) executes a single micro instruction.\r\n" +
                "  • Macro step (F8) executes a complete machine instruction.\r\n" +
                "  • Start (F5) runs the program, Esc or F5 pauses it.\r\n" +
                "  • The yellow arrow shows what will be executed next. Red values changed in the last step. Strongly coloured arrows and buses show the path the data took last.\r\n" +
                "  • The buttons in the diagram execute single micro instructions by hand.\r\n" +
                "  • A value can be typed into the fields to the left of the address bus and the data bus. Enter or the arrow puts it on the bus.\r\n" +
                "  • The \"Control unit\" switch (Ctrl+T) hides the control unit. Then everything is operated by hand.\r\n" +
                "  • Assembler and log appear in the side panel (Ctrl+L).\r\n\r\n" +
                "Entering programs:\r\n" +
                "  • Select a row in the memory table and start typing, for example TAKE 10, 1010, 01 010 or 5. Enter moves to the next row.\r\n" +
                "  • Or write the program as text on the \"Assembler\" tab and load it into memory with Ctrl+Enter. Labels (loop:) and comments (; ...) are allowed. \"010:\" sets the address.\r\n" +
                "  • Clicking the left edge of a memory row sets a breakpoint.",
                "Johnny ist ein vereinfachter von-Neumann-Rechner. Programm und Daten liegen gemeinsam im Arbeitsspeicher (1000 Zellen, Adressen 000 bis 999).\r\n\r\n" +
                "Jede Speicherzelle enthält eine Zahl von 0 bis 19999. Bei einem Befehl ist der Hi-Teil (die Tausender) der Opcode und der Lo-Teil (die letzten drei Stellen) die Adresse. Beispiel: 1012 bedeutet TAKE 012.\r\n\r\n" +
                "Das Steuerwerk holt einen Befehl nach dem anderen aus dem Speicher (Holphase) und führt ihn aus. Jeder Maschinenbefehl besteht aus kleinen Mikrobefehlen, die Werte über den Datenbus und den Adressbus bewegen. Das Rechenwerk rechnet mit dem Akkumulator.\r\n\r\n" +
                "Bedienung:\r\n" +
                "  • Mikroschritt (F7) führt einen einzelnen Mikrobefehl aus.\r\n" +
                "  • Makroschritt (F8) führt einen ganzen Maschinenbefehl aus.\r\n" +
                "  • Start (F5) lässt das Programm laufen, Esc oder F5 hält es an.\r\n" +
                "  • Der gelbe Pfeil zeigt, was als Nächstes ausgeführt wird. Rote Werte haben sich im letzten Schritt geändert. Kräftig gefärbte Pfeile und Busse zeigen den Weg, den die Daten zuletzt genommen haben.\r\n" +
                "  • Die Knöpfe im Schaltbild führen einzelne Mikrobefehle von Hand aus.\r\n" +
                "  • In die Felder links neben Adressbus und Datenbus kann ein Wert getippt werden. Enter oder der Pfeil legt ihn auf den Bus.\r\n" +
                "  • Der Schalter \"Steuerwerk\" (Strg+T) blendet das Steuerwerk aus. Dann wird alles von Hand bedient.\r\n" +
                "  • Assembler und Protokoll erscheinen in der Seitenleiste (Strg+L).\r\n\r\n" +
                "Programme eingeben:\r\n" +
                "  • In der Speichertabelle eine Zeile wählen und lostippen, zum Beispiel TAKE 10, 1010, 01 010 oder 5. Enter springt zur nächsten Zeile.\r\n" +
                "  • Oder im Register \"Assembler\" als Text schreiben und mit Strg+Enter in den Speicher übertragen. Sprungmarken (schleife:) und Kommentare (; ...) sind erlaubt. \"010:\" setzt die Adresse.\r\n" +
                "  • Ein Klick auf den linken Rand einer Speicherzeile setzt einen Haltepunkt.");
            basics.Controls.Add(tb); basics.Padding = new Padding(S(10));

            TabPage instr = new TabPage(L.S("Instructions", "Befehle"));
            ListView lv = MakeList(new[] { L.S("Instruction", "Befehl"), "Opcode", L.S("Effect", "Wirkung"), L.S("Micro program", "Mikroprogramm") }, new[] { 80, 64, 300, 360 });
            for (int op = 1; op < 20; op++)
            {
                List<string> seq = new List<string>();
                for (int k = 0; k < 10; k++) if (cpu.Mpm[op * 10 + k] != 0) seq.Add(Micro.Ops[cpu.Mpm[op * 10 + k]].Name);
                if (cpu.Names[op].Length == 0 && seq.Count == 0) continue;
                string help = "";
                if (set != null && set.Help != null && set.Help.ContainsKey(cpu.Names[op])) help = set.Help[cpu.Names[op]];
                lv.Items.Add(new ListViewItem(new[] { cpu.Names[op] + " a", op.ToString("00"), help, string.Join("  ·  ", seq.ToArray()) }));
            }
            instr.Controls.Add(lv);

            TabPage micro = new TabPage(L.S("Micro instructions", "Mikrobefehle"));
            ListView lm = MakeList(new[] { L.S("No.", "Nr."), "Name", L.S("Effect", "Wirkung") }, new[] { 44, 84, 660 });
            for (int i = 1; i < Micro.Ops.Length; i++) lm.Items.Add(new ListViewItem(new[] { i.ToString(), Micro.Ops[i].Name, Micro.Ops[i].Description }));
            micro.Controls.Add(lm);

            TabPage keys = new TabPage(L.S("Keyboard shortcuts", "Tastenkürzel"));
            ListView lk = MakeList(new[] { L.S("Key", "Taste"), L.S("Function", "Funktion") }, new[] { 170, 560 });
            string[,] k2 = L.De ? new string[,] {
                { "F7", "Mikroschritt" }, { "F8", "Makroschritt (ganzer Befehl)" }, { "F5", "Start / Pause" }, { "Esc", "Anhalten" },
                { "Strg+R", "Reset (Register auf 0, Speicher bleibt)" }, { "Strg+Z", "Rückgängig (Schritte und Änderungen)" },
                { "F9", "Haltepunkt an der gewählten Zeile" }, { "Strg+G", "Gehe zu Adresse" },
                { "Strg+T", "Steuerwerk ein- oder ausblenden" }, { "Strg+L", "Assembler und Protokoll ein- oder ausblenden" },
                { "Einfg", "Zeile einfügen (Adressen werden angepasst)" }, { "Umschalt+Entf", "Zeile löschen (Adressen werden angepasst)" },
                { "Entf", "Zelle auf 0 setzen" }, { "F2 oder Tippen", "Zelle bearbeiten" },
                { "Strg+Enter", "Assembler-Text in den Speicher übertragen" },
                { "Strg+N / Strg+O / Strg+S", "Neu / Öffnen / Speichern" }, { "F1", "Diese Hilfe" } } : new string[,] {
                { "F7", "Micro step" }, { "F8", "Macro step (whole instruction)" }, { "F5", "Start / Pause" }, { "Esc", "Pause" },
                { "Ctrl+R", "Reset (registers to 0, memory is kept)" }, { "Ctrl+Z", "Undo (steps and changes)" },
                { "F9", "Breakpoint at the selected row" }, { "Ctrl+G", "Go to address" },
                { "Ctrl+T", "Show or hide the control unit" }, { "Ctrl+L", "Show or hide assembler and log" },
                { "Ins", "Insert row (addresses are adjusted)" }, { "Shift+Del", "Delete row (addresses are adjusted)" },
                { "Del", "Set cell to 0" }, { "F2 or typing", "Edit cell" },
                { "Ctrl+Enter", "Load the assembler text into memory" },
                { "Ctrl+N / Ctrl+O / Ctrl+S", "New / Open / Save" }, { "F1", "This help" } };
            for (int i = 0; i < k2.GetLength(0); i++) lk.Items.Add(new ListViewItem(new[] { k2[i, 0], k2[i, 1] }));
            keys.Controls.Add(lk);

            tabs.TabPages.AddRange(new[] { basics, instr, micro, keys });
            Controls.Add(tabs); tabs.BringToFront();
            tabs.Padding = new Point(S(10), S(4));
            Panel pad = new Panel(); pad.Dock = DockStyle.Top; pad.Height = S(8); Controls.Add(pad);
            AddButton(L.S("Close", "Schließen"), DialogResult.Cancel);
            Size = new Size(S(880), S(580));
            MinimumSize = new Size(S(600), S(380));
        }

        ListView MakeList(string[] cols, int[] widths)
        {
            ListView lv = new ListView();
            lv.View = View.Details; lv.FullRowSelect = true; lv.Dock = DockStyle.Fill; lv.BorderStyle = BorderStyle.None;
            lv.HeaderStyle = ColumnHeaderStyle.Nonclickable; lv.GridLines = false;
            for (int i = 0; i < cols.Length; i++) lv.Columns.Add(cols[i], S(widths[i]));
            return lv;
        }
    }

    // ---------------------------------------------------------------- Info
    public sealed class AboutForm : DialogBase
    {
        public AboutForm() : base(L.S("About Johnny", "Über Johnny"))
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill; t.Padding = new Padding(S(18)); t.ColumnCount = 2; t.AutoSize = true;
            PictureBox pic = new PictureBox(); pic.Image = Icons.AppIcon(S(64)); pic.SizeMode = PictureBoxSizeMode.AutoSize; pic.Margin = new Padding(0, 0, S(16), 0);
            t.Controls.Add(pic, 0, 0); t.SetRowSpan(pic, 8);
            Label title = new Label(); title.Text = "Johnny"; title.Font = new Font(Font.FontFamily, 16f, FontStyle.Bold); title.AutoSize = true;
            Label sub = new Label(); sub.Text = L.S("Simulator of a simple von Neumann computer\nImproved edition 2026", "Simulator eines einfachen von-Neumann-Rechners\nVerbesserte Fassung 2026"); sub.AutoSize = true; sub.Margin = new Padding(S(3), 0, 0, S(12));
            Label body = new Label();
            body.Text = L.S(
                "Based on JOHNNY 1.01, © 2009–2014 Peter Dauscher,\nGymnasium am Kaiserdom, Speyer.\n\n" +
                "This edition was rewritten in 2026 with the help of Claude (Anthropic): " +
                "new user interface, undo, breakpoints, assembler, microcode editor. Micro instructions, microcodes and file formats match the original.\n\n" +
                "This program is free software. It may be redistributed and modified under the terms of the GNU General Public License, version 3. " +
                "It is provided without any warranty. The license text (LICENSE) and the source code " +
                "(folder \"Quellcode\") are on the project page.",
                "Auf Grundlage von JOHNNY 1.01, © 2009–2014 Peter Dauscher,\nGymnasium am Kaiserdom, Speyer.\n\n" +
                "Diese Fassung wurde 2026 mit Unterstützung von Claude (Anthropic) neu geschrieben: " +
                "neue Oberfläche, Rückgängig, Haltepunkte, Assembler, Mikrocode-Editor. Mikrobefehle, Mikrocodes und Dateiformate entsprechen dem Original.\n\n" +
                "Dieses Programm ist freie Software. Es darf unter den Bedingungen der GNU General Public License, Version 3, " +
                "weitergegeben und verändert werden. Es wird ohne jede Gewährleistung bereitgestellt. Lizenztext (LICENSE) und Quelltext " +
                "(Ordner \"Quellcode\") stehen auf der Projektseite.");
            body.AutoSize = true; body.MaximumSize = new Size(S(430), 0); body.Margin = new Padding(S(3), 0, 0, S(12));
            LinkLabel l1 = MakeLink("Original: sourceforge.net/projects/johnnysimulator", "https://sourceforge.net/projects/johnnysimulator/");
            LinkLabel l2 = MakeLink(L.S("Source code of the original: github.com/philip1337/johnny-simulator", "Quellcode des Originals: github.com/philip1337/johnny-simulator"), "https://github.com/philip1337/johnny-simulator");
            t.Controls.Add(title, 1, 0); t.Controls.Add(sub, 1, 1); t.Controls.Add(body, 1, 2); t.Controls.Add(l1, 1, 3); t.Controls.Add(l2, 1, 4);
            if (AppInfo.RepositoryUrl.Length > 0) t.Controls.Add(MakeLink(L.S("This project: ", "Dieses Projekt: ") + AppInfo.RepositoryText, AppInfo.RepositoryUrl), 1, 5);
            Label ded = new Label(); ded.Text = AppInfo.Dedication; ded.AutoSize = true; ded.Font = new Font(Font, FontStyle.Italic);
            ded.Margin = new Padding(S(3), S(10), 0, 0);
            t.Controls.Add(ded, 1, 6);
            Controls.Add(t); t.BringToFront();
            AddButton("OK", DialogResult.OK);
            Button splash = AddButton(L.S("Show splash screen", "Startbild anzeigen"), DialogResult.None);
            splash.Click += delegate { SplashForm f = new SplashForm(false); f.ShowOver(this); };
            AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
        }

        LinkLabel MakeLink(string text, string url)
        {
            LinkLabel l = new LinkLabel(); l.Text = text; l.AutoSize = true; l.Margin = new Padding(S(3), 0, 0, S(4));
            l.LinkClicked += delegate { try { Process.Start(url); } catch (Exception) { } };
            return l;
        }
    }

    // ---------------------------------------------------------------- Einstellungen (Datei im Benutzerprofil)
    public sealed class Settings
    {
        public int Speed = 5;
        public bool MicroRun = true, Follow = true, Adjust = true, HideCu = false, ShowSide = false, ShowSplash = true;
        public string Language = "en";
        public float FontSize = 10f;
        public Rectangle Bounds = Rectangle.Empty;
        public bool Maximized;
        public string LastDir = "";
        public int RightWidth = 0, LeftWidth = 0;

        static string FilePath
        {
            get { return Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Johnny"), "einstellungen.ini"); }
        }

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int eq = line.IndexOf('='); if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                    int i; float f;
                    switch (k)
                    {
                        case "tempo": if (int.TryParse(v, out i)) s.Speed = Math.Max(1, Math.Min(10, i)); break;
                        case "mikroschritte": s.MicroRun = v == "1"; break;
                        case "folgen": s.Follow = v == "1"; break;
                        case "adressen_anpassen": s.Adjust = v == "1"; break;
                        case "steuerwerk_aus": s.HideCu = v == "1"; break;
                        case "seitenleiste": s.ShowSide = v == "1"; break;
                        case "startbild": s.ShowSplash = v == "1"; break;
                        case "sprache": s.Language = v == "de" ? "de" : "en"; break;
                        case "schrift": if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) s.FontSize = Math.Max(8f, Math.Min(18f, f)); break;
                        case "maximiert": s.Maximized = v == "1"; break;
                        case "ordner": s.LastDir = v; break;
                        case "links": int.TryParse(v, out s.LeftWidth); break;
                        case "rechts": int.TryParse(v, out s.RightWidth); break;
                        case "fenster":
                            string[] p = v.Split(',');
                            int x, y, w, h;
                            if (p.Length == 4 && int.TryParse(p[0], out x) && int.TryParse(p[1], out y) && int.TryParse(p[2], out w) && int.TryParse(p[3], out h))
                                s.Bounds = new Rectangle(x, y, w, h);
                            break;
                    }
                }
            }
            catch (Exception) { }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string[] lines = {
                    "tempo=" + Speed, "mikroschritte=" + (MicroRun ? 1 : 0), "folgen=" + (Follow ? 1 : 0), "adressen_anpassen=" + (Adjust ? 1 : 0),
                    "steuerwerk_aus=" + (HideCu ? 1 : 0), "seitenleiste=" + (ShowSide ? 1 : 0), "startbild=" + (ShowSplash ? 1 : 0), "sprache=" + Language, "schrift=" + FontSize.ToString(CultureInfo.InvariantCulture), "maximiert=" + (Maximized ? 1 : 0),
                    "fenster=" + Bounds.X + "," + Bounds.Y + "," + Bounds.Width + "," + Bounds.Height, "ordner=" + LastDir,
                    "links=" + LeftWidth, "rechts=" + RightWidth };
                File.WriteAllLines(FilePath, lines);
            }
            catch (Exception) { }
        }
    }
}
