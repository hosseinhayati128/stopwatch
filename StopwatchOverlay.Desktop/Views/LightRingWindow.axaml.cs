using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using StopwatchOverlay.Platform;

namespace StopwatchOverlay.Desktop.Views;

public partial class LightRingWindow : Window
{
    private IntPtr _hwnd = IntPtr.Zero;
    private bool _requestedExcludeFromCapture;
    private bool? _lastCaptureAffinityAttempt;

    public LightRingWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero)
            return;

        _hwnd = handle;
        _lastCaptureAffinityAttempt = null;

        PlatformServices.WindowOverlay.SetClickThrough(_hwnd, true);
        PlatformServices.WindowOverlay.SetNoActivateToolWindow(_hwnd);
        PlatformServices.WindowOverlay.SetAlwaysOnTop(_hwnd, true);
        ApplyCaptureAffinityIfNeeded();
    }

    public void ApplySettings(double brightness, int width, bool excludeFromCapture)
    {
        // Brightness: 0.0 to 1.0, where 1.0 is pure white
        byte alpha = (byte)Math.Round(Math.Clamp(brightness, 0.1, 1.0) * 255);
        var ringBrush = new SolidColorBrush(Color.FromArgb(alpha, 255, 255, 255));
        LightRingBorder.BorderBrush = ringBrush;
        LightRingBorder.BorderThickness = new Thickness(Math.Clamp(width, 5, 100));

        _requestedExcludeFromCapture = excludeFromCapture;
        ApplyCaptureAffinityIfNeeded();
    }

    private void ApplyCaptureAffinityIfNeeded()
    {
        if (_hwnd == IntPtr.Zero || _lastCaptureAffinityAttempt == _requestedExcludeFromCapture)
            return;

        _lastCaptureAffinityAttempt = _requestedExcludeFromCapture;
        PlatformServices.WindowOverlay.SetCaptureAffinity(_hwnd, _requestedExcludeFromCapture);
    }

    protected override void OnClosed(EventArgs e)
    {
        _hwnd = IntPtr.Zero;
        _lastCaptureAffinityAttempt = null;
        base.OnClosed(e);
    }

    public void PositionOnScreen(Screen? screen)
    {
        screen ??= Screens.Primary;
        if (screen == null)
            return;

        var workArea = screen.WorkingArea;
        double scaling = screen.Scaling > 0 ? screen.Scaling : 1.0;

        Position = new PixelPoint(workArea.X, workArea.Y);
        Width = workArea.Width / scaling;
        Height = workArea.Height / scaling;
    }
}
