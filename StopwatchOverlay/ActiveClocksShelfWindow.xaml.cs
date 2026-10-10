using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace StopwatchOverlay;

public partial class ActiveClocksShelfWindow : Window
{
    public event Action<TimerSession>? SwitchToTimerRequested;
    public event Action<TimerSession>? PauseTimerRequested;
    public event Action? ShelfClosed;

    private readonly Dictionary<Guid, TextBlock> _timeBlocks = new();

    public ActiveClocksShelfWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        ShelfClosed?.Invoke();
    }

    public void UpdateClocks(IReadOnlyList<TimerSession> runningTimers, TimerSession? activeTimer)
    {
        // Filter: show running timers. If active timer is also running, show it with ACTIVE badge.
        var list = runningTimers?.Where(t => t.IsRunning).ToList() ?? new List<TimerSession>();

        // If the structure of timers changed, rebuild panel
        bool needsRebuild = list.Count != _timeBlocks.Count ||
                            list.Any(t => !_timeBlocks.ContainsKey(t.Id));

        if (needsRebuild)
        {
            ActiveClocksPanel.Children.Clear();
            _timeBlocks.Clear();

            if (list.Count == 0)
            {
                var emptyBlock = new TextBlock
                {
                    Text = "No running clocks",
                    Foreground = (Brush)FindResource("SecondaryTextBrush"),
                    FontSize = 11,
                    FontStyle = FontStyles.Italic,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 4, 0, 4)
                };
                ActiveClocksPanel.Children.Add(emptyBlock);
                return;
            }

            foreach (var timer in list)
            {
                var timerRef = timer;
                bool isActive = ReferenceEquals(timerRef, activeTimer);

                var rowBorder = new Border
                {
                    Style = (Style)FindResource("ShelfRowStyle"),
                    Tag = timerRef
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) }); // Dot
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Name
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Time
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Pause button

                // Pulsing/rec dot
                var dot = new System.Windows.Shapes.Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = (Brush)FindResource("RecBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                Grid.SetColumn(dot, 0);
                grid.Children.Add(dot);

                // Name
                var nameBlock = new TextBlock
                {
                    Text = timerRef.DisplayName,
                    Foreground = isActive ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("PrimaryTextBrush"),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(2, 0, 6, 0)
                };
                Grid.SetColumn(nameBlock, 1);
                grid.Children.Add(nameBlock);

                // Time block
                var timeBlock = new TextBlock
                {
                    Text = $"{timerRef.Elapsed:hh\\:mm\\:ss}",
                    FontFamily = (FontFamily)FindResource("ThemeTimerFontFamily"),
                    Foreground = (Brush)FindResource("AccentBrush"),
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 8, 0)
                };
                Grid.SetColumn(timeBlock, 2);
                grid.Children.Add(timeBlock);
                _timeBlocks[timerRef.Id] = timeBlock;

                // Pause button
                var pauseBtn = new Button
                {
                    Content = "❚❚",
                    ToolTip = $"Pause {timerRef.DisplayName}",
                    Style = (Style)FindResource("ShelfPauseButtonStyle"),
                    FontSize = 9,
                    VerticalAlignment = VerticalAlignment.Center
                };
                pauseBtn.Click += (s, e) =>
                {
                    PauseTimerRequested?.Invoke(timerRef);
                };
                Grid.SetColumn(pauseBtn, 3);
                grid.Children.Add(pauseBtn);

                rowBorder.Child = grid;

                // Clicking the row switches to this timer
                rowBorder.MouseLeftButtonDown += (s, e) =>
                {
                    SwitchToTimerRequested?.Invoke(timerRef);
                };

                ActiveClocksPanel.Children.Add(rowBorder);
            }
        }
        else
        {
            // Just update live times
            foreach (var timer in list)
            {
                if (_timeBlocks.TryGetValue(timer.Id, out var tb))
                {
                    tb.Text = $"{timer.Elapsed:hh\\:mm\\:ss}";
                }
            }
        }
    }

    public void RepositionUnder(Window targetWindow)
    {
        if (targetWindow == null || !targetWindow.IsVisible) return;

        double targetLeft = targetWindow.Left;
        double targetTop = targetWindow.Top;
        double targetWidth = targetWindow.ActualWidth > 0 ? targetWindow.ActualWidth : targetWindow.Width;
        double targetHeight = targetWindow.ActualHeight > 0 ? targetWindow.ActualHeight : targetWindow.Height;

        double selfWidth = ActualWidth > 0 ? ActualWidth : 260;
        double selfHeight = ActualHeight > 0 ? ActualHeight : 60;

        double newLeft = targetLeft + (targetWidth - selfWidth) / 2.0;
        double newTop = targetTop + targetHeight + 6.0;

        // Keep within virtual screen bounds
        double screenWidth = SystemParameters.VirtualScreenWidth;
        double screenHeight = SystemParameters.VirtualScreenHeight;

        if (newLeft < 0) newLeft = 0;
        if (newLeft + selfWidth > screenWidth) newLeft = screenWidth - selfWidth;
        if (newTop + selfHeight > screenHeight) newTop = targetTop - selfHeight - 6.0; // Place above if overflowing bottom

        Left = newLeft;
        Top = newTop;
    }
}
