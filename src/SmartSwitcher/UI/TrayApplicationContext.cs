using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using SmartSwitcher.Core;
using SmartSwitcher.Core.Models;
using SmartSwitcher.Logging;
using SmartSwitcher.Windows;

namespace SmartSwitcher.UI;

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ToolStripMenuItem _menuAuto;
    private readonly ToolStripMenuItem _menuPreferWifi;
    private readonly ToolStripMenuItem _menuPreferEth;
    private readonly ToolStripMenuItem _menuStartup;
    private readonly ToolStripMenuItem _menuCheckUpdate;
    private readonly ToolStripMenuItem _menuRestartUpdate;

    private readonly NetworkInterfaceManager _ifManager;
    private readonly ConnectionProbe _probe;
    private readonly ConnectionScorer _scorer;
    private readonly SwitchingPolicy _policy;
    private readonly RouteManager _routeManager;
    private readonly StatusForm _statusForm;
    private readonly UpdateService _updateService;

    private readonly System.Windows.Forms.Timer _timer;
    private bool _isChecking;
    private DateTime _lastThroughputTestTime = DateTime.MinValue;
    private static readonly TimeSpan ThroughputInterval = TimeSpan.FromMinutes(3);

    public TrayApplicationContext(bool startMinimized = false)
    {
        _ifManager = new NetworkInterfaceManager();
        _probe = new ConnectionProbe();
        _scorer = new ConnectionScorer();
        _policy = new SwitchingPolicy();
        _routeManager = new RouteManager();
        _statusForm = new StatusForm();
        _updateService = new UpdateService();

        _statusForm.RequestCheckNow += () => _ = RunMonitoringCycleAsync(forceThroughput: true);
        _statusForm.RequestModeChange += mode => SetMode(mode);
        _updateService.StateChanged += OnUpdateStateChanged;

        // Build Context Menu
        _contextMenu = new ContextMenuStrip();

        _menuRestartUpdate = new ToolStripMenuItem("⚡ Restart to Install Update", null, (s, e) => _updateService.RestartAndApplyUpdate())
        {
            Visible = false
        };

        _menuAuto = new ToolStripMenuItem("Auto", null, (s, e) => SetMode(RoutingMode.Auto)) { Checked = true };
        _menuPreferWifi = new ToolStripMenuItem("Prefer Wi-Fi", null, (s, e) => SetMode(RoutingMode.PreferWiFi));
        _menuPreferEth = new ToolStripMenuItem("Prefer Ethernet", null, (s, e) => SetMode(RoutingMode.PreferEthernet));

        var menuCheckNow = new ToolStripMenuItem("Check connections now", null, (s, e) => _ = RunMonitoringCycleAsync(forceThroughput: true));
        _menuCheckUpdate = new ToolStripMenuItem("Check for Updates...", null, async (s, e) => await CheckUpdatesManualAsync());
        var menuOpenStatus = new ToolStripMenuItem("Open Dashboard Window...", null, (s, e) => ShowStatusWindow());

        _menuStartup = new ToolStripMenuItem("Start with Windows", null, (s, e) => ToggleStartup())
        {
            Checked = StartupManager.IsStartupEnabled()
        };

        var menuExit = new ToolStripMenuItem("Exit", null, (s, e) => ExitApplication());

        _contextMenu.Items.AddRange(
        [
            _menuRestartUpdate,
            _menuAuto,
            _menuPreferWifi,
            _menuPreferEth,
            new ToolStripSeparator(),
            menuCheckNow,
            _menuCheckUpdate,
            new ToolStripSeparator(),
            menuOpenStatus,
            _menuStartup,
            new ToolStripSeparator(),
            menuExit
        ]);

        // Build Tray Icon using the pristine raw SVG squircle brand icon
        _currentIcon = GenerateBrandTrayIcon(activeType: null, isConnected: true);
        _notifyIcon = new NotifyIcon
        {
            Icon = _currentIcon,
            Text = "Smart Switcher - Initializing...",
            ContextMenuStrip = _contextMenu,
            Visible = true
        };

        // Left-click or double-click to restore dashboard
        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ShowStatusWindow();
            }
        };
        _notifyIcon.DoubleClick += (s, e) => ShowStatusWindow();

        AppLogger.Info("Smart Switcher tray service started.");

        // Periodic check timer (15 seconds for lightweight probes)
        _timer = new System.Windows.Forms.Timer
        {
            Interval = 15000
        };
        _timer.Tick += (s, e) => _ = RunMonitoringCycleAsync(forceThroughput: false);
        _timer.Start();

        // Run initial check right after launch
        _ = RunMonitoringCycleAsync(forceThroughput: true);

        // Automatically open the graphical UI if not launched in minimized background mode
        if (!startMinimized)
        {
            ShowStatusWindow();
        }

        // Silent background update check after 5 seconds
        Task.Delay(5000).ContinueWith(_ => _ = _updateService.CheckAndDownloadUpdatesAsync(silent: true));
    }

    private void OnUpdateStateChanged(UpdateState state, string? message)
    {
        if (state == UpdateState.ReadyToInstall && !string.IsNullOrEmpty(_updateService.LatestVersion))
        {
            _menuRestartUpdate.Text = $"⚡ Restart to Install v{_updateService.LatestVersion}";
            _menuRestartUpdate.Visible = true;
            _statusForm.SetUpdateReady(_updateService.LatestVersion, () => _updateService.RestartAndApplyUpdate());
            _notifyIcon.ShowBalloonTip(5000, "Smart Switcher Update Ready", $"Version {_updateService.LatestVersion} has been downloaded and is ready to install.", ToolTipIcon.Info);
        }
    }

    private async Task CheckUpdatesManualAsync()
    {
        _menuCheckUpdate.Enabled = false;
        _menuCheckUpdate.Text = "Checking for updates...";
        bool updateFound = await _updateService.CheckAndDownloadUpdatesAsync(silent: false);
        _menuCheckUpdate.Enabled = true;
        _menuCheckUpdate.Text = "Check for Updates...";
        if (!updateFound && _updateService.State == UpdateState.UpToDate)
        {
            _notifyIcon.ShowBalloonTip(3000, "Smart Switcher", "You are running the latest version.", ToolTipIcon.Info);
        }
    }

    private void SetMode(RoutingMode mode)
    {
        _policy.Mode = mode;
        _menuAuto.Checked = mode == RoutingMode.Auto;
        _menuPreferWifi.Checked = mode == RoutingMode.PreferWiFi;
        _menuPreferEth.Checked = mode == RoutingMode.PreferEthernet;

        // Instant visual feedback for user
        if (mode == RoutingMode.PreferWiFi)
        {
            UpdateTrayIcon(NetworkType.WiFi, isConnected: true);
            _notifyIcon.Text = "Smart Switcher: Wi-Fi (Manual Override)";
        }
        else if (mode == RoutingMode.PreferEthernet)
        {
            UpdateTrayIcon(NetworkType.Ethernet, isConnected: true);
            _notifyIcon.Text = "Smart Switcher: Ethernet (Manual Override)";
        }

        AppLogger.Info($"Mode changed to: {mode}");
        _ = RunMonitoringCycleAsync(forceThroughput: false);
    }

    private void ToggleStartup()
    {
        bool newState = !_menuStartup.Checked;
        if (StartupManager.SetStartup(newState))
        {
            _menuStartup.Checked = newState;
            AppLogger.Info($"Start with Windows set to: {newState}");
        }
        else
        {
            MessageBox.Show("Could not update startup registry setting.", "Smart Switcher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ShowStatusWindow()
    {
        if (!_statusForm.Visible)
        {
            _statusForm.Show();
        }
        _statusForm.WindowState = FormWindowState.Normal;
        _statusForm.BringToFront();
        _statusForm.Activate();
    }

    private async Task RunMonitoringCycleAsync(bool forceThroughput)
    {
        if (_isChecking) return;
        _isChecking = true;

        try
        {
            var candidates = _ifManager.GetCandidateInterfaces();
            if (candidates.Count == 0)
            {
                AppLogger.Warn("No candidate network interfaces detected.");
                UpdateTrayIcon(null, false);
                _notifyIcon.Text = "Smart Switcher: No Network Adapters";
                return;
            }

            bool runThroughput = forceThroughput || (DateTime.Now - _lastThroughputTestTime >= ThroughputInterval);

            // Concurrently probe all available adapters in parallel (fast & scalable to 2, 5, 10+ interfaces)
            var probeTasks = candidates.Select(async ifInfo =>
            {
                var metrics = await _probe.ProbeAsync(ifInfo, includeThroughput: runThroughput);
                var score = _scorer.CalculateScore(metrics);
                return new ScoredInterface
                {
                    Info = ifInfo,
                    Metrics = metrics,
                    Score = score
                };
            }).ToList();

            var scoredList = (await Task.WhenAll(probeTasks)).ToList();

            if (runThroughput && scoredList.Any(s => s.Metrics.DownloadThroughputMbps > 0))
            {
                _lastThroughputTestTime = DateTime.Now;
            }

            // Evaluate through policy engine (Emergency Zero-Delay Failover + Anti-Flap Hysteresis)
            var decision = _policy.Evaluate(scoredList);

            var ethScored = scoredList.FirstOrDefault(s => s.Info.Type == NetworkType.Ethernet);
            var wifiScored = scoredList.FirstOrDefault(s => s.Info.Type == NetworkType.WiFi);

            string ethLog = ethScored != null
                ? $"latency={ethScored.Metrics.InternetLatencyMs:F0}ms loss={ethScored.Metrics.PacketLossPercent:F0}% throughput={ethScored.Metrics.DownloadThroughputMbps:F0}Mbps score={ethScored.Score.TotalScore}"
                : "disconnected";

            string wifiLog = wifiScored != null
                ? $"latency={wifiScored.Metrics.InternetLatencyMs:F0}ms loss={wifiScored.Metrics.PacketLossPercent:F0}% throughput={wifiScored.Metrics.DownloadThroughputMbps:F0}Mbps score={wifiScored.Score.TotalScore}"
                : "disconnected";

            string? routeChangeLog = null;

            // Apply route change if decision requires it
            if (decision.ShouldSwitch)
            {
                var targetIf = decision.TargetInterface ??
                               scoredList.FirstOrDefault(s => s.Info.Type == decision.TargetType)?.Info;

                if (targetIf != null)
                {
                    var otherCandidates = scoredList
                        .Where(s => s.Info.InterfaceIndex != targetIf.InterfaceIndex)
                        .Select(s => s.Info)
                        .ToList();

                    bool switched = _routeManager.SetPreferredInterface(targetIf, otherCandidates);
                    if (switched)
                    {
                        _policy.SetActiveInterface(targetIf);
                        routeChangeLog = $"{targetIf.Name} ({(decision.IsEmergencyFailover ? "Emergency" : "Auto")})";
                    }
                }
            }

            // Log decision
            AppLogger.LogDecision(ethLog, wifiLog, decision.Reason, routeChangeLog);

            // Update UI & Tray Tooltip and Icon
            var active = _policy.CurrentPreferredType ?? decision.TargetType;
            bool anyInternet = scoredList.Any(s => s.Metrics.IsInternetReachable);
            UpdateTrayIcon(active, isConnected: anyInternet);

            int ethScoreVal = ethScored?.Score.TotalScore ?? 0;
            int wifiScoreVal = wifiScored?.Score.TotalScore ?? 0;
            string trayText = $"Smart Switcher: {active} (Active)\nEth: {ethScoreVal} | WiFi: {wifiScoreVal}";
            if (trayText.Length >= 64) trayText = trayText[..63]; // NotifyIcon text limit
            _notifyIcon.Text = trayText;

            _statusForm.UpdateMetrics(
                ethScored?.Metrics, ethScored?.Score,
                wifiScored?.Metrics, wifiScored?.Score,
                active,
                _policy.Mode);
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Monitoring cycle encountered error: {ex.Message}");
        }
        finally
        {
            _isChecking = false;
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private Icon? _currentIcon;
    private NetworkType? _lastActiveType;
    private bool? _lastConnectedState;

    private void UpdateTrayIcon(NetworkType? activeType, bool isConnected)
    {
        try
        {
            if (_currentIcon != null && _lastActiveType == activeType && _lastConnectedState == isConnected)
            {
                return; // State unchanged, keep current native icon
            }

            Icon newIcon = GenerateBrandTrayIcon(activeType, isConnected);

            var oldIcon = _currentIcon;
            _notifyIcon.Icon = newIcon;
            _currentIcon = newIcon;
            _lastActiveType = activeType;
            _lastConnectedState = isConnected;

            if (oldIcon != null)
            {
                DestroyIcon(oldIcon.Handle);
                oldIcon.Dispose();
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Failed to update tray icon: {ex.Message}");
        }
    }

    private static Icon GenerateBrandTrayIcon(NetworkType? activeType, bool isConnected)
    {
        try
        {
            string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "logo_32.png");
            if (File.Exists(logoPath))
            {
                using var baseBmp = new Bitmap(logoPath);

                if (!isConnected)
                {
                    // Render muted version for disconnected state
                    using var dimBmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(dimBmp))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.Clear(Color.Transparent);

                        var cm = new ColorMatrix
                        {
                            Matrix33 = 0.45f // 45% opacity
                        };
                        using var ia = new ImageAttributes();
                        ia.SetColorMatrix(cm);
                        g.DrawImage(baseBmp, new Rectangle(0, 0, 32, 32), 0, 0, 32, 32, GraphicsUnit.Pixel, ia);

                        // Amber disconnected indicator dot at bottom-right
                        using var warnBrush = new SolidBrush(Color.FromArgb(220, 160, 40));
                        using var warnPen = new Pen(Color.FromArgb(14, 19, 26), 1.2f);
                        g.FillEllipse(warnBrush, 22, 22, 8, 8);
                        g.DrawEllipse(warnPen, 22, 22, 8, 8);
                    }
                    IntPtr hDim = dimBmp.GetHicon();
                    return Icon.FromHandle(hDim);
                }

                // Connected: Use the pristine raw SVG squircle shape with zero white pixel fringes
                IntPtr hIcon = baseBmp.GetHicon();
                return Icon.FromHandle(hIcon);
            }

            string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
            if (File.Exists(icoPath))
            {
                return new Icon(icoPath, 32, 32);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Failed to load brand tray icon: {ex.Message}");
        }

        return GenerateFallbackIcon();
    }

    private static Icon GenerateFallbackIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var path = CreateRoundedRectanglePath(new Rectangle(1, 1, 30, 30), 7);
            using var bgBrush = new SolidBrush(Color.FromArgb(14, 19, 26));
            g.FillPath(bgBrush, path);

            using var pen = new Pen(Color.FromArgb(69, 195, 252), 2.0f);
            g.DrawEllipse(pen, 10, 10, 12, 12);
        }

        IntPtr hIcon = bitmap.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
        path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
        path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
        path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void ExitApplication()
    {
        _timer.Stop();
        _notifyIcon.Visible = false;
        _routeManager.RestoreDefaultMetrics();
        _statusForm.Dispose();
        _notifyIcon.Dispose();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _notifyIcon.Dispose();
            _contextMenu.Dispose();
            _routeManager.Dispose();
            _statusForm.Dispose();
        }
        base.Dispose(disposing);
    }
}
