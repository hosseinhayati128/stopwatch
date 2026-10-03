using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using StopwatchOverlay.Platform;

namespace StopwatchOverlay.Desktop.Views;

public partial class OverlayWindow : Window
{
    private readonly DispatcherTimer _hideControlsTimer;
    private IntPtr _hwnd = IntPtr.Zero;
    private bool _isClickThrough;
    private bool _isActive;
    private Color _textColor = Colors.White;
    private double _backgroundOpacity = 0.5;
    private bool _hideFromCapture;
    private Color _borderColor = Colors.Black;
    private int _fontSize = 48;
    private int _borderWidth = 2;
    private string _fontFamily = "Consolas";
    private bool _useThemeTextColor;
    private string _effectiveOverlayTheme = string.Empty;

    public string EffectiveOverlayTheme => _effectiveOverlayTheme;

    public event Action? PositionChangedByUser;
    public event Action? ActivationRequested;
    public event Action? ClockToggleRequested;
    public event Action? CloseRequested;
    public event Action? PauseResumeRequested;
    public event Action? ResetRequested;
    public event Action? EditRequested;

    public OverlayWindow()
    {
        InitializeComponent();

        _hideControlsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _hideControlsTimer.Tick += (_, _) =>
        {
            _hideControlsTimer.Stop();
            ActionPopup.IsOpen = false;
        };

        Opened += OnOpened;
        ApplyTheme(OverlayThemeCatalog.FollowApplicationTheme, AppThemeCatalog.Midnight);
        ApplySettings(Colors.White, Colors.Black, 48, 2, "Consolas", 0.5);
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero)
            return;

        _hwnd = handle;
        PlatformServices.WindowOverlay.SetNoActivateToolWindow(_hwnd);
        PlatformServices.WindowOverlay.SetAlwaysOnTop(_hwnd, true);
        PlatformServices.WindowOverlay.SetCaptureAffinity(_hwnd, _hideFromCapture);
        if (_isClickThrough)
        {
            PlatformServices.WindowOverlay.SetClickThrough(_hwnd, true);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _hideControlsTimer.Stop();
        ActionPopup.IsOpen = false;
        _hwnd = IntPtr.Zero;
        base.OnClosed(e);
    }

    public void UpdateTime(string timeText)
    {
        TimeText.Text = timeText;
        TimeTextShadow1.Text = timeText;
        TimeTextShadow2.Text = timeText;
        TimeTextShadow3.Text = timeText;
        TimeTextShadow4.Text = timeText;
    }

    public void SetTimerName(string? timerName)
    {
        string name = timerName?.Trim() ?? string.Empty;
        TimerNameText.Text = name;
        TimerNameText.IsVisible = OverlayPresentationPolicy.ShouldShowProjectName(name);
    }

    public void SetActive(bool active)
    {
        _isActive = active;
        ActiveIndicatorBorder.BorderBrush = active
            ? new SolidColorBrush(Color.FromArgb(220, 56, 189, 248))
            : Brushes.Transparent;
    }

    public void SetRunning(bool running)
    {
        PauseIcon.IsVisible = running;
        ResumeIcon.IsVisible = !running;
        ToolTip.SetTip(PauseResumeActionButton, running ? "Pause timer" : "Resume timer");
    }

    public void SetPauseResumeEnabled(bool enabled)
    {
        PauseResumeActionButton.IsEnabled = enabled;
        ToolTip.SetTip(
            PauseResumeActionButton,
            enabled
                ? (PauseIcon.IsVisible ? "Pause timer" : "Resume timer")
                : "Clock mode cannot be paused");
    }

    public void ApplySettings(
        Color textColor,
        Color borderColor,
        int fontSize,
        int borderWidth,
        string fontFamily,
        double backgroundOpacity,
        bool useThemeTextColor = false,
        NavigatorOpaqueParts opaqueParts = NavigatorOpaqueParts.Default)
    {
        _textColor = textColor;
        _borderColor = borderColor;
        _fontSize = Math.Clamp(fontSize, 16, 120);
        _borderWidth = Math.Clamp(borderWidth, 0, 20);
        _fontFamily = fontFamily;
        _useThemeTextColor = useThemeTextColor;
        _backgroundOpacity = OverlayPresentationPolicy.ClampBackgroundOpacity(backgroundOpacity);

        string resolvedFont = string.IsNullOrWhiteSpace(fontFamily) ? "Consolas, monospace" : fontFamily;
        var font = new FontFamily(resolvedFont);
        TimeText.FontFamily = font;
        TimeTextShadow1.FontFamily = font;
        TimeTextShadow2.FontFamily = font;
        TimeTextShadow3.FontFamily = font;
        TimeTextShadow4.FontFamily = font;

        Color timerColor = useThemeTextColor ? Colors.White : textColor;
        Color projectColor = useThemeTextColor ? timerColor : textColor;
        TimeText.Foreground = new SolidColorBrush(timerColor);
        TimeText.FontSize = _fontSize;
        TimerNameText.Foreground = new SolidColorBrush(projectColor);

        var outlineBrush = new SolidColorBrush(borderColor);
        TimeTextShadow1.Foreground = outlineBrush;
        TimeTextShadow2.Foreground = outlineBrush;
        TimeTextShadow3.Foreground = outlineBrush;
        TimeTextShadow4.Foreground = outlineBrush;

        TimeTextShadow1.FontSize = _fontSize;
        TimeTextShadow2.FontSize = _fontSize;
        TimeTextShadow3.FontSize = _fontSize;
        TimeTextShadow4.FontSize = _fontSize;

        UpdateShadowOffset(TimeTextShadow1, _borderWidth, _borderWidth);
        UpdateShadowOffset(TimeTextShadow2, -_borderWidth, -_borderWidth);
        UpdateShadowOffset(TimeTextShadow3, _borderWidth, -_borderWidth);
        UpdateShadowOffset(TimeTextShadow4, -_borderWidth, _borderWidth);

        byte alpha = (byte)Math.Round(_backgroundOpacity * 255);
        OverlayBackgroundSurface.Background = new SolidColorBrush(Color.FromArgb(alpha, 20, 24, 28));
        OverlayBorder.BorderBrush = new SolidColorBrush(borderColor);
        OverlayBorder.BorderThickness = new Thickness(_borderWidth);

        SetActive(_isActive);
    }

    public void ApplyTheme(string? overlayTheme, string? applicationTheme)
    {
        _effectiveOverlayTheme = OverlayThemeCatalog.Normalize(overlayTheme);
        if (_effectiveOverlayTheme == OverlayThemeCatalog.FollowApplicationTheme)
        {
            _effectiveOverlayTheme = AppThemeCatalog.Normalize(applicationTheme);
        }

        ApplySettings(
            _textColor,
            _borderColor,
            _fontSize,
            _borderWidth,
            _fontFamily,
            _backgroundOpacity,
            _useThemeTextColor);
    }

    public void SetHideFromCapture(bool hideFromCapture)
    {
        _hideFromCapture = hideFromCapture;
        if (_hwnd != IntPtr.Zero)
        {
            PlatformServices.WindowOverlay.SetCaptureAffinity(_hwnd, hideFromCapture);
        }
    }

    private static void UpdateShadowOffset(TextBlock textBlock, double x, double y)
    {
        textBlock.RenderTransform = new TranslateTransform(x, y);
    }

    public void SetRecIndicatorVisible(bool visible)
    {
        RecIndicator.IsVisible = visible;
    }

    public void SetClickThrough(bool clickThrough)
    {
        _isClickThrough = clickThrough;
        if (clickThrough)
        {
            ActionPopup.IsOpen = false;
        }

        if (_hwnd != IntPtr.Zero)
        {
            PlatformServices.WindowOverlay.SetClickThrough(_hwnd, clickThrough);
        }
    }

    private void OnTimerSurfacePointerEntered(object? sender, PointerEventArgs e)
    {
        if (_isClickThrough)
            return;

        _hideControlsTimer.Stop();
        ActionPopup.IsOpen = true;
    }

    private void OnTimerSurfacePointerExited(object? sender, PointerEventArgs e)
    {
        _hideControlsTimer.Stop();
        _hideControlsTimer.Start();
    }

    private void ActionSurface_PointerEntered(object? sender, PointerEventArgs e)
    {
        _hideControlsTimer.Stop();
    }

    private void ActionSurface_PointerExited(object? sender, PointerEventArgs e)
    {
        _hideControlsTimer.Stop();
        _hideControlsTimer.Start();
    }

    private void OnTimerSurfacePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_isClickThrough)
            return;

        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsRightButtonPressed)
        {
            e.Handled = true;
            ActivationRequested?.Invoke();
            ClockToggleRequested?.Invoke();
            return;
        }

        if (point.Properties.IsLeftButtonPressed)
        {
            ActivationRequested?.Invoke();
            var originalPos = Position;
            ActionPopup.IsOpen = false;
            BeginMoveDrag(e);
            if (Math.Abs(Position.X - originalPos.X) > 1 || Math.Abs(Position.Y - originalPos.Y) > 1)
            {
                PositionChangedByUser?.Invoke();
            }
        }
    }

    private void CloseActionButton_Click(object? sender, RoutedEventArgs e)
    {
        ActionPopup.IsOpen = false;
        ActivationRequested?.Invoke();
        CloseRequested?.Invoke();
    }

    private void PauseResumeActionButton_Click(object? sender, RoutedEventArgs e)
    {
        ActivationRequested?.Invoke();
        PauseResumeRequested?.Invoke();
    }

    private void ResetActionButton_Click(object? sender, RoutedEventArgs e)
    {
        ActivationRequested?.Invoke();
        ResetRequested?.Invoke();
    }

    private void EditActionButton_Click(object? sender, RoutedEventArgs e)
    {
        ActionPopup.IsOpen = false;
        ActivationRequested?.Invoke();
        EditRequested?.Invoke();
    }
}
