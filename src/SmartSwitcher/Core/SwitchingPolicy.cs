using SmartSwitcher.Core.Models;

namespace SmartSwitcher.Core;

public enum RoutingMode
{
    Auto,
    PreferWiFi,
    PreferEthernet
}

public class ScoredInterface
{
    public required NetworkInterfaceInfo Info { get; init; }
    public required ConnectionMetrics Metrics { get; init; }
    public required ConnectionScore Score { get; init; }

    public bool IsEmergencyDown =>
        !Metrics.IsInternetReachable ||
        Metrics.PacketLossPercent >= 50.0 ||
        Score.IsCriticalFailure;

    public bool IsHealthy =>
        Metrics.IsInternetReachable &&
        Metrics.PacketLossPercent < 20.0 &&
        Score.TotalScore >= 35;
}

public class SwitchDecision
{
    public bool ShouldSwitch { get; init; }
    public NetworkType TargetType { get; init; }
    public NetworkInterfaceInfo? TargetInterface { get; init; }
    public string Reason { get; init; } = string.Empty;
    public bool IsEmergencyFailover { get; init; }
    public bool IsCooldownActive { get; init; }
    public int ConsecutiveAdvantage { get; init; }
}

public class SwitchingPolicy
{
    // Anti-flapping & stabilization parameters (calm steady-state)
    public int ScoreDifferenceThreshold { get; set; } = 10;
    public int RequiredConsecutiveChecks { get; set; } = 2;
    public TimeSpan CooldownDuration { get; set; } = TimeSpan.FromSeconds(60);

    public RoutingMode Mode { get; set; } = RoutingMode.Auto;
    public NetworkType? CurrentPreferredType { get; private set; }
    public string? CurrentPreferredId { get; private set; }
    public DateTime LastSwitchTime { get; private set; } = DateTime.MinValue;

    private string? _pendingCandidateKey;
    private int _consecutiveAdvantageCount;

    public void SetActiveInterface(NetworkType type, DateTime? switchTime = null)
    {
        CurrentPreferredType = type;
        LastSwitchTime = switchTime ?? DateTime.Now;
        _pendingCandidateKey = null;
        _consecutiveAdvantageCount = 0;
    }

    public void SetActiveInterface(NetworkInterfaceInfo ifInfo, DateTime? switchTime = null)
    {
        CurrentPreferredType = ifInfo.Type;
        CurrentPreferredId = ifInfo.Id;
        LastSwitchTime = switchTime ?? DateTime.Now;
        _pendingCandidateKey = null;
        _consecutiveAdvantageCount = 0;
    }

    public void ResetCooldown()
    {
        LastSwitchTime = DateTime.MinValue;
    }

    /// <summary>
    /// Evaluates an arbitrary collection of scored interfaces (supports 2, 5, 10+ interfaces).
    /// Implements:
    ///   1. Emergency Zero-Delay Failover (when active connection loses internet/blackout).
    ///   2. Anti-Flapping Steady-State Hysteresis (cooldown, threshold difference, consecutive checks).
    ///   3. Manual Overrides (Prefer Wi-Fi, Prefer Ethernet).
    /// </summary>
    public SwitchDecision Evaluate(IReadOnlyList<ScoredInterface> candidates)
    {
        if (candidates.Count == 0)
        {
            return new SwitchDecision { ShouldSwitch = false, Reason = "No network adapters detected." };
        }

        // Single candidate available
        if (candidates.Count == 1)
        {
            var single = candidates[0];
            return CheckIfSwitchNeeded(single.Info, $"Only {single.Info.Name} is available.");
        }

        // Manual Overrides
        if (Mode == RoutingMode.PreferEthernet)
        {
            var eth = candidates.FirstOrDefault(c => c.Info.Type == NetworkType.Ethernet);
            if (eth != null)
                return CheckIfSwitchNeeded(eth.Info, "Manual override: Prefer Ethernet.");
        }
        else if (Mode == RoutingMode.PreferWiFi)
        {
            var wifi = candidates.FirstOrDefault(c => c.Info.Type == NetworkType.WiFi);
            if (wifi != null)
                return CheckIfSwitchNeeded(wifi.Info, "Manual override: Prefer Wi-Fi.");
        }

        // Identify current active connection
        var active = candidates.FirstOrDefault(c =>
            (CurrentPreferredId != null && c.Info.Id == CurrentPreferredId) ||
            (CurrentPreferredType != null && c.Info.Type == CurrentPreferredType));

        // If active is not yet initialized or disappeared from system
        if (active == null)
        {
            var bestInitial = candidates.OrderByDescending(c => c.Score.TotalScore).First();
            return new SwitchDecision
            {
                ShouldSwitch = true,
                TargetType = bestInitial.Info.Type,
                TargetInterface = bestInitial.Info,
                Reason = $"Initial selection: {bestInitial.Info.Name} picked with score {bestInitial.Score.TotalScore}."
            };
        }

        // =========================================================================
        // REGIME A: EMERGENCY ZERO-DELAY FAILOVER
        // If active connection is completely down, has >=50% packet loss, or score < 20
        // =========================================================================
        if (active.IsEmergencyDown)
        {
            // Find best alternative that actually has internet
            var emergencyBackup = candidates
                .Where(c => c.Info.Id != active.Info.Id && c.Metrics.IsInternetReachable)
                .OrderByDescending(c => c.Score.TotalScore)
                .FirstOrDefault();

            if (emergencyBackup != null)
            {
                // Reset consecutive counters
                _pendingCandidateKey = null;
                _consecutiveAdvantageCount = 0;

                return new SwitchDecision
                {
                    ShouldSwitch = true,
                    TargetType = emergencyBackup.Info.Type,
                    TargetInterface = emergencyBackup.Info,
                    IsEmergencyFailover = true,
                    Reason = $"[EMERGENCY FAILOVER] Active {active.Info.Name} lost internet (Loss: {active.Metrics.PacketLossPercent:F0}%, Score: {active.Score.TotalScore}). Switched immediately to {emergencyBackup.Info.Name} (Score: {emergencyBackup.Score.TotalScore})."
                };
            }
            else
            {
                return new SwitchDecision
                {
                    ShouldSwitch = false,
                    TargetType = active.Info.Type,
                    TargetInterface = active.Info,
                    Reason = $"Active {active.Info.Name} is down, but no other interfaces have working internet."
                };
            }
        }

        // =========================================================================
        // REGIME B: NORMAL STEADY-STATE HYSTERESIS (ANTI-FLAPPING)
        // Prevent ping-ponging across small score fluctuations
        // =========================================================================
        var timeSinceLastSwitch = DateTime.Now - LastSwitchTime;
        bool inCooldown = timeSinceLastSwitch < CooldownDuration;

        // Find the best alternative candidate
        var bestAlternative = candidates
            .Where(c => c.Info.Id != active.Info.Id && c.IsHealthy)
            .OrderByDescending(c => c.Score.TotalScore)
            .FirstOrDefault();

        if (bestAlternative == null)
        {
            return new SwitchDecision
            {
                ShouldSwitch = false,
                TargetType = active.Info.Type,
                TargetInterface = active.Info,
                Reason = $"{active.Info.Name} remains active (Score: {active.Score.TotalScore}). No healthy alternative found."
            };
        }

        int scoreDifference = bestAlternative.Score.TotalScore - active.Score.TotalScore;

        // Alternative must outperform active by at least ScoreDifferenceThreshold
        if (scoreDifference >= ScoreDifferenceThreshold)
        {
            string candidateKey = bestAlternative.Info.Id;
            if (_pendingCandidateKey == candidateKey)
            {
                _consecutiveAdvantageCount++;
            }
            else
            {
                _pendingCandidateKey = candidateKey;
                _consecutiveAdvantageCount = 1;
            }

            // Must sustain advantage across required consecutive checks
            if (_consecutiveAdvantageCount >= RequiredConsecutiveChecks)
            {
                if (inCooldown)
                {
                    int remainingSec = (int)(CooldownDuration - timeSinceLastSwitch).TotalSeconds;
                    return new SwitchDecision
                    {
                        ShouldSwitch = false,
                        TargetType = active.Info.Type,
                        TargetInterface = active.Info,
                        IsCooldownActive = true,
                        ConsecutiveAdvantage = _consecutiveAdvantageCount,
                        Reason = $"{bestAlternative.Info.Name} outperformed {active.Info.Name} (+{scoreDifference} pts, {_consecutiveAdvantageCount}/{RequiredConsecutiveChecks} checks), but cooldown is active ({remainingSec}s remaining to prevent flap)."
                    };
                }

                return new SwitchDecision
                {
                    ShouldSwitch = true,
                    TargetType = bestAlternative.Info.Type,
                    TargetInterface = bestAlternative.Info,
                    ConsecutiveAdvantage = _consecutiveAdvantageCount,
                    Reason = $"{bestAlternative.Info.Name} sustained +{scoreDifference} pt advantage over {active.Info.Name} ({bestAlternative.Score.TotalScore} vs {active.Score.TotalScore}) across {_consecutiveAdvantageCount} checks."
                };
            }
            else
            {
                return new SwitchDecision
                {
                    ShouldSwitch = false,
                    TargetType = active.Info.Type,
                    TargetInterface = active.Info,
                    ConsecutiveAdvantage = _consecutiveAdvantageCount,
                    Reason = $"{bestAlternative.Info.Name} shows advantage (+{scoreDifference} pts), confirming persistence ({_consecutiveAdvantageCount}/{RequiredConsecutiveChecks} checks)."
                };
            }
        }
        else
        {
            // Reset consecutive counter when advantage is lost or within hysteresis margin
            _pendingCandidateKey = null;
            _consecutiveAdvantageCount = 0;

            return new SwitchDecision
            {
                ShouldSwitch = false,
                TargetType = active.Info.Type,
                TargetInterface = active.Info,
                Reason = $"{active.Info.Name} remains active ({active.Score.TotalScore} vs {bestAlternative.Info.Name} {bestAlternative.Score.TotalScore}, diff: {scoreDifference} pts < threshold {ScoreDifferenceThreshold})."
            };
        }
    }

    /// <summary>
    /// Backward-compatible 2-adapter overload for CLI diagnostic mode and simple tests.
    /// </summary>
    public SwitchDecision Evaluate(
        ConnectionScore ethScore,
        ConnectionScore wifiScore,
        bool isEthAvailable,
        bool isWifiAvailable)
    {
        var candidates = new List<ScoredInterface>();

        if (isEthAvailable)
        {
            candidates.Add(new ScoredInterface
            {
                Info = new NetworkInterfaceInfo
                {
                    Id = "eth-0",
                    Name = "Ethernet",
                    Description = "Ethernet Adapter",
                    InterfaceIndex = 1,
                    Type = NetworkType.Ethernet,
                    LocalAddress = System.Net.IPAddress.Loopback
                },
                Metrics = new ConnectionMetrics
                {
                    IsInternetReachable = ethScore.IsUsable,
                    InternetLatencyMs = 20,
                    PacketLossPercent = ethScore.IsUsable ? 0 : 100
                },
                Score = ethScore
            });
        }

        if (isWifiAvailable)
        {
            candidates.Add(new ScoredInterface
            {
                Info = new NetworkInterfaceInfo
                {
                    Id = "wifi-0",
                    Name = "Wi-Fi",
                    Description = "Wireless Adapter",
                    InterfaceIndex = 2,
                    Type = NetworkType.WiFi,
                    LocalAddress = System.Net.IPAddress.Loopback
                },
                Metrics = new ConnectionMetrics
                {
                    IsInternetReachable = wifiScore.IsUsable,
                    InternetLatencyMs = 30,
                    PacketLossPercent = wifiScore.IsUsable ? 0 : 100
                },
                Score = wifiScore
            });
        }

        return Evaluate(candidates);
    }

    private SwitchDecision CheckIfSwitchNeeded(NetworkInterfaceInfo target, string reason)
    {
        if (CurrentPreferredType == target.Type && (CurrentPreferredId == null || CurrentPreferredId == target.Id))
        {
            return new SwitchDecision
            {
                ShouldSwitch = false,
                TargetType = target.Type,
                TargetInterface = target,
                Reason = reason
            };
        }

        return new SwitchDecision
        {
            ShouldSwitch = true,
            TargetType = target.Type,
            TargetInterface = target,
            Reason = reason
        };
    }
}
