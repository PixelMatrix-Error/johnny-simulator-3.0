// Johnny – example programs as assembler text (English and German). GPLv3, see LICENSE.
namespace Johnny
{
    public sealed class Example
    {
        readonly string titleEn, titleDe, noteEn, noteDe, fileEn, fileDe, srcEn, srcDe;
        public readonly string SetKey;
        public string Title { get { return L.S(titleEn, titleDe); } }
        public string Note { get { return L.S(noteEn, noteDe); } }
        public string FileName { get { return L.S(fileEn, fileDe); } }
        public string Source { get { return L.S(srcEn, srcDe); } }

        public Example(string setKey, string titleEn, string titleDe, string noteEn, string noteDe, string fileEn, string fileDe, string srcEn, string srcDe)
        {
            SetKey = setKey; this.titleEn = titleEn; this.titleDe = titleDe; this.noteEn = noteEn; this.noteDe = noteDe;
            this.fileEn = fileEn; this.fileDe = fileDe; this.srcEn = srcEn; this.srcDe = srcDe;
        }
    }

    public static class Examples
    {
        public static readonly Example[] All = new Example[]
        {
            new Example("standard", "Multiplication", "Multiplikation",
                "Original example: 5 · 3 by repeated addition", "Original-Beispiel: 5 · 3 durch wiederholte Addition",
                "multiplication", "multiplikation",
@"; Multiplication (example from the original Johnny)
; RAM[12] := RAM[10] * RAM[11]  by repeated addition
000: NULL 12     ; result := 0
001: TAKE 12     ; acc := result
002: ADD  10     ; acc := acc + factor 1
003: SAVE 12     ; result := acc
004: DEC  11     ; factor 2 := factor 2 - 1
005: TST  11     ; factor 2 = 0 ?  then skip the next instruction
006: JMP  1      ; no: keep adding
007: HLT  0      ; done
010: 5           ; factor 1
011: 3           ; factor 2
012: 0           ; result
",
@"; Multiplikation (Beispiel aus dem Original-Johnny)
; RAM[12] := RAM[10] * RAM[11]  durch wiederholte Addition
000: NULL 12     ; Ergebnis := 0
001: TAKE 12     ; Akku := Ergebnis
002: ADD  10     ; Akku := Akku + Faktor 1
003: SAVE 12     ; Ergebnis := Akku
004: DEC  11     ; Faktor 2 := Faktor 2 - 1
005: TST  11     ; Faktor 2 = 0 ?  dann nächsten Befehl überspringen
006: JMP  1      ; nein: weiter addieren
007: HLT  0      ; fertig
010: 5           ; Faktor 1
011: 3           ; Faktor 2
012: 0           ; Ergebnis
"),
            new Example("standard", "Addition", "Addition",
                "Add two numbers (the simplest start)", "Zwei Zahlen addieren (der einfachste Einstieg)",
                "addition", "addition",
@"; Addition: RAM[12] := RAM[10] + RAM[11]
000: TAKE 10     ; acc := first number
001: ADD  11     ; acc := acc + second number
002: SAVE 12     ; store the result
003: HLT  0
010: 7           ; first number
011: 5           ; second number
012: 0           ; result
",
@"; Addition: RAM[12] := RAM[10] + RAM[11]
000: TAKE 10     ; Akku := erste Zahl
001: ADD  11     ; Akku := Akku + zweite Zahl
002: SAVE 12     ; Ergebnis speichern
003: HLT  0
010: 7           ; erste Zahl
011: 5           ; zweite Zahl
012: 0           ; Ergebnis
"),
            new Example("standard", "Maximum of two numbers", "Maximum zweier Zahlen",
                "Comparison with SUB and TST, labels", "Vergleich mit SUB und TST, Sprungmarken",
                "maximum", "maximum",
@"; Maximum: RAM[23] := the larger of the numbers in RAM[20] and RAM[21]
; Trick: SUB never goes below 0. If a - b = 0, then a <= b.
000:    TAKE 20      ; acc := a
        SUB  21      ; acc := a - b  (at least 0)
        SAVE 22      ; helper cell := a - b
        TST  22      ; a - b = 0 ?  then skip the next instruction
        JMP  take_a  ; a is larger
        TAKE 21      ; otherwise: b is larger or equal
        JMP  done
take_a: TAKE 20
done:   SAVE 23
        HLT  0
020: 12              ; a
021: 30              ; b
022: 0               ; helper cell
023: 0               ; result
",
@"; Maximum: RAM[23] := die größere der Zahlen in RAM[20] und RAM[21]
; Trick: SUB rechnet nie unter 0. Ist a - b = 0, dann ist a <= b.
000:    TAKE 20      ; Akku := a
        SUB  21      ; Akku := a - b  (mindestens 0)
        SAVE 22      ; Hilfszelle := a - b
        TST  22      ; a - b = 0 ?  dann nächsten Befehl überspringen
        JMP  nimm_a  ; a ist größer
        TAKE 21      ; sonst: b ist größer oder gleich
        JMP  fertig
nimm_a: TAKE 20
fertig: SAVE 23
        HLT  0
020: 12              ; a
021: 30              ; b
022: 0               ; Hilfszelle
023: 0               ; Ergebnis
"),
            new Example("standard", "Sum 1 + 2 + ... + n", "Summe 1 + 2 + ... + n",
                "Loop with a counter", "Schleife mit Zähler",
                "sum_1_to_n", "summe_1_bis_n",
@"; Sum: RAM[21] := 1 + 2 + ... + n   (n is in RAM[20])
000:      NULL 21       ; sum := 0
loop:     TST  20       ; n = 0 ?  then skip the next instruction
          JMP  step
          HLT  0        ; n is 0: done
step:     TAKE 21
          ADD  20       ; sum := sum + n
          SAVE 21
          DEC  20       ; n := n - 1
          JMP  loop
020: 10                 ; n
021: 0                  ; sum
",
@"; Summe: RAM[21] := 1 + 2 + ... + n   (n steht in RAM[20])
000:      NULL 21       ; Summe := 0
schleife: TST  20       ; n = 0 ?  dann nächsten Befehl überspringen
          JMP  weiter
          HLT  0        ; n ist 0: fertig
weiter:   TAKE 21
          ADD  20       ; Summe := Summe + n
          SAVE 21
          DEC  20       ; n := n - 1
          JMP  schleife
020: 10                 ; n
021: 0                  ; Summe
"),
            new Example("standard", "Division with remainder", "Division mit Rest",
                "Repeated subtraction: 17 : 5 = 3 remainder 2", "Wiederholte Subtraktion: 17 : 5 = 3 Rest 2",
                "division_with_remainder", "division_mit_rest",
@"; Integer division: RAM[22] := RAM[20] : RAM[21], the remainder stays in RAM[20]
; Caution: a divisor of 0 causes an endless loop!
000:      NULL 22       ; quotient := 0
loop:     TAKE 21       ; acc := divisor
          SUB  20       ; acc := divisor - remainder (at least 0)
          SAVE 23
          TST  23       ; = 0 means: remainder >= divisor, so keep dividing
          JMP  done     ; otherwise remainder < divisor: done
          TAKE 20
          SUB  21
          SAVE 20       ; remainder := remainder - divisor
          INC  22       ; quotient := quotient + 1
          JMP  loop
done:     HLT  0
020: 17                 ; dividend (at the end: remainder)
021: 5                  ; divisor
022: 0                  ; quotient
023: 0                  ; helper cell
",
@"; Ganzzahlige Division: RAM[22] := RAM[20] : RAM[21], der Rest bleibt in RAM[20]
; Achtung: Teiler 0 ergibt eine Endlosschleife!
000:      NULL 22       ; Quotient := 0
schleife: TAKE 21       ; Akku := Teiler
          SUB  20       ; Akku := Teiler - Rest (mindestens 0)
          SAVE 23
          TST  23       ; = 0 heißt: Rest >= Teiler, also weiter teilen
          JMP  fertig   ; sonst ist Rest < Teiler: fertig
          TAKE 20
          SUB  21
          SAVE 20       ; Rest := Rest - Teiler
          INC  22       ; Quotient := Quotient + 1
          JMP  schleife
fertig:   HLT  0
020: 17                 ; Dividend (am Ende: Rest)
021: 5                  ; Teiler
022: 0                  ; Quotient
023: 0                  ; Hilfszelle
"),
            new Example("bonsai", "Addition (Bonsai)", "Addition (Bonsai)",
                "Original example addition.bma in the Bonsai instruction set", "Original-Beispiel addition.bma im Bonsai-Befehlssatz",
                "addition_bonsai", "addition_bonsai",
@"; Bonsai instruction set: addition by counting   RAM[8] := RAM[8] + RAM[9]
000: TST 9       ; RAM[9] = 0 ?  then skip the next instruction
001: JMP 3
002: JMP 6       ; done
003: DEC 9
004: INC 8
005: JMP 0
006: HLT 0
008: 2
009: 3
",
@"; Bonsai-Befehlssatz: Addition durch Zählen   RAM[8] := RAM[8] + RAM[9]
000: TST 9       ; RAM[9] = 0 ?  dann nächsten Befehl überspringen
001: JMP 3
002: JMP 6       ; fertig
003: DEC 9
004: INC 8
005: JMP 0
006: HLT 0
008: 2
009: 3
")
        };
    }
}
