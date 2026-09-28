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
    private int _currentFilterSeconds;
    private bool _handled;

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

        _currentFilterSeconds = Math.Max(1, settings.PeriodicReviewMinDurationSeconds);
        FilterDurationTextBox.Text = _currentFilterSeconds.ToString();

        DateTime startLocal = _startUtc.ToLocalTime();
        DateTime endLocal = _endUtc.ToLocalTime();
        int totalMin = Math.Max(1, (int)Math.Round((endLocal - startLocal).TotalMinutes));
        PeriodRangeBadge.Text = $"📅 {startLocal:HH:mm} – {endLocal:HH:mm} ({totalMin}m)";
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await ReloadDataAsync();
        Activate();
        Focus();
    }

    private async Task ReloadDataAsync()
    {
        ApplyFilterButton.IsEnabled = false;
        try
        {
            _model = await PeriodicReviewDataAggregator.FetchAndAggregateAsync(
                _startUtc,
                _endUtc,
                _settings,
                _history,
                _runningSessions,
                _currentFilterSeconds);

            RenderActivities();
            RenderStopwatchSlots();
        }
        finally
        {
            ApplyFilterButton.IsEnabled = true;
        }
    }

    private void RenderActivities()
    {
        ActivitiesListContainer.Children.Clear();

        if (_model == null || _model.Activities.Count == 0)
        {
            ActivityWatchStatusCard.Visibility = Visibility.Visible;
            if (_model != null && !_model.ActivityWatchAvailable)
            {
                ActivityWatchStatusHeading.Text = "⚠️ ActivityWatch is Offline";
                ActivityWatchStatusMessage.Text = !string.IsNullOrWhiteSpace(_model.ActivityWatchMessage)
                    ? _model.ActivityWatchMessage
                    : "Cannot connect to ActivityWatch at localhost:5600. Is ActivityWatch running?";
                ActivityWatchActionButtons.Visibility = Visibility.Visible;
                StartActivityWatchButton.Visibility = ActivityWatchLauncher.TryFindExecutable(out _) ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                ActivityWatchStatusHeading.Text = "No Activities Recorded";
                ActivityWatchStatusMessage.Text = "No foreground window activities were recorded in this time range.";
                ActivityWatchActionButtons.Visibility = Visibility.Collapsed;
            }
            return;
        }

        ActivityWatchStatusCard.Visibility = Visibility.Collapsed;

        foreach (var item in _model.Activities)
        {
            var card = new Border
            {
                Background = (Brush)FindResource("SurfaceRaisedBrush"),
                BorderBrush = (Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var stack = new StackPanel();

            // Header row: Time & duration
            var headerRow = new Grid();
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var timeText = new TextBlock
            {
                Text = item.TimeDisplay,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Foreground = (Brush)FindResource("AccentBrush")
            };
            Grid.SetColumn(timeText, 0);

            var durBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 56, 189, 248)),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 1, 6, 1),
                Child = new TextBlock
                {
                    Text = item.DurationDisplay,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("AccentBrush")
                }
            };
            Grid.SetColumn(durBadge, 1);

            headerRow.Children.Add(timeText);
            headerRow.Children.Add(durBadge);
            stack.Children.Add(headerRow);

            // App Name
            string appIcon = item.Type == "Web" ? "🌐" : "💻";
            var appText = new TextBlock
            {
                Text = $"{appIcon}  {item.App}",
                FontWeight = FontWeights.SemiBold,
                FontSize = 12.5,
                Foreground = (Brush)FindResource("PrimaryTextBrush"),
                Margin = new Thickness(0, 3, 0, 2)
            };
            stack.Children.Add(appText);

            // Details / Window Title
            if (!string.IsNullOrWhiteSpace(item.Details))
            {
                var detailsText = new TextBlock
                {
                    Text = item.Details,
                    FontSize = 11,
                    Foreground = (Brush)FindResource("SecondaryTextBrush"),
                    TextWrapping = TextWrapping.Wrap,
                    MaxHeight = 36,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                stack.Children.Add(detailsText);
            }

            card.Child = stack;
            ActivitiesListContainer.Children.Add(card);
        }
    }

    private void RenderStopwatchSlots()
    {
        StopwatchSlotsContainer.Children.Clear();

        if (_model == null || _model.StopwatchSlots.Count == 0)
        {
            var emptyText = new TextBlock
            {
                Text = "No time intervals found.",
                Foreground = (Brush)FindResource("SecondaryTextBrush"),
                FontSize = 12,
                Margin = new Thickness(10)
            };
            StopwatchSlotsContainer.Children.Add(emptyText);
            return;
        }

        foreach (var slot in _model.StopwatchSlots)
        {
            var card = new Border
            {
                Background = (Brush)FindResource("SurfaceRaisedBrush"),
                BorderBrush = (Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var stack = new StackPanel();

            // Row 1: Time, Duration & Status Badge
            var topGrid = new Grid();
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var timeText = new TextBlock
            {
                Text = $"{slot.TimeDisplay} ({slot.DurationDisplay})",
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Foreground = (Brush)FindResource("PrimaryTextBrush")
            };
            Grid.SetColumn(timeText, 0);

            var statusBadge = new Border
            {
                Background = slot.IsTracked
                    ? new SolidColorBrush(Color.FromArgb(40, 52, 211, 153))
                    : new SolidColorBrush(Color.FromArgb(40, 251, 146, 60)),
                BorderBrush = slot.IsTracked
                    ? new SolidColorBrush(Color.FromRgb(52, 211, 153))
                    : new SolidColorBrush(Color.FromRgb(251, 146, 60)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 1, 6, 1)
            };
            var badgeText = new TextBlock
            {
                Text = slot.StatusBadge,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = slot.IsTracked
                    ? new SolidColorBrush(Color.FromRgb(52, 211, 153))
                    : new SolidColorBrush(Color.FromRgb(251, 146, 60))
            };
            statusBadge.Child = badgeText;
            Grid.SetColumn(statusBadge, 1);

            topGrid.Children.Add(timeText);
            topGrid.Children.Add(statusBadge);
            stack.Children.Add(topGrid);

            // Row 2: Project Assignment Row
            var projRow = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            projRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            projRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            projRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var projLabel = new TextBlock
            {
                Text = "Project: ",
                FontSize = 11.5,
                FontWeight = FontWeights.Medium,
                Foreground = (Brush)FindResource("SecondaryTextBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            Grid.SetColumn(projLabel, 0);

            var combo = new ComboBox
            {
                IsEditable = false,
                Height = 32,
                MinHeight = 32,
                FontSize = 12,
                FontFamily = (FontFamily)FindResource("AppFontFamily"),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(combo, 1);

            // Populate known projects
            combo.Items.Add("(Untracked / Off)");
            foreach (var proj in _model.KnownProjects)
            {
                if (!string.IsNullOrWhiteSpace(proj) && !string.Equals(proj, "(Untracked / Off)", StringComparison.OrdinalIgnoreCase))
                {
                    combo.Items.Add(proj);
                }
            }

            // Ensure current slot's project is in the list
            if (!string.IsNullOrWhiteSpace(slot.SelectedProjectName) && !combo.Items.Contains(slot.SelectedProjectName))
            {
                combo.Items.Add(slot.SelectedProjectName);
            }

            combo.Items.Add("＋ New Project…");

            if (!string.IsNullOrWhiteSpace(slot.SelectedProjectName))
            {
                combo.SelectedItem = slot.SelectedProjectName;
            }
            else
            {
                combo.SelectedIndex = 0; // (Untracked / Off)
            }

            var newProjButton = new Button
            {
                Content = "+ New",
                Height = 32,
                Padding = new Thickness(10, 0, 10, 0),
                Margin = new Thickness(6, 0, 0, 0),
                Style = (Style)FindResource("SecondaryButton"),
                ToolTip = "Enter a new project name for this interval"
            };
            Grid.SetColumn(newProjButton, 2);

            void UpdateSlotState(string? newProject)
            {
                if (string.IsNullOrWhiteSpace(newProject) || newProject == "(Untracked / Off)")
                {
                    slot.SelectedProjectName = null;
                    slot.IsTracked = false;
                    statusBadge.Background = new SolidColorBrush(Color.FromArgb(40, 251, 146, 60));
                    statusBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(251, 146, 60));
                    badgeText.Foreground = new SolidColorBrush(Color.FromRgb(251, 146, 60));
                    badgeText.Text = "Untracked / Off";
                }
                else
                {
                    slot.SelectedProjectName = newProject.Trim();
                    slot.IsTracked = true;
                    statusBadge.Background = new SolidColorBrush(Color.FromArgb(40, 52, 211, 153));
                    statusBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(52, 211, 153));
                    badgeText.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
                    badgeText.Text = "Tracked";
                }
            }

            void HandleNewProjectPrompt()
            {
                string? created = PromptNewProjectName(slot.SelectedProjectName);
                if (!string.IsNullOrWhiteSpace(created))
                {
                    created = created.Trim();
                    if (!combo.Items.Contains(created))
                    {
                        int insertIdx = Math.Max(1, combo.Items.Count - 1);
                        combo.Items.Insert(insertIdx, created);
                    }
                    if (!_model.KnownProjects.Contains(created, StringComparer.OrdinalIgnoreCase))
                    {
                        _model.KnownProjects.Add(created);
                    }
                    combo.SelectedItem = created;
                    UpdateSlotState(created);
                }
                else
                {
                    combo.SelectedItem = slot.SelectedProjectName ?? "(Untracked / Off)";
                }
            }

            combo.SelectionChanged += (s, e) =>
            {
                if (combo.SelectedItem is string sel)
                {
                    if (sel == "＋ New Project…")
                    {
                        Dispatcher.BeginInvoke(new Action(HandleNewProjectPrompt));
                        return;
                    }

                    UpdateSlotState(sel);
                }
            };

            newProjButton.Click += (s, e) =>
            {
                HandleNewProjectPrompt();
            };

            projRow.Children.Add(projLabel);
            projRow.Children.Add(combo);
            projRow.Children.Add(newProjButton);
            stack.Children.Add(projRow);

            card.Child = stack;
            StopwatchSlotsContainer.Children.Add(card);
        }
    }

    private async void ApplyFilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(FilterDurationTextBox.Text.Trim(), out int parsed) && parsed > 0)
        {
            _currentFilterSeconds = parsed;
            await ReloadDataAsync();
        }
    }

    private async void FilterDurationTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (int.TryParse(FilterDurationTextBox.Text.Trim(), out int parsed) && parsed > 0)
            {
                _currentFilterSeconds = parsed;
                await ReloadDataAsync();
                e.Handled = true;
            }
        }
    }

    private void SaveReportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_handled) return;
        _handled = true;

        if (_model != null)
        {
            _onSave?.Invoke(_model.StopwatchSlots);
        }

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
            SkipReportButton_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && !(e.OriginalSource is TextBox))
        {
            SaveReportButton_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private string? PromptNewProjectName(string? current = null)
    {
        var inputDlg = new Window
        {
            Title = "Assign Project",
            Width = 360,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false
        };

        var border = new Border
        {
            Background = (Brush)FindResource("SurfaceRaisedBrush"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16)
        };
        border.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            BlurRadius = 18,
            Direction = 270,
            ShadowDepth = 4,
            Opacity = 0.5
        };

        var sp = new StackPanel();
        var label = new TextBlock
        {
            Text = "Enter Project Name:",
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = (Brush)FindResource("PrimaryTextBrush"),
            Margin = new Thickness(0, 0, 0, 10)
        };

        var textBox = new TextBox
        {
            Text = current ?? "",
            Height = 32,
            FontSize = 12.5,
            FontFamily = (FontFamily)FindResource("AppFontFamily"),
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = (Brush)FindResource("DialogSurfaceBrush"),
            Foreground = (Brush)FindResource("PrimaryTextBrush"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 0, 6, 0),
            Margin = new Thickness(0, 0, 0, 14)
        };

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var cancelBtn = new Button
        {
            Content = "Cancel",
            Height = 30,
            Padding = new Thickness(14, 0, 14, 0),
            Margin = new Thickness(0, 0, 8, 0),
            Style = (Style)FindResource("SecondaryButton"),
            IsCancel = true
        };

        var okBtn = new Button
        {
            Content = "OK",
            Height = 30,
            Padding = new Thickness(16, 0, 16, 0),
            Style = (Style)FindResource("ActionButton"),
            IsDefault = true
        };

        string? result = null;
        okBtn.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(textBox.Text))
            {
                result = textBox.Text.Trim();
                inputDlg.DialogResult = true;
            }
            else
            {
                inputDlg.DialogResult = false;
            }
        };

        btnPanel.Children.Add(cancelBtn);
        btnPanel.Children.Add(okBtn);
        sp.Children.Add(label);
        sp.Children.Add(textBox);
        sp.Children.Add(btnPanel);
        border.Child = sp;
        inputDlg.Content = border;

        textBox.Loaded += (_, _) =>
        {
            textBox.Focus();
            textBox.SelectAll();
        };

        bool? dlgResult = inputDlg.ShowDialog();
        return dlgResult == true ? result : null;
    }

    private async void StartActivityWatchButton_Click(object sender, RoutedEventArgs e)
    {
        StartActivityWatchButton.IsEnabled = false;
        ActivityWatchStatusMessage.Text = "Starting ActivityWatch, waiting for server...";

        bool launched = ActivityWatchLauncher.TryLaunch();
        if (!launched)
        {
            ActivityWatchStatusMessage.Text = "Could not locate ActivityWatch executable (aw-qt.exe). Please start ActivityWatch manually.";
            StartActivityWatchButton.IsEnabled = true;
            return;
        }

        // Poll up to 6 seconds for aw-server to come up
        for (int i = 0; i < 12; i++)
        {
            await Task.Delay(500);
            var client = new ActivityWatchClient(_settings.ActivityWatchServerUrl);
            var test = await client.TestConnectionAsync();
            if (test.Success)
            {
                break;
            }
        }

        await ReloadDataAsync();
        StartActivityWatchButton.IsEnabled = true;
    }

    private async void RetryActivityWatchButton_Click(object sender, RoutedEventArgs e)
    {
        RetryActivityWatchButton.IsEnabled = false;
        ActivityWatchStatusMessage.Text = "Retrying connection to ActivityWatch...";
        try
        {
            await ReloadDataAsync();
        }
        finally
        {
            RetryActivityWatchButton.IsEnabled = true;
        }
    }
}

