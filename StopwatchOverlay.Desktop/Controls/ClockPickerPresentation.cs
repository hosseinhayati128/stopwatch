using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace StopwatchOverlay.Desktop.Controls;

/// <summary>Immutable per-open values; the picker does not run another timer/render loop.</summary>
internal sealed record ClockPickerItem(int Number,string Name,string Time,string Mode,string State,
    bool Running,bool Current,string Details,bool RightToLeft)
{
    public override string ToString()=>Details;
    internal static ClockPickerItem Create(TimerSession timer,int index,TimerSession? active,DateTime now)
    {
        string mode=timer.Mode switch{1=>"Clock",2=>"Countdown",3=>"Timecode",_=>"Stopwatch"};
        string state=timer.Mode==1?"Live":timer.IsRunning?"Running":"Paused";
        string time=ReadTime(timer,now);
        string details=$"{index+1}. {timer.DisplayName} · {time} · {state} · {mode}"
            +(timer==active?" · Active":"")+(!string.IsNullOrWhiteSpace(timer.Category)?$" · [{timer.Category}]":"")
            +(timer.IsSeparated?" · Separated":"");
        bool rtl=false;
        foreach(char c in timer.DisplayName)
        {
            if(c is >= '\u0590' and <= '\u08FF' or >= '\uFB1D' and <= '\uFEFF'){rtl=true;break;}
            if(char.IsLetter(c))break;
        }
        return new(index+1,timer.DisplayName,time,mode,state,timer.IsRunning,timer==active,details,rtl);
    }

    internal static string ReadTime(TimerSession timer, DateTime now)
    {
        TimeSpan value = timer.Mode == 1 ? now.TimeOfDay : timer.Mode == 2 ? timer.CountdownRemaining : timer.Elapsed;
        return FormatTime(value);
    }

    internal static string FormatTime(TimeSpan value)
    {
        // Divide before Abs so even TimeSpan.MinValue is representable.
        long seconds = Math.Abs(value.Ticks / TimeSpan.TicksPerSecond);
        return FormattableString.Invariant($"{(value.Ticks < 0 ? "-" : "")}{seconds / 3600:00}:{seconds / 60 % 60:00}:{seconds % 60:00}");
    }
}

internal static class ClockPickerPresentation
{
    internal static readonly IBrush Navy=Brush.Parse("#102B3B"),Cream=Brush.Parse("#F6E8C9"),
        Muted=Brush.Parse("#655539"),Gold=Brush.Parse("#AF7C29");
    internal static readonly FontFamily Body=StopwatchTypography.Resolve("modern-deck").NameFont;
    private static readonly StopwatchTypographyProfile Navigator=StopwatchTypography.Resolve("navigator-bold");

    internal static readonly IBrush Rule = Brush.Parse("#40916A36");
    private static readonly IBrush Paused = Brush.Parse("#966328");
    private static readonly IBrush Live = Brush.Parse("#1C655D");

    internal static Control CreateRow(ClockPickerItem item, bool pixel)
    {
        var row = new Grid
        {
            Name = "ClockPickerRow", ColumnDefinitions = new("22,10,*,10,Auto"),
            FlowDirection = FlowDirection.LeftToRight, Margin = new Thickness(8, 7)
        };
        AutomationProperties.SetName(row, item.Details);
        ToolTip.SetTip(row, item.Details);

        var number = Label(item.Number.ToString(CultureInfo.InvariantCulture), 13, Muted,
            item.Number <= 9 ? FontWeight.Bold : FontWeight.Normal);
        number.Name = "ClockPickerNumber";
        if (pixel) number.FontFamily = StopwatchTypography.PixelNumbers;
        number.HorizontalAlignment = HorizontalAlignment.Center;
        row.Children.Add(number);

        var nameFont = item.RightToLeft ? new FontFamily("Segoe UI") : pixel ? Body : Navigator.NameFont;
        var name = Label(item.Name, pixel ? 15 : 18, Navy, FontWeight.Bold, nameFont);
        name.Name = "ClockPickerName";
        name.FlowDirection = item.RightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        name.TextWrapping = TextWrapping.Wrap;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        name.MaxLines = 2;

        var detail = new Grid { ColumnDefinitions = new("6,5,*") };
        detail.Children.Add(new Border
        {
            Width = 6, Height = 6, CornerRadius = new(pixel ? 0 : 3),
            Background = item.State == "Paused" ? Paused : Live, VerticalAlignment = VerticalAlignment.Center
        });
        var status = Label($"{item.Mode} · {item.State}", 11, Muted, FontWeight.Medium);
        status.Name = "ClockPickerMode";
        status.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetColumn(status, 2);
        detail.Children.Add(status);
        var info = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { name, detail } };
        Grid.SetColumn(info, 2);
        row.Children.Add(info);

        var time = CreateTime(item.Time, pixel);
        AutomationProperties.SetName(time, item.Time);
        time.HorizontalAlignment = HorizontalAlignment.Right;
        var values = new StackPanel
        {
            Spacing = 3, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right,
            Children = { time }
        };
        if (item.Current)
        {
            var active = Label("Active", 11, Live, FontWeight.Bold);
            active.Name = "ClockPickerActive";
            active.HorizontalAlignment = HorizontalAlignment.Right;
            values.Children.Add(active);
        }
        Grid.SetColumn(values, 4);
        row.Children.Add(values);
        var entry = new Grid { Children = { row } };
        if (item.Number > 1)
            entry.Children.Add(new Border { Height = 1, Background = Rule, IsHitTestVisible = false,
                VerticalAlignment = VerticalAlignment.Top });
        return entry;
    }

    private static Control CreateTime(string text, bool pixel)
    {
        const double maxWidth = 124;
        if (pixel)
        {
            var digits = new ControllerPixelDigits { Name = "ClockPickerTime", Text = text, FontSize = 26, Foreground = Navy };
            digits.Measure(Size.Infinity);
            if (digits.DesiredSize.Width > maxWidth)
                digits.FontSize *= (maxWidth - 6) / (digits.DesiredSize.Width - 6);
            return digits;
        }
        var size = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(Navigator.TimerFont, FontStyle.Normal, FontWeight.Black), 24, Navy);
        var label = Label(text, 24 * Math.Min(1, maxWidth / size.WidthIncludingTrailingWhitespace), Navy, FontWeight.Black, Navigator.TimerFont);
        label.Name = "ClockPickerTime";
        return label;
    }

    private static TextBlock Label(string text, double size, IBrush ink, FontWeight weight, FontFamily? font = null) =>
        new() { Text = text, FontSize = size, Foreground = ink, FontWeight = weight, FontFamily = font ?? Body,
            VerticalAlignment = VerticalAlignment.Center };
}
