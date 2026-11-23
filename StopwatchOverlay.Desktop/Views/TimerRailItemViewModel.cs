using System.ComponentModel;

namespace StopwatchOverlay.Desktop.Views;

public sealed class TimerRailItemViewModel
    : INotifyPropertyChanged
{
    internal TimerRailItemViewModel(TimerSession session) => Session = session;
    internal TimerSession Session { get; }
    public Guid Id => Session.Id;
    public string DisplayName { get; private set; } = "";
    public string DisplayTime { get; private set; } = "";
    public string State { get; private set; } = "";
    public bool IsRunning { get; private set; }
    public bool IsClock => State == "Clock";
    public string Details { get; private set; } = "";
    public string DisplaySummary => $"{DisplayTime} · {(IsRunning ? "Running" : "Paused")}{Details}";
    public string FullDescription => DisplayName + Details;
    public event PropertyChangedEventHandler? PropertyChanged;

    internal static string StateOf(TimerSession session)
    {
        if (session.Mode == 1) return "Clock";
        if (session.IsRunning) return "Running";
        if (session.Mode != 2) return session.HasAccumulatedTime ? "Paused" : "Ready";
        if (session.CountdownInitialized && session.CountdownRemaining == TimeSpan.Zero) return "Finished";
        return session.CountdownRemaining > TimeSpan.Zero && (session.CountdownInitialized || session.CountdownRemaining < session.CountdownDuration)
            ? "Paused" : "Ready";
    }

    internal void UpdateMetadata()
    {
        string name = Session.DisplayName;
        string details = (string.IsNullOrWhiteSpace(Session.Category) ? "" : $" · [{Session.Category}]") + (Session.IsSeparated ? " · Separated" : "");
        if (DisplayName == name && Details == details) return;
        DisplayName = name;
        Details = details;
        Changed(nameof(DisplayName));
        Changed(nameof(FullDescription));
        Changed(nameof(DisplaySummary));
    }

    internal void UpdateDisplay(string time, string state)
    {
        if (DisplayTime == time && State == state && IsRunning == Session.IsRunning) return;
        if (DisplayTime != time) { DisplayTime = time; Changed(nameof(DisplayTime)); }
        if (State != state) { State = state; Changed(nameof(State)); Changed(nameof(IsClock)); }
        if (IsRunning != Session.IsRunning) { IsRunning = Session.IsRunning; Changed(nameof(IsRunning)); }
        Changed(nameof(DisplaySummary));
    }

    private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
