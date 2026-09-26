using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StopwatchOverlay.ActivityWatch;

public sealed class ActivityWatchClient
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _baseUrl;

    public ActivityWatchClient(string? baseUrl = null)
    {
        _baseUrl = NormalizeBaseUrl(baseUrl);
    }

    public static string NormalizeBaseUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "http://localhost:5600";

        string trimmed = url.Trim();
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "http://" + trimmed;
        }

        return trimmed.TrimEnd('/');
    }

    public async Task<(bool Success, string Version, string? ErrorMessage)> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await HttpClient.GetAsync($"{_baseUrl}/api/0/info", ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return (false, "", $"Server returned HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
            }

            var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var info = JsonSerializer.Deserialize<AwServerInfo>(content, JsonOptions);
            return (true, info?.Version ?? "Connected", null);
        }
        catch (HttpRequestException ex)
        {
            return (false, "", $"Cannot connect to ActivityWatch: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return (false, "", "Connection timed out. Ensure ActivityWatch is running.");
        }
        catch (Exception ex)
        {
            return (false, "", ex.Message);
        }
    }

    public async Task<Dictionary<string, AwBucketInfo>> GetBucketsAsync(CancellationToken ct = default)
    {
        try
        {
            // Note: Trailing slash is required by aw-server to prevent 307 redirects
            using var response = await HttpClient.GetAsync($"{_baseUrl}/api/0/buckets/", ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return [];

            var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<Dictionary<string, AwBucketInfo>>(content, JsonOptions)
                   ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<List<AwEvent>> GetEventsAsync(
        string bucketId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        int limit = 50000,
        CancellationToken ct = default)
    {
        try
        {
            string startIso = Uri.EscapeDataString(startUtc.ToString("o"));
            string endIso = Uri.EscapeDataString(endUtc.ToString("o"));
            string url = $"{_baseUrl}/api/0/buckets/{Uri.EscapeDataString(bucketId)}/events?start={startIso}&end={endIso}&limit={limit}";

            using var response = await HttpClient.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return [];

            var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<AwEvent>>(content, JsonOptions)
                   ?? [];
        }
        catch
        {
            return [];
        }
    }
}
