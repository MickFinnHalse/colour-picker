// Colour Picker: a small PowerPoint-style colour picker for Windows.
// Spectrum + hue bar, Hex/RGB fields, screen eyedropper, standard and recent colours,
// and a hex button that copies the code to the clipboard.
// Built with the C# 5 compiler that ships with .NET Framework 4.x (see build.ps1).

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Colour Picker")]
[assembly: System.Reflection.AssemblyProduct("Colour Picker")]
[assembly: System.Reflection.AssemblyVersion("1.1.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.1.0.0")]

namespace ColourPicker
{
    static class Native
    {
        public static readonly IntPtr SystemAware = new IntPtr(-2);
        public static readonly IntPtr PerMonitorAwareV2 = new IntPtr(-4);
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr value);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            // The main window is system-DPI aware, so Windows scales it cleanly on any monitor.
            // The eyedropper switches its own thread to per-monitor awareness to read real pixels.
            try { if (!Native.SetProcessDpiAwarenessContext(Native.SystemAware)) Native.SetProcessDPIAware(); }
            catch { try { Native.SetProcessDPIAware(); } catch { } }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
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

    // Full-screen eyedropper over a frozen screenshot of every monitor, with a magnifier.
    // Created while the thread is per-monitor DPI aware, so its pixels are real screen pixels.
    class PickOverlay : Form
    {
        readonly Bitmap shot;
        readonly Rectangle vs;
        readonly float scale;
        readonly int cell;
        const int Half = 5; // 11 x 11 pixels in the magnifier
        Point cur;
        Rectangle help;
        bool pressed;
        public Color Picked;

        public PickOverlay(Bitmap shot, Rectangle virtualScreen, float scale)
        {
            this.shot = shot;
            vs = virtualScreen;
            this.scale = scale;
            cell = (int)Math.Round(12 * scale);
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            ShowIcon = false;
            TopMost = true;
            // Painted through a small per-paint buffer instead of a screen-sized double buffer.
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);
            Cursor = Cursors.Cross;
            Text = "Pick a colour";
            Bounds = vs;
            Point p = Cursor.Position;
            cur = new Point(p.X - vs.X, p.Y - vs.Y);

            Rectangle mon = Screen.FromPoint(p).Bounds;
            int hw = P(440), hh = P(34);
            help = new Rectangle(mon.X - vs.X + (mon.Width - hw) / 2, mon.Y - vs.Y + P(16), hw, hh);
        }

        int P(double v) { return (int)Math.Round(v * scale); }

        protected override void OnShown(EventArgs e) { base.OnShown(e); Activate(); }

        // If something else takes focus (Alt+Tab, a notification), cancel rather than pick on the next click.
        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (Visible && DialogResult == DialogResult.None) DialogResult = DialogResult.Cancel;
        }

        Color PixelAt(int x, int y)
        {
            if (x < 0 || y < 0 || x >= shot.Width || y >= shot.Height) return Color.Gray;
            Color c = shot.GetPixel(x, y);
            return Color.FromArgb(c.R, c.G, c.B);
        }

        Rectangle Loupe()
        {
            int size = (2 * Half + 1) * cell + 2;
            int lh = size + P(30);
            Rectangle mon = Screen.FromPoint(new Point(cur.X + vs.X, cur.Y + vs.Y)).Bounds;
            mon.Offset(-vs.X, -vs.Y);
            int x = cur.X + P(22), y = cur.Y + P(22);
            if (x + size > mon.Right) x = cur.X - P(22) - size;
            if (y + lh > mon.Bottom) y = cur.Y - P(22) - lh;
            return new Rectangle(x, y, size, lh);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle clip = Rectangle.Intersect(e.ClipRectangle, ClientRectangle);
            if (clip.Width <= 0 || clip.Height <= 0) return;
            using (Bitmap buf = new Bitmap(clip.Width, clip.Height, PixelFormat.Format32bppPArgb))
            {
                using (Graphics g = Graphics.FromImage(buf))
                {
                    g.TranslateTransform(-clip.X, -clip.Y);
                    Draw(g, clip);
                }
                e.Graphics.DrawImageUnscaled(buf, clip.X, clip.Y);
            }
        }

        void Draw(Graphics g, Rectangle clip)
        {
            g.DrawImage(shot, clip, clip, GraphicsUnit.Pixel);

            // Instructions
            if (help.IntersectsWith(clip))
            {
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(235, 28, 30, 36)))
                    g.FillRectangle(bg, help);
                using (Font f = new Font("Segoe UI", 10f))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString("Click to pick  ·  Arrow keys nudge  ·  Enter picks  ·  Esc cancels", f, Brushes.White, help, sf);
            }

            // Magnifier
            Rectangle L = Loupe();
            if (!L.IntersectsWith(clip)) return;
            g.FillRectangle(Brushes.White, L);
            int ox = L.X + 1, oy = L.Y + 1;
            for (int dy = -Half; dy <= Half; dy++)
                for (int dx = -Half; dx <= Half; dx++)
                    using (SolidBrush b = new SolidBrush(PixelAt(cur.X + dx, cur.Y + dy)))
                        g.FillRectangle(b, ox + (dx + Half) * cell, oy + (dy + Half) * cell, cell, cell);
            using (Pen grid = new Pen(Color.FromArgb(50, 0, 0, 0)))
                for (int i = 1; i < 2 * Half + 1; i++)
                {
                    g.DrawLine(grid, ox + i * cell, oy, ox + i * cell, oy + (2 * Half + 1) * cell);
                    g.DrawLine(grid, ox, oy + i * cell, ox + (2 * Half + 1) * cell, oy + i * cell);
                }
            Rectangle cr = new Rectangle(ox + Half * cell, oy + Half * cell, cell, cell);
            g.DrawRectangle(Pens.Black, cr);
            cr.Inflate(-1, -1);
            g.DrawRectangle(Pens.White, cr);

            Color c = PixelAt(cur.X, cur.Y);
            Rectangle strip = new Rectangle(L.X, L.Bottom - P(30), L.Width, P(30));
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(28, 30, 36))) g.FillRectangle(sb, strip);
            Rectangle sw = new Rectangle(strip.X + P(8), strip.Y + P(7), P(16), P(16));
            using (SolidBrush cb = new SolidBrush(c)) g.FillRectangle(cb, sw);
            g.DrawRectangle(Pens.White, sw);
            using (Font f = new Font("Consolas", 11f, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center })
                g.DrawString(Col.Hex(c), f, Brushes.White, new Rectangle(sw.Right + P(8), strip.Y, strip.Width - P(40), strip.Height), sf);
            using (Pen border = new Pen(Color.FromArgb(28, 30, 36))) g.DrawRectangle(border, L.X, L.Y, L.Width - 1, L.Height - 1);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Rectangle old = Loupe();
            cur = e.Location;
            Invalidate(old);
            Invalidate(Loupe());
        }

        // Act on release, not press, so the release doesn't fall through to the window underneath.
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!pressed) return;
            if (e.Button == MouseButtons.Left) { Picked = PixelAt(e.X, e.Y); DialogResult = DialogResult.OK; }
            else DialogResult = DialogResult.Cancel;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode;
            int step = (keyData & Keys.Shift) == Keys.Shift ? 10 : 1;
            Point p = Cursor.Position;
            switch (k)
            {
                case Keys.Escape: DialogResult = DialogResult.Cancel; return true;
                case Keys.Enter: Picked = PixelAt(cur.X, cur.Y); DialogResult = DialogResult.OK; return true;
                case Keys.Left: Cursor.Position = new Point(p.X - step, p.Y); return true;
                case Keys.Right: Cursor.Position = new Point(p.X + step, p.Y); return true;
                case Keys.Up: Cursor.Position = new Point(p.X, p.Y - step); return true;
                case Keys.Down: Cursor.Position = new Point(p.X, p.Y + step); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    class MainForm : Form
    {
        // Office's ten standard colours, as in PowerPoint's colour menu
        static readonly string[] Standard = { "#C00000", "#FF0000", "#FFC000", "#FFFF00", "#92D050", "#00B050", "#00B0F0", "#0070C0", "#002060", "#7030A0" };
        const int MaxRecent = 12;

        float S = 1f;
        double H, Sa, V;
        bool updating;
        readonly List<string> recent = new List<string>();
        readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColourPicker", "settings.txt");

        SpectrumBox spec;
        HueBar hueBar;
        Panel preview, recentPanel;
        Button hexBtn, pickBtn, copyRgbBtn;
        Label copyHint, recentEmpty;
        TextBox hexBox;
        NumericUpDown nR, nG, nB;
        CheckBox topBox;
        ToolTip tips = new ToolTip();
        Timer hintTimer = new Timer();

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
            ClientSize = new Size(P(470), P(432));

            spec = new SpectrumBox { Bounds = R(12, 12, 260, 220), TabIndex = 0 };
            hueBar = new HueBar { Bounds = R(282, 12, 22, 220), TabIndex = 1 };
            tips.SetToolTip(spec, "Drag to choose saturation and brightness. Arrow keys fine-tune.");
            tips.SetToolTip(hueBar, "Drag to choose the hue. Up and Down keys fine-tune.");

            preview = new Panel { Bounds = R(316, 12, 142, 72), BorderStyle = BorderStyle.FixedSingle, Cursor = Cursors.Hand, AccessibleName = "Colour preview, click to copy" };
            tips.SetToolTip(preview, "Click to copy the hex code");
            // Moving focus first commits any half-typed R, G or B value.
            preview.Click += delegate { hexBtn.Focus(); CopyHex(); };

            hexBtn = new Button { Bounds = R(316, 92, 142, 40), Font = new Font("Consolas", 14f, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, TabIndex = 2, UseMnemonic = false };
            hexBtn.FlatAppearance.BorderColor = SystemColors.ControlDark;
            tips.SetToolTip(hexBtn, "Click to copy the hex code (Ctrl+C)");
            hexBtn.Click += delegate { CopyHex(); };

            copyHint = new Label { Bounds = R(316, 134, 142, 20), Text = "Click to copy", TextAlign = ContentAlignment.MiddleCenter, ForeColor = SystemColors.GrayText };

            pickBtn = new Button { Bounds = R(316, 160, 142, 34), Text = "&Pick from screen", TabIndex = 3 };
            tips.SetToolTip(pickBtn, "Eyedropper: pick a colour from anywhere on any screen");
            pickBtn.Click += delegate { Pick(); };

            copyRgbBtn = new Button { Bounds = R(316, 202, 142, 30), Text = "Copy &RGB", TabIndex = 4 };
            tips.SetToolTip(copyRgbBtn, "Copy as R, G, B numbers, e.g. 68, 114, 196");
            copyRgbBtn.Click += delegate
            {
                Color c = Current();
                CopyText(c.R + ", " + c.G + ", " + c.B, "Copied RGB");
                AddRecent(Col.Hex(c));
            };

            Controls.Add(new Label { Text = "Hex", Bounds = R(12, 250, 30, 20), TextAlign = ContentAlignment.MiddleLeft });
            hexBox = new TextBox { Bounds = R(44, 248, 84, 24), Font = new Font("Consolas", 10f), CharacterCasing = CharacterCasing.Upper, MaxLength = 32, TabIndex = 5, AccessibleName = "Hex code" };
            tips.SetToolTip(hexBox, "Type or paste a hex code, e.g. #4472C4");
            nR = MakeNum(156, 6, "Red"); nG = MakeNum(238, 7, "Green"); nB = MakeNum(320, 8, "Blue");
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

            topBox = new CheckBox { Text = "Always on top", Bounds = R(12, 404, 200, 22), TabIndex = 50 };
            topBox.CheckedChanged += delegate { TopMost = topBox.Checked; };

            Controls.AddRange(new Control[] { spec, hueBar, preview, hexBtn, copyHint, pickBtn, copyRgbBtn, hexBox, nR, nG, nB, recentPanel, topBox });

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

            hintTimer.Interval = 1600;
            hintTimer.Tick += delegate { hintTimer.Stop(); copyHint.Text = "Click to copy"; copyHint.ForeColor = SystemColors.GrayText; };

            LoadSettings();
            RebuildRecent();
            Sync(null);
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
            Opacity = 0;
            Application.DoEvents();
            System.Threading.Thread.Sleep(200);
            IntPtr prevCtx = IntPtr.Zero;
            try { prevCtx = Native.SetThreadDpiAwarenessContext(Native.PerMonitorAwareV2); }
            catch (EntryPointNotFoundException) { }
            Bitmap shot = null;
            bool picked = false;
            try
            {
                Rectangle vs = SystemInformation.VirtualScreen;
                shot = new Bitmap(vs.Width, vs.Height, PixelFormat.Format32bppRgb);
                using (Graphics g = Graphics.FromImage(shot))
                    g.CopyFromScreen(vs.Location, Point.Empty, vs.Size, CopyPixelOperation.SourceCopy);
                using (PickOverlay ov = new PickOverlay(shot, vs, S))
                {
                    if (ov.ShowDialog(this) == DialogResult.OK)
                    {
                        SetFrom(ov.Picked, null);
                        AddRecent(Col.Hex(ov.Picked));
                        ShowHint("Picked " + Col.Hex(ov.Picked), true);
                        picked = true;
                    }
                }
            }
            catch (Exception ex)
            {
                ShowHint("Couldn't read the screen", false);
                tips.SetToolTip(copyHint, ex.Message);
            }
            finally
            {
                if (prevCtx != IntPtr.Zero) Native.SetThreadDpiAwarenessContext(prevCtx);
                if (shot != null) shot.Dispose();
                Opacity = 1;
                Activate();
                // Ready for another go straight away: one click, Enter or Space picks again.
                if (picked) pickBtn.Text = "&Pick again";
                pickBtn.Focus();
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
