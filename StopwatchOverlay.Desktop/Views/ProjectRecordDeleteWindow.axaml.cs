using System;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace StopwatchOverlay.Desktop.Views;

public partial class ProjectRecordDeleteWindow : Window
{
    public bool WasDeleted { get; private set; }

    public ProjectRecordDeleteWindow()
        : this(new ProjectWorkIntervalView(
            Guid.NewGuid(), Guid.NewGuid(), "sample_project", "Sample Project",
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow))
    {
    }

    public ProjectRecordDeleteWindow(ProjectWorkIntervalView record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.IsOpen)
            throw new ArgumentException("An active project record cannot be deleted.", nameof(record));

        InitializeComponent();

        DateTime startLocal = EnsureUtc(record.StartUtc).ToLocalTime();
        DateTime endLocal = EnsureUtc(record.EndUtc!.Value).ToLocalTime();
        ProjectText.Text = record.ProjectName;
        DateText.Text = startLocal.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture);
        TimeText.Text = endLocal.Date == startLocal.Date
            ? $"{startLocal:HH:mm} – {endLocal:HH:mm}"
            : $"{startLocal:MMM d, HH:mm} – {endLocal:MMM d, HH:mm}";
        DurationText.Text = FormatDuration(record.Duration(record.EndUtc.Value));
    }

    public static async Task<bool> ShowAsync(Window? owner, ProjectWorkIntervalView record)
    {
        var dialog = new ProjectRecordDeleteWindow(record);
        if (owner != null)
        {
            return await dialog.ShowDialog<bool>(owner);
        }

        var tcs = new TaskCompletionSource<bool>();
        dialog.Closed += (_, _) => tcs.TrySetResult(dialog.WasDeleted);
        dialog.Show();
        return await tcs.Task;
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        WasDeleted = false;
        Close(false);
    }

    private void Delete_Click(object? sender, RoutedEventArgs e)
    {
        WasDeleted = true;
        Close(true);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Cancel_Click(this, e);
        }
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static string FormatDuration(TimeSpan duration)
    {
        int hours = Math.Max(0, (int)duration.TotalHours);
        return hours > 0 ? $"{hours}:{duration.Minutes:00}" : $"{duration.Minutes}m";
    }
}
