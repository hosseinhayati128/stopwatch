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

        ShelfHeaderTitle.Foreground = _projectForeground;
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
                    Tag = timerRef,
                    ToolTip = isActive ? "Current active clock" : $"Click to switch to {timerRef.DisplayName}"
                };

                var grid = new Grid { Margin = new Thickness(0) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Name + dot (flexible)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Time
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Pause button

                // Left side: dot + name + active badge
                var nameDock = new DockPanel { LastChildFill = true, VerticalAlignment = VerticalAlignment.Center };

                var dot = new System.Windows.Shapes.Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = (Brush)FindResource("RecBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2, 0, 6, 0)
                };
                DockPanel.SetDock(dot, Dock.Left);
                nameDock.Children.Add(dot);

                if (isActive)
                {
                    var activeBadge = new Border
                    {
                        Background = (Brush)FindResource("AccentBrush"),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(4, 1, 4, 1),
                        Margin = new Thickness(4, 0, 4, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    DockPanel.SetDock(activeBadge, Dock.Right);
                    var activeText = new TextBlock
                    {
                        Text = "ACTIVE",
                        FontSize = 7.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White
                    };
                    activeBadge.Child = activeText;
                    nameDock.Children.Add(activeBadge);
                }

                var nameBlock = new TextBlock
                {
                    Text = timerRef.DisplayName,
                    FontFamily = (FontFamily)FindResource("AppFontFamily"),
                    Foreground = isActive
                        ? (Brush)FindResource("AccentBrush")
                        : (_projectForeground ?? (Brush)FindResource("OverlayProjectForegroundBrush")),
                    FontSize = 11,
                    FontWeight = isActive ? FontWeights.Bold : FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                nameDock.Children.Add(nameBlock);

                Grid.SetColumn(nameDock, 0);
                grid.Children.Add(nameDock);

                // Time block
                var timeBlock = new TextBlock
                {
                    Text = $"{timerRef.Elapsed:hh\\:mm\\:ss}",
                    FontFamily = _timerFont ?? (FontFamily)FindResource("ThemeTimerFontFamily"),
                    Foreground = _timerForeground ?? (Brush)FindResource("OverlayTimerForegroundBrush"),
                    FontSize = 12.5,
                    FontWeight = _timerFontWeight,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 6, 0)
                };
                Grid.SetColumn(timeBlock, 1);
                grid.Children.Add(timeBlock);
                _timeBlocks[timerRef.Id] = timeBlock;

                // Pause button
                var pauseBtn = new Button
                {
                    Content = "❚❚",
                    ToolTip = $"Pause {timerRef.DisplayName}",
                    Style = (Style)FindResource("ShelfPauseButtonStyle"),
                    FontSize = 8.5,
                    Width = 20,
                    Height = 20,
                    VerticalAlignment = VerticalAlignment.Center
                };
                pauseBtn.Click += (s, e) =>
                {
                    e.Handled = true;
                    PauseTimerRequested?.Invoke(timerRef);
                };
                Grid.SetColumn(pauseBtn, 2);
                grid.Children.Add(pauseBtn);

                rowBorder.Child = grid;

                // Clicking row switches to timer
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
