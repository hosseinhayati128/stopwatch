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
    private NavigatorOpaqueParts _opaqueParts = NavigatorOpaqueParts.Default;
    private string _effectiveOverlayTheme = string.Empty;

    public string EffectiveOverlayTheme => _effectiveOverlayTheme;

    public event Action? PositionChangedByUser;
    public event Action? ActivationRequested;
    public event Action? ClockToggleRequested;
    public event Action? CloseRequested;
    public event Action? PauseResumeRequested;
    public event Action? ResetRequested;
    public event Action? EditRequested;
    public event Action? SeparateMergeRequested;

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
        ClockFace.Text = timeText;
    }

    public void SetTimerName(string? timerName)
    {
        string name = timerName?.Trim() ?? string.Empty;
        ClockFace.ProjectName = name;
    }

    public void SetActive(bool active)
    {
        _isActive = active;
        ActiveIndicatorBorder.IsVisible = !ClockFace.IsNavigator;
        ActiveIndicatorBorder.BorderBrush = active
            ? ClockFace.BrushToken("ActiveItemBorderBrush", new SolidColorBrush(Color.FromArgb(220, 56, 189, 248)))
            : Brushes.Transparent;
    }

    public void SetInactiveSeparated(bool isInactiveSeparated, double inactiveOpacity)
    {
        Opacity = isInactiveSeparated ? Math.Clamp(inactiveOpacity, 0.05, 1.0) : 1.0;
        if (isInactiveSeparated)
        {
            SetActive(false);
        }
    }

    public void SetSeparateMergeState(bool isSeparated)
    {
        SeparateIcon.IsVisible = !isSeparated;
        MergeIcon.IsVisible = isSeparated;
        ToolTip.SetTip(
            SeparateMergeActionButton,
            isSeparated
                ? "Merge with herd overlay (Win+F2 → M)"
                : "Separate clock overlay (Win+F2 → B)");
    }

    public void SetRunning(bool running)
    {
        ClockFace.IsRunning = running;
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

        _opaqueParts = opaqueParts;
        ClockFace.ApplyAppearance(_textColor, _borderColor, _fontSize, _borderWidth, _fontFamily,
            _backgroundOpacity, _useThemeTextColor, _opaqueParts);
        OverlayBorder.Padding = ClockFace.ClockPadding;
        OverlayBorder.CornerRadius = ClockFace.ClockCornerRadius;
        ActiveIndicatorBorder.CornerRadius = ClockFace.ClockCornerRadius;
        ClockFace.ApplyToolbar(ActionSurface, new[] { CloseActionButton, PauseResumeActionButton, ResetActionButton, EditActionButton, SeparateMergeActionButton });
        ActionPopup.VerticalOffset = ClockFace.Token("OverlayToolbarGap", 6d);

        SetActive(_isActive);
    }

    public void ApplyTheme(string? overlayTheme, string? applicationTheme)
    {
        _effectiveOverlayTheme = OverlayThemeCatalog.Resolve(overlayTheme, applicationTheme);
        ClockFace.ApplyTheme(overlayTheme, applicationTheme);
        Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(ThemeResources.Load("Overlay/" + ThemeResources.OverlayFile(_effectiveOverlayTheme)));

        ApplySettings(
            _textColor,
            _borderColor,
            _fontSize,
            _borderWidth,
            _fontFamily,
            _backgroundOpacity,
            _useThemeTextColor,
            _opaqueParts);
    }

    public void SetHideFromCapture(bool hideFromCapture)
    {
        _hideFromCapture = hideFromCapture;
        if (_hwnd != IntPtr.Zero)
        {
            PlatformServices.WindowOverlay.SetCaptureAffinity(_hwnd, hideFromCapture);
        }
    }

    public void SetRecIndicatorVisible(bool visible)
    {
        ClockFace.ShowRec = visible;
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

    private void SeparateMergeActionButton_Click(object? sender, RoutedEventArgs e)
    {
        ActionPopup.IsOpen = false;
        ActivationRequested?.Invoke();
        SeparateMergeRequested?.Invoke();
    }
}
