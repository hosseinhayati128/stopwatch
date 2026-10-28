using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace StopwatchOverlay.Desktop.Controls;

/// <summary>Small gold line icons in the navigator rail; names remain visible as text.</summary>
public sealed class NikaRailIcon : Control
{
    public static readonly StyledProperty<string> NameTextProperty = AvaloniaProperty.Register<NikaRailIcon, string>(nameof(NameText), "");
    public string NameText { get => GetValue(NameTextProperty); set => SetValue(NameTextProperty, value); }
    static NikaRailIcon() => AffectsRender<NikaRailIcon>(NameTextProperty);
    public NikaRailIcon() { IsHitTestVisible = false; }
    public override void Render(DrawingContext context)
    {
        var gold = Brush.Parse("#F5D08B");
        var pen = new Pen(gold, 1.6);
        string path = NameText.ToLowerInvariant() switch
        {
            var name when name.Contains("read") => "M3,8 Q10,4 18,8 L18,31 Q10,26 3,30 Z M18,8 Q26,4 34,8 L34,30 Q26,26 18,31",
            var name when name.Contains("illustr") => "M9,28 L27,6 L33,10 L15,30 Z M9,28 Q2,31 3,36 Q12,36 15,30",
            var name when name.Contains("research") => "M7,3 L25,3 L32,10 L32,35 L7,35 Z M25,3 L25,11 L32,11 M12,16 L27,16 M12,22 L27,22 M12,28 L23,28",
            var name when name.Contains("program") => "M11,8 L2,19 L11,30 M27,8 L36,19 L27,30 M23,5 L15,33",
            _ => "M19,3 A16,16 0 1 0 19,35 A16,16 0 1 0 19,3 M25,11 L21,22 L12,28 L16,17 Z M19,0 L19,3 M19,35 L19,38"
        };
        using (context.PushTransform(Matrix.CreateScale(Bounds.Width / 38, Bounds.Height / 38)))
            context.DrawGeometry(null, pen, Geometry.Parse(path));
    }
}
