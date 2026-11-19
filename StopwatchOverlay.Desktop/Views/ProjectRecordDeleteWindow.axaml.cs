using System;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace StopwatchOverlay.Desktop.Views;

public partial class ProjectRecordDeleteWindow : Window
{
    private bool _completed;
    public bool WasDeleted { get; private set; }

    public ProjectRecordDeleteWindow()
        : this(new ProjectWorkIntervalView(
            Guid.NewGuid(), Guid.NewGuid(), "sample_project", "Sample Project",
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow))
    {
    }

    public ProjectRecordDeleteWindow(ProjectWorkIntervalView record, AppSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.IsOpen)
            throw new ArgumentException("An active project record cannot be deleted.", nameof(record));

        InitializeComponent();

        DateTime startUtc = EnsureUtc(record.StartUtc);
        DateTime endUtc = EnsureUtc(record.EndUtc!.Value);
        TimeSpan duration = endUtc > startUtc ? endUtc - startUtc : TimeSpan.Zero;
        DateTime startLocal = startUtc.ToLocalTime();
        DateTime endLocal = endUtc.ToLocalTime();
        ProjectText.Text = record.ProjectName;
        DateText.Text = startLocal.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture);
        TimeText.Text = endLocal.Date == startLocal.Date
            ? $"{startLocal:HH:mm} – {endLocal:HH:mm}"
            : $"{startLocal:MMM d, HH:mm} – {endLocal:MMM d, HH:mm}";
        DurationText.Text = FormatDuration(duration);
        if (AppThemeCatalog.Normalize(settings?.ThemeMode) == AppThemeCatalog.Nika)
            ApplyDeleteAppearance(settings!, startLocal, endLocal, duration);
        Opened += (_, _) => Dispatcher.UIThread.Post(() => { if (IsVisible) CancelButton.Focus(); });
        Closed += (_, _) => _completed = true;
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Escape) { e.Handled = true; Complete(false); }
        }, RoutingStrategies.Tunnel);
    }

    public static async Task<bool> ShowAsync(Window? owner, ProjectWorkIntervalView record, AppSettings? settings = null)
    {
        var dialog = new ProjectRecordDeleteWindow(record, settings);
        if (owner != null)
        {
            return await dialog.ShowDialog<bool>(owner);
        }

        var tcs = new TaskCompletionSource<bool>();
        dialog.Closed += (_, _) => tcs.TrySetResult(dialog.WasDeleted);
        dialog.Show();
        return await tcs.Task;
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Complete(false);
    private void Delete_Click(object? sender, RoutedEventArgs e) => Complete(true);
    private void Complete(bool deleted)
    {
        if (_completed) return;
        _completed = true;
        WasDeleted = deleted;
        Close(deleted);
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
