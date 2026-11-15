using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace StopwatchOverlay.ActivityWatch;

public sealed class ActivityWatchClient
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions StrictEventJsonOptions = new(JsonOptions)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { type =>
            {
                if (type.Type != typeof(AwEvent)) return;
                foreach (var property in type.Properties)
                    if (property.Name is "timestamp" or "duration" or "data") property.IsRequired = true;
            } }
        }
    };

    private readonly string _baseUrl;
    private readonly HttpClient _httpClient;

    public ActivityWatchClient(string? baseUrl = null, HttpClient? httpClient = null)
    {
        _baseUrl = NormalizeBaseUrl(baseUrl);
        _httpClient = httpClient ?? SharedHttpClient;
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
            using var response = await _httpClient.GetAsync($"{_baseUrl}/api/0/info", ct).ConfigureAwait(false);
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

    public async Task<Dictionary<string, AwBucketInfo>> GetBucketsAsync(CancellationToken ct = default, bool throwOnError = false)
    {
        try
        {
            // Note: Trailing slash is required by aw-server to prevent 307 redirects
            using var response = await _httpClient.GetAsync($"{_baseUrl}/api/0/buckets/", ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var buckets = JsonSerializer.Deserialize<Dictionary<string, AwBucketInfo>>(content, JsonOptions);
            if (buckets == null || buckets.Values.Any(b => b == null || string.IsNullOrWhiteSpace(b.Id)))
                throw new InvalidDataException("ActivityWatch returned an invalid watcher list.");
            return buckets;
        }
        catch when (!throwOnError)
        {
            return [];
        }
    }

    public async Task<List<AwEvent>> GetEventsAsync(
        string bucketId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        int limit = 50000,
        CancellationToken ct = default, bool throwOnError = false)
    {
        try
        {
            string startIso = Uri.EscapeDataString(startUtc.ToString("o"));
            string endIso = Uri.EscapeDataString(endUtc.ToString("o"));
            string url = $"{_baseUrl}/api/0/buckets/{Uri.EscapeDataString(bucketId)}/events?start={startIso}&end={endIso}&limit={limit}";

            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var events = JsonSerializer.Deserialize<List<AwEvent>>(content, throwOnError ? StrictEventJsonOptions : JsonOptions);
            if (throwOnError && (events == null || events.Any(e => e == null || e.Timestamp == default ||
                    !double.IsFinite(e.DurationSeconds) || e.DurationSeconds < 0 || e.Data == null)))
                throw new InvalidDataException("ActivityWatch returned invalid activity data.");
            if (throwOnError && events!.Count >= limit)
                throw new InvalidDataException("ActivityWatch reached the event limit; the report was not replaced with partial data.");
            return events ?? [];
        }
        catch when (!throwOnError)
        {
            return [];
        }
    }
}
