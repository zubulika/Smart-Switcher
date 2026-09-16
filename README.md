# Smart Switcher (Windows 11)

A lightweight Windows 11 system utility that keeps both Ethernet and Wi-Fi active simultaneously, independently evaluates the real-world connection quality of each interface, and dynamically sets the preferred default Internet route without disconnecting or disabling adapters.

---

## Key Features

1. **True Interface-Bound Probing (Zero Route Leakage):**
   - Does not make generic requests through the default route.
   - Binds directly to the adapter's local IP and applies Winsock `IP_UNICAST_IF` (interface index) to ensure probe packets strictly exit through that specific adapter.
2. **Mobile Carrier Hotspot Compatibility:**
   - Probes using TCP connect (ports 53/443) and HTTP HEAD to avoid carrier-grade NAT dropping ICMP echo requests.
3. **Controlled Throughput Measurement:**
   - Measures download throughput using a bounded 1MB payload from Cloudflare's CDN, preserving mobile hotspot data.
4. **Multi-Factor Scoring Engine (0 - 100):**
   - Evaluates:
     - Internet Reachability (Gatekeeper: unreachable = 0)
     - Internet Latency (0 to 35 pts)
     - Packet Loss & Jitter (0 to 25 pts)
     - Download Throughput (0 to 25 pts)
     - Gateway Latency & Stability (0 to 15 pts)
5. **Hysteresis & Flap Prevention:**
   - **Threshold:** The alternate connection must beat the active connection by at least 8 points.
   - **Persistence:** Advantage must persist across at least 2 consecutive checks.
   - **Cooldown:** 45-second stabilization cooldown between switches.
   - **Emergency Failover:** If the active connection loses Internet completely, it fails over immediately without delay.
6. **Safe Windows Route Management:**
   - Modifies IPv4 interface metrics using `store=active` (in-memory only; non-destructive).
   - Preferred connection metric: `15`. Alternate connection metric: `45`.
   - On application exit, automatically restores Windows default automatic metrics.
7. **Modern Windows 11 Graphical UI Dashboard:**
   - Launches directly as a full desktop GUI dashboard with modern Fluent dark theme styling (`#0f172a` slate).
   - **Hero Active Route Banner:** Visually displays which adapter is carrying all system internet traffic.
   - **Live Connection Comparison Cards:** Real-time latency, packet loss %, throughput Mbps, and 0–100 quality score meters for both Ethernet and Wi-Fi.
   - **Interactive Mode Selector:** Switch between `[ ⚡ Auto ]`, `[ 📶 Prefer Wi-Fi ]`, and `[ 🔌 Prefer Ethernet ]` with a single click.
   - **Manual Speed & Latency Probe:** Instant on-demand measurement button.
   - **Live Decision & Audit Stream:** Real-time color-coded decision log feed.
   - **System Tray Minimization:** Minimize to tray seamlessly, with single/double-click restore and dynamic tray icon (Blue Wi-Fi badge / Green Monitor badge).
8. **Local File Logging:**
   - Logs decision rationale to `%LocalAppData%\SmartSwitcher\logs\smart_switcher.log`.

---

## How to Run

### 1. Diagnostic CLI Mode (No Admin Required)
Runs a single comprehensive check, measures both adapters, scores them, reports the winner, and exits without changing any routes:

```powershell
dotnet run --project src/SmartSwitcher -- --diagnostic
```

Or run the compiled binary:
```powershell
.\src\SmartSwitcher\bin\Debug\net8.0-windows\SmartSwitcher.exe --diagnostic
```

Example output:
```text
=================================================
        Smart Switcher - Diagnostic Mode         
=================================================
Discovering interfaces and probing connections...
Note: Routing table will NOT be modified.

Probing Ethernet (Ethernet - Realtek PCIe GbE Family Controller)... Done.
Probing Wi-Fi (WiFi - Realtek 8811CU Wireless LAN 802.11ac USB NIC)... Done.

Ethernet
  Internet:    Connected
  Gateway:     0 ms
  Latency:     17 ms
  Packet loss: 0%
  Download:    25 Mbps
  Score:       91

Wi-Fi
  Internet:    Connected
  Gateway:     24 ms
  Latency:     56 ms
  Packet loss: 0%
  Download:    7 Mbps
  Score:       74

Selected: Ethernet
Reason:   Ethernet remains active (Eth: 91, WiFi: 74, diff: -17 pts < threshold 8).
=================================================
```

### 2. System Tray Mode
To run in background tray mode with automatic route metric switching, run the executable as Administrator (required by Windows to adjust route metrics):

```powershell
Start-Process ".\src\SmartSwitcher\bin\Debug\net8.0-windows\SmartSwitcher.exe" -Verb RunAs
```

---

## Project Structure

```
Smart-Switcher/
├── SmartSwitcher.sln
├── src/
│   └── SmartSwitcher/
│       ├── Core/
│       │   ├── Models/
│       │   │   ├── NetworkInterfaceInfo.cs
│       │   │   ├── ConnectionMetrics.cs
│       │   │   └── ConnectionScore.cs
│       │   ├── NetworkInterfaceManager.cs
│       │   ├── ConnectionProbe.cs
│       │   ├── ConnectionScorer.cs
│       │   └── SwitchingPolicy.cs
│       ├── Windows/
│       │   ├── RouteManager.cs
│       │   └── StartupManager.cs
│       ├── Logging/
│       │   └── AppLogger.cs
│       ├── UI/
│       │   ├── TrayApplicationContext.cs
│       │   └── StatusForm.cs
│       ├── app.manifest
│       └── Program.cs
└── tests/
    └── SmartSwitcher.Tests/
        ├── ConnectionScorerTests.cs
        └── SwitchingPolicyTests.cs
```

---

## Running Unit Tests

```powershell
dotnet test
```
