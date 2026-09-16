using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SmartSwitcher.Core;
using SmartSwitcher.Core.Models;
using SmartSwitcher.Logging;
using SmartSwitcher.Windows;

namespace SmartSwitcher.UI;

/// <summary>
/// Full-bleed studio dashboard workspace built to Linear / Apple / Vercel standards.
/// Supports full-screen maximization, resizable layout, Inter typography, and subpixel ClearType antialiasing.
/// </summary>
public class StatusForm : Form
{
    // =========================================================================
    // BRAND COLOR PALETTE (Strictly extracted from Asset 2.svg)
    // =========================================================================
    private static readonly Color BrandCanvas = Color.FromArgb(9, 13, 19);         // #090d13 (cls-11)
    private static readonly Color BrandSurface = Color.FromArgb(17, 22, 31);       // #11161f (Studio card)
    private static readonly Color BrandBorder = Color.FromArgb(34, 44, 58);        // #222c3a (cls-10)
    private static readonly Color BrandBorderSubtle = Color.FromArgb(24, 32, 43);  // #18202b

    // Vibrant Electric Cyan & Blue Accents
    private static readonly Color AccentCyan = Color.FromArgb(69, 195, 252);       // #45c3fc (cls-9)
    private static readonly Color AccentBlue = Color.FromArgb(21, 140, 242);       // #158cf2 (cls-13)
    private static readonly Color AccentBadgeBg = Color.FromArgb(12, 35, 60);      // Dark navy badge

    // Typography & Silver Accents
    private static readonly Color TextWhite = Color.FromArgb(246, 252, 254);       // #f6fcfe (cls-22)
    private static readonly Color TextSilver = Color.FromArgb(216, 226, 236);      // #d8e2ec (cls-18)
    private static readonly Color TextMuted = Color.FromArgb(138, 151, 166);       // #8a97a6 (cls-2)
    private static readonly Color TextSubtle = Color.FromArgb(96, 108, 122);       // #606c7a (cls-7)

    // Header Controls
    private readonly Panel _pnlHeader;
    private readonly PictureBox _picLogo;
    private readonly SmoothLabel _lblTitle;
    private readonly SmoothLabel _lblSubTitle;
    private readonly SmoothLabel _lblActiveBanner;
    private readonly ModernButton _btnFullScreenToggle;
    private readonly ModernButton _btnUpdatePill;

    // Segmented Mode Buttons
    private readonly ModernButton _btnModeAuto;
    private readonly ModernButton _btnModeWifi;
    private readonly ModernButton _btnModeEth;

    // Ethernet Card Controls
    private readonly Panel _cardEth;
    private readonly SmoothLabel _lblEthBadge;
    private readonly SmoothLabel _lblEthStatus;
    private readonly SmoothLabel _lblEthScoreVal;
    private readonly SmoothProgressBar _pbEthScore;
    private readonly SmoothLabel _lblEthLatVal;
    private readonly SmoothLabel _lblEthGwVal;
    private readonly SmoothLabel _lblEthLossVal;
    private readonly SmoothLabel _lblEthSpeedVal;

    // Wi-Fi Card Controls
    private readonly Panel _cardWifi;
    private readonly SmoothLabel _lblWifiBadge;
    private readonly SmoothLabel _lblWifiStatus;
    private readonly SmoothLabel _lblWifiScoreVal;
    private readonly SmoothProgressBar _pbWifiScore;
    private readonly SmoothLabel _lblWifiLatVal;
    private readonly SmoothLabel _lblWifiGwVal;
    private readonly SmoothLabel _lblWifiLossVal;
    private readonly SmoothLabel _lblWifiSpeedVal;

    // Bottom Controls & Telemetry Feed
    private readonly RichTextBox _txtLogs;
    private readonly ModernButton _btnProbeNow;
    private readonly ModernButton _btnMinimizeTray;
    private readonly CheckBox _chkStartup;

    // Full Screen State
    private bool _isFullScreen;
    private FormWindowState _previousWindowState = FormWindowState.Normal;
    private FormBorderStyle _previousBorderStyle = FormBorderStyle.Sizable;

    public event Action? RequestCheckNow;
    public event Action<RoutingMode>? RequestModeChange;

    public StatusForm()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw, true);

        Text = "Smart Switcher - Connection Quality & Intelligent Routing";
        Size = new Size(880, 740);
        MinimumSize = new Size(720, 620);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;
        ShowInTaskbar = true;
        BackColor = BrandCanvas;
        ForeColor = TextWhite;
        Font = ThemeFonts.Body;

        // Try load official window icon with true alpha transparency
        try
        {
            string pngIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "logo_48.png");
            if (File.Exists(pngIconPath))
            {
                using var bmp = new Bitmap(pngIconPath);
                Icon = Icon.FromHandle(bmp.GetHicon());
            }
            else
            {
                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
                if (File.Exists(icoPath))
                {
                    Icon = new Icon(icoPath);
                }
            }
        }
        catch
        {
            // Non-critical
        }

        // =========================================================================
        // 1. BRAND HEADER (Pinned Top, Edge-to-Edge Studio Header)
        // =========================================================================
        _pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 90,
            BackColor = BrandCanvas
        };
        _pnlHeader.Paint += (s, e) =>
        {
            using var borderPen = new Pen(BrandBorderSubtle, 1.0f);
            e.Graphics.DrawLine(borderPen, 0, _pnlHeader.Height - 1, _pnlHeader.Width, _pnlHeader.Height - 1);
        };

        // Brand Logo Image
        _picLogo = new PictureBox
        {
            Location = new Point(24, 13),
            Size = new Size(64, 64),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };

        try
        {
            string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "logo_128.png");
            if (!File.Exists(logoPath))
            {
                logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "logo_64.png");
            }
            if (File.Exists(logoPath))
            {
                _picLogo.Image = Image.FromFile(logoPath);
            }
        }
        catch
        {
            // Fallback
        }

        string appVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        _lblTitle = new SmoothLabel
        {
            Text = $"SMART SWITCHER  v{appVersion}",
            Font = ThemeFonts.TitleLarge,
            ForeColor = TextWhite,
            Location = new Point(98, 15),
            AutoSize = true
        };

        _lblSubTitle = new SmoothLabel
        {
            Text = "INTELLIGENT DUAL-ROUTE ENGINE",
            Font = ThemeFonts.Subhead,
            ForeColor = AccentCyan,
            Location = new Point(100, 39),
            AutoSize = true
        };

        _lblActiveBanner = new SmoothLabel
        {
            Text = "● ACTIVE ROUTE: Evaluating connection telemetry...",
            Font = ThemeFonts.StatusBanner,
            ForeColor = TextSilver,
            Location = new Point(100, 58),
            AutoSize = true
        };

        _btnFullScreenToggle = new ModernButton
        {
            Text = "Full Screen (F11)",
            Size = new Size(130, 28),
            BaseBackColor = BrandSurface,
            BorderColor = BrandBorderSubtle,
            Font = ThemeFonts.Badge,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _btnFullScreenToggle.Location = new Point(_pnlHeader.Width - 154, 30);
        _btnFullScreenToggle.Click += (s, e) => ToggleFullScreen();

        _btnUpdatePill = new ModernButton
        {
            Text = "⚡ Update Ready",
            Size = new Size(180, 28),
            BaseBackColor = AccentBadgeBg,
            BorderColor = AccentCyan,
            Font = ThemeFonts.Badge,
            Visible = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _btnUpdatePill.Location = new Point(_pnlHeader.Width - 344, 30);

        _pnlHeader.Resize += (s, e) =>
        {
            _btnFullScreenToggle.Location = new Point(_pnlHeader.Width - 154, 30);
            _btnUpdatePill.Location = new Point(_pnlHeader.Width - 344, 30);
        };

        _pnlHeader.Controls.AddRange([_picLogo, _lblTitle, _lblSubTitle, _lblActiveBanner, _btnFullScreenToggle, _btnUpdatePill]);

        // =========================================================================
        // 2. MODE SELECTOR STRIP (Pinned Beneath Header)
        // =========================================================================
        var pnlModes = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(24, 6, 24, 6),
            BackColor = BrandCanvas
        };

        var tblModes = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        tblModes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        tblModes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        tblModes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));

        _btnModeAuto = new ModernButton { Text = "Auto (Quality Engine)", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0), Font = ThemeFonts.ButtonText };
        _btnModeWifi = new ModernButton { Text = "Prefer Wi-Fi", Dock = DockStyle.Fill, Margin = new Padding(3, 0, 3, 0), Font = ThemeFonts.ButtonText };
        _btnModeEth = new ModernButton { Text = "Prefer Ethernet", Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 0), Font = ThemeFonts.ButtonText };

        _btnModeAuto.Click += (s, e) => RequestModeChange?.Invoke(RoutingMode.Auto);
        _btnModeWifi.Click += (s, e) => RequestModeChange?.Invoke(RoutingMode.PreferWiFi);
        _btnModeEth.Click += (s, e) => RequestModeChange?.Invoke(RoutingMode.PreferEthernet);

        tblModes.Controls.Add(_btnModeAuto, 0, 0);
        tblModes.Controls.Add(_btnModeWifi, 1, 0);
        tblModes.Controls.Add(_btnModeEth, 2, 0);
        pnlModes.Controls.Add(tblModes);

        // =========================================================================
        // 3. BOTTOM ACTION BAR (Pinned Bottom, Fixed Height)
        // =========================================================================
        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            Padding = new Padding(24, 10, 24, 10),
            BackColor = BrandCanvas
        };
        pnlBottom.Paint += (s, e) =>
        {
            using var borderPen = new Pen(BrandBorderSubtle, 1.0f);
            e.Graphics.DrawLine(borderPen, 0, 0, pnlBottom.Width, 0);
        };

        _btnProbeNow = new ModernButton
        {
            Text = "Measure Quality Now",
            Size = new Size(180, 36),
            Location = new Point(24, 10),
            BaseBackColor = AccentBlue,
            BorderColor = AccentCyan,
            Font = ThemeFonts.ButtonText,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _btnProbeNow.Click += (s, e) => RequestCheckNow?.Invoke();

        _chkStartup = new CheckBox
        {
            Text = "Start with Windows",
            Location = new Point(220, 18),
            AutoSize = true,
            ForeColor = TextMuted,
            Font = ThemeFonts.Body,
            Checked = StartupManager.IsStartupEnabled(),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _chkStartup.CheckedChanged += (s, e) => StartupManager.SetStartup(_chkStartup.Checked);

        _btnMinimizeTray = new ModernButton
        {
            Text = "Minimize to Tray",
            Size = new Size(160, 36),
            BaseBackColor = BrandSurface,
            BorderColor = BrandBorder,
            Font = ThemeFonts.ButtonText,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        _btnMinimizeTray.Location = new Point(pnlBottom.Width - 184, 10);
        _btnMinimizeTray.Click += (s, e) => Hide();
        pnlBottom.Resize += (s, e) =>
        {
            _btnMinimizeTray.Location = new Point(pnlBottom.Width - 184, 10);
        };

        pnlBottom.Controls.AddRange([_btnProbeNow, _chkStartup, _btnMinimizeTray]);

        // =========================================================================
        // 4. MAIN WORKSPACE (Dock = Fill, Resizes Gracefully to Any Screen Size)
        // =========================================================================
        var pnlMain = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 4, 24, 8),
            BackColor = BrandCanvas
        };

        // Two Cards Side-by-Side (50% / 50% responsive table)
        var tblCards = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 245,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        tblCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50.0f));
        tblCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50.0f));

        _cardEth = CreateStudioCard(
            "ETHERNET", "DIRECT ROUTER",
            TextSilver,
            out _lblEthBadge,
            out _lblEthStatus,
            out _lblEthScoreVal,
            out _pbEthScore,
            out _lblEthLatVal,
            out _lblEthGwVal,
            out _lblEthLossVal,
            out _lblEthSpeedVal);
        _cardEth.Margin = new Padding(0, 0, 6, 0);

        _cardWifi = CreateStudioCard(
            "WI-FI", "MOBILE HOTSPOT",
            AccentCyan,
            out _lblWifiBadge,
            out _lblWifiStatus,
            out _lblWifiScoreVal,
            out _pbWifiScore,
            out _lblWifiLatVal,
            out _lblWifiGwVal,
            out _lblWifiLossVal,
            out _lblWifiSpeedVal);
        _cardWifi.Margin = new Padding(6, 0, 0, 0);

        tblCards.Controls.Add(_cardEth, 0, 0);
        tblCards.Controls.Add(_cardWifi, 1, 0);

        // Live Telemetry Stream (Expands to Fill All Remaining Space)
        var pnlLogSection = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 10, 0, 0)
        };

        var lblLogTitle = new SmoothLabel
        {
            Text = "LIVE ENGINE TELEMETRY & ROUTING DECISIONS",
            Dock = DockStyle.Top,
            Height = 22,
            Font = ThemeFonts.Subhead,
            ForeColor = TextSubtle
        };

        var pnlLogBox = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BrandCanvas,
            Padding = new Padding(10, 8, 10, 8)
        };
        pnlLogBox.Paint += (s, e) =>
        {
            var r = new Rectangle(0, 0, pnlLogBox.Width - 1, pnlLogBox.Height - 1);
            using var path = CreateRoundedRectPath(r, 6);
            using var pen = new Pen(BrandBorderSubtle, 1.0f);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(pen, path);
        };

        _txtLogs = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = BrandCanvas,
            ForeColor = TextSilver,
            BorderStyle = BorderStyle.None,
            Font = ThemeFonts.LogText,
            ScrollBars = RichTextBoxScrollBars.Vertical
        };
        pnlLogBox.Controls.Add(_txtLogs);

        pnlLogSection.Controls.Add(pnlLogBox);
        pnlLogSection.Controls.Add(lblLogTitle);

        pnlMain.Controls.Add(pnlLogSection);
        pnlMain.Controls.Add(tblCards);

        // Assemble Window Panels in Order
        Controls.Add(pnlMain);
        Controls.Add(pnlBottom);
        Controls.Add(pnlModes);
        Controls.Add(_pnlHeader);

        FormClosing += (s, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
        };

        AppLogger.OnLogEntry += AppendLog;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F11)
        {
            ToggleFullScreen();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    public void ToggleFullScreen()
    {
        if (!_isFullScreen)
        {
            _previousWindowState = WindowState;
            _previousBorderStyle = FormBorderStyle;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            _isFullScreen = true;
            _btnFullScreenToggle.Text = "Exit Full (F11)";
        }
        else
        {
            FormBorderStyle = _previousBorderStyle;
            WindowState = _previousWindowState;
            _isFullScreen = false;
            _btnFullScreenToggle.Text = "Full Screen (F11)";
        }
    }

    private Panel CreateStudioCard(
        string primaryName, string subName,
        Color accentColor,
        out SmoothLabel lblBadge,
        out SmoothLabel lblStatus,
        out SmoothLabel lblScoreVal,
        out SmoothProgressBar pbScore,
        out SmoothLabel lblLatVal,
        out SmoothLabel lblGwVal,
        out SmoothLabel lblLossVal,
        out SmoothLabel lblSpeedVal)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BrandSurface,
            Padding = new Padding(14)
        };
        card.Paint += (s, e) =>
        {
            var r = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
            using var path = CreateRoundedRectPath(r, 8);
            using var pen = new Pen(BrandBorder, 1.0f);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(pen, path);
        };

        // Header strip
        var pnlCardTop = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.Transparent
        };

        var lblPrimary = new SmoothLabel
        {
            Text = primaryName,
            Font = ThemeFonts.CardTitle,
            ForeColor = TextWhite,
            Location = new Point(0, 0),
            AutoSize = true
        };

        var lblSub = new SmoothLabel
        {
            Text = subName,
            Font = ThemeFonts.Badge,
            ForeColor = TextSubtle,
            Location = new Point(1, 18),
            AutoSize = true
        };

        var badgeControl = new SmoothLabel
        {
            Text = "STANDBY",
            Font = ThemeFonts.Badge,
            ForeColor = TextSubtle,
            BackColor = Color.FromArgb(20, 27, 36),
            Size = new Size(72, 20),
            TextAlign = ContentAlignment.MiddleCenter,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        badgeControl.Location = new Point(pnlCardTop.Width - 74, 2);
        badgeControl.Paint += (s, e) =>
        {
            var r = new Rectangle(0, 0, badgeControl.Width - 1, badgeControl.Height - 1);
            using var path = CreateRoundedRectPath(r, 4);
            using var pen = new Pen(BrandBorderSubtle, 1.0f);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(pen, path);
        };
        pnlCardTop.Resize += (s, e) =>
        {
            badgeControl.Location = new Point(pnlCardTop.Width - 74, 2);
        };
        lblBadge = badgeControl;

        var pnlDiv = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 1,
            BackColor = BrandBorderSubtle
        };

        pnlCardTop.Controls.AddRange([lblPrimary, lblSub, badgeControl, pnlDiv]);

        // Score Section
        var pnlScore = new Panel
        {
            Dock = DockStyle.Top,
            Height = 72,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 6, 0, 0)
        };

        var lblScoreLabel = new SmoothLabel
        {
            Text = "QUALITY SCORE",
            Font = ThemeFonts.Badge,
            ForeColor = TextSubtle,
            Location = new Point(0, 6),
            AutoSize = true
        };

        lblScoreVal = new SmoothLabel
        {
            Text = "--",
            Font = ThemeFonts.ScoreDisplay,
            ForeColor = TextWhite,
            Location = new Point(-2, 18),
            AutoSize = true
        };

        var lblScoreMax = new SmoothLabel
        {
            Text = "/ 100",
            Font = ThemeFonts.ScoreMax,
            ForeColor = TextSubtle,
            Location = new Point(54, 28),
            AutoSize = true
        };

        var statusControl = new SmoothLabel
        {
            Text = "Probing telemetry...",
            Font = ThemeFonts.Badge,
            ForeColor = TextMuted,
            Size = new Size(180, 18),
            TextAlign = ContentAlignment.MiddleRight,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        statusControl.Location = new Point(pnlScore.Width - 180, 26);
        pnlScore.Resize += (s, e) =>
        {
            statusControl.Location = new Point(pnlScore.Width - 180, 26);
        };
        lblStatus = statusControl;

        pbScore = new SmoothProgressBar
        {
            Dock = DockStyle.Bottom,
            Height = 6,
            TrackColor = Color.FromArgb(24, 32, 42),
            FillColor = accentColor,
            GradientEndColor = AccentBlue,
            Maximum = 100,
            Value = 0
        };

        pnlScore.Controls.AddRange([lblScoreLabel, lblScoreVal, lblScoreMax, lblStatus, pbScore]);

        // Metric Grid (3 Columns Responsive Table)
        var tblMetrics = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 14, 0, 0)
        };
        tblMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        tblMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        tblMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));

        // Column 1: Latency
        var pnlCol1 = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var lblLatH = new SmoothLabel { Text = "LATENCY", Font = ThemeFonts.MetricLabel, ForeColor = TextSubtle, Location = new Point(0, 0), AutoSize = true };
        lblLatVal = new SmoothLabel { Text = "-- ms", Font = ThemeFonts.MetricValue, ForeColor = TextWhite, Location = new Point(0, 16), AutoSize = true };
        lblGwVal = new SmoothLabel { Text = "GW: -- ms", Font = ThemeFonts.Badge, ForeColor = TextSubtle, Location = new Point(0, 38), AutoSize = true };
        pnlCol1.Controls.AddRange([lblLatH, lblLatVal, lblGwVal]);

        // Column 2: Loss
        var pnlCol2 = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var lblLossH = new SmoothLabel { Text = "PACKET LOSS", Font = ThemeFonts.MetricLabel, ForeColor = TextSubtle, Location = new Point(0, 0), AutoSize = true };
        lblLossVal = new SmoothLabel { Text = "0.0%", Font = ThemeFonts.MetricValue, ForeColor = TextWhite, Location = new Point(0, 16), AutoSize = true };
        var lblLossSub = new SmoothLabel { Text = "TCP verified", Font = ThemeFonts.Badge, ForeColor = TextSubtle, Location = new Point(0, 38), AutoSize = true };
        pnlCol2.Controls.AddRange([lblLossH, lblLossVal, lblLossSub]);

        // Column 3: Speed
        var pnlCol3 = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var lblSpeedH = new SmoothLabel { Text = "THROUGHPUT", Font = ThemeFonts.MetricLabel, ForeColor = TextSubtle, Location = new Point(0, 0), AutoSize = true };
        lblSpeedVal = new SmoothLabel { Text = "-- Mbps", Font = ThemeFonts.MetricValue, ForeColor = TextWhite, Location = new Point(0, 16), AutoSize = true };
        var lblSpeedSub = new SmoothLabel { Text = "Cloudflare", Font = ThemeFonts.Badge, ForeColor = TextSubtle, Location = new Point(0, 38), AutoSize = true };
        pnlCol3.Controls.AddRange([lblSpeedH, lblSpeedVal, lblSpeedSub]);

        tblMetrics.Controls.Add(pnlCol1, 0, 0);
        tblMetrics.Controls.Add(pnlCol2, 1, 0);
        tblMetrics.Controls.Add(pnlCol3, 2, 0);

        card.Controls.Add(tblMetrics);
        card.Controls.Add(pnlScore);
        card.Controls.Add(pnlCardTop);

        return card;
    }

    private static GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public void UpdateMetrics(
        ConnectionMetrics? ethMetrics, ConnectionScore? ethScore,
        ConnectionMetrics? wifiMetrics, ConnectionScore? wifiScore,
        NetworkType? activeType, RoutingMode mode)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => UpdateMetrics(ethMetrics, ethScore, wifiMetrics, wifiScore, activeType, mode));
            return;
        }

        // Update Mode Buttons Selection
        _btnModeAuto.IsSelected = mode == RoutingMode.Auto;
        _btnModeWifi.IsSelected = mode == RoutingMode.PreferWiFi;
        _btnModeEth.IsSelected = mode == RoutingMode.PreferEthernet;

        // Update Active Banner
        string activeName = activeType switch
        {
            NetworkType.WiFi => "Wi-Fi (Mobile Hotspot)",
            NetworkType.Ethernet => "Ethernet (Direct Router)",
            _ => "Evaluating telemetry..."
        };

        _lblActiveBanner.Text = $"● ACTIVE ROUTE: {activeName}";
        _lblActiveBanner.ForeColor = AccentCyan;

        // Update Ethernet Card
        if (ethMetrics != null && ethScore != null)
        {
            bool isEthActive = activeType == NetworkType.Ethernet;
            _lblEthBadge.Text = isEthActive ? "ACTIVE" : "STANDBY";
            _lblEthBadge.ForeColor = isEthActive ? AccentCyan : TextSubtle;
            _lblEthBadge.BackColor = isEthActive ? AccentBadgeBg : Color.FromArgb(20, 27, 36);

            _lblEthStatus.Text = ethMetrics.IsInternetReachable ? "Connected" : "Offline";
            _lblEthStatus.ForeColor = ethMetrics.IsInternetReachable ? AccentCyan : TextSubtle;

            _lblEthLatVal.Text = $"{ethMetrics.InternetLatencyMs:F0} ms";
            _lblEthGwVal.Text = $"GW: {ethMetrics.GatewayLatencyMs:F0} ms";
            _lblEthLossVal.Text = $"{ethMetrics.PacketLossPercent:F1}%";
            _lblEthSpeedVal.Text = ethMetrics.DownloadThroughputMbps > 0 ? $"{ethMetrics.DownloadThroughputMbps:F0} Mbps" : "-- Mbps";

            _lblEthScoreVal.Text = $"{ethScore.TotalScore}";
            _pbEthScore.Value = Math.Clamp(ethScore.TotalScore, 0, 100);
        }

        // Update Wi-Fi Card
        if (wifiMetrics != null && wifiScore != null)
        {
            bool isWifiActive = activeType == NetworkType.WiFi;
            _lblWifiBadge.Text = isWifiActive ? "ACTIVE" : "STANDBY";
            _lblWifiBadge.ForeColor = isWifiActive ? AccentCyan : TextSubtle;
            _lblWifiBadge.BackColor = isWifiActive ? AccentBadgeBg : Color.FromArgb(20, 27, 36);

            _lblWifiStatus.Text = wifiMetrics.IsInternetReachable ? "Connected" : "Offline";
            _lblWifiStatus.ForeColor = wifiMetrics.IsInternetReachable ? AccentCyan : TextSubtle;

            _lblWifiLatVal.Text = $"{wifiMetrics.InternetLatencyMs:F0} ms";
            _lblWifiGwVal.Text = $"GW: {wifiMetrics.GatewayLatencyMs:F0} ms";
            _lblWifiLossVal.Text = $"{wifiMetrics.PacketLossPercent:F1}%";
            _lblWifiSpeedVal.Text = wifiMetrics.DownloadThroughputMbps > 0 ? $"{wifiMetrics.DownloadThroughputMbps:F0} Mbps" : "-- Mbps";

            _lblWifiScoreVal.Text = $"{wifiScore.TotalScore}";
            _pbWifiScore.Value = Math.Clamp(wifiScore.TotalScore, 0, 100);
        }
    }

    public void SetUpdateReady(string version, Action onRestartClick)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => SetUpdateReady(version, onRestartClick));
            return;
        }

        _btnUpdatePill.Text = $"⚡ Restart for v{version}";
        _btnUpdatePill.Visible = true;
        _btnUpdatePill.Click += (s, e) => onRestartClick();
    }

    private void AppendLog(string logLine)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLog(logLine));
            return;
        }

        if (_txtLogs.IsDisposed) return;

        Color tagColor = TextSilver;
        if (logLine.Contains("EMERGENCY")) tagColor = TextWhite;
        else if (logLine.Contains("Route preference changed")) tagColor = AccentCyan;
        else if (logLine.Contains("Decision:")) tagColor = AccentBlue;

        _txtLogs.SelectionStart = _txtLogs.TextLength;
        _txtLogs.SelectionLength = 0;
        _txtLogs.SelectionColor = tagColor;
        _txtLogs.AppendText(logLine + Environment.NewLine);
        _txtLogs.ScrollToCaret();

        // Cap lines at 800
        if (_txtLogs.Lines.Length > 800)
        {
            _txtLogs.Text = string.Join(Environment.NewLine, _txtLogs.Lines.TakeLast(400));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            AppLogger.OnLogEntry -= AppendLog;
        }
        base.Dispose(disposing);
    }
}
