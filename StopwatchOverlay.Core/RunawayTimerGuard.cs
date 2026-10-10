using System;

namespace StopwatchOverlay;

public static class RunawayTimerGuard
{
    public static Func<TimerSession, bool>? CustomRunawayPromptHandler { get; set; }

    /// <summary>
    /// Determines if an active timer should be automatically paused because it has exceeded the
    /// continuous runtime threshold while the user has been inactive.
    /// </summary>
    public static bool ShouldPauseRunaway(TimeSpan elapsed, TimeSpan idleDuration, double maxContinuousHours)
    {
        if (maxContinuousHours <= 0) return false;
        TimeSpan threshold = TimeSpan.FromHours(maxContinuousHours);
        // Requires timer reaching threshold and user being idle for at least 15 minutes
        return elapsed >= threshold && idleDuration >= TimeSpan.FromMinutes(15);
    }

    /// <summary>
    /// Calculates the excess duration beyond the maximum continuous runtime limit.
    /// </summary>
    public static TimeSpan CalculateExcess(TimeSpan elapsed, double maxContinuousHours)
    {
        if (maxContinuousHours <= 0) return TimeSpan.Zero;
        TimeSpan threshold = TimeSpan.FromHours(maxContinuousHours);
        return elapsed > threshold ? elapsed - threshold : TimeSpan.Zero;
    }

    /// <summary>
    /// Checks whether an interval crossed midnight from a previous calendar day.
    /// </summary>
    public static bool CrossedMidnight(DateTime startUtc, DateTime endUtc)
    {
        return startUtc.ToLocalTime().Date < endUtc.ToLocalTime().Date;
    }
}
