using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using StopwatchOverlay.ActivityWatch;
using Xunit;

namespace StopwatchOverlay.Tests;

public sealed class ActivityWatchExportTests
{
    [Theory]
    [InlineData(true, "http")]
    [InlineData(false, "http")]
    [InlineData(true, "json")]
    [InlineData(false, "json")]
    [InlineData(false, "null")]
    [InlineData(false, "shape")]
    [InlineData(false, "missing-duration")]
    public async Task FailedRead_PreservesExistingReport(bool buckets, string failure)
    {
        using var f = new Fixture();
        File.WriteAllText(f.Target, "previous report: keep these bytes");
        f.Http.FailBuckets = buckets; f.Http.Failure = failure;
        var result = await f.Sync();
        Assert.False(result.Success); Assert.Equal("previous report: keep these bytes", File.ReadAllText(f.Target));
        Assert.Empty(Directory.GetFiles(f.Folder, "*.tmp.*"));
    }

    [Fact]
    public async Task EmptyEvents_SucceedButMissingWatchersDoNotReplaceReport()
    {
        using var f = new Fixture(); f.Http.Events = "[]";
        var result = await f.Sync(); Assert.True(result.Success, result.Message); Assert.Equal(0, result.AppCount);
        string before = File.ReadAllText(f.Target); f.Http.Buckets = "{}";
        result = await f.Sync(); Assert.False(result.Success); Assert.Equal(before, File.ReadAllText(f.Target));
    }

    [Fact]
    public async Task SuccessfulExport_ReplacesOnlyItsDateAndPreservesOtherDays()
    {
        using var f = new Fixture();
        File.WriteAllText(f.Target, "# Keep header\n\n## 📅 2026-11-14\nprevious day\n\n## 📅 2026-11-15\nstale section\n\n## 📅 2026-11-16\nnext day\n");
        var result = await f.Sync(); Assert.True(result.Success, result.Message); Assert.Equal(1, result.AppCount);
        string text = File.ReadAllText(f.Target);
        Assert.Contains("previous day", text); Assert.Contains("next day", text); Assert.DoesNotContain("stale section", text);
        Assert.Contains("Sample document", text); Assert.Empty(Directory.GetFiles(f.Folder, "*.tmp.*"));
    }

    [Theory]
    [InlineData("../outside.md")]
    [InlineData("..\\outside.md")]
    [InlineData("C:\\outside.md")]
    [InlineData("CON.md")]
    [InlineData("AUX")]
    [InlineData("report:secret.md")]
    [InlineData("report.")]
    public async Task InvalidFilename_RejectsBeforeReadingOrWriting(string name)
    {
        using var f = new Fixture(); f.Settings.ActivityWatchExportFileName = name;
        var validation = typeof(ActivityWatchSync).GetMethod("TryNormalizeExportFileName");
        Assert.True(validation != null, "Safe filename validation is required before exercising an invalid export path.");
        Assert.False((bool)validation!.Invoke(null, new object?[] { name, null })!);
        var result = await f.Sync(); Assert.False(result.Success); Assert.Equal(0, f.Http.Requests); Assert.Empty(Directory.GetFiles(f.Folder));
    }

    [Theory]
    [InlineData("", "ActivityWatch Log.md")]
    [InlineData("گزارش", "گزارش.md")]
    public async Task ValidFilename_UsesDefaultOrAddsMarkdownSuffix(string input, string expected)
    {
        using var f = new Fixture(); f.Settings.ActivityWatchExportFileName = input;
        var result = await f.Sync(); Assert.True(result.Success, result.Message); Assert.Equal(Path.Combine(f.Folder, expected), result.TargetFilePath);
    }

    [Fact]
    public async Task PendingExport_UsesCapturedOptionsAndRejectsSameFileOverlap()
    {
        using var f = new Fixture(); f.Http.Delay = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<ActivityWatchSyncResult> pending = f.Sync(); await f.Http.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Task<ActivityWatchSyncResult>? duplicateTask = null;
        try
        {
            int calls = f.Http.Requests; duplicateTask = f.Sync(); var duplicate = await duplicateTask.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.False(duplicate.Success); Assert.Equal(calls, f.Http.Requests);
            f.Settings.ActivityWatchExportFileName = "other.md"; f.Settings.ActivityWatchIncludeTitles = false;
            f.Settings.ActivityWatchMinDurationSeconds = 3600;
            f.Http.Delay.SetResult(true); var result = await pending;
            Assert.True(result.Success, result.Message); Assert.Equal(f.Target, result.TargetFilePath); Assert.Equal(1, result.AppCount);
            Assert.Contains("`Sample document`", File.ReadAllText(f.Target)); Assert.False(File.Exists(Path.Combine(f.Folder, "other.md")));
        }
        finally { f.Http.Delay.TrySetResult(true); await pending; if (duplicateTask != null) await duplicateTask; }
    }

    [Fact]
    public async Task Cancellation_PreservesExistingBytesAndReleasesDuplicateGuard()
    {
        using var f = new Fixture(); File.WriteAllText(f.Target, "keep");
        f.Http.Delay = new(TaskCreationOptions.RunContinuationsAsynchronously); using var stop = new CancellationTokenSource();
        var pending = f.Sync(stop.Token); await f.Http.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); stop.Cancel();
        Assert.False((await pending).Success); Assert.Equal("keep", File.ReadAllText(f.Target));
        f.Http.Delay = null; Assert.True((await f.Sync()).Success);
    }

    [Fact]
    public async Task WriteFailure_ReturnsFailureAndRemovesTemporaryFile()
    {
        using var f = new Fixture(); Directory.CreateDirectory(f.Target);
        var result = await f.Sync(); Assert.False(result.Success); Assert.True(Directory.Exists(f.Target));
        Assert.Empty(Directory.GetFiles(f.Folder, "*.tmp.*"));
    }

    [Fact]
    public async Task EventLimit_StrictExportRejectsPartialDataButReviewReadsRemainTolerant()
    {
        using var f = new Fixture(); using var http = new HttpClient(f.Http);
        var client = new ActivityWatchClient(httpClient: http);
        await Assert.ThrowsAsync<InvalidDataException>(() => client.GetEventsAsync("window", DateTimeOffset.Now.AddHours(-1), DateTimeOffset.Now, limit: 1, throwOnError: true));
        Assert.Single(await client.GetEventsAsync("window", DateTimeOffset.Now.AddHours(-1), DateTimeOffset.Now, limit: 1));
        f.Http.Failure = "http";
        Assert.Empty(await client.GetEventsAsync("window", DateTimeOffset.Now.AddHours(-1), DateTimeOffset.Now));
    }

    [Fact]
    public async Task ExplicitZeroDuration_RemainsValidAndTolerantReadsStillAcceptMissingDuration()
    {
        using var f = new Fixture(); f.Http.Events = f.Http.Events.Replace("\"duration\":60", "\"duration\":0");
        var result = await f.Sync(); Assert.True(result.Success, result.Message); Assert.Equal(0, result.AppCount);
        f.Http.Failure = "missing-duration";
        using var http = new HttpClient(f.Http); var client = new ActivityWatchClient(httpClient: http);
        Assert.Equal(0, Assert.Single(await client.GetEventsAsync("window", DateTimeOffset.Now.AddHours(-1), DateTimeOffset.Now)).DurationSeconds);
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly string Folder = Path.Combine(Path.GetTempPath(), "StopwatchExportTests-" + Guid.NewGuid().ToString("N"));
        internal readonly AppSettings Settings; internal readonly FakeHttp Http = new(); private readonly HttpClient _client;
        internal string Target => Path.Combine(Folder, "ActivityWatch Log.md");
        internal Fixture()
        {
            Directory.CreateDirectory(Folder); _client = new(Http);
            Settings = new() { ObsidianVaultFolder = Folder, ActivityWatchIncludeWeb = false, ActivityWatchIncludeTitles = true };
        }
        internal Task<ActivityWatchSyncResult> Sync(CancellationToken token = default)
        {
            var method = typeof(ActivityWatchSync).GetMethod("SyncAsync", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(AppSettings), typeof(DateOnly?), typeof(CancellationToken), typeof(HttpClient) }, null);
            Assert.True(method != null, "Export requires an isolated HTTP seam before running native export logic.");
            return (Task<ActivityWatchSyncResult>)method!.Invoke(null, new object?[] { Settings, new DateOnly(2026, 11, 15), token, _client })!;
        }
        public void Dispose() { _client.Dispose(); Directory.Delete(Folder, true); }
    }

    private sealed class FakeHttp : HttpMessageHandler
    {
        internal int Requests; internal bool FailBuckets; internal string? Failure;
        internal string Buckets = "{\"window\":{\"id\":\"window\",\"type\":\"currentwindow\"}}";
        internal string Events = "[{\"timestamp\":\"2026-11-15T09:00:00Z\",\"duration\":60,\"data\":{\"app\":\"editor.exe\",\"title\":\"Sample document\"}}]";
        internal TaskCompletionSource<bool>? Delay;
        internal readonly TaskCompletionSource<bool> Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++; Entered.TrySetResult(true);
            if (Delay != null) await Delay.Task.WaitAsync(cancellationToken);
            bool buckets = request.RequestUri!.AbsolutePath.EndsWith("/buckets/", StringComparison.Ordinal);
            bool fail = Failure != null && FailBuckets == buckets;
            return new(fail && Failure == "http" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
            { Content = new StringContent(fail ? Failure == "null" ? "null" : Failure == "shape" ? "[{}]" : Failure == "missing-duration" ? "[{\"timestamp\":\"2026-11-15T09:00:00Z\",\"data\":{\"app\":\"editor.exe\"}}]" : "not json" : buckets ? Buckets : Events) };
        }
    }
}
