using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace StopwatchOverlay.Desktop.Views;

public partial class ProjectRecordEditorWindow : Window
{
    private const string NewProjectKey = "__new_project__";

    private readonly ProjectWorkIntervalView? _record;
    private readonly Func<string, DateTime, DateTime, ProjectRecordMutationResult>? _commit;
    private readonly string? _theme;
    private bool _loaded;
    private bool _accepted;
    private Control? _validationTarget;

    public string ProjectName { get; private set; } = "";
    public DateTime StartUtc { get; private set; }
    public DateTime EndUtc { get; private set; }
    public ProjectWorkIntervalView? SavedRecord { get; private set; }

    public ProjectRecordEditorWindow()
        : this(Array.Empty<ProjectInfoView>(), null)
    {
    }

    public ProjectRecordEditorWindow(
        IReadOnlyList<ProjectInfoView> projects,
        string? initialProjectKey,
        ProjectWorkIntervalView? record = null,
        Func<string, DateTime, DateTime, ProjectRecordMutationResult>? commit = null,
        DateTime? initialLocalDate = null,
        string? theme = null)
    {
        ArgumentNullException.ThrowIfNull(projects);

        _record = record;
        _commit = commit;
        _theme = theme;
        InitializeComponent();
        foreach (var date in new[] { StartDatePicker, EndDatePicker })
            date.DateValidationError += (_, e) =>
            {
                e.ThrowException = false;
                // The native picker otherwise restores the old date after invalid text.
                date.SelectedDate = null;
            };

        var choices = projects
            .OrderBy(project => project.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(project => new ProjectChoice(project.Key, project.Name, false))
            .ToList();

        if (record != null
            && !choices.Any(choice => string.Equals(
                choice.Key,
                record.ProjectKey,
                StringComparison.OrdinalIgnoreCase)))
        {
            choices.Add(new ProjectChoice(record.ProjectKey, record.ProjectName, false));
        }

        choices.Add(new ProjectChoice(NewProjectKey, "＋ New project…", true));
        ProjectSelector.ItemsSource = choices;

        string? preferredKey = record?.ProjectKey ?? initialProjectKey;
        ProjectChoice? selected = choices.FirstOrDefault(choice =>
            !choice.IsNew
            && string.Equals(choice.Key, preferredKey, StringComparison.OrdinalIgnoreCase));
        ProjectSelector.SelectedItem = selected ?? choices.Last();

        DateTime endLocal;
        DateTime startLocal;
        if (record?.EndUtc is DateTime recordEndUtc)
        {
            startLocal = EnsureUtc(record.StartUtc).ToLocalTime();
            endLocal = EnsureUtc(recordEndUtc).ToLocalTime();
            HeadingText.Text = "Edit project record";
            Title = "Edit project record";
            SaveButton.Content = "Save changes";
            IntroText.Text = "Correct the project or time range for this completed record. Times use your computer's local time zone.";
        }
        else
        {
            DateTime now = DateTime.Now;
            DateTime preferredDate = initialLocalDate?.Date ?? now.Date;
            if (preferredDate > now.Date)
                preferredDate = now.Date;

            bool useFirstHour = preferredDate < now.Date
                                && now.TimeOfDay < TimeSpan.FromHours(1);
            endLocal = new DateTime(
                preferredDate.Year,
                preferredDate.Month,
                preferredDate.Day,
                useFirstHour ? 1 : now.Hour,
                useFirstHour ? 0 : now.Minute,
                0,
                DateTimeKind.Local);
            startLocal = endLocal.AddHours(-1);
        }

        StartDatePicker.SelectedDate = startLocal.Date;
        EndDatePicker.SelectedDate = endLocal.Date;
        StartTimeBox.Text = startLocal.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        EndTimeBox.Text = endLocal.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        ApplyNavigatorLayout();
        UpdateDurationPreview();

        Opened += (_, _) =>
        {
            _loaded = true;
            UpdateNewProjectPanel();
            UpdateDurationPreview();

            if (IsAddingProject)
                NewProjectBox.Focus();
        };
    }

    public static async Task<bool> ShowAsync(
        Window? owner,
        IReadOnlyList<ProjectInfoView> projects,
        string? initialProjectKey,
        ProjectWorkIntervalView? record = null,
        Func<string, DateTime, DateTime, ProjectRecordMutationResult>? commit = null,
        DateTime? initialLocalDate = null,
        string? theme = null)
    {
        var dialog = new ProjectRecordEditorWindow(projects, initialProjectKey, record, commit, initialLocalDate, theme);
        if (owner != null)
        {
            return await dialog.ShowDialog<bool>(owner);
        }

        var tcs = new TaskCompletionSource<bool>();
        dialog.Closed += (_, _) => tcs.TrySetResult(dialog._accepted);
        dialog.Show();
        return await tcs.Task;
    }

    private void ProjectSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ProjectSelector.SelectedItem is ProjectChoice { IsNew: false })
            _navigatorAdding = false;
        UpdateNewProjectPanel();
        UpdateDurationPreview();
    }

    private void UpdateNewProjectPanel()
    {
        bool isNew = IsAddingProject;
        NewProjectPanel.IsVisible = isNew;
        UpdateNavigatorProject();
        if (_loaded && isNew)
            NewProjectBox.Focus();
    }

    private void DatePicker_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
        => UpdateDurationPreview();

    private void Input_Changed(object? sender, TextChangedEventArgs e)
        => UpdateDurationPreview();

    private void UpdateDurationPreview()
    {
        HideValidation();
        if (!TryReadUtcRange(out _, out DateTime startUtc, out DateTime endUtc, out _))
        {
            DurationPreviewText.Text = "—";
            UpdateNavigatorSummary(null);
            return;
        }

        DurationPreviewText.Text = FormatDuration(endUtc - startUtc);
        UpdateNavigatorSummary(endUtc - startUtc);
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryReadUtcRange(
                out string projectName,
                out DateTime startUtc,
                out DateTime endUtc,
                out string error))
        {
            ShowValidation(error);
            return;
        }

        ProjectName = projectName;
        StartUtc = startUtc;
        EndUtc = endUtc;

        if (_commit != null)
        {
            ProjectRecordMutationResult result;
            try
            {
                result = _commit(projectName, startUtc, endUtc);
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
            {
                CrashLogger.LogRecoverable(exception, "ProjectRecordCommit");
                ShowValidation(exception is InvalidOperationException
                    ? "Project records are temporarily read-only. Refresh the dashboard and try again."
                    : "The record could not be saved because one or more values are invalid. Review the form and try again.");
                return;
            }

            if (result.Status != ProjectRecordMutationStatus.Success)
            {
                ShowValidation(result.Status switch
                {
                    ProjectRecordMutationStatus.NotFound =>
                        "This record no longer exists. Close this editor and refresh the dashboard.",
                    ProjectRecordMutationStatus.OpenInterval =>
                        "This timer is currently running. Pause it before editing the record.",
                    ProjectRecordMutationStatus.Overlap =>
                        "That range overlaps another record from the same timer. Adjust the start or end time.",
                    _ => "The record could not be saved."
                });
                return;
            }

            SavedRecord = result.Record;
        }

        _accepted = true;
        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && (StartDatePicker.IsDropDownOpen || EndDatePicker.IsDropDownOpen || ProjectSelector.IsDropDownOpen))
        {
            StartDatePicker.IsDropDownOpen = EndDatePicker.IsDropDownOpen = ProjectSelector.IsDropDownOpen = false;
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(false);
        }
    }

    private bool TryReadUtcRange(
        out string projectName,
        out DateTime startUtc,
        out DateTime endUtc,
        out string error)
    {
        projectName = "";
        startUtc = default;
        endUtc = default;
        error = "";
        _validationTarget = null;

        var projectChoice = ProjectSelector.SelectedItem as ProjectChoice;
        if (!IsAddingProject && projectChoice == null)
        {
            _validationTarget = ProjectSelector;
            error = "Choose a project.";
            return false;
        }

        projectName = IsAddingProject ? (NewProjectBox.Text?.Trim() ?? "") : projectChoice!.Name;
        try
        {
            projectName = ProjectTimeHistory.NormalizeProjectName(projectName);
        }
        catch (ArgumentException)
        {
            _validationTarget = NewProjectBox;
            error = "Enter a valid project name using 200 or fewer printable characters.";
            return false;
        }

        if (!TryReadLocalDateTime(StartDatePicker, StartTimeBox, "start", out DateTime startLocal, out error, out _validationTarget)
            || !TryReadLocalDateTime(EndDatePicker, EndTimeBox, "end", out DateTime endLocal, out error, out _validationTarget))
        {
            return false;
        }

        bool preserveStart = _record != null
            && EndpointMatchesOriginal(StartDatePicker, StartTimeBox, _record.StartUtc);
        bool preserveEnd = _record?.EndUtc is DateTime originalEnd
            && EndpointMatchesOriginal(EndDatePicker, EndTimeBox, originalEnd);

        if (TimeZoneInfo.Local.IsAmbiguousTime(startLocal) && !preserveStart)
        {
            _validationTarget = StartTimeBox;
            error = "The start time occurs twice locally because the daylight-saving clock moves backward. Choose an unambiguous time.";
            return false;
        }
        if (TimeZoneInfo.Local.IsAmbiguousTime(endLocal) && !preserveEnd)
        {
            _validationTarget = EndTimeBox;
            error = "The end time occurs twice locally because the daylight-saving clock moves backward. Choose an unambiguous time.";
            return false;
        }

        try
        {
            startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, TimeZoneInfo.Local);
            endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, TimeZoneInfo.Local);
        }
        catch (ArgumentException)
        {
            error = "One of these local times does not exist because the clock changes at daylight saving time.";
            return false;
        }

        if (preserveStart)
        {
            startUtc = EnsureUtc(_record!.StartUtc);
        }
        if (preserveEnd && _record!.EndUtc is DateTime originalEndUtc)
        {
            endUtc = EnsureUtc(originalEndUtc);
        }

        if (endUtc <= startUtc)
        {
            _validationTarget = EndTimeBox;
            error = "End time must be later than start time.";
            return false;
        }

        if (endUtc > DateTime.UtcNow.AddSeconds(1))
        {
            _validationTarget = EndDatePicker;
            error = "A historical record cannot end in the future.";
            return false;
        }

        return true;
    }

    private static bool TryReadLocalDateTime(
        CalendarDatePicker datePicker,
        TextBox timeBox,
        string label,
        out DateTime local,
        out string error,
        out Control? invalidField)
    {
        local = default;
        error = "";
        invalidField = null;
        if (datePicker.SelectedDate is not DateTime date)
        {
            invalidField = datePicker;
            error = $"Choose the {label} date.";
            return false;
        }

        string text = timeBox.Text?.Trim() ?? "";
        if (!DateTime.TryParseExact(
                text,
                ["HH:mm", "HH:mm:ss"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime parsedTime))
        {
            invalidField = timeBox;
            error = $"Enter the {label} time as HH:mm or HH:mm:ss, for example 09:30:15.";
            return false;
        }

        local = DateTime.SpecifyKind(date.Date + parsedTime.TimeOfDay, DateTimeKind.Unspecified);
        if (TimeZoneInfo.Local.IsInvalidTime(local))
        {
            invalidField = timeBox;
            error = $"The {label} time does not exist locally because the clock changes at daylight saving time.";
            return false;
        }

        return true;
    }

    private static bool EndpointMatchesOriginal(
        CalendarDatePicker datePicker,
        TextBox timeBox,
        DateTime originalUtc)
    {
        DateTime originalLocal = EnsureUtc(originalUtc).ToLocalTime();
        return datePicker.SelectedDate?.Date == originalLocal.Date
            && string.Equals(
                timeBox.Text?.Trim() ?? "",
                originalLocal.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
    }

    private void ShowValidation(string message)
    {
        ValidationText.Text = message;
        ValidationText.IsVisible = true;
        UpdateNavigatorValidation(true);
        if (_theme == AppThemeCatalog.Nika)
            Dispatcher.UIThread.Post(() =>
            {
                if (IsVisible && ValidationText.IsVisible) ValidationText.BringIntoView();
            }, DispatcherPriority.Background);
    }

    private void HideValidation()
    {
        ValidationText.Text = "";
        ValidationText.IsVisible = false;
        UpdateNavigatorValidation(false);
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 24)
            return $"{(int)duration.TotalDays}d {duration.Hours}h {duration.Minutes}m";
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        if (duration.TotalMinutes >= 1)
            return $"{(int)duration.TotalMinutes}m";
        return $"{Math.Max(0, (int)duration.TotalSeconds)}s";
    }

    public sealed record ProjectChoice(string Key, string Name, bool IsNew)
    {
        public override string ToString() => Name;
    }
}
