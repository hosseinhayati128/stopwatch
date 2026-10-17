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
    public static readonly AttachedProperty<string> ActionProperty = AvaloniaProperty.RegisterAttached<NavigatorDialArtwork, Control, string>("Action", "Play");
    public string Action { get => GetValue(ActionProperty); set => SetValue(ActionProperty, value); }
    static NavigatorDialArtwork() => AffectsRender<NavigatorDialArtwork>(ActionProperty);

    // The same engraved symbols and 42%-diameter sizing as the WPF navigator.
    private static readonly System.Collections.Generic.Dictionary<string, Geometry> Glyphs = new()
    {
        ["Close"] = Geometry.Parse("M 8,0 L 21,13 34,0 42,8 29,21 42,34 34,42 21,29 8,42 0,34 13,21 0,8 Z"),
        ["Play"] = Geometry.Parse("M 5,1 L 40,21 5,41 Z"),
        ["Pause"] = Geometry.Parse("M 3,2 H 15 V 40 H 3 Z M 27,2 H 39 V 40 H 27 Z"),
        ["Reset"] = Geometry.Parse("M 35,6 L 41,0 41,19 22,19 28,13 C 18,6 7,13 8,24 C 9,36 25,40 32,29 L 40,32 C 29,52 1,44 0,25 C -1,5 20,-5 35,6 Z")
    };
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
        if (Glyphs.TryGetValue(Action, out var source))
        {
            var glyph = source.Clone();
            var bounds = glyph.Bounds;
            double scale = diameter * .42 / Math.Max(bounds.Width, bounds.Height);
            glyph.Transform = new MatrixTransform(Matrix.CreateScale(scale, scale) *
                Matrix.CreateTranslation(Bounds.Width / 2 - bounds.Center.X * scale, Bounds.Height / 2 - bounds.Center.Y * scale));
            var fill = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(.6, 1, RelativeUnit.Relative) };
            fill.GradientStops.Add(new GradientStop(Color.Parse("#FFF0B2"), 0));
            fill.GradientStops.Add(new GradientStop(Color.Parse("#E5C776"), .5));
            fill.GradientStops.Add(new GradientStop(Color.Parse("#A57C35"), 1));
            context.DrawGeometry(fill, new Pen(Brush.Parse("#352316"), diameter * .03), glyph);
        }
    }
}
