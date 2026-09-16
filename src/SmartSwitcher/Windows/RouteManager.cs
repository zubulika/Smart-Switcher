using System.Diagnostics;
using System.Security.Principal;
using SmartSwitcher.Core.Models;
using SmartSwitcher.Logging;

namespace SmartSwitcher.Windows;

public class RouteManager : IDisposable
{
    private const int PreferredMetric = 15;
    private const int FallbackMetric = 45;

    private readonly List<int> _managedInterfaceIndices = new();
    private bool _isDisposed;

    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Adjusts the Windows IPv4 interface metric so the specified target interface becomes
    /// the preferred default internet route (Metric 15), while all other candidates receive
    /// a lower priority fallback metric (Metric 45+), without disconnecting or disabling any adapter.
    /// Uses store=active so changes are in-memory and revert on reboot.
    /// </summary>
    public bool SetPreferredInterface(
        NetworkInterfaceInfo preferred,
        IEnumerable<NetworkInterfaceInfo>? others = null)
    {
        if (!IsAdministrator())
        {
            AppLogger.Warn("RouteManager: Administrator privileges required to change route metrics.");
            return false;
        }

        bool success = true;

        // Set preferred interface to highest priority (metric 15)
        success &= SetInterfaceMetric(preferred.InterfaceIndex, PreferredMetric);
        lock (_managedInterfaceIndices)
        {
            if (!_managedInterfaceIndices.Contains(preferred.InterfaceIndex))
                _managedInterfaceIndices.Add(preferred.InterfaceIndex);
        }

        // Set all other alternate adapters to lower priority (metric 45)
        if (others != null)
        {
            int fallbackMetric = FallbackMetric;
            foreach (var alt in others)
            {
                if (alt.InterfaceIndex == preferred.InterfaceIndex) continue;

                success &= SetInterfaceMetric(alt.InterfaceIndex, fallbackMetric);
                lock (_managedInterfaceIndices)
                {
                    if (!_managedInterfaceIndices.Contains(alt.InterfaceIndex))
                        _managedInterfaceIndices.Add(alt.InterfaceIndex);
                }
                fallbackMetric += 2; // Incremental fallback ordering
            }
        }

        if (success)
        {
            AppLogger.Info($"RouteManager: Preferred route switched to {preferred.Name} (Metric {PreferredMetric}).");
        }

        return success;
    }

    public bool SetPreferredInterface(NetworkInterfaceInfo preferred, NetworkInterfaceInfo? secondary)
    {
        var others = secondary != null ? new[] { secondary } : null;
        return SetPreferredInterface(preferred, others);
    }

    /// <summary>
    /// Restores automatic metric management on all previously modified interfaces.
    /// </summary>
    public void RestoreDefaultMetrics()
    {
        if (!IsAdministrator()) return;

        lock (_managedInterfaceIndices)
        {
            foreach (int ifIndex in _managedInterfaceIndices)
            {
                RestoreAutomaticMetric(ifIndex);
            }
            _managedInterfaceIndices.Clear();
        }
        AppLogger.Info("RouteManager: Reverted interface metrics to Windows Automatic.");
    }

    private static bool SetInterfaceMetric(int ifIndex, int metric)
    {
        try
        {
            // netsh interface ipv4 set interface <idx> metric=<val> store=active
            var psi = new ProcessStartInfo
            {
                FileName = "netsh.exe",
                Arguments = $"interface ipv4 set interface {ifIndex} metric={metric} store=active",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            process.WaitForExit(3000);
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Failed to set metric {metric} on interface {ifIndex}: {ex.Message}");
            return false;
        }
    }

    private static void RestoreAutomaticMetric(int ifIndex)
    {
        try
        {
            // Use PowerShell Set-NetIPInterface to re-enable AutomaticMetric
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"Set-NetIPInterface -InterfaceIndex {ifIndex} -AutomaticMetric Enabled -PolicyStore ActiveStore -ErrorAction SilentlyContinue\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            process?.WaitForExit(3000);
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Failed to restore automatic metric on interface {ifIndex}: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        RestoreDefaultMetrics();
    }
}
