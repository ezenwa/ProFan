using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ProFan
{
    internal static class L
    {
        internal static bool Spanish;
        internal static void Init()
        {
            string value = null;
            try
            {
                string ini = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ProFan.ini");
                if (File.Exists(ini))
                    foreach (string line in File.ReadAllLines(ini))
                        if (line.TrimStart().StartsWith("Language=", StringComparison.OrdinalIgnoreCase))
                            value = line.Substring(line.IndexOf('=') + 1).Trim().ToLowerInvariant();
            }
            catch { }
            Spanish = value == "es" || (value == null && CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es");
        }
        internal static string T(string es, string en) { return Spanish ? es : en; }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            L.Init();
            bool exitCommand = args.Length > 0 && string.Equals(args[0], "--exit", StringComparison.OrdinalIgnoreCase);
            bool exitEventCreated;
            using (var exitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "ProFan-ExitSignal", out exitEventCreated))
            {
            bool showEventCreated;
            using (var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "ProFan-ShowSignal", out showEventCreated))
            {
            bool created;
            using (var mutex = new Mutex(true, "ProFan-ASUS-HN7306", out created))
            {
                if (!created)
                {
                    if (exitCommand)
                    {
                        exitSignal.Set();
                        return;
                    }
                    showSignal.Set();
                    return;
                }
                if (exitCommand) return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm(exitSignal, showSignal, UserSettings.StartMinimized));
            }
            }
            }
        }
    }

    internal static class UserSettings
    {
        private const string RegistryPath = @"Software\ProFan";

        internal static bool StartMinimized
        {
            get
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                        return key != null && Convert.ToInt32(key.GetValue("StartMinimized", 0), CultureInfo.InvariantCulture) != 0;
                }
                catch { return false; }
            }
            set
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    if (key == null) throw new InvalidOperationException(L.T("No se pudo guardar la preferencia.", "Could not save the preference."));
                    key.SetValue("StartMinimized", value ? 1 : 0, RegistryValueKind.DWord);
                }
            }
        }
    }

    internal sealed class AsusAcpi : IDisposable
    {
        private const string DevicePath = @"\\.\ATKACPI";
        private const uint ControlCode = 0x0022240C;
        private const uint Dsts = 0x53545344;
        private const uint Devs = 0x53564544;
        internal const uint PerformanceMode = 0x00120075;
        internal const uint CpuFan = 0x00110013;
        internal const uint GpuFan = 0x00110014;
        internal const int FullSpeed = 3;
        private const uint CpuFanCurve = 0x00110024;
        private const uint GpuFanCurve = 0x00110025;
        private static readonly IntPtr InvalidHandle = new IntPtr(-1);
        private IntPtr handle;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(IntPtr device, uint code, byte[] input, uint inputSize, byte[] output, uint outputSize, ref uint returned, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr value);

        internal AsusAcpi()
        {
            handle = CreateFile(DevicePath, 0xC0000000, 3, IntPtr.Zero, 3, 0x80, IntPtr.Zero);
            if (handle == InvalidHandle)
                throw new InvalidOperationException(L.T("No se pudo abrir el controlador ASUS ATKACPI. Código: ", "Could not open the ASUS ATKACPI driver. Code: ") + Marshal.GetLastWin32Error());
        }

        private byte[] Call(uint method, byte[] arguments)
        {
            var input = new byte[8 + arguments.Length];
            Buffer.BlockCopy(BitConverter.GetBytes(method), 0, input, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes((uint)arguments.Length), 0, input, 4, 4);
            Buffer.BlockCopy(arguments, 0, input, 8, arguments.Length);
            var output = new byte[16];
            uint returned = 0;
            if (!DeviceIoControl(handle, ControlCode, input, (uint)input.Length, output, (uint)output.Length, ref returned, IntPtr.Zero))
                throw new InvalidOperationException(L.T("El controlador ASUS rechazó la solicitud. Código: ", "The ASUS driver rejected the request. Code: ") + Marshal.GetLastWin32Error());
            return output;
        }

        internal int Get(uint deviceId)
        {
            var arguments = new byte[8];
            Buffer.BlockCopy(BitConverter.GetBytes(deviceId), 0, arguments, 0, 4);
            return BitConverter.ToInt32(Call(Dsts, arguments), 0) - 65536;
        }

        internal int Set(uint deviceId, int value)
        {
            var arguments = new byte[8];
            Buffer.BlockCopy(BitConverter.GetBytes(deviceId), 0, arguments, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(value), 0, arguments, 4, 4);
            return BitConverter.ToInt32(Call(Devs, arguments), 0);
        }

        internal int Set(uint deviceId, byte[] values)
        {
            var arguments = new byte[4 + values.Length];
            Buffer.BlockCopy(BitConverter.GetBytes(deviceId), 0, arguments, 0, 4);
            Buffer.BlockCopy(values, 0, arguments, 4, values.Length);
            return BitConverter.ToInt32(Call(Devs, arguments), 0);
        }

        internal bool StartManualControl(int percent)
        {
            if (Set(PerformanceMode, FullSpeed) != 1) return false;
            Thread.Sleep(100);
            return RefreshManualCurve(percent);
        }

        internal bool RefreshManualCurve(int percent)
        {
            byte speed = (byte)Math.Max(20, Math.Min(100, percent));
            byte[] curve = { 20, 30, 40, 50, 60, 70, 80, 90, speed, speed, speed, speed, speed, speed, speed, speed };
            int cpu = Set(CpuFanCurve, (byte[])curve.Clone());
            int gpu = Set(GpuFanCurve, (byte[])curve.Clone());
            return cpu == 1 && gpu == 1;
        }

        internal int FanRpm(uint deviceId)
        {
            int units = Get(deviceId) & 0xFFFF;
            return units >= 0 && units <= 120 ? units * 100 : -1;
        }

        public void Dispose()
        {
            if (handle != IntPtr.Zero && handle != InvalidHandle)
            {
                CloseHandle(handle);
                handle = IntPtr.Zero;
            }
        }
    }

    internal sealed class FluentButton : Button
    {
        internal Color AccentColor = Color.FromArgb(0, 120, 212);
        internal Color SelectedTextColor = Color.White;
        internal bool Selected;
        internal bool DrawFocusRing = true;
        private bool hovered;
        private bool pressed;
        private float hoverProgress;
        private readonly System.Windows.Forms.Timer transition = new System.Windows.Forms.Timer();

        internal FluentButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            transition.Interval = 15;
            transition.Tick += delegate
            {
                float target = hovered ? 1F : 0F;
                hoverProgress += (target - hoverProgress) * 0.28F;
                if (Math.Abs(target - hoverProgress) < 0.02F) { hoverProgress = target; transition.Stop(); }
                Invalidate();
            };
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; transition.Start(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; transition.Start(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        private static Color Blend(Color from, Color to, float amount)
        {
            amount = Math.Max(0F, Math.Min(1F, amount));
            return Color.FromArgb(
                (int)(from.A + ((to.A - from.A) * amount)),
                (int)(from.R + ((to.R - from.R) * amount)),
                (int)(from.G + ((to.G - from.G) * amount)),
                (int)(from.B + ((to.B - from.B) * amount)));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent != null ? Parent.BackColor : Color.FromArgb(24, 24, 28));
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color normal = Selected ? AccentColor : Color.FromArgb(43, 45, 49);
            Color hover = Selected ? Color.FromArgb(18, 138, 224) : Color.FromArgb(52, 59, 68);
            Color fill = Blend(normal, hover, hoverProgress);
            if (pressed) fill = Selected ? Color.FromArgb(0, 90, 158) : Color.FromArgb(31, 72, 104);
            if (!Enabled) fill = Color.FromArgb(38, 38, 43);
            Rectangle face = ClientRectangle;
            face.Inflate(-2, -2);
            if (pressed) face.Offset(0, 1);
            Color border = Selected ? Color.FromArgb(96, 205, 255) : Blend(Color.FromArgb(64, 66, 72), Color.FromArgb(82, 142, 184), hoverProgress);
            using (var path = RoundedRect(face, 10))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border, Selected ? 1.5F : 1F))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }
            if (DrawFocusRing && Focused && Enabled && ShowFocusCues)
            {
                Rectangle focus = face; focus.Inflate(-3, -3);
                using (var focusPath = RoundedRect(focus, 7))
                using (var focusPen = new Pen(Color.FromArgb(190, 96, 205, 255), 1F))
                    e.Graphics.DrawPath(focusPen, focusPath);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, face,
                Selected ? SelectedTextColor : (Enabled ? Color.FromArgb(245, 245, 248) : Color.Gray),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) transition.Dispose();
            base.Dispose(disposing);
        }

        internal static GraphicsPath RoundedRect(Rectangle rectangle, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            var arc = new Rectangle(rectangle.X, rectangle.Y, diameter, diameter);
            path.AddArc(arc, 180, 90);
            arc.X = rectangle.Right - diameter - 1; path.AddArc(arc, 270, 90);
            arc.Y = rectangle.Bottom - diameter - 1; path.AddArc(arc, 0, 90);
            arc.X = rectangle.X; path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(32, 32, 36); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(32, 32, 36); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(32, 32, 36); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(32, 32, 36); } }
        public override Color MenuItemSelected { get { return Color.FromArgb(0, 93, 172); } }
        public override Color MenuItemBorder { get { return Color.FromArgb(0, 174, 239); } }
        public override Color MenuBorder { get { return Color.FromArgb(72, 72, 80); } }
        public override Color SeparatorDark { get { return Color.FromArgb(72, 72, 80); } }
        public override Color SeparatorLight { get { return Color.FromArgb(72, 72, 80); } }
    }

    internal sealed class MainForm : Form
    {
        private readonly FluentButton[] percentageButtons = new FluentButton[5];
        private readonly FluentButton automatic = new FluentButton();
        private readonly Label titleLabel = new Label();
        private readonly Label state = new Label();
        private readonly Label rpm = new Label();
        private readonly Label subtitle = new Label();
        private readonly Label speedTitle = new Label();
        private readonly Label footnote = new Label();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer animationTimer = new System.Windows.Forms.Timer();
        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly ContextMenuStrip trayMenu = new ContextMenuStrip();
        private readonly ToolStripLabel trayStatus = new ToolStripLabel();
        private readonly ToolStripMenuItem trayAuto = new ToolStripMenuItem("Automático");
        private readonly ToolStripMenuItem trayStartMinimized = new ToolStripMenuItem();
        private readonly ToolStripMenuItem trayUpdates = new ToolStripMenuItem();
        private readonly ToolStripMenuItem[] traySpeeds = new ToolStripMenuItem[5];
        private AsusAcpi acpi;
        private bool manualActive;
        private bool exitRequested;
        private bool restoring;
        private int previousMode;
        private int manualSpeed = 100;
        private int refreshCounter;
        private const int ManualRefreshSeconds = 2;
        private int lastCpuRpm;
        private int lastGpuRpm;
        private int frameIndex;
        private Icon[] trayFrames;
        private RegisteredWaitHandle exitWait;
        private RegisteredWaitHandle showWait;
        private bool applyingLayout;
        private bool syncingStartMinimized;
        private bool handlingInitialMinimize;
        private readonly Size normalWindowSize;
        private readonly bool startMinimized;
        private bool checkingForUpdates;
        private string updateUrl;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hwnd, int command);
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hwnd);

        internal MainForm(EventWaitHandle exitSignal, EventWaitHandle showSignal, bool startMinimized)
        {
            this.startMinimized = startMinimized;
            handlingInitialMinimize = startMinimized;
            Text = "ProFan";
            ClientSize = new Size(520, 410);
            normalWindowSize = Size;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            SizeGripStyle = SizeGripStyle.Hide;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(24, 24, 28);
            ForeColor = Color.White;
            Font = new Font("Segoe UI Variable Text", 10F, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            titleLabel.Text = "ProFan";
            titleLabel.Font = new Font("Segoe UI Variable Display", 20F, FontStyle.Bold);
            titleLabel.AutoSize = true;
            titleLabel.ForeColor = Color.White;
            subtitle.Text = L.T("Control rápido de refrigeración · ASUS ProArt PX13", "Quick cooling control · ASUS ProArt PX13");
            subtitle.Font = new Font("Segoe UI Variable Text", 9.5F);
            subtitle.AutoSize = false;
            subtitle.TextAlign = ContentAlignment.MiddleLeft;
            subtitle.ForeColor = Color.FromArgb(108, 206, 245);

            state.Text = L.T("●  AUTOMÁTICO", "●  AUTOMATIC");
            state.Font = new Font("Segoe UI Variable Text", 12F, FontStyle.Bold);
            state.AutoSize = true;
            state.ForeColor = Color.FromArgb(0, 174, 239);

            speedTitle.Text = L.T("Velocidad manual", "Manual speed");
            speedTitle.AutoSize = true;
            speedTitle.ForeColor = Color.FromArgb(220, 220, 225);
            speedTitle.Font = new Font("Segoe UI Variable Text", 10F, FontStyle.Bold);
            int[] values = { 20, 40, 60, 80, 100 };
            for (int i = 0; i < values.Length; i++)
            {
                var button = new FluentButton();
                button.Text = values[i] + "%";
                button.Tag = values[i];
                button.Size = new Size(82, 52);
                button.Font = new Font("Segoe UI Variable Text", 10F, FontStyle.Bold);
                button.Click += PercentageClick;
                percentageButtons[i] = button;
            }

            automatic.Text = L.T("Volver a Automático", "Return to Automatic");
            automatic.Size = new Size(446, 48);
            automatic.Font = new Font("Segoe UI Variable Text", 10F, FontStyle.Bold);
            automatic.AccentColor = Color.FromArgb(0, 93, 172);
            automatic.SelectedTextColor = Color.White;
            automatic.Click += delegate { SetAutomatic(true); };

            rpm.Text = "CPU  — RPM     ·     GPU  — RPM";
            rpm.AutoSize = true;
            rpm.ForeColor = Color.FromArgb(205, 205, 212);
            rpm.Font = new Font("Segoe UI Variable Text", 10F, FontStyle.Bold);

            footnote.Text = L.T(
                "Cerrar conserva el modo y la velocidad actuales.\r\nProFan permanece disponible en la bandeja.",
                "Closing keeps the current mode and speed.\r\nProFan remains available in the system tray.");
            footnote.AutoSize = false;
            footnote.ForeColor = Color.FromArgb(145, 145, 155);
            footnote.Font = new Font("Segoe UI Variable Text", 9F);
            footnote.TextAlign = ContentAlignment.TopLeft;

            Controls.Add(titleLabel); Controls.Add(subtitle); Controls.Add(state); Controls.Add(speedTitle);
            foreach (var button in percentageButtons) Controls.Add(button);
            Controls.Add(automatic); Controls.Add(rpm); Controls.Add(footnote);

            BuildTrayMenu(values);
            Load += OnLoad;
            Shown += delegate
            {
                BeginInvoke(new Action(delegate
                {
                    if (handlingInitialMinimize)
                    {
                        handlingInitialMinimize = false;
                        Hide();
                        WindowState = FormWindowState.Normal;
                        CenterMainWindow();
                    }
                    ApplyContentLayout();
                }));
            };
            FormClosing += OnClosing;
            Resize += delegate
            {
                if (WindowState == FormWindowState.Minimized)
                {
                    if (handlingInitialMinimize) return;
                    Hide();
                    ShowTrayTip(L.T("ProFan continúa disponible aquí.", "ProFan is still available here."));
                }
                else ApplyContentLayout();
            };
            SystemEvents.SessionEnding += SessionEnding;
            SystemEvents.PowerModeChanged += PowerModeChanged;
            AppDomain.CurrentDomain.ProcessExit += ProcessExit;
            timer.Interval = 1000;
            timer.Tick += UpdateStatus;
            animationTimer.Interval = 120;
            animationTimer.Tick += AnimateTray;
            animationTimer.Start();
            if (startMinimized)
                WindowState = FormWindowState.Minimized;
            exitWait = ThreadPool.RegisterWaitForSingleObject(exitSignal, delegate
            {
                if (!IsDisposed && IsHandleCreated)
                    BeginInvoke(new Action(delegate { exitRequested = true; SafeRestore(); Close(); }));
            }, null, Timeout.Infinite, false);
            showWait = ThreadPool.RegisterWaitForSingleObject(showSignal, delegate
            {
                if (!IsDisposed && IsHandleCreated)
                    BeginInvoke(new Action(ShowFromTray));
            }, null, Timeout.Infinite, false);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int enabled = 1;
            int mica = 2;
            int rounded = 2;
            try
            {
                DwmSetWindowAttribute(Handle, 20, ref enabled, 4);
                DwmSetWindowAttribute(Handle, 38, ref mica, 4);
                DwmSetWindowAttribute(Handle, 33, ref rounded, 4);
            }
            catch { }
        }

        protected override void WndProc(ref Message message)
        {
            const int WmSysCommand = 0x0112;
            const int ScSize = 0xF000;
            const int ScMaximize = 0xF030;
            if (message.Msg == WmSysCommand)
            {
                int command = message.WParam.ToInt32() & 0xFFF0;
                if (command == ScSize || command == ScMaximize) return;
            }
            base.WndProc(ref message);
        }

        private void BuildTrayMenu(int[] values)
        {
            trayFrames = CreateTrayFrames();
            trayMenu.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());
            trayMenu.BackColor = Color.FromArgb(32, 32, 36);
            trayMenu.ForeColor = Color.White;
            trayStatus.Text = "ProFan";
            trayStatus.Font = new Font("Segoe UI Variable Text", 9F, FontStyle.Bold);
            trayStatus.ForeColor = Color.FromArgb(0, 174, 239);
            trayStatus.Padding = new Padding(8, 5, 8, 5);
            trayMenu.Items.Add(trayStatus);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayAuto.Text = L.T("Automático", "Automatic");
            trayAuto.Click += delegate { SetAutomatic(true); };
            trayMenu.Items.Add(trayAuto);
            trayMenu.Items.Add(new ToolStripSeparator());
            for (int i = 0; i < values.Length; i++)
            {
                int captured = values[i];
                var item = new ToolStripMenuItem(captured + "%");
                item.Click += delegate { ApplyManual(captured, true); };
                traySpeeds[i] = item;
                trayMenu.Items.Add(item);
            }
            trayMenu.Items.Add(new ToolStripSeparator());
            var open = new ToolStripMenuItem(L.T("Abrir ProFan", "Open ProFan"));
            open.Click += delegate { ShowFromTray(); };
            var about = new ToolStripMenuItem(L.T("Acerca de ProFan", "About ProFan"));
            about.Click += delegate { ShowAbout(); };
            trayUpdates.Text = L.T("Buscar actualizaciones", "Check for updates");
            trayUpdates.Click += delegate
            {
                if (string.IsNullOrEmpty(updateUrl)) CheckForUpdates(true);
                else OpenUpdatePage();
            };
            trayStartMinimized.Text = L.T("Iniciar minimizado", "Start minimized");
            trayStartMinimized.CheckOnClick = true;
            trayStartMinimized.Checked = startMinimized;
            trayStartMinimized.CheckedChanged += TrayStartMinimizedChanged;
            var exit = new ToolStripMenuItem(L.T("Salir", "Exit"));
            exit.Click += delegate { exitRequested = true; SafeRestore(); Close(); };
            trayMenu.Items.Add(open);
            trayMenu.Items.Add(about);
            trayMenu.Items.Add(trayUpdates);
            trayMenu.Items.Add(trayStartMinimized);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(exit);

            tray.Icon = trayFrames[0];
            tray.Text = L.T("ProFan · Automático", "ProFan · Automatic");
            tray.ContextMenuStrip = trayMenu;
            tray.Visible = true;
            tray.DoubleClick += delegate { ShowFromTray(); };
            tray.BalloonTipClicked += delegate { if (!string.IsNullOrEmpty(updateUrl)) OpenUpdatePage(); };
        }

        private void ShowAbout()
        {
            using (var about = new Form())
            using (var content = new FlowLayoutPanel())
            {
                about.Text = L.T("Acerca de ProFan", "About ProFan");
                about.ClientSize = new Size(420, 330);
                about.FormBorderStyle = FormBorderStyle.FixedDialog;
                about.MaximizeBox = false;
                about.MinimizeBox = false;
                about.ShowInTaskbar = false;
                about.StartPosition = FormStartPosition.CenterScreen;
                about.KeyPreview = true;
                about.BackColor = Color.FromArgb(24, 24, 28);
                about.ForeColor = Color.White;
                about.Font = new Font("Segoe UI Variable Text", 10F);
                about.AutoScaleMode = AutoScaleMode.Dpi;
                about.AutoScaleDimensions = new SizeF(96F, 96F);
                about.Icon = Icon;

                content.Dock = DockStyle.Fill;
                content.Padding = new Padding(30, 24, 30, 24);
                content.FlowDirection = FlowDirection.TopDown;
                content.WrapContents = false;
                content.BackColor = about.BackColor;

                var heading = AboutLabel("ProFan", 20F, FontStyle.Bold, Color.White, 352, 44);
                var version = AboutLabel(L.T("Versión ", "Version ") + Application.ProductVersion, 9F, FontStyle.Regular, Color.FromArgb(165, 165, 174), 352, 26);
                var authorCaption = AboutLabel(L.T("AUTOR", "AUTHOR"), 8.5F, FontStyle.Bold, Color.FromArgb(0, 174, 239), 352, 24);
                var author = AboutLabel("Joshua Ezenwa", 11F, FontStyle.Bold, Color.White, 352, 29);
                var legal = AboutLabel("Copyright © 2026 Joshua Ezenwa\r\nGNU General Public License v3.0", 9F, FontStyle.Regular, Color.FromArgb(165, 165, 174), 352, 48);
                heading.Margin = new Padding(0, 0, 0, 6);
                version.Margin = new Padding(0, 0, 0, 10);
                authorCaption.Margin = new Padding(0, 0, 0, 2);
                author.Margin = new Padding(0, 0, 0, 10);
                legal.Margin = new Padding(0, 0, 0, 6);
                var close = new FluentButton();
                close.Text = L.T("Cerrar", "Close");
                close.Size = new Size(352, 46);
                close.Margin = new Padding(0, 6, 0, 0);
                close.Font = new Font("Segoe UI Variable Text", 10F, FontStyle.Bold);
                close.Selected = true;
                close.AccentColor = Color.FromArgb(0, 93, 172);
                close.DrawFocusRing = false;
                close.Click += delegate { about.Close(); };
                about.KeyDown += delegate(object sender, KeyEventArgs e)
                {
                    if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Escape)
                    {
                        e.SuppressKeyPress = true;
                        about.Close();
                    }
                };

                content.Controls.Add(heading);
                content.Controls.Add(version);
                content.Controls.Add(authorCaption);
                content.Controls.Add(author);
                content.Controls.Add(legal);
                content.Controls.Add(close);
                about.Controls.Add(content);
                about.ShowDialog();
            }
        }

        private static Label AboutLabel(string text, float size, FontStyle style, Color color, int width, int height)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI Variable Text", size, style),
                ForeColor = color,
                AutoSize = false,
                Size = new Size(width, height),
                Margin = new Padding(0),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private Icon[] CreateTrayFrames()
        {
            var frames = new Icon[12];
            for (int frame = 0; frame < frames.Length; frame++)
            {
                using (var bitmap = new Bitmap(32, 32))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.Clear(Color.Transparent);
                    using (var background = new SolidBrush(Color.FromArgb(25, 27, 31))) graphics.FillEllipse(background, 1, 1, 30, 30);
                    using (var ring = new Pen(Color.FromArgb(0, 174, 239), 2)) graphics.DrawEllipse(ring, 2, 2, 28, 28);
                    graphics.TranslateTransform(16, 16);
                    graphics.RotateTransform(frame * (360F / frames.Length));
                    using (var blade = new SolidBrush(Color.FromArgb(0, 174, 239)))
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            graphics.FillEllipse(blade, -1.5F, -12F, 7F, 12F);
                            graphics.RotateTransform(72F);
                        }
                    }
                    using (var hub = new SolidBrush(Color.White)) graphics.FillEllipse(hub, -3F, -3F, 6F, 6F);
                    graphics.ResetTransform();
                    IntPtr handle = bitmap.GetHicon();
                    try { frames[frame] = (Icon)Icon.FromHandle(handle).Clone(); }
                    finally { DestroyIcon(handle); }
                }
            }
            return frames;
        }

        private void AnimateTray(object sender, EventArgs e)
        {
            if (trayFrames == null || trayFrames.Length == 0) return;
            bool spinning = manualActive || lastCpuRpm > 0 || lastGpuRpm > 0;
            if (!spinning) { frameIndex = 0; tray.Icon = trayFrames[0]; return; }
            frameIndex = (frameIndex + 1) % trayFrames.Length;
            tray.Icon = trayFrames[frameIndex];
            animationTimer.Interval = manualActive
                ? ManualAnimationInterval(manualSpeed)
                : AutomaticAnimationInterval(Math.Max(lastCpuRpm, lastGpuRpm));
        }

        private static int ManualAnimationInterval(int percent)
        {
            switch (percent)
            {
                case 20: return 170;
                case 40: return 140;
                case 60: return 110;
                case 80: return 80;
                default: return 55;
            }
        }

        private static int AutomaticAnimationInterval(int rpm)
        {
            if (rpm <= 0) return 180;
            int interval = 200 - (rpm / 35);
            return Math.Max(55, Math.Min(180, interval));
        }

        private void UpdateTrayStatus()
        {
            string mode = manualActive ? manualSpeed + "%" : L.T("Auto", "Auto");
            string cpu = lastCpuRpm > 0 ? lastCpuRpm.ToString("N0") : "—";
            string gpu = lastGpuRpm > 0 ? lastGpuRpm.ToString("N0") : "—";
            string line1 = "ProFan · " + mode;
            string line2 = "CPU " + cpu + " · GPU " + gpu + " RPM";
            trayStatus.Text = line1 + Environment.NewLine + line2;
            string tip = line1 + Environment.NewLine + line2;
            tray.Text = tip.Length <= 63 ? tip : line1;
        }

        private void OnLoad(object sender, EventArgs e)
        {
            try
            {
                acpi = new AsusAcpi();
                int mode = acpi.Get(AsusAcpi.PerformanceMode);
                previousMode = mode >= 0 && mode <= 4 && mode != AsusAcpi.FullSpeed ? mode : 0;
                if (acpi.Set(AsusAcpi.PerformanceMode, previousMode) != 1)
                    throw new InvalidOperationException(L.T(
                        "No se pudo iniciar el control automático del firmware ASUS.",
                        "Could not initialize ASUS firmware automatic control."));
                manualActive = false;
                timer.Start();
                UpdateStatus(null, EventArgs.Empty);
                PaintState();
                ApplyContentLayout();
            }
            catch (Exception ex)
            {
                foreach (var button in percentageButtons) button.Enabled = false;
                state.Text = L.T("●  NO DISPONIBLE", "●  UNAVAILABLE");
                state.ForeColor = Color.FromArgb(255, 99, 106);
                MessageBox.Show(ex.Message, "ProFan", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            CheckForUpdates(false);
        }

        private void CheckForUpdates(bool notifyWhenCurrent)
        {
            if (checkingForUpdates) return;
            checkingForUpdates = true;
            trayUpdates.Enabled = false;
            trayUpdates.Text = L.T("Buscando actualizaciones…", "Checking for updates…");

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                    var request = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/ezenwa/ProFan/releases/latest");
                    request.UserAgent = "ProFan/" + Application.ProductVersion;
                    request.Accept = "application/vnd.github+json";
                    request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                    request.Timeout = 7000;
                    request.ReadWriteTimeout = 7000;
                    string json;
                    using (var response = (HttpWebResponse)request.GetResponse())
                    using (var reader = new StreamReader(response.GetResponseStream()))
                        json = reader.ReadToEnd();

                    Match match = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"v?([0-9]+(?:\\.[0-9]+){1,3})\"", RegexOptions.IgnoreCase);
                    Version latest;
                    Version current;
                    if (!match.Success || !Version.TryParse(match.Groups[1].Value, out latest) || !Version.TryParse(Application.ProductVersion, out current))
                        throw new InvalidOperationException("Invalid release version.");

                    string versionText = match.Groups[1].Value;
                    string releaseUrl = "https://github.com/ezenwa/ProFan/releases/tag/v" + versionText;
                    if (!IsDisposed && IsHandleCreated)
                        BeginInvoke(new Action(delegate
                        {
                            checkingForUpdates = false;
                            trayUpdates.Enabled = true;
                            if (latest > current)
                            {
                                updateUrl = releaseUrl;
                                trayUpdates.Text = L.T("Descargar ProFan v", "Download ProFan v") + versionText;
                                ShowTrayTip(L.T("Actualización disponible: ProFan v", "Update available: ProFan v") + versionText + L.T(". Haz clic aquí para descargar.", ". Click here to download."));
                            }
                            else
                            {
                                updateUrl = null;
                                trayUpdates.Text = L.T("Buscar actualizaciones", "Check for updates");
                                if (notifyWhenCurrent) ShowTrayTip(L.T("ProFan está actualizado.", "ProFan is up to date."));
                            }
                        }));
                }
                catch
                {
                    if (!IsDisposed && IsHandleCreated)
                        BeginInvoke(new Action(delegate
                        {
                            checkingForUpdates = false;
                            trayUpdates.Enabled = true;
                            trayUpdates.Text = L.T("Buscar actualizaciones", "Check for updates");
                            if (notifyWhenCurrent) ShowTrayTip(L.T("No se pudo comprobar si hay actualizaciones.", "Could not check for updates."));
                        }));
                }
            });
        }

        private void OpenUpdatePage()
        {
            if (string.IsNullOrEmpty(updateUrl)) return;
            try { Process.Start(new ProcessStartInfo(updateUrl) { UseShellExecute = true }); }
            catch { MessageBox.Show(L.T("No se pudo abrir la página de descarga.", "Could not open the download page."), "ProFan", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void ApplyContentLayout()
        {
            if (applyingLayout || ClientSize.Width < 300 || ClientSize.Height < 300) return;
            applyingLayout = true;
            try
            {
                int left = Math.Max(28, (int)Math.Round(36F * DeviceDpi / 96F));
                int right = left;
                int contentWidth = ClientSize.Width - left - right;
                int gapSmall = Math.Max(4, (int)Math.Round(6F * DeviceDpi / 96F));
                int gapMedium = Math.Max(8, (int)Math.Round(9F * DeviceDpi / 96F));
                int outerMargin = Math.Max(16, (int)Math.Round(20F * DeviceDpi / 96F));
                int y = outerMargin;

                titleLabel.Location = new Point(left, y);
                y = titleLabel.Bottom + gapSmall;

                subtitle.Location = new Point(left, y);
                subtitle.Size = new Size(contentWidth, Math.Max(subtitle.PreferredHeight, (int)Math.Round(22F * DeviceDpi / 96F)));
                y = subtitle.Bottom + gapMedium;

                state.Location = new Point(left, y);
                y = state.Bottom + gapMedium;

                speedTitle.Location = new Point(left, y);
                y = speedTitle.Bottom + gapSmall;

                int buttonGap = Math.Max(6, (int)Math.Round(8F * DeviceDpi / 96F));
                int buttonWidth = (contentWidth - (buttonGap * 4)) / 5;
                int buttonHeight = Math.Max(42, (int)Math.Round(48F * DeviceDpi / 96F));
                for (int i = 0; i < percentageButtons.Length; i++)
                {
                    percentageButtons[i].Location = new Point(left + (i * (buttonWidth + buttonGap)), y);
                    percentageButtons[i].Size = new Size(buttonWidth, buttonHeight);
                }
                y += buttonHeight + gapMedium;

                automatic.Location = new Point(left, y);
                automatic.Size = new Size(contentWidth, Math.Max(42, (int)Math.Round(46F * DeviceDpi / 96F)));
                y = automatic.Bottom + gapMedium;

                rpm.Location = new Point(left, y);
                y = rpm.Bottom + gapMedium;

                int footerHeight = TextRenderer.MeasureText(
                    footnote.Text,
                    footnote.Font,
                    new Size(contentWidth, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height + 4;
                int balancedFooterY = ClientSize.Height - outerMargin - footerHeight;
                footnote.Location = new Point(left, Math.Max(y, balancedFooterY));
                footnote.Size = new Size(contentWidth, footerHeight);
            }
            finally { applyingLayout = false; }
        }

        private void PercentageClick(object sender, EventArgs e)
        {
            ApplyManual((int)((Button)sender).Tag, false);
        }

        private void TrayStartMinimizedChanged(object sender, EventArgs e)
        {
            if (syncingStartMinimized) return;
            SaveStartMinimized(trayStartMinimized.Checked);
        }

        private void SaveStartMinimized(bool value)
        {
            try
            {
                UserSettings.StartMinimized = value;
                syncingStartMinimized = true;
                trayStartMinimized.Checked = value;
            }
            catch (Exception ex)
            {
                syncingStartMinimized = true;
                bool saved = UserSettings.StartMinimized;
                trayStartMinimized.Checked = saved;
                MessageBox.Show(ex.Message, "ProFan", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { syncingStartMinimized = false; }
        }

        private void ApplyManual(int percent, bool fromTray)
        {
            if (acpi == null) return;
            try
            {
                bool wasManual = manualActive;
                bool startRequired = true;
                if (!manualActive)
                {
                    int current = acpi.Get(AsusAcpi.PerformanceMode);
                    if (current >= 0 && current <= 4 && current != AsusAcpi.FullSpeed) previousMode = current;
                }
                else
                {
                    startRequired = acpi.Get(AsusAcpi.PerformanceMode) != AsusAcpi.FullSpeed;
                }
                bool applied = startRequired
                    ? acpi.StartManualControl(percent)
                    : acpi.RefreshManualCurve(percent);
                if (!applied)
                {
                    if (!wasManual) acpi.Set(AsusAcpi.PerformanceMode, previousMode);
                    throw new InvalidOperationException(L.T("El firmware no aceptó la velocidad manual en ambos ventiladores.", "The firmware did not accept the manual speed for both fans."));
                }
                manualSpeed = percent;
                manualActive = true;
                refreshCounter = 0;
                PaintState();
                if (fromTray) ShowTrayTip(L.T("Velocidad manual: ", "Manual speed: ") + percent + "%");
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "ProFan", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void SetAutomatic(bool notify)
        {
            try
            {
                RestoreAutomatic();
                PaintState();
                if (notify) ShowTrayTip(L.T("Control devuelto al firmware ASUS.", "Control returned to ASUS firmware."));
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "ProFan", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void RestoreAutomatic()
        {
            if (!manualActive || restoring || acpi == null) return;
            restoring = true;
            try
            {
                int restore = previousMode == AsusAcpi.FullSpeed ? 0 : previousMode;
                if (acpi.Set(AsusAcpi.PerformanceMode, restore) != 1)
                    throw new InvalidOperationException(L.T("No se pudo devolver el control al firmware ASUS.", "Could not return control to ASUS firmware."));
                manualActive = false;
            }
            finally { restoring = false; }
        }

        private void PaintState()
        {
            if (manualActive)
            {
                state.Text = "●  MANUAL " + manualSpeed + "%";
                state.ForeColor = Color.FromArgb(0, 174, 239);
                automatic.Text = L.T("Volver a Automático", "Return to Automatic");
                automatic.Enabled = true;
            }
            else
            {
                state.Text = L.T("●  AUTOMÁTICO", "●  AUTOMATIC");
                state.ForeColor = Color.FromArgb(0, 174, 239);
                automatic.Text = L.T("Automático activo", "Automatic active");
                automatic.Enabled = true;
            }
            foreach (var button in percentageButtons)
            {
                button.Selected = manualActive && (int)button.Tag == manualSpeed;
                button.Invalidate();
            }
            automatic.Selected = !manualActive;
            automatic.Invalidate();
            trayAuto.Checked = !manualActive;
            for (int i = 0; i < traySpeeds.Length; i++) traySpeeds[i].Checked = manualActive && (int)percentageButtons[i].Tag == manualSpeed;
            tray.Text = manualActive ? "ProFan · Manual " + manualSpeed + "%" : L.T("ProFan · Automático", "ProFan · Automatic");
            UpdateTrayStatus();
        }

        private void UpdateStatus(object sender, EventArgs e)
        {
            if (acpi == null) return;
            try
            {
                if (manualActive && ++refreshCounter >= ManualRefreshSeconds)
                {
                    int mode = acpi.Get(AsusAcpi.PerformanceMode);
                    bool applied = mode == AsusAcpi.FullSpeed
                        ? acpi.RefreshManualCurve(manualSpeed)
                        : acpi.StartManualControl(manualSpeed);
                    if (!applied)
                        throw new InvalidOperationException(L.T("No se pudo mantener la velocidad manual.", "Could not maintain the manual speed."));
                    refreshCounter = 0;
                }
                int cpu = acpi.FanRpm(AsusAcpi.CpuFan);
                int gpu = acpi.FanRpm(AsusAcpi.GpuFan);
                lastCpuRpm = cpu > 0 ? cpu : 0;
                lastGpuRpm = gpu > 0 ? gpu : 0;
                rpm.Text = "CPU  " + (cpu > 0 ? cpu.ToString("N0") : "—") + " RPM     ·     GPU  " + (gpu > 0 ? gpu.ToString("N0") : "—") + " RPM";
                UpdateTrayStatus();
            }
            catch { rpm.Text = "CPU  — RPM     ·     GPU  — RPM"; }
        }

        private void ShowFromTray()
        {
            handlingInitialMinimize = false;
            ShowInTaskbar = true;
            Show();
            WindowState = FormWindowState.Normal;
            ShowWindow(Handle, 9);
            CenterMainWindow();
            BringToFront();
            Activate();
            SetForegroundWindow(Handle);
            BeginInvoke(new Action(delegate
            {
                if (IsDisposed) return;
                WindowState = FormWindowState.Normal;
                ShowWindow(Handle, 9);
                CenterMainWindow();
                BringToFront();
                Activate();
                SetForegroundWindow(Handle);
            }));
        }

        private void CenterMainWindow()
        {
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            int x = area.Left + Math.Max(0, (area.Width - normalWindowSize.Width) / 2);
            int y = area.Top + Math.Max(0, (area.Height - normalWindowSize.Height) / 2);
            Bounds = new Rectangle(new Point(x, y), normalWindowSize);
        }

        private void ShowTrayTip(string message)
        {
            tray.BalloonTipTitle = "ProFan";
            tray.BalloonTipText = message;
            tray.ShowBalloonTip(1800);
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (!exitRequested && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                string keptMode = manualActive
                    ? L.T("Manual ", "Manual ") + manualSpeed + "%"
                    : L.T("Automático", "Automatic");
                ShowTrayTip(L.T("Se conserva el modo ", "Keeping ") + keptMode + L.T(". Usa clic derecho para control rápido.", ". Right-click for quick controls."));
                return;
            }
            timer.Stop();
            animationTimer.Stop();
            SafeRestore();
            tray.Visible = false;
            tray.Dispose();
            if (trayFrames != null) foreach (var frame in trayFrames) if (frame != null) frame.Dispose();
            if (exitWait != null) { exitWait.Unregister(null); exitWait = null; }
            if (showWait != null) { showWait.Unregister(null); showWait = null; }
            SystemEvents.SessionEnding -= SessionEnding;
            SystemEvents.PowerModeChanged -= PowerModeChanged;
            if (acpi != null) acpi.Dispose();
        }

        private void PowerModeChanged(object sender, PowerModeChangedEventArgs e) { if (e.Mode == PowerModes.Suspend) { SafeRestore(); PaintState(); } }
        private void SessionEnding(object sender, SessionEndingEventArgs e) { SafeRestore(); }
        private void ProcessExit(object sender, EventArgs e) { SafeRestore(); }
        private void SafeRestore() { try { RestoreAutomatic(); } catch { } }
    }
}
