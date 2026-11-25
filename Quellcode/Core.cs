// Johnny – Simulator eines einfachen von-Neumann-Rechners (verbesserte Fassung 2026)
// Kern: Prozessor, Mikrobefehle, Dateiformate, Assembler. Ohne Oberfläche.
//
// Basiert auf JOHNNY 1.01 (C) 2009-2014 Peter Dauscher, Gymnasium am Kaiserdom, Speyer.
// Freie Software unter der GNU General Public License Version 3 (siehe LICENSE).
// Die Mikrobefehle, ihre Nummern, die Original-Mikrocodes und die Dateiformate
// entsprechen exakt dem Original (guido_main.pas).

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Johnny
{
    /// <summary>Language of the user interface: English (default) or German.</summary>
    public static class L
    {
        public static bool De;
        public static string S(string en, string de) { return De ? de : en; }
    }

    public sealed class MicroOp
    {
        public readonly int Id;
        public readonly string Name;
        readonly string en, de;
        public string Description { get { return L.S(en, de); } }
        public MicroOp(int id, string name, string en, string de) { Id = id; Name = name; this.en = en; this.de = de; }
    }

    public static class Micro
    {
        public static readonly MicroOp[] Ops = new MicroOp[]
        {
            new MicroOp(0,  "---",     "No operation.", "Keine Operation."),
            new MicroOp(1,  "db->ram", "The value on the data bus is written into the memory cell whose address is on the address bus.", "Der Wert auf dem Datenbus wird in die Speicherzelle geschrieben, deren Adresse auf dem Adressbus liegt."),
            new MicroOp(2,  "ram->db", "The content of the addressed memory cell is put on the data bus.", "Der Inhalt der adressierten Speicherzelle wird auf den Datenbus gelegt."),
            new MicroOp(3,  "db->ins", "The value on the data bus is loaded into the instruction register (hi part = opcode, lo part = address).", "Der Wert auf dem Datenbus wird ins Befehlsregister übernommen (Hi-Teil = Opcode, Lo-Teil = Adresse)."),
            new MicroOp(4,  "ins->ab", "The address part of the instruction register is put on the address bus.", "Der Adressteil des Befehlsregisters wird auf den Adressbus gelegt."),
            new MicroOp(5,  "ins->mc", "The opcode is loaded into the micro program counter. The micro program of the instruction starts.", "Der Opcode wird in den Mikroprogrammzähler übernommen. Das Mikroprogramm des Befehls beginnt."),
            new MicroOp(6,  "inc_mc",  "The micro program counter is increased by 1 (the following micro instruction is skipped).", "Der Mikroprogrammzähler wird um 1 erhöht (der folgende Mikrobefehl wird übersprungen)."),
            new MicroOp(7,  "mc:=0",   "The micro program counter is set to 0. Then the next instruction is fetched.", "Der Mikroprogrammzähler wird auf 0 gesetzt. Danach wird der nächste Befehl geholt."),
            new MicroOp(8,  "pc->ab",  "The program counter is put on the address bus.", "Der Programmzähler wird auf den Adressbus gelegt."),
            new MicroOp(9,  "pc++",    "The program counter is increased by 1.", "Der Programmzähler wird um 1 erhöht."),
            new MicroOp(10, "=0:pc++", "Only if the accumulator is 0, the program counter is increased by 1.", "Nur wenn der Akkumulator 0 ist, wird der Programmzähler um 1 erhöht."),
            new MicroOp(11, "ins->pc", "The address part of the instruction register is loaded into the program counter (jump).", "Der Adressteil des Befehlsregisters wird in den Programmzähler übernommen (Sprung)."),
            new MicroOp(12, "acc:=0",  "The accumulator is set to 0.", "Der Akkumulator wird auf 0 gesetzt."),
            new MicroOp(13, "plus",    "Accumulator := accumulator + data bus (at most 19999).", "Akkumulator := Akkumulator + Datenbus (höchstens 19999)."),
            new MicroOp(14, "minus",   "Accumulator := accumulator - data bus (not below 0).", "Akkumulator := Akkumulator - Datenbus (nicht kleiner als 0)."),
            new MicroOp(15, "acc->db", "The accumulator is put on the data bus.", "Der Akkumulator wird auf den Datenbus gelegt."),
            new MicroOp(16, "acc++",   "The accumulator is increased by 1.", "Der Akkumulator wird um 1 erhöht."),
            new MicroOp(17, "acc--",   "The accumulator is decreased by 1 (not below 0).", "Der Akkumulator wird um 1 verringert (nicht kleiner als 0)."),
            new MicroOp(18, "db->acc", "The value on the data bus is loaded into the accumulator.", "Der Wert auf dem Datenbus wird in den Akkumulator übernommen."),
            new MicroOp(19, "stopp",   "The program is stopped.", "Das Programm wird angehalten.")
        };

        public static int IndexOf(string name)
        {
            for (int i = 0; i < Ops.Length; i++) if (Ops[i].Name == name) return i;
            return 0;
        }
    }

    public sealed class InstructionSet
    {
        public readonly string Key;
        readonly string titleEn, titleDe;
        public readonly int[] Mpm;
        public readonly string[] Names;
        readonly Dictionary<string, string> helpEn, helpDe;
        public string Title { get { return L.S(titleEn, titleDe); } }
        public Dictionary<string, string> Help { get { return L.De ? helpDe : helpEn; } }
        public InstructionSet(string key, string titleEn, string titleDe, int[] mpm, string[] names, Dictionary<string, string> helpEn, Dictionary<string, string> helpDe)
        { Key = key; this.titleEn = titleEn; this.titleDe = titleDe; Mpm = mpm; Names = Cpu.FixNames(names); this.helpEn = helpEn; this.helpDe = helpDe; }
    }

    public static class InstructionSets
    {
        static int[] Blocks(Dictionary<int, int[]> def)
        {
            int[] m = new int[200];
            foreach (KeyValuePair<int, int[]> kv in def)
                for (int i = 0; i < kv.Value.Length; i++) m[kv.Key * 10 + i] = kv.Value[i];
            return m;
        }

        static Dictionary<int, int[]> StdBlocks()
        {
            Dictionary<int, int[]> d = new Dictionary<int, int[]>();
            d[0] = new[] { 8, 2, 3, 5 };
            d[1] = new[] { 12, 4, 2, 13, 9, 7 };
            d[2] = new[] { 4, 2, 13, 9, 7 };
            d[3] = new[] { 4, 2, 14, 9, 7 };
            d[4] = new[] { 4, 15, 1, 9, 7 };
            d[5] = new[] { 11, 7 };
            d[6] = new[] { 4, 2, 18, 10, 9, 7 };
            d[7] = new[] { 12, 4, 2, 13, 16, 15, 1, 9, 7 };
            d[8] = new[] { 12, 4, 2, 13, 17, 15, 1, 9, 7 };
            d[9] = new[] { 4, 12, 15, 1, 9, 7 };
            d[10] = new[] { 19, 7 };
            d[19] = new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 7 };
            return d;
        }

        static Dictionary<string, string> StdHelp(bool alt, bool de)
        {
            Dictionary<string, string> h = new Dictionary<string, string>();
            h["TAKE"] = de ? "Akku := RAM[a]" : "Acc := RAM[a]";
            h["ADD"] = de ? "Akku := Akku + RAM[a]" : "Acc := Acc + RAM[a]";
            h["SUB"] = de ? "Akku := Akku - RAM[a] (nicht unter 0)" : "Acc := Acc - RAM[a] (not below 0)";
            h["SAVE"] = de ? "RAM[a] := Akku" : "RAM[a] := Acc";
            h["JMP"] = de ? "Springe zur Adresse a" : "Jump to address a";
            h["TST"] = alt ? (de ? "Wenn der Akku 0 ist: nächsten Befehl überspringen" : "If the accumulator is 0: skip the next instruction")
                           : (de ? "Wenn RAM[a] = 0: nächsten Befehl überspringen (lädt RAM[a] in den Akku)" : "If RAM[a] = 0: skip the next instruction (loads RAM[a] into Acc)");
            h["INC"] = "RAM[a] := RAM[a] + 1";
            h["DEC"] = de ? "RAM[a] := RAM[a] - 1 (nicht unter 0)" : "RAM[a] := RAM[a] - 1 (not below 0)";
            h["NULL"] = "RAM[a] := 0";
            h["HLT"] = de ? "Programm anhalten" : "Stop the program";
            return h;
        }

        static Dictionary<string, string> BonsaiHelp(bool de)
        {
            Dictionary<string, string> h = new Dictionary<string, string>();
            h["INC"] = "RAM[a] := RAM[a] + 1";
            h["DEC"] = "RAM[a] := RAM[a] - 1";
            h["JMP"] = de ? "Springe zur Adresse a" : "Jump to address a";
            h["TST"] = de ? "Wenn RAM[a] = 0: nächsten Befehl überspringen" : "If RAM[a] = 0: skip the next instruction";
            h["HLT"] = de ? "Programm anhalten" : "Stop the program";
            return h;
        }

        public static readonly InstructionSet Standard;
        public static readonly InstructionSet StandardAlt;
        public static readonly InstructionSet Bonsai;
        public static readonly InstructionSet[] All;

        static InstructionSets()
        {
            string[] stdNames = { "", "TAKE", "ADD", "SUB", "SAVE", "JMP", "TST", "INC", "DEC", "NULL", "HLT" };
            Standard = new InstructionSet("standard", "Standard", "Standard", Blocks(StdBlocks()), stdNames, StdHelp(false, false), StdHelp(false, true));
            Dictionary<int, int[]> alt = StdBlocks();
            alt[6] = new[] { 10, 9, 7 };
            StandardAlt = new InstructionSet("standard_alt", "Standard (old: TST tests the accumulator)", "Standard (alt: TST prüft den Akku)",
                Blocks(alt), stdNames, StdHelp(true, false), StdHelp(true, true));
            Dictionary<int, int[]> b = new Dictionary<int, int[]>();
            b[0] = new[] { 8, 2, 3, 5 };
            b[1] = new[] { 4, 2, 18, 16, 15, 1, 9, 7 };
            b[2] = new[] { 4, 2, 18, 17, 15, 1, 9, 7 };
            b[3] = new[] { 11, 7 };
            b[4] = new[] { 4, 2, 18, 10, 9, 7 };
            b[5] = new[] { 19, 7 };
            b[19] = new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 7 };
            Bonsai = new InstructionSet("bonsai", "Bonsai", "Bonsai", Blocks(b), new[] { "", "INC", "DEC", "JMP", "TST", "HLT", "NN", "NN", "NN", "NN" }, BonsaiHelp(false), BonsaiHelp(true));
            All = new[] { Standard, StandardAlt, Bonsai };
        }

        public static InstructionSet ByKey(string key)
        {
            foreach (InstructionSet s in All) if (s.Key == key) return s;
            return null;
        }
    }

    public struct StepResult
    {
        public int Op;
        public int Address;
        public bool Halt;
        public string Error;
    }

    public sealed class MacroResult
    {
        public bool Halted;
        public string Error;
        public readonly List<StepResult> Steps = new List<StepResult>();
    }

    public sealed class Snapshot
    {
        public int[] Ram;
        public int Ab, Db, InsOp, InsAd, Pc, MOp, MMc, Acc;
        public long MicroCount, MacroCount;
    }

    public sealed class Cpu
    {
        public const int RamSize = 1000;
        public const int MaxValue = 19999;

        public int[] Ram = new int[RamSize];
        public int Ab, Db, InsOp, InsAd, Pc, MOp, MMc, Acc;
        public long MicroCount, MacroCount;
        public int[] Mpm = new int[200];
        public string[] Names = new string[20];

        public Cpu() { LoadSet(InstructionSets.Standard); }

        public static int Hi(int v) { return v / 1000; }
        public static int Lo(int v) { return v % 1000; }
        public static int Clamp(int v) { return v > MaxValue ? MaxValue : (v < 0 ? 0 : v); }

        public static string[] FixNames(string[] a)
        {
            string[] r = new string[20];
            for (int i = 0; i < 20; i++)
                r[i] = (a != null && i < a.Length && a[i] != null) ? a[i].Trim().ToUpperInvariant() : "";
            return r;
        }

        public void LoadSet(InstructionSet s)
        {
            Array.Copy(s.Mpm, Mpm, 200);
            Names = FixNames(s.Names);
        }

        public void ResetRegisters()
        {
            Ab = Db = InsOp = InsAd = Pc = MOp = MMc = Acc = 0;
            MicroCount = MacroCount = 0;
        }

        public Snapshot Save(bool withRam)
        {
            Snapshot s = new Snapshot();
            if (withRam) s.Ram = (int[])Ram.Clone();
            s.Ab = Ab; s.Db = Db; s.InsOp = InsOp; s.InsAd = InsAd; s.Pc = Pc; s.MOp = MOp; s.MMc = MMc; s.Acc = Acc;
            s.MicroCount = MicroCount; s.MacroCount = MacroCount;
            return s;
        }

        public void Restore(Snapshot s)
        {
            if (s.Ram != null) Ram = (int[])s.Ram.Clone();
            Ab = s.Ab; Db = s.Db; InsOp = s.InsOp; InsAd = s.InsAd; Pc = s.Pc; MOp = s.MOp; MMc = s.MMc; Acc = s.Acc;
            MicroCount = s.MicroCount; MacroCount = s.MacroCount;
        }

        /// <summary>Führt Mikrobefehl n aus (wie execute_MC_Number im Original). Rückgabe true = stopp.</summary>
        public bool Exec(int n)
        {
            switch (n)
            {
                case 1: Ram[Ab] = Db; break;
                case 2: Db = Ram[Ab]; break;
                case 3: InsOp = Hi(Db); InsAd = Lo(Db); break;
                case 4: Ab = InsAd; break;
                case 5: MOp = InsOp; MMc = 0; break;
                case 6: MMc++; break;
                case 7: MOp = 0; MMc = 0; break;
                case 8: Ab = Pc; break;
                case 9: Pc = (Pc + 1) % RamSize; break;
                case 10: if (Acc == 0) Pc = (Pc + 1) % RamSize; break;
                case 11: Pc = InsAd; break;
                case 12: Acc = 0; break;
                case 13: Acc = Clamp(Acc + Db); break;
                case 14: Acc = Clamp(Acc - Db); break;
                case 15: Db = Acc; break;
                case 16: Acc = Clamp(Acc + 1); break;
                case 17: Acc = Clamp(Acc - 1); break;
                case 18: Acc = Db; break;
                case 19: return true;
            }
            return false;
        }

        public string OpName(int op)
        {
            if (op == 0) return L.S("Fetch", "Holphase");
            return Names[op].Length > 0 ? Names[op] : "Opcode " + op.ToString("00");
        }

        /// <summary>Nummer des Mikrobefehls, der als Nächstes ausgeführt wird (-1 = keiner).</summary>
        public int NextMicroOp { get { return MMc <= 9 ? Mpm[10 * MOp + MMc] : -1; } }

        public bool AtFetchStart { get { return MOp == 0 && MMc == 0; } }

        /// <summary>Ein Mikroschritt wie im Original: Befehl aus MPM[10*op+mc] ausführen,
        /// danach mc + 1 (außer bei ins->mc und mc:=0).</summary>
        public StepResult MicroStep()
        {
            StepResult r = new StepResult();
            if (MMc > 9)
            {
                bool empty = true;
                for (int k = 0; k < 10; k++) if (Mpm[MOp * 10 + k] != 0) empty = false;
                r.Error = empty
                    ? string.Format(L.S("The instruction with opcode {0:00} is not defined (no micro program). Is there perhaps a number instead of an instruction at this memory location?", "Der Befehl mit Opcode {0:00} ist nicht definiert (kein Mikroprogramm). Steht an dieser Stelle im RAM vielleicht eine Zahl statt eines Befehls?"), MOp)
                    : string.Format(L.S("The micro program for {0} (opcode {1:00}) does not end with \"mc:=0\".", "Das Mikroprogramm für {0} (Opcode {1:00}) endet nicht mit \"mc:=0\"."), OpName(MOp), MOp);
                return r;
            }
            r.Address = 10 * MOp + MMc;
            r.Op = Mpm[r.Address];
            r.Halt = Exec(r.Op);
            if (r.Op != 5 && r.Op != 7) MMc++;
            MicroCount++;
            if (r.Op == 5) MacroCount++;
            return r;
        }

        /// <summary>Ein Makroschritt: Mikroschritte, bis der Mikroprogrammzähler wieder bei 0 steht.</summary>
        public MacroResult MacroStep(bool collect, Action<StepResult> onStep = null)
        {
            MacroResult res = new MacroResult();
            for (int i = 0; i < 100; i++)
            {
                StepResult r = MicroStep();
                if (r.Error != null) { res.Error = r.Error; return res; }
                if (collect) res.Steps.Add(r);
                if (onStep != null) onStep(r);
                if (r.Halt) res.Halted = true;
                if (10 * MOp + MMc == 0) return res;
            }
            res.Error = L.S("Endless loop in the microcode: the micro program counter does not return to the fetch phase.", "Endlosschleife im Mikrocode: Der Mikroprogrammzähler kehrt nicht zur Holphase zurück.");
            return res;
        }

        public int MnemonicIndex(string word) { return MnemonicIndex(Names, word); }

        public static int MnemonicIndex(string[] names, string word)
        {
            string w = word.ToUpperInvariant();
            for (int i = 1; i < 20; i++) if (names[i].Length > 0 && names[i] == w) return i;
            return -1;
        }

        /// <summary>Befehlstext einer Zelle, z. B. "TAKE 012" (leer bei Daten).</summary>
        public string CellText(int v)
        {
            int h = Hi(v);
            if (h > 0) return (Names[h].Length > 0 ? Names[h] : "??") + " " + Lo(v).ToString("000");
            return "";
        }

        /// <summary>Eingabe einer Zelle: "TAKE 10", "take10", "1010", "01 010", "1.010", "5".</summary>
        public static bool ParseCell(string text, string[] names, out int value, out string error)
        {
            value = 0; error = null;
            string t = (text ?? "").Trim();
            if (t.Length == 0) return true;
            Match m = Regex.Match(t, @"^([0-9]{1,2})\s*[ .:|,]\s*([0-9]{1,3})$");
            if (m.Success)
            {
                int h = int.Parse(m.Groups[1].Value), l = int.Parse(m.Groups[2].Value);
                if (h > 19) { error = L.S("The hi part (opcode) must be between 0 and 19.", "Der Hi-Teil (Opcode) muss zwischen 0 und 19 liegen."); return false; }
                value = h * 1000 + l; return true;
            }
            if (Regex.IsMatch(t, @"^[0-9]+$"))
            {
                long v;
                if (!long.TryParse(t, out v) || v > MaxValue) { error = L.S("Numbers only go up to 19999.", "Zahlen gehen nur bis 19999."); return false; }
                value = (int)v; return true;
            }
            m = Regex.Match(t, @"^([A-Za-z_][A-Za-z0-9_]*)(?:\s+([0-9]{1,3}))?$");
            if (m.Success)
            {
                int op = MnemonicIndex(names, m.Groups[1].Value);
                if (op >= 0) { value = op * 1000 + (m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0); return true; }
            }
            Match m2 = Regex.Match(t, @"^([A-Za-z_]+)([0-9]{1,3})$");
            if (m2.Success)
            {
                int op = MnemonicIndex(names, m2.Groups[1].Value);
                if (op >= 0) { value = op * 1000 + int.Parse(m2.Groups[2].Value); return true; }
            }
            if (m.Success) { error = L.S("Unknown instruction \"", "Unbekannter Befehl \"") + m.Groups[1].Value + "\"."; return false; }
            error = L.S("Input not understood. Examples: TAKE 10, 1010, 01 010 or 5", "Eingabe nicht verstanden. Beispiele: TAKE 10, 1010, 01 010 oder 5");
            return false;
        }

        public static int LastUsed(int[] ram)
        {
            for (int i = ram.Length - 1; i >= 0; i--) if (ram[i] != 0) return i;
            return -1;
        }
    }

    // ------------------------------------------------------------------ Assembler
    public sealed class AsmError
    {
        public int Line;
        public string Message;
        public override string ToString() { return L.S("Line ", "Zeile ") + Line + ": " + Message; }
    }

    public sealed class AsmResult
    {
        public int[] Ram = new int[Cpu.RamSize];
        public readonly List<AsmError> Errors = new List<AsmError>();
        public int Count;
    }

    public static class Assembler
    {
        sealed class Item { public int Address, Line, Value, Op = -1; public string Operand; }

        static string StripComment(string s)
        {
            int cut = s.Length;
            int a = s.IndexOf(';'); if (a >= 0 && a < cut) cut = a;
            a = s.IndexOf("//", StringComparison.Ordinal); if (a >= 0 && a < cut) cut = a;
            a = s.IndexOf('#'); if (a >= 0 && a < cut) cut = a;
            return s.Substring(0, cut);
        }

        static void Err(AsmResult r, int line, string msg) { AsmError e = new AsmError(); e.Line = line; e.Message = msg; r.Errors.Add(e); }

        public static AsmResult Assemble(string text, string[] names)
        {
            AsmResult res = new AsmResult();
            Dictionary<string, int> labels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Dictionary<int, int> used = new Dictionary<int, int>();
            List<Item> items = new List<Item>();
            int addr = 0;
            string[] lines = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int li = 0; li < lines.Length; li++)
            {
                int ln = li + 1;
                string s = StripComment(lines[li]).Trim();
                while (true)
                {
                    Match m = Regex.Match(s, @"^([0-9]+)\s*:\s*");
                    if (m.Success)
                    {
                        int a;
                        if (!int.TryParse(m.Groups[1].Value, out a) || a >= Cpu.RamSize) { Err(res, ln, L.S("The address ", "Die Adresse ") + m.Groups[1].Value + L.S(" does not exist (0 to 999).", " gibt es nicht (0 bis 999).")); a = 0; }
                        addr = a; s = s.Substring(m.Length); continue;
                    }
                    m = Regex.Match(s, @"^([A-Za-z_][A-Za-z0-9_]*)\s*:\s*");
                    if (m.Success)
                    {
                        string k = m.Groups[1].Value;
                        if (labels.ContainsKey(k)) Err(res, ln, L.S("The label \"", "Die Sprungmarke \"") + k + L.S("\" is defined twice.", "\" gibt es doppelt."));
                        labels[k] = addr; s = s.Substring(m.Length); continue;
                    }
                    break;
                }
                if (s.Length == 0) continue;
                List<string> tok = new List<string>();
                foreach (string p in Regex.Split(s, @"[\s,]+")) if (p.Length > 0) tok.Add(p);
                Item it = new Item();
                if (Regex.IsMatch(tok[0], @"^[0-9]+$"))
                {
                    long v;
                    if (tok.Count == 2 && Regex.IsMatch(tok[1], @"^[0-9]{1,3}$") && tok[0].Length <= 2 && int.Parse(tok[0]) <= 19)
                        v = int.Parse(tok[0]) * 1000 + int.Parse(tok[1]);
                    else if (tok.Count > 1) { Err(res, ln, L.S("Nothing may follow a number.", "Nach einer Zahl darf nichts mehr stehen.")); continue; }
                    else if (!long.TryParse(tok[0], out v)) v = long.MaxValue;
                    if (v > Cpu.MaxValue) { Err(res, ln, L.S("Numbers only go up to 19999.", "Zahlen gehen nur bis 19999.")); continue; }
                    it.Value = (int)v;
                }
                else
                {
                    int op = Cpu.MnemonicIndex(names, tok[0]);
                    if (op < 0) { Err(res, ln, L.S("Unknown instruction \"", "Unbekannter Befehl \"") + tok[0] + "\"."); continue; }
                    if (tok.Count > 2) { Err(res, ln, L.S("Too many operands. Allowed: INSTRUCTION address", "Zu viele Angaben. Erlaubt ist: BEFEHL Adresse")); continue; }
                    it.Op = op;
                    it.Operand = tok.Count > 1 ? tok[1] : null;
                }
                int prev;
                if (used.TryGetValue(addr, out prev)) Err(res, ln, L.S("The address ", "Die Adresse ") + addr.ToString("000") + L.S(" is already used in line ", " ist schon in Zeile ") + prev + L.S(".", " belegt."));
                used[addr] = ln;
                it.Address = addr; it.Line = ln;
                items.Add(it);
                addr++;
                if (addr >= Cpu.RamSize) addr = 0;
            }
            foreach (Item it in items)
            {
                int v = it.Value;
                if (it.Op >= 0)
                {
                    int a = 0;
                    if (it.Operand != null)
                    {
                        if (Regex.IsMatch(it.Operand, @"^[0-9]+$")) { if (!int.TryParse(it.Operand, out a)) a = int.MaxValue; }
                        else if (labels.ContainsKey(it.Operand)) a = labels[it.Operand];
                        else { Err(res, it.Line, L.S("The label \"", "Die Sprungmarke \"") + it.Operand + L.S("\" is not defined.", "\" ist nicht definiert.")); continue; }
                    }
                    if (a > 999) { Err(res, it.Line, L.S("Addresses only go up to 999.", "Adressen gehen nur bis 999.")); continue; }
                    v = it.Op * 1000 + a;
                }
                res.Ram[it.Address] = v;
            }
            res.Errors.Sort(delegate(AsmError x, AsmError y) { return x.Line.CompareTo(y.Line); });
            res.Count = items.Count;
            return res;
        }

        public static string Disassemble(int[] ram, string[] names)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(L.S("; Johnny program (address: content)\r\n", "; Johnny-Programm (Adresse: Inhalt)\r\n"));
            int end = Cpu.LastUsed(ram);
            for (int i = 0; i <= end; i++)
            {
                int h = Cpu.Hi(ram[i]), l = Cpu.Lo(ram[i]);
                sb.Append(i.ToString("000")).Append(": ");
                if (h > 0 && names[h].Length > 0) sb.Append(names[h].PadRight(5)).Append(l);
                else sb.Append(ram[i]);
                sb.Append("\r\n");
            }
            return sb.ToString();
        }
    }

    // ------------------------------------------------------------------ Dateiformate des Original-Johnny
    public static class Formats
    {
        const string CRLF = "\r\n";

        static List<string> SplitLines(string t)
        {
            if (t.Length > 0 && t[0] == '﻿') t = t.Substring(1);
            List<string> a = new List<string>(t.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'));
            while (a.Count > 0 && a[a.Count - 1].Trim().Length == 0) a.RemoveAt(a.Count - 1);
            return a;
        }

        public static int[] ParseRam(string text)
        {
            int[] ram = new int[Cpu.RamSize];
            List<string> ls = SplitLines(text);
            for (int i = 0; i < ls.Count && i < Cpu.RamSize; i++)
            {
                long v;
                ram[i] = long.TryParse(ls[i].Trim(), out v) ? (int)Math.Max(0, Math.Min(Cpu.MaxValue, v)) : 0;
            }
            return ram;
        }

        public static string SerializeRam(int[] ram)
        {
            StringBuilder sb = new StringBuilder();
            foreach (int v in ram) sb.Append(v.ToString("000")).Append(CRLF);
            return sb.ToString();
        }

        public static int[] ParseMpc(string text)
        {
            int[] m = new int[200];
            List<string> ls = SplitLines(text);
            for (int i = 0; i < ls.Count && i < 200; i++)
            {
                int v;
                m[i] = (int.TryParse(ls[i].Trim(), out v) && v >= 0 && v <= 19) ? v : 0;
            }
            return m;
        }

        public static string SerializeMpc(int[] mpm)
        {
            StringBuilder sb = new StringBuilder();
            foreach (int v in mpm) sb.Append(v).Append(CRLF);
            return sb.ToString();
        }

        public static string[] ParseNam(string text)
        {
            if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);
            return Cpu.FixNames(text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'));
        }

        public static string SerializeNam(string[] names)
        {
            return string.Join(CRLF, Cpu.FixNames(names)) + CRLF;
        }

        /// <summary>Bonsai-Format: Spalte 1 = Opcode (eine Ziffer), Spalten 2-5 = Adresse/Wert. ";" = Kommentar.</summary>
        public static int[] ParseBma(string text)
        {
            int[] ram = new int[Cpu.RamSize];
            int i = 0;
            foreach (string s in SplitLines(text))
            {
                if (s.StartsWith(";")) continue;
                if (i >= Cpu.RamSize) break;
                int h = (s.Length > 0 && s[0] >= '0' && s[0] <= '9') ? s[0] - '0' : 0;
                int l = 0;
                if (s.Length > 1) { string a = s.Substring(1, Math.Min(4, s.Length - 1)).Trim(); if (!int.TryParse(a, out l)) l = 0; }
                if (l < 0) l = 0;
                if (l > 999) l %= 1000;
                ram[i++] = h * 1000 + l;
            }
            return ram;
        }

        public static string SerializeBma(int[] ram)
        {
            StringBuilder sb = new StringBuilder();
            int end = Math.Min(Cpu.RamSize - 1, Cpu.LastUsed(ram) + 1);   // wie das Original: eine Null-Zelle am Ende
            for (int i = 0; i <= end; i++)
            {
                int h = Cpu.Hi(ram[i]), l = Cpu.Lo(ram[i]);
                sb.Append(h > 0 ? h.ToString() + l.ToString("0000") : l.ToString().PadLeft(5)).Append(CRLF);
            }
            return sb.ToString();
        }

        public static List<string> CheckBonsai(int[] ram)
        {
            List<string> p = new List<string>();
            int end = Cpu.LastUsed(ram);
            if (end >= 64) p.Add(L.S("The program is too long for the Bonsai computer (more than 64 cells).", "Das Programm ist zu lang für den Bonsai-Rechner (mehr als 64 Zellen)."));
            for (int i = 0; i <= end; i++)
            {
                int h = Cpu.Hi(ram[i]), l = Cpu.Lo(ram[i]);
                if (h > 5 || l > 255 || (h > 0 && l > 63)) { p.Add(L.S("Cell ", "Zelle ") + i.ToString("000") + L.S(": number too large (above 255) or address out of range (above 63).", ": Zahl zu groß (über 255) oder Adresse außerhalb (über 63).")); break; }
            }
            return p;
        }

        public static bool IsNumberFile(string t)
        {
            int n = 0;
            foreach (string l in SplitLines(t))
            {
                if (l.Trim().Length == 0) continue;
                if (!Regex.IsMatch(l, @"^\s*[0-9]+\s*$")) return false;
                n++;
            }
            return n > 0;
        }

        /// <summary>Liest eine Textdatei: UTF-8, sonst Windows-Zeichensatz (wie im Original).</summary>
        public static string ReadText(string path)
        {
            byte[] b = File.ReadAllBytes(path);
            try { return new UTF8Encoding(false, true).GetString(b); }
            catch (DecoderFallbackException) { return Encoding.Default.GetString(b); }
        }

        public static void WriteText(string path, string text, bool utf8)
        {
            File.WriteAllBytes(path, (utf8 ? new UTF8Encoding(false) : Encoding.Default).GetBytes(text));
        }
    }

    // ------------------------------------------------------------------ Zeilen einfügen/löschen
    public static class RamEdit
    {
        /// <summary>Fügt bei p eine leere Zelle ein, schiebt den Block bis zur nächsten leeren Zelle nach unten.</summary>
        public static string InsertRow(int[] ram, int p, bool adjust, string[] names, out int changed)
        {
            changed = 0;
            int e = -1;
            for (int i = p; i < Cpu.RamSize; i++) if (ram[i] == 0) { e = i; break; }
            if (e < 0) return L.S("No space: there is no empty cell left from this row on.", "Kein Platz: Ab dieser Zeile gibt es keine leere Zelle mehr.");
            if (adjust)
                for (int i = 0; i < Cpu.RamSize; i++)
                {
                    int h = Cpu.Hi(ram[i]), l = Cpu.Lo(ram[i]);
                    if (h > 0 && names[h].Length > 0 && l >= p && l < e) { ram[i] = h * 1000 + l + 1; changed++; }
                }
            for (int i = e; i > p; i--) ram[i] = ram[i - 1];
            ram[p] = 0;
            return null;
        }

        /// <summary>Löscht die Zelle p, schiebt den Block bis zur nächsten leeren Zelle nach oben.</summary>
        public static void DeleteRow(int[] ram, int p, bool adjust, string[] names, out int changed, out int dangling)
        {
            changed = 0; dangling = 0;
            int e = Cpu.RamSize;
            for (int i = p + 1; i < Cpu.RamSize; i++) if (ram[i] == 0) { e = i; break; }
            if (adjust)
                for (int i = 0; i < Cpu.RamSize; i++)
                {
                    int h = Cpu.Hi(ram[i]), l = Cpu.Lo(ram[i]);
                    if (h > 0 && names[h].Length > 0)
                    {
                        if (l > p && l < e) { ram[i] = h * 1000 + l - 1; changed++; }
                        else if (l == p) dangling++;
                    }
                }
            for (int i = p; i < e - 1; i++) ram[i] = ram[i + 1];
            ram[e - 1] = 0;
        }
    }
}
