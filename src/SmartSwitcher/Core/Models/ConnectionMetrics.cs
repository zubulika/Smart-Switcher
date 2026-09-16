namespace SmartSwitcher.Core.Models;

public class ConnectionMetrics
{
    public bool IsInternetReachable { get; init; }
    public double GatewayLatencyMs { get; init; } = -1;
    public double InternetLatencyMs { get; init; } = -1;
    public double PacketLossPercent { get; init; } = 100.0;
    public double DownloadThroughputMbps { get; init; } = 0.0;
    public double StabilityScore { get; init; } = 0.0;
    public string? PublicIpAddress { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string? ErrorMessage { get; init; }

    public static ConnectionMetrics Unreachable(string? error = null) => new()
    {
        IsInternetReachable = false,
        ErrorMessage = error,
        PacketLossPercent = 100.0,
        InternetLatencyMs = 9999,
        GatewayLatencyMs = 9999,
        DownloadThroughputMbps = 0,
        StabilityScore = 0
    };
}
