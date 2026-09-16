using System.Runtime.InteropServices;
using SmartSwitcher.Core;
using SmartSwitcher.Core.Models;
using SmartSwitcher.Logging;
using SmartSwitcher.UI;

namespace SmartSwitcher;

static class Program
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    private const int ATTACH_PARENT_PROCESS = -1;
    private const int STD_OUTPUT_HANDLE = -11;
    private const int STD_ERROR_HANDLE = -12;

    [STAThread]
    static void Main(string[] args)
    {
        // Intercept Velopack install/update/uninstall hooks at the true process entry point
        Velopack.VelopackApp.Build().Run();

        bool isDiagnostic = args.Any(a => a.Equals("--diagnostic", StringComparison.OrdinalIgnoreCase) ||
                                          a.Equals("-d", StringComparison.OrdinalIgnoreCase) ||
                                          a.Equals("--test", StringComparison.OrdinalIgnoreCase));

        if (isDiagnostic)
        {
            if (!Console.IsOutputRedirected)
            {
                if (AttachConsole(ATTACH_PARENT_PROCESS))
                {
                    var handle = GetStdHandle(STD_OUTPUT_HANDLE);
                    if (handle != IntPtr.Zero && handle != (IntPtr)(-1))
                    {
                        var fileStream = new FileStream(new Microsoft.Win32.SafeHandles.SafeFileHandle(handle, false), FileAccess.Write);
                        var writer = new StreamWriter(fileStream, Console.OutputEncoding) { AutoFlush = true };
                        Console.SetOut(writer);
                        Console.SetError(writer);
                    }
                }
            }

            Console.WriteLine();
            RunDiagnosticModeAsync().GetAwaiter().GetResult();
            return;
        }

        bool startMinimized = args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
                                            a.Equals("-m", StringComparison.OrdinalIgnoreCase));

        // WinForms Desktop GUI & System Tray Mode
        ApplicationConfiguration.Initialize();
        using var trayContext = new TrayApplicationContext(startMinimized);
        Application.Run(trayContext);
    }

    private static async Task RunDiagnosticModeAsync()
    {
        Console.WriteLine("=================================================");
        Console.WriteLine("        Smart Switcher - Diagnostic Mode         ");
        Console.WriteLine("=================================================");
        Console.WriteLine("Discovering interfaces and probing connections...");
        Console.WriteLine("Note: Routing table will NOT be modified.\n");

        var ifManager = new NetworkInterfaceManager();
        var candidates = ifManager.GetCandidateInterfaces();

        var eth = ifManager.GetEthernetInterface(candidates);
        var wifi = ifManager.GetWiFiInterface(candidates);

        if (eth == null && wifi == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: No physical Ethernet or Wi-Fi network interfaces detected.");
            Console.ResetColor();
            return;
        }

        var probe = new ConnectionProbe();
        var scorer = new ConnectionScorer();
        var policy = new SwitchingPolicy();

        ConnectionMetrics? ethMetrics = null;
        ConnectionScore? ethScore = null;
        ConnectionMetrics? wifiMetrics = null;
        ConnectionScore? wifiScore = null;

        // Probe Ethernet
        if (eth != null)
        {
            Console.Write($"Probing Ethernet ({eth.Name} - {eth.Description})... ");
            ethMetrics = await probe.ProbeAsync(eth, includeThroughput: true);
            ethScore = scorer.CalculateScore(ethMetrics);
            Console.WriteLine("Done.");
        }

        // Probe Wi-Fi
        if (wifi != null)
        {
            Console.Write($"Probing Wi-Fi ({wifi.Name} - {wifi.Description})... ");
            wifiMetrics = await probe.ProbeAsync(wifi, includeThroughput: true);
            wifiScore = scorer.CalculateScore(wifiMetrics);
            Console.WriteLine("Done.");
        }

        Console.WriteLine();

        // Print results in the exact requested format
        if (eth != null && ethMetrics != null && ethScore != null)
        {
            PrintInterfaceSummary("Ethernet", ethMetrics, ethScore);
        }
        else
        {
            Console.WriteLine("Ethernet\n  Status: Not detected or disconnected\n");
        }

        if (wifi != null && wifiMetrics != null && wifiScore != null)
        {
            PrintInterfaceSummary("Wi-Fi", wifiMetrics, wifiScore);
        }
        else
        {
            Console.WriteLine("Wi-Fi\n  Status: Not detected or disconnected\n");
        }

        // Evaluate decision
        var decision = policy.Evaluate(
            ethScore ?? ConnectionScore.Zero("Not available"),
            wifiScore ?? ConnectionScore.Zero("Not available"),
            eth != null,
            wifi != null);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Selected: {decision.TargetType}");
        Console.ResetColor();
        Console.WriteLine($"Reason:   {decision.Reason}");
        Console.WriteLine("=================================================\n");

        string ethLog = ethMetrics != null
            ? $"latency={ethMetrics.InternetLatencyMs:F0}ms loss={ethMetrics.PacketLossPercent:F0}% throughput={ethMetrics.DownloadThroughputMbps:F0}Mbps score={ethScore?.TotalScore}"
            : "disconnected";
        string wifiLog = wifiMetrics != null
            ? $"latency={wifiMetrics.InternetLatencyMs:F0}ms loss={wifiMetrics.PacketLossPercent:F0}% throughput={wifiMetrics.DownloadThroughputMbps:F0}Mbps score={wifiScore?.TotalScore}"
            : "disconnected";
        AppLogger.LogDecision(ethLog, wifiLog, decision.Reason);
    }

    private static void PrintInterfaceSummary(string title, ConnectionMetrics m, ConnectionScore s)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(title);
        Console.ResetColor();

        string internetStatus = m.IsInternetReachable ? "Connected" : "Disconnected";
        Console.WriteLine($"  Internet:    {internetStatus}");
        if (m.GatewayLatencyMs >= 0)
        {
            Console.WriteLine($"  Gateway:     {m.GatewayLatencyMs:F0} ms");
        }
        Console.WriteLine($"  Latency:     {m.InternetLatencyMs:F0} ms");
        Console.WriteLine($"  Packet loss: {m.PacketLossPercent:F0}%");
        Console.WriteLine($"  Download:    {m.DownloadThroughputMbps:F0} Mbps");

        Console.ForegroundColor = s.TotalScore >= 70 ? ConsoleColor.Green : (s.TotalScore >= 40 ? ConsoleColor.DarkYellow : ConsoleColor.Red);
        Console.WriteLine($"  Score:       {s.TotalScore}");
        Console.ResetColor();
        Console.WriteLine();
    }
}