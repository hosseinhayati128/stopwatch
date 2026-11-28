using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using StopwatchOverlay.Desktop.Platform;

namespace StopwatchOverlay.Desktop.Controls;

public partial class ClockPlacementPreview : UserControl
{
    public ClockPlacementPreview()
    {
        InitializeComponent();
        FontFamily = ClockPickerPresentation.Body;
    }

    internal void Update(string position, Screen? screen, TimerSession? session, Size overlaySize, bool showPrimary)
    {
        if (screen == null || screen.Bounds.Width <= 0 || screen.Bounds.Height <= 0 ||
            screen.WorkingArea.Width <= 0 || screen.WorkingArea.Height <= 0)
        {
            PlacementCaption.Text = "No display available";
            MonitorStage.IsVisible = false;
            return;
        }

        MonitorStage.IsVisible = true;
        var bounds = screen.Bounds;
        var area = screen.WorkingArea;
        double scale = Math.Min(320d / bounds.Width, 180d / bounds.Height);
        MonitorSurface.Width = bounds.Width * scale;
        MonitorSurface.Height = bounds.Height * scale;
        double left = (area.X - bounds.X) * scale;
        double top = (area.Y - bounds.Y) * scale;
        WorkArea.Width = area.Width * scale;
        WorkArea.Height = area.Height * scale;
        Canvas.SetLeft(WorkArea, left);
        Canvas.SetTop(WorkArea, top);
        double bottomInset = (bounds.Bottom - area.Bottom) * scale;
        TaskbarHandle.IsVisible = bottomInset >= 3 && MonitorSurface.Width >= TaskbarHandle.Width;
        Canvas.SetLeft(TaskbarHandle, (MonitorSurface.Width - TaskbarHandle.Width) / 2);
        Canvas.SetTop(TaskbarHandle, MonitorSurface.Height - bottomInset / 2 - 1);

        var saved = OverlayScreenConstraint.SavedPosition(session, screen);
        bool custom = position == "Custom";
        string caption = custom
            ? saved is { } point ? $"Custom placement  ·  Saved at {point.X}, {point.Y} px" : "Custom placement  ·  No saved position"
            : $"{position}  ·  {bounds.Width} × {bounds.Height}";
        PlacementCaption.Text = showPrimary ? $"{caption}  ·  Primary display" : caption;
        ClockHost.IsVisible = !custom || saved.HasValue;
        if (!ClockHost.IsVisible) return;

        if (overlaySize.Width <= 0 || overlaySize.Height <= 0) overlaySize = new Size(260, 84);
        var placed = OverlayScreenConstraint.PlaceOnScreen(position, saved.GetValueOrDefault(), overlaySize, screen);
        // Enlarge the sample for readability and project its position onto the remaining travel.
        double sampleScale = Math.Min(1, Math.Min(WorkArea.Width / 160, WorkArea.Height / 70));
        ClockHost.Width = 122 * sampleScale;
        ClockHost.Height = 41 * sampleScale;
        double inset = 3 * sampleScale;
        double Travel(int offset, int pixels, double logical) =>
            offset / Math.Max(1d, pixels - Math.Ceiling(logical * screen.Scaling));
        double x = Travel(placed.X - area.X, area.Width, overlaySize.Width);
        double y = Travel(placed.Y - area.Y, area.Height, overlaySize.Height);
        Canvas.SetLeft(ClockHost, left + inset + x * (WorkArea.Width - ClockHost.Width - 2 * inset));
        Canvas.SetTop(ClockHost, top + inset + y * (WorkArea.Height - ClockHost.Height - 2 * inset));
    }
}
