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
    private double _noteDeletionHeight;

    private void ConfigureNoteDeletionActions()
    {
        _noteDeletionConfigured = true;
        Closed += (_, _) => _noteDeletionCompleted = true;
        Opened += (_, _) => Dispatcher.UIThread.Post(() => { if (IsVisible) CancelButton.Focus(); });
        AddHandler(KeyDownEvent, (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Complete(false); } }, RoutingStrategies.Tunnel);
    }

    private void SizeNoteDeletion(AppSettings settings, double height)
    {
        _noteDeletionRoot = (Control)Content!;
        _noteDeletionHeight = height;
        SystemDecorations = SystemDecorations.None; Background = Brushes.Transparent; TransparencyLevelHint = [WindowTransparencyLevel.Transparent]; CanResize = false;
        MinWidth = MinHeight = 0; MaxHeight = double.PositiveInfinity; SizeToContent = SizeToContent.Manual;
        double scale = AppUiScale.Normalize(settings.UiScalePercent) / 100; Width = 540 * scale; Height = _noteDeletionHeight * scale;
        Opened += (_, _) => { if (Screens.ScreenFromWindow(this) is { } screen) FitNoteDeletionToScreen(screen); else FitNoteDeletionContent(); };
        SizeChanged += (_, _) => FitNoteDeletionContent();
    }
    internal void FitNoteDeletionToScreen(Screen screen)
    {
        MaxWidth = Math.Floor(screen.WorkingArea.Width / screen.Scaling); MaxHeight = Math.Floor(screen.WorkingArea.Height / screen.Scaling);
        Width = Math.Min(Width, MaxWidth); Height = Math.Min(Height, MaxHeight);
        var area = screen.WorkingArea; double width = Math.Ceiling(Width * screen.Scaling), height = Math.Ceiling(Height * screen.Scaling);
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
            Width = 540 * scale; Height = _noteDeletionHeight * scale;
            if (Screens.ScreenFromWindow(this) is { } screen) { FitNoteDeletionToScreen(screen); return; }
        }
        frame.Width = (ClientSize.Width > 0 ? ClientSize.Width : Width) / scale; frame.Height = (ClientSize.Height > 0 ? ClientSize.Height : Height) / scale;
        if (Content is LayoutTransformControl transform) transform.InvalidateArrange();
    }
}
