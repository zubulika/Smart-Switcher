using SmartSwitcher.Core.Models;

namespace SmartSwitcher.Core;

public class ConnectionScorer
{
    /// <summary>
    /// Computes a normalized score from 0 to 100 for the measured connection.
    /// Factors: Internet availability (critical), Packet loss (very important),
    /// Latency (important), Throughput (important), Stability & Gateway (important).
    /// </summary>
    public ConnectionScore CalculateScore(ConnectionMetrics metrics)
    {
        if (!metrics.IsInternetReachable || metrics.PacketLossPercent >= 99.0)
        {
            return ConnectionScore.Zero("Internet unreachable or 100% packet loss.");
        }

        // 1. Latency Points (0 - 35)
        // 10ms or less gets full 35 pts. 200ms or higher gets ~0 pts.
        double latencyPts;
        if (metrics.InternetLatencyMs <= 10)
        {
            latencyPts = 35.0;
        }
        else if (metrics.InternetLatencyMs >= 220)
        {
            latencyPts = 0.0;
        }
        else
        {
            latencyPts = 35.0 * (1.0 - ((metrics.InternetLatencyMs - 10) / 210.0));
        }

        // 2. Packet Loss Points (0 - 25)
        // Steep cubic drop-off for packet loss: in real networking, even 5-10% loss degrades UX significantly
        double healthRatio = Math.Clamp(1.0 - (metrics.PacketLossPercent / 100.0), 0.0, 1.0);
        double packetLossPts = 25.0 * Math.Pow(healthRatio, 3);

        // 3. Download Throughput Points (0 - 25)
        // Logarithmic scale up to 100 Mbps (10 Mbps ~ 12.5 pts, 50 Mbps ~ 21 pts, 100+ Mbps = 25 pts)
        double throughputPts;
        if (metrics.DownloadThroughputMbps <= 0.5)
        {
            throughputPts = 0.0;
        }
        else
        {
            double logVal = Math.Log10(metrics.DownloadThroughputMbps);
            throughputPts = Math.Clamp(logVal / 2.0 * 25.0, 0.0, 25.0);
        }

        // 4. Gateway & Stability Points (0 - 15)
        double gatewayPts = 0.0;
        if (metrics.GatewayLatencyMs >= 0)
        {
            // Clean local LAN gateway under 5ms gets 5 pts
            gatewayPts = metrics.GatewayLatencyMs <= 5 ? 5.0 : Math.Max(0, 5.0 - (metrics.GatewayLatencyMs - 5) * 0.2);
        }

        double stabilityPts = 10.0 * (metrics.StabilityScore / 100.0);
        double gatewayStabilityPts = Math.Clamp(gatewayPts + stabilityPts, 0.0, 15.0);

        // Calculate total
        double total = latencyPts + packetLossPts + throughputPts + gatewayStabilityPts;
        int roundedTotal = (int)Math.Round(Math.Clamp(total, 0.0, 100.0));

        string summary = $"Lat: {latencyPts:F1}/35, Loss: {packetLossPts:F1}/25, Speed: {throughputPts:F1}/25, Stab: {gatewayStabilityPts:F1}/15";

        return new ConnectionScore
        {
            TotalScore = roundedTotal,
            LatencyPoints = latencyPts,
            PacketLossPoints = packetLossPts,
            ThroughputPoints = throughputPts,
            GatewayStabilityPoints = gatewayStabilityPts,
            IsUsable = roundedTotal > 10,
            Summary = summary
        };
    }
}
