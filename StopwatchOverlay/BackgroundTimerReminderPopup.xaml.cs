using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace StopwatchOverlay;

public partial class BackgroundTimerReminderPopup : Window
{
    public event Action<Guid>? KeepRunningRequested;
    public event Action<Guid>? SwitchRequested;
    public event Action<Guid>? StopRequested;
    public event Action<Guid>? TimedOut;

    private readonly DispatcherTimer _countdownTimer;
    private int _remainingSeconds;
    private Guid _timerSessionId;

    public Guid TimerSessionId => _timerSessionId;

    public BackgroundTimerReminderPopup()
    {
        InitializeComponent();

        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _countdownTimer.Tick += CountdownTimer_Tick;
    }

    public void ApplyTheme(string? overlayTheme, string? applicationTheme)
    {
        string effective = OverlayThemeManager.Apply(this, overlayTheme, applicationTheme);
        Themes.NavigatorVisual.SetEnabled(this, effective == OverlayThemeCatalog.Pirate);
        Themes.PirateVisual.SetEnabled(this, effective == OverlayThemeCatalog.Pirate);
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

        // Resolve timer font
        var timerFont = OverlayThemeManager.ResolveTimerFont(this, overlayTheme, fontFamily);
        TimerElapsedText.FontFamily = timerFont;

        // Resolve colors
        Color themeTimerColor = GetThemeColor("OverlayTimerForegroundBrush", textColor);
        Color timerColor = useThemeTextColor ? themeTimerColor : textColor;
        Color themeProjectColor = GetThemeColor("OverlayProjectForegroundBrush", timerColor);
        Color projectColor = useThemeTextColor ? themeProjectColor : textColor;

        TimerElapsedText.Foreground = new SolidColorBrush(timerColor);
        TimerNameText.Foreground = new SolidColorBrush(projectColor);

        // Background surface with user's opacity and pattern
        Color chrome = GetThemeColor("OverlayChromeBrush", Colors.Black);
        Brush surface = AppBackgroundManager.CreateOverlaySurfaceBrush(chrome, bgOpacity);
        ReminderBackgroundSurface.Background = surface;

        // Border matching clock overlay
        Color chromeBorder = OverlayThemeCatalog.Resolve(overlayTheme, applicationTheme) != OverlayThemeCatalog.Midnight
            ? GetThemeColor("OverlayChromeBorderBrush", textColor)
            : Color.FromArgb(255, textColor.R, textColor.G, textColor.B);
        ReminderBorder.BorderBrush = new SolidColorBrush(chromeBorder);

        if (TryFindResource("ThemeOverlayCornerRadius") is CornerRadius cr)
        {
            ReminderBorder.CornerRadius = cr;
            ReminderBackgroundSurface.CornerRadius = cr;
            ReminderInnerRule.CornerRadius = cr;
        }

        if (TryFindResource("OverlayShadowEffect") is System.Windows.Media.Effects.Effect shadow)
        {
            ReminderBackgroundSurface.Effect = shadow;
        }
        bool pirate = Themes.PirateVisual.GetEnabled(this);
        PirateFrame.Visibility = PiratePaper.Visibility = pirate ? Visibility.Visible : Visibility.Collapsed;
        PirateFrame.Opacity = PiratePaper.Opacity = Math.Clamp(bgOpacity, 0, 1);
        ReminderBackgroundSurface.Visibility = pirate ? Visibility.Collapsed : Visibility.Visible;
        ReminderBorder.BorderThickness = new Thickness(pirate ? 0 : 1);
        ReminderBorder.Padding = pirate ? new Thickness(22) : new Thickness(14, 10, 14, 10);
        ReminderBorder.MinWidth = pirate ? 380 : 310;
        ReminderBorder.MaxWidth = pirate ? 460 : 400;
        if (pirate) ReminderInnerRule.Visibility = Visibility.Collapsed;
        else ReminderInnerRule.SetResourceReference(VisibilityProperty, "OverlayInnerRuleVisibility");
        ReminderHeading.FontSize = pirate ? 22 : 11;
        ReminderHeading.Foreground = pirate ? new SolidColorBrush(Color.FromRgb(242, 213, 156)) : (Brush)FindResource("OverlayProjectForegroundBrush");
        TimerNameText.FontSize = pirate ? 21 : (double)FindResource("OverlayProjectFontSize");
        TimerElapsedText.FontSize = pirate ? 38 : 18;
        TimerDetails.Margin = pirate ? new Thickness(18, 14, 18, 0) : new Thickness(0);
        TimerElapsedText.Margin = pirate ? new Thickness(18, 6, 18, 14) : new Thickness(0);
        TimerElapsedText.HorizontalAlignment = pirate ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        Grid.SetColumn(TimerElapsedText, pirate ? 0 : 1);
        Grid.SetColumnSpan(TimerElapsedText, pirate ? 2 : 1);
        Grid.SetRow(TimerElapsedText, pirate ? 1 : 0);
        Grid.SetColumnSpan(TimerDetails, pirate ? 2 : 1);
        TypographyManager.ApplyWindow(this);
    }

    private Color GetThemeColor(string key, Color fallback)
        => TryFindResource(key) is SolidColorBrush brush
            ? brush.Color
            : fallback;

    public void ShowAlert(BackgroundTimerAlert alert, Window? targetWindow)
    {
        PrepareAlert(alert);
        Reposition(targetWindow);
        _countdownTimer.Stop();
        _countdownTimer.Start();
        Show();
    }

    internal void PrepareAlert(BackgroundTimerAlert alert)
    {
        _timerSessionId = alert.TimerSessionId;
        _remainingSeconds = (int)Math.Max(5, alert.Duration.TotalSeconds);

        TimerNameText.Text = string.IsNullOrWhiteSpace(alert.TimerName)
            ? $"Timer {alert.TimerNumber}"
            : alert.TimerName;

        TimerElapsedText.Text = $"{alert.Elapsed:hh\\:mm\\:ss}";
        TimeoutCountdownText.Text = $"Closes in {_remainingSeconds}s";

        bool isFinal = alert.ReminderIndex >= alert.MaxRemindersBeforeStop;
        if (isFinal)
        {
            ReminderBadgeBorder.BorderBrush = (Brush)FindResource("RecBrush");
            ReminderBadgeBorder.Background = new SolidColorBrush(Color.FromArgb(40, 229, 57, 53));
            ReminderBadgeText.Foreground = (Brush)FindResource("RecBrush");
            ReminderBadgeText.Text = $"⚠️ Final reminder ({alert.ReminderIndex} of {alert.MaxRemindersBeforeStop})";
            if (Themes.PirateVisual.GetEnabled(this))
            {
                ReminderBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(120, 48, 36));
                ReminderBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(255, 232, 189));
            }
        }
        else
        {
            ReminderBadgeBorder.SetResourceReference(Border.BorderBrushProperty, "OverlayToolbarBorderBrush");
            ReminderBadgeBorder.SetResourceReference(Border.BackgroundProperty, "OverlayToolbarSurfaceBrush");
            ReminderBadgeText.SetResourceReference(TextBlock.ForegroundProperty, "OverlayActionForegroundBrush");
            ReminderBadgeText.Text = $"Reminder {alert.ReminderIndex} of {alert.MaxRemindersBeforeStop}";
        }

    }

    public void UpdateElapsed(TimeSpan elapsed)
    {
        TimerElapsedText.Text = $"{elapsed:hh\\:mm\\:ss}";
    }

    private void CountdownTimer_Tick(object? sender, EventArgs e)
    {
        _remainingSeconds--;
        if (_remainingSeconds <= 0)
        {
            _countdownTimer.Stop();
            Hide();
            TimedOut?.Invoke(_timerSessionId);
        }
        else
        {
            TimeoutCountdownText.Text = $"Closes in {_remainingSeconds}s";
        }
    }

    private void KeepRunningButton_Click(object sender, RoutedEventArgs e)
    {
        _countdownTimer.Stop();
        Hide();
        KeepRunningRequested?.Invoke(_timerSessionId);
    }

    private void SwitchButton_Click(object sender, RoutedEventArgs e)
    {
        _countdownTimer.Stop();
        Hide();
        SwitchRequested?.Invoke(_timerSessionId);
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _countdownTimer.Stop();
        Hide();
        StopRequested?.Invoke(_timerSessionId);
    }

    public void Reposition(Window? targetWindow)
    {
        double selfWidth = ActualWidth > 0 ? ActualWidth : 340;
        double selfHeight = ActualHeight > 0 ? ActualHeight : 110;

        if (targetWindow != null && targetWindow.IsVisible)
        {
            double targetLeft = targetWindow.Left;
            double targetTop = targetWindow.Top;
            double targetWidth = targetWindow.ActualWidth > 0 ? targetWindow.ActualWidth : targetWindow.Width;
            double targetHeight = targetWindow.ActualHeight > 0 ? targetWindow.ActualHeight : targetWindow.Height;

            double newLeft = targetLeft + (targetWidth - selfWidth) / 2.0;
            double newTop = targetTop + targetHeight + 6.0;

            double vLeft = SystemParameters.VirtualScreenLeft;
            double vTop = SystemParameters.VirtualScreenTop;
            double screenWidth = SystemParameters.VirtualScreenWidth;
            double screenHeight = SystemParameters.VirtualScreenHeight;

            if (newLeft < vLeft) newLeft = vLeft;
            if (newLeft + selfWidth > vLeft + screenWidth) newLeft = vLeft + screenWidth - selfWidth;
            if (newTop + selfHeight > vTop + screenHeight) newTop = targetTop - selfHeight - 6.0;
            if (newTop < vTop) newTop = vTop;

            Left = newLeft;
            Top = newTop;
        }
        else
        {
            // Center-bottom of primary work area
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - selfWidth - 20;
            Top = workArea.Bottom - selfHeight - 20;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _countdownTimer.Stop();
        base.OnClosed(e);
    }
}
