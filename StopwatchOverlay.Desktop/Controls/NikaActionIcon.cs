using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace StopwatchOverlay.Desktop.Controls;

/// <summary>Consistent line icons for Nika buttons, independent of font glyph support.</summary>
public sealed class NikaActionIcon : Control
{
    public static readonly StyledProperty<string> LabelProperty = AvaloniaProperty.Register<NikaActionIcon, string>(nameof(Label), "");
    public static readonly StyledProperty<IBrush> InkProperty = AvaloniaProperty.Register<NikaActionIcon, IBrush>(nameof(Ink), Brushes.AntiqueWhite);
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public IBrush Ink { get => GetValue(InkProperty); set => SetValue(InkProperty, value); }
    public static readonly StyledProperty<bool> PixelProperty = AvaloniaProperty.Register<NikaActionIcon, bool>(nameof(Pixel));
    public bool Pixel { get => GetValue(PixelProperty); set => SetValue(PixelProperty, value); }
    private static readonly Dictionary<string, Geometry> PixelSymbols = new()
    {
        ["Pause"] = Geometry.Parse("M2,1H5V11H2Z M7,1H10V11H7Z"),
        ["Start"] = Geometry.Parse("M2,1H4V2H6V3H8V4H10V5H11V7H10V8H8V9H6V10H4V11H2Z"),
        ["Add lap"] = Geometry.Parse("M1,1H3V3H1Z M5,1H11V3H5Z M1,5H3V7H1Z M5,5H11V7H5Z M1,9H3V11H1Z M5,9H11V11H5Z"),
        ["Reset"] = Geometry.Parse("M1,2H3V4H4V3H5V2H9V3H10V4H11V9H10V10H9V11H4V10H3V8H5V9H9V5H8V4H6V5H5V6H6V7H1Z"),
        ["Hide overlay"] = Geometry.Parse("M0,5H1V4H2V3H4V2H8V3H10V4H11V5H12V7H11V8H10V9H8V10H4V9H2V8H1V7H0Z M2,5V7H3V8H9V7H10V5H9V4H3V5Z M5,5H7V7H5Z"),
        ["Edit"] = Geometry.Parse("M1,8H2V7H3V6H4V5H5V4H6V3H7V2H8V1H10V2H11V4H10V5H9V6H8V7H7V8H6V9H5V10H4V11H1Z M3,8V9H4V8Z"),
    };
    private static readonly Dictionary<string, Geometry> Symbols = new()
    {
        ["Start"] = Geometry.Parse("M6,3 L21,12 L6,21 Z"),
        ["Pause"] = Geometry.Parse("M5,3 H9 V21 H5 Z M15,3 H19 V21 H15 Z"),
        ["Add lap"] = Geometry.Parse("M3,5 H5 M9,5 H22 M3,12 H5 M9,12 H22 M3,19 H5 M9,19 H22"),
        ["Reset"] = Geometry.Parse("M4,9 A9,9 0 1 1 4,17 M4,3 V9 H10"),
        ["Hide overlay"] = Geometry.Parse("M1,12 Q12,-3 23,12 Q12,27 1,12 Z M16,12 A4,4 0 1 1 8,12 A4,4 0 1 1 16,12"),
        ["Edit"] = Geometry.Parse("M4,16 L17,3 L21,7 L8,20 L3,21 Z M14,6 L18,10"),
        ["New timer"] = Geometry.Parse("M12,3 V21 M3,12 H21"),
        ["Records & Analytics"] = Geometry.Parse("M3,13 H7 V22 H3 Z M10,7 H14 V22 H10 Z M17,2 H21 V22 H17 Z"),
        ["Settings"] = Geometry.Parse("M12,2 V5 M12,19 V22 M2,12 H5 M19,12 H22 M5,5 L7,7 M17,17 L19,19 M5,19 L7,17 M17,7 L19,5 M19,12 A7,7 0 1 1 5,12 A7,7 0 1 1 19,12 M15,12 A3,3 0 1 1 9,12 A3,3 0 1 1 15,12"),
        ["Shortcuts"] = Geometry.Parse("M2,5 H22 V19 H2 Z M5,8 H6 M9,8 H10 M13,8 H14 M17,8 H19 M5,12 H6 M9,12 H10 M13,12 H14 M17,12 H19 M7,16 H17")
    };
    static NikaActionIcon() => AffectsRender<NikaActionIcon>(LabelProperty, InkProperty, PixelProperty);
    public NikaActionIcon() { IsHitTestVisible = false; }
    public override void Render(DrawingContext context)
    {
        string key = Label switch { "Resume" => "Start", "Show overlay" => "Hide overlay", "Set project" => "Edit", "+ New timer" => "New timer", _ => Label };
        if (Pixel && PixelSymbols.TryGetValue(key, out var pixel))
        {
            using (context.PushTransform(Matrix.CreateScale(Bounds.Width / 12, Bounds.Height / 12)))
                context.DrawGeometry(Ink, null, pixel);
            return;
        }
        if (!Symbols.TryGetValue(key, out var geometry)) return;
        bool filled = key is "Start" or "Pause" or "Records & Analytics";
        using (context.PushTransform(Matrix.CreateScale(Bounds.Width / 24, Bounds.Height / 24)))
            context.DrawGeometry(filled ? Ink : null, filled ? null : new Pen(Ink, 1.7, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geometry);
    }
}
