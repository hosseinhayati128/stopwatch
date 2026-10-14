using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace StopwatchOverlay.Desktop.Controls;

/// <summary>Renders a navigator dial in local coordinates on the actual render context.</summary>
public sealed class NavigatorDialArtwork : Control
{
    private static readonly Lazy<Bitmap> Artwork = new(() =>
    {
        using var stream = AssetLoader.Open(new Uri("avares://StopwatchOverlay.Desktop/Assets/Pirate/navigator-dial.png"));
        return new Bitmap(stream);
    });
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;
        double diameter = Math.Min(Bounds.Width, Bounds.Height);
        context.DrawImage(Artwork.Value, new Rect((Bounds.Width - diameter) / 2, (Bounds.Height - diameter) / 2, diameter, diameter));
    }
}
