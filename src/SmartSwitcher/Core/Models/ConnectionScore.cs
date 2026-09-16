namespace SmartSwitcher.Core.Models;

public class ConnectionScore
{
    public int TotalScore { get; init; }
    public double LatencyPoints { get; init; }
    public double PacketLossPoints { get; init; }
    public double ThroughputPoints { get; init; }
    public double GatewayStabilityPoints { get; init; }
    public bool IsUsable { get; init; }
    public bool IsCriticalFailure => !IsUsable || TotalScore < 20;
    public string Summary { get; init; } = string.Empty;

    public static ConnectionScore Zero(string reason = "Unreachable") => new()
    {
        TotalScore = 0,
        IsUsable = false,
        Summary = reason
    };
}
