namespace StopwatchOverlay;

public enum TodoDueState { None, Scheduled, Soon, Overdue }

public static class TodoDeadlines
{
    public static TodoDueState GetState(NoteEntry entry, DateTime? dueAt, DateTime now)
        => entry.IsCompleted || !dueAt.HasValue ? TodoDueState.None
        : dueAt <= now ? TodoDueState.Overdue
        : dueAt <= now.AddHours(24) ? TodoDueState.Soon : TodoDueState.Scheduled;

    public static string Label(DateTime dueAt, TodoDueState state)
        => (state == TodoDueState.Overdue ? "Overdue · " : "Due · ") + dueAt.ToString("ddd, dd MMM yyyy · HH:mm");
}
