using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using StopwatchOverlay.PeriodicReview;

namespace StopwatchOverlay.Desktop.Views;

public partial class PeriodicReviewWindow : Window
{
    private readonly DateTime _startUtc;
    private readonly DateTime _endUtc;
    private readonly AppSettings _settings;
    private readonly ProjectHistoryView _history;
    private readonly IEnumerable<TimerSession>? _runningSessions;
    private readonly Action<List<PeriodicReviewStopwatchSlot>>? _onSave;
    private readonly Func<string, string>? _onRegisterProject;

    private PeriodicReviewModel? _model;
    private bool _isStep2;
    private bool _handled;
    private double _thresholdPercent;
    private bool _isOthersExpanded;

    // Selection model for Step 1 & 2
    private readonly List<ReviewProjectSelectionItem> _availableProjects = [];
    private readonly List<ReviewProjectSelectionItem> _selectedItems = [];

    // Selected items card view holders for immediate, bidirectional sync
    private sealed class AllocatedCardViewHolder
    {
        public ReviewProjectSelectionItem Item { get; set; } = null!;
        public Border Border { get; set; } = null!;
        public TextBlock? RangeBadgeText { get; set; }
        public Slider Slider { get; set; } = null!;
        public TextBox MinInput { get; set; } = null!;
        public TextBox? StartBox { get; set; }
        public TextBox? EndBox { get; set; }
    }

    private readonly List<AllocatedCardViewHolder> _cardViewHolders = [];
    private bool _isUpdatingViews;

    // Drag-to-move state for chart timeline
    private ReviewProjectSelectionItem? _draggingItem;
    private Point _dragStartPoint;
    private DateTime _dragOriginalStartUtc;
    private TimeSpan _dragItemDuration;
    private DateTime _dragMinStartUtc;
    private DateTime _dragMaxStartUtc;

    // Unallocated drag state
    private bool _isUnallocDragging;
    private Point _unallocStartPoint;
    private TimelineSlot? _unallocDragSlot;

    // Distinct theme colors for multi-color timeline distribution bar
    private static readonly Color[] PaletteColors =
    [
        Color.FromRgb(66, 185, 232),   // Cyan
        Color.FromRgb(82, 201, 124),   // Green
        Color.FromRgb(244, 178, 77),   // Orange / Amber
        Color.FromRgb(255, 115, 95),   // Coral / Red
        Color.FromRgb(173, 127, 240),  // Purple
        Color.FromRgb(240, 98, 146),   // Pink
        Color.FromRgb(77, 208, 225)    // Turquoise
    ];

    public List<PeriodicReviewStopwatchSlot>? SavedSlots { get; private set; }

    public PeriodicReviewWindow()
        : this(DateTime.UtcNow.AddMinutes(-30), DateTime.UtcNow, new AppSettings(),
               new ProjectTimeHistory().CreateView(DateTime.UtcNow),
               null, null, null)
    {
    }

    public PeriodicReviewWindow(
        DateTime startUtc,
        DateTime endUtc,
        AppSettings settings,
        ProjectHistoryView history,
        IEnumerable<TimerSession>? runningSessions,
        Action<List<PeriodicReviewStopwatchSlot>>? onSave,
        Func<string, string>? onRegisterProject = null)
    {
        InitializeComponent();

        _startUtc = PeriodicReviewDataAggregator.TruncateToMinute(ProjectTimeHistory.NormalizeUtc(startUtc));
        _endUtc = PeriodicReviewDataAggregator.TruncateToMinute(ProjectTimeHistory.NormalizeUtc(endUtc));
        if (_endUtc <= _startUtc)
        {
            _endUtc = _startUtc.AddMinutes(1);
        }
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _runningSessions = runningSessions;
        _onSave = onSave;
        _onRegisterProject = onRegisterProject;
        _thresholdPercent = _settings.PeriodicReviewMinActivityPercent;

        DateTime startLocal = _startUtc.ToLocalTime();
        DateTime endLocal = _endUtc.ToLocalTime();
        int totalMin = Math.Max(1, (int)Math.Round((endLocal - startLocal).TotalMinutes));
        PeriodRangeBadge.Text = $"📅 {startLocal:HH:mm} – {endLocal:HH:mm} ({totalMin}m)";
        ThresholdPercentText.Text = _thresholdPercent <= 0.0 ? "Off" : $"{_thresholdPercent:0.#}%";

        DistributionBarContainer.Background = Brushes.Transparent;
        DistributionBarContainer.PointerMoved += DistributionBarContainer_PointerMoved;
        DistributionBarContainer.PointerReleased += DistributionBarContainer_PointerReleased;
        DistributionBarContainer.PointerCaptureLost += (_, _) =>
        {
            _draggingItem = null;
            _isUnallocDragging = false;
            _unallocDragSlot = null;
        };

        InitializeProjectsList();

        Loaded += Window_Loaded;
    }

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private int TotalPeriodMinutes => Math.Max(1, (int)Math.Round((_endUtc - _startUtc).TotalMinutes));
    private TimeSpan TotalPeriodDuration => _endUtc > _startUtc ? _endUtc - _startUtc : TimeSpan.FromMinutes(30);

    private async void Window_Loaded(object? sender, RoutedEventArgs e)
    {
        RenderProjectCards();
        UpdateStep1SequenceSummary();
        RenderActivityLoadingState();

        Activate();
        Focus();

        await LoadReviewDataAsync();
    }

    private void RenderActivityLoadingState()
    {
        ActivitySummariesContainer.Children.Clear();
        var loadingPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 40, 0, 0),
            Spacing = 5
        };
        var spinner = new TextBlock
        {
            Text = "⏳ Loading ActivityWatch records...",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        if (Application.Current?.TryFindResource("AccentBrush", out var ab) == true && ab is IBrush aBrush)
            spinner.Foreground = aBrush;

        var sub = new TextBlock
        {
            Text = "Analyzing background application and web activity...",
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sb) == true && sb is IBrush sBrush)
            sub.Foreground = sBrush;

        loadingPanel.Children.Add(spinner);
        loadingPanel.Children.Add(sub);
        ActivitySummariesContainer.Children.Add(loadingPanel);
    }

    private async Task LoadReviewDataAsync()
    {
        try
        {
            _model = await PeriodicReviewDataAggregator.FetchAndAggregateAsync(
                _startUtc,
                _endUtc,
                _settings,
                _history,
                _runningSessions);

            SyncKnownProjects();
            RenderProjectCards();
            RenderActivitySummaries();
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "PeriodicReviewWindow.LoadData");
        }
    }

    private void InitializeProjectsList()
    {
        _availableProjects.Clear();

        // 1. Break / Empty item
        _availableProjects.Add(new ReviewProjectSelectionItem
        {
            ProjectName = "Break / Empty",
            IsBreak = true
        });

        // 2. Known projects from history view
        foreach (var p in _history.Projects)
        {
            if (!string.IsNullOrWhiteSpace(p.Name) &&
                !string.Equals(p.Name, "(Untracked / Off)", StringComparison.OrdinalIgnoreCase) &&
                !_availableProjects.Any(ap => string.Equals(ap.ProjectName, p.Name, StringComparison.OrdinalIgnoreCase)))
            {
                _availableProjects.Add(new ReviewProjectSelectionItem
                {
                    ProjectName = p.Name.Trim(),
                    IsBreak = false
                });
            }
        }

        // If no projects exist, add a starter project
        if (_availableProjects.Count <= 1)
        {
            _availableProjects.Add(new ReviewProjectSelectionItem
            {
                ProjectName = "General Work",
                IsBreak = false
            });
        }
    }

    private void SyncKnownProjects()
    {
        if (_model == null) return;
        bool anyAdded = false;
        foreach (var proj in _model.KnownProjects)
        {
            if (!string.IsNullOrWhiteSpace(proj) &&
                !string.Equals(proj, "(Untracked / Off)", StringComparison.OrdinalIgnoreCase) &&
                !_availableProjects.Any(p => string.Equals(p.ProjectName, proj, StringComparison.OrdinalIgnoreCase)))
            {
                _availableProjects.Add(new ReviewProjectSelectionItem
                {
                    ProjectName = proj.Trim(),
                    IsBreak = false
                });
                anyAdded = true;
            }
        }

        if (anyAdded)
        {
            RenderProjectCards();
        }
    }

    // ==========================================
    // STEP 1: Render Overview & Project Cards
    // ==========================================

    private void RenderStep1()
    {
        RenderProjectCards();
        RenderActivitySummaries();
        UpdateStep1SequenceSummary();
    }

    private void RenderProjectCards()
    {
        ProjectCardsContainer.Children.Clear();

        foreach (var item in _availableProjects)
        {
            var card = CreateProjectCard(item);
            ProjectCardsContainer.Children.Add(card);
        }
    }

    private Control CreateProjectCard(ReviewProjectSelectionItem item)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 8, 8),
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        var contentGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*")
        };

        if (item.IsSelected)
        {
            border.Background = new SolidColorBrush(Color.FromArgb(40, 66, 185, 232));
            if (Application.Current?.TryFindResource("AccentBrush", out var ab) == true && ab is IBrush aBrush)
                border.BorderBrush = aBrush;
            border.BorderThickness = new Thickness(1.5);

            var badgeBorder = new Border
            {
                CornerRadius = new CornerRadius(10),
                MinWidth = 20,
                Height = 20,
                Padding = new Thickness(5, 0, 5, 0),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            if (Application.Current?.TryFindResource("AccentBrush", out var abb) == true && abb is IBrush abBrush)
                badgeBorder.Background = abBrush;

            var badgeText = new TextBlock
            {
                Text = item.SelectionOrder.ToString(),
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            badgeBorder.Child = badgeText;
            Grid.SetColumn(badgeBorder, 0);
            contentGrid.Children.Add(badgeBorder);
        }
        else
        {
            if (Application.Current?.TryFindResource("SurfaceRaisedBrush", out var srb) == true && srb is IBrush srBrush)
                border.Background = srBrush;
            if (Application.Current?.TryFindResource("BorderBrush", out var bb) == true && bb is IBrush bBrush)
                border.BorderBrush = bBrush;
            border.BorderThickness = new Thickness(1);

            var iconText = new TextBlock
            {
                Text = item.IsBreak ? "☕ " : "📁 ",
                FontSize = 12,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(iconText, 0);
            contentGrid.Children.Add(iconText);
        }

        var labelText = new TextBlock
        {
            Text = item.DisplayName,
            FontSize = 12,
            FontWeight = item.IsSelected ? FontWeight.SemiBold : FontWeight.Normal,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (item.IsSelected)
        {
            if (Application.Current?.TryFindResource("PrimaryTextBrush", out var pb) == true && pb is IBrush pBrush)
                labelText.Foreground = pBrush;
        }
        else
        {
            if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sb) == true && sb is IBrush sBrush)
                labelText.Foreground = sBrush;
        }
        Grid.SetColumn(labelText, 1);
        contentGrid.Children.Add(labelText);

        border.Child = contentGrid;

        border.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
            {
                ToggleProjectSelection(item);
            }
        };

        return border;
    }

    private void ToggleProjectSelection(ReviewProjectSelectionItem item)
    {
        if (item.IsSelected)
        {
            _selectedItems.Remove(item);
            item.SelectionOrder = 0;
            for (int i = 0; i < _selectedItems.Count; i++)
            {
                _selectedItems[i].SelectionOrder = i + 1;
            }
        }
        else
        {
            _selectedItems.Add(item);
            item.SelectionOrder = _selectedItems.Count;
        }

        RenderProjectCards();
        UpdateStep1SequenceSummary();
    }

    private void UpdateStep1SequenceSummary()
    {
        if (_selectedItems.Count == 0)
        {
            SelectedSequenceText.Text = "None selected (click cards above in sequence)";
            if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sb) == true && sb is IBrush sBrush)
                SelectedSequenceText.Foreground = sBrush;
            ContinueToStep2Button.IsEnabled = false;
        }
        else
        {
            var parts = _selectedItems.Select((it, idx) => $"{idx + 1}. {it.DisplayName}");
            SelectedSequenceText.Text = string.Join("  ➔  ", parts);
            if (Application.Current?.TryFindResource("AccentBrush", out var ab) == true && ab is IBrush aBrush)
                SelectedSequenceText.Foreground = aBrush;
            ContinueToStep2Button.IsEnabled = true;
        }
    }

    private void AddNewProjectButton_Click(object? sender, RoutedEventArgs e)
    {
        CreateAndSelectNewProject();
    }

    private void NewProjectTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            CreateAndSelectNewProject();
        }
    }

    private void CreateAndSelectNewProject()
    {
        string name = NewProjectTextBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name)) return;

        if (_onRegisterProject != null)
        {
            name = _onRegisterProject(name);
        }

        var existing = _availableProjects.FirstOrDefault(p => string.Equals(p.ProjectName, name, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            existing = new ReviewProjectSelectionItem
            {
                ProjectName = name,
                IsBreak = false
            };
            _availableProjects.Add(existing);
        }

        if (!existing.IsSelected)
        {
            _selectedItems.Add(existing);
            existing.SelectionOrder = _selectedItems.Count;
        }

        NewProjectTextBox.Text = "";
        RenderProjectCards();
        UpdateStep1SequenceSummary();
    }

    private void DecreaseThresholdButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_thresholdPercent <= 1.0) _thresholdPercent = 0.0;
        else if (_thresholdPercent <= 2.0) _thresholdPercent = 1.0;
        else if (_thresholdPercent <= 3.0) _thresholdPercent = 2.0;
        else if (_thresholdPercent <= 5.0) _thresholdPercent = 3.0;
        else if (_thresholdPercent <= 10.0) _thresholdPercent = 5.0;
        else _thresholdPercent = Math.Max(0.0, _thresholdPercent - 5.0);

        UpdateThresholdUI();
    }

    private void IncreaseThresholdButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_thresholdPercent < 1.0) _thresholdPercent = 1.0;
        else if (_thresholdPercent < 2.0) _thresholdPercent = 2.0;
        else if (_thresholdPercent < 3.0) _thresholdPercent = 3.0;
        else if (_thresholdPercent < 5.0) _thresholdPercent = 5.0;
        else if (_thresholdPercent < 10.0) _thresholdPercent = 10.0;
        else _thresholdPercent = Math.Min(30.0, _thresholdPercent + 5.0);

        UpdateThresholdUI();
    }

    private void UpdateThresholdUI()
    {
        ThresholdPercentText.Text = _thresholdPercent <= 0.0 ? "Off" : $"{_thresholdPercent:0.#}%";
        _settings.PeriodicReviewMinActivityPercent = _thresholdPercent;
        SettingsStore.Save(_settings);
        RenderActivitySummaries();
    }

    private void RenderActivitySummaries()
    {
        ActivitySummariesContainer.Children.Clear();

        if (_model == null)
        {
            RenderActivityLoadingState();
            return;
        }

        var rawList = _model.RawActivitySummaries.Count > 0 ? _model.RawActivitySummaries : _model.ActivitySummaries;
        var groups = PeriodicReviewDataAggregator.FilterAndGroupActivitiesSummary(rawList, TotalPeriodDuration, _thresholdPercent);

        if (groups.Count == 0)
        {
            if (!_model.ActivityWatchAvailable)
            {
                ActivityWatchStatusCard.IsVisible = true;
                ActivityWatchStatusHeading.Text = "⚠️ ActivityWatch is Offline";
                ActivityWatchStatusMessage.Text = _model.ActivityWatchMessage ?? "Cannot connect to ActivityWatch.";
                RetryActivityWatchButton.IsVisible = true;
            }
            else
            {
                ActivityWatchStatusCard.IsVisible = false;
                var emptyNotice = new TextBlock
                {
                    Text = "No ActivityWatch logs recorded during this period.",
                    FontSize = 12,
                    Margin = new Thickness(10, 20, 10, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                if (Application.Current?.TryFindResource("SecondaryTextBrush", out var stb) == true && stb is IBrush stBrush)
                    emptyNotice.Foreground = stBrush;
                ActivitySummariesContainer.Children.Add(emptyNotice);
            }
            return;
        }

        ActivityWatchStatusCard.IsVisible = false;

        foreach (var group in groups)
        {
            ActivitySummariesContainer.Children.Add(CreateActivitySummaryCard(group));
        }
    }

    private Control CreateActivitySummaryCard(ActivitySummaryGroup group)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 8),
            BorderThickness = new Thickness(1)
        };
        if (Application.Current?.TryFindResource("SurfaceRaisedBrush", out var srb) == true && srb is IBrush srBrush)
            border.Background = srBrush;
        if (Application.Current?.TryFindResource("BorderBrush", out var bb) == true && bb is IBrush bBrush)
            border.BorderBrush = bBrush;

        var rootGrid = new Grid();
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Row 0: Top Header
        var topGrid = new Grid();
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Percentage Badge
        IBrush badgeBg = group.IsOther
            ? new SolidColorBrush(Color.FromArgb(190, 100, 115, 130))
            : (group.IsIdle
                ? new SolidColorBrush(Color.FromArgb(180, 80, 90, 100))
                : (group.Percentage >= 25.0
                    ? (Application.Current?.TryFindResource("AccentBrush", out var ab) == true && ab is IBrush aBrush ? aBrush : Brushes.DodgerBlue)
                    : new SolidColorBrush(Color.FromArgb(200, 36, 120, 160))));

        var pctBadge = new Border
        {
            Background = badgeBg,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var pctText = new TextBlock
        {
            Text = group.PercentageDisplay,
            FontSize = 12,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White
        };
        pctBadge.Child = pctText;
        Grid.SetColumn(pctBadge, 0);
        topGrid.Children.Add(pctBadge);

        // Title and App Stack
        var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var mainTitle = new TextBlock
        {
            Text = group.Title,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        if (Application.Current?.TryFindResource("PrimaryTextBrush", out var pb) == true && pb is IBrush pBrush)
            mainTitle.Foreground = pBrush;
        titleStack.Children.Add(mainTitle);

        if (!string.Equals(group.App, group.Title, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(group.App))
        {
            var subApp = new TextBlock
            {
                Text = group.App,
                FontSize = 10.5,
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            if (Application.Current?.TryFindResource("SecondaryTextBrush", out var stb) == true && stb is IBrush stBrush)
                subApp.Foreground = stBrush;
            titleStack.Children.Add(subApp);
        }
        Grid.SetColumn(titleStack, 1);
        topGrid.Children.Add(titleStack);

        // Duration & Category
        var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var catBadge = new Border
        {
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1, 5, 1),
            Margin = new Thickness(0, 0, 8, 0),
            BorderThickness = new Thickness(1)
        };
        if (Application.Current?.TryFindResource("DialogSurfaceBrush", out var dsb) == true && dsb is IBrush dsBrush)
            catBadge.Background = dsBrush;
        if (Application.Current?.TryFindResource("BorderBrush", out var bb2) == true && bb2 is IBrush bBrush2)
            catBadge.BorderBrush = bBrush2;

        string catLabel = group.IsOther
            ? "📦 Others"
            : (group.IsIdle ? "💤 Idle" : (string.Equals(group.Category, "Web", StringComparison.OrdinalIgnoreCase) ? "🌐 Web" : "💻 App"));
        var catText = new TextBlock
        {
            Text = catLabel,
            FontSize = 10
        };
        if (Application.Current?.TryFindResource("SecondaryTextBrush", out var stbb) == true && stbb is IBrush stbBrush)
            catText.Foreground = stbBrush;
        catBadge.Child = catText;
        rightStack.Children.Add(catBadge);

        var durText = new TextBlock
        {
            Text = group.DurationDisplay,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (Application.Current?.TryFindResource("PrimaryTextBrush", out var pb2) == true && pb2 is IBrush pBrush2)
            durText.Foreground = pBrush2;
        rightStack.Children.Add(durText);

        Grid.SetColumn(rightStack, 2);
        topGrid.Children.Add(rightStack);
        rootGrid.Children.Add(topGrid);

        // Row 1: Horizontal Percentage Bar Indicator
        var barTrack = new Border
        {
            CornerRadius = new CornerRadius(2),
            Height = 4,
            Margin = new Thickness(0, 8, 0, 0)
        };
        if (Application.Current?.TryFindResource("DialogSurfaceBrush", out var dsb2) == true && dsb2 is IBrush dsBrush2)
            barTrack.Background = dsBrush2;

        var barFillGrid = new Grid();
        barFillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Clamp(group.Percentage, 0.5, 100), GridUnitType.Star) });
        barFillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0, 100 - group.Percentage), GridUnitType.Star) });

        var barFill = new Border
        {
            Background = badgeBg,
            CornerRadius = new CornerRadius(2),
            Height = 4
        };
        Grid.SetColumn(barFill, 0);
        barFillGrid.Children.Add(barFill);
        barTrack.Child = barFillGrid;

        Grid.SetRow(barTrack, 1);
        rootGrid.Children.Add(barTrack);

        // Interactive details & expandable list for Others
        if (group.IsOther && group.SubItems.Count > 0)
        {
            var tipItems = group.SubItems.Take(15).Select(s => $"• {s.Title} ({s.PercentageDisplay}, {s.DurationDisplay})");
            string tip = string.Join("\n", tipItems);
            if (group.SubItems.Count > 15) tip += $"\n... and {group.SubItems.Count - 15} more";
            ToolTip.SetTip(border, $"Activities below {_thresholdPercent:0.#}%:\n{tip}\n\n(Click card to toggle details)");
            border.Cursor = new Cursor(StandardCursorType.Hand);
            border.PointerPressed += (s, e) =>
            {
                if (e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
                {
                    _isOthersExpanded = !_isOthersExpanded;
                    RenderActivitySummaries();
                }
            };

            if (_isOthersExpanded)
            {
                rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var subItemsPanel = new StackPanel { Margin = new Thickness(6, 10, 6, 2) };
                foreach (var sub in group.SubItems)
                {
                    var subRow = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                    subRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    subRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    subRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var dot = new TextBlock { Text = "• ", FontSize = 11 };
                    if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sb) == true && sb is IBrush sBrush)
                        dot.Foreground = sBrush;
                    Grid.SetColumn(dot, 0);
                    subRow.Children.Add(dot);

                    var sTitle = new TextBlock
                    {
                        Text = sub.Title,
                        FontSize = 11,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };
                    if (Application.Current?.TryFindResource("PrimaryTextBrush", out var pb3) == true && pb3 is IBrush pBrush3)
                        sTitle.Foreground = pBrush3;
                    ToolTip.SetTip(sTitle, $"{sub.App} — {sub.Title}");
                    Grid.SetColumn(sTitle, 1);
                    subRow.Children.Add(sTitle);

                    var sDur = new TextBlock
                    {
                        Text = $"{sub.PercentageDisplay} ({sub.DurationDisplay})",
                        FontSize = 10.5,
                        Margin = new Thickness(6, 0, 0, 0)
                    };
                    if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sb2) == true && sb2 is IBrush sBrush2)
                        sDur.Foreground = sBrush2;
                    Grid.SetColumn(sDur, 2);
                    subRow.Children.Add(sDur);

                    subItemsPanel.Children.Add(subRow);
                }
                Grid.SetRow(subItemsPanel, 2);
                rootGrid.Children.Add(subItemsPanel);
            }
        }

        border.Child = rootGrid;
        return border;
    }

    private async void RetryActivityWatchButton_Click(object? sender, RoutedEventArgs e)
    {
        await LoadReviewDataAsync();
    }

    // ==========================================
    // STEP 2: Time Allocation & Verification
    // ==========================================

    private void ContinueToStep2Button_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedItems.Count == 0) return;

        int totalMin = TotalPeriodMinutes;
        double currentAllocated = _selectedItems.Sum(it => it.AllocatedMinutes);

        if (currentAllocated <= 0)
        {
            int baseMinutes = totalMin / _selectedItems.Count;
            int remainder = totalMin % _selectedItems.Count;

            DateTime cursor = _startUtc;
            for (int i = 0; i < _selectedItems.Count; i++)
            {
                int min = baseMinutes + (i == _selectedItems.Count - 1 ? remainder : 0);
                _selectedItems[i].AllocatedMinutes = min;
                _selectedItems[i].StartUtc = cursor;
                _selectedItems[i].EndUtc = cursor.AddMinutes(min);
                cursor = cursor.AddMinutes(min);
            }
        }
        else
        {
            PeriodicReviewDataAggregator.RepackTimelineIntervals(_selectedItems, _startUtc, _endUtc);
        }

        _isStep2 = true;
        Step1Container.IsVisible = false;
        Step2Container.IsVisible = true;

        StepIndicatorBadge.Text = "Step 2 of 2: Time Allocation";
        StepSubtitleText.Text = "Step 2: Allocate & position time. Drag items on the timeline to move them. Click ∨ to set exact times.";
        FooterShortcutHintText.Text = "Press Esc to go back · Enter to verify and save";

        Step1SkipButton.IsVisible = false;
        Step2BackButton.IsVisible = true;
        Step2VerifyButton.IsVisible = true;

        RenderStep2();
    }

    private void Step2BackButton_Click(object? sender, RoutedEventArgs e)
    {
        _isStep2 = false;
        Step2Container.IsVisible = false;
        Step1Container.IsVisible = true;

        StepIndicatorBadge.Text = "Step 1 of 2: Projects & Overview";
        StepSubtitleText.Text = "Step 1: Select the projects you worked on in order (or breaks), guided by your ActivityWatch percentage report.";
        FooterShortcutHintText.Text = "Press Esc to skip without changes";

        Step1SkipButton.IsVisible = true;
        Step2BackButton.IsVisible = false;
        Step2VerifyButton.IsVisible = false;

        RenderStep1();
    }

    private void RenderStep2()
    {
        DateTime startLocal = _startUtc.ToLocalTime();
        DateTime endLocal = _endUtc.ToLocalTime();
        DateTime midLocal = startLocal + TimeSpan.FromMinutes(TotalPeriodMinutes / 2.0);

        TimelineScaleStartText.Text = startLocal.ToString("HH:mm");
        TimelineScaleMidText.Text = midLocal.ToString("HH:mm");
        TimelineScaleEndText.Text = endLocal.ToString("HH:mm");

        PeriodicReviewDataAggregator.BuildFullTimeline(_startUtc, _endUtc, _selectedItems);

        RenderAllocatedCards();
        SyncAllViews(updateCards: true);
    }

    private void SetItemAllocatedMinutes(ReviewProjectSelectionItem changedItem, double targetMinutes)
    {
        PeriodicReviewDataAggregator.RebalanceAllocatedMinutes(_selectedItems, changedItem, targetMinutes, TotalPeriodMinutes);
        PeriodicReviewDataAggregator.RepackTimelineIntervals(_selectedItems, _startUtc, _endUtc);
        SyncAllViews(updateCards: true);
    }

    private void SyncAllViews(bool updateCards = true)
    {
        var fullTimeline = PeriodicReviewDataAggregator.BuildFullTimeline(_startUtc, _endUtc, _selectedItems);

        RenderDistributionBar(fullTimeline);
        RenderTimelinePreview(fullTimeline);
        UpdateStep2SummaryText(fullTimeline);

        if (updateCards && _cardViewHolders.Count > 0)
        {
            _isUpdatingViews = true;
            try
            {
                foreach (var holder in _cardViewHolders)
                {
                    var it = holder.Item;
                    DateTime startLocal = (it.StartUtc ?? _startUtc).ToLocalTime();
                    DateTime endLocal = (it.EndUtc ?? _endUtc).ToLocalTime();
                    int min = (int)Math.Round(it.AllocatedMinutes);

                    if (holder.RangeBadgeText != null)
                    {
                        holder.RangeBadgeText.Text = $"{startLocal:HH:mm} – {endLocal:HH:mm} ({min}m)";
                    }

                    if (Math.Abs(holder.Slider.Value - it.AllocatedMinutes) > 0.01)
                    {
                        holder.Slider.Value = it.AllocatedMinutes;
                    }

                    string minStr = min.ToString();
                    if (holder.MinInput.Text != minStr && !holder.MinInput.IsFocused)
                    {
                        holder.MinInput.Text = minStr;
                    }

                    if (holder.StartBox != null && !holder.StartBox.IsFocused)
                    {
                        holder.StartBox.Text = startLocal.ToString("HH:mm");
                    }

                    if (holder.EndBox != null && !holder.EndBox.IsFocused)
                    {
                        holder.EndBox.Text = endLocal.ToString("HH:mm");
                    }

                    ToolTip.SetTip(holder.Border, CreateRichToolTip(it));
                }
            }
            finally
            {
                _isUpdatingViews = false;
            }
        }
    }

    private void RenderAllocatedCards()
    {
        AllocatedProjectsContainer.Children.Clear();
        _cardViewHolders.Clear();

        int totalMin = TotalPeriodMinutes;

        for (int i = 0; i < _selectedItems.Count; i++)
        {
            var item = _selectedItems[i];
            int index = i;
            var (card, holder) = CreateAllocatedProjectCard(item, index, totalMin);
            _cardViewHolders.Add(holder);
            AllocatedProjectsContainer.Children.Add(card);
        }
    }

    private (Control Card, AllocatedCardViewHolder Holder) CreateAllocatedProjectCard(ReviewProjectSelectionItem item, int index, int maxMinutes)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 10),
            BorderThickness = new Thickness(1)
        };
        if (Application.Current?.TryFindResource("SurfaceRaisedBrush", out var srb) == true && srb is IBrush srBrush)
            border.Background = srBrush;
        if (Application.Current?.TryFindResource("BorderBrush", out var bb) == true && bb is IBrush bBrush)
            border.BorderBrush = bBrush;

        var color = PaletteColors[index % PaletteColors.Length];
        ToolTip.SetTip(border, CreateRichToolTip(item));

        var stack = new StackPanel { Spacing = 6 };

        // 1. Header: Badge + Project Name + Time Range badge + Shift buttons (◀ ▶) + Order buttons (▲ ▼) + Expand Chevron (∨)
        var headGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var badge = new Border
        {
            Background = new SolidColorBrush(color),
            CornerRadius = new CornerRadius(10),
            MinWidth = 22,
            Height = 22,
            Padding = new Thickness(6, 0, 6, 0),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        badge.Child = new TextBlock
        {
            Text = $"#{index + 1}",
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(badge, 0);
        headGrid.Children.Add(badge);

        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Spacing = 8 };
        var nameText = new TextBlock
        {
            Text = item.DisplayName,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (Application.Current?.TryFindResource("PrimaryTextBrush", out var pb) == true && pb is IBrush pBrush)
            nameText.Foreground = pBrush;
        titleStack.Children.Add(nameText);

        DateTime itemStartLocal = (item.StartUtc ?? _startUtc).ToLocalTime();
        DateTime itemEndLocal = (item.EndUtc ?? _startUtc.AddMinutes(item.AllocatedMinutes)).ToLocalTime();

        var rangeBadge = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 1, 6, 1),
            VerticalAlignment = VerticalAlignment.Center
        };
        if (Application.Current?.TryFindResource("DialogSurfaceBrush", out var dsb) == true && dsb is IBrush dsBrush)
            rangeBadge.Background = dsBrush;

        var rangeBadgeText = new TextBlock
        {
            Text = $"{itemStartLocal:HH:mm} – {itemEndLocal:HH:mm} ({item.AllocatedMinutes:0}m)",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold
        };
        if (Application.Current?.TryFindResource("AccentBrush", out var ab) == true && ab is IBrush aBrush)
            rangeBadgeText.Foreground = aBrush;
        rangeBadge.Child = rangeBadgeText;
        titleStack.Children.Add(rangeBadge);

        Grid.SetColumn(titleStack, 1);
        headGrid.Children.Add(titleStack);

        // Header Action Controls: Shift (◀ ▶) + Move (▲ ▼) + Expand Chevron (∨)
        var actionStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Spacing = 3 };

        var shiftLeftBtn = new Button
        {
            Content = "◀",
            Width = 24,
            Height = 22,
            Padding = new Thickness(0),
            FontSize = 9,
            Classes = { "modern" }
        };
        ToolTip.SetTip(shiftLeftBtn, "Shift 1 minute earlier on timeline");
        shiftLeftBtn.Click += (_, _) => ShiftItem(item, -1);
        actionStack.Children.Add(shiftLeftBtn);

        var shiftRightBtn = new Button
        {
            Content = "▶",
            Width = 24,
            Height = 22,
            Padding = new Thickness(0),
            FontSize = 9,
            Classes = { "modern" }
        };
        ToolTip.SetTip(shiftRightBtn, "Shift 1 minute later on timeline");
        shiftRightBtn.Click += (_, _) => ShiftItem(item, 1);
        actionStack.Children.Add(shiftRightBtn);

        if (index > 0)
        {
            var upBtn = new Button
            {
                Content = "▲",
                Width = 24,
                Height = 22,
                Padding = new Thickness(0),
                FontSize = 9,
                Classes = { "modern" }
            };
            ToolTip.SetTip(upBtn, "Swap order with previous project");
            upBtn.Click += (_, _) =>
            {
                (_selectedItems[index], _selectedItems[index - 1]) = (_selectedItems[index - 1], _selectedItems[index]);
                for (int k = 0; k < _selectedItems.Count; k++) _selectedItems[k].SelectionOrder = k + 1;
                PeriodicReviewDataAggregator.RepackTimelineIntervals(_selectedItems, _startUtc, _endUtc);
                RenderAllocatedCards();
                SyncAllViews(updateCards: true);
            };
            actionStack.Children.Add(upBtn);
        }
        if (index < _selectedItems.Count - 1)
        {
            var downBtn = new Button
            {
                Content = "▼",
                Width = 24,
                Height = 22,
                Padding = new Thickness(0),
                FontSize = 9,
                Classes = { "modern" }
            };
            ToolTip.SetTip(downBtn, "Swap order with next project");
            downBtn.Click += (_, _) =>
            {
                (_selectedItems[index], _selectedItems[index + 1]) = (_selectedItems[index + 1], _selectedItems[index]);
                for (int k = 0; k < _selectedItems.Count; k++) _selectedItems[k].SelectionOrder = k + 1;
                PeriodicReviewDataAggregator.RepackTimelineIntervals(_selectedItems, _startUtc, _endUtc);
                RenderAllocatedCards();
                SyncAllViews(updateCards: true);
            };
            actionStack.Children.Add(downBtn);
        }

        var expandBtn = new Button
        {
            Content = item.IsExpanded ? "▲" : "∨",
            Width = 26,
            Height = 22,
            Padding = new Thickness(0),
            FontSize = 10,
            FontWeight = FontWeight.Bold,
            Classes = { "modern" }
        };
        ToolTip.SetTip(expandBtn, item.IsExpanded ? "Collapse exact time fields" : "Expand to set exact start and end times");
        expandBtn.Click += (_, _) =>
        {
            item.IsExpanded = !item.IsExpanded;
            RenderAllocatedCards();
            SyncAllViews(updateCards: true);
        };
        actionStack.Children.Add(expandBtn);

        Grid.SetColumn(actionStack, 2);
        headGrid.Children.Add(actionStack);
        stack.Children.Add(headGrid);

        // 2. Duration Controls: Slider + Text box + Quick +/- Buttons
        var ctrlGrid = new Grid();
        ctrlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ctrlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ctrlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var slider = new Slider
        {
            Minimum = 0,
            Maximum = maxMinutes,
            Value = item.AllocatedMinutes,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };

        var minInput = new TextBox
        {
            Text = ((int)Math.Round(item.AllocatedMinutes)).ToString(),
            Width = 44,
            Height = 26,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };

        var presetStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 0, 0), Spacing = 3 };

        Button minus5Btn = new() { Content = "-5", Width = 28, Height = 26, Padding = new Thickness(0), FontSize = 10.5, Classes = { "modern" } };
        minus5Btn.Click += (_, _) => SetItemAllocatedMinutes(item, Math.Max(0, item.AllocatedMinutes - 5));
        presetStack.Children.Add(minus5Btn);

        Button minus1Btn = new() { Content = "-1", Width = 28, Height = 26, Padding = new Thickness(0), FontSize = 10.5, Classes = { "modern" } };
        minus1Btn.Click += (_, _) => SetItemAllocatedMinutes(item, Math.Max(0, item.AllocatedMinutes - 1));
        presetStack.Children.Add(minus1Btn);

        Button plus1Btn = new() { Content = "+1", Width = 28, Height = 26, Padding = new Thickness(0), FontSize = 10.5, Classes = { "modern" } };
        plus1Btn.Click += (_, _) => SetItemAllocatedMinutes(item, Math.Min(maxMinutes, item.AllocatedMinutes + 1));
        presetStack.Children.Add(plus1Btn);

        Button plus5Btn = new() { Content = "+5", Width = 28, Height = 26, Padding = new Thickness(0), FontSize = 10.5, Classes = { "modern" } };
        plus5Btn.Click += (_, _) => SetItemAllocatedMinutes(item, Math.Min(maxMinutes, item.AllocatedMinutes + 5));
        presetStack.Children.Add(plus5Btn);

        slider.PropertyChanged += (_, se) =>
        {
            if (se.Property == Slider.ValueProperty && !_isUpdatingViews)
            {
                SetItemAllocatedMinutes(item, slider.Value);
            }
        };

        minInput.LostFocus += (_, _) =>
        {
            if (double.TryParse(minInput.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
            {
                SetItemAllocatedMinutes(item, Math.Clamp(parsed, 0, maxMinutes));
            }
            else
            {
                minInput.Text = ((int)Math.Round(item.AllocatedMinutes)).ToString();
            }
        };

        minInput.KeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Enter)
            {
                ke.Handled = true;
                if (double.TryParse(minInput.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                {
                    SetItemAllocatedMinutes(item, Math.Clamp(parsed, 0, maxMinutes));
                }
            }
        };

        Grid.SetColumn(slider, 0);
        Grid.SetColumn(minInput, 1);
        Grid.SetColumn(presetStack, 2);

        ctrlGrid.Children.Add(slider);
        ctrlGrid.Children.Add(minInput);
        ctrlGrid.Children.Add(presetStack);
        stack.Children.Add(ctrlGrid);

        // 3. Optional Expandable Start & End Exact Time fields
        TextBox? startBox = null;
        TextBox? endBox = null;
        if (item.IsExpanded)
        {
            var (expBorder, sBox, eBox) = CreateExpandableTimeFields(item);
            startBox = sBox;
            endBox = eBox;
            stack.Children.Add(expBorder);
        }

        border.Child = stack;

        var holder = new AllocatedCardViewHolder
        {
            Item = item,
            Border = border,
            RangeBadgeText = rangeBadgeText,
            Slider = slider,
            MinInput = minInput,
            StartBox = startBox,
            EndBox = endBox
        };

        return (border, holder);
    }

    private (Border Border, TextBox StartBox, TextBox EndBox) CreateExpandableTimeFields(ReviewProjectSelectionItem item)
    {
        var expandBorder = new Border
        {
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 10, 0, 0),
            BorderThickness = new Thickness(1)
        };
        if (Application.Current?.TryFindResource("DialogSurfaceBrush", out var dsb) == true && dsb is IBrush dsBrush)
            expandBorder.Background = dsBrush;
        if (Application.Current?.TryFindResource("BorderBrush", out var bb) == true && bb is IBrush bBrush)
            expandBorder.BorderBrush = bBrush;

        var rootStack = new StackPanel { Spacing = 8 };

        var headerText = new TextBlock
        {
            Text = "🕒 Exact Start & End Time (Custom Timeline Positioning)",
            FontSize = 11.5,
            FontWeight = FontWeight.SemiBold
        };
        if (Application.Current?.TryFindResource("AccentBrush", out var ab) == true && ab is IBrush aBrush)
            headerText.Foreground = aBrush;
        rootStack.Children.Add(headerText);

        var timeGrid = new Grid();
        timeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        timeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        timeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Left: Start Time Field
        var startStack = new StackPanel { Spacing = 4 };
        var startLabel = new TextBlock { Text = "Start Time (HH:mm):", FontSize = 11 };
        if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sb) == true && sb is IBrush sBrush)
            startLabel.Foreground = sBrush;
        startStack.Children.Add(startLabel);

        var startInputRow = new Grid();
        startInputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        startInputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        DateTime initialStartLocal = (item.StartUtc ?? _startUtc).ToLocalTime();
        var startBox = new TextBox
        {
            Text = initialStartLocal.ToString("HH:mm"),
            Height = 28,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        startBox.LostFocus += (_, _) =>
        {
            if (TryParseLocalTime(startBox.Text, _startUtc, _endUtc, out DateTime parsedUtc))
            {
                SetItemStartTime(item, parsedUtc);
            }
            else
            {
                startBox.Text = (item.StartUtc ?? _startUtc).ToLocalTime().ToString("HH:mm");
            }
        };
        startBox.KeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Enter)
            {
                ke.Handled = true;
                if (TryParseLocalTime(startBox.Text, _startUtc, _endUtc, out DateTime parsedUtc))
                {
                    SetItemStartTime(item, parsedUtc);
                }
            }
        };
        Grid.SetColumn(startBox, 0);
        startInputRow.Children.Add(startBox);

        var startNudgeStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 0, 0), Spacing = 2 };
        Button sMinus1 = new() { Content = "-1m", Height = 28, Padding = new Thickness(5, 0, 5, 0), FontSize = 10, Classes = { "modern" } };
        sMinus1.Click += (_, _) => ShiftItem(item, -1);
        startNudgeStack.Children.Add(sMinus1);

        Button sPlus1 = new() { Content = "+1m", Height = 28, Padding = new Thickness(5, 0, 5, 0), FontSize = 10, Classes = { "modern" } };
        sPlus1.Click += (_, _) => ShiftItem(item, 1);
        startNudgeStack.Children.Add(sPlus1);

        Grid.SetColumn(startNudgeStack, 1);
        startInputRow.Children.Add(startNudgeStack);
        startStack.Children.Add(startInputRow);
        Grid.SetColumn(startStack, 0);
        timeGrid.Children.Add(startStack);

        // Right: End Time Field
        var endStack = new StackPanel { Spacing = 4 };
        var endLabel = new TextBlock { Text = "End Time (HH:mm):", FontSize = 11 };
        if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sb2) == true && sb2 is IBrush sBrush2)
            endLabel.Foreground = sBrush2;
        endStack.Children.Add(endLabel);

        var endInputRow = new Grid();
        endInputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        endInputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        DateTime initialEndLocal = (item.EndUtc ?? _startUtc.AddMinutes(item.AllocatedMinutes)).ToLocalTime();
        var endBox = new TextBox
        {
            Text = initialEndLocal.ToString("HH:mm"),
            Height = 28,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        endBox.LostFocus += (_, _) =>
        {
            if (TryParseLocalTime(endBox.Text, _startUtc, _endUtc, out DateTime parsedUtc))
            {
                SetItemEndTime(item, parsedUtc);
            }
            else
            {
                endBox.Text = (item.EndUtc ?? _endUtc).ToLocalTime().ToString("HH:mm");
            }
        };
        endBox.KeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Enter)
            {
                ke.Handled = true;
                if (TryParseLocalTime(endBox.Text, _startUtc, _endUtc, out DateTime parsedUtc))
                {
                    SetItemEndTime(item, parsedUtc);
                }
            }
        };
        Grid.SetColumn(endBox, 0);
        endInputRow.Children.Add(endBox);

        var endNudgeStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 0, 0), Spacing = 2 };
        Button eMinus1 = new() { Content = "-1m", Height = 28, Padding = new Thickness(5, 0, 5, 0), FontSize = 10, Classes = { "modern" } };
        eMinus1.Click += (_, _) => AdjustEnd(item, -1);
        endNudgeStack.Children.Add(eMinus1);

        Button ePlus1 = new() { Content = "+1m", Height = 28, Padding = new Thickness(5, 0, 5, 0), FontSize = 10, Classes = { "modern" } };
        ePlus1.Click += (_, _) => AdjustEnd(item, 1);
        endNudgeStack.Children.Add(ePlus1);

        Grid.SetColumn(endNudgeStack, 1);
        endInputRow.Children.Add(endNudgeStack);
        endStack.Children.Add(endInputRow);
        Grid.SetColumn(endStack, 2);
        timeGrid.Children.Add(endStack);

        rootStack.Children.Add(timeGrid);
        expandBorder.Child = rootStack;
        return (expandBorder, startBox, endBox);
    }

    private static bool TryParseLocalTime(string? text, DateTime referenceStartUtc, DateTime referenceEndUtc, out DateTime resultUtc)
    {
        resultUtc = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string[] formats = ["H:mm", "HH:mm", "h:mm", "hh:mm", "H:m", "h:m"];
        if (DateTime.TryParseExact(text.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
        {
            DateTime refStartLocal = referenceStartUtc.ToLocalTime();
            DateTime refEndLocal = referenceEndUtc.ToLocalTime();

            DateTime candidateLocal = new DateTime(refStartLocal.Year, refStartLocal.Month, refStartLocal.Day, parsed.Hour, parsed.Minute, 0, DateTimeKind.Local);
            DateTime candidateUtc = candidateLocal.ToUniversalTime();

            if (candidateUtc < referenceStartUtc && refEndLocal.Date > refStartLocal.Date)
            {
                DateTime nextDayCandidate = candidateLocal.AddDays(1);
                DateTime nextDayUtc = nextDayCandidate.ToUniversalTime();
                if (nextDayUtc <= referenceEndUtc.AddMinutes(5))
                {
                    candidateUtc = nextDayUtc;
                }
            }

            resultUtc = PeriodicReviewDataAggregator.TruncateToMinute(candidateUtc);
            return true;
        }
        return false;
    }

    private void SetItemStartTime(ReviewProjectSelectionItem item, DateTime newStartUtc)
    {
        newStartUtc = PeriodicReviewDataAggregator.TruncateToMinute(newStartUtc);

        var sorted = _selectedItems
            .OrderBy(it => it.StartUtc ?? _startUtc)
            .ToList();

        int idx = sorted.IndexOf(item);
        DateTime minStart = (idx > 0 && sorted[idx - 1].EndUtc.HasValue) ? sorted[idx - 1].EndUtc!.Value : _startUtc;
        DateTime maxEnd = (idx < sorted.Count - 1 && sorted[idx + 1].StartUtc.HasValue) ? sorted[idx + 1].StartUtc!.Value : _endUtc;

        TimeSpan duration = TimeSpan.FromMinutes(Math.Max(0, item.AllocatedMinutes));

        if (newStartUtc < minStart) newStartUtc = minStart;
        DateTime newEndUtc = newStartUtc + duration;
        if (newEndUtc > maxEnd)
        {
            newEndUtc = maxEnd;
            newStartUtc = newEndUtc - duration;
            if (newStartUtc < minStart) newStartUtc = minStart;
        }

        item.StartUtc = newStartUtc;
        item.EndUtc = newEndUtc;

        SyncAllViews(updateCards: true);
    }

    private void SetItemEndTime(ReviewProjectSelectionItem item, DateTime newEndUtc)
    {
        newEndUtc = PeriodicReviewDataAggregator.TruncateToMinute(newEndUtc);
        DateTime start = item.StartUtc ?? _startUtc;
        if (newEndUtc < start) newEndUtc = start;
        double newMinutes = (newEndUtc - start).TotalMinutes;
        SetItemAllocatedMinutes(item, newMinutes);
    }

    private void ShiftItem(ReviewProjectSelectionItem item, int deltaMinutes)
    {
        if (!item.StartUtc.HasValue || !item.EndUtc.HasValue) return;

        TimeSpan duration = item.EndUtc.Value - item.StartUtc.Value;
        DateTime newStart = item.StartUtc.Value.AddMinutes(deltaMinutes);
        DateTime newEnd = newStart + duration;

        var sorted = _selectedItems
            .OrderBy(it => it.StartUtc ?? _startUtc)
            .ToList();

        int idx = sorted.IndexOf(item);
        DateTime minStart = (idx > 0 && sorted[idx - 1].EndUtc.HasValue) ? sorted[idx - 1].EndUtc!.Value : _startUtc;
        DateTime maxEnd = (idx < sorted.Count - 1 && sorted[idx + 1].StartUtc.HasValue) ? sorted[idx + 1].StartUtc!.Value : _endUtc;

        if (newStart < minStart)
        {
            newStart = minStart;
            newEnd = newStart + duration;
        }
        if (newEnd > maxEnd)
        {
            newEnd = maxEnd;
            newStart = newEnd - duration;
            if (newStart < minStart) newStart = minStart;
        }

        item.StartUtc = newStart;
        item.EndUtc = newEnd;

        SyncAllViews(updateCards: true);
    }

    private void AdjustEnd(ReviewProjectSelectionItem item, int deltaMinutes)
    {
        SetItemAllocatedMinutes(item, item.AllocatedMinutes + deltaMinutes);
    }

    private Control CreateRichToolTip(string title, double minutes, DateTime startUtc, DateTime endUtc, bool isBreak)
    {
        int totalMin = TotalPeriodMinutes;
        double hours = minutes / 60.0;
        double percentage = totalMin > 0 ? (minutes / totalMin) * 100.0 : 0.0;

        DateTime startLocal = startUtc.ToLocalTime();
        DateTime endLocal = endUtc.ToLocalTime();

        var ttStack = new StackPanel { Spacing = 3 };
        string icon = isBreak ? "☕" : "🏷️";
        var t1 = new TextBlock
        {
            Text = $"{icon} {title}",
            FontWeight = FontWeight.Bold,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 4)
        };
        if (Application.Current?.TryFindResource("PrimaryTextBrush", out var pb) == true && pb is IBrush pBrush)
            t1.Foreground = pBrush;
        ttStack.Children.Add(t1);

        var t2 = new TextBlock
        {
            Text = $"⏱️ Duration: {minutes:0} minutes ({hours:0.00} hours)",
            FontSize = 11.5
        };
        if (Application.Current?.TryFindResource("AccentBrush", out var ab) == true && ab is IBrush aBrush)
            t2.Foreground = aBrush;
        ttStack.Children.Add(t2);

        var t3 = new TextBlock
        {
            Text = $"📊 Share: {percentage:0.#}% of review period",
            FontSize = 11.5
        };
        if (Application.Current?.TryFindResource("PrimaryTextBrush", out var pb2) == true && pb2 is IBrush pBrush2)
            t3.Foreground = pBrush2;
        ttStack.Children.Add(t3);

        if (endLocal > startLocal)
        {
            var t4 = new TextBlock
            {
                Text = $"📅 Timeline: {startLocal:HH:mm} – {endLocal:HH:mm}",
                FontSize = 11.5
            };
            if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sb) == true && sb is IBrush sBrush)
                t4.Foreground = sBrush;
            ttStack.Children.Add(t4);
        }
        else if (minutes <= 0)
        {
            var t5 = new TextBlock
            {
                Text = "💡 0 minutes · Click pin or use slider below to allocate time",
                FontSize = 11
            };
            if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sb2) == true && sb2 is IBrush sBrush2)
                t5.Foreground = sBrush2;
            ttStack.Children.Add(t5);
        }

        return ttStack;
    }

    private Control CreateRichToolTip(ReviewProjectSelectionItem item)
    {
        DateTime start = item.StartUtc ?? _startUtc;
        DateTime end = item.EndUtc ?? start.AddMinutes(item.AllocatedMinutes);
        return CreateRichToolTip(item.DisplayName, item.AllocatedMinutes, start, end, item.IsBreak);
    }

    private Control CreateRichToolTip(TimelineSlot slot)
    {
        if (slot.IsUnallocated)
        {
            var stack = new StackPanel { Spacing = 2 };
            stack.Children.Add(new TextBlock { Text = "⚪ Unallocated Time Gap", FontWeight = FontWeight.Bold, FontSize = 12 });
            stack.Children.Add(new TextBlock { Text = $"⏱️ {slot.DurationMinutes:0}m ({slot.DurationMinutes / 60.0:0.00}h)", FontSize = 11 });
            stack.Children.Add(new TextBlock { Text = $"📅 {slot.StartLocal:HH:mm} – {slot.EndLocal:HH:mm}", FontSize = 11 });
            stack.Children.Add(new TextBlock { Text = "💡 Drag or click to shift adjacent tasks", FontSize = 10 });
            return stack;
        }
        return CreateRichToolTip(slot.DisplayName, slot.DurationMinutes, slot.StartUtc, slot.EndUtc, slot.IsBreak);
    }

    private void RenderDistributionBar(List<TimelineSlot> slots)
    {
        DistributionBarContainer.Children.Clear();
        DistributionBarContainer.ColumnDefinitions.Clear();

        if (slots.Count == 0) return;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot.DurationMinutes <= 0)
            {
                if (slot.Item != null)
                {
                    RenderZeroMinutePin(slot);
                }
                continue;
            }

            DistributionBarContainer.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(slot.DurationMinutes, GridUnitType.Star)
            });

            var segBorder = new Border
            {
                BorderThickness = new Thickness(0.5),
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(1, 0, 1, 0),
                ClipToBounds = true
            };
            if (Application.Current?.TryFindResource("BorderBrush", out var bb) == true && bb is IBrush bBrush)
                segBorder.BorderBrush = bBrush;

            if (slot.IsUnallocated)
            {
                segBorder.Background = new SolidColorBrush(Color.FromArgb(80, 50, 60, 70));
                segBorder.Cursor = new Cursor(StandardCursorType.SizeWestEast);

                var unallocStack = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var unallocText = new TextBlock
                {
                    Text = $"⚪ {slot.DurationMinutes:0}m",
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sb) == true && sb is IBrush sBrush)
                    unallocText.Foreground = sBrush;
                unallocStack.Children.Add(unallocText);
                segBorder.Child = unallocStack;
                ToolTip.SetTip(segBorder, CreateRichToolTip(slot));

                AttachUnallocatedDrag(segBorder, slot);
            }
            else
            {
                var item = slot.Item!;
                int itemIdx = _selectedItems.IndexOf(item);
                var segColor = PaletteColors[itemIdx >= 0 ? (itemIdx % PaletteColors.Length) : 0];

                segBorder.Background = item.IsBreak
                    ? new SolidColorBrush(Color.FromArgb(160, 100, 110, 120))
                    : new SolidColorBrush(segColor);

                segBorder.Cursor = new Cursor(StandardCursorType.SizeWestEast);
                ToolTip.SetTip(segBorder, CreateRichToolTip(slot));

                var segContent = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                segContent.Children.Add(new TextBlock
                {
                    Text = item.DisplayName,
                    FontSize = 10.5,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Brushes.White,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                segContent.Children.Add(new TextBlock
                {
                    Text = $"{slot.StartLocal:HH:mm}–{slot.EndLocal:HH:mm}",
                    FontSize = 9.5,
                    Foreground = Brushes.White,
                    Opacity = 0.9,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                segBorder.Child = segContent;

                AttachProjectBlockDrag(segBorder, item);
            }

            Grid.SetColumn(segBorder, DistributionBarContainer.ColumnDefinitions.Count - 1);
            DistributionBarContainer.Children.Add(segBorder);
        }
    }

    private void RenderZeroMinutePin(TimelineSlot slot)
    {
        var item = slot.Item!;
        int itemIdx = _selectedItems.IndexOf(item);
        var segColor = PaletteColors[itemIdx >= 0 ? (itemIdx % PaletteColors.Length) : 0];

        DistributionBarContainer.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(24, GridUnitType.Pixel)
        });

        var pinBorder = new Border
        {
            Width = 22,
            Height = 28,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = item.IsBreak
                ? new SolidColorBrush(Color.FromArgb(140, 100, 110, 120))
                : new SolidColorBrush(Color.FromArgb(220, segColor.R, segColor.G, segColor.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(1, 0, 1, 0),
            ClipToBounds = true,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        if (Application.Current?.TryFindResource("BorderBrush", out var bb) == true && bb is IBrush bBrush)
            pinBorder.BorderBrush = bBrush;

        ToolTip.SetTip(pinBorder, CreateRichToolTip(item.DisplayName, 0, slot.StartUtc, slot.EndUtc, item.IsBreak));

        var pinStack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        pinStack.Children.Add(new TextBlock
        {
            Text = itemIdx >= 0 ? $"#{itemIdx + 1}" : "+",
            FontSize = 9.5,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        pinBorder.Child = pinStack;

        pinBorder.PointerPressed += (s, e) =>
        {
            if (e.GetCurrentPoint(pinBorder).Properties.IsLeftButtonPressed)
            {
                e.Handled = true;
                int defaultAlloc = TotalPeriodMinutes >= 10 ? 5 : 1;
                SetItemAllocatedMinutes(item, defaultAlloc);
            }
        };

        Grid.SetColumn(pinBorder, DistributionBarContainer.ColumnDefinitions.Count - 1);
        DistributionBarContainer.Children.Add(pinBorder);
    }

    private void AttachProjectBlockDrag(Border segBorder, ReviewProjectSelectionItem item)
    {
        segBorder.PointerPressed += (s, e) =>
        {
            if (!e.GetCurrentPoint(segBorder).Properties.IsLeftButtonPressed) return;

            _draggingItem = item;
            _isUnallocDragging = false;
            _unallocDragSlot = null;
            _dragStartPoint = e.GetPosition(DistributionBarContainer);
            _dragOriginalStartUtc = item.StartUtc ?? _startUtc;
            _dragItemDuration = (item.EndUtc ?? _dragOriginalStartUtc.AddMinutes(item.AllocatedMinutes)) - _dragOriginalStartUtc;

            var sorted = _selectedItems
                .Where(it => it.AllocatedMinutes > 0 && it.StartUtc.HasValue)
                .OrderBy(it => it.StartUtc!.Value)
                .ToList();

            int idx = sorted.IndexOf(item);
            _dragMinStartUtc = (idx > 0 && sorted[idx - 1].EndUtc.HasValue) ? sorted[idx - 1].EndUtc!.Value : _startUtc;
            DateTime maxEnd = (idx < sorted.Count - 1 && sorted[idx + 1].StartUtc.HasValue) ? sorted[idx + 1].StartUtc!.Value : _endUtc;
            _dragMaxStartUtc = maxEnd - _dragItemDuration;
            if (_dragMaxStartUtc < _dragMinStartUtc) _dragMaxStartUtc = _dragMinStartUtc;

            e.Pointer.Capture(DistributionBarContainer);
            e.Handled = true;
        };
    }

    private void AttachUnallocatedDrag(Border segBorder, TimelineSlot unallocSlot)
    {
        segBorder.PointerPressed += (s, e) =>
        {
            if (!e.GetCurrentPoint(segBorder).Properties.IsLeftButtonPressed) return;

            _draggingItem = null;
            _isUnallocDragging = true;
            _unallocDragSlot = unallocSlot;
            _unallocStartPoint = e.GetPosition(DistributionBarContainer);

            e.Pointer.Capture(DistributionBarContainer);
            e.Handled = true;
        };
    }

    private void DistributionBarContainer_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggingItem != null)
        {
            Point currentPoint = e.GetPosition(DistributionBarContainer);
            double deltaX = currentPoint.X - _dragStartPoint.X;
            double trackWidth = Math.Max(1, DistributionBarContainer.Bounds.Width);
            double deltaMinutes = (deltaX / trackWidth) * TotalPeriodMinutes;

            int deltaMin = (int)Math.Round(deltaMinutes);
            DateTime newStart = _dragOriginalStartUtc.AddMinutes(deltaMin);
            if (newStart < _dragMinStartUtc) newStart = _dragMinStartUtc;
            if (newStart > _dragMaxStartUtc) newStart = _dragMaxStartUtc;

            if (_draggingItem.StartUtc != newStart)
            {
                _draggingItem.StartUtc = newStart;
                _draggingItem.EndUtc = newStart + _dragItemDuration;
                SyncAllViews(updateCards: true);
            }
        }
        else if (_isUnallocDragging && _unallocDragSlot != null)
        {
            Point currentPoint = e.GetPosition(DistributionBarContainer);
            double deltaX = currentPoint.X - _unallocStartPoint.X;
            double trackWidth = Math.Max(1, DistributionBarContainer.Bounds.Width);
            double deltaMinutes = (deltaX / trackWidth) * TotalPeriodMinutes;

            if (Math.Abs(deltaMinutes) >= 1.0)
            {
                int stepMinutes = deltaMinutes > 0 ? 1 : -1;
                _unallocStartPoint = currentPoint;

                var subsequent = _selectedItems
                    .Where(it => it.StartUtc.HasValue && it.StartUtc.Value >= _unallocDragSlot.EndUtc)
                    .OrderBy(it => it.StartUtc!.Value)
                    .ToList();

                if (subsequent.Count > 0)
                {
                    ShiftItem(subsequent[0], stepMinutes);
                }
            }
        }
    }

    private void DistributionBarContainer_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggingItem != null || _isUnallocDragging)
        {
            _draggingItem = null;
            _isUnallocDragging = false;
            _unallocDragSlot = null;
            e.Pointer.Capture(null);
            SyncAllViews(updateCards: true);
            e.Handled = true;
        }
    }

    private void RenderTimelinePreview(List<TimelineSlot> fullTimeline)
    {
        TimelinePreviewContainer.Children.Clear();

        foreach (var slot in fullTimeline)
        {
            if (slot.DurationMinutes <= 0) continue;

            var card = CreateTimelineCard(slot);
            TimelinePreviewContainer.Children.Add(card);
        }
    }

    private Control CreateTimelineCard(TimelineSlot slot)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 6),
            BorderThickness = new Thickness(1)
        };
        if (Application.Current?.TryFindResource("SurfaceRaisedBrush", out var srb) == true && srb is IBrush srBrush)
            border.Background = srBrush;
        if (Application.Current?.TryFindResource("BorderBrush", out var bb) == true && bb is IBrush bBrush)
            border.BorderBrush = bBrush;

        ToolTip.SetTip(border, CreateRichToolTip(slot));

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        int durationMin = Math.Max(1, (int)Math.Round(slot.DurationMinutes));

        var timeText = new TextBlock
        {
            Text = $"{slot.StartLocal:HH:mm} – {slot.EndLocal:HH:mm}",
            FontSize = 11.5,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        if (slot.IsUnallocated)
        {
            if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sb) == true && sb is IBrush sBrush)
                timeText.Foreground = sBrush;
        }
        else
        {
            if (Application.Current?.TryFindResource("AccentBrush", out var ab) == true && ab is IBrush aBrush)
                timeText.Foreground = aBrush;
        }
        Grid.SetColumn(timeText, 0);
        grid.Children.Add(timeText);

        var titleText = new TextBlock
        {
            Text = slot.DisplayName,
            FontSize = 11.5,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        if (slot.IsUnallocated)
        {
            if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sb2) == true && sb2 is IBrush sBrush2)
                titleText.Foreground = sBrush2;
        }
        else
        {
            if (Application.Current?.TryFindResource("PrimaryTextBrush", out var pb) == true && pb is IBrush pBrush)
                titleText.Foreground = pBrush;
        }
        Grid.SetColumn(titleText, 1);
        grid.Children.Add(titleText);

        string badgeText = slot.IsUnallocated
            ? "Unallocated"
            : (slot.IsBreak ? "Untracked" : "Recorded");

        var badge = new Border
        {
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 2, 6, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        if (Application.Current?.TryFindResource("DialogSurfaceBrush", out var dsb) == true && dsb is IBrush dsBrush)
            badge.Background = dsBrush;

        var badgeBlock = new TextBlock
        {
            Text = $"{durationMin}m · {badgeText}",
            FontSize = 10
        };
        if (slot.IsUnallocated)
        {
            if (Application.Current?.TryFindResource("SecondaryTextBrush", out var sb3) == true && sb3 is IBrush sBrush3)
                badgeBlock.Foreground = sBrush3;
        }
        else
        {
            if (Application.Current?.TryFindResource("PrimaryTextBrush", out var pb2) == true && pb2 is IBrush pBrush2)
                badgeBlock.Foreground = pBrush2;
        }
        badge.Child = badgeBlock;
        Grid.SetColumn(badge, 2);
        grid.Children.Add(badge);

        border.Child = grid;
        return border;
    }

    private void UpdateStep2SummaryText(List<TimelineSlot> fullTimeline)
    {
        int totalMin = TotalPeriodMinutes;
        int totalAllocated = (int)Math.Round(_selectedItems.Sum(it => it.AllocatedMinutes));
        int totalUnallocated = (int)Math.Round(fullTimeline.Where(s => s.IsUnallocated).Sum(s => s.DurationMinutes));

        if (totalAllocated > totalMin)
        {
            Step2AllocationSummaryText.Text = $"⚠️ Allocated: {totalAllocated}m / {totalMin}m (Exceeds review period by {totalAllocated - totalMin}m)";
            if (Application.Current?.TryFindResource("WarningBrush", out var wb) == true && wb is IBrush wBrush)
                Step2AllocationSummaryText.Foreground = wBrush;
        }
        else if (totalUnallocated > 0)
        {
            Step2AllocationSummaryText.Text = $"Allocated: {totalAllocated}m / {totalMin}m · Remaining: {totalUnallocated}m (Unallocated / Gaps)";
            if (Application.Current?.TryFindResource("PrimaryTextBrush", out var pb) == true && pb is IBrush pBrush)
                Step2AllocationSummaryText.Foreground = pBrush;
        }
        else
        {
            Step2AllocationSummaryText.Text = $"✓ Fully Allocated: {totalAllocated}m / {totalMin}m (Continuous)";
            if (Application.Current?.TryFindResource("SuccessBrush", out var sb) == true && sb is IBrush sBrush)
                Step2AllocationSummaryText.Foreground = sBrush;
        }
    }

    // ==========================================
    // SAVE & VERIFY
    // ==========================================

    private void Step2VerifyButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_handled) return;
        _handled = true;

        var fullTimeline = PeriodicReviewDataAggregator.BuildFullTimeline(_startUtc, _endUtc, _selectedItems);
        var slotsToSave = new List<PeriodicReviewStopwatchSlot>();

        // Pre-existing closed intervals inside [_startUtc, _endUtc] should be cleaned up
        if (_model != null)
        {
            foreach (var existingSlot in _model.StopwatchSlots)
            {
                if (existingSlot.ExistingIntervalId.HasValue && !existingSlot.IsOpenTimer)
                {
                    slotsToSave.Add(new PeriodicReviewStopwatchSlot
                    {
                        ExistingIntervalId = existingSlot.ExistingIntervalId,
                        SelectedProjectName = null, // Setting null deletes the closed interval
                        StartUtc = existingSlot.StartUtc,
                        EndUtc = existingSlot.EndUtc
                    });
                }
            }
        }

        // Add verified project intervals
        foreach (var slot in fullTimeline)
        {
            if (slot.IsUnallocated || slot.IsBreak || slot.Item == null || string.IsNullOrWhiteSpace(slot.Item.ProjectName) || slot.DurationMinutes <= 0)
            {
                continue;
            }

            slotsToSave.Add(new PeriodicReviewStopwatchSlot
            {
                ExistingIntervalId = null,
                SelectedProjectName = slot.Item.ProjectName.Trim(),
                StartUtc = slot.StartUtc,
                EndUtc = slot.EndUtc,
                IsTracked = true,
                IsManuallyAdded = true
            });
        }

        SavedSlots = slotsToSave;
        _onSave?.Invoke(slotsToSave);
        Close(slotsToSave);
    }

    private void SkipReportButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_handled) return;
        _handled = true;
        Close(null);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (_isStep2)
            {
                Step2BackButton_Click(this, e);
            }
            else
            {
                SkipReportButton_Click(this, e);
            }
        }
        else if (e.Key == Key.Enter && !NewProjectTextBox.IsFocused)
        {
            if (_isStep2)
            {
                e.Handled = true;
                Step2VerifyButton_Click(this, e);
            }
            else if (_selectedItems.Count > 0)
            {
                e.Handled = true;
                ContinueToStep2Button_Click(this, e);
            }
        }
    }
}
