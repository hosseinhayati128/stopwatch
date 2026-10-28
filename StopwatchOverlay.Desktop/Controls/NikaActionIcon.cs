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
    static NikaActionIcon() => AffectsRender<NikaActionIcon>(LabelProperty, InkProperty);
    public NikaActionIcon() { IsHitTestVisible = false; }
    public override void Render(DrawingContext context)
    {
        string key = Label switch { "Show overlay" => "Hide overlay", "Set project" => "Edit", "+ New timer" => "New timer", _ => Label };
        if (!Symbols.TryGetValue(key, out var geometry)) return;
        bool filled = key is "Start" or "Pause" or "Records & Analytics";
        using (context.PushTransform(Matrix.CreateScale(Bounds.Width / 24, Bounds.Height / 24)))
            context.DrawGeometry(filled ? Ink : null, filled ? null : new Pen(Ink, 1.7, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geometry);
    }
}
