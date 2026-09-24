using System;
using System.Collections.Generic;

namespace StopwatchOverlay;

/// <summary>
/// Captures the pre-edit state of a timer and its project intervals to support undoing saved edits.
/// </summary>
public sealed record TimerEditUndoState(
    Guid TimerSessionId,
    string TimerDisplayName,
    string TimerProjectName,
    TimeSpan Elapsed,
    TimeSpan CountdownRemaining,
    int Mode,
    bool IsRunning,
    List<WorkIntervalDocumentEntry> Intervals);
