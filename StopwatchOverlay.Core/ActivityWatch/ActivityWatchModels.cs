using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StopwatchOverlay.ActivityWatch;

public sealed class AwServerInfo
{
    [JsonPropertyName("hostname")]
    public string Hostname { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("device_id")]
    public string DeviceId { get; set; } = "";
}

public sealed class AwBucketInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("client")]
    public string Client { get; set; } = "";

    [JsonPropertyName("hostname")]
    public string Hostname { get; set; } = "";
}

public sealed class AwEvent
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("duration")]
    public double DurationSeconds { get; set; }

    [JsonPropertyName("data")]
    public Dictionary<string, JsonElement>? Data { get; set; }

    public string? GetStringData(string key)
    {
        if (Data != null && Data.TryGetValue(key, out var elem))
        {
            if (elem.ValueKind == JsonValueKind.String)
                return elem.GetString();
        }
        return null;
    }

    public string App => GetStringData("app") ?? "Unknown";
    public string Title => GetStringData("title") ?? "";
    public string Url => GetStringData("url") ?? "";
    public string Status => GetStringData("status") ?? "";
}

public sealed record AwWindowDetail(string Title, TimeSpan Duration);

public sealed class AwAppSummary
{
    public string AppName { get; init; } = "";
    public TimeSpan TotalDuration { get; set; }
    public Dictionary<string, TimeSpan> Titles { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class AwWebSummary
{
    public string Domain { get; init; } = "";
    public TimeSpan TotalDuration { get; set; }
    public Dictionary<string, TimeSpan> Pages { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record AwTimelineInterval(
    DateOnly Date,
    string Activity,
    string Details,
    TimeOnly Start,
    TimeOnly End,
    TimeSpan Duration,
    string Type)
{
    public int DurationMinutes => Math.Max(1, (int)Math.Round(Duration.TotalMinutes));
}

public sealed class AwDailyReport
{
    public DateOnly Date { get; init; }
    public TimeSpan TotalActiveDuration { get; set; }
    public TimeSpan TotalAfkDuration { get; set; }
    public List<AwAppSummary> Applications { get; init; } = [];
    public List<AwWebSummary> WebSites { get; init; } = [];
    public List<AwTimelineInterval> Intervals { get; init; } = [];
}

public sealed record ActivityWatchSyncResult(
    bool Success,
    int AppCount,
    int WebCount,
    string TargetFilePath,
    string? Message = null);
