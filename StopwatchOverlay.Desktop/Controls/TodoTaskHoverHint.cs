using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using IconPath = Avalonia.Controls.Shapes.Path;

namespace StopwatchOverlay.Desktop.Controls;

internal static class TodoTaskHoverHint
{
    private readonly record struct HintLine(string Icon, string Lead, string Rest);

    private static HintLine Hold(bool pixel, bool completed, bool manual = false) => new(
        pixel ? "M5,1.5 H11 V3 H13 V12 H11 V14.5 H5 V13 H3 V4 H5 Z M8,2 V6" :
            "M8,1.5 C4.8,1.5 3.5,3.4 3.5,6 V9.5 C3.5,12.6 5.3,14.5 8,14.5 C10.7,14.5 12.5,12.6 12.5,9.5 V6 C12.5,3.4 11.2,1.5 8,1.5 Z M8,2 V6",
        manual ? "Hold checkbox for 1 second" : "Hold for 1 second", completed ? " to reopen" : " to complete");

    internal static void Attach(Grid card, HoldToCompleteCheckBox row, AppSettings settings,
        bool completed, bool inProgress, bool manual, string? date)
    {
        HintLine? toggle = completed ? null : new HintLine(
            "M5,5 L6.5,14 L9,11 L12,14 L14,12 L11,9 L14,8 Z M2,2 L3,3 M5,0.8 V2 M0.8,5 H2",
            "Double-click", inProgress ? " to clear in progress" : " to mark in progress");
        Attach(row, settings, top =>
        {
            // Include the state and deadline below the title, leaving expanded subtasks out of the anchor.
            double height = card.RowDefinitions.Take(3).Sum(r => r.ActualHeight);
            return new Rect(card.TranslatePoint(default, top) ?? default, new Size(card.Bounds.Width, height));
        }, Hold(settings.TodoAppearance.IsPixel, completed, manual), toggle, date);
    }

    internal static void AttachSubtask(HoldToCompleteCheckBox row, AppSettings settings) =>
        Attach(row, settings, top => DetailBounds(row, top), Hold(settings.TodoAppearance.IsPixel, row.IsChecked == true));

    internal static void AttachNote(TextBlock text, AppSettings settings, bool hasLinks)
    {
        // The space between wrapped lines must receive pointer events too.
        text.Background = Brushes.Transparent;
        bool pixel = settings.TodoAppearance.IsPixel;
        var menu = new HintLine(pixel ? "M4,3 H12 V4.5 H4 Z M4,7 H12 V8.5 H4 Z M4,11 H12 V12.5 H4 Z" :
            "M3,4 H13 M3,8 H13 M3,12 H13", "Right-click", " for note actions");
        var link = new HintLine(pixel ? "M7,4 L9,2 H12 L14,4 V7 L11,10 H9 M9,12 L7,14 H4 L2,12 V9 L5,6 H7 M5.5,10.5 L10.5,5.5" :
            "M6.5,5 L8.8,2.7 A3.3,3.3 0 0 1 13.5,7.4 L10.9,10 A3.3,3.3 0 0 1 7,10.4 M9.5,11 L7.2,13.3 A3.3,3.3 0 0 1 2.5,8.6 L5.1,6 A3.3,3.3 0 0 1 9,5.6 M6,10 L10,6",
            "Click", text.Inlines?.Count > 1 ? " a link to open it" : " to open the link");
        Attach(text, settings, top => DetailBounds(text, top), hasLinks ? link : menu, hasLinks ? menu : null);
    }

    private static Rect DetailBounds(Control row, TopLevel top)
    {
        Control frame = row.FindAncestorOfType<TodoFrame>() ?? row;
        return new Rect((frame.TranslatePoint(default, top) ?? default).X, (row.TranslatePoint(default, top) ?? default).Y,
            frame.Bounds.Width, row.Bounds.Height);
    }

    private static void Attach(Control row, AppSettings settings, Func<TopLevel, Rect> getAnchor, HintLine first, HintLine? second = null, string? date = null)
    {
        bool pixel = settings.TodoAppearance.IsPixel;
        var tip = new ToolTip();
        double zoom = 0;
        ToolTip.SetTip(row, tip);
        ToolTip.AddToolTipOpeningHandler(row, (_, e) =>
        {
            if (!ReferenceEquals(e.Source, row)) return;
            double scale = AppUiScale.Normalize(settings.UiScalePercent) / 100;
            if (scale == zoom) return;
            zoom = scale;
            tip.MaxWidth = Math.Max(280, 330 * scale);
            tip.Template = new FuncControlTemplate<ToolTip>((_, _) => BuildFrame(first, second, date, pixel, scale));
        });
        ToolTip.SetPlacement(row, PlacementMode.Custom);
        ToolTip.SetCustomPopupPlacementCallback(row, placement =>
        {
            double gap = 7 * zoom;
            placement.AnchorRectangle = getAnchor(TopLevel.GetTopLevel(row)!).Inflate(new Thickness(gap, 0));
            placement.Anchor = PopupAnchor.Right;
            placement.Gravity = PopupGravity.Right;
            placement.Offset = default;
            placement.ConstraintAdjustment = PopupPositionerConstraintAdjustment.FlipX |
                PopupPositionerConstraintAdjustment.SlideX | PopupPositionerConstraintAdjustment.SlideY;
        });
        AutomationProperties.SetHelpText(row, first.Lead + first.Rest + "." +
            (second is { } next ? " " + next.Lead + next.Rest + "." : "") +
            (date == null ? "" : " " + date));
    }

    private static TodoFrame BuildFrame(HintLine first, HintLine? second, string? date, bool pixel, double zoom)
    {
        var body = new StackPanel { Spacing = 6 * zoom };
        void Line(string icon, string lead, string rest, bool isDate = false)
        {
            var accent = isDate ? TodoVisuals.FineGold : Brush.Parse("#C6D9DB");
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            var canvas = new Canvas { Width = 16, Height = 16 };
            canvas.Children.Add(new IconPath { Data = Geometry.Parse(icon), Stroke = accent,
                StrokeThickness = 1.4, StrokeLineCap = pixel ? PenLineCap.Square : PenLineCap.Round,
                StrokeJoin = pixel ? PenLineJoin.Miter : PenLineJoin.Round });
            double size = Math.Max(13, 15 * zoom);
            grid.Children.Add(new Viewbox { Child = canvas, Width = size, Height = size,
                Margin = new Thickness(0, 0, 8 * zoom, 0), VerticalAlignment = VerticalAlignment.Center });
            var label = new TextBlock { FontFamily = TodoVisuals.BodyFont, FontSize = Math.Max(12, (isDate ? 13 : 14) * zoom),
                Foreground = isDate ? accent : TodoVisuals.Ivory, TextWrapping = TextWrapping.Wrap, MaxWidth = Math.Max(210, 275 * zoom) };
            label.Inlines!.Add(new Run(lead) { FontWeight = isDate ? FontWeight.Normal : FontWeight.SemiBold });
            if (rest.Length > 0) label.Inlines.Add(new Run(rest));
            Grid.SetColumn(label, 1);
            grid.Children.Add(label);
            body.Children.Add(grid);
        }
        Line(first.Icon, first.Lead, first.Rest);
        if (second is { } next) Line(next.Icon, next.Lead, next.Rest);
        if (date != null)
        {
            body.Children.Add(new Border { Height = 1, Background = Brush.Parse("#49616A") });
            Line(pixel ? "M5,1.5 H11 V3 H13 V5 H14.5 V11 H13 V13 H11 V14.5 H5 V13 H3 V11 H1.5 V5 H3 V3 H5 Z M8,4 V8 H11" :
                "M8,1.5 A6.5,6.5 0 1 1 8,14.5 A6.5,6.5 0 1 1 8,1.5 Z M8,4 V8 H11", date, "", true);
        }
        return new TodoFrame { Child = body, Pixel = pixel, CornerRadius = new CornerRadius(6 * zoom),
            Padding = new Thickness(12 * zoom, 9 * zoom), BorderThickness = new Thickness(pixel ? Math.Max(1.5, 2 * zoom) : 1),
            Background = TodoVisuals.Navy, BorderBrush = TodoVisuals.Gold };
    }
}
