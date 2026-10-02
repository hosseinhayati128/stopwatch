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
        Timeout = TimeSpan.FromSeconds(25),
        DefaultRequestHeaders = { { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) StopwatchOverlay/1.0" } }
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

        // 1. Measure Ping Latency (3500ms timeout for cellular stability)
        long? pingMs = await MeasurePingAsync(pingHost, timeoutMs: 3500, cancellationToken);
        if (!pingMs.HasValue)
        {
            // Try fallback host (8.8.8.8)
            pingMs = await MeasurePingAsync("8.8.8.8", timeoutMs: 3500, cancellationToken);
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

        // 2. Measure Download Speed with streaming adaptive throughput
        double? downloadMbps = null;
        string notes = "Stable";

        if (sampleBytes > 0)
        {
            var (speed, error) = await MeasureDownloadSpeedAsync(sampleBytes, cancellationToken);
            downloadMbps = speed;
            if (error != null && !downloadMbps.HasValue)
            {
                notes = $"Speed test failed: {error}";
            }
        }

        // 3. Classify Status
        InternetStatus status = InternetStatus.Online;
        if (pingMs.Value > 250 || (downloadMbps.HasValue && downloadMbps.Value < 0.5))
        {
            status = InternetStatus.Slow;
            if (notes == "Stable")
            {
                if (pingMs.Value > 250 && downloadMbps.HasValue && downloadMbps.Value < 0.5)
                {
                    notes = $"High latency ({pingMs.Value} ms) & low speed ({downloadMbps.Value:F1} Mbps)";
                }
                else if (pingMs.Value > 250)
                {
                    notes = $"High latency ({pingMs.Value} ms)";
                }
                else
                {
                    notes = $"Low speed ({downloadMbps!.Value:F1} Mbps)";
                }
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

    private static async Task<(double? mbps, string? error)> MeasureDownloadSpeedAsync(
        int sampleBytes,
        CancellationToken cancellationToken)
    {
        // Try HTTPS first, then plain HTTP Cloudflare (if TLS/SNI blocked), then CDN fallback
        string[] speedEndpoints = [
            $"https://speed.cloudflare.com/__down?bytes={sampleBytes}",
            $"http://speed.cloudflare.com/__down?bytes={sampleBytes}",
            "https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css"
        ];

        string? lastError = null;
        const int speedTestBudgetSeconds = 10;

        foreach (var url in speedEndpoints)
        {
            long totalBytesRead = 0;
            var sw = Stopwatch.StartNew();

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(speedTestBudgetSeconds));

                using var response = await HttpClient.GetAsync(
                    url,
                    HttpCompletionOption.ResponseHeadersRead,
                    cts.Token).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    continue;

                await using var stream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
                byte[] buffer = new byte[16384];

                while (totalBytesRead < sampleBytes && sw.Elapsed < TimeSpan.FromSeconds(speedTestBudgetSeconds))
                {
                    int read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cts.Token).ConfigureAwait(false);
                    if (read == 0) break;
                    totalBytesRead += read;
                }

                sw.Stop();

                // If at least 20 KB was read, calculate throughput accurately from bytes received
                if (totalBytesRead >= 20_000)
                {
                    double elapsedSec = Math.Max(0.05, sw.Elapsed.TotalSeconds);
                    double totalBits = totalBytesRead * 8.0;
                    double mbps = Math.Round((totalBits / 1_000_000.0) / elapsedSec, 1);
                    return (mbps, null);
                }
            }
            catch (OperationCanceledException) when (totalBytesRead >= 20_000)
            {
                // Budget timeout reached, but enough data was downloaded to calculate real speed!
                sw.Stop();
                double elapsedSec = Math.Max(0.05, sw.Elapsed.TotalSeconds);
                double totalBits = totalBytesRead * 8.0;
                double mbps = Math.Round((totalBits / 1_000_000.0) / elapsedSec, 1);
                return (mbps, null);
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }
        }

        return (null, lastError);
    }

    private static async Task<long?> MeasurePingAsync(string host, int timeoutMs, CancellationToken cancellationToken)
    {
        // 1. Try ICMP Ping
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, Math.Min(timeoutMs, 2000));
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
            "http://connectivitycheck.gstatic.com/generate_204",
            "http://www.google.com/generate_204",
            "https://1.1.1.1/generate_204",
            "http://detectportal.firefox.com/success.txt"
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
