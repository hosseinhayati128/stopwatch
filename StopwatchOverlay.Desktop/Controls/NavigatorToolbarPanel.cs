using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace StopwatchOverlay.Desktop.Controls;

/// <summary>Navigator artwork under the existing interactive action buttons.</summary>
public sealed class NavigatorToolbarPanel : Panel
{
    public bool NavigatorEnabled { get; set; }
    public double ReferenceWidth { get; set; } = 520;
    public double Spacing { get; set; } = 4;
    protected override Size MeasureOverride(Size availableSize)
    {
        var visible = Children.Where(child => child.IsVisible).ToArray();
        if (!NavigatorEnabled)
        {
            double normalWidth = 0, height = 0;
            foreach (var child in visible) { child.Measure(availableSize); normalWidth += child.DesiredSize.Width; height = Math.Max(height, child.DesiredSize.Height); }
            return new Size(normalWidth + Math.Max(0, visible.Length - 1) * Spacing, height);
        }
        double width = Math.Max(0, double.IsFinite(availableSize.Width) ? Math.Min(ReferenceWidth, availableSize.Width) : ReferenceWidth);
        double diameter = width * Math.Min(.24, .84 / Math.Max(1, visible.Length));
        foreach (var child in visible) { child.Width = child.Height = diameter; child.Measure(new Size(diameter, diameter)); }
        return new Size(width, width * 340 / 900);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        var visible = Children.Where(child => child.IsVisible).ToArray();
        if (!NavigatorEnabled)
        {
            double x = 0;
            foreach (var child in visible) { child.Arrange(new Rect(x, (finalSize.Height - child.DesiredSize.Height) / 2, child.DesiredSize.Width, child.DesiredSize.Height)); x += child.DesiredSize.Width + Spacing; }
            return finalSize;
        }
        double diameter = finalSize.Width * Math.Min(.24, .84 / Math.Max(1, visible.Length));
        double gap = visible.Length > 1 ? (finalSize.Width * .86 - diameter) / (visible.Length - 1) : 0;
        double top = Math.Max(0, (finalSize.Height - diameter) / 2);
        for (int i = 0; i < visible.Length; i++) visible[i].Arrange(new Rect(finalSize.Width * .07 + i * gap, top, diameter, diameter));
        return finalSize;
    }
}

/// <summary>Draws chart layers on the real render context, behind independently opaque dials.</summary>
public sealed class NavigatorToolbarArtwork : Control
{
    private static readonly Lazy<Bitmap> Artwork = new(() =>
    {
        using var stream = AssetLoader.Open(new Uri("avares://StopwatchOverlay.Desktop/Assets/Pirate/navigator-toolbar.png"));
        return new Bitmap(stream);
    });
    public NavigatorOpaqueParts OpaqueParts { get; set; }
    public double SurfaceOpacity { get; set; }
    public override void Render(DrawingContext dc)
    {
        base.Render(dc);
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0) return;
        var bounds = new Rect(size);
        Geometry map = Geometry.Parse("M218,107 L1100,107 Q1180,202 1310,154 L1430,93 L1664,147 L1845,109 L1936,223 L1936,535 L1844,650 L1295,651 L1165,630 L935,651 L779,628 L641,623 L467,651 L211,651 L124,552 L118,220Z");
        map.Transform = new ScaleTransform(size.Width / 2060, size.Height / 763);
        Geometry frame = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(bounds), map);
        foreach (var (geometry, part) in new[] { (map, NavigatorOpaqueParts.ControlMap), (frame, NavigatorOpaqueParts.ControlBoard) })
            using (dc.PushOpacity((OpaqueParts & part) != 0 ? 1 : SurfaceOpacity))
            using (dc.PushGeometryClip(geometry)) dc.DrawImage(Artwork.Value, bounds);
    }
}
