using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation.Peers;
using Avalonia.Automation;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace StopwatchOverlay.Desktop.Controls;

/// <summary>Draws the same normalized atlas crops used by the Windows One Piece theme.</summary>
public sealed class PirateArtwork : Control
{
    public static readonly StyledProperty<string> PartProperty = AvaloniaProperty.Register<PirateArtwork, string>(nameof(Part), "Wood");
    public static readonly AttachedProperty<string> IconProperty = AvaloniaProperty.RegisterAttached<PirateArtwork, Control, string>("Icon", "");
    public string Part { get => GetValue(PartProperty); set => SetValue(PartProperty, value); }
    public static string GetIcon(Control control) => control.GetValue(IconProperty);
    public static void SetIcon(Control control, string value) => control.SetValue(IconProperty, value);
    private static readonly Dictionary<string, Bitmap> Images = new();
    static PirateArtwork() => AffectsRender<PirateArtwork>(PartProperty);
    public PirateArtwork() { IsHitTestVisible = false; }
    public override void Render(DrawingContext context) => RenderPart(context, Part, Bounds.Size);

    internal static void RenderPart(DrawingContext context, string part, Size bounds)
    {
        var (file, crop, uniform) = part switch
        {
            "Header" => ("controller-background.png", new Rect(.024,.035,.950,.103), false),
            "Rail" => ("controller-background.png", new Rect(.024,.198,.293,.770), false),
            "Map" => ("controller-background.png", new Rect(.322,.200,.651,.725), false),
            "Corner" => ("controller-background.png", new Rect(0,0,.090,.118), false),
            "Brass" => ("brass-plate.png", new Rect(.045,.275,.91,.38), false),
            "Lion" => ("icons.png", new Rect(0,0,.25,.5), true),
            "Ram" => ("icons.png", new Rect(.25,0,.25,.5), true),
            "Telescope" => ("icons.png", new Rect(.5,0,.25,.5), true),
            "Anchor" => ("icons.png", new Rect(.75,0,.25,.5), true),
            "Swords" => ("icons.png", new Rect(0,.5,.25,.5), true),
            "Skull" => ("icons.png", new Rect(.25,.5,.25,.5), true),
            "Paint" => ("icons.png", new Rect(.5,.5,.25,.5), true),
            "LogPose" => ("navigation-tools.png", new Rect(0,0,.5,1), true),
            "Hammer" => ("navigation-tools.png", new Rect(.5,0,.5,1), true),
            "" => ("", default(Rect), false),
            _ => ("controller-background.png", new Rect(.42,.038,.47,.14), false)
        };
        if (file.Length == 0 || bounds.Width <= 0 || bounds.Height <= 0) return;
        if (!Images.TryGetValue(file, out var bitmap))
        {
            bitmap = new Bitmap(AssetLoader.Open(new Uri($"avares://StopwatchOverlay.Desktop/Assets/Pirate/{file}")));
            Images.Add(file, bitmap);
        }
        var source = new Rect(crop.X * bitmap.Size.Width, crop.Y * bitmap.Size.Height, crop.Width * bitmap.Size.Width, crop.Height * bitmap.Size.Height);
        var destination = new Rect(bounds);
        if (uniform)
        {
            double scale = Math.Min(destination.Width / source.Width, destination.Height / source.Height);
            var size = new Size(source.Width * scale, source.Height * scale);
            destination = new Rect((bounds.Width - size.Width) / 2, (bounds.Height - size.Height) / 2, size.Width, size.Height);
        }
        if (part == "Corner")
        {
            // The atlas corner is rectangular; retain only the brass arms so its
            // wood interior cannot cover header text or workspace controls.
            using (context.PushClip(new Rect(0, 0, bounds.Width, bounds.Height * .28)))
                context.DrawImage(bitmap, source, destination);
            using (context.PushClip(new Rect(0, bounds.Height * .28, bounds.Width * .27, bounds.Height * .72)))
                context.DrawImage(bitmap, source, destination);
            return;
        }
        context.DrawImage(bitmap, source, destination);
    }
}

/// <summary>Keeps normal wrapping, with the Windows two-column action order in One Piece.</summary>
public sealed class PirateActionPanel : WrapPanel
{
    public static readonly StyledProperty<bool> NikaProperty = AvaloniaProperty.Register<PirateActionPanel, bool>(nameof(Nika));
    public bool Nika { get => GetValue(NikaProperty); set => SetValue(NikaProperty, value); }
    public static readonly StyledProperty<bool> PirateProperty = AvaloniaProperty.Register<PirateActionPanel, bool>(nameof(Pirate));
    public bool Pirate { get => GetValue(PirateProperty); set => SetValue(PirateProperty, value); }
    static PirateActionPanel() => AffectsMeasure<PirateActionPanel>(PirateProperty, NikaProperty);
    protected override Size MeasureOverride(Size availableSize)
    {
        if (Nika)
        {
            double usable = double.IsFinite(availableSize.Width) ? availableSize.Width : 880;
            int nikaColumns = usable < 600 ? 2 : 5;
            foreach (var child in Children) child.Measure(new Size(usable / nikaColumns, 60));
            return new Size(usable, nikaColumns == 5 ? 60 : 204);
        }
        if (!Pirate) return base.MeasureOverride(availableSize);
        double width = double.IsFinite(availableSize.Width) ? availableSize.Width : 680;
        int columns = width < 500 ? 1 : 2;
        var visible = Children.Where(c => c.IsVisible).ToArray();
        foreach (var child in visible) child.Measure(new Size(Math.Max(0, (width - (columns - 1) * 14) / columns), 74));
        int rows = (visible.Length + columns - 1) / columns;
        return new Size(width, Math.Max(0, rows * 80 - 6));
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Nika)
        {
            double[] widths = [.227, .199, .182, .254, .138];
            bool narrow = finalSize.Width < 600;
            double x = 0;
            for (int index = 0; index < Children.Count; index++)
            {
                double nikaWidth = narrow ? (finalSize.Width - 12) / 2 : (finalSize.Width - 48) * widths[index];
                Children[index].Arrange(new Rect(narrow ? index % 2 * (nikaWidth + 12) : x, narrow ? index / 2 * 72 : 0, nikaWidth, 60));
                x += nikaWidth + 12;
            }
            return finalSize;
        }
        if (!Pirate) return base.ArrangeOverride(finalSize);
        int columns = finalSize.Width < 500 ? 1 : 2;
        double width = Math.Max(0, (finalSize.Width - (columns - 1) * 14) / columns);
        int[] slots = [0, 2, 1, 3];
        int i = 0;
        foreach (var child in Children.Where(c => c.IsVisible))
        {
            // Keep Start/Reset followed by Lap/Overlay in either layout.
            int slot = columns == 2 && i < slots.Length ? slots[i] : i;
            child.Arrange(new Rect(slot % columns * (width + 14), slot / columns * 80, width, 74));
            i++;
        }
        return finalSize;
    }
}

/// <summary>Engraved live timer lettering; the value remains bound to the accessible time display.</summary>
public sealed class PirateTimerText : Control
{
    protected override AutomationPeer OnCreateAutomationPeer() => new TimerPeer(this);
    private sealed class TimerPeer(PirateTimerText owner) : ControlAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(PirateTimerText);
        protected override string GetNameCore() => owner.Text;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
    }
    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<PirateTimerText,string>(nameof(Text), "");
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    static PirateTimerText() { AffectsMeasure<PirateTimerText>(TextProperty); AffectsRender<PirateTimerText>(TextProperty); }
    private Geometry Lettering() => new FormattedText(Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
        new Typeface(new FontFamily("avares://StopwatchOverlay.Desktop/Assets/Fonts/Pirate/#Almendra")), 132, Brushes.Black).BuildGeometry(new Point()) ?? new GeometryGroup();
    protected override Size MeasureOverride(Size availableSize)
    {
        var bounds = Lettering().Bounds;
        return new Size(Math.Max(1, bounds.Width + 12), Math.Max(1, bounds.Height + 12));
    }
    public override void Render(DrawingContext context)
    {
        var geometry = Lettering();
        if (geometry.Bounds.Width <= 0 || geometry.Bounds.Height <= 0) return;
        using (context.PushTransform(Matrix.CreateTranslation(6 - geometry.Bounds.X, 8 - geometry.Bounds.Y)))
            context.DrawGeometry(Brush.Parse("#563519"), new Pen(Brush.Parse("#3C2313"), 5), geometry);
        using (context.PushTransform(Matrix.CreateTranslation(6 - geometry.Bounds.X, 6 - geometry.Bounds.Y)))
        {
            var gold = new LinearGradientBrush { StartPoint = new RelativePoint(0,0,RelativeUnit.Relative), EndPoint = new RelativePoint(0,1,RelativeUnit.Relative) };
            gold.GradientStops.Add(new GradientStop(Color.Parse("#F9E1A6"), 0));
            gold.GradientStops.Add(new GradientStop(Color.Parse("#CEA969"), .45));
            gold.GradientStops.Add(new GradientStop(Color.Parse("#835C2C"), .7));
            gold.GradientStops.Add(new GradientStop(Color.Parse("#E8C786"), 1));
            context.DrawGeometry(gold, new Pen(Brush.Parse("#2F1E13"), 3), geometry);
            context.DrawGeometry(null, new Pen(Brush.Parse("#B4F7DD9E"), .65), geometry);
        }
    }
}
