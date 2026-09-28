using System;

namespace StopwatchOverlay;

public enum FocusPauseReason
{
    ValidBreak = 1,
    Distraction = 2,
    IdeaBrainstorming = 3,
    Unspecified = 4,
    TaskSwitch = 5
}

public sealed record FocusPauseRecord(
    Guid Id,
    Guid TimerSessionId,
    string ProjectName,
    DateTime PauseStartUtc,
    DateTime ResumeUtc,
    FocusPauseReason Reason,
    string? Note = null)
{
    public TimeSpan Duration => ResumeUtc > PauseStartUtc
        ? ResumeUtc - PauseStartUtc
        : TimeSpan.Zero;

    public string ReasonDisplayName => Reason switch
    {
        FocusPauseReason.ValidBreak => "Valid Break",
        FocusPauseReason.Distraction => "Distraction",
        FocusPauseReason.IdeaBrainstorming => "Idea / Brainstorming",
        FocusPauseReason.TaskSwitch => "Switch Between Tasks",
        _ => "Unspecified"
    };

    public string ReasonIcon => Reason switch
    {
        FocusPauseReason.ValidBreak => "☕",
        FocusPauseReason.Distraction => "⚡",
        FocusPauseReason.IdeaBrainstorming => "💡",
        FocusPauseReason.TaskSwitch => "🔀",
        _ => "⏸️"
    };
}
