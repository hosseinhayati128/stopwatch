using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace StopwatchOverlay.Desktop.Controls;

internal sealed class StopwatchPartHighlight(ThemedClockFace face, Border toolbar) : Control
{
    private static readonly Pen Edge = new(Brush.Parse("#123F43"), 3);
    private static readonly Pen Ring = new(Brush.Parse("#79F5D6"), 1.8);
    private static readonly IBrush Tint = Brush.Parse("#1679F5D6");
    internal StopwatchOpaqueLayers Layer { get; private set; }
    internal StopwatchAppearanceSettings Profile { get; set; } = new();

    internal void ShowLayer(StopwatchOpaqueLayers layer)
    {
        if (Layer == layer) return;
        Layer = layer;
        IsVisible = layer != StopwatchOpaqueLayers.None;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (Layer == StopwatchOpaqueLayers.None || face.Bounds.Width <= 0) return;
        if (Layer == StopwatchOpaqueLayers.HoverControls)
        {
            Outline(toolbar, new Rect(toolbar.Bounds.Size));
            return;
        }
        var type = StopwatchTypography.Resolve(Profile.Typography);
        var frame = StopwatchAppearanceAssets.Shared.Get("frame", Profile.Frame);
        var parts = StopwatchFaceLayout.Create(face.Bounds.Size, frame, face.TextSize / 48, out _);
        switch (Layer)
        {
            case StopwatchOpaqueLayers.Frame:
                Outline(face, parts.Frame);
                break;
            case StopwatchOpaqueLayers.TimeText:
                var time = parts.Time(StopwatchTypography.Width(face.Text, type, face.TextSize));
                Outline(face, new Rect(time.X, time.Y + 5 * parts.Scale, time.Width, 44 * parts.Scale).Intersect(parts.Content));
                break;
            case StopwatchOpaqueLayers.Artwork when Profile.Artwork != "clean":
                Outline(face, parts.Artwork(Profile.Artwork == "corner-stamp").Intersect(parts.ArtworkClip).Intersect(parts.Frame));
                break;
            case StopwatchOpaqueLayers.ProjectName:
            case StopwatchOpaqueLayers.StateMarkers:
                var name = parts.ProjectName(parts.FormatName(face.ProjectName, type, Brushes.Black));
                if (Layer == StopwatchOpaqueLayers.ProjectName)
                    Outline(face, name.Intersect(parts.Content));
                else
                {
                    Outline(face, parts.Marker(name));
                    Outline(face, parts.ActiveBadge);
                    if (face.ShowRec)
                        Outline(face, new Rect(parts.Content.Right - 36 * parts.Scale, parts.Content.Top + 5 * parts.Scale,
                            27 * parts.Scale, 10 * parts.Scale));
                }
                break;
        }

        void Outline(Control source, Rect bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0 || source.TransformToVisual(this) is not { } transform) return;
            var rect = bounds.TransformToAABB(transform).Inflate(1);
            context.DrawRectangle(Tint, Edge, rect, 2.5, 2.5);
            context.DrawRectangle(null, Ring, rect, 2.5, 2.5);
        }
    }
}
