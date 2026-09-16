using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using SmartSwitcher.Core.Models;

namespace SmartSwitcher.Core;

public class ConnectionProbe
{
    private static readonly (IPAddress ip, int port)[] InternetProbeEndpoints =
    [
        (IPAddress.Parse("1.1.1.1"), 53),
        (IPAddress.Parse("8.8.8.8"), 53),
        (IPAddress.Parse("1.0.0.1"), 53),
        (IPAddress.Parse("8.8.4.4"), 53)
    ];

    private const string ThroughputTestUrl = "https://speed.cloudflare.com/__down?bytes=1048576"; // 1 MB
    private const int ThroughputTimeoutMs = 6000;
    private const int TcpConnectTimeoutMs = 1500;

    /// <summary>
    /// Measures the connection quality of the specified network interface in isolation.
    /// All sockets and HTTP requests are explicitly bound to the adapter's local IP and interface index.
    /// </summary>
    public async Task<ConnectionMetrics> ProbeAsync(
        NetworkInterfaceInfo ifInfo,
        bool includeThroughput = true,
        CancellationToken ct = default)
    {
        try
        {
            // 1. Gateway latency (ICMP ping directly on local subnet)
            double gatewayLatency = await MeasureGatewayLatencyAsync(ifInfo, ct);

            // 2. Internet reachability, Internet latency & Packet Loss
            var (isReachable, avgLatency, packetLoss, jitter) = await MeasureInternetLatencyAndLossAsync(ifInfo, ct);

            if (!isReachable)
            {
                return ConnectionMetrics.Unreachable("Internet probe failed to reach targets.");
            }

            // 3. Optional throughput test (controlled 1MB download bound to interface)
            double throughputMbps = 0.0;
            string? publicIp = null;

            if (includeThroughput)
            {
                var (speed, ip) = await MeasureThroughputAndPublicIpAsync(ifInfo, ct);
                throughputMbps = speed;
                publicIp = ip;
            }

            // 4. Calculate stability score (0 - 100)
            // Low jitter and 0% packet loss produce ~100. High loss or severe jitter penalizes stability.
            double stability = CalculateStabilityScore(packetLoss, jitter);

            return new ConnectionMetrics
            {
                IsInternetReachable = true,
                GatewayLatencyMs = gatewayLatency,
                InternetLatencyMs = avgLatency,
                PacketLossPercent = packetLoss,
                DownloadThroughputMbps = throughputMbps,
                StabilityScore = stability,
                PublicIpAddress = publicIp,
                Timestamp = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            return ConnectionMetrics.Unreachable(ex.Message);
        }
    }

    private async Task<double> MeasureGatewayLatencyAsync(NetworkInterfaceInfo ifInfo, CancellationToken ct)
    {
        if (ifInfo.GatewayAddress == null)
            return -1;

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ifInfo.GatewayAddress, 1000);
            if (reply.Status == IPStatus.Success)
            {
                return reply.RoundtripTime;
            }
        }
        catch
        {
            // Gateway may not respond to ICMP
        }

        return -1;
    }

    private async Task<(bool isReachable, double avgLatency, double packetLoss, double jitter)>
        MeasureInternetLatencyAndLossAsync(NetworkInterfaceInfo ifInfo, CancellationToken ct)
    {
        var latencies = new List<double>();
        int totalAttempts = InternetProbeEndpoints.Length;
        int failedAttempts = 0;

        foreach (var (targetIp, port) in InternetProbeEndpoints)
        {
            if (ct.IsCancellationRequested) break;

            double rtt = await ProbeTcpLatencyAsync(ifInfo.LocalAddress, ifInfo.InterfaceIndex, targetIp, port, ct);
            if (rtt >= 0)
            {
                latencies.Add(rtt);
            }
            else
            {
                failedAttempts++;
            }
        }

        if (latencies.Count == 0)
        {
            return (false, -1, 100.0, 0);
        }

        double lossPercent = (double)failedAttempts / totalAttempts * 100.0;
        double avgLatency = latencies.Average();

        // Calculate jitter (mean absolute deviation from the average)
        double jitter = latencies.Select(l => Math.Abs(l - avgLatency)).Average();

        return (true, avgLatency, lossPercent, jitter);
    }

    private async Task<double> ProbeTcpLatencyAsync(
        IPAddress localAddress,
        int ifIndex,
        IPAddress targetIp,
        int targetPort,
        CancellationToken ct)
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };

        try
        {
            // Force socket outbound through this interface
            socket.Bind(new IPEndPoint(localAddress, 0));
            int nboIfIndex = IPAddress.HostToNetworkOrder(ifIndex);
            socket.SetSocketOption(SocketOptionLevel.IP, (SocketOptionName)31, nboIfIndex);

            var sw = Stopwatch.StartNew();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TcpConnectTimeoutMs);

            await socket.ConnectAsync(new IPEndPoint(targetIp, targetPort), cts.Token);
            sw.Stop();
            return sw.Elapsed.TotalMilliseconds;
        }
        catch
        {
            return -1;
        }
    }

    private async Task<(double throughputMbps, string? publicIp)> MeasureThroughputAndPublicIpAsync(
        NetworkInterfaceInfo ifInfo,
        CancellationToken ct)
    {
        try
        {
            var handler = new SocketsHttpHandler
            {
                ConnectCallback = async (context, token) =>
                {
                    var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    socket.Bind(new IPEndPoint(ifInfo.LocalAddress, 0));
                    int nboIfIndex = IPAddress.HostToNetworkOrder(ifInfo.InterfaceIndex);
                    socket.SetSocketOption(SocketOptionLevel.IP, (SocketOptionName)31, nboIfIndex);
                    await socket.ConnectAsync(context.DnsEndPoint, token);
                    return new NetworkStream(socket, ownsSocket: true);
                }
            };

            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMilliseconds(ThroughputTimeoutMs)
            };

            // Query public IP quickly
            string? publicIp = null;
            try
            {
                using var ipCts = new CancellationTokenSource(2000);
                publicIp = (await client.GetStringAsync("https://api.ipify.org", ipCts.Token)).Trim();
            }
            catch
            {
                // Non-critical, throughput test can still proceed
            }

            // Download 1MB controlled payload
            var sw = Stopwatch.StartNew();
            using var response = await client.GetAsync(ThroughputTestUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            byte[] buffer = new byte[32768];
            long totalBytes = 0;
            using var stream = await response.Content.ReadAsStreamAsync(ct);

            int read;
            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                totalBytes += read;
            }
            sw.Stop();

            double elapsedSec = sw.Elapsed.TotalSeconds;
            if (elapsedSec <= 0.05 || totalBytes == 0)
                return (0, publicIp);

            double mbps = (totalBytes * 8.0) / (elapsedSec * 1_000_000.0);
            return (Math.Round(mbps, 2), publicIp);
        }
        catch
        {
            return (0, null);
        }
    }

    private static double CalculateStabilityScore(double packetLossPercent, double jitterMs)
    {
        // 100 base score
        double score = 100.0;

        // Packet loss heavily degrades stability
        score -= packetLossPercent * 2.0;

        // High jitter (>15ms) degrades stability
        if (jitterMs > 15)
        {
            score -= Math.Min(30, (jitterMs - 15) * 1.5);
        }

        return Math.Clamp(Math.Round(score, 1), 0.0, 100.0);
    }
}
