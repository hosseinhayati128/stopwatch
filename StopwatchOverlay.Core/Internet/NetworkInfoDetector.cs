using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;

namespace StopwatchOverlay.Internet;

public sealed record NetworkConnectionInfo(
    bool IsConnected,
    string ConnectionType,
    string NetworkName,
    int? SignalPercent,
    string AdapterDescription,
    string DisplayText);

public static class NetworkInfoDetector
{
    public static NetworkConnectionInfo GetCurrentNetworkInfo()
    {
        try
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                return new NetworkConnectionInfo(
                    IsConnected: false,
                    ConnectionType: "Offline",
                    NetworkName: "Disconnected",
                    SignalPercent: null,
                    AdapterDescription: string.Empty,
                    DisplayText: "❌ Disconnected");
            }

            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                             ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                             ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .ToList();

            if (interfaces.Count == 0)
            {
                return new NetworkConnectionInfo(
                    IsConnected: false,
                    ConnectionType: "Offline",
                    NetworkName: "Disconnected",
                    SignalPercent: null,
                    AdapterDescription: string.Empty,
                    DisplayText: "❌ Disconnected");
            }

            // Check if active interface is Wi-Fi
            var wifiInterface = interfaces.FirstOrDefault(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);
            if (wifiInterface != null)
            {
                var wlanInfo = TryGetWlanInfo();
                if (wlanInfo != null && !string.IsNullOrWhiteSpace(wlanInfo.Value.Ssid))
                {
                    string signalStr = wlanInfo.Value.Signal.HasValue ? $" ({wlanInfo.Value.Signal.Value}%)" : string.Empty;
                    string display = $"📶 {wlanInfo.Value.Ssid}{signalStr}";
                    return new NetworkConnectionInfo(
                        IsConnected: true,
                        ConnectionType: "Wi-Fi",
                        NetworkName: wlanInfo.Value.Ssid,
                        SignalPercent: wlanInfo.Value.Signal,
                        AdapterDescription: wifiInterface.Description,
                        DisplayText: display);
                }

                return new NetworkConnectionInfo(
                    IsConnected: true,
                    ConnectionType: "Wi-Fi",
                    NetworkName: wifiInterface.Name,
                    SignalPercent: null,
                    AdapterDescription: wifiInterface.Description,
                    DisplayText: $"📶 Wi-Fi ({wifiInterface.Name})");
            }

            // Ethernet or other wired/cellular connection
            var primary = interfaces.FirstOrDefault(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                          ?? interfaces.First();

            string connType = primary.NetworkInterfaceType switch
            {
                NetworkInterfaceType.Ethernet => "Ethernet",
                NetworkInterfaceType.Ppp => "Cellular/Dialup",
                _ => primary.NetworkInterfaceType.ToString()
            };

            string icon = connType == "Ethernet" ? "🔌" : "🌐";
            string netName = string.IsNullOrWhiteSpace(primary.Name) ? connType : primary.Name;
            string displayText = $"{icon} {netName}";

            return new NetworkConnectionInfo(
                IsConnected: true,
                ConnectionType: connType,
                NetworkName: netName,
                SignalPercent: null,
                AdapterDescription: primary.Description,
                DisplayText: displayText);
        }
        catch
        {
            return new NetworkConnectionInfo(
                IsConnected: false,
                ConnectionType: "Unknown",
                NetworkName: "Unknown",
                SignalPercent: null,
                AdapterDescription: string.Empty,
                DisplayText: "❓ Unknown Connection");
        }
    }

    public static (string Ssid, int? Signal)? TryGetWlanInfo()
    {
        try
        {
            var psi = new ProcessStartInfo("netsh", "wlan show interfaces")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(1500);

            return ParseNetshWlanOutput(output);
        }
        catch
        {
            return null;
        }
    }

    public static (string Ssid, int? Signal)? ParseNetshWlanOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return null;

        string? ssid = null;
        int? signal = null;
        bool connected = false;

        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split(':', 2);
            if (parts.Length != 2) continue;

            string key = parts[0].Trim().ToLowerInvariant();
            string val = parts[1].Trim();

            if (key == "state" && val.Equals("connected", StringComparison.OrdinalIgnoreCase))
            {
                connected = true;
            }
            else if (key == "ssid")
            {
                ssid = val;
            }
            else if (key == "signal")
            {
                string numStr = val.Replace("%", "").Trim();
                if (int.TryParse(numStr, out int s))
                {
                    signal = s;
                }
            }
        }

        if (connected && !string.IsNullOrWhiteSpace(ssid))
        {
            return (ssid, signal);
        }

        return null;
    }
}
