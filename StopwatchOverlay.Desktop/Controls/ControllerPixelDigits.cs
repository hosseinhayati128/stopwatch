using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace StopwatchOverlay.Desktop.Controls;

/// <summary>Compact readouts share the Pixel UI numerals. The large clock uses its selected face.</summary>
public sealed class ControllerPixelDigits : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty = TextBlock.TextProperty.AddOwner<ControllerPixelDigits>();
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    static ControllerPixelDigits()
    {
        AffectsMeasure<ControllerPixelDigits>(TextProperty, FontSizeProperty);
        AffectsRender<ControllerPixelDigits>(TextProperty, FontSizeProperty, ForegroundProperty);
    }

    private StopwatchTypographyProfile Face = StopwatchTypography.Resolve("pixel-voyage");
    private readonly Dictionary<char, DrawingGroup> _glyphs = new();
    private IBrush? _ink;
    private double _size;
    private ControllerDigitAtlas? _largeFace;

    internal void SelectLargeFace(string id)
    {
        var face = ControllerDigitAtlas.Resolve(id);
        if (_largeFace == face && Face == StopwatchTypography.OriginalPixelFace) return;
        Face = StopwatchTypography.OriginalPixelFace;
        _glyphs.Clear();
        _largeFace = face;
        RenderOptions.SetBitmapInterpolationMode(this, Avalonia.Media.Imaging.BitmapInterpolationMode.None);
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(Math.Ceiling(_largeFace?.Measure(Text ?? "", FontSize) ?? StopwatchTypography.Width(Text ?? "", Face, FontSize * 156 / 166)) + 6, FontSize * 173 / 166);

    public override void Render(DrawingContext context)
    {
        if (Text is not { Length: > 0 } time || Foreground is not { } ink) return;
        if (_largeFace is not null)
        {
            _largeFace.Draw(context, time, FontSize, Bounds.Size);
            return;
        }
        double size = FontSize * 156 / 166;
        if (_size != size || !Equals(_ink, ink))
        {
            _glyphs.Clear();
            _size = size;
            _ink = ink;
        }

        var origin = StopwatchTypography.CenteredTimeOrigin(time, new Point(Bounds.Width / 2, Bounds.Height / 2), Face, _size);
        foreach (char character in time)
        {
            if (!_glyphs.TryGetValue(character, out var glyph))
            {
                glyph = new DrawingGroup();
                using (var drawing = glyph.Open())
                    StopwatchTypography.DrawTime(drawing, character.ToString(), default, Face, _size, ink);
                _glyphs.Add(character, glyph);
            }
            using (context.PushTransform(Matrix.CreateTranslation(origin.X, origin.Y)))
                glyph.Draw(context);
            origin = new Point(origin.X + StopwatchTypography.Advance(character, Face, _size), origin.Y);
        }
    }
}
