using SmartSwitcher.Core;
using SmartSwitcher.Core.Models;
using Xunit;

namespace SmartSwitcher.Tests;

public class ConnectionScorerTests
{
    private readonly ConnectionScorer _scorer = new();

    [Fact]
    public void UnreachableConnection_YieldsZeroScore()
    {
        var metrics = ConnectionMetrics.Unreachable("DNS failure");
        var score = _scorer.CalculateScore(metrics);

        Assert.Equal(0, score.TotalScore);
        Assert.False(score.IsUsable);
    }

    [Fact]
    public void HighPacketLoss_DrasticallyReducesScore()
    {
        var normalMetrics = new ConnectionMetrics
        {
            IsInternetReachable = true,
            InternetLatencyMs = 25,
            PacketLossPercent = 0,
            DownloadThroughputMbps = 50,
            GatewayLatencyMs = 2,
            StabilityScore = 95
        };

        var lossyMetrics = new ConnectionMetrics
        {
            IsInternetReachable = true,
            InternetLatencyMs = 25,
            PacketLossPercent = 30, // 30% loss
            DownloadThroughputMbps = 50,
            GatewayLatencyMs = 2,
            StabilityScore = 40
        };

        var normalScore = _scorer.CalculateScore(normalMetrics);
        var lossyScore = _scorer.CalculateScore(lossyMetrics);

        Assert.True(normalScore.TotalScore > 75);
        Assert.True(lossyScore.TotalScore < normalScore.TotalScore - 20);
    }

    [Fact]
    public void FasterThroughputAndLowerLatency_YieldsHigherScore()
    {
        var fastMetrics = new ConnectionMetrics
        {
            IsInternetReachable = true,
            InternetLatencyMs = 15,
            PacketLossPercent = 0,
            DownloadThroughputMbps = 120,
            GatewayLatencyMs = 1,
            StabilityScore = 100
        };

        var slowMetrics = new ConnectionMetrics
        {
            IsInternetReachable = true,
            InternetLatencyMs = 110,
            PacketLossPercent = 0,
            DownloadThroughputMbps = 5,
            GatewayLatencyMs = 15,
            StabilityScore = 75
        };

        var fastScore = _scorer.CalculateScore(fastMetrics);
        var slowScore = _scorer.CalculateScore(slowMetrics);

        Assert.True(fastScore.TotalScore > slowScore.TotalScore);
        Assert.True(fastScore.TotalScore >= 90);
    }
}
