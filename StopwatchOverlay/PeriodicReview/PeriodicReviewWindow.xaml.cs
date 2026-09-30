using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StopwatchOverlay.ActivityWatch;

namespace StopwatchOverlay.PeriodicReview;

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

    // Drag-to-move state for chart timeline
    private ReviewProjectSelectionItem? _draggingItem;
    private Point _dragStartPoint;
    private DateTime _dragOriginalStartUtc;
    private TimeSpan _dragItemDuration;
    private DateTime _dragMinStartUtc;
    private DateTime _dragMaxStartUtc;

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

        _startUtc = ProjectTimeHistory.NormalizeUtc(startUtc);
        _endUtc = ProjectTimeHistory.NormalizeUtc(endUtc);
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

        InitializeProjectsList();
    }

    private int TotalPeriodMinutes => Math.Max(1, (int)Math.Round((_endUtc - _startUtc).TotalMinutes));
    private TimeSpan TotalPeriodDuration => _endUtc > _startUtc ? _endUtc - _startUtc : TimeSpan.FromMinutes(30);

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Render project cards immediately from known history so the user sees UI instantly
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
            Margin = new Thickness(0, 40, 0, 0)
        };
        var spinner = new TextBlock
        {
            Text = "⏳ Loading ActivityWatch records...",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("AccentBrush"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var sub = new TextBlock
        {
            Text = "Analyzing background application and web activity...",
            FontSize = 11,
            Foreground = (Brush)FindResource("SecondaryTextBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 5, 0, 0)
        };
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

    private UIElement CreateProjectCard(ReviewProjectSelectionItem item)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 8, 8),
            Cursor = Cursors.Hand,
            SnapsToDevicePixels = true
        };

        var contentGrid = new Grid();
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        if (item.IsSelected)
        {
            // Accent highlight border & background
            border.Background = new SolidColorBrush(Color.FromArgb(40, 66, 185, 232));
            border.BorderBrush = (Brush)FindResource("AccentBrush");
            border.BorderThickness = new Thickness(1.5);

            // Sequence Badge
            var badgeBorder = new Border
            {
                Background = (Brush)FindResource("AccentBrush"),
                CornerRadius = new CornerRadius(10),
                MinWidth = 20,
                Height = 20,
                Padding = new Thickness(5, 0, 5, 0),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var badgeText = new TextBlock
            {
                Text = item.SelectionOrder.ToString(),
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            badgeBorder.Child = badgeText;
            Grid.SetColumn(badgeBorder, 0);
            contentGrid.Children.Add(badgeBorder);
        }
        else
        {
            // Neutral card
            border.Background = (Brush)FindResource("SurfaceRaisedBrush");
            border.BorderBrush = item.IsBreak
                ? (Brush)FindResource("BorderBrush")
                : (Brush)FindResource("BorderBrush");
            border.BorderThickness = new Thickness(1);

            // Icon
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
            FontWeight = item.IsSelected ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = item.IsSelected ? (Brush)FindResource("PrimaryTextBrush") : (Brush)FindResource("SecondaryTextBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(labelText, 1);
        contentGrid.Children.Add(labelText);

        border.Child = contentGrid;

        border.MouseLeftButtonDown += (_, _) =>
        {
            ToggleProjectSelection(item);
        };

        return border;
    }

    private void ToggleProjectSelection(ReviewProjectSelectionItem item)
    {
        if (item.IsSelected)
        {
            // Deselect: remove from sequence and re-index
            _selectedItems.Remove(item);
            item.SelectionOrder = 0;
            for (int i = 0; i < _selectedItems.Count; i++)
            {
                _selectedItems[i].SelectionOrder = i + 1;
            }
        }
        else
        {
            // Select: assign next order index
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
            SelectedSequenceText.Foreground = (Brush)FindResource("SecondaryTextBrush");
            ContinueToStep2Button.IsEnabled = false;
        }
        else
        {
            var parts = _selectedItems.Select((it, idx) => $"{idx + 1}. {it.DisplayName}");
            SelectedSequenceText.Text = string.Join("  ➔  ", parts);
            SelectedSequenceText.Foreground = (Brush)FindResource("AccentBrush");
            ContinueToStep2Button.IsEnabled = true;
        }
    }

    private void AddNewProjectButton_Click(object sender, RoutedEventArgs e)
    {
        CreateAndSelectNewProject();
    }

    private void NewProjectTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            CreateAndSelectNewProject();
        }
    }

    private void CreateAndSelectNewProject()
    {
        string name = NewProjectTextBox.Text.Trim();
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

    private void DecreaseThresholdButton_Click(object sender, RoutedEventArgs e)
    {
        if (_thresholdPercent <= 1.0) _thresholdPercent = 0.0;
        else if (_thresholdPercent <= 2.0) _thresholdPercent = 1.0;
        else if (_thresholdPercent <= 3.0) _thresholdPercent = 2.0;
        else if (_thresholdPercent <= 5.0) _thresholdPercent = 3.0;
        else if (_thresholdPercent <= 10.0) _thresholdPercent = 5.0;
        else _thresholdPercent = Math.Max(0.0, _thresholdPercent - 5.0);

        UpdateThresholdUI();
    }

    private void IncreaseThresholdButton_Click(object sender, RoutedEventArgs e)
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
                ActivityWatchStatusCard.Visibility = Visibility.Visible;
                ActivityWatchStatusMessage.Text = _model.ActivityWatchMessage ?? "Cannot connect to ActivityWatch.";
            }
            else
            {
                ActivityWatchStatusCard.Visibility = Visibility.Collapsed;
                var emptyNotice = new TextBlock
                {
                    Text = "No ActivityWatch logs recorded during this period.",
                    Foreground = (Brush)FindResource("SecondaryTextBrush"),
                    FontSize = 12,
                    Margin = new Thickness(10, 20, 10, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                ActivitySummariesContainer.Children.Add(emptyNotice);
            }
            return;
        }

        ActivityWatchStatusCard.Visibility = Visibility.Collapsed;

        foreach (var group in groups)
        {
            ActivitySummariesContainer.Children.Add(CreateActivitySummaryCard(group));
        }
    }

    private UIElement CreateActivitySummaryCard(ActivitySummaryGroup group)
    {
        var border = new Border
        {
            Background = (Brush)FindResource("SurfaceRaisedBrush"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 8),
            SnapsToDevicePixels = true
        };

        var rootGrid = new Grid();
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Row 0: Top Header (Percentage badge, App/Title, Category, Duration)
        var topGrid = new Grid();
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // % badge
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Title / App
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Category badge + duration

        // Percentage Badge
        Brush badgeBg = group.IsOther
            ? new SolidColorBrush(Color.FromArgb(190, 100, 115, 130))
            : (group.IsIdle
                ? new SolidColorBrush(Color.FromArgb(180, 80, 90, 100))
                : (group.Percentage >= 25.0 ? (Brush)FindResource("AccentBrush") : new SolidColorBrush(Color.FromArgb(200, 36, 120, 160))));

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
            FontWeight = FontWeights.Bold,
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
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("PrimaryTextBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        titleStack.Children.Add(mainTitle);

        if (!string.Equals(group.App, group.Title, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(group.App))
        {
            var subApp = new TextBlock
            {
                Text = group.App,
                FontSize = 10.5,
                Foreground = (Brush)FindResource("SecondaryTextBrush"),
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            titleStack.Children.Add(subApp);
        }
        Grid.SetColumn(titleStack, 1);
        topGrid.Children.Add(titleStack);

        // Duration & Category
        var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var catBadge = new Border
        {
            Background = (Brush)FindResource("DialogSurfaceBrush"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1, 5, 1),
            Margin = new Thickness(0, 0, 8, 0)
        };
        string catLabel = group.IsOther
            ? "📦 Others"
            : (group.IsIdle ? "💤 Idle" : (string.Equals(group.Category, "Web", StringComparison.OrdinalIgnoreCase) ? "🌐 Web" : "💻 App"));
        var catText = new TextBlock
        {
            Text = catLabel,
            FontSize = 10,
            Foreground = (Brush)FindResource("SecondaryTextBrush")
        };
        catBadge.Child = catText;
        rightStack.Children.Add(catBadge);

        var durText = new TextBlock
        {
            Text = group.DurationDisplay,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("PrimaryTextBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };
        rightStack.Children.Add(durText);

        Grid.SetColumn(rightStack, 2);
        topGrid.Children.Add(rightStack);

        rootGrid.Children.Add(topGrid);

        // Row 1: Horizontal Percentage Bar Indicator
        var barTrack = new Border
        {
            Background = (Brush)FindResource("DialogSurfaceBrush"),
            CornerRadius = new CornerRadius(2),
            Height = 4,
            Margin = new Thickness(0, 8, 0, 0),
            SnapsToDevicePixels = true
        };
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
            border.ToolTip = $"Activities below {_thresholdPercent:0.#}%:\n{tip}\n\n(Click card to toggle details)";
            border.Cursor = Cursors.Hand;
            border.MouseLeftButtonUp += (s, e) =>
            {
                _isOthersExpanded = !_isOthersExpanded;
                RenderActivitySummaries();
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

                    var dot = new TextBlock { Text = "• ", Foreground = (Brush)FindResource("SecondaryTextBrush"), FontSize = 11 };
                    Grid.SetColumn(dot, 0);
                    subRow.Children.Add(dot);

                    var sTitle = new TextBlock
                    {
                        Text = sub.Title,
                        FontSize = 11,
                        Foreground = (Brush)FindResource("PrimaryTextBrush"),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        ToolTip = $"{sub.App} — {sub.Title}"
                    };
                    Grid.SetColumn(sTitle, 1);
                    subRow.Children.Add(sTitle);

                    var sDur = new TextBlock
                    {
                        Text = $"{sub.PercentageDisplay} ({sub.DurationDisplay})",
                        FontSize = 10.5,
                        Foreground = (Brush)FindResource("SecondaryTextBrush"),
                        Margin = new Thickness(6, 0, 0, 0)
                    };
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

    private async void RetryActivityWatchButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadReviewDataAsync();
    }

    // ==========================================
    // STEP 2: Time Allocation & Verification
    // ==========================================

    private void ContinueToStep2Button_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedItems.Count == 0) return;

        int totalMin = TotalPeriodMinutes;
        double currentAllocated = _selectedItems.Sum(it => it.AllocatedMinutes);

        // Smart initial allocation if unassigned
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
            // Ensure StartUtc and EndUtc exist
            DateTime cursor = _startUtc;
            foreach (var it in _selectedItems)
            {
                if (!it.StartUtc.HasValue || !it.EndUtc.HasValue)
                {
                    it.StartUtc = cursor;
                    it.EndUtc = cursor.AddMinutes(it.AllocatedMinutes);
                }
                cursor = it.EndUtc ?? cursor;
            }
        }

        _isStep2 = true;
        Step1Container.Visibility = Visibility.Collapsed;
        Step2Container.Visibility = Visibility.Visible;

        StepIndicatorBadge.Text = "Step 2 of 2: Time Allocation";
        StepSubtitleText.Text = "Step 2: Allocate & position time. Drag items on the timeline to move them. Click ∨ to set exact times.";
        FooterShortcutHintText.Text = "Press Esc to go back · Enter to verify and save";

        Step1SkipButton.Visibility = Visibility.Collapsed;
        Step2BackButton.Visibility = Visibility.Visible;
        Step2VerifyButton.Visibility = Visibility.Visible;

        RenderStep2();
    }

    private void Step2BackButton_Click(object sender, RoutedEventArgs e)
    {
        _isStep2 = false;
        Step2Container.Visibility = Visibility.Collapsed;
        Step1Container.Visibility = Visibility.Visible;

        StepIndicatorBadge.Text = "Step 1 of 2: Projects & Overview";
        StepSubtitleText.Text = "Step 1: Select the projects you worked on in order (or breaks), guided by your ActivityWatch percentage report.";
        FooterShortcutHintText.Text = "Press Esc to skip without changes";

        Step1SkipButton.Visibility = Visibility.Visible;
        Step2BackButton.Visibility = Visibility.Collapsed;
        Step2VerifyButton.Visibility = Visibility.Collapsed;

        RenderStep1();
    }

    private void RenderStep2()
    {
        // Update Time Scale Markers
        DateTime startLocal = _startUtc.ToLocalTime();
        DateTime endLocal = _endUtc.ToLocalTime();
        DateTime midLocal = startLocal + TimeSpan.FromMinutes(TotalPeriodMinutes / 2.0);

        TimelineScaleStartText.Text = startLocal.ToString("HH:mm");
        TimelineScaleMidText.Text = midLocal.ToString("HH:mm");
        TimelineScaleEndText.Text = endLocal.ToString("HH:mm");

        // Build full contiguous timeline (projects + unallocated gaps)
        var fullTimeline = PeriodicReviewDataAggregator.BuildFullTimeline(_startUtc, _endUtc, _selectedItems);

        RenderAllocatedCards();
        RenderDistributionBar(fullTimeline);
        RenderTimelinePreview(fullTimeline);
        UpdateStep2SummaryText(fullTimeline);
    }

    private void RenderAllocatedCards()
    {
        AllocatedProjectsContainer.Children.Clear();

        int totalMin = TotalPeriodMinutes;

        for (int i = 0; i < _selectedItems.Count; i++)
        {
            var item = _selectedItems[i];
            int index = i;
            var card = CreateAllocatedProjectCard(item, index, totalMin);
            AllocatedProjectsContainer.Children.Add(card);
        }
    }

    private UIElement CreateAllocatedProjectCard(ReviewProjectSelectionItem item, int index, int maxMinutes)
    {
        var border = new Border
        {
            Background = (Brush)FindResource("SurfaceRaisedBrush"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 10),
            SnapsToDevicePixels = true
        };

        var color = PaletteColors[index % PaletteColors.Length];

        // Attach rich Hover ToolTip
        border.ToolTip = CreateRichToolTip(item);

        var stack = new StackPanel();

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
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(badge, 0);
        headGrid.Children.Add(badge);

        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var nameText = new TextBlock
        {
            Text = item.DisplayName,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("PrimaryTextBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };
        titleStack.Children.Add(nameText);

        if (item.StartUtc.HasValue && item.EndUtc.HasValue)
        {
            var rangeBadge = new Border
            {
                Background = (Brush)FindResource("DialogSurfaceBrush"),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            rangeBadge.Child = new TextBlock
            {
                Text = $"{item.StartUtc.Value.ToLocalTime():HH:mm} – {item.EndUtc.Value.ToLocalTime():HH:mm} ({item.AllocatedMinutes:0}m)",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("AccentBrush")
            };
            titleStack.Children.Add(rangeBadge);
        }
        Grid.SetColumn(titleStack, 1);
        headGrid.Children.Add(titleStack);

        // Header Action Controls: Shift (◀ ▶) + Move (▲ ▼) + Expand Chevron (∨)
        var actionStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        // Shift Buttons (◀ 1m earlier, ▶ 1m later)
        var shiftLeftBtn = new Button
        {
            Content = "◀",
            Width = 24,
            Height = 22,
            Margin = new Thickness(0, 0, 3, 0),
            Padding = new Thickness(0),
            FontSize = 9,
            Style = (Style)FindResource("SecondaryButton"),
            ToolTip = "Shift 1 minute earlier on timeline"
        };
        shiftLeftBtn.Click += (_, _) => ShiftItem(item, -1);
        actionStack.Children.Add(shiftLeftBtn);

        var shiftRightBtn = new Button
        {
            Content = "▶",
            Width = 24,
            Height = 22,
            Margin = new Thickness(0, 0, 6, 0),
            Padding = new Thickness(0),
            FontSize = 9,
            Style = (Style)FindResource("SecondaryButton"),
            ToolTip = "Shift 1 minute later on timeline"
        };
        shiftRightBtn.Click += (_, _) => ShiftItem(item, 1);
        actionStack.Children.Add(shiftRightBtn);

        // Order Buttons (▲ ▼)
        if (index > 0)
        {
            var upBtn = new Button
            {
                Content = "▲",
                Width = 24,
                Height = 22,
                Margin = new Thickness(0, 0, 3, 0),
                Padding = new Thickness(0),
                FontSize = 9,
                Style = (Style)FindResource("SecondaryButton"),
                ToolTip = "Swap order with previous project"
            };
            upBtn.Click += (_, _) =>
            {
                (_selectedItems[index], _selectedItems[index - 1]) = (_selectedItems[index - 1], _selectedItems[index]);
                for (int k = 0; k < _selectedItems.Count; k++) _selectedItems[k].SelectionOrder = k + 1;
                RenderStep2();
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
                Margin = new Thickness(0, 0, 6, 0),
                Padding = new Thickness(0),
                FontSize = 9,
                Style = (Style)FindResource("SecondaryButton"),
                ToolTip = "Swap order with next project"
            };
            downBtn.Click += (_, _) =>
            {
                (_selectedItems[index], _selectedItems[index + 1]) = (_selectedItems[index + 1], _selectedItems[index]);
                for (int k = 0; k < _selectedItems.Count; k++) _selectedItems[k].SelectionOrder = k + 1;
                RenderStep2();
            };
            actionStack.Children.Add(downBtn);
        }

        // Expand Chevron Button (∨ / ▲)
        var expandBtn = new Button
        {
            Content = item.IsExpanded ? "▲" : "∨",
            Width = 26,
            Height = 22,
            Padding = new Thickness(0),
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Style = (Style)FindResource("SecondaryButton"),
            ToolTip = item.IsExpanded ? "Collapse exact time fields" : "Expand to set exact start and end times"
        };
        expandBtn.Click += (_, _) =>
        {
            item.IsExpanded = !item.IsExpanded;
            RenderAllocatedCards();
        };
        actionStack.Children.Add(expandBtn);

        Grid.SetColumn(actionStack, 2);
        headGrid.Children.Add(actionStack);
        stack.Children.Add(headGrid);

        // 2. Duration Controls: Slider + Text box + Quick +/- Buttons
        var ctrlGrid = new Grid();
        ctrlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Slider
        ctrlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // TextBox (min)
        ctrlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Preset Buttons

        var slider = new Slider
        {
            Minimum = 0,
            Maximum = maxMinutes,
            Value = item.AllocatedMinutes,
            TickFrequency = 5,
            IsSnapToTickEnabled = false,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };

        var minInput = new TextBox
        {
            Text = ((int)Math.Round(item.AllocatedMinutes)).ToString(),
            Width = 44,
            Height = 26,
            TextAlignment = TextAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Background = (Brush)FindResource("DialogSurfaceBrush"),
            Foreground = (Brush)FindResource("PrimaryTextBrush"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 8, 0)
        };

        slider.ValueChanged += (_, args) =>
        {
            double newMin = Math.Round(args.NewValue);
            if (item.AllocatedMinutes != newMin)
            {
                item.AllocatedMinutes = newMin;
                if (item.StartUtc.HasValue) item.EndUtc = item.StartUtc.Value.AddMinutes(newMin);
                minInput.Text = ((int)newMin).ToString();
                border.ToolTip = CreateRichToolTip(item);
                var fullTimeline = PeriodicReviewDataAggregator.BuildFullTimeline(_startUtc, _endUtc, _selectedItems);
                RenderDistributionBar(fullTimeline);
                RenderTimelinePreview(fullTimeline);
                UpdateStep2SummaryText(fullTimeline);
            }
        };

        minInput.TextChanged += (_, _) =>
        {
            if (int.TryParse(minInput.Text, out int parsed))
            {
                int clamped = Math.Clamp(parsed, 0, maxMinutes);
                if ((int)item.AllocatedMinutes != clamped)
                {
                    item.AllocatedMinutes = clamped;
                    slider.Value = clamped;
                    if (item.StartUtc.HasValue) item.EndUtc = item.StartUtc.Value.AddMinutes(clamped);
                    border.ToolTip = CreateRichToolTip(item);
                    var fullTimeline = PeriodicReviewDataAggregator.BuildFullTimeline(_startUtc, _endUtc, _selectedItems);
                    RenderDistributionBar(fullTimeline);
                    RenderTimelinePreview(fullTimeline);
                    UpdateStep2SummaryText(fullTimeline);
                }
            }
        };

        Grid.SetColumn(slider, 0);
        ctrlGrid.Children.Add(slider);

        Grid.SetColumn(minInput, 1);
        ctrlGrid.Children.Add(minInput);

        // Preset +/- buttons
        var quickStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        Button CreateQuickButton(string label, int delta)
        {
            var btn = new Button
            {
                Content = label,
                Padding = new Thickness(6, 2, 6, 2),
                Height = 26,
                Margin = new Thickness(0, 0, 4, 0),
                FontSize = 11,
                Style = (Style)FindResource("SecondaryButton")
            };
            btn.Click += (_, _) =>
            {
                double newVal = Math.Clamp(item.AllocatedMinutes + delta, 0, maxMinutes);
                item.AllocatedMinutes = newVal;
                if (item.StartUtc.HasValue) item.EndUtc = item.StartUtc.Value.AddMinutes(newVal);
                slider.Value = newVal;
                minInput.Text = ((int)newVal).ToString();
                border.ToolTip = CreateRichToolTip(item);
                var fullTimeline = PeriodicReviewDataAggregator.BuildFullTimeline(_startUtc, _endUtc, _selectedItems);
                RenderDistributionBar(fullTimeline);
                RenderTimelinePreview(fullTimeline);
                UpdateStep2SummaryText(fullTimeline);
            };
            return btn;
        }

        quickStack.Children.Add(CreateQuickButton("-5m", -5));
        quickStack.Children.Add(CreateQuickButton("+5m", 5));
        quickStack.Children.Add(CreateQuickButton("+10m", 10));

        Grid.SetColumn(quickStack, 2);
        ctrlGrid.Children.Add(quickStack);
        stack.Children.Add(ctrlGrid);

        // 3. Expandable Section: Exact Start & End Time Fields (when IsExpanded == true)
        if (item.IsExpanded)
        {
            var expandPanel = CreateExactTimePanel(item);
            stack.Children.Add(expandPanel);
        }

        border.Child = stack;
        return border;
    }

    private UIElement CreateExactTimePanel(ReviewProjectSelectionItem item)
    {
        var expandBorder = new Border
        {
            Background = (Brush)FindResource("DialogSurfaceBrush"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 10, 0, 0)
        };

        var rootStack = new StackPanel();

        var headerText = new TextBlock
        {
            Text = "🕒 Exact Start & End Time (Custom Timeline Positioning)",
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("AccentBrush"),
            Margin = new Thickness(0, 0, 0, 8)
        };
        rootStack.Children.Add(headerText);

        var timeGrid = new Grid();
        timeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        timeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        timeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Left Column: Start Time Field
        var startStack = new StackPanel();
        startStack.Children.Add(new TextBlock
        {
            Text = "Start Time (HH:mm):",
            FontSize = 11,
            Foreground = (Brush)FindResource("SecondaryTextBrush"),
            Margin = new Thickness(0, 0, 0, 4)
        });

        var startInputRow = new Grid();
        startInputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        startInputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var startBox = new TextBox
        {
            Text = item.StartUtc?.ToLocalTime().ToString("HH:mm") ?? _startUtc.ToLocalTime().ToString("HH:mm"),
            Height = 28,
            Padding = new Thickness(6, 2, 6, 2),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Background = (Brush)FindResource("SurfaceRaisedBrush"),
            Foreground = (Brush)FindResource("PrimaryTextBrush"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        startBox.LostFocus += (_, _) =>
        {
            if (TryParseLocalTime(startBox.Text, _startUtc, out DateTime parsedUtc))
            {
                SetItemStartTime(item, parsedUtc);
            }
            else
            {
                startBox.Text = item.StartUtc?.ToLocalTime().ToString("HH:mm") ?? "";
            }
        };
        startBox.KeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Enter)
            {
                ke.Handled = true;
                if (TryParseLocalTime(startBox.Text, _startUtc, out DateTime parsedUtc))
                {
                    SetItemStartTime(item, parsedUtc);
                }
            }
        };
        Grid.SetColumn(startBox, 0);
        startInputRow.Children.Add(startBox);

        // Start Nudge Buttons (-1m, +1m)
        var startNudgeStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 0, 0) };
        Button sMinus1 = new() { Content = "-1m", Height = 28, Padding = new Thickness(5, 0, 5, 0), FontSize = 10, Style = (Style)FindResource("SecondaryButton"), Margin = new Thickness(0, 0, 2, 0) };
        sMinus1.Click += (_, _) => ShiftItem(item, -1);
        startNudgeStack.Children.Add(sMinus1);

        Button sPlus1 = new() { Content = "+1m", Height = 28, Padding = new Thickness(5, 0, 5, 0), FontSize = 10, Style = (Style)FindResource("SecondaryButton") };
        sPlus1.Click += (_, _) => ShiftItem(item, 1);
        startNudgeStack.Children.Add(sPlus1);

        Grid.SetColumn(startNudgeStack, 1);
        startInputRow.Children.Add(startNudgeStack);
        startStack.Children.Add(startInputRow);
        Grid.SetColumn(startStack, 0);
        timeGrid.Children.Add(startStack);

        // Right Column: End Time Field
        var endStack = new StackPanel();
        endStack.Children.Add(new TextBlock
        {
            Text = "End Time (HH:mm):",
            FontSize = 11,
            Foreground = (Brush)FindResource("SecondaryTextBrush"),
            Margin = new Thickness(0, 0, 0, 4)
        });

        var endInputRow = new Grid();
        endInputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        endInputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var endBox = new TextBox
        {
            Text = item.EndUtc?.ToLocalTime().ToString("HH:mm") ?? _endUtc.ToLocalTime().ToString("HH:mm"),
            Height = 28,
            Padding = new Thickness(6, 2, 6, 2),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Background = (Brush)FindResource("SurfaceRaisedBrush"),
            Foreground = (Brush)FindResource("PrimaryTextBrush"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        endBox.LostFocus += (_, _) =>
        {
            if (TryParseLocalTime(endBox.Text, _startUtc, out DateTime parsedUtc))
            {
                SetItemEndTime(item, parsedUtc);
            }
            else
            {
                endBox.Text = item.EndUtc?.ToLocalTime().ToString("HH:mm") ?? "";
            }
        };
        endBox.KeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Enter)
            {
                ke.Handled = true;
                if (TryParseLocalTime(endBox.Text, _startUtc, out DateTime parsedUtc))
                {
                    SetItemEndTime(item, parsedUtc);
                }
            }
        };
        Grid.SetColumn(endBox, 0);
        endInputRow.Children.Add(endBox);

        // End Nudge Buttons (-1m, +1m)
        var endNudgeStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 0, 0) };
        Button eMinus1 = new() { Content = "-1m", Height = 28, Padding = new Thickness(5, 0, 5, 0), FontSize = 10, Style = (Style)FindResource("SecondaryButton"), Margin = new Thickness(0, 0, 2, 0) };
        eMinus1.Click += (_, _) => AdjustEnd(item, -1);
        endNudgeStack.Children.Add(eMinus1);

        Button ePlus1 = new() { Content = "+1m", Height = 28, Padding = new Thickness(5, 0, 5, 0), FontSize = 10, Style = (Style)FindResource("SecondaryButton") };
        ePlus1.Click += (_, _) => AdjustEnd(item, 1);
        endNudgeStack.Children.Add(ePlus1);

        Grid.SetColumn(endNudgeStack, 1);
        endInputRow.Children.Add(endNudgeStack);
        endStack.Children.Add(endInputRow);
        Grid.SetColumn(endStack, 2);
        timeGrid.Children.Add(endStack);

        rootStack.Children.Add(timeGrid);
        expandBorder.Child = rootStack;
        return expandBorder;
    }

    private static bool TryParseLocalTime(string text, DateTime referenceUtc, out DateTime resultUtc)
    {
        resultUtc = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string[] formats = ["H:mm", "HH:mm", "h:mm", "hh:mm", "H:m", "h:m"];
        if (DateTime.TryParseExact(text.Trim(), formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime parsed))
        {
            DateTime refLocal = referenceUtc.ToLocalTime();
            DateTime targetLocal = new DateTime(refLocal.Year, refLocal.Month, refLocal.Day, parsed.Hour, parsed.Minute, 0, DateTimeKind.Local);
            resultUtc = targetLocal.ToUniversalTime();
            return true;
        }
        return false;
    }

    private void SetItemStartTime(ReviewProjectSelectionItem item, DateTime newStartUtc)
    {
        if (newStartUtc < _startUtc) newStartUtc = _startUtc;
        if (newStartUtc > _endUtc) newStartUtc = _endUtc;

        TimeSpan duration = TimeSpan.FromMinutes(item.AllocatedMinutes);
        DateTime newEndUtc = newStartUtc + duration;
        if (newEndUtc > _endUtc)
        {
            newEndUtc = _endUtc;
            newStartUtc = newEndUtc - duration;
            if (newStartUtc < _startUtc) newStartUtc = _startUtc;
        }

        item.StartUtc = newStartUtc;
        item.EndUtc = newEndUtc;
        item.AllocatedMinutes = Math.Round((newEndUtc - newStartUtc).TotalMinutes);

        RenderStep2();
    }

    private void SetItemEndTime(ReviewProjectSelectionItem item, DateTime newEndUtc)
    {
        if (newEndUtc > _endUtc) newEndUtc = _endUtc;
        DateTime start = item.StartUtc ?? _startUtc;
        if (newEndUtc <= start) newEndUtc = start.AddMinutes(1);

        item.EndUtc = newEndUtc;
        item.AllocatedMinutes = Math.Round((newEndUtc - start).TotalMinutes);

        RenderStep2();
    }

    private void ShiftItem(ReviewProjectSelectionItem item, int deltaMinutes)
    {
        if (!item.StartUtc.HasValue || !item.EndUtc.HasValue) return;

        TimeSpan duration = item.EndUtc.Value - item.StartUtc.Value;
        DateTime newStart = item.StartUtc.Value.AddMinutes(deltaMinutes);
        DateTime newEnd = newStart + duration;

        // Find preceding and following items bounds
        var sorted = _selectedItems
            .Where(it => it.AllocatedMinutes > 0 && it.StartUtc.HasValue)
            .OrderBy(it => it.StartUtc!.Value)
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

        RenderStep2();
    }

    private void AdjustEnd(ReviewProjectSelectionItem item, int deltaMinutes)
    {
        if (!item.StartUtc.HasValue || !item.EndUtc.HasValue) return;

        DateTime newEnd = item.EndUtc.Value.AddMinutes(deltaMinutes);

        var sorted = _selectedItems
            .Where(it => it.AllocatedMinutes > 0 && it.StartUtc.HasValue)
            .OrderBy(it => it.StartUtc!.Value)
            .ToList();

        int idx = sorted.IndexOf(item);
        DateTime maxEnd = (idx < sorted.Count - 1 && sorted[idx + 1].StartUtc.HasValue) ? sorted[idx + 1].StartUtc!.Value : _endUtc;

        if (newEnd > maxEnd) newEnd = maxEnd;
        if (newEnd <= item.StartUtc.Value) newEnd = item.StartUtc.Value.AddMinutes(1);

        item.EndUtc = newEnd;
        item.AllocatedMinutes = Math.Round((newEnd - item.StartUtc.Value).TotalMinutes);

        RenderStep2();
    }

    private ToolTip CreateRichToolTip(ReviewProjectSelectionItem item)
    {
        int totalMin = TotalPeriodMinutes;
        double minutes = item.AllocatedMinutes;
        double hours = minutes / 60.0;
        double percentage = totalMin > 0 ? (minutes / totalMin) * 100.0 : 0.0;

        DateTime startLocal = (item.StartUtc ?? _startUtc).ToLocalTime();
        DateTime endLocal = (item.EndUtc ?? _endUtc).ToLocalTime();

        var tt = new ToolTip
        {
            Background = (Brush)FindResource("DialogSurfaceBrush"),
            BorderBrush = (Brush)FindResource("AccentBrush"),
            BorderThickness = new Thickness(1.5),
            Foreground = (Brush)FindResource("PrimaryTextBrush"),
            Padding = new Thickness(10, 8, 10, 8)
        };

        var ttStack = new StackPanel();
        ttStack.Children.Add(new TextBlock
        {
            Text = $"🏷️ {item.DisplayName}",
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = (Brush)FindResource("PrimaryTextBrush"),
            Margin = new Thickness(0, 0, 0, 4)
        });
        ttStack.Children.Add(new TextBlock
        {
            Text = $"⏱️ Duration: {minutes:0} minutes ({hours:0.00} hours)",
            FontSize = 11.5,
            Foreground = (Brush)FindResource("AccentBrush")
        });
        ttStack.Children.Add(new TextBlock
        {
            Text = $"📊 Share: {percentage:0.#}% of review period",
            FontSize = 11.5,
            Foreground = (Brush)FindResource("PrimaryTextBrush"),
            Margin = new Thickness(0, 2, 0, 0)
        });
        if (endLocal > startLocal)
        {
            ttStack.Children.Add(new TextBlock
            {
                Text = $"📅 Timeline: {startLocal:HH:mm} – {endLocal:HH:mm}",
                FontSize = 11.5,
                Foreground = (Brush)FindResource("SecondaryTextBrush"),
                Margin = new Thickness(0, 2, 0, 0)
            });
        }

        tt.Content = ttStack;
        return tt;
    }

    private void RenderDistributionBar(List<TimelineSlot> slots)
    {
        DistributionBarContainer.Children.Clear();
        DistributionBarContainer.ColumnDefinitions.Clear();

        if (slots.Count == 0) return;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot.DurationMinutes <= 0) continue;

            DistributionBarContainer.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(slot.DurationMinutes, GridUnitType.Star)
            });

            var segBorder = new Border
            {
                BorderBrush = (Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(0.5),
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(1, 0, 1, 0),
                SnapsToDevicePixels = true,
                ClipToBounds = true
            };

            if (slot.IsUnallocated)
            {
                // Unallocated Gap block
                segBorder.Background = new SolidColorBrush(Color.FromArgb(80, 50, 60, 70));
                segBorder.Cursor = Cursors.SizeWE;

                var unallocStack = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                unallocStack.Children.Add(new TextBlock
                {
                    Text = $"⚪ {slot.DurationMinutes:0}m",
                    FontSize = 10,
                    Foreground = (Brush)FindResource("SecondaryTextBrush"),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                segBorder.Child = unallocStack;

                // Tooltip
                segBorder.ToolTip = new ToolTip
                {
                    Content = $"⚪ Unallocated Time Gap\n⏱️ {slot.DurationMinutes:0}m ({slot.DurationMinutes / 60.0:0.00}h)\n📅 {slot.StartLocal:HH:mm} – {slot.EndLocal:HH:mm}\n💡 Drag or click to shift adjacent tasks"
                };

                // Dragging unallocated block shifts subsequent tasks!
                int slotIndex = i;
                AttachUnallocatedDrag(segBorder, slot, slotIndex);
            }
            else
            {
                // Project Item block
                var item = slot.Item!;
                int itemIdx = _selectedItems.IndexOf(item);
                var segColor = PaletteColors[itemIdx >= 0 ? (itemIdx % PaletteColors.Length) : 0];

                segBorder.Background = item.IsBreak
                    ? new SolidColorBrush(Color.FromArgb(160, 100, 110, 120))
                    : new SolidColorBrush(segColor);

                segBorder.Cursor = Cursors.SizeWE;
                segBorder.ToolTip = CreateRichToolTip(item);

                var segContent = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                segContent.Children.Add(new TextBlock
                {
                    Text = item.DisplayName,
                    FontSize = 10.5,
                    FontWeight = FontWeights.SemiBold,
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

                // Attach Drag handler to project block
                AttachProjectBlockDrag(segBorder, item);
            }

            Grid.SetColumn(segBorder, DistributionBarContainer.ColumnDefinitions.Count - 1);
            DistributionBarContainer.Children.Add(segBorder);
        }
    }

    private void AttachProjectBlockDrag(Border segBorder, ReviewProjectSelectionItem item)
    {
        segBorder.PreviewMouseLeftButtonDown += (s, e) =>
        {
            _draggingItem = item;
            _dragStartPoint = e.GetPosition(DistributionBarContainer);
            _dragOriginalStartUtc = item.StartUtc ?? _startUtc;
            _dragItemDuration = (item.EndUtc ?? _dragOriginalStartUtc.AddMinutes(item.AllocatedMinutes)) - _dragOriginalStartUtc;

            // Compute bounding constraints based on adjacent items
            var sorted = _selectedItems
                .Where(it => it.AllocatedMinutes > 0 && it.StartUtc.HasValue)
                .OrderBy(it => it.StartUtc!.Value)
                .ToList();

            int idx = sorted.IndexOf(item);
            _dragMinStartUtc = (idx > 0 && sorted[idx - 1].EndUtc.HasValue) ? sorted[idx - 1].EndUtc!.Value : _startUtc;
            DateTime maxEnd = (idx < sorted.Count - 1 && sorted[idx + 1].StartUtc.HasValue) ? sorted[idx + 1].StartUtc!.Value : _endUtc;
            _dragMaxStartUtc = maxEnd - _dragItemDuration;
            if (_dragMaxStartUtc < _dragMinStartUtc) _dragMaxStartUtc = _dragMinStartUtc;

            segBorder.CaptureMouse();
            e.Handled = true;
        };

        segBorder.PreviewMouseMove += (s, e) =>
        {
            if (_draggingItem == item && segBorder.IsMouseCaptured)
            {
                Point currentPoint = e.GetPosition(DistributionBarContainer);
                double deltaX = currentPoint.X - _dragStartPoint.X;
                double trackWidth = Math.Max(1, DistributionBarContainer.ActualWidth);
                double deltaMinutes = (deltaX / trackWidth) * TotalPeriodMinutes;

                DateTime newStart = _dragOriginalStartUtc.AddMinutes(deltaMinutes);
                if (newStart < _dragMinStartUtc) newStart = _dragMinStartUtc;
                if (newStart > _dragMaxStartUtc) newStart = _dragMaxStartUtc;

                item.StartUtc = newStart;
                item.EndUtc = newStart + _dragItemDuration;

                var fullTimeline = PeriodicReviewDataAggregator.BuildFullTimeline(_startUtc, _endUtc, _selectedItems);
                RenderDistributionBar(fullTimeline);
                RenderTimelinePreview(fullTimeline);
                UpdateStep2SummaryText(fullTimeline);
            }
        };

        segBorder.PreviewMouseLeftButtonUp += (s, e) =>
        {
            if (_draggingItem == item)
            {
                _draggingItem = null;
                segBorder.ReleaseMouseCapture();

                // Snap to closest integer minute on mouse release
                if (item.StartUtc.HasValue)
                {
                    DateTime sUtc = item.StartUtc.Value;
                    int sec = sUtc.Second;
                    DateTime rounded = sec >= 30 ? sUtc.AddSeconds(60 - sec) : sUtc.AddSeconds(-sec);
                    if (rounded < _dragMinStartUtc) rounded = _dragMinStartUtc;
                    if (rounded > _dragMaxStartUtc) rounded = _dragMaxStartUtc;
                    item.StartUtc = rounded;
                    item.EndUtc = rounded + _dragItemDuration;
                }

                RenderStep2();
                e.Handled = true;
            }
        };
    }

    private void AttachUnallocatedDrag(Border segBorder, TimelineSlot unallocSlot, int slotIndex)
    {
        Point unallocStartPoint = default;
        bool isUnallocDragging = false;

        segBorder.PreviewMouseLeftButtonDown += (s, e) =>
        {
            isUnallocDragging = true;
            unallocStartPoint = e.GetPosition(DistributionBarContainer);
            segBorder.CaptureMouse();
            e.Handled = true;
        };

        segBorder.PreviewMouseMove += (s, e) =>
        {
            if (isUnallocDragging && segBorder.IsMouseCaptured)
            {
                Point currentPoint = e.GetPosition(DistributionBarContainer);
                double deltaX = currentPoint.X - unallocStartPoint.X;
                double trackWidth = Math.Max(1, DistributionBarContainer.ActualWidth);
                double deltaMinutes = (deltaX / trackWidth) * TotalPeriodMinutes;

                if (Math.Abs(deltaMinutes) >= 1.0)
                {
                    int stepMinutes = deltaMinutes > 0 ? 1 : -1;
                    unallocStartPoint = currentPoint;

                    // If dragged right, shift subsequent items right; if dragged left, shift them left
                    var subsequent = _selectedItems
                        .Where(it => it.StartUtc.HasValue && it.StartUtc.Value >= unallocSlot.EndUtc)
                        .OrderBy(it => it.StartUtc!.Value)
                        .ToList();

                    if (subsequent.Count > 0)
                    {
                        ShiftItem(subsequent[0], stepMinutes);
                    }
                }
            }
        };

        segBorder.PreviewMouseLeftButtonUp += (s, e) =>
        {
            if (isUnallocDragging)
            {
                isUnallocDragging = false;
                segBorder.ReleaseMouseCapture();
                RenderStep2();
                e.Handled = true;
            }
        };
    }

    private void RenderTimelinePreview(List<TimelineSlot> fullTimeline)
    {
        TimelinePreviewContainer.Children.Clear();

        foreach (var slot in fullTimeline)
        {
            if (slot.DurationMinutes <= 0) continue;

            var card = CreateTimelineCard(
                slot.StartLocal,
                slot.EndLocal,
                slot.DisplayName,
                slot.IsBreak,
                slot.IsUnallocated);

            TimelinePreviewContainer.Children.Add(card);
        }
    }

    private UIElement CreateTimelineCard(DateTime start, DateTime end, string title, bool isBreak, bool isUnallocated)
    {
        var border = new Border
        {
            Background = (Brush)FindResource("SurfaceRaisedBrush"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 6),
            SnapsToDevicePixels = true
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        int durationMin = Math.Max(1, (int)Math.Round((end - start).TotalMinutes));

        var timeText = new TextBlock
        {
            Text = $"{start:HH:mm} – {end:HH:mm}",
            FontSize = 11.5,
            FontWeight = FontWeights.Bold,
            Foreground = isUnallocated ? (Brush)FindResource("SecondaryTextBrush") : (Brush)FindResource("AccentBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        Grid.SetColumn(timeText, 0);
        grid.Children.Add(timeText);

        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = isUnallocated ? (Brush)FindResource("SecondaryTextBrush") : (Brush)FindResource("PrimaryTextBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(titleText, 1);
        grid.Children.Add(titleText);

        var badgeText = isUnallocated
            ? "Unallocated"
            : (isBreak ? "Untracked" : "Recorded");

        var badge = new Border
        {
            Background = (Brush)FindResource("DialogSurfaceBrush"),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 2, 6, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        badge.Child = new TextBlock
        {
            Text = $"{durationMin}m · {badgeText}",
            FontSize = 10,
            Foreground = isUnallocated ? (Brush)FindResource("SecondaryTextBrush") : (Brush)FindResource("PrimaryTextBrush")
        };
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
            Step2AllocationSummaryText.Foreground = (Brush)FindResource("WarningBrush");
        }
        else if (totalUnallocated > 0)
        {
            Step2AllocationSummaryText.Text = $"Allocated: {totalAllocated}m / {totalMin}m · Remaining: {totalUnallocated}m (Unallocated / Gaps)";
            Step2AllocationSummaryText.Foreground = (Brush)FindResource("PrimaryTextBrush");
        }
        else
        {
            Step2AllocationSummaryText.Text = $"✓ Fully Allocated: {totalAllocated}m / {totalMin}m (Continuous)";
            Step2AllocationSummaryText.Foreground = (Brush)FindResource("SuccessBrush");
        }
    }

    // ==========================================
    // SAVE & VERIFY
    // ==========================================

    private void Step2VerifyButton_Click(object sender, RoutedEventArgs e)
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
            if (slot.IsUnallocated || slot.IsBreak || slot.Item == null || string.IsNullOrWhiteSpace(slot.Item.ProjectName))
            {
                // Break or Unallocated gaps remain untracked (no record saved)
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

        _onSave?.Invoke(slotsToSave);
        Close();
    }

    private void SkipReportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_handled) return;
        _handled = true;
        Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (_isStep2)
            {
                Step2BackButton_Click(sender, e);
            }
            else
            {
                SkipReportButton_Click(sender, e);
            }
        }
        else if (e.Key == Key.Enter && !NewProjectTextBox.IsFocused)
        {
            if (_isStep2)
            {
                e.Handled = true;
                Step2VerifyButton_Click(sender, e);
            }
            else if (_selectedItems.Count > 0)
            {
                e.Handled = true;
                ContinueToStep2Button_Click(sender, e);
            }
        }
    }
}
