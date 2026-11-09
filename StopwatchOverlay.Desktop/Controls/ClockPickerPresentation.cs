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
        TimeSpan value=timer.Mode==1?now.TimeOfDay:timer.Mode==2?timer.CountdownRemaining:timer.Elapsed;
        // Abs after dividing ticks also handles TimeSpan.MinValue safely.
        long seconds=Math.Abs(value.Ticks/TimeSpan.TicksPerSecond);
        string time=FormattableString.Invariant($"{(value.Ticks<0?"-":"")}{seconds/3600:00}:{seconds/60%60:00}:{seconds%60:00}");
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
}

internal static class ClockPickerPresentation
{
    internal static readonly IBrush Navy=Brush.Parse("#102B3B"),Cream=Brush.Parse("#F6E8C9"),
        Muted=Brush.Parse("#655539"),Gold=Brush.Parse("#AF7C29");
    internal static readonly FontFamily Body=StopwatchTypography.Resolve("modern-deck").NameFont;
    private static readonly StopwatchTypographyProfile Navigator=StopwatchTypography.Resolve("navigator-bold");

    internal static Control CreateRow(ClockPickerItem item)
    {
        var row=new Grid{Name="ClockPickerRow",ColumnDefinitions=new("24,10,10,8,*,Auto,8,Auto"),
            Height=41,FlowDirection=FlowDirection.LeftToRight};
        var rule=new Border{BorderBrush=Brush.Parse("#499A7B48"),BorderThickness=new(0,0,0,.7),IsHitTestVisible=false};
        Grid.SetColumnSpan(rule,8);row.Children.Add(rule);
        AutomationProperties.SetName(row,item.Details);ToolTip.SetTip(row,item.Details);
        var key=new Border{Name="ClockPickerKeycap",Width=22,Height=22,CornerRadius=new(3),
            Background=Navy,BorderBrush=Gold,BorderThickness=new(.8),IsVisible=item.Number<=9,
            Child=new TextBlock{Text=item.Number.ToString(CultureInfo.InvariantCulture),FontFamily=Body,FontSize=12,
                FontWeight=FontWeight.SemiBold,Foreground=Cream,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
        row.Children.Add(key);
        row.Children.Add(new TextBlock{Text=item.Number.ToString(CultureInfo.InvariantCulture),IsVisible=item.Number>9,
            FontFamily=Body,FontSize=11,Foreground=Muted,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center});
        var status=new ClockPickerStatus(item){Width=10,Height=12,VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false};
        Grid.SetColumn(status,2);row.Children.Add(status);
        var name=new TextBlock{Name="ClockPickerName",Text=item.Name,FontFamily=item.RightToLeft?new("Segoe UI"):Navigator.NameFont,
            FontWeight=item.RightToLeft?FontWeight.SemiBold:FontWeight.Bold,FontSize=item.RightToLeft?17:20,Foreground=Navy,
            TextTrimming=TextTrimming.CharacterEllipsis,TextWrapping=TextWrapping.NoWrap,MaxLines=1,
            FlowDirection=item.RightToLeft?FlowDirection.RightToLeft:FlowDirection.LeftToRight,VerticalAlignment=VerticalAlignment.Center};
        Grid.SetColumn(name,4);row.Children.Add(name);
        var badge=new Border{Name="ClockPickerActive",IsVisible=item.Current,Width=45,Height=16,Margin=new(6,0,0,0),
            Background=Navy,BorderBrush=Gold,BorderThickness=new(.8),CornerRadius=new(3),VerticalAlignment=VerticalAlignment.Center,
            Child=new TextBlock{Text="ACTIVE",FontFamily=Body,FontWeight=FontWeight.SemiBold,FontSize=8,Foreground=Cream,
                HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
        Grid.SetColumn(badge,5);row.Children.Add(badge);
        var measured=new FormattedText(item.Time,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,
            new Typeface(Navigator.TimerFont,FontStyle.Normal,FontWeight.Black),22,Navy);
        double timeSize=Math.Min(22,22*110/Math.Max(1,measured.WidthIncludingTrailingWhitespace));
        var time=new TextBlock{Name="ClockPickerTime",Text=item.Time,FontFamily=Navigator.TimerFont,FontWeight=FontWeight.Black,
            FontSize=timeSize,Foreground=Navy,TextWrapping=TextWrapping.NoWrap,FlowDirection=FlowDirection.LeftToRight,
            HorizontalAlignment=HorizontalAlignment.Right};
        var mode=new TextBlock{Name="ClockPickerMode",Text=item.Mode,FontFamily=Body,FontSize=9,Foreground=Muted,
            HorizontalAlignment=HorizontalAlignment.Right};
        var values=new StackPanel{Spacing=0,VerticalAlignment=VerticalAlignment.Center};values.Children.Add(time);values.Children.Add(mode);
        Grid.SetColumn(values,7);row.Children.Add(values);
        return row;
    }
}

internal sealed class ClockPickerStatus(ClockPickerItem item):Control
{
    private static readonly IBrush Teal=Brush.Parse("#087480"),Amber=Brush.Parse("#995817");
    public override void Render(DrawingContext dc)
    {
        var rect=new Rect(0,1,9,10);
        if(item.Mode=="Clock")
        {
            dc.DrawEllipse(null,new Pen(Teal,1.3),rect.Center,4.5,4.5);
            dc.DrawLine(new Pen(Teal,1.2),rect.Center,new Point(rect.Center.X,2));
            dc.DrawLine(new Pen(Teal,1.2),rect.Center,new Point(8,8));
        }
        else if(item.Running)
        {
            var triangle=new StreamGeometry();using(var path=triangle.Open())
            {path.BeginFigure(rect.TopLeft,true);path.LineTo(new Point(rect.Right,rect.Center.Y));path.LineTo(rect.BottomLeft);path.EndFigure(true);}
            dc.DrawGeometry(Teal,null,triangle);
        }
        else
        {dc.DrawRectangle(Amber,null,new Rect(0,1,3,10));dc.DrawRectangle(Amber,null,new Rect(6,1,3,10));}
    }
}
