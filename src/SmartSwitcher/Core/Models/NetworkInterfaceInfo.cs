using System.Net;
using System.Net.NetworkInformation;

namespace SmartSwitcher.Core.Models;

public enum NetworkType
{
    Ethernet,
    WiFi,
    Other
}

public class NetworkInterfaceInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required int InterfaceIndex { get; init; }
    public required NetworkType Type { get; init; }
    public required IPAddress LocalAddress { get; init; }
    public IPAddress? GatewayAddress { get; init; }
    public OperationalStatus Status { get; init; }
    public int CurrentMetric { get; set; }
    public bool IsAutomaticMetric { get; set; }

    public override string ToString() =>
        $"{Name} [{Type}] (Index={InterfaceIndex}, IP={LocalAddress}, Gateway={GatewayAddress})";
}
