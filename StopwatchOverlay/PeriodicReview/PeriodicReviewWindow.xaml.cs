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

    private PeriodicReviewModel? _model;
    private bool _isStep2;
    private bool _handled;

    // Selection model for Step 1 & 2
    private readonly List<ReviewProjectSelectionItem> _availableProjects = [];
    private readonly List<ReviewProjectSelectionItem> _selectedItems = [];

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
        Action<List<PeriodicReviewStopwatchSlot>>? onSave)
    {
        InitializeComponent();

        _startUtc = ProjectTimeHistory.NormalizeUtc(startUtc);
        _endUtc = ProjectTimeHistory.NormalizeUtc(endUtc);
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _runningSessions = runningSessions;
        _onSave = onSave;

        DateTime startLocal = _startUtc.ToLocalTime();
        DateTime endLocal = _endUtc.ToLocalTime();
        int totalMin = Math.Max(1, (int)Math.Round((endLocal - startLocal).TotalMinutes));
        PeriodRangeBadge.Text = $"📅 {startLocal:HH:mm} – {endLocal:HH:mm} ({totalMin}m)";
    }

    private int TotalPeriodMinutes => Math.Max(1, (int)Math.Round((_endUtc - _startUtc).TotalMinutes));

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadReviewDataAsync();
        Activate();
        Focus();
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

            InitializeProjectsList();
            RenderStep1();
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

        // 2. Known projects from history
        if (_model != null)
        {
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
                }
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

            // Empty indicator or icon
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

    private void RenderActivitySummaries()
    {
        ActivitySummariesContainer.Children.Clear();

        if (_model == null || _model.ActivitySummaries.Count == 0)
        {
            if (_model != null && !_model.ActivityWatchAvailable)
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

        foreach (var group in _model.ActivitySummaries)
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
        Brush badgeBg = group.IsIdle
            ? new SolidColorBrush(Color.FromArgb(180, 80, 90, 100))
            : (group.Percentage >= 25.0 ? (Brush)FindResource("AccentBrush") : new SolidColorBrush(Color.FromArgb(200, 36, 120, 160)));

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
        var catText = new TextBlock
        {
            Text = group.IsIdle ? "💤 Idle" : (string.Equals(group.Category, "Web", StringComparison.OrdinalIgnoreCase) ? "🌐 Web" : "💻 App"),
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

        // Smart default initial minutes: split total period minutes among selected items
        int totalMin = TotalPeriodMinutes;
        double currentAllocated = _selectedItems.Sum(it => it.AllocatedMinutes);

        if (currentAllocated <= 0)
        {
            int baseMinutes = totalMin / _selectedItems.Count;
            int remainder = totalMin % _selectedItems.Count;

            for (int i = 0; i < _selectedItems.Count; i++)
            {
                _selectedItems[i].AllocatedMinutes = baseMinutes + (i == _selectedItems.Count - 1 ? remainder : 0);
            }
        }

        _isStep2 = true;
        Step1Container.Visibility = Visibility.Collapsed;
        Step2Container.Visibility = Visibility.Visible;

        StepIndicatorBadge.Text = "Step 2 of 2: Time Allocation";
        StepSubtitleText.Text = "Step 2: Allocate time to your selected projects. Hover over any card for details.";
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
        // Re-compute timeline intervals based on current minutes
        PeriodicReviewDataAggregator.ComputeTimeline(_startUtc, _endUtc, _selectedItems);

        RenderAllocatedCards();
        RenderDistributionBar();
        RenderTimelinePreview();
        UpdateStep2SummaryText();
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

        // Header: Badge + Project Name + Order Buttons (▲ ▼)
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

        var nameText = new TextBlock
        {
            Text = item.DisplayName,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("PrimaryTextBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(nameText, 1);
        headGrid.Children.Add(nameText);

        // Move Up / Down Buttons
        var moveStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (index > 0)
        {
            var upBtn = new Button
            {
                Content = "▲",
                Width = 24,
                Height = 22,
                Margin = new Thickness(0, 0, 4, 0),
                Padding = new Thickness(0),
                FontSize = 9,
                Style = (Style)FindResource("SecondaryButton"),
                ToolTip = "Move earlier in timeline"
            };
            upBtn.Click += (_, _) =>
            {
                (_selectedItems[index], _selectedItems[index - 1]) = (_selectedItems[index - 1], _selectedItems[index]);
                for (int k = 0; k < _selectedItems.Count; k++) _selectedItems[k].SelectionOrder = k + 1;
                RenderStep2();
            };
            moveStack.Children.Add(upBtn);
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
                Style = (Style)FindResource("SecondaryButton"),
                ToolTip = "Move later in timeline"
            };
            downBtn.Click += (_, _) =>
            {
                (_selectedItems[index], _selectedItems[index + 1]) = (_selectedItems[index + 1], _selectedItems[index]);
                for (int k = 0; k < _selectedItems.Count; k++) _selectedItems[k].SelectionOrder = k + 1;
                RenderStep2();
            };
            moveStack.Children.Add(downBtn);
        }
        Grid.SetColumn(moveStack, 2);
        headGrid.Children.Add(moveStack);

        stack.Children.Add(headGrid);

        // Controls: Slider + Text box + Quick +/- Buttons
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
            item.AllocatedMinutes = Math.Round(args.NewValue);
            minInput.Text = ((int)item.AllocatedMinutes).ToString();
            border.ToolTip = CreateRichToolTip(item);
            RenderDistributionBar();
            RenderTimelinePreview();
            UpdateStep2SummaryText();
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
                    border.ToolTip = CreateRichToolTip(item);
                    RenderDistributionBar();
                    RenderTimelinePreview();
                    UpdateStep2SummaryText();
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
                slider.Value = newVal;
                minInput.Text = ((int)newVal).ToString();
                border.ToolTip = CreateRichToolTip(item);
                RenderDistributionBar();
                RenderTimelinePreview();
                UpdateStep2SummaryText();
            };
            return btn;
        }

        quickStack.Children.Add(CreateQuickButton("-5m", -5));
        quickStack.Children.Add(CreateQuickButton("+5m", 5));
        quickStack.Children.Add(CreateQuickButton("+10m", 10));

        Grid.SetColumn(quickStack, 2);
        ctrlGrid.Children.Add(quickStack);

        stack.Children.Add(ctrlGrid);
        border.Child = stack;

        return border;
    }

    private ToolTip CreateRichToolTip(ReviewProjectSelectionItem item)
    {
        int totalMin = TotalPeriodMinutes;
        double minutes = item.AllocatedMinutes;
        double hours = minutes / 60.0;
        double percentage = totalMin > 0 ? (minutes / totalMin) * 100.0 : 0.0;

        DateTime startLocal = item.CalculatedStartUtc.ToLocalTime();
        DateTime endLocal = item.CalculatedEndUtc.ToLocalTime();

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
        if (item.CalculatedEndUtc > item.CalculatedStartUtc)
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

    private void RenderDistributionBar()
    {
        DistributionBarContainer.Children.Clear();
        DistributionBarContainer.ColumnDefinitions.Clear();

        double totalMin = TotalPeriodMinutes;
        double totalAllocated = _selectedItems.Sum(it => it.AllocatedMinutes);

        if (totalMin <= 0) return;

        for (int i = 0; i < _selectedItems.Count; i++)
        {
            var item = _selectedItems[i];
            if (item.AllocatedMinutes <= 0) continue;

            DistributionBarContainer.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(item.AllocatedMinutes, GridUnitType.Star)
            });

            var segColor = PaletteColors[i % PaletteColors.Length];
            var segBorder = new Border
            {
                Background = item.IsBreak
                    ? new SolidColorBrush(Color.FromArgb(140, 100, 110, 120))
                    : new SolidColorBrush(segColor),
                ToolTip = $"{item.DisplayName}: {item.AllocatedMinutes:0}m"
            };
            Grid.SetColumn(segBorder, DistributionBarContainer.ColumnDefinitions.Count - 1);
            DistributionBarContainer.Children.Add(segBorder);
        }

        // Remaining unallocated gap
        double remaining = totalMin - totalAllocated;
        if (remaining > 0)
        {
            DistributionBarContainer.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(remaining, GridUnitType.Star)
            });

            var unallocatedSeg = new Border
            {
                Background = (Brush)FindResource("SurfaceRaisedBrush"),
                ToolTip = $"Unallocated: {remaining:0}m"
            };
            Grid.SetColumn(unallocatedSeg, DistributionBarContainer.ColumnDefinitions.Count - 1);
            DistributionBarContainer.Children.Add(unallocatedSeg);
        }
    }

    private void RenderTimelinePreview()
    {
        TimelinePreviewContainer.Children.Clear();

        var timeline = PeriodicReviewDataAggregator.ComputeTimeline(_startUtc, _endUtc, _selectedItems);

        DateTime cursor = _startUtc;

        foreach (var (start, end, item) in timeline)
        {
            var itemCard = CreateTimelineCard(
                start.ToLocalTime(),
                end.ToLocalTime(),
                item.DisplayName,
                item.IsBreak,
                isUnallocated: false);
            TimelinePreviewContainer.Children.Add(itemCard);
            cursor = end;
        }

        // Check if there is an unallocated remainder at the end
        if (cursor < _endUtc)
        {
            var unallocCard = CreateTimelineCard(
                cursor.ToLocalTime(),
                _endUtc.ToLocalTime(),
                "⚪ Unallocated (No project)",
                isBreak: false,
                isUnallocated: true);
            TimelinePreviewContainer.Children.Add(unallocCard);
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

        int durationMin = (int)Math.Round((end - start).TotalMinutes);

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
            ? "No record"
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

    private void UpdateStep2SummaryText()
    {
        int totalMin = TotalPeriodMinutes;
        int totalAllocated = (int)Math.Round(_selectedItems.Sum(it => it.AllocatedMinutes));
        int remaining = Math.Max(0, totalMin - totalAllocated);

        if (totalAllocated > totalMin)
        {
            Step2AllocationSummaryText.Text = $"⚠️ Allocated: {totalAllocated}m / {totalMin}m (Exceeds period by {totalAllocated - totalMin}m)";
            Step2AllocationSummaryText.Foreground = (Brush)FindResource("WarningBrush");
        }
        else if (remaining > 0)
        {
            Step2AllocationSummaryText.Text = $"Allocated: {totalAllocated}m / {totalMin}m · Remaining: {remaining}m (Unallocated)";
            Step2AllocationSummaryText.Foreground = (Brush)FindResource("PrimaryTextBrush");
        }
        else
        {
            Step2AllocationSummaryText.Text = $"✓ Fully Allocated: {totalAllocated}m / {totalMin}m";
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

        var timeline = PeriodicReviewDataAggregator.ComputeTimeline(_startUtc, _endUtc, _selectedItems);
        var slotsToSave = new List<PeriodicReviewStopwatchSlot>();

        // Pre-existing closed intervals inside [_startUtc, _endUtc] should be cleaned up
        if (_model != null)
        {
            foreach (var existingSlot in _model.StopwatchSlots)
            {
                if (existingSlot.ExistingIntervalId.HasValue && !existingSlot.IsOpenTimer)
                {
                    // Mark for deletion so newly verified timeline takes precedence
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
        foreach (var (start, end, item) in timeline)
        {
            if (item.IsBreak || string.IsNullOrWhiteSpace(item.ProjectName))
            {
                // Break / Empty slots remain untracked (no record saved)
                continue;
            }

            slotsToSave.Add(new PeriodicReviewStopwatchSlot
            {
                ExistingIntervalId = null,
                SelectedProjectName = item.ProjectName.Trim(),
                StartUtc = start,
                EndUtc = end,
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
