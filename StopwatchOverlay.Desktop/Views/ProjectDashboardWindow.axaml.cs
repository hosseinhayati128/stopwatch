using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace StopwatchOverlay.Desktop.Views;

public partial class ProjectDashboardWindow : Window
{
    private const int RecordsPerPage = 20;
    private const int HeatmapWeekCount = 53;
    private const double HeatmapCellSize = 13;
    private const double HeatmapCellStep = 17;

    private static readonly Color[] ProjectPalette =
    [
        Color.FromRgb(66, 185, 232),
        Color.FromRgb(126, 204, 139),
        Color.FromRgb(244, 178, 77),
        Color.FromRgb(180, 139, 236),
        Color.FromRgb(235, 111, 146),
        Color.FromRgb(82, 201, 184),
        Color.FromRgb(239, 130, 84),
        Color.FromRgb(116, 151, 232)
    ];

    private readonly Func<ProjectHistoryView> _historyProvider;
    private readonly Func<string, DateTime, DateTime, ProjectRecordMutationResult> _addRecord;
    private readonly Func<Guid, string, DateTime, DateTime, ProjectRecordMutationResult> _updateRecord;
    private readonly Func<Guid, ProjectRecordMutationResult> _deleteRecord;
    private readonly Func<string, ProjectDeletionResult>? _deleteProject;
    private readonly Func<bool> _canMutateRecords;
    private readonly Func<string?> _recordsPersistenceWarning;
    private readonly AppSettings? _settings;
    private readonly DispatcherTimer _liveRefreshTimer;
    private readonly Dictionary<DateTime, Button> _heatmapCells = [];
    private ProjectHistoryView? _history;
    private IReadOnlyList<DisplayInterval> _records = [];
    private DashboardRange _selectedRange = DashboardRange.Day;
    private DateTime? _selectedDayLocal;
    private DateTime _latestLocalDate = DateTime.Today;
    private bool _followToday = true;
    private string? _selectedProjectKey;
    private bool _updatingProjectFilter;
    private int _recordsPageIndex;
    private DateTime? _heatmapFirstDate;
    private DateTime? _heatmapRenderedToday;
    private bool _recordDialogOpen;
    private bool _refreshFailed;

    public ProjectDashboardWindow()
        : this(() => new ProjectTimeHistory().CreateView(DateTime.UtcNow))
    {
    }

    public ProjectDashboardWindow(ProjectHistoryView history)
        : this(
            () => history,
            (_, _, _) => new ProjectRecordMutationResult(ProjectRecordMutationStatus.NotFound),
            (_, _, _, _) => new ProjectRecordMutationResult(ProjectRecordMutationStatus.NotFound),
            _ => new ProjectRecordMutationResult(ProjectRecordMutationStatus.NotFound),
            () => false,
            () => "Record editing is unavailable in this read-only dashboard.")
    {
    }

    public ProjectDashboardWindow(Func<ProjectHistoryView> historyProvider)
        : this(
            historyProvider,
            (_, _, _) => new ProjectRecordMutationResult(ProjectRecordMutationStatus.NotFound),
            (_, _, _, _) => new ProjectRecordMutationResult(ProjectRecordMutationStatus.NotFound),
            _ => new ProjectRecordMutationResult(ProjectRecordMutationStatus.NotFound),
            () => false,
            () => "Record editing is unavailable in this read-only dashboard.")
    {
    }

    public ProjectDashboardWindow(
        Func<ProjectHistoryView> historyProvider,
        Func<string, DateTime, DateTime, ProjectRecordMutationResult> addRecord,
        Func<Guid, string, DateTime, DateTime, ProjectRecordMutationResult> updateRecord,
        Func<Guid, ProjectRecordMutationResult> deleteRecord,
        Func<bool> canMutateRecords,
        Func<string?> recordsPersistenceWarning,
        AppSettings? settings = null,
        Func<string, ProjectDeletionResult>? deleteProject = null)
    {
        ArgumentNullException.ThrowIfNull(historyProvider);
        ArgumentNullException.ThrowIfNull(addRecord);
        ArgumentNullException.ThrowIfNull(updateRecord);
        ArgumentNullException.ThrowIfNull(deleteRecord);
        ArgumentNullException.ThrowIfNull(canMutateRecords);
        ArgumentNullException.ThrowIfNull(recordsPersistenceWarning);

        _historyProvider = historyProvider;
        _addRecord = addRecord;
        _updateRecord = updateRecord;
        _deleteRecord = deleteRecord;
        _canMutateRecords = canMutateRecords;
        _recordsPersistenceWarning = recordsPersistenceWarning;
        _settings = settings;
        _deleteProject = deleteProject;

        InitializeComponent();

        _liveRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _liveRefreshTimer.Tick += LiveRefreshTimer_Tick;

        Loaded += Window_Loaded;
        Closed += (_, _) => _liveRefreshTimer.Stop();

        SelectRange(DashboardRange.Day);
        RefreshData();
    }

    private void Window_Loaded(object? sender, RoutedEventArgs e)
    {
        HeatmapScrollViewer.ScrollToEnd();
    }

    private void LiveRefreshTimer_Tick(object? sender, EventArgs e)
    {
        if (_recordDialogOpen) return;
        RefreshData(isLiveTick: true);
    }

    private void RefreshButton_Click(object? sender, RoutedEventArgs e)
    {
        RefreshData();
    }

    public void RefreshData(bool isLiveTick = false)
    {
        try
        {
            ProjectHistoryView history = _historyProvider();
            ApplyData(history);
            _refreshFailed = false;

            if (!_liveRefreshTimer.IsEnabled)
            {
                _liveRefreshTimer.Start();
            }
        }
        catch (Exception ex)
        {
            if (!_refreshFailed)
            {
                CrashLogger.LogRecoverable(ex, "ProjectDashboardWindow.RefreshData");
                _refreshFailed = true;
            }
        }
    }

    private void ApplyData(ProjectHistoryView history)
    {
        _history = history;
        DateTime asOfUtc = EnsureUtc(history.AsOfUtc);
        DateTime asOfLocal = asOfUtc.ToLocalTime();
        _latestLocalDate = asOfLocal.Date;

        if (_followToday && _selectedRange == DashboardRange.Day)
        {
            _selectedDayLocal = _latestLocalDate;
        }

        DateTime targetLocalDay = _selectedRange == DashboardRange.Day
            ? (_selectedDayLocal ?? _latestLocalDate)
            : _latestLocalDate;

        DashboardUtcRange utcRange = ProjectDashboardAnalytics.CreateRange(
            _selectedRange,
            targetLocalDay,
            asOfUtc,
            TimeZoneInfo.Local);

        UpdateProjectFilterSelector(history);

        ProjectFilterOption? selectedProject = ProjectFilterSelector.SelectedItem as ProjectFilterOption;
        _selectedProjectKey = selectedProject?.Key;

        DeleteProjectButton.IsVisible = _deleteProject != null && !string.IsNullOrWhiteSpace(_selectedProjectKey);

        List<DisplayInterval> intervals = CollectIntervals(history, utcRange, selectedProject?.Key, asOfUtc);
        _records = intervals;

        UpdateSummaryKPIs(intervals, history, asOfUtc);
        UpdateRangeHeader(asOfLocal, targetLocalDay);

        RenderProjectBars(intervals);
        RenderDailyBars(BuildDayTotals(intervals));
        RenderHeatmap(history, asOfLocal, selectedProject);
        RenderTimeline(intervals);
        RenderRecords();
        UpdatePersistenceWarning();
    }

    private void UpdateRangeHeader(DateTime asOfLocal, DateTime targetLocalDay)
    {
        string rangeTitle = _selectedRange switch
        {
            DashboardRange.Day => targetLocalDay == asOfLocal.Date
                ? "Today"
                : targetLocalDay.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture),
            DashboardRange.SevenDays => "Last 7 days",
            DashboardRange.ThirtyDays => "Last 30 days",
            DashboardRange.AllTime => "All time",
            _ => "Selected period"
        };

        RangeHeadingText.Text = rangeTitle;
        UpdatedText.Text = $"Updated {asOfLocal:HH:mm:ss}";
    }

    private void UpdateSummaryKPIs(List<DisplayInterval> intervals, ProjectHistoryView history, DateTime asOfUtc)
    {
        TimeSpan totalTracked = TimeSpan.FromTicks(intervals.Sum(i => i.Duration.Ticks));
        TotalTrackedText.Text = FormatCompactDuration(totalTracked);

        SessionCountText.Text = intervals.Count.ToString(CultureInfo.CurrentCulture);

        int projectCount = intervals
            .Select(i => i.ProjectKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        ProjectCountText.Text = projectCount.ToString(CultureInfo.CurrentCulture);

        int activeCount = history.Intervals.Count(i => !i.EndUtc.HasValue);
        ActiveCountText.Text = activeCount.ToString(CultureInfo.CurrentCulture);
        ActiveStatusDot.IsVisible = activeCount > 0;
    }

    private void UpdateProjectFilterSelector(ProjectHistoryView history)
    {
        _updatingProjectFilter = true;
        try
        {
            string? currentKey = (ProjectFilterSelector.SelectedItem as ProjectFilterOption)?.Key;

            var options = new List<ProjectFilterOption>
            {
                new(null, "All projects")
            };

            foreach (var proj in history.Projects.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(proj.Name))
                {
                    options.Add(new ProjectFilterOption(proj.Key, proj.Name));
                }
            }

            ProjectFilterSelector.ItemsSource = options;

            var match = options.FirstOrDefault(o => string.Equals(o.Key, currentKey, StringComparison.OrdinalIgnoreCase))
                        ?? options[0];
            ProjectFilterSelector.SelectedItem = match;
        }
        finally
        {
            _updatingProjectFilter = false;
        }
    }

    private void ProjectFilterSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingProjectFilter) return;
        RefreshData();
    }

    private void SelectRange(DashboardRange range)
    {
        _selectedRange = range;
        if (range == DashboardRange.Day)
        {
            _followToday = _selectedDayLocal == null || _selectedDayLocal == _latestLocalDate;
        }
        else
        {
            _followToday = false;
        }

        DayButton.Classes.Set("primary", range == DashboardRange.Day);
        SevenDaysButton.Classes.Set("primary", range == DashboardRange.SevenDays);
        ThirtyDaysButton.Classes.Set("primary", range == DashboardRange.ThirtyDays);
        AllTimeButton.Classes.Set("primary", range == DashboardRange.AllTime);

        PreviousDayButton.IsEnabled = range == DashboardRange.Day;
        NextDayButton.IsEnabled = range == DashboardRange.Day && _selectedDayLocal < _latestLocalDate;

        RefreshData();
    }

    private void RangeButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && Enum.TryParse<DashboardRange>(tag, out var range))
        {
            SelectRange(range);
        }
    }

    private void PreviousDayButton_Click(object? sender, RoutedEventArgs e)
    {
        DateTime current = _selectedDayLocal ?? _latestLocalDate;
        _selectedDayLocal = current.AddDays(-1);
        _followToday = false;
        SelectRange(DashboardRange.Day);
    }

    private void NextDayButton_Click(object? sender, RoutedEventArgs e)
    {
        DateTime current = _selectedDayLocal ?? _latestLocalDate;
        if (current < _latestLocalDate)
        {
            _selectedDayLocal = current.AddDays(1);
            _followToday = _selectedDayLocal == _latestLocalDate;
            SelectRange(DashboardRange.Day);
        }
    }

    // ==========================================
    // CHARTS RENDERING
    // ==========================================

    private void RenderProjectBars(IReadOnlyList<DisplayInterval> intervals)
    {
        ProjectBarsPanel.Children.Clear();
        var totals = intervals
            .GroupBy(item => item.ProjectKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ProjectTotal(
                group.Key,
                group.First().ProjectName,
                TimeSpan.FromTicks(group.Sum(item => item.Duration.Ticks))))
            .OrderByDescending(item => item.Duration)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (totals.Count == 0)
        {
            ProjectBarsPanel.Children.Add(CreateMutedMessage("No project time yet."));
            return;
        }

        double maximumTicks = Math.Max(1, totals[0].Duration.Ticks);
        foreach (ProjectTotal project in totals)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 13) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Spacing = 8 };
            label.Children.Add(new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = GetProjectBrush(project.Key),
                VerticalAlignment = VerticalAlignment.Center
            });
            string projectCat = _history?.Projects.FirstOrDefault(p => string.Equals(p.Key, project.Key, StringComparison.OrdinalIgnoreCase))?.Category ?? "";
            string barDisplay = !string.IsNullOrWhiteSpace(projectCat) ? $"{project.Name} [{projectCat}]" : project.Name;
            var nameBlock = new TextBlock
            {
                Text = barDisplay,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 118,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (Application.Current?.TryFindResource("PrimaryTextBrush", out var pb) == true && pb is IBrush pBrush)
                nameBlock.Foreground = pBrush;
            ToolTip.SetTip(nameBlock, barDisplay);
            label.Children.Add(nameBlock);
            row.Children.Add(label);

            var track = new Border
            {
                Height = 10,
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            if (Application.Current?.TryFindResource("SurfaceRaisedBrush", out var srb) == true && srb is IBrush srBrush)
                track.Background = srBrush;

            Grid.SetColumn(track, 1);
            var proportion = new Grid();
            double value = project.Duration.Ticks / maximumTicks;
            proportion.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.004, value), GridUnitType.Star) });
            proportion.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0, 1 - value), GridUnitType.Star) });

            var fill = new Border
            {
                Background = GetProjectBrush(project.Key),
                CornerRadius = new CornerRadius(5)
            };
            proportion.Children.Add(fill);
            track.Child = proportion;
            row.Children.Add(track);

            var duration = new TextBlock
            {
                Text = FormatCompactDuration(project.Duration),
                FontSize = 12,
                MinWidth = 62,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sb) == true && sb is IBrush sBrush)
                duration.Foreground = sBrush;

            Grid.SetColumn(duration, 2);
            row.Children.Add(duration);
            ProjectBarsPanel.Children.Add(row);
        }
    }

    private void RenderDailyBars(IReadOnlyList<DayTotal> totals)
    {
        DailyBarsPanel.Children.Clear();
        if (totals.Count == 0)
        {
            DailyBarsPanel.Children.Add(CreateMutedMessage("No daily totals yet."));
            return;
        }

        double maximumTicks = Math.Max(1, totals.Max(item => item.Duration.Ticks));
        foreach (DayTotal day in totals)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(74) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var dateText = new TextBlock
            {
                Text = day.Date.ToString("MMM d", CultureInfo.CurrentCulture),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sb) == true && sb is IBrush sBrush)
                dateText.Foreground = sBrush;
            row.Children.Add(dateText);

            var track = new Border
            {
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 9, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            if (Application.Current?.TryFindResource("SurfaceRaisedBrush", out var srb) == true && srb is IBrush srBrush)
                track.Background = srBrush;

            Grid.SetColumn(track, 1);
            var proportion = new Grid();
            double value = day.Duration.Ticks / maximumTicks;
            proportion.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.004, value), GridUnitType.Star) });
            proportion.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0, 1 - value), GridUnitType.Star) });

            var fill = new Border
            {
                CornerRadius = new CornerRadius(4)
            };
            if (Application.Current?.TryFindResource("AccentBrush", out var ab) == true && ab is IBrush aBrush)
                fill.Background = aBrush;
            else
                fill.Background = Brushes.DodgerBlue;

            proportion.Children.Add(fill);
            track.Child = proportion;
            row.Children.Add(track);

            var valueText = new TextBlock
            {
                Text = FormatCompactDuration(day.Duration),
                FontSize = 12,
                MinWidth = 52,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sb2) == true && sb2 is IBrush sBrush2)
                valueText.Foreground = sBrush2;

            Grid.SetColumn(valueText, 2);
            row.Children.Add(valueText);
            DailyBarsPanel.Children.Add(row);
        }
    }

    // ==========================================
    // HEATMAP RENDERING
    // ==========================================

    private void RenderHeatmap(
        ProjectHistoryView history,
        DateTime asOfLocal,
        ProjectFilterOption? selectedProject)
    {
        DateTime today = asOfLocal.Date;
        int daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
        DateTime currentWeekStart = today.AddDays(-daysSinceMonday);
        DateTime firstDate = currentWeekStart.AddDays(-(HeatmapWeekCount - 1) * 7);
        DateTime lastDate = currentWeekStart.AddDays(6);

        IReadOnlyList<HeatmapDayValue> values = ProjectDashboardAnalytics.BuildHeatmap(
            history,
            firstDate,
            lastDate,
            selectedProject?.Key,
            TimeZoneInfo.Local);

        long maximumTicks = values
            .Where(item => item.Date <= today)
            .Select(item => item.Duration.Ticks)
            .DefaultIfEmpty(0)
            .Max();

        string scope = selectedProject?.Key is null
            ? "All projects"
            : selectedProject.Name;
        HeatmapScopeText.Text = $"{scope} · last 12 months";
        ToolTip.SetTip(HeatmapScopeText, HeatmapScopeText.Text);
        HeatmapEmptyText.Text = maximumTicks == 0
            ? selectedProject?.Key is null
                ? "No tracked time for any project in the last 12 months."
                : $"No tracked time for {scope} in the last 12 months."
            : "";

        IBrush[] levelBrushes = CreateHeatmapLevelBrushes();
        EnsureHeatmapGrid(firstDate, today);

        foreach (HeatmapDayValue value in values)
        {
            if (!_heatmapCells.TryGetValue(value.Date, out Button? cell)) continue;

            bool isFuture = value.Date > today;
            bool isSelected = _selectedRange == DashboardRange.Day && _selectedDayLocal == value.Date;
            int level = GetHeatmapLevel(value.Duration.Ticks, maximumTicks);
            string tip = CreateHeatmapToolTip(value, scope, isFuture);

            cell.Background = isFuture ? levelBrushes[0] : levelBrushes[level];
            if (isSelected)
            {
                cell.BorderThickness = new Thickness(2);
                if (Application.Current?.TryFindResource("AccentBrush", out var ab) == true && ab is IBrush aBrush)
                    cell.BorderBrush = aBrush;
            }
            else
            {
                cell.BorderThickness = new Thickness(0.5);
                if (Application.Current?.TryFindResource("BorderBrush", out var bb) == true && bb is IBrush bBrush)
                    cell.BorderBrush = bBrush;
            }

            cell.Opacity = isFuture ? 0.3 : 1.0;
            cell.Cursor = isFuture ? new Cursor(StandardCursorType.Arrow) : new Cursor(StandardCursorType.Hand);
            cell.IsEnabled = !isFuture;
            ToolTip.SetTip(cell, tip);
        }

        _heatmapRenderedToday = today;
        RenderHeatmapLegend(levelBrushes);
    }

    private void EnsureHeatmapGrid(DateTime firstDate, DateTime today)
    {
        if (_heatmapFirstDate == firstDate && _heatmapCells.Count == HeatmapWeekCount * 7)
            return;

        _heatmapFirstDate = firstDate;
        _heatmapCells.Clear();
        HeatmapHost.Children.Clear();
        HeatmapHost.ColumnDefinitions.Clear();
        HeatmapHost.RowDefinitions.Clear();

        HeatmapHost.Width = 42 + (HeatmapWeekCount * HeatmapCellStep);
        HeatmapHost.Height = 25 + (7 * HeatmapCellStep);
        HeatmapHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });

        for (int week = 0; week < HeatmapWeekCount; week++)
        {
            HeatmapHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(HeatmapCellStep) });
        }

        HeatmapHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25) });
        for (int day = 0; day < 7; day++)
        {
            HeatmapHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(HeatmapCellStep) });
        }

        AddHeatmapLabels(firstDate);

        for (int week = 0; week < HeatmapWeekCount; week++)
        {
            for (int day = 0; day < 7; day++)
            {
                DateTime date = firstDate.AddDays((week * 7) + day);
                var cell = new Button
                {
                    Width = HeatmapCellSize,
                    Height = HeatmapCellSize,
                    Padding = new Thickness(0),
                    CornerRadius = new CornerRadius(2),
                    Tag = date
                };

                cell.Click += (_, _) =>
                {
                    _selectedDayLocal = date;
                    _followToday = date == _latestLocalDate;
                    SelectRange(DashboardRange.Day);
                };

                Grid.SetColumn(cell, week + 1);
                Grid.SetRow(cell, day + 1);
                HeatmapHost.Children.Add(cell);
                _heatmapCells[date] = cell;
            }
        }
    }

    private void AddHeatmapLabels(DateTime firstDate)
    {
        string[] dayLabels = ["Mon", "", "Wed", "", "Fri", "", ""];
        for (int day = 0; day < 7; day++)
        {
            if (string.IsNullOrEmpty(dayLabels[day])) continue;
            var lbl = new TextBlock
            {
                Text = dayLabels[day],
                FontSize = 9.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sb) == true && sb is IBrush sBrush)
                lbl.Foreground = sBrush;

            Grid.SetColumn(lbl, 0);
            Grid.SetRow(lbl, day + 1);
            HeatmapHost.Children.Add(lbl);
        }

        string lastMonth = "";
        for (int week = 0; week < HeatmapWeekCount; week++)
        {
            DateTime weekDate = firstDate.AddDays(week * 7);
            string month = weekDate.ToString("MMM", CultureInfo.CurrentCulture);
            if (month != lastMonth && weekDate.Day <= 7)
            {
                lastMonth = month;
                var monthLbl = new TextBlock
                {
                    Text = month,
                    FontSize = 9.5,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sb) == true && sb is IBrush sBrush)
                    monthLbl.Foreground = sBrush;

                Grid.SetColumn(monthLbl, week + 1);
                Grid.SetRow(monthLbl, 0);
                HeatmapHost.Children.Add(monthLbl);
            }
        }
    }

    private void RenderHeatmapLegend(IBrush[] levelBrushes)
    {
        HeatmapLegendPanel.Children.Clear();
        var lessText = new TextBlock { Text = "Less ", FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
        if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sb) == true && sb is IBrush sBrush)
            lessText.Foreground = sBrush;
        HeatmapLegendPanel.Children.Add(lessText);

        for (int i = 0; i < levelBrushes.Length; i++)
        {
            var b = new Border
            {
                Width = 10,
                Height = 10,
                CornerRadius = new CornerRadius(2),
                Background = levelBrushes[i],
                Margin = new Thickness(1, 0, 1, 0)
            };
            HeatmapLegendPanel.Children.Add(b);
        }

        var moreText = new TextBlock { Text = " More", FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
        if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sb2) == true && sb2 is IBrush sBrush2)
            moreText.Foreground = sBrush2;
        HeatmapLegendPanel.Children.Add(moreText);
    }

    private static IBrush[] CreateHeatmapLevelBrushes()
    {
        return
        [
            new SolidColorBrush(Color.FromArgb(40, 100, 120, 140)),  // Level 0 (none)
            new SolidColorBrush(Color.FromArgb(120, 36, 120, 160)),  // Level 1
            new SolidColorBrush(Color.FromArgb(180, 52, 152, 219)),  // Level 2
            new SolidColorBrush(Color.FromArgb(220, 41, 128, 185)),  // Level 3
            new SolidColorBrush(Color.FromRgb(66, 185, 232))          // Level 4 (peak)
        ];
    }

    private static int GetHeatmapLevel(long ticks, long maxTicks)
    {
        if (ticks <= 0 || maxTicks <= 0) return 0;
        double ratio = (double)ticks / maxTicks;
        if (ratio < 0.25) return 1;
        if (ratio < 0.50) return 2;
        if (ratio < 0.75) return 3;
        return 4;
    }

    private static string CreateHeatmapToolTip(HeatmapDayValue val, string scope, bool isFuture)
    {
        if (isFuture) return $"{val.Date:dddd, MMMM d, yyyy}\n(Future date)";
        string dur = val.Duration.Ticks > 0 ? FormatCompactDuration(val.Duration) : "No activity";
        return $"{val.Date:dddd, MMMM d, yyyy}\n{dur} · {scope}\n{val.RecordCount} session{(val.RecordCount == 1 ? "" : "s")}";
    }

    // ==========================================
    // TIMELINE RENDERING
    // ==========================================

    private void RenderTimeline(IReadOnlyList<DisplayInterval> intervals)
    {
        TimelineDaysPanel.Children.Clear();
        List<DayFragment> fragments = intervals
            .SelectMany(SplitByLocalDay)
            .OrderByDescending(item => item.Date)
            .ThenBy(item => item.StartLocal)
            .ToList();

        if (fragments.Count == 0)
        {
            TimelineDaysPanel.Children.Add(CreateMutedMessage("No sessions to place on the timeline."));
            return;
        }

        foreach (IGrouping<DateTime, DayFragment> dayGroup in fragments.GroupBy(item => item.Date))
        {
            List<TimelineItem> items = AssignTimelineLanes(dayGroup.ToList());
            int laneCount = Math.Max(1, items.Max(item => item.Lane) + 1);
            DayFragment dayBounds = items[0].Fragment;

            var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var dateLbl = new TextBlock
            {
                Text = dayGroup.Key.ToString("ddd, MMM d", CultureInfo.CurrentCulture),
                FontSize = 12,
                Margin = new Thickness(0, 4, 12, 0)
            };
            if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sb) == true && sb is IBrush sBrush)
                dateLbl.Foreground = sBrush;
            row.Children.Add(dateLbl);

            var track = new Grid
            {
                Height = Math.Max(42, laneCount * 25 + 18),
                ClipToBounds = true
            };
            if (Application.Current?.TryFindResource("SurfaceRaisedBrush", out var srb) == true && srb is IBrush srBrush)
                track.Background = srBrush;

            Grid.SetColumn(track, 1);
            var guides = new Canvas { IsHitTestVisible = false };
            var segments = new Canvas();
            track.Children.Add(guides);
            track.Children.Add(segments);
            row.Children.Add(track);

            void DrawTrack()
            {
                double width = track.Bounds.Width;
                double height = track.Bounds.Height;
                DrawTimelineGuides(guides, width, height, dayBounds);
                DrawTimelineSegments(segments, items, width);
            }

            track.Loaded += (_, _) => DrawTrack();
            track.SizeChanged += (_, _) => DrawTrack();
            TimelineDaysPanel.Children.Add(row);
        }
    }

    private void DrawTimelineGuides(Canvas canvas, double width, double height, DayFragment day)
    {
        canvas.Children.Clear();
        TimeSpan dayDuration = day.DayEndUtc - day.DayStartUtc;
        if (width <= 0 || dayDuration <= TimeSpan.Zero) return;

        for (int hour = 0; hour <= 24; hour += 6)
        {
            DateTime boundaryUtc = LocalBoundaryToUtc(day.Date.AddHours(hour));
            double fraction = Math.Clamp((boundaryUtc - day.DayStartUtc).TotalSeconds / dayDuration.TotalSeconds, 0, 1);
            double x = width * fraction;

            var label = new TextBlock
            {
                Text = hour == 24 ? "24:00" : $"{hour:00}:00",
                FontSize = 9
            };
            if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sb) == true && sb is IBrush sBrush)
                label.Foreground = sBrush;

            Canvas.SetLeft(label, Math.Clamp(x - (hour == 0 ? 0 : hour == 24 ? 30 : 14), 0, Math.Max(0, width - 30)));
            Canvas.SetTop(label, 1);
            canvas.Children.Add(label);

            if (hour is > 0 and < 24)
            {
                var guide = new Line
                {
                    StartPoint = new Point(x, 15),
                    EndPoint = new Point(x, height),
                    StrokeThickness = 1
                };
                if (Application.Current?.TryFindResource("BorderBrush", out var bb) == true && bb is IBrush bBrush)
                    guide.Stroke = bBrush;
                canvas.Children.Add(guide);
            }
        }
    }

    private void DrawTimelineSegments(Canvas canvas, IReadOnlyList<TimelineItem> items, double width)
    {
        canvas.Children.Clear();
        if (width <= 0) return;

        foreach (TimelineItem item in items)
        {
            TimeSpan dayDuration = item.Fragment.DayEndUtc - item.Fragment.DayStartUtc;
            if (dayDuration <= TimeSpan.Zero) continue;

            double startFrac = Math.Clamp((item.Fragment.StartUtc - item.Fragment.DayStartUtc).TotalSeconds / dayDuration.TotalSeconds, 0, 1);
            double endFrac = Math.Clamp((item.Fragment.EndUtc - item.Fragment.DayStartUtc).TotalSeconds / dayDuration.TotalSeconds, 0, 1);
            double left = width * startFrac;
            double segWidth = Math.Max(3, width * (endFrac - startFrac));

            SolidColorBrush segBrush = GetProjectBrush(item.Fragment.ProjectKey);
            var segment = new Border
            {
                Width = segWidth,
                Height = 20,
                Background = segBrush,
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true
            };
            ToolTip.SetTip(segment, CreateTimelineToolTip(item.Fragment));

            if (segWidth >= 64)
            {
                segment.Child = new TextBlock
                {
                    Text = item.Fragment.ProjectName,
                    Foreground = Brushes.White,
                    FontSize = 10,
                    FontWeight = FontWeight.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(6, 2, 6, 2),
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            Canvas.SetLeft(segment, left);
            Canvas.SetTop(segment, item.Lane * 25 + 17);
            canvas.Children.Add(segment);
        }
    }

    private static string CreateTimelineToolTip(DayFragment fragment)
    {
        string end = fragment.IsLive && fragment.EndsAtIntervalEnd
            ? "Now"
            : FormatLocalTime(fragment.EndLocal, fragment.EndUtc);
        return $"{fragment.ProjectName}\n{FormatLocalTime(fragment.StartLocal, fragment.StartUtc)} – {end}\n{FormatDetailedDuration(fragment.Duration)}";
    }

    private static List<TimelineItem> AssignTimelineLanes(IReadOnlyList<DayFragment> fragments)
    {
        var laneEnds = new List<DateTime>();
        var result = new List<TimelineItem>(fragments.Count);
        foreach (DayFragment fragment in fragments.OrderBy(item => item.StartUtc))
        {
            int lane = laneEnds.FindIndex(end => end <= fragment.StartUtc);
            if (lane < 0)
            {
                lane = laneEnds.Count;
                laneEnds.Add(fragment.EndUtc);
            }
            else
            {
                laneEnds[lane] = fragment.EndUtc;
            }

            result.Add(new TimelineItem(fragment, lane));
        }

        return result;
    }

    // ==========================================
    // RECORDS RENDERING & CRUD
    // ==========================================

    private void RecordsButton_Click(object? sender, RoutedEventArgs e)
    {
        ProjectRecordsExpander.IsExpanded = true;
        RenderRecords();
        ProjectRecordsExpander.BringIntoView();
    }

    private void PreviousRecordsPageButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_recordsPageIndex <= 0) return;
        _recordsPageIndex--;
        RenderRecords();
    }

    private void NextRecordsPageButton_Click(object? sender, RoutedEventArgs e)
    {
        int pageCount = Math.Max(1, (_records.Count + RecordsPerPage - 1) / RecordsPerPage);
        if (_recordsPageIndex + 1 >= pageCount) return;
        _recordsPageIndex++;
        RenderRecords();
    }

    private void RenderRecords()
    {
        RecordsPanel.Children.Clear();
        if (_history == null)
        {
            RecordsPageStatusText.Text = "0 records";
            PreviousRecordsPageButton.IsEnabled = false;
            NextRecordsPageButton.IsEnabled = false;
            RecordsEmptyState.IsVisible = true;
            RecordsPanel.IsVisible = false;
            return;
        }

        bool canEdit = SafeCanMutateRecords();
        DateTime asOfUtc = EnsureUtc(_history.AsOfUtc);
        int pageCount = Math.Max(1, (_records.Count + RecordsPerPage - 1) / RecordsPerPage);
        _recordsPageIndex = Math.Clamp(_recordsPageIndex, 0, pageCount - 1);
        List<DisplayInterval> visibleRecords = _records
            .Skip(_recordsPageIndex * RecordsPerPage)
            .Take(RecordsPerPage)
            .ToList();

        int first = _records.Count == 0 ? 0 : (_recordsPageIndex * RecordsPerPage) + 1;
        int last = Math.Min(_records.Count, (_recordsPageIndex + 1) * RecordsPerPage);
        RecordsPageStatusText.Text = _records.Count == 0
            ? "0 records"
            : $"{first}–{last} of {_records.Count}";
        PreviousRecordsPageButton.IsEnabled = _recordsPageIndex > 0;
        NextRecordsPageButton.IsEnabled = _recordsPageIndex + 1 < pageCount;

        foreach (var group in visibleRecords
                     .GroupBy(interval => interval.StartLocal.Date)
                     .OrderByDescending(group => group.Key))
        {
            var header = new TextBlock
            {
                Text = group.Key.ToString("dddd, MMMM d", CultureInfo.CurrentCulture),
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(0, RecordsPanel.Children.Count == 0 ? 0 : 14, 0, 7)
            };
            if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sb) == true && sb is IBrush sBrush)
                header.Foreground = sBrush;
            RecordsPanel.Children.Add(header);

            foreach (DisplayInterval interval in group.OrderByDescending(item => item.StartUtc))
            {
                RecordsPanel.Children.Add(CreateRecordCard(interval, asOfUtc, canEdit));
            }
        }

        bool empty = _records.Count == 0;
        RecordsEmptyState.IsVisible = empty;
        RecordsPanel.IsVisible = !empty;

        ProjectFilterOption? selected = ProjectFilterSelector.SelectedItem as ProjectFilterOption;
        RecordsEmptyHeadingText.Text = selected?.Key == null
            ? "No project records in this period"
            : $"No {selected.Name} records in this period";
    }

    private Control CreateRecordCard(DisplayInterval interval, DateTime asOfUtc, bool canEdit)
    {
        ProjectWorkIntervalView record = interval.Source;
        DateTime startLocal = interval.StartLocal;
        DateTime endLocal = interval.EndLocal;

        var card = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 8),
            BorderThickness = new Thickness(1)
        };
        if (Application.Current?.TryFindResource("SurfaceRaisedBrush", out var srb) == true && srb is IBrush srBrush)
            card.Background = srBrush;
        if (Application.Current?.TryFindResource("BorderBrush", out var bb) == true && bb is IBrush bBrush)
            card.BorderBrush = bBrush;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 105 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 82 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var stripe = new Border
        {
            Width = 4,
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 1, 0, 1)
        };
        if (Application.Current?.TryFindResource("AccentBrush", out var ab) == true && ab is IBrush aBrush)
            stripe.Background = aBrush;
        Grid.SetColumn(stripe, 0);
        grid.Children.Add(stripe);

        string category = _history?.Projects.FirstOrDefault(p => string.Equals(p.Key, record.ProjectKey, StringComparison.OrdinalIgnoreCase))?.Category ?? "";
        string displayProject = !string.IsNullOrWhiteSpace(category) ? $"{record.ProjectName}  [{category}]" : record.ProjectName;
        var project = new TextBlock
        {
            Text = displayProject,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 10, 0)
        };
        if (Application.Current?.TryFindResource("PrimaryTextBrush", out var pb) == true && pb is IBrush pBrush)
            project.Foreground = pBrush;
        ToolTip.SetTip(project, displayProject);
        Grid.SetColumn(project, 1);
        grid.Children.Add(project);

        var date = new TextBlock
        {
            Text = startLocal.ToString("MMM d, yyyy", CultureInfo.CurrentCulture),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sb) == true && sb is IBrush sBrush)
            date.Foreground = sBrush;
        Grid.SetColumn(date, 2);
        grid.Children.Add(date);

        string startTimeLabel = FormatLocalTime(startLocal, interval.StartUtc);
        string endLabel = interval.IsLive
            ? "Now"
            : endLocal.Date == startLocal.Date
                ? FormatLocalTime(endLocal, interval.EndUtc)
                : $"{endLocal.ToString("MMM d", CultureInfo.CurrentCulture)}, {FormatLocalTime(endLocal, interval.EndUtc)}";
        string timeLabel = $"{startTimeLabel} – {endLabel}";
        var times = new TextBlock
        {
            Text = timeLabel,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sb2) == true && sb2 is IBrush sBrush2)
            times.Foreground = sBrush2;
        Grid.SetColumn(times, 3);
        grid.Children.Add(times);

        var duration = new TextBlock
        {
            Text = FormatDetailedDuration(interval.Duration),
            FontWeight = FontWeight.SemiBold,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (Application.Current?.TryFindResource("PrimaryTextBrush", out var pb2) == true && pb2 is IBrush pBrush2)
            duration.Foreground = pBrush2;
        Grid.SetColumn(duration, 4);
        grid.Children.Add(duration);

        // Action Buttons: Edit & Delete
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        if (!record.IsOpen && canEdit)
        {
            var editBtn = new Button
            {
                Content = "Edit",
                Classes = { "modern" },
                Height = 28,
                Padding = new Thickness(8, 2, 8, 2),
                FontSize = 11
            };
            editBtn.Click += async (_, _) => await EditRecordAsync(record);
            actions.Children.Add(editBtn);

            var deleteBtn = new Button
            {
                Content = "Delete",
                Classes = { "modern", "danger" },
                Height = 28,
                Padding = new Thickness(8, 2, 8, 2),
                FontSize = 11
            };
            deleteBtn.Click += async (_, _) => await DeleteRecordAsync(record);
            actions.Children.Add(deleteBtn);
        }
        else if (record.IsOpen)
        {
            var runningBadge = new Border
            {
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            if (Application.Current?.TryFindResource("DialogSurfaceBrush", out var dsb) == true && dsb is IBrush dsBrush)
                runningBadge.Background = dsBrush;
            var badgeText = new TextBlock { Text = "Running", FontSize = 10 };
            if (Application.Current?.TryFindResource("AccentBrush", out var abb) == true && abb is IBrush abBrush)
                badgeText.Foreground = abBrush;
            runningBadge.Child = badgeText;
            actions.Children.Add(runningBadge);
        }

        Grid.SetColumn(actions, 5);
        grid.Children.Add(actions);

        card.Child = grid;
        return card;
    }

    private async void AddRecordButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_history == null) return;
        _recordDialogOpen = true;
        try
        {
            var dialog = new ProjectRecordEditorWindow(
                _history.Projects,
                _selectedProjectKey,
                commit: (proj, sUtc, eUtc) => _addRecord(proj, sUtc, eUtc),
                initialLocalDate: _selectedRange == DashboardRange.Day ? _selectedDayLocal : null);

            bool confirmed = await dialog.ShowDialog<bool>(this);
            if (confirmed)
            {
                if (_selectedProjectKey != null && dialog.SavedRecord != null)
                {
                    _selectedProjectKey = dialog.SavedRecord.ProjectKey;
                }
                _recordsPageIndex = 0;
                RefreshData();
            }
        }
        finally
        {
            _recordDialogOpen = false;
        }
    }

    private async Task EditRecordAsync(ProjectWorkIntervalView record)
    {
        if (_history == null) return;
        _recordDialogOpen = true;
        try
        {
            var dialog = new ProjectRecordEditorWindow(
                _history.Projects,
                record.ProjectKey,
                record,
                (proj, sUtc, eUtc) => _updateRecord(record.Id, proj, sUtc, eUtc));

            bool confirmed = await dialog.ShowDialog<bool>(this);
            if (confirmed)
            {
                if (_selectedProjectKey != null && dialog.SavedRecord != null)
                {
                    _selectedProjectKey = dialog.SavedRecord.ProjectKey;
                }
                _recordsPageIndex = 0;
                RefreshData();
            }
        }
        finally
        {
            _recordDialogOpen = false;
        }
    }

    private async Task DeleteRecordAsync(ProjectWorkIntervalView record)
    {
        _recordDialogOpen = true;
        try
        {
            var dialog = new ProjectRecordDeleteWindow(record);
            bool confirmed = await dialog.ShowDialog<bool>(this);
            if (confirmed)
            {
                var result = _deleteRecord(record.Id);
                RefreshData();

                if (result.Status != ProjectRecordMutationStatus.Success)
                {
                    string msg = result.Status switch
                    {
                        ProjectRecordMutationStatus.NotFound => "That record no longer exists. The dashboard has been refreshed.",
                        ProjectRecordMutationStatus.OpenInterval => "An active record cannot be deleted. Pause its timer first.",
                        _ => "The record could not be deleted."
                    };
                    await ConfirmationDialogWindow.ShowAsync(this, "Delete Record", "Cannot Delete Record", msg, "OK");
                }
            }
        }
        finally
        {
            _recordDialogOpen = false;
        }
    }

    private async void DeleteProjectButton_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedProjectKey) || _deleteProject == null) return;
        var selectedOption = ProjectFilterSelector.SelectedItem as ProjectFilterOption;
        if (selectedOption == null) return;

        bool confirmed = await ConfirmationDialogWindow.ShowAsync(
            this,
            "Delete Project",
            $"Delete \"{selectedOption.Name}\"?",
            "Permanently delete this project and all its recorded sessions? This action cannot be undone.",
            "Delete Project",
            destructive: true);

        if (confirmed)
        {
            var result = _deleteProject(_selectedProjectKey);
            if (result.Status == ProjectDeletionStatus.Success)
            {
                _selectedProjectKey = null;
                RefreshData();
            }
            else if (result.Status == ProjectDeletionStatus.ActiveTimerRunning)
            {
                await ConfirmationDialogWindow.ShowAsync(
                    this,
                    "Delete Project",
                    "Cannot Delete Project",
                    $"Cannot delete '{selectedOption.Name}' because a timer is currently tracking this project. Pause or stop the timer first.",
                    "OK");
            }
            else
            {
                await ConfirmationDialogWindow.ShowAsync(
                    this,
                    "Delete Project",
                    "Cannot Delete Project",
                    $"Project '{selectedOption.Name}' was not found. The dashboard has been refreshed.",
                    "OK");
                RefreshData();
            }
        }
    }

    private async void ExportButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_settings == null)
        {
            await ConfirmationDialogWindow.ShowAsync(this, "Obsidian Export", "Settings Unavailable", "Settings are not available.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.ObsidianVaultFolder) || !Directory.Exists(_settings.ObsidianVaultFolder))
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Obsidian Vault or Target Folder",
                AllowMultiple = false
            });

            if (folders != null && folders.Count > 0)
            {
                _settings.ObsidianVaultFolder = folders[0].Path.LocalPath;
                SettingsStore.Save(_settings);
            }
            else
            {
                return;
            }
        }

        var result = ObsidianLogSync.SyncHistory(_historyProvider(), _settings);
        await ConfirmationDialogWindow.ShowAsync(
            this,
            "Obsidian Export",
            result.Success ? "Export Completed" : "Export Notice",
            result.Message ?? "Obsidian sync completed.",
            "OK");
    }

    private void UpdatePersistenceWarning()
    {
        string? warning = _recordsPersistenceWarning();
        if (!string.IsNullOrWhiteSpace(warning))
        {
            RecordsPersistenceWarningPanel.IsVisible = true;
            RecordsPersistenceWarningText.Text = warning;
        }
        else
        {
            RecordsPersistenceWarningPanel.IsVisible = false;
        }
    }

    private bool SafeCanMutateRecords()
    {
        try { return _canMutateRecords(); }
        catch { return false; }
    }

    // ==========================================
    // HELPERS & INTERVAL SLICING
    // ==========================================

    private static List<DisplayInterval> CollectIntervals(
        ProjectHistoryView history,
        DashboardUtcRange range,
        string? projectKeyFilter,
        DateTime asOfUtc)
    {
        var result = new List<DisplayInterval>();
        foreach (ProjectWorkIntervalView interval in history.Intervals)
        {
            if (projectKeyFilter != null && !string.Equals(interval.ProjectKey, projectKeyFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            DateTime startUtc = EnsureUtc(interval.StartUtc);
            DateTime endUtc = interval.EndUtc.HasValue ? EnsureUtc(interval.EndUtc.Value) : asOfUtc;
            bool isLive = !interval.EndUtc.HasValue;

            if (range.StartUtc.HasValue && endUtc <= range.StartUtc.Value) continue;
            if (startUtc >= range.EndUtc) continue;

            DateTime clippedStart = range.StartUtc.HasValue && startUtc < range.StartUtc.Value ? range.StartUtc.Value : startUtc;
            DateTime clippedEnd = endUtc > range.EndUtc ? range.EndUtc : endUtc;

            if (clippedEnd > clippedStart)
            {
                result.Add(new DisplayInterval(interval, interval.ProjectKey, interval.ProjectName, clippedStart, clippedEnd, isLive));
            }
        }

        return result;
    }

    private static List<DayTotal> BuildDayTotals(IReadOnlyList<DisplayInterval> intervals)
    {
        var totals = new Dictionary<DateTime, long>();
        foreach (DisplayInterval interval in intervals)
        {
            foreach (DayFragment fragment in SplitByLocalDay(interval))
            {
                totals.TryGetValue(fragment.Date, out long ticks);
                totals[fragment.Date] = ticks + fragment.Duration.Ticks;
            }
        }

        return totals
            .Select(item => new DayTotal(item.Key, TimeSpan.FromTicks(item.Value)))
            .OrderByDescending(item => item.Date)
            .ToList();
    }

    private static IEnumerable<DayFragment> SplitByLocalDay(DisplayInterval interval)
    {
        DateTime currentUtc = interval.StartUtc;
        DateTime endUtc = interval.EndUtc;

        while (currentUtc < endUtc)
        {
            DateTime currentLocal = currentUtc.ToLocalTime();
            DateTime dayStartLocal = currentLocal.Date;
            DateTime dayEndLocal = dayStartLocal.AddDays(1);

            DateTime dayStartUtc = LocalBoundaryToUtc(dayStartLocal);
            DateTime dayEndUtc = LocalBoundaryToUtc(dayEndLocal);

            DateTime fragEndUtc = endUtc < dayEndUtc ? endUtc : dayEndUtc;
            DateTime fragStartLocal = currentUtc.ToLocalTime();
            DateTime fragEndLocal = fragEndUtc.ToLocalTime();

            yield return new DayFragment(
                interval.ProjectKey,
                interval.ProjectName,
                dayStartLocal,
                dayStartUtc,
                dayEndUtc,
                currentUtc,
                fragEndUtc,
                fragStartLocal,
                fragEndLocal,
                fragEndUtc - currentUtc,
                interval.IsLive,
                fragEndUtc == endUtc);

            currentUtc = fragEndUtc;
        }
    }

    private static DateTime LocalBoundaryToUtc(DateTime localDate)
    {
        DateTime unspecified = DateTime.SpecifyKind(localDate, DateTimeKind.Unspecified);
        return TimeZoneInfo.Local.IsInvalidTime(unspecified)
            ? localDate.ToUniversalTime()
            : TimeZoneInfo.ConvertTimeToUtc(unspecified, TimeZoneInfo.Local);
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static string FormatLocalTime(DateTime local, DateTime utc) => local.ToString("t", CultureInfo.CurrentCulture);

    private static string FormatCompactDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            int hours = (int)duration.TotalHours;
            return duration.Minutes == 0 ? $"{hours}h" : $"{hours}h {duration.Minutes}m";
        }
        if (duration.TotalMinutes >= 1)
        {
            return $"{(int)duration.TotalMinutes}m";
        }
        return duration.TotalSeconds >= 1 ? $"{(int)duration.TotalSeconds}s" : "0m";
    }

    private static string FormatDetailedDuration(TimeSpan duration)
    {
        int hours = (int)duration.TotalHours;
        return $"{hours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
    }

    private SolidColorBrush GetProjectBrush(string key)
    {
        uint hash = 2166136261;
        foreach (char character in key.ToUpperInvariant())
        {
            hash ^= character;
            hash *= 16777619;
        }

        return new SolidColorBrush(ProjectPalette[hash % (uint)ProjectPalette.Length]);
    }

    private TextBlock CreateMutedMessage(string text)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = 12,
            Margin = new Thickness(0, 5, 0, 5)
        };
        if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sb) == true && sb is IBrush sBrush)
            tb.Foreground = sBrush;
        return tb;
    }

    private sealed record DisplayInterval(
        ProjectWorkIntervalView Source,
        string ProjectKey,
        string ProjectName,
        DateTime StartUtc,
        DateTime EndUtc,
        bool IsLive)
    {
        public TimeSpan Duration => EndUtc - StartUtc;
        public DateTime StartLocal => StartUtc.ToLocalTime();
        public DateTime EndLocal => EndUtc.ToLocalTime();
    }

    private sealed record ProjectTotal(string Key, string Name, TimeSpan Duration);
    private sealed record DayTotal(DateTime Date, TimeSpan Duration);
    private sealed record DayFragment(
        string ProjectKey,
        string ProjectName,
        DateTime Date,
        DateTime DayStartUtc,
        DateTime DayEndUtc,
        DateTime StartUtc,
        DateTime EndUtc,
        DateTime StartLocal,
        DateTime EndLocal,
        TimeSpan Duration,
        bool IsLive,
        bool EndsAtIntervalEnd);
    private sealed record TimelineItem(DayFragment Fragment, int Lane);

    private sealed record ProjectFilterOption(string? Key, string Name)
    {
        public override string ToString() => Name;
    }
}
