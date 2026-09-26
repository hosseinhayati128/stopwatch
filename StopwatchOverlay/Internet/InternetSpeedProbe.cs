using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace StopwatchOverlay.Internet;

public enum InternetStatus
{
    Online,
    Slow,
    Offline
}

public sealed record InternetCheckResult(
    DateTime Timestamp,
    InternetStatus Status,
    long? PingMs,
    double? DownloadMbps,
    NetworkConnectionInfo NetworkInfo,
    string Notes)
{
    public string StatusBadge => Status switch
    {
        InternetStatus.Online => "🟢 Online",
        InternetStatus.Slow => "🟡 Slow",
        InternetStatus.Offline => "🔴 Offline",
        _ => "❓ Unknown"
    };

    public string PingDisplay => PingMs.HasValue ? $"{PingMs.Value} ms" : "-";
    public string SpeedDisplay => DownloadMbps.HasValue ? $"{DownloadMbps.Value:F1} Mbps" : "-";
}

public static class InternetSpeedProbe
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public static async Task<InternetCheckResult> CheckConnectionAsync(
        int sampleBytes = 1_000_000,
        string pingHost = "1.1.1.1",
        CancellationToken cancellationToken = default)
    {
        DateTime now = DateTime.Now;
        var netInfo = NetworkInfoDetector.GetCurrentNetworkInfo();

        if (!netInfo.IsConnected)
        {
            return new InternetCheckResult(
                Timestamp: now,
                Status: InternetStatus.Offline,
                PingMs: null,
                DownloadMbps: null,
                NetworkInfo: netInfo,
                Notes: "No network connection");
        }

        // 1. Measure Ping Latency
        long? pingMs = await MeasurePingAsync(pingHost, timeoutMs: 2500, cancellationToken);
        if (!pingMs.HasValue)
        {
            // Try fallback host (8.8.8.8)
            pingMs = await MeasurePingAsync("8.8.8.8", timeoutMs: 2500, cancellationToken);
        }

        if (!pingMs.HasValue)
        {
            return new InternetCheckResult(
                Timestamp: now,
                Status: InternetStatus.Offline,
                PingMs: null,
                DownloadMbps: null,
                NetworkInfo: netInfo,
                Notes: "Ping timed out (offline or blocked)");
        }

        // 2. Measure Download Speed
        double? downloadMbps = null;
        string notes = "Stable";

        if (sampleBytes > 0)
        {
            try
            {
                string testUrl = $"https://speed.cloudflare.com/__down?bytes={sampleBytes}";
                var sw = Stopwatch.StartNew();
                using var response = await HttpClient.GetAsync(testUrl, HttpCompletionOption.ResponseContentRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                var data = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                sw.Stop();

                double elapsedSec = Math.Max(0.05, sw.Elapsed.TotalSeconds);
                double totalBits = data.Length * 8.0;
                downloadMbps = Math.Round((totalBits / 1_000_000.0) / elapsedSec, 1);
            }
            catch (Exception ex)
            {
                notes = $"Speed test failed: {ex.Message}";
            }
        }

        // 3. Classify Status
        InternetStatus status = InternetStatus.Online;
        if (pingMs.Value > 250 || (downloadMbps.HasValue && downloadMbps.Value < 0.5))
        {
            status = InternetStatus.Slow;
            if (notes == "Stable")
            {
                notes = pingMs.Value > 250 ? $"High latency ({pingMs.Value} ms)" : "Low speed (<0.5 Mbps)";
            }
        }

        return new InternetCheckResult(
            Timestamp: now,
            Status: status,
            PingMs: pingMs,
            DownloadMbps: downloadMbps,
            NetworkInfo: netInfo,
            Notes: notes);
    }

    private static async Task<long?> MeasurePingAsync(string host, int timeoutMs, CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, timeoutMs);
            if (reply.Status == IPStatus.Success)
            {
                return reply.RoundtripTime;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }
}
