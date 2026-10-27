// Colour Picker: a PowerPoint-style eyedropper for Windows.
// Opens straight into a live eyedropper: move the pointer over anything on any screen,
// a magnifier shows the colour and hex code, click to pick. Win+Shift+C picks again from anywhere.
// Also: spectrum + hue bar, Hex/RGB fields, click-to-copy hex, standard and recent colours.
// Built with the C# 5 compiler that ships with .NET Framework 4.x (see build.ps1).

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Colour Picker")]
[assembly: System.Reflection.AssemblyProduct("Colour Picker")]
[assembly: System.Reflection.AssemblyVersion("2.0.1.0")]
[assembly: System.Reflection.AssemblyFileVersion("2.0.1.0")]

namespace ColourPicker
{
    static class Native
    {
        public static readonly IntPtr SystemAware = new IntPtr(-2);
        public static readonly IntPtr PerMonitorAwareV2 = new IntPtr(-4);
        public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8, MOD_NOREPEAT = 0x4000;
        const uint WDA_EXCLUDEFROMCAPTURE = 0x11;

        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr value);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint affinity);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int RegisterWindowMessage(string name);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(int processId);

        // Keeps the eyedropper's own windows out of the pixels it reads (Windows 10 2004 and later).
        public static void ExcludeFromCapture(IntPtr hWnd)
        {
            try { SetWindowDisplayAffinity(hWnd, WDA_EXCLUDEFROMCAPTURE); } catch { }
        }
    }

    // A short diagnostic log in %LOCALAPPDATA%\ColourPicker\log.txt, so problems can be traced without guessing.
    static class Log
    {
        static readonly string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColourPicker", "log.txt");

        public static void Write(string line)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                FileInfo fi = new FileInfo(path);
                if (fi.Exists && fi.Length > 200000) fi.Delete();
                File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + line + Environment.NewLine);
            }
            catch { }
        }
    }

    static class Program
    {
        public static readonly int PickNowMessage = Native.RegisterWindowMessage("IllumiEd.ColourPicker.PickNow");

        [STAThread]
        static void Main()
        {
            // The main window is system-DPI aware, so Windows scales it cleanly on any monitor.
            // The eyedropper switches its own thread to per-monitor awareness to read real pixels.
            try { if (!Native.SetProcessDpiAwarenessContext(Native.SystemAware)) Native.SetProcessDPIAware(); }
            catch { try { Native.SetProcessDPIAware(); } catch { } }

            bool first;
            using (Mutex single = new Mutex(true, "Local\\IllumiEd.ColourPicker", out first))
            {
                if (!first)
                {
                    // Already running: opening it again starts the eyedropper in the running copy.
                    Native.AllowSetForegroundWindow(-1);
                    Native.PostMessage(Native.HWND_BROADCAST, PickNowMessage, IntPtr.Zero, IntPtr.Zero);
                    return;
                }
                Log.Write("started, version " + Application.ProductVersion);
                Application.ThreadException += (s, e) =>
                {
                    Log.Write("ERROR " + e.Exception);
                    MessageBox.Show("Something went wrong: " + e.Exception.Message + "\n\nDetails are in %LOCALAPPDATA%\\ColourPicker\\log.txt.", "Colour Picker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                };
                AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Write("FATAL " + e.ExceptionObject);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }
    }

    static class Col
    {
        public static Color FromHsv(double h, double s, double v)
        {
            h = ((h % 360) + 360) % 360;
            double c = v * s, x = c * (1 - Math.Abs((h / 60) % 2 - 1)), m = v - c;
            double r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; }
            else if (h < 120) { r = x; g = c; }
            else if (h < 180) { g = c; b = x; }
            else if (h < 240) { g = x; b = c; }
            else if (h < 300) { r = x; b = c; }
            else { r = c; b = x; }
            return Color.FromArgb(To255(r + m), To255(g + m), To255(b + m));
        }

        static int To255(double d)
        {
            int i = (int)Math.Round(d * 255);
            return i < 0 ? 0 : (i > 255 ? 255 : i);
        }

        public static void ToHsv(Color c, out double h, out double s, out double v)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b)), d = mx - mn;
            h = 0;
            if (d > 0)
            {
                if (mx == r) h = 60 * (((g - b) / d) % 6);
                else if (mx == g) h = 60 * ((b - r) / d + 2);
                else h = 60 * ((r - g) / d + 4);
                if (h < 0) h += 360;
            }
            s = mx > 0 ? d / mx : 0;
            v = mx;
        }

        public static string Hex(Color c)
        {
            return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }

        public static bool TryParseHex(string t, out Color c)
        {
            c = Color.Empty;
            if (t == null) return false;
            t = t.Trim();
            if (t.StartsWith("#")) t = t.Substring(1);
            if (t.Length == 3) t = new string(new[] { t[0], t[0], t[1], t[1], t[2], t[2] });
            if (t.Length != 6) return false;
            int n;
            if (!int.TryParse(t, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out n)) return false;
            c = Color.FromArgb((n >> 16) & 255, (n >> 8) & 255, n & 255);
            return true;
        }

        public static double Clamp01(double x) { return x < 0 ? 0 : (x > 1 ? 1 : x); }
    }

    // Saturation (left to right) by brightness (top to bottom) for the current hue.
    class SpectrumBox : Control
    {
        public double Hue, Sat, Val;
        public event EventHandler Changed;
        Bitmap bmp;
        double builtHue = -1;

        public SpectrumBox()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
            TabStop = true;
            Cursor = Cursors.Cross;
            AccessibleName = "Saturation and brightness";
            AccessibleRole = AccessibleRole.Graphic;
        }

        void Build()
        {
            int w = Math.Max(2, Width), h = Math.Max(2, Height);
            if (bmp != null && bmp.Width == w && bmp.Height == h && builtHue == Hue) return;
            if (bmp == null || bmp.Width != w || bmp.Height != h)
            {
                if (bmp != null) bmp.Dispose();
                bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            }
            builtHue = Hue;
            Color pure = Col.FromHsv(Hue, 1, 1);
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            int[] row = new int[w];
            for (int y = 0; y < h; y++)
            {
                double v = 1 - (double)y / (h - 1);
                for (int x = 0; x < w; x++)
                {
                    double s = (double)x / (w - 1);
                    int r = (int)Math.Round(v * ((1 - s) * 255 + s * pure.R));
                    int g = (int)Math.Round(v * ((1 - s) * 255 + s * pure.G));
                    int b = (int)Math.Round(v * ((1 - s) * 255 + s * pure.B));
                    row[x] = unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;
                }
                Marshal.Copy(row, 0, IntPtr.Add(bd.Scan0, y * bd.Stride), w);
            }
            bmp.UnlockBits(bd);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Build();
            Graphics g = e.Graphics;
            g.DrawImageUnscaled(bmp, 0, 0);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float x = (float)(Sat * (Width - 1)), y = (float)((1 - Val) * (Height - 1));
            float r = 6f * DeviceDpi / 96f;
            using (Pen outer = new Pen(Color.Black, 1.5f))
            using (Pen inner = new Pen(Color.White, 2f))
            {
                g.DrawEllipse(outer, x - r - 1.5f, y - r - 1.5f, 2 * r + 3, 2 * r + 3);
                g.DrawEllipse(inner, x - r, y - r, 2 * r, 2 * r);
            }
            if (Focused) ControlPaint.DrawFocusRectangle(g, ClientRectangle);
        }

        void SetFromPoint(Point p)
        {
            Sat = Col.Clamp01((double)p.X / (Width - 1));
            Val = 1 - Col.Clamp01((double)p.Y / (Height - 1));
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        protected override void OnMouseDown(MouseEventArgs e) { Focus(); if (e.Button == MouseButtons.Left) SetFromPoint(e.Location); }
        protected override void OnMouseMove(MouseEventArgs e) { if (e.Button == MouseButtons.Left) SetFromPoint(e.Location); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override bool IsInputKey(Keys k)
        {
            Keys key = k & Keys.KeyCode;
            return key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down || base.IsInputKey(k);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            double step = e.Shift ? 0.1 : 0.01;
            if (e.KeyCode == Keys.Left) Sat = Col.Clamp01(Sat - step);
            else if (e.KeyCode == Keys.Right) Sat = Col.Clamp01(Sat + step);
            else if (e.KeyCode == Keys.Up) Val = Col.Clamp01(Val + step);
            else if (e.KeyCode == Keys.Down) Val = Col.Clamp01(Val - step);
            else { base.OnKeyDown(e); return; }
            e.Handled = true;
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }
    }

    // Vertical hue strip, red at the top.
    class HueBar : Control
    {
        public double Hue;
        public event EventHandler Changed;
        Bitmap bmp;

        public HueBar()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleName = "Hue";
            AccessibleRole = AccessibleRole.Slider;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            int w = Math.Max(2, Width), h = Math.Max(2, Height);
            if (bmp == null || bmp.Width != w || bmp.Height != h)
            {
                if (bmp != null) bmp.Dispose();
                bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                using (Graphics bg = Graphics.FromImage(bmp))
                    for (int y = 0; y < h; y++)
                        using (Pen p = new Pen(Col.FromHsv(360.0 * y / (h - 1), 1, 1)))
                            bg.DrawLine(p, 0, y, w, y);
            }
            Graphics g = e.Graphics;
            g.DrawImageUnscaled(bmp, 0, 0);
            int my = (int)Math.Round(Hue / 360 * (h - 1));
            int t = Math.Max(3, (int)Math.Round(3f * DeviceDpi / 96f));
            using (Pen outer = new Pen(Color.Black, 1f)) g.DrawRectangle(outer, 0, my - t, w - 1, 2 * t);
            using (Pen inner = new Pen(Color.White, 1f)) g.DrawRectangle(inner, 1, my - t + 1, w - 3, 2 * t - 2);
            if (Focused) ControlPaint.DrawFocusRectangle(g, ClientRectangle);
        }

        void SetFromY(int y)
        {
            Hue = 360 * Col.Clamp01((double)y / (Height - 1));
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        protected override void OnMouseDown(MouseEventArgs e) { Focus(); if (e.Button == MouseButtons.Left) SetFromY(e.Y); }
        protected override void OnMouseMove(MouseEventArgs e) { if (e.Button == MouseButtons.Left) SetFromY(e.Y); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override bool IsInputKey(Keys k)
        {
            Keys key = k & Keys.KeyCode;
            return key == Keys.Up || key == Keys.Down || base.IsInputKey(k);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            double step = e.Shift ? 10 : 1;
            if (e.KeyCode == Keys.Up) Hue = Math.Max(0, Hue - step);
            else if (e.KeyCode == Keys.Down) Hue = Math.Min(360, Hue + step);
            else { base.OnKeyDown(e); return; }
            e.Handled = true;
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }
    }

    // The magnifier that follows the pointer. Click-through, never takes focus,
    // and kept out of screen capture so it never appears in the pixels being read.
    class Loupe : Form
    {
        public const int Half = 5, N = 2 * Half + 1; // 11 x 11 pixels
        readonly float scale;
        readonly int cell, strip;
        readonly Color[] px = new Color[N * N];
        readonly Font hexFont, tipFont;

        public Loupe(float scale)
        {
            this.scale = scale;
            cell = P(12);
            strip = P(46);
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            ShowIcon = false;
            // No TopMost property here: WinForms makes a TopMost form active when it is shown,
            // which would take focus from the eyedropper. WS_EX_TOPMOST is set in CreateParams instead.
            DoubleBuffered = true;
            Opacity = 0.99; // makes it a layered window, so WS_EX_TRANSPARENT lets clicks pass through
            ClientSize = new Size(N * cell + 2, N * cell + 2 + strip);
            hexFont = new Font("Consolas", 12f, FontStyle.Bold);
            tipFont = new Font("Segoe UI", 8f);
            for (int i = 0; i < px.Length; i++) px[i] = Color.Gray;
        }

        int P(double v) { return (int)Math.Round(v * scale); }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08 /* WS_EX_TOPMOST */ | 0x20 /* WS_EX_TRANSPARENT */ | 0x80 /* WS_EX_TOOLWINDOW */ | 0x08000000 /* WS_EX_NOACTIVATE */;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Native.ExcludeFromCapture(Handle); }

        public Color Centre { get { return px[Half * N + Half]; } }

        public void ShowSample(Bitmap sample, Point cursor)
        {
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    Color c = sample.GetPixel(x, y);
                    px[y * N + x] = Color.FromArgb(c.R, c.G, c.B);
                }
            Rectangle mon = Screen.FromPoint(cursor).Bounds;
            int x0 = cursor.X + P(24), y0 = cursor.Y + P(24);
            if (x0 + Width > mon.Right) x0 = cursor.X - P(24) - Width;
            if (y0 + Height > mon.Bottom) y0 = cursor.Y - P(24) - Height;
            Location = new Point(x0, y0);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.White);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                    using (SolidBrush b = new SolidBrush(px[y * N + x]))
                        g.FillRectangle(b, 1 + x * cell, 1 + y * cell, cell, cell);
            using (Pen grid = new Pen(Color.FromArgb(50, 0, 0, 0)))
                for (int i = 1; i < N; i++)
                {
                    g.DrawLine(grid, 1 + i * cell, 1, 1 + i * cell, 1 + N * cell);
                    g.DrawLine(grid, 1, 1 + i * cell, 1 + N * cell, 1 + i * cell);
                }
            Rectangle cr = new Rectangle(1 + Half * cell, 1 + Half * cell, cell, cell);
            g.DrawRectangle(Pens.Black, cr);
            cr.Inflate(-1, -1);
            g.DrawRectangle(Pens.White, cr);

            Rectangle bar = new Rectangle(0, ClientSize.Height - strip, ClientSize.Width, strip);
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(28, 30, 36))) g.FillRectangle(sb, bar);
            Rectangle sw = new Rectangle(P(8), bar.Y + P(6), P(18), P(18));
            using (SolidBrush cb = new SolidBrush(Centre)) g.FillRectangle(cb, sw);
            g.DrawRectangle(Pens.White, sw);
            g.DrawString(Col.Hex(Centre), hexFont, Brushes.White, sw.Right + P(6), bar.Y + P(4));
            using (SolidBrush tip = new SolidBrush(Color.FromArgb(190, 196, 206)))
                g.DrawString("Click to pick · Esc cancels", tipFont, tip, P(7), bar.Y + P(27));
            using (Pen border = new Pen(Color.FromArgb(28, 30, 36))) g.DrawRectangle(border, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { hexFont.Dispose(); tipFont.Dispose(); }
            base.Dispose(disposing);
        }
    }

    // Live eyedropper: a nearly invisible window over every monitor catches the click,
    // while the screen underneath keeps updating and is read pixel by pixel as the pointer moves.
    // Created while the thread is per-monitor DPI aware, so coordinates are real screen pixels.
    class Eyedropper : Form
    {
        readonly Loupe loupe;
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly Bitmap sample = new Bitmap(Loupe.N, Loupe.N, PixelFormat.Format32bppRgb);
        bool pressed;
        public Color Picked;

        public Eyedropper(Rectangle virtualScreen, float scale)
        {
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            ShowIcon = false;
            TopMost = true;
            BackColor = Color.Black;
            Opacity = 0.01; // invisible to the eye, but still receives the mouse
            Cursor = Cursors.Cross;
            Text = "Pick a colour";
            Bounds = virtualScreen;
            loupe = new Loupe(scale);
            timer.Interval = 30;
            timer.Tick += delegate { Sample(); };
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Native.ExcludeFromCapture(Handle); }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Sample();
            loupe.Show(this);
            Activate();
            timer.Start();
            Log.Write("eyedropper shown: bounds " + Bounds + ", magnifier at " + loupe.Bounds + ", active " + (Form.ActiveForm == this));
        }

        void Sample()
        {
            Point p = Cursor.Position;
            try
            {
                using (Graphics g = Graphics.FromImage(sample))
                    g.CopyFromScreen(p.X - Loupe.Half, p.Y - Loupe.Half, 0, 0, new Size(Loupe.N, Loupe.N), CopyPixelOperation.SourceCopy);
            }
            catch (System.ComponentModel.Win32Exception) { return; } // e.g. a secure desktop prompt is showing
            loupe.ShowSample(sample, p);
        }

        void Finish(DialogResult result)
        {
            if (DialogResult != DialogResult.None) return;
            if (result == DialogResult.OK) { Sample(); Picked = loupe.Centre; }
            Log.Write("eyedropper finished: " + result + (result == DialogResult.OK ? " " + Col.Hex(Picked) : "") + " at " + Cursor.Position);
            DialogResult = result;
        }

        // Act on release, not press, so the release doesn't fall through to the window underneath.
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!pressed) return;
            Finish(e.Button == MouseButtons.Left ? DialogResult.OK : DialogResult.Cancel);
        }

        // Losing focus does not cancel: the eyedropper stays on top, and Esc or right-click cancels.
        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            Log.Write("eyedropper lost focus");
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode;
            int step = (keyData & Keys.Shift) == Keys.Shift ? 10 : 1;
            Point p = Cursor.Position;
            switch (k)
            {
                case Keys.Escape: Finish(DialogResult.Cancel); return true;
                case Keys.Enter: case Keys.Space: Finish(DialogResult.OK); return true;
                case Keys.Left: Cursor.Position = new Point(p.X - step, p.Y); return true;
                case Keys.Right: Cursor.Position = new Point(p.X + step, p.Y); return true;
                case Keys.Up: Cursor.Position = new Point(p.X, p.Y - step); return true;
                case Keys.Down: Cursor.Position = new Point(p.X, p.Y + step); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            loupe.Close();
            base.OnFormClosed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { timer.Dispose(); loupe.Dispose(); sample.Dispose(); }
            base.Dispose(disposing);
        }
    }

    class MainForm : Form
    {
        // Office's ten standard colours, as in PowerPoint's colour menu
        static readonly string[] Standard = { "#C00000", "#FF0000", "#FFC000", "#FFFF00", "#92D050", "#00B050", "#00B0F0", "#0070C0", "#002060", "#7030A0" };
        const int MaxRecent = 12;
        const int HotkeyId = 1;

        float S = 1f;
        double H, Sa, V;
        bool updating, picking;
        string hotkeyName;
        readonly List<string> recent = new List<string>();
        readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColourPicker", "settings.txt");

        SpectrumBox spec;
        HueBar hueBar;
        Panel preview, recentPanel;
        Button hexBtn, pickBtn, copyRgbBtn;
        Label copyHint, recentEmpty, hotkeyLabel;
        TextBox hexBox;
        NumericUpDown nR, nG, nB;
        CheckBox topBox, startBox;
        ToolTip tips = new ToolTip();
        System.Windows.Forms.Timer hintTimer = new System.Windows.Forms.Timer();

        int P(double v) { return (int)Math.Round(v * S); }
        Rectangle R(int x, int y, int w, int h) { return new Rectangle(P(x), P(y), P(w), P(h)); }

        public MainForm()
        {
            AutoScaleMode = AutoScaleMode.None;
            using (Graphics g = CreateGraphics()) S = g.DpiX / 96f;
            Text = "Colour Picker";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            KeyPreview = true;
            Font = new Font("Segoe UI", 9f);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            ClientSize = new Size(P(470), P(458));

            spec = new SpectrumBox { Bounds = R(12, 12, 260, 220), TabIndex = 10 };
            hueBar = new HueBar { Bounds = R(282, 12, 22, 220), TabIndex = 11 };
            tips.SetToolTip(spec, "Drag to choose saturation and brightness. Arrow keys fine-tune.");
            tips.SetToolTip(hueBar, "Drag to choose the hue. Up and Down keys fine-tune.");

            pickBtn = new Button { Bounds = R(316, 12, 142, 44), Text = "&Pick from screen", Font = new Font("Segoe UI", 10f, FontStyle.Bold), TabIndex = 0 };
            tips.SetToolTip(pickBtn, "Eyedropper: point at anything on any screen and click to pick its colour");
            pickBtn.Click += delegate { Pick(); };

            preview = new Panel { Bounds = R(316, 64, 142, 58), BorderStyle = BorderStyle.FixedSingle, Cursor = Cursors.Hand, AccessibleName = "Colour preview, click to copy" };
            tips.SetToolTip(preview, "Click to copy the hex code");
            // Moving focus first commits any half-typed R, G or B value.
            preview.Click += delegate { hexBtn.Focus(); CopyHex(); };

            hexBtn = new Button { Bounds = R(316, 130, 142, 40), Font = new Font("Consolas", 14f, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, TabIndex = 1, UseMnemonic = false };
            hexBtn.FlatAppearance.BorderColor = SystemColors.ControlDark;
            tips.SetToolTip(hexBtn, "Click to copy the hex code (Ctrl+C)");
            hexBtn.Click += delegate { CopyHex(); };

            copyHint = new Label { Bounds = R(316, 172, 142, 20), Text = "Click to copy", TextAlign = ContentAlignment.MiddleCenter, ForeColor = SystemColors.GrayText };

            copyRgbBtn = new Button { Bounds = R(316, 202, 142, 30), Text = "Copy &RGB", TabIndex = 2 };
            tips.SetToolTip(copyRgbBtn, "Copy as R, G, B numbers, e.g. 68, 114, 196");
            copyRgbBtn.Click += delegate
            {
                Color c = Current();
                CopyText(c.R + ", " + c.G + ", " + c.B, "Copied RGB");
                AddRecent(Col.Hex(c));
            };

            Controls.Add(new Label { Text = "Hex", Bounds = R(12, 250, 30, 20), TextAlign = ContentAlignment.MiddleLeft });
            hexBox = new TextBox { Bounds = R(44, 248, 84, 24), Font = new Font("Consolas", 10f), CharacterCasing = CharacterCasing.Upper, MaxLength = 32, TabIndex = 12, AccessibleName = "Hex code" };
            tips.SetToolTip(hexBox, "Type or paste a hex code, e.g. #4472C4");
            nR = MakeNum(156, 13, "Red"); nG = MakeNum(238, 14, "Green"); nB = MakeNum(320, 15, "Blue");
            Controls.Add(new Label { Text = "R", Bounds = R(140, 250, 16, 20), TextAlign = ContentAlignment.MiddleLeft });
            Controls.Add(new Label { Text = "G", Bounds = R(222, 250, 16, 20), TextAlign = ContentAlignment.MiddleLeft });
            Controls.Add(new Label { Text = "B", Bounds = R(304, 250, 16, 20), TextAlign = ContentAlignment.MiddleLeft });

            Controls.Add(new Label { Text = "Standard colours", Bounds = R(12, 286, 200, 18), ForeColor = SystemColors.GrayText });
            for (int i = 0; i < Standard.Length; i++)
            {
                Color c; Col.TryParseHex(Standard[i], out c);
                Button sw = MakeSwatch(c, R(12 + i * 36, 306, 30, 30));
                sw.TabIndex = 20 + i;
                Controls.Add(sw);
            }

            Controls.Add(new Label { Text = "Recent colours", Bounds = R(12, 346, 200, 18), ForeColor = SystemColors.GrayText });
            recentPanel = new Panel { Bounds = R(12, 366, 450, 32), TabIndex = 40 };
            recentEmpty = new Label { Text = "Colours you copy or pick will appear here.", Bounds = new Rectangle(0, P(6), P(440), P(20)), ForeColor = SystemColors.GrayText };

            topBox = new CheckBox { Text = "Always on top", Bounds = R(12, 404, 130, 22), TabIndex = 50 };
            topBox.CheckedChanged += delegate { TopMost = topBox.Checked; };
            startBox = new CheckBox { Text = "Start picking when it opens", Bounds = R(150, 404, 300, 22), TabIndex = 51, Checked = true };
            hotkeyLabel = new Label { Bounds = R(12, 430, 446, 20), ForeColor = SystemColors.GrayText };

            Controls.AddRange(new Control[] { spec, hueBar, pickBtn, preview, hexBtn, copyHint, copyRgbBtn, hexBox, nR, nG, nB, recentPanel, topBox, startBox, hotkeyLabel });

            spec.Changed += delegate { Sa = spec.Sat; V = spec.Val; Sync(spec); };
            hueBar.Changed += delegate { H = hueBar.Hue; Sync(hueBar); };
            // Apply only complete 6-digit codes while typing; 3-digit shorthand is expanded when the entry is committed.
            hexBox.TextChanged += delegate
            {
                if (updating) return;
                string t = hexBox.Text.Trim().TrimStart('#');
                Color c;
                if (t.Length == 6 && Col.TryParseHex(t, out c)) SetFrom(c, hexBox);
            };
            hexBox.Leave += delegate { CommitHexBox(); };
            hexBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; CommitHexBox(); hexBox.SelectAll(); } };
            EventHandler numChanged = (s, e) =>
            {
                if (updating) return;
                SetFrom(Color.FromArgb((int)nR.Value, (int)nG.Value, (int)nB.Value), s);
            };
            nR.ValueChanged += numChanged; nG.ValueChanged += numChanged; nB.ValueChanged += numChanged;

            hintTimer.Interval = 2500;
            hintTimer.Tick += delegate { hintTimer.Stop(); copyHint.Text = "Click to copy"; copyHint.ForeColor = SystemColors.GrayText; };

            LoadSettings();
            RebuildRecent();
            Sync(null);
            ShowHotkey();
        }

        // Global shortcut: Win+Shift+C, or Ctrl+Alt+Shift+C if another app already owns that.
        // The handle is created early (CreateGraphics at the top of the constructor), before the
        // controls exist, so this only registers the key; ShowHotkey fills in the label later.
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            hotkeyName = null;
            if (Native.RegisterHotKey(Handle, HotkeyId, Native.MOD_WIN | Native.MOD_SHIFT | Native.MOD_NOREPEAT, (uint)Keys.C)) hotkeyName = "Win+Shift+C";
            else if (Native.RegisterHotKey(Handle, HotkeyId, Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_SHIFT | Native.MOD_NOREPEAT, (uint)Keys.C)) hotkeyName = "Ctrl+Alt+Shift+C";
            ShowHotkey();
        }

        void ShowHotkey()
        {
            if (hotkeyLabel == null || pickBtn == null) return;
            hotkeyLabel.Text = hotkeyName != null
                ? hotkeyName + " picks a colour from anywhere while this app is open."
                : "No keyboard shortcut: Win+Shift+C is taken by another app.";
            tips.SetToolTip(pickBtn, "Eyedropper: point at anything on any screen and click to pick its colour" + (hotkeyName != null ? " (" + hotkeyName + ")" : ""));
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            Native.UnregisterHotKey(Handle, HotkeyId);
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if ((m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId) || (m.Msg == Program.PickNowMessage && Program.PickNowMessage != 0))
            {
                BeginInvoke(new MethodInvoker(Pick));
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (startBox.Checked) BeginInvoke(new MethodInvoker(Pick));
        }

        NumericUpDown MakeNum(int x, int tab, string name)
        {
            return new NumericUpDown { Bounds = R(x, 248, 56, 24), Minimum = 0, Maximum = 255, TabIndex = tab, AccessibleName = name };
        }

        Button MakeSwatch(Color c, Rectangle bounds)
        {
            string hex = Col.Hex(c);
            Button b = new Button { Bounds = bounds, BackColor = c, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, TabStop = true, Tag = c, AccessibleName = hex };
            b.FlatAppearance.BorderColor = SystemColors.ControlDark;
            b.FlatAppearance.MouseOverBackColor = c;
            b.FlatAppearance.MouseDownBackColor = c;
            tips.SetToolTip(b, hex);
            b.Click += delegate { SetFrom(c, null); };
            return b;
        }

        Color Current() { return Col.FromHsv(H, Sa, V); }

        void CommitHexBox()
        {
            Color c;
            if (Col.TryParseHex(hexBox.Text, out c)) SetFrom(c, null);
            else Sync(null);
        }

        void SetFrom(Color c, object source)
        {
            double h, s, v;
            Col.ToHsv(c, out h, out s, out v);
            if (s > 0 && v > 0) H = h;
            Sa = s; V = v;
            Sync(source);
        }

        void Sync(object source)
        {
            if (updating) return;
            updating = true;
            try
            {
                Color c = Current();
                string hex = Col.Hex(c);
                spec.Hue = H; spec.Sat = Sa; spec.Val = V; spec.Invalidate();
                hueBar.Hue = H; hueBar.Invalidate();
                preview.BackColor = c;
                hexBtn.Text = hex;
                hexBtn.AccessibleName = hex + ", click to copy";
                if (source != hexBox) hexBox.Text = hex;
                if (source != nR) nR.Value = c.R;
                if (source != nG) nG.Value = c.G;
                if (source != nB) nB.Value = c.B;
            }
            finally { updating = false; }
        }

        void ShowHint(string text, bool ok)
        {
            tips.SetToolTip(copyHint, null);
            copyHint.Text = text;
            copyHint.ForeColor = ok ? Color.FromArgb(0, 120, 60) : Color.FromArgb(170, 40, 30);
            hexBtn.AccessibleDescription = text;
            hintTimer.Stop();
            hintTimer.Start();
        }

        void CopyText(string text, string hint)
        {
            try { Clipboard.SetDataObject(text, true, 5, 100); ShowHint(hint, true); }
            catch (Exception) { ShowHint("Clipboard busy, try again", false); }
        }

        void CopyHex()
        {
            string hex = Col.Hex(Current());
            CopyText(hex, "Copied " + hex);
            AddRecent(hex);
        }

        void Pick()
        {
            if (picking) return;
            picking = true;
            // Get this window out of the way so whatever is behind it can be picked too.
            Opacity = 0;
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Application.DoEvents();
            IntPtr prevCtx = IntPtr.Zero;
            try { prevCtx = Native.SetThreadDpiAwarenessContext(Native.PerMonitorAwareV2); }
            catch (EntryPointNotFoundException) { }
            bool picked = false;
            try
            {
                Log.Write("pick started: virtual screen " + SystemInformation.VirtualScreen + ", per-monitor DPI " + (prevCtx != IntPtr.Zero) + ", scale " + S);
                using (Eyedropper ed = new Eyedropper(SystemInformation.VirtualScreen, S))
                {
                    if (ed.ShowDialog(this) == DialogResult.OK)
                    {
                        SetFrom(ed.Picked, null);
                        AddRecent(Col.Hex(ed.Picked));
                        ShowHint("Picked " + Col.Hex(ed.Picked), true);
                        picked = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("ERROR in pick: " + ex);
                ShowHint("Couldn't read the screen", false);
                tips.SetToolTip(copyHint, ex.Message);
            }
            finally
            {
                if (prevCtx != IntPtr.Zero) Native.SetThreadDpiAwarenessContext(prevCtx);
                Opacity = 1;
                Activate();
                picking = false;
                // Ready for another go straight away: one click, Enter or Space picks again.
                if (picked) pickBtn.Text = "&Pick again";
                if (picked) hexBtn.Focus(); else pickBtn.Focus();
            }
        }

        void AddRecent(string hex)
        {
            recent.Remove(hex);
            recent.Insert(0, hex);
            if (recent.Count > MaxRecent) recent.RemoveRange(MaxRecent, recent.Count - MaxRecent);
            RebuildRecent();
            SaveSettings();
        }

        void RebuildRecent()
        {
            bool hadFocus = recentPanel.ContainsFocus;
            recentPanel.SuspendLayout();
            Control[] old = new Control[recentPanel.Controls.Count];
            recentPanel.Controls.CopyTo(old, 0);
            recentPanel.Controls.Clear();
            foreach (Control c in old)
                if (c != recentEmpty) { tips.SetToolTip(c, null); c.Dispose(); }
            if (recent.Count == 0) recentPanel.Controls.Add(recentEmpty);
            for (int i = 0; i < recent.Count; i++)
            {
                Color c;
                if (Col.TryParseHex(recent[i], out c))
                {
                    Button sw = MakeSwatch(c, new Rectangle(P(i * 36), 0, P(30), P(30)));
                    sw.TabIndex = i;
                    recentPanel.Controls.Add(sw);
                }
            }
            recentPanel.ResumeLayout();
            if (hadFocus) hexBtn.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.C) && !(ActiveControl is TextBox) && !(ActiveControl is NumericUpDown))
            {
                CopyHex();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void LoadSettings()
        {
            Color start = Color.FromArgb(0x44, 0x72, 0xC4); // Office "Blue, Accent 1"
            try
            {
                if (File.Exists(settingsPath))
                {
                    foreach (string line in File.ReadAllLines(settingsPath))
                    {
                        int eq = line.IndexOf('=');
                        if (eq < 0) continue;
                        string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                        Color c;
                        if (k == "last" && Col.TryParseHex(v, out c)) start = c;
                        else if (k == "top") topBox.Checked = v == "1";
                        else if (k == "pickOnStart") startBox.Checked = v != "0";
                        else if (k == "recent")
                        {
                            foreach (string h in v.Split(','))
                                if (Col.TryParseHex(h, out c) && recent.Count < MaxRecent) recent.Add(Col.Hex(c));
                        }
                        else if (k == "pos")
                        {
                            string[] xy = v.Split(',');
                            int x, y;
                            if (xy.Length == 2 && int.TryParse(xy[0], out x) && int.TryParse(xy[1], out y))
                            {
                                Point p = new Point(x, y);
                                foreach (Screen s in Screen.AllScreens)
                                    if (s.WorkingArea.Contains(new Point(x + P(40), y + P(10)))) { StartPosition = FormStartPosition.Manual; Location = p; break; }
                            }
                        }
                    }
                }
            }
            catch { }
            SetFrom(start, null);
        }

        void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                Point pos = WindowState == FormWindowState.Normal ? Location : RestoreBounds.Location;
                File.WriteAllLines(settingsPath, new[]
                {
                    "last=" + Col.Hex(Current()),
                    "top=" + (topBox.Checked ? "1" : "0"),
                    "pickOnStart=" + (startBox.Checked ? "1" : "0"),
                    "recent=" + string.Join(",", recent.ToArray()),
                    "pos=" + pos.X + "," + pos.Y
                });
            }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveSettings();
            base.OnFormClosing(e);
        }
    }
}
