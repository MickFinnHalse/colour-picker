// Colour Picker Setup: a small per-user installer with ColourPicker.exe embedded in it.
// No admin rights needed. Installs to %LOCALAPPDATA%\Programs\ColourPicker by default,
// creates Desktop and Start menu shortcuts, and registers in Settings > Apps so it can be
// uninstalled. Run with /uninstall to remove it (that is what Settings > Apps runs).
// Built with the C# 5 compiler that ships with .NET Framework 4.x (see build.ps1 -Package).

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Colour Picker Setup")]
[assembly: AssemblyProduct("Colour Picker")]
[assembly: AssemblyVersion("2.0.2.0")]
[assembly: AssemblyFileVersion("2.0.2.0")]

namespace ColourPickerSetup
{
    static class Installer
    {
        public const string AppName = "Colour Picker";
        public const string ExeName = "ColourPicker.exe";
        public const string SetupName = "ColourPickerSetup.exe";
        public const string Publisher = "Illumin-Ed";
        public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\IllumiEd.ColourPicker";

        public static string Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version.ToString(3); }
        }

        public static string DefaultFolder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ColourPicker"); }
        }

        public static string DesktopShortcut
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk"); }
        }

        public static string StartMenuShortcut
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk"); }
        }

        // The folder from a previous install, if there is one.
        public static string InstalledFolder()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(UninstallKey))
            {
                string dir = k == null ? null : k.GetValue("InstallLocation") as string;
                return string.IsNullOrEmpty(dir) ? null : dir;
            }
        }

        // Ask a running Colour Picker to close so its exe can be replaced or removed.
        public static bool CloseRunningApp()
        {
            foreach (Process p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ExeName)))
            {
                try
                {
                    p.CloseMainWindow();
                    if (!p.WaitForExit(4000)) return false;
                }
                catch (InvalidOperationException) { } // already gone
                finally { p.Dispose(); }
            }
            return true;
        }

        // Copies the app (and this installer, for uninstalling) into the folder,
        // then creates shortcuts and the Settings > Apps entry as asked.
        public static string Install(string folder, Stream payload, bool desktop, bool startMenu, bool register)
        {
            Directory.CreateDirectory(folder);
            string exe = Path.Combine(folder, ExeName);
            using (FileStream fs = File.Create(exe)) payload.CopyTo(fs);

            string self = Application.ExecutablePath;
            string setupCopy = Path.Combine(folder, SetupName);
            if (register && !string.Equals(Path.GetFullPath(self), Path.GetFullPath(setupCopy), StringComparison.OrdinalIgnoreCase))
                File.Copy(self, setupCopy, true);

            if (desktop) MakeShortcut(DesktopShortcut, exe, folder);
            if (startMenu) MakeShortcut(StartMenuShortcut, exe, folder);

            if (register)
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(UninstallKey))
                {
                    k.SetValue("DisplayName", AppName);
                    k.SetValue("DisplayVersion", Version);
                    k.SetValue("Publisher", Publisher);
                    k.SetValue("DisplayIcon", exe + ",0");
                    k.SetValue("InstallLocation", folder);
                    k.SetValue("UninstallString", "\"" + setupCopy + "\" /uninstall");
                    k.SetValue("QuietUninstallString", "\"" + setupCopy + "\" /uninstall /quiet");
                    k.SetValue("URLInfoAbout", "https://github.com/MickFinnHalse/colour-picker");
                    k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    k.SetValue("EstimatedSize", (int)Math.Max(1, new FileInfo(exe).Length / 1024 + 60), RegistryValueKind.DWord);
                }
            }
            return exe;
        }

        public static void MakeShortcut(string lnkPath, string target, string workingDir)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            object lnk = null;
            try
            {
                lnk = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                Type t = lnk.GetType();
                t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[] { target });
                t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk, new object[] { workingDir });
                t.InvokeMember("IconLocation", BindingFlags.SetProperty, null, lnk, new object[] { target + ",0" });
                t.InvokeMember("Description", BindingFlags.SetProperty, null, lnk, new object[] { "Pick a colour from anywhere on screen and copy its hex code" });
                t.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
            }
            finally
            {
                if (lnk != null) Marshal.FinalReleaseComObject(lnk);
                Marshal.FinalReleaseComObject(shell);
            }
        }

        // Removes the app, its settings and log, the shortcuts and the Settings > Apps entry.
        // The installer copy inside the folder cannot delete itself while running, so a hidden
        // command removes it and the folder a few seconds after this process exits.
        public static void Uninstall(string folder)
        {
            if (folder != null)
            {
                foreach (string name in new[] { ExeName, "settings.txt", "log.txt" })
                {
                    string f = Path.Combine(folder, name);
                    if (File.Exists(f)) File.Delete(f);
                }
            }
            foreach (string lnk in new[] { DesktopShortcut, StartMenuShortcut })
                if (File.Exists(lnk)) File.Delete(lnk);
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);

            // Only ever remove a folder this installer would have made.
            if (folder == null || !string.Equals(Path.GetFileName(folder.TrimEnd('\\')), "ColourPicker", StringComparison.OrdinalIgnoreCase)) return;
            string self = Application.ExecutablePath;
            bool selfInFolder = string.Equals(Path.GetDirectoryName(Path.GetFullPath(self)).TrimEnd('\\'), Path.GetFullPath(folder).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
            if (selfInFolder)
            {
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe",
                    "/c ping 127.0.0.1 -n 4 >nul & del /f /q \"" + self + "\" & rmdir \"" + folder + "\"");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
            }
            else
            {
                try { Directory.Delete(folder, false); } catch (IOException) { } // not empty: leave the user's other files
            }
        }

        public static Stream Payload()
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream(ExeName);
        }
    }

    class SetupForm : Form
    {
        float S = 1f;
        TextBox folderBox;
        CheckBox desktopBox, startBox, launchBox;
        Button installBtn, cancelBtn;
        Label status;

        int P(double v) { return (int)Math.Round(v * S); }
        Rectangle R(int x, int y, int w, int h) { return new Rectangle(P(x), P(y), P(w), P(h)); }

        public SetupForm()
        {
            AutoScaleMode = AutoScaleMode.None;
            using (Graphics g = CreateGraphics()) S = g.DpiX / 96f;
            Text = "Install " + Installer.AppName;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            ClientSize = new Size(P(480), P(300));

            string existing = Installer.InstalledFolder();

            Controls.Add(new Label { Text = Installer.AppName + " " + Installer.Version, Bounds = R(16, 14, 440, 28), Font = new Font("Segoe UI", 14f, FontStyle.Bold) });
            Controls.Add(new Label
            {
                Text = "An eyedropper like PowerPoint's. Point at anything on your screen, click to pick its colour, and copy the hex code.",
                Bounds = R(16, 46, 448, 36)
            });

            Controls.Add(new Label { Text = "Install to", Bounds = R(16, 92, 200, 18) });
            folderBox = new TextBox { Bounds = R(16, 112, 360, 24), Text = existing ?? Installer.DefaultFolder };
            Button browse = new Button { Text = "Browse…", Bounds = R(384, 110, 80, 28) };
            browse.Click += delegate
            {
                using (FolderBrowserDialog d = new FolderBrowserDialog { Description = "Choose where to install " + Installer.AppName, SelectedPath = folderBox.Text })
                    if (d.ShowDialog(this) == DialogResult.OK) folderBox.Text = Path.Combine(d.SelectedPath, "ColourPicker");
            };

            desktopBox = new CheckBox { Text = "Add a Desktop shortcut", Bounds = R(16, 150, 300, 22), Checked = true };
            startBox = new CheckBox { Text = "Add to the Start menu", Bounds = R(16, 174, 300, 22), Checked = true };
            launchBox = new CheckBox { Text = "Open " + Installer.AppName + " when done", Bounds = R(16, 198, 300, 22), Checked = true };

            status = new Label { Bounds = R(16, 228, 448, 20), ForeColor = SystemColors.GrayText,
                Text = existing != null ? "Already installed. Installing again updates it and keeps your colours." : "No admin rights needed. You can uninstall it from Settings > Apps." };

            installBtn = new Button { Text = existing != null ? "Update" : "Install", Bounds = R(288, 258, 84, 30) };
            cancelBtn = new Button { Text = "Cancel", Bounds = R(380, 258, 84, 30), DialogResult = DialogResult.Cancel };
            installBtn.Click += delegate { DoInstall(); };
            AcceptButton = installBtn;
            CancelButton = cancelBtn;

            Controls.AddRange(new Control[] { folderBox, browse, desktopBox, startBox, launchBox, status, installBtn, cancelBtn });
        }

        void DoInstall()
        {
            string folder = folderBox.Text.Trim();
            if (folder.Length == 0 || !Path.IsPathRooted(folder))
            {
                MessageBox.Show(this, "Choose a full folder path, for example " + Installer.DefaultFolder, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            installBtn.Enabled = false;
            status.ForeColor = SystemColors.ControlText;
            status.Text = "Installing…";
            status.Refresh();
            try
            {
                while (!Installer.CloseRunningApp())
                {
                    if (MessageBox.Show(this, Installer.AppName + " is still open. Close it, then click Retry.", Text,
                        MessageBoxButtons.RetryCancel, MessageBoxIcon.Information) == DialogResult.Cancel)
                    {
                        status.Text = "Cancelled. Nothing was changed.";
                        installBtn.Enabled = true;
                        return;
                    }
                }
                string exe;
                using (Stream payload = Installer.Payload())
                {
                    if (payload == null) throw new InvalidOperationException("This installer is incomplete: the app isn't inside it.");
                    exe = Installer.Install(folder, payload, desktopBox.Checked, startBox.Checked, true);
                }
                if (launchBox.Checked) Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = folder, UseShellExecute = true });
                else MessageBox.Show(this, Installer.AppName + " is installed. Open it from the Start menu or the Desktop.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                status.ForeColor = Color.FromArgb(170, 40, 30);
                status.Text = "Couldn't install: " + ex.Message;
                installBtn.Enabled = true;
            }
        }
    }

    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        [STAThread]
        static int Main(string[] args)
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool uninstall = false, quiet = false;
            foreach (string a in args)
            {
                if (a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase)) uninstall = true;
                else if (a.Equals("/quiet", StringComparison.OrdinalIgnoreCase)) quiet = true;
            }

            if (!uninstall)
            {
                Application.Run(new SetupForm());
                return 0;
            }

            string title = "Uninstall " + Installer.AppName;
            if (!quiet && MessageBox.Show("Remove " + Installer.AppName + ", its shortcuts and its saved colours?", title,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return 1;
            try
            {
                while (!Installer.CloseRunningApp())
                {
                    if (quiet) return 2;
                    if (MessageBox.Show(Installer.AppName + " is still open. Close it, then click Retry.", title,
                        MessageBoxButtons.RetryCancel, MessageBoxIcon.Information) == DialogResult.Cancel)
                        return 1;
                }
                string folder = Installer.InstalledFolder() ?? Path.GetDirectoryName(Application.ExecutablePath);
                Installer.Uninstall(folder);
                if (!quiet) MessageBox.Show(Installer.AppName + " has been removed.", title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }
            catch (Exception ex)
            {
                if (!quiet) MessageBox.Show("Couldn't finish removing " + Installer.AppName + ": " + ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 3;
            }
        }
    }
}
