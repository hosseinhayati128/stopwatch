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

    private FontFamily? _timerFont;
    private Brush? _timerForeground;
    private Brush? _projectForeground;
    private FontWeight _timerFontWeight = FontWeights.Bold;
    private string _effectiveOverlayTheme = OverlayThemeCatalog.Midnight;
    private double _backgroundOpacity = 0.5;
    private double _targetWidth = 260;

    public ActiveClocksShelfWindow()
    {
        InitializeComponent();
    }

    public void ApplyTheme(string? overlayTheme, string? applicationTheme)
    {
        string effectiveTheme = OverlayThemeManager.Apply(this, overlayTheme, applicationTheme);
        Themes.NavigatorVisual.SetEnabled(this, effectiveTheme == OverlayThemeCatalog.Pirate);
        _effectiveOverlayTheme = effectiveTheme;

        Visibility lightScope = effectiveTheme == OverlayThemeCatalog.AcanthusLight
            ? Visibility.Visible : Visibility.Collapsed;
        Themes.AcanthusVisual.SetScope(this, lightScope);
    }

    public void ApplyOverlaySettings(
        string? overlayTheme,
        string? applicationTheme,
        Color textColor,
        Color borderColor,
        string fontFamily,
        double bgOpacity,
        bool useThemeTextColor)
    {
        ApplyTheme(overlayTheme, applicationTheme);
        _backgroundOpacity = bgOpacity;

        // Resolve timer font matching overlay
        _timerFont = OverlayThemeManager.ResolveTimerFont(this, _effectiveOverlayTheme, fontFamily);

        // Resolve colors matching overlay
        Color themeTimerColor = GetThemeColor("OverlayTimerForegroundBrush", textColor);
        Color timerColor = useThemeTextColor ? themeTimerColor : textColor;
        Color themeProjectColor = GetThemeColor("OverlayProjectForegroundBrush", timerColor);
        Color projectColor = useThemeTextColor ? themeProjectColor : textColor;

        _timerForeground = new SolidColorBrush(timerColor);
        _projectForeground = new SolidColorBrush(projectColor);
        _timerFontWeight = TryFindResource("OverlayTimerFontWeight") is FontWeight fw ? fw : FontWeights.Bold;

        // Background surface matching clock overlay with user's background opacity & pattern
        Color chrome = GetThemeColor("OverlayChromeBrush", Colors.Black);
        Brush surface = AppBackgroundManager.CreateOverlaySurfaceBrush(chrome, bgOpacity);
        ShelfBackgroundSurface.Background = surface;

        // Border matching clock overlay
        Color chromeBorder = _effectiveOverlayTheme != OverlayThemeCatalog.Midnight
            ? GetThemeColor("OverlayChromeBorderBrush", textColor)
            : Color.FromArgb(255, textColor.R, textColor.G, textColor.B);
        var chromeBorderBrush = new SolidColorBrush(chromeBorder);
        ShelfBorder.BorderBrush = chromeBorderBrush;

        if (TryFindResource("ThemeOverlayFrameBorderThickness") is Thickness bt && bt.Left > 0)
        {
            ShelfBorder.BorderThickness = bt;
        }
        else
        {
            ShelfBorder.BorderThickness = _effectiveOverlayTheme == OverlayThemeCatalog.Midnight
                ? new Thickness(0)
                : new Thickness(1);
        }

        if (TryFindResource("ThemeOverlayCornerRadius") is CornerRadius cr)
        {
            ShelfBorder.CornerRadius = cr;
            ShelfBackgroundSurface.CornerRadius = cr;
            ShelfInnerRule.CornerRadius = cr;
        }

        if (TryFindResource("OverlayShadowEffect") is System.Windows.Media.Effects.Effect shadow)
        {
            ShelfBackgroundSurface.Effect = shadow;
        }
    }

    private Color GetThemeColor(string key, Color fallback)
        => TryFindResource(key) is SolidColorBrush brush
            ? brush.Color
            : fallback;

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        ShelfClosed?.Invoke();
    }

    private void Window_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        Hide();
        ShelfClosed?.Invoke();
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            Hide();
            ShelfClosed?.Invoke();
        }
    }

    public void UpdateClocks(IReadOnlyList<TimerSession> runningTimers, TimerSession? activeTimer)
    {
        var list = runningTimers?.Where(t => t.IsRunning).ToList() ?? new List<TimerSession>();

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
                    Foreground = _projectForeground ?? (Brush)FindResource("OverlayProjectForegroundBrush"),
                    Opacity = 0.6,
                    FontSize = 11.5,
                    FontStyle = FontStyles.Italic,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 6, 0, 6)
                };
                ActiveClocksPanel.Children.Add(emptyBlock);
                return;
            }

            foreach (var timer in list)
            {
                var timerRef = timer;
                bool isActive = ReferenceEquals(timerRef, activeTimer);

                // Add subtle separator between multiple clocks
                if (ActiveClocksPanel.Children.Count > 0)
                {
                    var separator = new Border
                    {
                        Height = 0.5,
                        Background = (Brush)FindResource("OverlayChromeBorderBrush"),
                        Opacity = 0.2,
                        Margin = new Thickness(8, 2, 8, 2)
                    };
                    ActiveClocksPanel.Children.Add(separator);
                }

                // Card container
                var cardBorder = new Border
                {
                    Style = (Style)FindResource("ShelfRowStyle"),
                    Tag = timerRef,
                    ToolTip = isActive ? $"{timerRef.DisplayName} (Active clock)" : $"Click to switch to {timerRef.DisplayName}"
                };

                if (isActive)
                {
                    // Subtle accent border for active clock - no text badge or red dot
                    cardBorder.BorderBrush = (Brush)FindResource("AccentBrush");
                    cardBorder.BorderThickness = new Thickness(1);
                    cardBorder.Opacity = 1.0;
                }
                else
                {
                    cardBorder.BorderBrush = Brushes.Transparent;
                    cardBorder.BorderThickness = new Thickness(1);
                    cardBorder.Opacity = 0.88;
                }

                // Two-line layout: Line 1 = clock + pause button, Line 2 = project name
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Line 1: Clock
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Line 2: Project name

                // Line 1: Clock row with centered time and right-aligned pause button
                var clockRow = new Grid { Margin = new Thickness(0, 1, 0, 0) };
                clockRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) }); // Left spacer to balance pause button
                clockRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Centered time
                clockRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) }); // Right pause button

                // Time block (centered, prominent digits matching clock overlay)
                var timeBlock = new TextBlock
                {
                    Text = FormatTimerDisplay(timerRef),
                    FontFamily = _timerFont ?? (FontFamily)FindResource("ThemeTimerFontFamily"),
                    Foreground = _timerForeground ?? (Brush)FindResource("OverlayTimerForegroundBrush"),
                    FontSize = 21,
                    FontWeight = _timerFontWeight,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                };
                Grid.SetColumn(timeBlock, 1);
                clockRow.Children.Add(timeBlock);
                _timeBlocks[timerRef.Id] = timeBlock;

                // Pause button (right side of line 1)
                var pauseBtn = new Button
                {
                    Content = "❚❚",
                    ToolTip = $"Pause {timerRef.DisplayName}",
                    Style = (Style)FindResource("ShelfPauseButtonStyle"),
                    FontSize = 8.5,
                    Width = 20,
                    Height = 20,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                pauseBtn.Click += (s, e) =>
                {
                    e.Handled = true;
                    PauseTimerRequested?.Invoke(timerRef);
                };
                Grid.SetColumn(pauseBtn, 2);
                clockRow.Children.Add(pauseBtn);

                Grid.SetRow(clockRow, 0);
                grid.Children.Add(clockRow);

                // Line 2: Project name (spans full width across card, centered, full space)
                string displayName = timerRef.DisplayName;

                var nameBlock = new TextBlock
                {
                    Text = displayName,
                    FontFamily = (FontFamily)FindResource("AppFontFamily"),
                    Foreground = isActive
                        ? (Brush)FindResource("AccentBrush")
                        : (_projectForeground ?? (Brush)FindResource("OverlayProjectForegroundBrush")),
                    FontSize = 12,
                    FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(4, 1, 4, 3),
                    Opacity = isActive ? 1.0 : 0.82
                };
                Grid.SetRow(nameBlock, 1);
                grid.Children.Add(nameBlock);

                cardBorder.Child = grid;

                // Clicking row switches to timer
                cardBorder.MouseLeftButtonDown += (s, e) =>
                {
                    SwitchToTimerRequested?.Invoke(timerRef);
                };

                ActiveClocksPanel.Children.Add(cardBorder);
            }
        }
        else
        {
            // Just update live times
            foreach (var timer in list)
            {
                if (_timeBlocks.TryGetValue(timer.Id, out var tb))
                {
                    tb.Text = FormatTimerDisplay(timer);
                }
            }
        }
    }

    private static string FormatTimerDisplay(TimerSession timer)
    {
        if (timer.Mode == 2)
        {
            var remaining = timer.CountdownRemaining;
            bool isNegative = remaining < TimeSpan.Zero;
            var abs = isNegative ? remaining.Negate() : remaining;
            string sign = isNegative ? "-" : "";
            return $"{sign}{(long)abs.TotalHours:D2}:{abs.Minutes:D2}:{abs.Seconds:D2}";
        }
        return $"{timer.Elapsed:hh\\:mm\\:ss}";
    }

    public void RepositionUnder(Window targetWindow)
    {
        if (targetWindow == null || !targetWindow.IsVisible) return;

        double targetLeft = targetWindow.Left;
        double targetTop = targetWindow.Top;
        double targetWidth = targetWindow.ActualWidth > 0 ? targetWindow.ActualWidth : (targetWindow.Width > 0 ? targetWindow.Width : 260);
        double targetHeight = targetWindow.ActualHeight > 0 ? targetWindow.ActualHeight : (targetWindow.Height > 0 ? targetWindow.Height : 50);

        if (targetWidth <= 0) targetWidth = 260;

        _targetWidth = targetWidth;

        // Match the exact rendered width of the clock overlay
        if (Math.Abs(this.Width - targetWidth) > 0.5)
        {
            this.Width = targetWidth;
            ShelfSurface.Width = targetWidth;
            ShelfBackgroundSurface.Width = targetWidth;
            ShelfBorder.Width = targetWidth;
        }

        double newLeft = targetLeft;
        double newTop = targetTop + targetHeight + 3.0; // Docked 3px directly below overlay

        // Keep within virtual screen bounds
        double vLeft = SystemParameters.VirtualScreenLeft;
        double vTop = SystemParameters.VirtualScreenTop;
        double screenWidth = SystemParameters.VirtualScreenWidth;
        double screenHeight = SystemParameters.VirtualScreenHeight;

        if (newLeft < vLeft) newLeft = vLeft;
        if (newLeft + targetWidth > vLeft + screenWidth) newLeft = vLeft + screenWidth - targetWidth;

        double currentHeight = ActualHeight > 0 ? ActualHeight : 50;
        if (newTop + currentHeight > vTop + screenHeight)
        {
            newTop = targetTop - currentHeight - 3.0; // Place above if overflowing bottom
        }
        if (newTop < vTop) newTop = vTop;

        if (Math.Abs(this.Left - newLeft) > 0.5) this.Left = newLeft;
        if (Math.Abs(this.Top - newTop) > 0.5) this.Top = newTop;
    }
}
