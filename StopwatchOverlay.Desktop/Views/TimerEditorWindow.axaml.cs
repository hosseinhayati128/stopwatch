using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace StopwatchOverlay.Desktop.Views;

public partial class TimerEditorWindow : Window
{
    private readonly TimerSession _timer;
    private readonly IReadOnlyList<ProjectWorkIntervalView> _intervals;
    private readonly ProjectWorkIntervalView? _latestInterval;
    private readonly DispatcherTimer _liveRefreshTimer;
    private readonly AppSettings? _settings;

    private readonly TimeSpan _originalTotalElapsed;
    private readonly DateTime? _latestStartLocal;
    private readonly DateTime? _latestEndLocal;

    private bool _isUpdatingInternally;
    private bool _discardRecords;
    private bool _isCustomProjectMode;
    private bool _isCustomCategoryMode;
    private bool _isRunning;
    private TimeSpan _currentTimeSpan;

    public bool WasSaved { get; private set; }
    public bool UndoRequested { get; private set; }
    public bool DiscardRecordsRequested { get; private set; }
    public bool DeleteTimerRequested { get; private set; }
    public TimeSpan NewTimeValue { get; private set; }
    public TimeSpan Delta => NewTimeValue - _originalTotalElapsed;
    public string? NewProjectName { get; private set; }
    public string NewCategory { get; private set; } = "Work";
    public double NewScorePerHour { get; private set; } = 1.0;
    public bool NewIsRunning { get; private set; }

    public TimerEditorWindow()
        : this(new TimerSession(1), Array.Empty<string>(), Array.Empty<ProjectWorkIntervalView>())
    {
    }

    public TimerEditorWindow(
        TimerSession timer,
        IReadOnlyList<string> projectNames,
        IReadOnlyList<ProjectWorkIntervalView> intervals,
        bool canUndo = false,
        AppSettings? settings = null)
    {
        _timer = timer ?? throw new ArgumentNullException(nameof(timer));
        _intervals = intervals ?? Array.Empty<ProjectWorkIntervalView>();
        _latestInterval = _intervals.LastOrDefault();
        _isRunning = timer.IsRunning;
        _settings = settings;

        _originalTotalElapsed = timer.Mode == 2 ? timer.CountdownRemaining : timer.Elapsed;
        if (_originalTotalElapsed < TimeSpan.Zero) _originalTotalElapsed = TimeSpan.Zero;
        _currentTimeSpan = _originalTotalElapsed;

        InitializeComponent();

        Title = $"Edit {timer.DisplayName}";
        HeadingText.Text = $"Edit {timer.DisplayName}";

        string mode = timer.Mode switch
        {
            1 => "Clock",
            2 => "Countdown",
            3 => "Timecode",
            _ => "Stopwatch"
        };
        string state = _isRunning ? "Running" : "Paused";
        string proj = string.IsNullOrWhiteSpace(timer.Name) ? "Unassigned" : timer.Name;
        SubheadingText.Text = $"{mode} · {state} · Project: {proj}";

        TimeValueLabel.Text = timer.Mode == 2
            ? "REMAINING TIME"
            : "TOTAL ELAPSED TIME";

        // Populate Projects
        ProjectSelector.Items.Add("(Unassigned)");
        int selectedIndex = 0;
        int index = 1;
        foreach (var p in projectNames.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase))
        {
            ProjectSelector.Items.Add(p);
            if (string.Equals(p, timer.Name, StringComparison.OrdinalIgnoreCase))
            {
                selectedIndex = index;
            }
            index++;
        }
        ProjectSelector.SelectedIndex = selectedIndex;
        UpdateIdleStopCheck();

        // Populate Categories
        var categories = (_settings?.GetNormalizedProjectCategories() ?? AppSettings.DefaultProjectCategories)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        string currentCat = string.IsNullOrWhiteSpace(timer.Category) ? "Work" : timer.Category.Trim();
        if (!categories.Contains(currentCat, StringComparer.OrdinalIgnoreCase))
        {
            categories.Insert(0, currentCat);
        }

        int catSelectedIndex = 0;
        for (int i = 0; i < categories.Count; i++)
        {
            CategorySelector.Items.Add(categories[i]);
            if (string.Equals(categories[i], currentCat, StringComparison.OrdinalIgnoreCase))
            {
                catSelectedIndex = i;
            }
        }
        CategorySelector.SelectedIndex = catSelectedIndex;
        NewCategory = currentCat;

        double initialScore = timer.ScorePerHour > 0 ? timer.ScorePerHour : 1.0;
        ScorePerHourBox.Value = (decimal)initialScore;
        NewScorePerHour = initialScore;

        // Populate Segments List
        var segmentItems = new List<SegmentItem>();
        for (int i = 0; i < _intervals.Count; i++)
        {
            var iv = _intervals[i];
            DateTime start = iv.StartUtc.ToLocalTime();
            DateTime? end = iv.EndUtc?.ToLocalTime();
            TimeSpan dur = (end ?? DateTime.Now) - start;
            if (dur < TimeSpan.Zero) dur = TimeSpan.Zero;
            string endStr = end.HasValue ? end.Value.ToString("HH:mm:ss") : "Now (Running)";
            segmentItems.Add(new SegmentItem(
                $"Segment {i + 1}: {start:HH:mm:ss} – {endStr}",
                FormatDuration(dur)));
        }
        SegmentsList.ItemsSource = segmentItems;
        SegmentsExpander.Header = $"Recorded Segments ({_intervals.Count})";
        if (_intervals.Count == 0)
        {
            SegmentsExpander.IsVisible = false;
        }

        // Populate Latest Segment Start Time
        if (_latestInterval != null)
        {
            _latestStartLocal = _latestInterval.StartUtc.ToLocalTime();
            _latestEndLocal = _latestInterval.EndUtc?.ToLocalTime();
            StartTimeBox.Text = _latestStartLocal.Value.ToString("HH:mm:ss");

            string segEnd = _latestEndLocal.HasValue ? _latestEndLocal.Value.ToString("HH:mm:ss") : "Now (running)";
            TimeSpan segDur = (_latestEndLocal ?? DateTime.Now) - _latestStartLocal.Value;
            if (segDur < TimeSpan.Zero) segDur = TimeSpan.Zero;
            SegmentDetailText.Text = $"Latest segment ({_intervals.Count} of {_intervals.Count}): {_latestStartLocal:HH:mm:ss} – {segEnd} ({FormatDuration(segDur)})";
        }
        else
        {
            DateTime fallbackStart = DateTime.Now - _originalTotalElapsed;
            _latestStartLocal = fallbackStart;
            StartTimeBox.Text = fallbackStart.ToString("HH:mm:ss");
            SegmentDetailText.Text = "No previously saved segments recorded for this timer.";
        }

        // Initialize Time Value Box and Summary
        TimeValueBox.Text = FormatDuration(_currentTimeSpan);
        UpdateAdjustmentSummary();

        UpdateRunningButton();
        UpdateRecordsStatus();
        UndoPreviousEditButton.IsVisible = canUndo;

        _liveRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _liveRefreshTimer.Tick += (_, _) => UpdateLivePreview();

        Opened += (_, _) =>
        {
            _liveRefreshTimer.Start();
            UpdateLivePreview();
            TimeValueBox.Focus();
            TimeValueBox.SelectAll();
        };
    }

    protected override void OnClosed(EventArgs e)
    {
        _liveRefreshTimer.Stop();
        base.OnClosed(e);
    }

    private void UpdateLivePreview()
    {
        if (_timer.Mode == 1)
        {
            LiveTimePreview.Text = $"Live: {DateTime.Now:HH:mm:ss}";
        }
        else if (_timer.Mode == 2)
        {
            LiveTimePreview.Text = $"Remaining: {FormatDuration(_timer.CountdownRemaining)}";
        }
        else
        {
            LiveTimePreview.Text = $"Live: {FormatDuration(_timer.Elapsed)}";
        }
    }

    private void UpdateRunningButton()
    {
        PauseResumeButton.Content = _isRunning ? "Pause Timer" : "Resume Timer";
        if (_isRunning)
        {
            PauseResumeButton.Classes.Remove("primary");
            PauseResumeButton.Classes.Add("modern");
        }
        else
        {
            PauseResumeButton.Classes.Remove("modern");
            PauseResumeButton.Classes.Add("primary");
        }
    }

    private void UpdateRecordsStatus()
    {
        if (_discardRecords)
        {
            RecordsStatusText.Text = "Session records will be removed from project history upon saving.";
            if (Application.Current?.TryFindResource("DangerTextBrush", out var dang) == true && dang is IBrush dBrush)
                RecordsStatusText.Foreground = dBrush;
            DiscardRecordsButton.IsEnabled = false;
        }
        else if (_intervals.Count > 0 || _timer.HasAccumulatedTime)
        {
            RecordsStatusText.Text = $"Tracking {_intervals.Count} segment(s) in project history.";
            if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sec) == true && sec is IBrush sBrush)
                RecordsStatusText.Foreground = sBrush;
            DiscardRecordsButton.IsEnabled = true;
        }
        else
        {
            RecordsStatusText.Text = "No active work interval recorded for this timer yet.";
            if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sec) == true && sec is IBrush sBrush)
                RecordsStatusText.Foreground = sBrush;
            DiscardRecordsButton.IsEnabled = true;
        }
    }

    private void UpdateAdjustmentSummary()
    {
        TimeSpan delta = _currentTimeSpan - _originalTotalElapsed;
        if (delta == TimeSpan.Zero)
        {
            AdjustmentSummaryText.Text = _intervals.Count > 0
                ? $"Total: {FormatDuration(_originalTotalElapsed)} across {_intervals.Count} segment(s)"
                : $"Total: {FormatDuration(_originalTotalElapsed)}";
            if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sec) == true && sec is IBrush sBrush)
                AdjustmentSummaryText.Foreground = sBrush;
        }
        else if (delta > TimeSpan.Zero)
        {
            if (_latestInterval != null)
            {
                DateTime now = DateTime.Now;
                DateTime effectiveEnd = _latestEndLocal ?? now;
                TimeSpan forwardAvailable = effectiveEnd < now ? now - effectiveEnd : TimeSpan.Zero;
                TimeSpan forwardAdd = delta < forwardAvailable ? delta : forwardAvailable;
                TimeSpan backwardAdd = delta - forwardAdd;

                if (backwardAdd > TimeSpan.Zero)
                {
                    if (forwardAdd > TimeSpan.Zero)
                    {
                        AdjustmentSummaryText.Text = $"+{FormatDuration(delta)}: +{FormatDuration(forwardAdd)} forward to {now:HH:mm:ss} (current time), +{FormatDuration(backwardAdd)} backward into previous gaps/segments";
                    }
                    else
                    {
                        AdjustmentSummaryText.Text = $"+{FormatDuration(delta)} will be added backward into previous gaps/segments (capped at current time {now:HH:mm:ss})";
                    }
                }
                else
                {
                    DateTime newEnd = effectiveEnd + delta;
                    AdjustmentSummaryText.Text = $"+{FormatDuration(delta)} will be added from latest segment (extending to {newEnd:HH:mm:ss})";
                }
            }
            else
            {
                AdjustmentSummaryText.Text = $"+{FormatDuration(delta)} will be added to project records";
            }
            if (Application.Current?.TryFindResource("AccentBrush", out var acc) == true && acc is IBrush aBrush)
                AdjustmentSummaryText.Foreground = aBrush;
        }
        else
        {
            AdjustmentSummaryText.Text = $"-{FormatDuration(-delta)} will eliminate time from the latest segments backwards";
            if (Application.Current?.TryFindResource("AccentBrush", out var acc) == true && acc is IBrush aBrush)
                AdjustmentSummaryText.Foreground = aBrush;
        }
    }

    private void TimeValueBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isUpdatingInternally) return;

        if (TryParseDuration(TimeValueBox.Text, out TimeSpan duration))
        {
            _currentTimeSpan = duration;
            UpdateAdjustmentSummary();
            SaveButton.IsEnabled = true;
        }
        else
        {
            AdjustmentSummaryText.Text = "Please enter a valid duration (e.g. 01:30:00, 15m, 45s).";
            if (Application.Current?.TryFindResource("DangerTextBrush", out var dang) == true && dang is IBrush dBrush)
                AdjustmentSummaryText.Foreground = dBrush;
            SaveButton.IsEnabled = false;
        }
    }

    private void StartTimeBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isUpdatingInternally) return;

        string text = StartTimeBox.Text?.Trim() ?? "";
        if (TimeOnly.TryParse(text, CultureInfo.InvariantCulture, out TimeOnly t)
            || TimeOnly.TryParse(text, CultureInfo.CurrentCulture, out t))
        {
            DateTime today = DateTime.Today.Add(t.ToTimeSpan());

            if (_latestStartLocal.HasValue)
            {
                DateTime baseDate = _latestStartLocal.Value.Date;
                DateTime candidate = baseDate.Add(t.ToTimeSpan());
                if (candidate > DateTime.Now) candidate = candidate.AddDays(-1);

                TimeSpan diff = _latestStartLocal.Value - candidate;
                TimeSpan newTotal = _originalTotalElapsed + diff;
                if (newTotal >= TimeSpan.Zero)
                {
                    _currentTimeSpan = newTotal;
                    _isUpdatingInternally = true;
                    TimeValueBox.Text = FormatDuration(newTotal);
                    _isUpdatingInternally = false;
                    UpdateAdjustmentSummary();
                    SaveButton.IsEnabled = true;
                    return;
                }
            }
            else
            {
                if (today > DateTime.Now) today = today.AddDays(-1);
                TimeSpan elapsed = DateTime.Now - today;
                if (elapsed >= TimeSpan.Zero)
                {
                    _currentTimeSpan = elapsed;
                    _isUpdatingInternally = true;
                    TimeValueBox.Text = FormatDuration(elapsed);
                    _isUpdatingInternally = false;
                    UpdateAdjustmentSummary();
                    SaveButton.IsEnabled = true;
                    return;
                }
            }
        }

        SaveButton.IsEnabled = false;
    }

    private void QuickAdjust_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            if (tag == "zero")
            {
                _currentTimeSpan = TimeSpan.Zero;
            }
            else if (int.TryParse(tag, out int secondsDelta))
            {
                _currentTimeSpan += TimeSpan.FromSeconds(secondsDelta);
                if (_currentTimeSpan < TimeSpan.Zero)
                {
                    _currentTimeSpan = TimeSpan.Zero;
                }
            }

            _isUpdatingInternally = true;
            TimeValueBox.Text = FormatDuration(_currentTimeSpan);
            _isUpdatingInternally = false;
            UpdateAdjustmentSummary();
            SaveButton.IsEnabled = true;
        }
    }

    private void PauseResumeButton_Click(object? sender, RoutedEventArgs e)
    {
        _isRunning = !_isRunning;
        UpdateRunningButton();
    }

    private void DiscardRecordsButton_Click(object? sender, RoutedEventArgs e)
    {
        _discardRecords = true;
        UpdateRecordsStatus();
    }

    private async void DeleteTimerButton_Click(object? sender, RoutedEventArgs e)
    {
        bool confirmed = await ConfirmationDialogWindow.ShowAsync(
            this,
            "Delete Timer",
            $"Delete {_timer.DisplayName}?",
            "Are you sure you want to delete and close this timer?",
            "Delete",
            destructive: true);

        if (confirmed)
        {
            DeleteTimerRequested = true;
            WasSaved = true;
            Close(true);
        }
    }

    private void ToggleProjectInputButton_Click(object? sender, RoutedEventArgs e)
    {
        _isCustomProjectMode = !_isCustomProjectMode;
        if (_isCustomProjectMode)
        {
            ProjectSelector.IsVisible = false;
            CustomProjectBox.IsVisible = true;
            ToggleProjectInputButton.Content = "List...";
            UpdateIdleStopCheck();
            CustomProjectBox.Focus();
        }
        else
        {
            CustomProjectBox.IsVisible = false;
            ProjectSelector.IsVisible = true;
            ToggleProjectInputButton.Content = "New...";
            UpdateIdleStopCheck();
            ProjectSelector.Focus();
        }
    }

    private void ProjectSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateIdleStopCheck();
    }

    private void CustomProjectBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isCustomProjectMode)
        {
            UpdateIdleStopCheck();
        }
    }

    private void ToggleCategoryInputButton_Click(object? sender, RoutedEventArgs e)
    {
        _isCustomCategoryMode = !_isCustomCategoryMode;
        if (_isCustomCategoryMode)
        {
            CategorySelector.IsVisible = false;
            CustomCategoryBox.IsVisible = true;
            ToggleCategoryInputButton.Content = "List...";
            CustomCategoryBox.Focus();
        }
        else
        {
            CustomCategoryBox.IsVisible = false;
            CategorySelector.IsVisible = true;
            ToggleCategoryInputButton.Content = "New...";
            CategorySelector.Focus();
        }
    }

    private void CategorySelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
    }

    private void CustomCategoryBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
    }

    private string? GetCurrentSelectedProject()
    {
        if (_isCustomProjectMode)
        {
            string text = CustomProjectBox.Text?.Trim() ?? "";
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        if (ProjectSelector.SelectedIndex > 0 && ProjectSelector.SelectedItem is string proj)
        {
            return proj;
        }
        return null;
    }

    private void UpdateIdleStopCheck()
    {
        if (IdleStopForProjectCheck == null) return;
        if (_settings == null)
        {
            IdleStopForProjectCheck.IsVisible = false;
            return;
        }

        IdleStopForProjectCheck.IsVisible = true;
        string? project = GetCurrentSelectedProject();
        bool hasProject = !string.IsNullOrWhiteSpace(project);

        bool isActive = _settings.IsIdleStopActiveForProject(project, out int timeoutMinutes, out _);
        IdleStopForProjectCheck.IsChecked = isActive;

        if (hasProject)
        {
            IdleStopForProjectCheck.Content = $"Idle stop for \"{project}\" ({timeoutMinutes}m timeout)";
        }
        else
        {
            IdleStopForProjectCheck.Content = $"Idle stop for unassigned timers ({timeoutMinutes}m timeout)";
        }
    }

    private void IdleStopForProjectCheck_Click(object? sender, RoutedEventArgs e)
    {
        if (_settings == null) return;
        bool isChecked = IdleStopForProjectCheck.IsChecked == true;
        string? project = GetCurrentSelectedProject();
        if (string.IsNullOrWhiteSpace(project))
        {
            _settings.IdleStopUnnamedTimers = isChecked;
        }
        else
        {
            int timeout = _settings.DefaultIdleStopTimeoutMinutes;
            if (_settings.ProjectIdleRules.TryGetValue(project, out var rule) && rule.IdleMinutes > 0)
            {
                timeout = rule.IdleMinutes;
            }
            _settings.SetProjectIdleRule(project, isChecked, timeout);
        }
        SettingsStore.Save(_settings);
        UpdateIdleStopCheck();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void UndoPreviousEditButton_Click(object? sender, RoutedEventArgs e)
    {
        UndoRequested = true;
        WasSaved = true;
        Close(true);
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryParseDuration(TimeValueBox.Text, out TimeSpan duration))
        {
            return;
        }

        NewTimeValue = duration;
        DiscardRecordsRequested = _discardRecords;
        NewIsRunning = _isRunning;

        if (_isCustomProjectMode)
        {
            NewProjectName = CustomProjectBox.Text?.Trim() ?? "";
        }
        else if (ProjectSelector.SelectedIndex > 0 && ProjectSelector.SelectedItem is string proj)
        {
            NewProjectName = proj;
        }
        else
        {
            NewProjectName = "";
        }

        if (_isCustomCategoryMode)
        {
            string customCat = CustomCategoryBox.Text?.Trim() ?? "";
            NewCategory = string.IsNullOrWhiteSpace(customCat) ? "Work" : customCat;
        }
        else if (CategorySelector.SelectedItem is string cat && !string.IsNullOrWhiteSpace(cat))
        {
            NewCategory = cat.Trim();
        }
        else
        {
            NewCategory = "Work";
        }

        decimal boxScore = ScorePerHourBox.Value ?? 1.0m;
        NewScorePerHour = (boxScore < 0) ? 1.0 : (double)boxScore;

        WasSaved = true;
        Close(true);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(false);
        }
        else if (e.Key == Key.Enter && (e.KeyModifiers & KeyModifiers.Control) != 0)
        {
            e.Handled = true;
            SaveButton_Click(this, e);
        }
        else if (e.Key == Key.Z && (e.KeyModifiers & KeyModifiers.Control) != 0)
        {
            if (UndoPreviousEditButton.IsVisible)
            {
                e.Handled = true;
                UndoPreviousEditButton_Click(this, e);
            }
        }
    }

    public static string FormatDuration(TimeSpan time)
    {
        int totalHours = (int)time.TotalHours;
        return totalHours > 0
            ? string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:mm\\:ss}", totalHours, time)
            : string.Format(CultureInfo.InvariantCulture, "{0:mm\\:ss}", time);
    }

    public static bool TryParseDuration(string? input, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(input)) return false;
        input = input.Trim();

        if (input is "0" or "00:00" or "00:00:00")
        {
            duration = TimeSpan.Zero;
            return true;
        }

        var parts = input.Split(':');
        if (parts.Length == 3
            && int.TryParse(parts[0], out int h)
            && int.TryParse(parts[1], out int m)
            && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double s))
        {
            if (h >= 0 && m >= 0 && s >= 0)
            {
                duration = TimeSpan.FromHours(h) + TimeSpan.FromMinutes(m) + TimeSpan.FromSeconds(s);
                return true;
            }
        }

        if (parts.Length == 2
            && int.TryParse(parts[0], out int min)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double sec))
        {
            if (min >= 0 && sec >= 0)
            {
                duration = TimeSpan.FromMinutes(min) + TimeSpan.FromSeconds(sec);
                return true;
            }
        }

        var parsed = CountdownParser.Parse(input, DateTime.Now);
        if (parsed.Success && parsed.Duration.HasValue && parsed.Duration.Value >= TimeSpan.Zero)
        {
            duration = parsed.Duration.Value;
            return true;
        }

        if (TimeSpan.TryParse(input, CultureInfo.InvariantCulture, out duration) && duration >= TimeSpan.Zero)
        {
            return true;
        }

        return false;
    }
}
