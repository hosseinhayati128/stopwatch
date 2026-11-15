using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using StopwatchOverlay.Internet;
using Xunit;

namespace StopwatchOverlay.Tests;

public class InternetMonitorServiceTests
{
    private static readonly InternetCheckResult Result = new(DateTime.Now, InternetStatus.Online, 24, 38.6,
        new(true, "Wi-Fi", "Sample", 88, "Sample", "Sample"), "Sample");

    [Fact]
    public async Task ScheduledCheckHonorsEnablementAndRunningTimers()
    {
        var settings = new AppSettings { InternetMonitorEnabled = false, InternetMonitorOnlyDuringTimers = true };
        bool running = false; int calls = 0, saves = 0, events = 0;
        using var monitor = Create(settings, () => running, (_, _) => { calls++; return Task.FromResult(Result); }, (_, _) => saves++);
        monitor.CheckCompleted += _ => events++;
        await Check(monitor); Assert.Equal(0, calls);
        settings.InternetMonitorEnabled = true; monitor.RestartTimer();
        await Check(monitor); Assert.Equal(0, calls);
        running = true; await Check(monitor);
        Assert.Equal(1, calls); Assert.Equal(1, saves); Assert.Equal(1, events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReconfigurationOrDisposalCancelsAndDiscardsLateResult(bool dispose)
    {
        var settings = new AppSettings { InternetMonitorEnabled = true };
        var pending = new TaskCompletionSource<InternetCheckResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = default; int calls = 0, saves = 0, events = 0;
        using var monitor = Create(settings, () => true, (_, ct) => { token = ct; calls++; return pending.Task; }, (_, _) => saves++);
        monitor.CheckCompleted += _ => events++;
        var first = Check(monitor); await Check(monitor); Assert.Equal(1, calls);
        if (dispose) monitor.Dispose(); else { settings.InternetMonitorEnabled = false; monitor.RestartTimer(); }
        Assert.True(token.IsCancellationRequested);
        pending.SetResult(Result); await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await first);
        Assert.Equal(0, saves); Assert.Equal(0, events);
    }

    [Fact]
    public async Task ManualCheckWorksWithAutomaticMonitoringOffAndSavesOnce()
    {
        int calls = 0, saves = 0;
        using var monitor = Create(new() { InternetMonitorEnabled = false }, () => false,
            (_, _) => { calls++; return Task.FromResult(Result); }, (_, _) => saves++);
        Assert.Same(Result, await monitor.RunCheckOnceAsync()); Assert.Equal(1, calls); Assert.Equal(1, saves);
    }

    private static InternetMonitorService Create(AppSettings settings, Func<bool> running,
        Func<int, CancellationToken, Task<InternetCheckResult>> probe, Action<InternetCheckResult, AppSettings> save)
    {
        var ctor = typeof(InternetMonitorService).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
            [typeof(Func<AppSettings>), typeof(Func<bool>), typeof(Func<int, CancellationToken, Task<InternetCheckResult>>), typeof(Action<InternetCheckResult, AppSettings>)], null);
        Assert.NotNull(ctor);
        return (InternetMonitorService)ctor.Invoke([(Func<AppSettings>)(() => settings), running, probe, save]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManualAndScheduledSamplesNeverOverlap(bool manualFirst)
    {
        int calls = 0, saves = 0;
        var firstResult = new TaskCompletionSource<InternetCheckResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondResult = new TaskCompletionSource<InternetCheckResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<int, CancellationToken, Task<InternetCheckResult>> probe = (_, _) => Interlocked.Increment(ref calls) == 1 ? firstResult.Task : secondResult.Task;
        using var monitor = Create(new() { InternetMonitorEnabled = true }, () => true, probe, (_, _) => saves++);
        Task<InternetCheckResult> Manual() => monitor.TestConnectionAsync(1_000_000, CancellationToken.None);
        Task first = manualFirst ? (Task)Manual() : Check(monitor);
        Task second = manualFirst ? (Task)Check(monitor) : Manual();
        Assert.Equal(1, calls);
        firstResult.SetResult(Result); await first;
        for (int i = 0; i < 100 && Volatile.Read(ref calls) < 2; i++) await Task.Delay(5);
        Assert.Equal(2, calls); secondResult.SetResult(Result); await second;
        Assert.Equal(1, saves);
    }
    private static Task<InternetCheckResult?> Check(InternetMonitorService monitor)
    {
        var method = typeof(InternetMonitorService).GetMethod("CheckAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method); return (Task<InternetCheckResult?>)method.Invoke(monitor, [true, null])!;
    }
}
