using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace StopwatchOverlay.PeriodicReview;

public enum PeriodicReviewPromptResult
{
    Accepted, // Pressed Y
    Skipped,  // Pressed Esc
    Ignored   // Auto-closed after timeout
}

public partial class PeriodicReviewPromptWindow : Window
{
    private readonly Action<PeriodicReviewPromptResult>? _onResult;
    private readonly DispatcherTimer _timer;
    private int _remainingSeconds;
    private readonly int _snoozeMinutes;
    private readonly int _intervalMinutes;
    private bool _handled;

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
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
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
        _onResult?.Invoke(result);
        Close();
    }

    private void ReviewNowButton_Click(object sender, RoutedEventArgs e)
        => Finish(PeriodicReviewPromptResult.Accepted);

    private void SkipButton_Click(object sender, RoutedEventArgs e)
        => Finish(PeriodicReviewPromptResult.Skipped);

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
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
