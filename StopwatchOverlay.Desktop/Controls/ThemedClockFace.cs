using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation.Peers;
using Avalonia.Automation;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia.Controls.Templates;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;

namespace StopwatchOverlay.Desktop.Controls;

/// <summary>The shared floating-clock renderer used by live overlays and settings previews.</summary>
public sealed class ThemedClockFace : Control
{
    protected override AutomationPeer OnCreateAutomationPeer() => new ClockPeer(this);
    private sealed class ClockPeer(ThemedClockFace owner) : ControlAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(ThemedClockFace);
        protected override string GetNameCore() => $"{owner.ProjectName} {owner.Text}".Trim();
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
    }

    private string _text = "00:00:00.0", _projectName = "";
    private bool _showRec;
    public string Text { get => _text; set { _text = value ?? ""; InvalidateMeasure(); InvalidateVisual(); } }
    public string ProjectName { get => _projectName; set { _projectName = value?.Trim() ?? ""; InvalidateMeasure(); InvalidateVisual(); } }
    public bool ShowRec { get => _showRec; set { _showRec = value; InvalidateMeasure(); InvalidateVisual(); } }
    public bool IsRunning { get; set; }
    public string EffectiveOverlayTheme { get; private set; } = OverlayThemeCatalog.Midnight;
    public bool IsNavigator => EffectiveOverlayTheme == OverlayThemeCatalog.Pirate;
    public double TextSize { get; private set; } = 48;
    public IBrush TimerBrush { get; private set; } = Brushes.White;
    public IBrush NameBrush { get; private set; } = Brushes.White;
    public FontFamily TimerFont { get; private set; } = new("Consolas");
    public double SurfaceOpacity { get; private set; } = .5;
    public IBrush OutlineBrush { get; private set; } = Brushes.Black;
    public double OutlineWidth { get; private set; } = 2;
    public NavigatorOpaqueParts OpaqueParts { get; private set; } = NavigatorOpaqueParts.Default;
    public Thickness ClockPadding => Token("ThemeOverlayPadding", new Thickness(15, 8));
    public CornerRadius ClockCornerRadius => Token("ThemeOverlayCornerRadius", new CornerRadius(8));
    public T Token<T>(string key, T fallback) => Resources.TryGetResource(key, ActualThemeVariant, out var value) && value is T typed ? typed : fallback;
    public IBrush BrushToken(string key, IBrush? fallback = null) => Token<IBrush>(key, fallback ?? Brushes.Transparent);
    public double LayerOpacity(NavigatorOpaqueParts part) => (OpaqueParts & part) != 0 ? 1 : SurfaceOpacity;
    public void ApplySettings(AppSettings settings)
    {
        ApplyTheme(settings.OverlayTheme, settings.ApplicationTheme);
        ApplyAppearance(ParseColor(settings.TextColor, Colors.White), ParseColor(settings.BorderColor, Colors.Black),
            settings.TextSize, settings.BorderWidth, settings.FontFamily, settings.BackgroundOpacity / 100,
            settings.TextColor == "Theme default", settings.OpaqueOverlayParts);
        ShowRec = settings.ShowRecIndicator;
    }
    private static Color ParseColor(string value, Color fallback) => value switch
    {
        "Charcoal" => Color.FromRgb(44, 41, 36),
        "Dark Gray" => Colors.DarkGray,
        _ => Color.TryParse(value, out var color) ? color : fallback
    };
    public void ApplyTheme(string? overlayTheme, string? applicationTheme)
    {
        EffectiveOverlayTheme = OverlayThemeCatalog.Resolve(overlayTheme, applicationTheme);
        Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(ThemeResources.Load("Overlay/" + ThemeResources.OverlayFile(EffectiveOverlayTheme)));
        InvalidateMeasure(); InvalidateVisual();
    }
    public void ApplyAppearance(Color textColor, Color borderColor, double size, double outlineWidth, string family,
        double opacity, bool themeText, NavigatorOpaqueParts parts)
    {
        TextSize = double.IsFinite(size) ? Math.Clamp(size, 16, 120) : 48;
        OutlineWidth = double.IsFinite(outlineWidth) ? Math.Clamp(outlineWidth, 0, 20) : 2;
        SurfaceOpacity = OverlayPresentationPolicy.ClampBackgroundOpacity(opacity);
        OpaqueParts = parts & NavigatorOpaqueParts.All;
        OutlineBrush = new SolidColorBrush(borderColor);
        TimerBrush = themeText ? BrushToken("OverlayTimerForegroundBrush", new SolidColorBrush(textColor)) : new SolidColorBrush(textColor);
        NameBrush = themeText ? BrushToken("OverlayProjectForegroundBrush", TimerBrush) : TimerBrush;
        // Consolas is the legacy default. Explicit nondefault families continue to win.
        TimerFont = string.IsNullOrWhiteSpace(family) || family.Equals("Consolas", StringComparison.OrdinalIgnoreCase)
            || family.Equals("Cascadia Mono", StringComparison.OrdinalIgnoreCase)
            ? Token("ThemeTimerFontFamily", new FontFamily("Consolas")) : new FontFamily(family);
        InvalidateMeasure(); InvalidateVisual();
    }
    private sealed class SavedButtonTemplate(IControlTemplate? template) { public IControlTemplate? Template { get; } = template; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Button, SavedButtonTemplate> OriginalTemplates = new();
    private static readonly FuncControlTemplate<Button> NavigatorButtonTemplate = new((button, scope) =>
    {
        var grid = new Grid { Background = Brushes.Transparent };
        var dial = new NavigatorDialArtwork { IsHitTestVisible = false };
        dial.Bind(NavigatorDialArtwork.ActionProperty, button.GetObservable(NavigatorDialArtwork.ActionProperty));
        grid.Children.Add(dial);
        var hoverRing = new Avalonia.Controls.Shapes.Ellipse
        {
            Margin = new Thickness(12), Stroke = Brush.Parse("#FFF2B3"), StrokeThickness = 2,
            Opacity = .7, IsHitTestVisible = false
        };
        hoverRing.Bind(IsVisibleProperty, button.GetObservable(IsPointerOverProperty));
        grid.Children.Add(hoverRing);
        var content = new ContentPresenter
        {
            IsVisible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0)
        };
        content.Bind(ContentPresenter.ContentProperty, button.GetObservable(Button.ContentProperty));
        content.Bind(ContentPresenter.ForegroundProperty, button.GetObservable(Button.ForegroundProperty));
        grid.Children.Add(content);
        return grid;
    });
    /// <summary>Apply the clock palette to the detached hover toolbar and preview actions.</summary>
    public void ApplyToolbar(Border surface, System.Collections.Generic.IReadOnlyList<Button> buttons)
    {
        surface.Resources.MergedDictionaries.Clear();
        surface.Resources.MergedDictionaries.Add(ThemeResources.Load("Overlay/" + ThemeResources.OverlayFile(EffectiveOverlayTheme)));
        surface.Background = BrushToken("OverlayToolbarSurfaceBrush");
        surface.BorderBrush = BrushToken("OverlayToolbarBorderBrush");
        surface.BorderThickness = new Thickness(1);
        surface.CornerRadius = Token("OverlayToolbarCornerRadius", new CornerRadius(8));
        surface.Padding = Token("OverlayToolbarPadding", new Thickness(3));
        if (IsNavigator)
        {
            surface.Background = Brushes.Transparent;
            surface.BorderThickness = new Thickness(0);
            surface.Padding = new Thickness(0);
        }
        surface.Opacity = 1;
        if (surface.Child is StackPanel original)
        {
            surface.Child = null;
            var replacement = new NavigatorToolbarPanel { Spacing = original.Spacing };
            foreach (var child in original.Children.ToArray()) { original.Children.Remove(child); replacement.Children.Add(child); }
            var layers = new Grid();
            layers.Children.Add(new NavigatorToolbarArtwork { IsHitTestVisible = false });
            layers.Children.Add(replacement);
            surface.Child = layers;
        }
        var panel = (surface.Child as Grid)?.Children.OfType<NavigatorToolbarPanel>().FirstOrDefault();
        var artwork = (surface.Child as Grid)?.Children.OfType<NavigatorToolbarArtwork>().FirstOrDefault();
        if (panel != null)
        {
            panel.NavigatorEnabled = IsNavigator;
            panel.ReferenceWidth = Math.Max(Em * 8.6, (Text.Sum(Advance) * Em / 100 + Em * .15) / .53);
            if (artwork != null)
            {
                artwork.IsVisible = IsNavigator;
                artwork.OpaqueParts = OpaqueParts;
                artwork.SurfaceOpacity = SurfaceOpacity;
                artwork.InvalidateVisual();
            }
            panel.InvalidateMeasure(); panel.InvalidateVisual();
        }
        for (int i = 0; i < buttons.Count; i++)
        {
            var button = buttons[i];
            button.SetValue(NavigatorDialArtwork.ActionProperty, i == 0 ? "Close" : i == 1 ? IsRunning ? "Pause" : "Play" : i == 2 ? "Reset" : "");
            var originalTemplate = OriginalTemplates.GetValue(button, b => new SavedButtonTemplate(b.Template));
            bool changingTemplate = IsNavigator
                ? !ReferenceEquals(button.Template, NavigatorButtonTemplate)
                : ReferenceEquals(button.Template, NavigatorButtonTemplate);
            if (changingTemplate)
            {
                // Release the real icon control before another presenter takes ownership.
                foreach (var presenter in button.GetVisualDescendants().OfType<ContentPresenter>().ToArray())
                {
                    if (ReferenceEquals(presenter.Content, button.Content))
                    {
                        presenter.Content = null;
                        presenter.UpdateChild();
                    }
                }
                if (IsNavigator) button.Template = NavigatorButtonTemplate;
                else if (originalTemplate.Template != null) button.Template = originalTemplate.Template;
                else button.ClearValue(Button.TemplateProperty);
            }
            button.Classes.Set("clockAction", true);
            string prefix = i == 0 ? "OverlayClose" : i == 1 ? "OverlayPause" : "OverlayAction";
            button.Width = button.Height = Token("OverlayActionSize", 28d);
            button.MinWidth = button.MinHeight = 0;
            button.Padding = new Thickness(0);
            button.Background = BrushToken(prefix + "BackgroundBrush");
            button.BorderBrush = BrushToken(prefix + "BorderBrush");
            button.Foreground = BrushToken(prefix + "ForegroundBrush", BrushToken("OverlayActionForegroundBrush", Brushes.White));
            button.BorderThickness = Token("OverlayActionBorderThickness", new Thickness(0));
            button.CornerRadius = Token("ThemeOverlayButtonCornerRadius", new CornerRadius(5));
            if (IsNavigator)
            {
                button.Width = button.Height = 48 * Em / 48;
                button.Background = Brushes.Transparent;
                button.BorderThickness = new Thickness(0);
                button.Opacity = LayerOpacity(NavigatorOpaqueParts.ControlDials);
            }
            else button.Opacity = 1;
            foreach (var descendant in button.GetLogicalDescendants())
            {
                if (descendant is Avalonia.Controls.Shapes.Shape shape)
                {
                    if (shape.Stroke != null) shape.Stroke = button.Foreground;
                    if (shape.Fill != null) shape.Fill = button.Foreground;
                }
            }
        }
    }
    private static Bitmap Load(string name)
    {
        using var stream = AssetLoader.Open(new Uri($"avares://StopwatchOverlay.Desktop/Assets/Pirate/navigator-{name}.png"));
        return new Bitmap(stream);
    }
    private static readonly Lazy<Bitmap> Map = new(() => Load("map"));
    // Trace the inner edge of the wooden frame in the source artwork coordinates.
    // The compass and diagonal ropes cross that edge and belong to the frame layer.
    private static Geometry PaperGeometry(double width, double height)
    {
        Geometry paper = new RectangleGeometry(new Rect(139, 151, 1274, 751));
        Geometry ornaments = new GeometryGroup
        {
            Children =
            {
                new EllipseGeometry(new Rect(34, 33, 222, 230)),
                Geometry.Parse("M1199,49 C1247,43 1260,65 1298,111 L1495,326 Q1510,352 1498,384 L1482,360 L1270,132 C1244,99 1220,82 1199,79Z"),
                Geometry.Parse("M1486,734 L1515,760 C1441,860 1331,951 1250,997 L1207,972 C1292,917 1410,817 1486,734Z")
            }
        };
        var result = new CombinedGeometry(GeometryCombineMode.Exclude, paper, ornaments)
        { Transform = new ScaleTransform(width / 1536, height / 1024) };
        return result;
    }
    private static readonly Lazy<Bitmap> Plate = new(() => Load("plate"));
    private void DrawPlate(DrawingContext dc, Rect bounds)
    {
        var size = Plate.Value.Size;
        dc.DrawImage(Plate.Value, new Rect(0, size.Height * .14, size.Width, size.Height * .70), bounds);
    }
    private static readonly Geometry[] Segments = new[]
    {
        "M9,0H49L57,7 49,14H9L1,7Z", "M51,10L58,3V44L51,51 44,44V18Z",
        "M51,51L58,58V97L51,90 44,82V59Z", "M9,86H49L57,94 49,100H9L1,94Z",
        "M7,51L14,59V82L7,90 0,97V58Z", "M0,3L7,10 14,18V44L7,51 0,44Z",
        "M9,44H49L57,51 49,58H9L1,51Z"
    }.Select(path => { Geometry geometry = Geometry.Parse(path); return geometry; }).ToArray();
    private static readonly int[] DigitMasks = [0x3f, 0x06, 0x5b, 0x4f, 0x66, 0x6d, 0x7d, 0x07, 0x7f, 0x6f];
    private static readonly IBrush Bevel = CreateBevel();
    private static IBrush CreateBevel()
    {
        var brush = new SolidColorBrush(Color.FromRgb(239, 213, 151));
        
        return brush;
    }
    private double Em => double.IsFinite(TextSize) ? Math.Clamp(TextSize, 8, 210) : 48;
    private bool Digital => Text.All(c => char.IsAsciiDigit(c) || c is ':' or '.' or ',' or '-' or ' ')
        && (TimerFont.Name.Contains("Consolas", StringComparison.OrdinalIgnoreCase)
            || TimerFont.Name.Contains("Cascadia", StringComparison.OrdinalIgnoreCase)
            || TimerFont.Name.Contains("Barlow", StringComparison.OrdinalIgnoreCase));
    private static double Advance(char c) => c is ':' or ' ' ? 28 : c is '.' or ',' ? 22 : 70;
    private FormattedText Format(string text, FontFamily font, double size, IBrush brush, FontWeight? weight = null) => new(text,
        CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(font, FontStyle.Normal, weight ?? FontWeight.Bold), size, brush);

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!IsNavigator) return MeasureStandard();
        double textWidth = Digital ? Text.Sum(Advance) * Em / 100 : Format(Text, TimerFont, Em, TimerBrush).WidthIncludingTrailingWhitespace;
        double width = Math.Max(Em * 8.6, (textWidth + Em * .15) / .53);
        return new Size(width, width * 2 / 3);
    }

    public Rect MetalBounds => new(Bounds.Width * .155, Bounds.Height * .352, Bounds.Width * .705, Bounds.Height * .264);
    public Rect MetalInsetBounds => new(MetalBounds.X + MetalBounds.Width * .117, MetalBounds.Y + MetalBounds.Height * .12,
        MetalBounds.Width * .766, MetalBounds.Height * .76);

    public override void Render(DrawingContext dc)
    {
        base.Render(dc);
        if (!IsNavigator) { RenderStandard(dc); return; }
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;
        var bounds = new Rect(Bounds.Size);
        var plate = MetalBounds;
        var inset = new RectangleGeometry(MetalInsetBounds, Em * .015, Em * .015);
        Geometry paper = PaperGeometry(Bounds.Width, Bounds.Height);
        Geometry frame = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(bounds), paper);
        foreach (var (clip, part) in new[] { (paper, NavigatorOpaqueParts.ClockMap), (frame, NavigatorOpaqueParts.ClockFrame) })
            using (dc.PushOpacity(LayerOpacity(part)))
            using (dc.PushGeometryClip(clip)) dc.DrawImage(Map.Value, bounds);
        using (dc.PushOpacity(LayerOpacity(NavigatorOpaqueParts.MetalFill)))
        using (dc.PushGeometryClip(inset)) DrawPlate(dc, plate);
        var rim = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(plate), inset);
        using (dc.PushOpacity(LayerOpacity(NavigatorOpaqueParts.MetalBorder)))
        using (dc.PushGeometryClip(rim)) DrawPlate(dc, plate);
        using (dc.PushOpacity(LayerOpacity(NavigatorOpaqueParts.TimerText))) DrawTime(dc, plate);
        if (!string.IsNullOrWhiteSpace(ProjectName))
        {
            using (dc.PushOpacity(LayerOpacity(NavigatorOpaqueParts.ProjectName)))
            {
                FormattedText label = Format(ProjectName, new FontFamily("Times New Roman"), Em * .49, NameBrush);
                label.MaxTextWidth = Bounds.Width * .77;
                label.MaxTextHeight = Em * .75;
                label.Trimming = TextTrimming.CharacterEllipsis;
                label.TextAlignment = TextAlignment.Center;
                dc.DrawText(label, new Point(Bounds.Width * .125, Bounds.Height * .655));
            }
        }
        if (ShowRec) dc.DrawEllipse(Brushes.Firebrick, new Pen(Bevel, 1), new Point(Bounds.Width * .875, Bounds.Height * .31), Em * .07, Em * .07);
    }

    private void DrawTime(DrawingContext dc, Rect plate)
    {
        if (!Digital)
        {
            var text = Format(Text, TimerFont, Em, TimerBrush);
            Geometry? geometry = text.BuildGeometry(new Point(plate.X + (plate.Width - text.Width) / 2, plate.Y + (plate.Height - text.Height) / 2));
            if (geometry != null) dc.DrawGeometry(TimerBrush, new Pen(OutlineBrush, Math.Clamp(OutlineWidth, 0, 8)), geometry);
            return;
        }
        double scale = Em / 100;
        double width = Text.Sum(Advance) * scale;
        double left = plate.X + (plate.Width - width) / 2;
        double top = plate.Y + (plate.Height - Em) / 2;
        double x = 0;
        var pen = new Pen(Bevel, 1.4 * scale);
        var outline = new Pen(OutlineBrush, Math.Clamp(OutlineWidth, 0, 8) + pen.Thickness);
        // Keep transforms on the geometry. Changing the shared drawing context here
        // can contaminate later sibling rendering beneath a scaled settings Viewbox.
        void DrawSegment(Geometry source)
        {
            var geometry = source.Clone();
            geometry.Transform = new MatrixTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(left + x * scale, top));
            if (OutlineWidth > 0) dc.DrawGeometry(null, outline, geometry);
            dc.DrawGeometry(TimerBrush, pen, geometry);
        }
        void DrawDot(Point source, double radius)
        {
            var center = new Point(left + (x + source.X) * scale, top + source.Y * scale);
            if (OutlineWidth > 0) dc.DrawEllipse(null, outline, center, radius * scale, radius * scale);
            dc.DrawEllipse(TimerBrush, pen, center, radius * scale, radius * scale);
        }
        foreach (char c in Text)
        {
            if (char.IsAsciiDigit(c) || c == '-')
            {
                int mask = c == '-' ? 0x40 : DigitMasks[c - '0'];
                for (int segment = 0; segment < 7; segment++)
                    if ((mask & (1 << segment)) != 0) DrawSegment(Segments[segment]);
                if (c == '0') DrawSegment(Geometry.Parse("M41,17L45,22 18,84 13,89 13,82Z"));
            }
            else if (c == ':') { DrawDot(new Point(10, 30), 7); DrawDot(new Point(10, 72), 7); }
            else if (c is '.' or ',') DrawDot(new Point(8, 93), 6);
            x += Advance(c);
        }
    }

    private FormattedText StandardTime() => Format(Text, TimerFont, Em, TimerBrush, Token("OverlayTimerFontWeight", FontWeight.Bold));
    private FormattedText StandardName() => Format(ProjectName, Token("AppFontFamily", new FontFamily("Inter")),
        Token("OverlayProjectFontSize", 13d), NameBrush, Token("OverlayProjectFontWeight", FontWeight.SemiBold));
    private Size MeasureStandard()
    {
        var text = StandardTime(); var padding = ClockPadding;
        double width = text.WidthIncludingTrailingWhitespace + padding.Left + padding.Right + OutlineWidth * 2 + (ShowRec ? 24 : 0);
        width = Math.Max(width, Token("OverlayMinimumWidth", 0d) * Em / 48);
        double height = text.Height + padding.Top + padding.Bottom + OutlineWidth * 2;
        if (ProjectName.Length > 0) height += StandardName().Height + Token("OverlayProjectMargin", new Thickness(4, 2, 4, 0)).Top;
        return new Size(width, height);
    }
    private void RenderStandard(DrawingContext dc)
    {
        var rect = new Rect(Bounds.Size).Deflate(.5); var radius = ClockCornerRadius.TopLeft;
        using (dc.PushOpacity(SurfaceOpacity)) dc.DrawRectangle(BrushToken("OverlayChromeBrush"), null, rect, radius, radius);
        double border = Token("ThemeOverlayFrameBorderThickness", new Thickness(0)).Left;
        if (border > 0) dc.DrawRectangle(null, new Pen(BrushToken("OverlayChromeBorderBrush"), border), rect, radius, radius);
        if (EffectiveOverlayTheme == OverlayThemeCatalog.AcanthusLight)
            dc.DrawRectangle(null, new Pen(BrushToken("OverlayInnerRuleBrush"), 1), rect.Deflate(8), 8, 8);
        var text = StandardTime(); var padding = ClockPadding;
        var pos = new Point((Bounds.Width - text.WidthIncludingTrailingWhitespace + (ShowRec ? 24 : 0)) / 2, padding.Top + OutlineWidth);
        var glyphs = text.BuildGeometry(pos);
        if (glyphs != null) dc.DrawGeometry(TimerBrush, OutlineWidth > 0 ? new Pen(OutlineBrush, OutlineWidth * 2) : null, glyphs);
        // Fill again over the stroke so outline growth never reduces the glyph face.
        dc.DrawText(text, pos);
        if (ShowRec) dc.DrawEllipse(BrushToken("RecBrush", Brushes.Red), null, new Point(pos.X - 17, pos.Y + text.Height / 2), 7, 7);
        if (ProjectName.Length > 0)
        {
            var name = StandardName(); name.MaxTextWidth = Math.Max(1, Bounds.Width - padding.Left - padding.Right);
            name.Trimming = TextTrimming.CharacterEllipsis; name.TextAlignment = TextAlignment.Center;
            using (dc.PushOpacity(Token("OverlayProjectOpacity", .78d)))
                dc.DrawText(name, new Point(padding.Left, pos.Y + text.Height + Token("OverlayProjectMargin", new Thickness(4, 2, 4, 0)).Top));
        }
        DrawOrnaments(dc);
    }
    private static readonly System.Collections.Generic.Dictionary<string, (Geometry geometry, bool fill)[]> OrnamentCache = new();
    private void Ornament(DrawingContext dc, string file, Point position)
    {
        if (!OrnamentCache.TryGetValue(file, out var paths))
        {
            using var stream = AssetLoader.Open(new Uri($"avares://StopwatchOverlay.Desktop/Assets/Ornaments/Overlay/{file}.svg"));
            var xml = XDocument.Load(stream);
            paths = xml.Descendants().Where(e => e.Name.LocalName == "path")
                .Select(e => (Geometry.Parse(e.Attribute("d")!.Value), e.Attribute("stroke") == null)).ToArray();
            OrnamentCache[file] = paths;
        }
        using (dc.PushTransform(Matrix.CreateTranslation(position.X, position.Y)))
            foreach (var (geometry, fill) in paths)
                dc.DrawGeometry(fill ? BrushToken("OverlayOrnamentBrush") : null,
                    fill ? null : new Pen(BrushToken("OverlayOrnamentBrush"), 1, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geometry);
    }
    private void DrawOrnaments(DrawingContext dc)
    {
        if (EffectiveOverlayTheme is OverlayThemeCatalog.AcanthusDarkElegantOlive or OverlayThemeCatalog.AcanthusLight)
        {
            Ornament(dc, "elegant-olive-corner-left", new Point(8, 7));
            Ornament(dc, "elegant-olive-corner-right", new Point(Bounds.Width - 26, 7));
        }
        else if (EffectiveOverlayTheme == OverlayThemeCatalog.AcanthusDarkGoldCrest)
        {
            double y = Bounds.Height - 17;
            var pen = new Pen(BrushToken("OverlayOrnamentBrush"), .75);
            dc.DrawLine(pen, new Point(24, y), new Point(Bounds.Width / 2 - 16, y));
            dc.DrawLine(pen, new Point(Bounds.Width / 2 + 16, y), new Point(Bounds.Width - 24, y));
            Ornament(dc, "gold-crest", new Point(Bounds.Width / 2 - 9, Bounds.Height - 26));
        }
        else if (EffectiveOverlayTheme == OverlayThemeCatalog.AcanthusDarkMinimalBotanical)
            Ornament(dc, "minimal-botanical-leaf", new Point(Bounds.Width / 2 - 7, Bounds.Height - 7));
    }
}
