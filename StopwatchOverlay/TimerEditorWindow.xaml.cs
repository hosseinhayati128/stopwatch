using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace StopwatchOverlay
{
    public sealed record SegmentItem(string Label, string Duration);

    public partial class TimerEditorWindow : Window
    {
        private readonly TimerSession _timer;
        private readonly IReadOnlyList<ProjectWorkIntervalView> _intervals;
        private readonly ProjectWorkIntervalView? _latestInterval;
        private readonly DispatcherTimer _liveRefreshTimer;

        private readonly TimeSpan _originalTotalElapsed;
        private readonly DateTime? _latestStartLocal;
        private readonly DateTime? _latestEndLocal;

        private bool _isUpdatingInternally;
        private bool _discardRecords;
        private bool _isCustomProjectMode;
        private bool _isRunning;
        private TimeSpan _currentTimeSpan;

        public bool WasSaved { get; private set; }
        public bool UndoRequested { get; private set; }
        public bool DiscardRecordsRequested { get; private set; }
        public bool DeleteTimerRequested { get; private set; }
        public TimeSpan NewTimeValue { get; private set; }
        public TimeSpan Delta => NewTimeValue - _originalTotalElapsed;
        public string? NewProjectName { get; private set; }
        public bool NewIsRunning { get; private set; }

        public TimerEditorWindow(
            TimerSession timer,
            IReadOnlyList<string> projectNames,
            IReadOnlyList<ProjectWorkIntervalView> intervals,
            bool canUndo = false)
        {
            _timer = timer ?? throw new ArgumentNullException(nameof(timer));
            _intervals = intervals ?? Array.Empty<ProjectWorkIntervalView>();
            _latestInterval = _intervals.LastOrDefault();
            _isRunning = timer.IsRunning;

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
            if (projectNames != null)
            {
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
            }
            ProjectSelector.SelectedIndex = selectedIndex;

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
                SegmentsExpander.Visibility = Visibility.Collapsed;
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
            UndoPreviousEditButton.Visibility = canUndo ? Visibility.Visible : Visibility.Collapsed;

            _liveRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _liveRefreshTimer.Tick += (_, _) => UpdateLivePreview();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _liveRefreshTimer.Start();
            UpdateLivePreview();
            TimeValueBox.Focus();
            TimeValueBox.SelectAll();
        }

        protected override void OnClosed(EventArgs e)
        {
            _liveRefreshTimer.Stop();
            base.OnClosed(e);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                DialogResult = false;
                Close();
            }
            else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                e.Handled = true;
                SaveButton_Click(sender, e);
            }
            else if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                if (UndoPreviousEditButton.Visibility == Visibility.Visible)
                {
                    e.Handled = true;
                    UndoPreviousEditButton_Click(sender, e);
                }
            }
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
                PauseResumeButton.Style = (Style)FindResource("ModernButton");
            }
            else
            {
                PauseResumeButton.Style = (Style)FindResource("StartButton");
            }
        }

        private void UpdateRecordsStatus()
        {
            if (_discardRecords)
            {
                RecordsStatusText.Text = "Session records will be removed from project history upon saving.";
                RecordsStatusText.Foreground = (System.Windows.Media.Brush)FindResource("DangerTextBrush");
                DiscardRecordsButton.IsEnabled = false;
            }
            else if (_intervals.Count > 0 || _timer.HasAccumulatedTime)
            {
                RecordsStatusText.Text = $"Tracking {_intervals.Count} segment(s) in project history.";
                RecordsStatusText.Foreground = (System.Windows.Media.Brush)FindResource("SecondaryTextBrush");
                DiscardRecordsButton.IsEnabled = true;
            }
            else
            {
                RecordsStatusText.Text = "No active work interval recorded for this timer yet.";
                RecordsStatusText.Foreground = (System.Windows.Media.Brush)FindResource("SecondaryTextBrush");
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
                AdjustmentSummaryText.Foreground = (System.Windows.Media.Brush)FindResource("SecondaryTextBrush");
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
                AdjustmentSummaryText.Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush");
            }
            else
            {
                AdjustmentSummaryText.Text = $"-{FormatDuration(-delta)} will eliminate time from the latest segments backwards";
                AdjustmentSummaryText.Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush");
            }
        }

        private void TimeValueBox_TextChanged(object sender, TextChangedEventArgs e)
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
                AdjustmentSummaryText.Foreground = (System.Windows.Media.Brush)FindResource("DangerTextBrush");
                SaveButton.IsEnabled = false;
            }
        }

        private void StartTimeBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingInternally) return;

            if (TimeOnly.TryParse(StartTimeBox.Text, CultureInfo.InvariantCulture, out TimeOnly t)
                || TimeOnly.TryParse(StartTimeBox.Text, CultureInfo.CurrentCulture, out t))
            {
                DateTime today = DateTime.Today.Add(t.ToTimeSpan());

                if (_latestStartLocal.HasValue)
                {
                    DateTime baseDate = _latestStartLocal.Value.Date;
                    DateTime candidate = baseDate.Add(t.ToTimeSpan());
                    if (candidate > DateTime.Now) candidate = candidate.AddDays(-1);

                    TimeSpan diff = _latestStartLocal.Value - candidate; // Earlier start means more time
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

        private void QuickAdjust_Click(object sender, RoutedEventArgs e)
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

        private void PauseResumeButton_Click(object sender, RoutedEventArgs e)
        {
            _isRunning = !_isRunning;
            UpdateRunningButton();
        }

        private void DiscardRecordsButton_Click(object sender, RoutedEventArgs e)
        {
            _discardRecords = true;
            UpdateRecordsStatus();
        }

        private void DeleteTimerButton_Click(object sender, RoutedEventArgs e)
        {
            var confirm = new ConfirmationDialogWindow(
                "Delete Timer",
                $"Delete {_timer.DisplayName}?",
                "Are you sure you want to delete and close this timer?",
                "Delete",
                destructive: true)
            {
                Owner = this
            };

            if (confirm.ShowDialog() == true)
            {
                DeleteTimerRequested = true;
                WasSaved = true;
                DialogResult = true;
                Close();
            }
        }

        private void ToggleProjectInputButton_Click(object sender, RoutedEventArgs e)
        {
            _isCustomProjectMode = !_isCustomProjectMode;
            if (_isCustomProjectMode)
            {
                ProjectSelector.Visibility = Visibility.Collapsed;
                CustomProjectBox.Visibility = Visibility.Visible;
                ToggleProjectInputButton.Content = "List...";
                CustomProjectBox.Focus();
            }
            else
            {
                CustomProjectBox.Visibility = Visibility.Collapsed;
                ProjectSelector.Visibility = Visibility.Visible;
                ToggleProjectInputButton.Content = "New...";
                ProjectSelector.Focus();
            }
        }

        private void ProjectSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Normal selection
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void UndoPreviousEditButton_Click(object sender, RoutedEventArgs e)
        {
            UndoRequested = true;
            WasSaved = true;
            DialogResult = true;
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseDuration(TimeValueBox.Text, out TimeSpan duration))
            {
                MessageBox.Show(this, "Please enter a valid time duration or format (e.g. 01:30:00 or 15m).", "Invalid Time", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            NewTimeValue = duration;
            DiscardRecordsRequested = _discardRecords;
            NewIsRunning = _isRunning;

            if (_isCustomProjectMode)
            {
                NewProjectName = CustomProjectBox.Text.Trim();
            }
            else if (ProjectSelector.SelectedIndex > 0 && ProjectSelector.SelectedItem is string proj)
            {
                NewProjectName = proj;
            }
            else
            {
                NewProjectName = "";
            }

            WasSaved = true;
            DialogResult = true;
            Close();
        }

        public static string FormatDuration(TimeSpan time)
        {
            int totalHours = (int)time.TotalHours;
            return totalHours > 0
                ? string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:mm\\:ss}", totalHours, time)
                : string.Format(CultureInfo.InvariantCulture, "{0:mm\\:ss}", time);
        }

        public static bool TryParseDuration(string input, out TimeSpan duration)
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
}
