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
    }

    private Color GetThemeColor(string key, Color fallback)
        => TryFindResource(key) is SolidColorBrush brush
            ? brush.Color
            : fallback;

    public void ShowAlert(BackgroundTimerAlert alert, Window? targetWindow)
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
        }
        else
        {
            ReminderBadgeBorder.SetResourceReference(Border.BorderBrushProperty, "OverlayToolbarBorderBrush");
            ReminderBadgeBorder.SetResourceReference(Border.BackgroundProperty, "OverlayToolbarSurfaceBrush");
            ReminderBadgeText.SetResourceReference(TextBlock.ForegroundProperty, "OverlayActionForegroundBrush");
            ReminderBadgeText.Text = $"Reminder {alert.ReminderIndex} of {alert.MaxRemindersBeforeStop}";
        }

        Reposition(targetWindow);

        _countdownTimer.Stop();
        _countdownTimer.Start();

        Show();
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
