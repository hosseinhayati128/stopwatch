namespace StopwatchOverlay.PeriodicReview;

public enum PeriodicReviewPromptResult
{
    Accepted, // Pressed Y
    Skipped,  // Pressed Esc
    Ignored   // Auto-closed after timeout
}
