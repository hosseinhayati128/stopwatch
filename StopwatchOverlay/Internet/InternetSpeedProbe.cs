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
    private static readonly HttpClient HttpClient = new(new SocketsHttpHandler
    {
        SslOptions = new System.Net.Security.SslClientAuthenticationOptions
        {
            CertificateRevocationCheckMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck
        },
        ConnectTimeout = TimeSpan.FromSeconds(5)
    })
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
        // 1. Try ICMP Ping
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, Math.Min(timeoutMs, 1500));
            // Real internet ICMP pings across public networks take at least 4ms.
            // When TUN adapters (v2rayN, Clash, TAP, etc.) or local loopbacks intercept ICMP,
            // they reply locally with 0ms or <2ms. In that case, ignore the fake ICMP response and measure HTTP latency.
            if (reply.Status == IPStatus.Success && reply.RoundtripTime >= 4)
            {
                return reply.RoundtripTime;
            }
        }
        catch
        {
            // ICMP failed or blocked, proceed to HTTP probe
        }

        // 2. Measure real HTTP roundtrip latency (works reliably with VPNs, proxies, and TUN adapters)
        string[] latencyEndpoints = [
            "http://cp.cloudflare.com/generate_204",
            "http://www.google.com/generate_204",
            "http://connectivitycheck.gstatic.com/generate_204"
        ];

        foreach (var endpoint in latencyEndpoints)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(timeoutMs);

                using var response = await HttpClient.GetAsync(
                    endpoint,
                    HttpCompletionOption.ResponseHeadersRead,
                    cts.Token).ConfigureAwait(false);

                sw.Stop();
                if (response.IsSuccessStatusCode)
                {
                    return Math.Max(1, sw.ElapsedMilliseconds);
                }
            }
            catch
            {
                // Try next endpoint
            }
        }

        return null;
    }
}
