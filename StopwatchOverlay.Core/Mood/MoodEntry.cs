using System;
using System.Collections.Generic;

namespace StopwatchOverlay.Mood;

/// <summary>
/// Represents a user's emotional and mental state recorded during a periodic review.
/// </summary>
public sealed class MoodEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }

    /// <summary>
    /// Mood score on a scale of 1.0 to 10.0 with 0.1 precision (e.g. 4.8, 5.1, 7.5).
    /// </summary>
    public double Score { get; set; } = 7.0;

    /// <summary>
    /// Feeling keywords selected by the user (e.g., Happy, Thrilled, Anxious, Sad, Depressed).
    /// </summary>
    public List<string> Keywords { get; set; } = [];

    /// <summary>
    /// Optional reflection or note explaining the mood or context.
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Names of projects worked on during this period.
    /// </summary>
    public List<string> AssociatedProjects { get; set; } = [];

    /// <summary>
    /// Indicates whether the feelings page was skipped for this review.
    /// </summary>
    public bool IsSkipped { get; set; }

    public DateTime TimestampLocal => TimestampUtc.ToLocalTime();
    public DateTime PeriodStartLocal => PeriodStartUtc.ToLocalTime();
    public DateTime PeriodEndLocal => PeriodEndUtc.ToLocalTime();

    public string ScoreDisplay => Score.ToString("0.0");

    public string MoodBadge
    {
        get
        {
            if (Score < 3.0) return "🔴 Distressed";
            if (Score < 5.0) return "🟠 Down";
            if (Score < 7.0) return "🟡 Neutral";
            if (Score < 8.5) return "🟢 Good";
            return "🌟 Thrilled";
        }
    }

    public string MoodEmoji
    {
        get
        {
            if (Score < 3.0) return "😭";
            if (Score < 5.0) return "🙁";
            if (Score < 7.0) return "😐";
            if (Score < 8.5) return "😊";
            return "🤩";
        }
    }
}
