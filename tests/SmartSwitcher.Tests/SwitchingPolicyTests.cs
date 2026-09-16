using System.Net;
using SmartSwitcher.Core;
using SmartSwitcher.Core.Models;
using Xunit;

namespace SmartSwitcher.Tests;

public class SwitchingPolicyTests
{
    private readonly SwitchingPolicy _policy = new()
    {
        ScoreDifferenceThreshold = 10,
        RequiredConsecutiveChecks = 2,
        CooldownDuration = TimeSpan.FromSeconds(60)
    };

    private static ScoredInterface CreateScoredInterface(
        string id,
        string name,
        NetworkType type,
        int totalScore,
        bool isReachable = true,
        double packetLoss = 0.0)
    {
        return new ScoredInterface
        {
            Info = new NetworkInterfaceInfo
            {
                Id = id,
                Name = name,
                Description = $"{name} Adapter",
                InterfaceIndex = id.GetHashCode() & 0x7FFF,
                Type = type,
                LocalAddress = IPAddress.Parse("192.168.1.10")
            },
            Metrics = new ConnectionMetrics
            {
                IsInternetReachable = isReachable,
                InternetLatencyMs = 25,
                PacketLossPercent = packetLoss,
                DownloadThroughputMbps = 50
            },
            Score = new ConnectionScore
            {
                TotalScore = isReachable ? totalScore : 0,
                IsUsable = isReachable && packetLoss < 99 && totalScore >= 20,
                Summary = $"Score {totalScore}"
            }
        };
    }

    [Fact]
    public void MultiInterface_SelectsBestCandidateAmongMultipleAdapters()
    {
        var eth1 = CreateScoredInterface("eth-1", "Ethernet 1", NetworkType.Ethernet, 65);
        var eth2 = CreateScoredInterface("eth-2", "Ethernet 2", NetworkType.Ethernet, 70);
        var wifi1 = CreateScoredInterface("wifi-1", "Wi-Fi Hotspot", NetworkType.WiFi, 92);
        var wifi2 = CreateScoredInterface("wifi-2", "Guest Wi-Fi", NetworkType.WiFi, 40);

        var decision = _policy.Evaluate(new[] { eth1, eth2, wifi1, wifi2 });

        Assert.True(decision.ShouldSwitch);
        Assert.Equal("wifi-1", decision.TargetInterface?.Id);
        Assert.Equal(NetworkType.WiFi, decision.TargetType);
    }

    [Fact]
    public void EmergencyBlackout_ZeroDelayFailover_WhenActiveLosesInternet()
    {
        var activeEth = CreateScoredInterface("eth-1", "Ethernet", NetworkType.Ethernet, 90);
        _policy.SetActiveInterface(activeEth.Info, DateTime.Now); // Activated right now (cooldown is technically active)

        // Ethernet suddenly suffers complete blackout (ISP cut / WAN down)
        var deadEth = CreateScoredInterface("eth-1", "Ethernet", NetworkType.Ethernet, 0, isReachable: false, packetLoss: 100.0);
        var healthyWifi = CreateScoredInterface("wifi-1", "Wi-Fi", NetworkType.WiFi, 80);

        // Even with cooldown active and only 1 check, EMERGENCY FAILOVER must trigger instantly!
        var decision = _policy.Evaluate(new[] { deadEth, healthyWifi });

        Assert.True(decision.ShouldSwitch);
        Assert.True(decision.IsEmergencyFailover);
        Assert.Equal("wifi-1", decision.TargetInterface?.Id);
        Assert.Contains("[EMERGENCY FAILOVER]", decision.Reason);
    }

    [Fact]
    public void AntiFlapping_SmallFluctuationsDoNotSwitch()
    {
        var activeEth = CreateScoredInterface("eth-1", "Ethernet", NetworkType.Ethernet, 75);
        _policy.SetActiveInterface(activeEth.Info);
        _policy.ResetCooldown();

        // Wi-Fi is slightly better (82 vs 75, diff = 7 < threshold 10)
        var slightlyBetterWifi = CreateScoredInterface("wifi-1", "Wi-Fi", NetworkType.WiFi, 82);

        var decision = _policy.Evaluate(new[] { activeEth, slightlyBetterWifi });

        // Anti-flapping: should NOT switch for minor fluctuations
        Assert.False(decision.ShouldSwitch);
        Assert.Equal(NetworkType.Ethernet, decision.TargetType);
        Assert.Contains("remains active", decision.Reason);
    }

    [Fact]
    public void AntiFlapping_RequiresConsecutiveAdvantage_BeforeSwitching()
    {
        var activeEth = CreateScoredInterface("eth-1", "Ethernet", NetworkType.Ethernet, 70);
        _policy.SetActiveInterface(activeEth.Info);
        _policy.ResetCooldown();

        // Wi-Fi is significantly better (88 vs 70, diff = 18 >= 10)
        var superiorWifi = CreateScoredInterface("wifi-1", "Wi-Fi", NetworkType.WiFi, 88);

        // Check 1: Advantage detected, but waiting for persistence
        var decision1 = _policy.Evaluate(new[] { activeEth, superiorWifi });
        Assert.False(decision1.ShouldSwitch);
        Assert.Equal(1, decision1.ConsecutiveAdvantage);

        // Check 2: Advantage sustained across consecutive checks -> Switch allowed!
        var decision2 = _policy.Evaluate(new[] { activeEth, superiorWifi });
        Assert.True(decision2.ShouldSwitch);
        Assert.Equal(NetworkType.WiFi, decision2.TargetType);
        Assert.Contains("sustained", decision2.Reason);
    }

    [Fact]
    public void AntiFlapping_CooldownPreventsRapidPingPong()
    {
        var activeEth = CreateScoredInterface("eth-1", "Ethernet", NetworkType.Ethernet, 70);
        _policy.SetActiveInterface(activeEth.Info, DateTime.Now); // Switch just happened 0 seconds ago

        var superiorWifi = CreateScoredInterface("wifi-1", "Wi-Fi", NetworkType.WiFi, 92);

        // Check 1 & Check 2
        _policy.Evaluate(new[] { activeEth, superiorWifi });
        var decision2 = _policy.Evaluate(new[] { activeEth, superiorWifi });

        // Cooldown blocks steady-state switch to protect established TCP streams
        Assert.False(decision2.ShouldSwitch);
        Assert.True(decision2.IsCooldownActive);
        Assert.Contains("cooldown is active", decision2.Reason);
    }

    [Fact]
    public void ManualOverrides_PreferWiFi_and_PreferEthernet()
    {
        var weakEth = CreateScoredInterface("eth-1", "Ethernet", NetworkType.Ethernet, 45);
        var strongWifi = CreateScoredInterface("wifi-1", "Wi-Fi", NetworkType.WiFi, 95);

        // Prefer Ethernet manual override
        _policy.Mode = RoutingMode.PreferEthernet;
        var decisionEth = _policy.Evaluate(new[] { weakEth, strongWifi });
        Assert.True(decisionEth.ShouldSwitch);
        Assert.Equal(NetworkType.Ethernet, decisionEth.TargetType);
        Assert.Contains("Manual override", decisionEth.Reason);

        // Prefer Wi-Fi manual override
        _policy.Mode = RoutingMode.PreferWiFi;
        _policy.SetActiveInterface(weakEth.Info);
        var decisionWifi = _policy.Evaluate(new[] { weakEth, strongWifi });
        Assert.True(decisionWifi.ShouldSwitch);
        Assert.Equal(NetworkType.WiFi, decisionWifi.TargetType);
        Assert.Contains("Manual override", decisionWifi.Reason);
    }
}
