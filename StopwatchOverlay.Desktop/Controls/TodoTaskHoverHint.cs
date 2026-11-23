using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using IconPath = Avalonia.Controls.Shapes.Path;

namespace StopwatchOverlay.Desktop.Controls;

internal static class TodoTaskHoverHint
{
    private readonly record struct HintLine(string Icon, string Lead, string Rest);

    private sealed class HintPopup(AppSettings settings, HintLine[] lines, string? date) : ToolTip
    {
        public double Zoom { get; private set; }
        private bool _pixel;
        protected override Type StyleKeyOverride => typeof(ToolTip);

        public void Prepare()
        {
            double scale = AppUiScale.Normalize(settings.UiScalePercent) / 100;
            bool pixel = settings.TodoAppearance.IsPixel;
            if (scale == Zoom && pixel == _pixel) return;
            Zoom = scale;
            _pixel = pixel;
            MaxWidth = Math.Max(280, 330 * scale);
            Template = new FuncControlTemplate<ToolTip>((_, _) => BuildFrame(lines, date, pixel, scale));
        }
    }

    private static HintLine Click(bool pixel, string action) => new(
        pixel ? "M5,1.5 H11 V3 H13 V12 H11 V14.5 H5 V13 H3 V4 H5 Z M8,2 V6" :
            "M8,1.5 C5,1.5 3.5,3.5 3.5,6 V9.5 C3.5,12.7 5.3,14.5 8,14.5 C10.7,14.5 12.5,12.7 12.5,9.5 V6 C12.5,3.5 11,1.5 8,1.5 Z M8,2 V6",
        "Click", action);

    internal static void SetLauncher(Button button, AppSettings settings, bool expanded, bool themed)
    {
        if (ToolTip.GetTip(button) is HintPopup)
        {
            ToolTip.SetIsOpen(button, false);
            ToolTip.RemoveToolTipOpeningHandler(button, PrepareOnOpening);
        }
        if (!themed)
        {
            button.ClearValue(ToolTip.PlacementProperty);
            button.ClearValue(ToolTip.CustomPopupPlacementCallbackProperty);
            button.ClearValue(AutomationProperties.HelpTextProperty);
            ToolTip.SetTip(button, expanded ? "Click to collapse todos · Drag to move" : "Click to expand todos · Drag to move");
            return;
        }
        Attach(button, settings, top => BoundsIn(button, top), [
            Click(settings.TodoAppearance.IsPixel, expanded ? " to collapse todos" : " to expand todos"),
            new("M8,1.5 V14.5 M5.5,4 L8,1.5 L10.5,4 M5.5,12 L8,14.5 L10.5,12 M1.5,8 H14.5 M4,5.5 L1.5,8 L4,10.5 M12,5.5 L14.5,8 L12,10.5", "Drag", " to move the overlay")
        ], edge: expanded ? PopupAnchor.TopLeft : PopupAnchor.Right);
    }

    internal static void AttachPriority(Button badge, Grid card, AppSettings settings, int priority)
    {
        Attach(badge, settings, top =>
        {
            var button = BoundsIn(badge, top);
            return new Rect(button.X, button.Y, button.Width, TaskBounds(card, top).Bottom - button.Y);
        }, [new("M2,2 H8.5 L14.5,8 L8,14.5 L2,8.5 Z M5,5 H5.7", $"Priority {priority}", " · 1 is highest"),
            Click(settings.TodoAppearance.IsPixel, " to change priority")], edge: PopupAnchor.BottomLeft);
    }

    internal static void AttachDragHandle(Button handle, AppSettings settings) =>
        Attach(handle, settings, top => BoundsIn(handle, top), [
            new("M5,2.5 H6.5 M9.5,2.5 H11 M5,7.5 H6.5 M9.5,7.5 H11 M5,12.5 H6.5 M9.5,12.5 H11", "Drag", " handle or title to reorder"),
            new("M2.5,2 H13.5 M2.5,14 H13.5 M8,4 V12 M5.5,6.5 L8,4 L10.5,6.5 M5.5,9.5 L8,12 L10.5,9.5", "Hold", " near a list edge to scroll"),
            new("M1.5,4 H14.5 V12 H1.5 Z M4,6 H5 M7,6 H8 M10,6 H11 M4,9 H5 M7,9 H11", "Focus the handle,", " then press ↑ or ↓")
        ], edge: PopupAnchor.Left);

    private static Rect BoundsIn(Control control, TopLevel top) => new(control.TranslatePoint(default, top) ?? default, control.Bounds.Size);

    // The main task ends after its state and deadline, above any expanded details.
    private static Rect TaskBounds(Grid card, TopLevel top) => new(card.TranslatePoint(default, top) ?? default,
        new Size(card.Bounds.Width, card.RowDefinitions.Take(3).Sum(r => r.ActualHeight)));

    private static HintLine Hold(bool pixel, bool completed, bool manual = false) => new(
        pixel ? "M5,1.5 H11 V3 H13 V12 H11 V14.5 H5 V13 H3 V4 H5 Z M8,2 V6" :
            "M8,1.5 C4.8,1.5 3.5,3.4 3.5,6 V9.5 C3.5,12.6 5.3,14.5 8,14.5 C10.7,14.5 12.5,12.6 12.5,9.5 V6 C12.5,3.4 11.2,1.5 8,1.5 Z M8,2 V6",
        manual ? "Hold checkbox for 1 second" : "Hold for 1 second", completed ? " to reopen" : " to complete");

    internal static void Attach(Grid card, HoldToCompleteCheckBox row, AppSettings settings,
        bool completed, bool inProgress, bool manual, string? date)
    {
        var hold = Hold(settings.TodoAppearance.IsPixel, completed, manual);
        Attach(row, settings, top => TaskBounds(card, top), completed ? [hold] : [hold, new(
            "M5,5 L6.5,14 L9,11 L12,14 L14,12 L11,9 L14,8 Z M2,2 L3,3 M5,0.8 V2 M0.8,5 H2",
            "Double-click", inProgress ? " to clear in progress" : " to mark in progress")], date);
    }

    internal static void AttachSubtask(HoldToCompleteCheckBox row, AppSettings settings) =>
        Attach(row, settings, top => DetailBounds(row, top), [Hold(settings.TodoAppearance.IsPixel, row.IsChecked == true)]);

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
        Attach(text, settings, top => DetailBounds(text, top), hasLinks ? [link, menu] : [menu]);
    }

    private static Rect DetailBounds(Control row, TopLevel top)
    {
        Control frame = row.FindAncestorOfType<TodoFrame>() ?? row;
        return new Rect((frame.TranslatePoint(default, top) ?? default).X, (row.TranslatePoint(default, top) ?? default).Y,
            frame.Bounds.Width, row.Bounds.Height);
    }

    private static void PrepareOnOpening(object? sender, CancelRoutedEventArgs e)
    {
        if (sender is Control owner && ReferenceEquals(e.Source, owner) && ToolTip.GetTip(owner) is HintPopup tip)
            tip.Prepare();
    }

    private static void Attach(Control row, AppSettings settings, Func<TopLevel, Rect> getAnchor, HintLine[] lines,
        string? date = null, PopupAnchor edge = PopupAnchor.Right)
    {
        var tip = new HintPopup(settings, lines, date);
        ToolTip.SetTip(row, tip);
        ToolTip.AddToolTipOpeningHandler(row, PrepareOnOpening);
        ToolTip.SetPlacement(row, PlacementMode.Custom);
        ToolTip.SetCustomPopupPlacementCallback(row, placement =>
        {
            double gap = 7 * tip.Zoom;
            bool vertical = edge is PopupAnchor.TopLeft or PopupAnchor.BottomLeft;
            placement.AnchorRectangle = getAnchor(TopLevel.GetTopLevel(row)!).Inflate(vertical ? new Thickness(0, gap) : new Thickness(gap, 0));
            placement.Anchor = edge;
            placement.Gravity = edge switch
            {
                PopupAnchor.TopLeft => PopupGravity.TopRight,
                PopupAnchor.BottomLeft => PopupGravity.BottomRight,
                PopupAnchor.Left => PopupGravity.Left,
                _ => PopupGravity.Right
            };
            placement.Offset = default;
            placement.ConstraintAdjustment = PopupPositionerConstraintAdjustment.FlipX | PopupPositionerConstraintAdjustment.FlipY |
                PopupPositionerConstraintAdjustment.SlideX | PopupPositionerConstraintAdjustment.SlideY;
        });
        AutomationProperties.SetHelpText(row, string.Join(". ", lines.Select(line => line.Lead + line.Rest)) + "." +
            (date == null ? "" : " " + date));
    }

    private static TodoFrame BuildFrame(HintLine[] lines, string? date, bool pixel, double zoom)
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
        foreach (var line in lines) Line(line.Icon, line.Lead, line.Rest);
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
