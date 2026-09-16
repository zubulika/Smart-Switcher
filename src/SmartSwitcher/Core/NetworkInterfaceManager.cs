using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using SmartSwitcher.Core.Models;

namespace SmartSwitcher.Core;

public class NetworkInterfaceManager
{
    private static readonly string[] VirtualKeywords =
    [
        "virtual", "vmware", "virtualbox", "hyper-v", "vethernet",
        "tap", "tun", "loopback", "pseudo", "wireguard", "tailscale",
        "wsl", "docker", "bluetooth", "npcap"
    ];

    /// <summary>
    /// Discovers operational physical Ethernet and Wi-Fi adapters.
    /// Virtual, tunnel, and loopback adapters are filtered out.
    /// </summary>
    public List<NetworkInterfaceInfo> GetCandidateInterfaces()
    {
        var results = new List<NetworkInterfaceInfo>();
        var interfaces = NetworkInterface.GetAllNetworkInterfaces();

        foreach (var ni in interfaces)
        {
            if (ni.OperationalStatus != OperationalStatus.Up)
                continue;

            if (ni.NetworkInterfaceType != NetworkInterfaceType.Ethernet &&
                ni.NetworkInterfaceType != NetworkInterfaceType.Wireless80211)
            {
                continue;
            }

            string desc = ni.Description.ToLowerInvariant();
            string name = ni.Name.ToLowerInvariant();

            // Filter out virtual/tunnel devices
            if (VirtualKeywords.Any(k => desc.Contains(k) || name.Contains(k)))
                continue;

            var ipProps = ni.GetIPProperties();
            var ipv4Props = ipProps.GetIPv4Properties();
            if (ipv4Props == null)
                continue;

            // Find IPv4 unicast address
            var unicast = ipProps.UnicastAddresses
                .FirstOrDefault(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork);

            if (unicast == null)
                continue;

            // Find default gateway
            var gateway = ipProps.GatewayAddresses
                .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork)?.Address;

            var netType = ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211
                ? NetworkType.WiFi
                : NetworkType.Ethernet;

            results.Add(new NetworkInterfaceInfo
            {
                Id = ni.Id,
                Name = ni.Name,
                Description = ni.Description,
                InterfaceIndex = ipv4Props.Index,
                Type = netType,
                LocalAddress = unicast.Address,
                GatewayAddress = gateway,
                Status = ni.OperationalStatus,
                CurrentMetric = 0, // Filled in by route manager when needed
                IsAutomaticMetric = true
            });
        }

        return results;
    }

    /// <summary>
    /// Convenience helper to find the primary Ethernet adapter.
    /// </summary>
    public NetworkInterfaceInfo? GetEthernetInterface(IEnumerable<NetworkInterfaceInfo>? candidates = null)
    {
        candidates ??= GetCandidateInterfaces();
        return candidates.FirstOrDefault(i => i.Type == NetworkType.Ethernet);
    }

    /// <summary>
    /// Convenience helper to find the primary Wi-Fi adapter.
    /// </summary>
    public NetworkInterfaceInfo? GetWiFiInterface(IEnumerable<NetworkInterfaceInfo>? candidates = null)
    {
        candidates ??= GetCandidateInterfaces();
        return candidates.FirstOrDefault(i => i.Type == NetworkType.WiFi);
    }
}
