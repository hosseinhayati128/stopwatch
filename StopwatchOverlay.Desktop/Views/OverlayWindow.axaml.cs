using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using StopwatchOverlay.Platform;
using StopwatchOverlay.Desktop.Platform;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class OverlayWindow : Window
{
    private readonly DispatcherTimer _hideControlsTimer;
    private bool _closed;
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
    private StopwatchAppearanceSettings? _stopwatchAppearance;
    private bool _isRunning, _isClockMode;
    private bool _isInactiveSeparated;
    private double _selectionOpacity=1;
    private StopwatchPresentationState? _appliedStopwatchState;
    private StopwatchAppearanceSettings? _appliedStopwatchAppearance;
    private double _appliedSelectionOpacity=double.NaN;

    public void ApplyAppearance(AppSettings settings)
    {
        _appliedStopwatchState=null;
        _stopwatchAppearance=null;
        ClockFace.ApplyStopwatchAppearance(null);
        StopwatchToolbarStyler.Apply(ActionSurface,ActionButtons,null,new(_isActive,_isRunning,_isClockMode),1);
        ApplyTheme(settings.OverlayTheme,settings.ApplicationTheme);
        ClockFace.ApplySettings(settings);
        ApplySettings(ClockFace.TimerBrush is SolidColorBrush text?text.Color:Colors.White,
            ClockFace.OutlineBrush is SolidColorBrush border?border.Color:Colors.Black,
            (int)settings.TextSize,(int)settings.BorderWidth,settings.FontFamily,settings.BackgroundOpacity/100,
            settings.TextColor=="Theme default",settings.OpaqueOverlayParts);
        if(_effectiveOverlayTheme==OverlayThemeCatalog.Nika)
        {
            _stopwatchAppearance=settings.StopwatchAppearance.Copy();_stopwatchAppearance.Normalize();
            ClockFace.ApplyStopwatchAppearance(_stopwatchAppearance);
        }
        RefreshStopwatchPresentation();
        SetActive(_isActive);
        SetInactiveSeparated(_isInactiveSeparated,StopwatchAppearancePolicy.ResolveInactiveOpacityPercent(settings)/100);
    }

    private Button[] ActionButtons => new[]{CloseActionButton,PauseResumeActionButton,ResetActionButton,EditActionButton,SeparateMergeActionButton};
    private void RefreshStopwatchPresentation()
    {
        var state=new StopwatchPresentationState(_isActive,_isRunning,_isClockMode);
        if (_appliedStopwatchState == state && ReferenceEquals(_appliedStopwatchAppearance,_stopwatchAppearance)
            && _appliedSelectionOpacity == _selectionOpacity) return;
        ClockFace.SetStopwatchPresentationState(state);
        StopwatchOpacity.Apply(ClockFace,_stopwatchAppearance!=null?_selectionOpacity:1);
        if(_stopwatchAppearance!=null)StopwatchToolbarStyler.Apply(ActionSurface,ActionButtons,_stopwatchAppearance,state,_selectionOpacity);
        _appliedStopwatchState=state;
        _appliedStopwatchAppearance=_stopwatchAppearance;
        _appliedSelectionOpacity=_selectionOpacity;
    }

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
        WindowsTopmostGuard.Attach(this);
        OverlayScreenConstraint.Attach(this);

        _hideControlsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _hideControlsTimer.Tick += (_, _) =>
        {
            _hideControlsTimer.Stop();
            ActionPopup.IsOpen = false;
        };

        PropertyChanged += (_, change) =>
        {
            if (change.Property != IsVisibleProperty) return;
            if (IsVisible) RestoreFloatingState();
        };
        Deactivated += (_, _) => RestoreFloatingState();

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
        RestoreFloatingState();
    }

    private void RestoreFloatingState()
    {
        // Reapply after Avalonia finishes showing/updating native chrome. X11
        // also drops ABOVE when a window is unmapped, as with the todo overlay.
        Dispatcher.UIThread.Post(() =>
        {
            if (_closed || !IsVisible) return;
            if (!OperatingSystem.IsWindows())
            {
                Topmost = false;
                Topmost = true;
            }
            _hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (_hwnd != IntPtr.Zero)
            {
                PlatformServices.WindowOverlay.SetNoActivateToolWindow(_hwnd);
                PlatformServices.WindowOverlay.SetAlwaysOnTop(_hwnd, true);
            }
        }, DispatcherPriority.Background);
    }

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
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
        ActiveIndicatorBorder.IsVisible = !ClockFace.IsNavigator && !ClockFace.HasStopwatchAppearance;
        ActiveIndicatorBorder.BorderBrush = active
            ? ClockFace.BrushToken("ActiveItemBorderBrush", new SolidColorBrush(Color.FromArgb(220, 56, 189, 248)))
            : Brushes.Transparent;
        RefreshStopwatchPresentation();
    }

    public void SetInactiveSeparated(bool isInactiveSeparated, double inactiveOpacity)
    {
        _isInactiveSeparated=isInactiveSeparated;
        _selectionOpacity=isInactiveSeparated?Math.Clamp(inactiveOpacity,0.05,1):1;
        Opacity=_stopwatchAppearance!=null?1:_selectionOpacity;
        if (isInactiveSeparated)
        {
            SetActive(false);
        }
        RefreshStopwatchPresentation();
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
        _isRunning=running;
        ClockFace.IsRunning = running;
        PauseResumeActionButton.SetValue(Controls.NavigatorDialArtwork.ActionProperty, running ? "Pause" : "Play");
        PauseIcon.IsVisible = running;
        ResumeIcon.IsVisible = !running;
        ToolTip.SetTip(PauseResumeActionButton, running ? "Pause timer" : "Resume timer");
        RefreshStopwatchPresentation();
    }

    public void SetPauseResumeEnabled(bool enabled)
    {
        _isClockMode=!enabled;
        PauseResumeActionButton.IsEnabled = enabled;
        ToolTip.SetTip(
            PauseResumeActionButton,
            enabled
                ? (PauseIcon.IsVisible ? "Pause timer" : "Resume timer")
                : "Clock mode cannot be paused");
        RefreshStopwatchPresentation();
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
        // Applying toolbar appearance restores its default selection opacity;
        // reapply this window's current selection after changing settings.
        _appliedStopwatchState=null;
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
        _appliedStopwatchState=null;
        _effectiveOverlayTheme = OverlayThemeCatalog.Resolve(overlayTheme, applicationTheme);
        _stopwatchAppearance=_effectiveOverlayTheme==OverlayThemeCatalog.Nika?_stopwatchAppearance??new StopwatchAppearanceSettings():null;
        ClockFace.ApplyTheme(overlayTheme, applicationTheme);
        ClockFace.ApplyStopwatchAppearance(_stopwatchAppearance);
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
        Opacity=_stopwatchAppearance!=null?1:_selectionOpacity;
        RefreshStopwatchPresentation();
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
