using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace StopwatchOverlay.Desktop.Controls;

// Paint the complete numeral inside the native button. Tight text-presenter clips
// can cut off an edge when the popup runs at a fractional page or monitor scale.
public sealed class RecordCalendarNumber : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty = TextBlock.TextProperty.AddOwner<RecordCalendarNumber>();
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    private (string Text, FontFamily Family, double Size) _key;
    private Geometry? _shape;

    static RecordCalendarNumber() => AffectsRender<RecordCalendarNumber>(TextProperty, FontSizeProperty, FontFamilyProperty, ForegroundProperty);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Text is not { Length: > 0 } text || Foreground is not {} ink) return;
        var key = (text, FontFamily, FontSize);
        if (_key != key)
        {
            var label = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily), FontSize, Brushes.Black);
            _shape = label.BuildGeometry(default);
            _key = key;
        }
        if (_shape == null) return;
        var bounds = _shape.Bounds;
        double x = (Bounds.Width - bounds.Width) / 2;
        double y = (Bounds.Height - bounds.Height) / 2;
        if (this.GetVisualRoot() is TopLevel root && this.TransformToVisual(root) is {} transform)
        {
            var device = transform * Matrix.CreateScale(root.RenderScaling,root.RenderScaling);
            if (device.TryInvert(out var inverse))
            {
                var position = device.Transform(new Point(x, y));
                var snapped = inverse.Transform(new Point(Math.Round(position.X), Math.Round(position.Y)));
                x = snapped.X;
                y = snapped.Y;
            }
        }
        using (context.PushTransform(Matrix.CreateTranslation(x - bounds.X, y - bounds.Y)))
            context.DrawGeometry(ink, null, _shape);
    }
}
