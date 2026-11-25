// Johnny – Programmstart. GPLv3, siehe LICENSE.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("Johnny")]
[assembly: AssemblyDescription("Simulator eines einfachen von-Neumann-Rechners")]
[assembly: AssemblyProduct("Johnny – verbesserte Fassung 2026")]
[assembly: AssemblyCopyright("© 2009–2014 Peter Dauscher; Neufassung 2026. GNU GPL v3")]
[assembly: AssemblyVersion("2026.1.0.0")]
[assembly: AssemblyFileVersion("2026.1.0.0")]

namespace Johnny
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Werkzeuge für den Bau und die Prüfung (nicht für den normalen Gebrauch)
            if (args.Length >= 2 && args[0] == "--icon") { WriteIcon(args[1]); return 0; }
            if (args.Length >= 2 && args[0] == "--screenshot") return Screenshot(args);
            if (args.Length >= 3 && args[0] == "--dialog") return DialogShot(args[1], args[2]);
            if (args.Length >= 2 && args[0] == "--selftest")
            {
                MainForm t = new MainForm(); t.TestMode = true; t.Opacity = 0.01; t.ShowInTaskbar = false;
                t.StartPosition = FormStartPosition.Manual; t.Location = new Point(40, 40); t.Size = new Size(1366, 768);
                t.Show(); Application.DoEvents();
                string res;
                try { res = t.SelfTest(); } catch (Exception ex) { res = "AUSNAHME: " + ex; }
                File.WriteAllText(args[1], res);
                t.Close();
                return res.Contains("ALLE TESTS BESTANDEN") ? 0 : 1;
            }

            MainForm f = new MainForm();
            if (args.Length >= 1 && File.Exists(args[0])) f.OpenOnStart = args[0];
            Application.Run(f);
            return 0;
        }

        /// <summary>Bildschirmfoto des Hauptfensters: --screenshot datei.png [beispiel] [makroschritte] [mikroschritte] [breite] [höhe]</summary>
        static int Screenshot(string[] a)
        {
            int ex = a.Length > 2 ? int.Parse(a[2]) : 0, macro = a.Length > 3 ? int.Parse(a[3]) : 0, micro = a.Length > 4 ? int.Parse(a[4]) : 0;
            int w = a.Length > 5 ? int.Parse(a[5]) : 1366, h = a.Length > 6 ? int.Parse(a[6]) : 768;
            MainForm f = new MainForm();
            f.TestMode = true;
            f.TestHideCu = a.Length > 7 && a[7] == "cu0";
            f.TestShowCu = a.Length > 7 && a[7] == "cu1";
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(40, 40);
            f.WindowState = FormWindowState.Normal;
            f.Size = new Size(w, h);
            f.ShowInTaskbar = false;
            f.Opacity = 0.01;
            f.Show();
            Application.DoEvents();
            if (a.Length > 8 && a[8] == "de") f.TestSetLanguage(true);
            f.TestLoadExample(ex, macro, micro);
            Application.DoEvents();
            using (Bitmap bmp = new Bitmap(f.Width, f.Height))
            {
                f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                bmp.Save(a[1], ImageFormat.Png);
            }
            f.Close();
            return 0;
        }

        static int DialogShot(string which, string path)
        {
            if (path.EndsWith("_de.png")) L.De = true;
            Cpu cpu = new Cpu();
            Form d = which == "splash" ? (Form)new SplashForm(false)
                   : which == "help" ? (Form)new HelpForm(cpu, InstructionSets.Standard)
                   : which == "about" ? (Form)new AboutForm()
                   : which == "record" ? (Form)new RecordDialog(cpu)
                   : (Form)new MicrocodeEditor(cpu, "Consolas");
            d.StartPosition = FormStartPosition.Manual; d.Location = new Point(40, 40); d.Opacity = 0.01; d.ShowInTaskbar = false;
            d.Show(); Application.DoEvents();
            using (Bitmap bmp = new Bitmap(d.Width, d.Height)) { d.DrawToBitmap(bmp, new Rectangle(0, 0, d.Width, d.Height)); bmp.Save(path, ImageFormat.Png); }
            d.Close();
            return 0;
        }

        /// <summary>Schreibt das Programmsymbol als .ico (PNG-Einträge in mehreren Größen).</summary>
        static void WriteIcon(string path)
        {
            int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
            List<byte[]> pngs = new List<byte[]>();
            foreach (int s in sizes)
                using (Bitmap b = Icons.AppIcon(s))
                using (MemoryStream ms = new MemoryStream()) { b.Save(ms, ImageFormat.Png); pngs.Add(ms.ToArray()); }
            using (BinaryWriter w = new BinaryWriter(File.Create(path)))
            {
                w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)0); w.Write((byte)0); w.Write((short)1); w.Write((short)32);
                    w.Write(pngs[i].Length); w.Write(offset);
                    offset += pngs[i].Length;
                }
                foreach (byte[] p in pngs) w.Write(p);
            }
        }
    }
}
