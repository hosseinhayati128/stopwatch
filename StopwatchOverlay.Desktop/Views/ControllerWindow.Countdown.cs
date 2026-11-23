using System;
using System.Globalization;
using Avalonia.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class ControllerWindow
{
    // The controller ticks at 30 Hz. The setup fields only need a new projection on
    // edits, timer changes, or when the local clock reaches the next second.
    private readonly record struct CountdownViewState(Guid Timer, long Second, bool Running, bool Until,
        bool Smart, bool Initialized, string Input, TimeSpan Duration, TimeSpan PausedRemaining, DateTime Target);
    private CountdownViewState? _lastCountdownView;
    private TimeSpan? _setupRemaining;

    private void UpdateCountdownSetup(TimerSession timer, DateTime now)
    {
        var state = new CountdownViewState(timer.Id, now.Ticks / TimeSpan.TicksPerSecond, timer.IsRunning,
            timer.UseClockTarget, _settings.UseSmartCountdownInput, timer.CountdownInitialized, CountdownInput(timer),
            timer.CountdownDuration, timer.IsRunning ? default : timer.CountdownRemaining, timer.ClockTarget);
        if (_lastCountdownView == state) return;
        _lastCountdownView = state;

        var result = ResolveCountdown(timer, now);
        _setupRemaining = result.Duration ?? result.Target - now;
        bool valid = result.Success;
        bool showError = !valid && !timer.IsRunning;
        CountdownInputError.Text = result.Error;
        CountdownInputError.IsVisible = showError;
        CountdownTextFrame.Classes.Set("invalid", showError);
        CountdownDurationPanel.Classes.Set("invalid", showError);
        CountdownPresets.IsVisible = !timer.UseClockTarget;
        CountdownPresets.IsEnabled = !timer.IsRunning;
        CountdownFinish.IsVisible = valid && (!timer.CountdownInitialized || timer.CountdownRemaining > TimeSpan.Zero);
        var presetDuration = timer.CountdownInitialized || timer.IsRunning ? timer.CountdownDuration : result.Duration;
        foreach (var child in CountdownPresets.Children)
            if (child is Button preset && int.TryParse(preset.Tag as string, out int minutes))
                preset.Classes.Set("selected", !timer.UseClockTarget && presetDuration == TimeSpan.FromMinutes(minutes));

        if (!CountdownFinish.IsVisible) return;
        var finish = result.Target ?? now + result.Duration!.Value;
        var days = (finish.Date - now.Date).Days;
        CountdownDay.Text = days switch { 0 => "Today", 1 => "Tomorrow", _ => finish.ToString("ddd, d MMM", CultureInfo.CurrentCulture) };
        CountdownTime.Text = finish.ToString("HH:mm", CultureInfo.InvariantCulture);
        CountdownDetail.IsVisible = result.Target.HasValue;
        if (CountdownDetail.IsVisible)
        {
            var minutes = (long)Math.Ceiling((finish - now).TotalMinutes);
            string distance = minutes <= 1 && finish - now < TimeSpan.FromMinutes(1) ? "Less than a minute" : $"{minutes:N0} min";
            CountdownDetail.Text = days is 0 or 1 ? $"{finish:ddd, d MMM}  ·  {distance} from now" : $"{distance} from now";
        }
    }
}
