using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;

namespace StopwatchOverlay.Desktop.Views;

public partial class ConfirmationDialogWindow
{
    private bool _noteDeletionConfigured, _noteDeletionCompleted;
    private Control? _noteDeletionRoot;
    private double _noteDeletionWidth, _noteDeletionHeight;

    private void ConfigureNoteDeletionActions()
    {
        _noteDeletionConfigured = true;
        Closed += (_, _) => _noteDeletionCompleted = true;
        Opened += (_, _) => Dispatcher.UIThread.Post(() => { if (IsVisible) CancelButton.Focus(); });
        AddHandler(KeyDownEvent, (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Complete(false); } }, RoutingStrategies.Tunnel);
    }

    private void SizeNoteDeletion(AppSettings settings, double? height, double width = 540)
    {
        _noteDeletionRoot = (Control)Content!;
        _noteDeletionWidth = width;
        _noteDeletionHeight = height ?? double.NaN;
        SystemDecorations = SystemDecorations.None; Background = Brushes.Transparent; TransparencyLevelHint = [WindowTransparencyLevel.Transparent]; CanResize = false;
        MinWidth = MinHeight = 0; MaxHeight = double.PositiveInfinity;
        SizeToContent = height.HasValue ? SizeToContent.Manual : SizeToContent.Height;
        double scale = AppUiScale.Normalize(settings.UiScalePercent) / 100; Width = width * scale; Height = _noteDeletionHeight * scale;
        Opened += (_, _) => { if (Screens.ScreenFromWindow(this) is { } screen) FitNoteDeletionToScreen(screen); else FitNoteDeletionContent(); };
        SizeChanged += (_, _) => FitNoteDeletionContent();
    }
    internal void FitNoteDeletionToScreen(Screen screen)
    {
        MaxWidth = Math.Floor(screen.WorkingArea.Width / screen.Scaling); MaxHeight = Math.Floor(screen.WorkingArea.Height / screen.Scaling);
        Width = Math.Min(Width, MaxWidth);
        if (double.IsFinite(_noteDeletionHeight)) Height = Math.Min(Height, MaxHeight);
        var area = screen.WorkingArea;
        double width = Math.Min(area.Width, Math.Ceiling(Width * screen.Scaling));
        double height = Math.Min(area.Height, Math.Ceiling((double.IsFinite(_noteDeletionHeight) ? Height : ClientSize.Height) * screen.Scaling));
        double centerX = area.X + area.Width / 2d, centerY = area.Y + area.Height / 2d;
        if (Owner is Window owner)
        {
            var size = owner.FrameSize ?? owner.ClientSize;
            centerX = owner.Position.X + size.Width * owner.RenderScaling / 2; centerY = owner.Position.Y + size.Height * owner.RenderScaling / 2;
        }
        Position = new PixelPoint((int)Math.Clamp(centerX - width / 2, area.X, area.Right - width), (int)Math.Clamp(centerY - height / 2, area.Y, area.Bottom - height));
        FitNoteDeletionContent();
    }
    internal void FitNoteDeletionContent(bool resizeWindow = false)
    {
        if (_noteDeletionRoot is not { } frame) return;
        double scale = DesktopPageAppearance.Scale(this);
        if (resizeWindow)
        {
            Width = _noteDeletionWidth * scale; Height = _noteDeletionHeight * scale;
            if (Screens.ScreenFromWindow(this) is { } screen) { FitNoteDeletionToScreen(screen); return; }
        }
        frame.Width = (ClientSize.Width > 0 ? ClientSize.Width : Width) / scale;
        if (double.IsFinite(_noteDeletionHeight)) frame.Height = (ClientSize.Height > 0 ? ClientSize.Height : Height) / scale;
        if (Content is LayoutTransformControl transform) transform.InvalidateArrange();
    }
}
