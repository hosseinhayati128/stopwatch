using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using StopwatchOverlay.Desktop.Controls;
using Calendar = Avalonia.Controls.Calendar;

namespace StopwatchOverlay.Desktop.Views;

public partial class ProjectRecordEditorWindow
{
    private static readonly FontFamily CalendarLettering =
        new("avares://StopwatchOverlay.Desktop/Assets/Fonts/Stopwatch/#Stopwatch Calendar");

    private void ConfigureRecordCalendar(CalendarDatePicker picker)
    {
        picker.IsTodayHighlighted = true;
        picker.TemplateApplied += (_, args) =>
        {
            var toggle = args.NameScope.Get<Button>("PART_Button");
            bool pixel = DesktopPageAppearance.UsesPixelController;
            toggle.Theme = (ControlTheme)this.FindResource("RecordCalendarToggle")!;
            toggle.CornerRadius = new CornerRadius(pixel ? 0 : 3);
            toggle.Resources["RecordCalendarIconStroke"] = pixel ? 2d : 1.6;
            AutomationProperties.SetName(toggle, picker == StartDatePicker ? "Open start date calendar" : "Open end date calendar");
            var calendar = args.NameScope.Get<Calendar>("PART_Calendar");
            var popup = args.NameScope.Get<Popup>("PART_Popup");
            // Keep the date picker's actual calendar and its native input handlers.
            popup.Child = null;
            var wrapper = new LayoutTransformControl { Child = calendar };
            popup.Child = wrapper;
            calendar.Theme = (ControlTheme)this.FindResource("RecordCalendar")!;
            calendar.FontFamily = ClockPickerPresentation.Body;
            // CalendarDatePicker accepts or restores the value before this bubbling handler.
            // Consume its dismissal key so the record editor does not also close.
            calendar.KeyDown += (_, key) =>
            {
                if (!picker.IsDropDownOpen && key.Key is Key.Escape or Key.Enter or Key.Space)
                    key.Handled = true;
            };
            popup.Opened += (_, _) =>
            {
                double scale = DesktopPageAppearance.Scale(this);
                wrapper.LayoutTransform = new ScaleTransform(scale, scale);
                SetCalendarPalette(calendar, DesktopPageAppearance.UsesPixelController, scale);
                calendar.ApplyTemplate();
                UpdateCalendarWeeks(calendar);
            };
            calendar.DisplayDateChanged += (_, _) => UpdateCalendarWeeks(calendar);
            calendar.DisplayModeChanged += (_, _) => UpdateCalendarWeeks(calendar);
            calendar.TemplateApplied += (_, template) =>
            {
                var item = template.NameScope.Get<CalendarItem>("PART_CalendarItem");
                item.TemplateApplied += (_, _) => UpdateCalendarWeeks(calendar);
            };
        };
    }

    private static void SetCalendarPalette(Calendar calendar, bool pixel, double scale)
    {
        var resources = calendar.Resources;
        // One letter pixel is an eighth of an em. Use integral device pixels at every zoom.
        double deviceScale = scale * (TopLevel.GetTopLevel(calendar)?.RenderScaling ?? 1);
        double pixelSize = 8 * Math.Max(1, Math.Round(2.4 * deviceScale)) / deviceScale;
        resources["DateDigitFont"] = pixel ? StopwatchTypography.PixelNumbers : ClockPickerPresentation.Body;
        resources["DateHeadingFont"] = pixel ? CalendarLettering : ClockPickerPresentation.Body;
        resources["DateHeadingWeight"] = pixel ? FontWeight.Normal : FontWeight.SemiBold;
        resources["DateDigitSize"] = pixel ? pixelSize : 15d;
        resources["DateHeadingSize"] = pixel ? pixelSize : 19d;
        resources["TodayMarkWidth"] = pixel ? 8d : 5d;
        resources["DateCellRadius"] = new CornerRadius(pixel ? 0 : 5);
        resources["DateFrameRadius"] = new CornerRadius(pixel ? 0 : 8);
        resources["DateFrameThickness"] = new Thickness(pixel ? 2 : 1);
        resources["DateFrameBrush"] = pixel ? ClockPickerPresentation.Navy : Brush.Parse("#A77A38");
        resources["DateBackArrow"] = Geometry.Parse(pixel
            ? "M9,1 H6 V3 H4 V5 H2 V7 H4 V9 H6 V11 H9 V9 H7 V7 H5 V5 H7 V3 H9 Z"
            : "M8,0 L2,6 L8,12 L9.5,10.5 L5,6 L9.5,1.5 Z");
        resources["DateForwardArrow"] = Geometry.Parse(pixel
            ? "M2,1 H5 V3 H7 V5 H9 V7 H7 V9 H5 V11 H2 V9 H4 V7 H6 V5 H4 V3 H2 Z"
            : "M3,0 L9,6 L3,12 L1.5,10.5 L6,6 L1.5,1.5 Z");
    }

    private static void UpdateCalendarWeeks(Calendar calendar)
    {
        var monthGrid = calendar.GetVisualDescendants().OfType<Grid>().FirstOrDefault(g => g.Name == "PART_MonthView");
        if (monthGrid == null || calendar.DisplayMode != CalendarMode.Month) return;
        var month = calendar.DisplayDate;
        var first = new DateTime(month.Year, month.Month, 1);
        int offset = ((int)first.DayOfWeek - (int)calendar.FirstDayOfWeek + 7) % 7;
        // CalendarItem puts a whole preceding week in row one when the month starts on the first weekday.
        int firstRow = offset == 0 && first != DateTime.MinValue ? 2 : 1;
        int rows = (offset + DateTime.DaysInMonth(month.Year, month.Month) + 6) / 7;
        for (int r = 1; r < monthGrid.RowDefinitions.Count; r++)
            monthGrid.RowDefinitions[r].Height = new GridLength(r >= firstRow && r < firstRow + rows ? 33 : 0);
        var format = CultureInfo.CurrentCulture.DateTimeFormat;
        foreach (var cell in monthGrid.Children)
        {
            int r = Grid.GetRow(cell);
            if (cell is TextBlock weekday && r == 0)
            {
                var day = (DayOfWeek)(((int)calendar.FirstDayOfWeek + Grid.GetColumn(cell)) % 7);
                var name = format.GetAbbreviatedDayName(day);
                weekday.Text = StringInfo.ParseCombiningCharacters(name).Length <= 2 ? name : new StringInfo(name).SubstringByTextElements(0, 2);
            }
            else if (cell is CalendarDayButton)
                cell.IsVisible = r >= firstRow && r < firstRow + rows;
        }
    }
}
