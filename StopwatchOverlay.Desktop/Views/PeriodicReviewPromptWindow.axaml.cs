using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using StopwatchOverlay.PeriodicReview;

namespace StopwatchOverlay.Desktop.Views;

public partial class PeriodicReviewPromptWindow : Window
{
    private readonly Action<PeriodicReviewPromptResult>? _onResult;
    private readonly DispatcherTimer _timer;
    private int _remainingSeconds;
    private readonly int _snoozeMinutes;
    private readonly int _intervalMinutes;
    private bool _handled;

    public PeriodicReviewPromptResult Result { get; private set; } = PeriodicReviewPromptResult.Ignored;

    public PeriodicReviewPromptWindow()
        : this(DateTime.UtcNow.AddMinutes(-30), DateTime.UtcNow, 5, 30, 30, null)
    {
    }

    public PeriodicReviewPromptWindow(
        DateTime startUtc,
        DateTime endUtc,
        int timeoutSeconds,
        int snoozeMinutes,
        int intervalMinutes,
        Action<PeriodicReviewPromptResult>? onResult)
    {
        InitializeComponent();

        _onResult = onResult;
        _remainingSeconds = timeoutSeconds > 0 ? timeoutSeconds : 5;
        _snoozeMinutes = snoozeMinutes > 0 ? snoozeMinutes : 30;
        _intervalMinutes = intervalMinutes > 0 ? intervalMinutes : 30;

        DateTime startLocal = startUtc.ToLocalTime();
        DateTime endLocal = endUtc.ToLocalTime();
        int totalMin = Math.Max(1, (int)Math.Round((endLocal - startLocal).TotalMinutes));

        PeriodSummaryText.Text = $"Review what you were doing ({startLocal:HH:mm} – {endLocal:HH:mm}, {totalMin}m)";
        SkipSubText.Text = $"Skip this term and remind again in {_intervalMinutes}m";
        CountdownText.Text = $"Closing in {_remainingSeconds}s (snoozes for {_snoozeMinutes}m)";

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += Timer_Tick;

        Opened += OnWindowOpened;
    }

    public static async Task<PeriodicReviewPromptResult> ShowAsync(
        Window? owner,
        DateTime startUtc,
        DateTime endUtc,
        int timeoutSeconds,
        int snoozeMinutes,
        int intervalMinutes)
    {
        var tcs = new TaskCompletionSource<PeriodicReviewPromptResult>();
        var dialog = new PeriodicReviewPromptWindow(
            startUtc,
            endUtc,
            timeoutSeconds,
            snoozeMinutes,
            intervalMinutes,
            res => tcs.TrySetResult(res));

        if (owner != null)
            dialog.Show(owner);
        else
            dialog.Show();

        return await tcs.Task;
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        _timer.Start();
        Activate();
        Focus();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _remainingSeconds--;
        if (_remainingSeconds <= 0)
        {
            _timer.Stop();
            Finish(PeriodicReviewPromptResult.Ignored);
            return;
        }

        CountdownText.Text = $"Closing in {_remainingSeconds}s (snoozes for {_snoozeMinutes}m)";
    }

    private void Finish(PeriodicReviewPromptResult result)
    {
        if (_handled) return;
        _handled = true;
        _timer.Stop();
        Result = result;
        _onResult?.Invoke(result);
        Close();
    }

    private void ReviewNowButton_Click(object? sender, RoutedEventArgs e)
        => Finish(PeriodicReviewPromptResult.Accepted);

    private void SkipButton_Click(object? sender, RoutedEventArgs e)
        => Finish(PeriodicReviewPromptResult.Skipped);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Y)
        {
            Finish(PeriodicReviewPromptResult.Accepted);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Finish(PeriodicReviewPromptResult.Skipped);
            e.Handled = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _timer.Stop();
        if (!_handled)
        {
            Finish(PeriodicReviewPromptResult.Ignored);
        }
    }
}
