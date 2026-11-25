using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class TimerEditorWindow
{
    private TextBlock? _segmentDuration;
    private void RefreshSegmentDuration()
    {
        if (_segmentDuration == null || _latestStartLocal is not DateTime original) return;
        if (!TimeOnly.TryParse(StartTimeBox.Text, out var time)) { _segmentDuration.Text = "—"; return; }
        var start = original.Date.Add(time.ToTimeSpan());
        if (start > DateTime.Now && start.Date > DateTime.MinValue.Date) start = start.AddDays(-1);
        _segmentDuration.Text = SegmentDuration((_latestEndLocal ?? DateTime.Now) - start);
    }
    static readonly IBrush Navy=Brush.Parse("#102B3B"),Muted=Brush.Parse("#655238"),Teal=Brush.Parse("#21665B"),Rule=Brush.Parse("#CCAD79");
    static readonly FontFamily Body=StopwatchTypography.Resolve("modern-deck").NameFont;
    static TextBlock Text(string value,double size,IBrush? ink=null,bool strong=false) => new() {
        Text=value,FontFamily=Body,Foreground=ink??Navy,FontSize=size,FontWeight=strong?FontWeight.SemiBold:FontWeight.Normal,
        TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center };
    static Control Column(string title,Control value) => new StackPanel{Spacing=4,Children={Text(title,11,Muted),value}};
    static string SegmentDuration(TimeSpan t) => TimerEditorWindow.FormatDuration(t < TimeSpan.Zero ? TimeSpan.Zero : t);

    void BuildRecordedSegments(StackPanel section,TextBox start,TextBlock detail,Expander earlier,
        IReadOnlyList<ProjectWorkIntervalView> records,bool pixel)
    {



        section.Spacing=8;
        section.Children.Add(Text("Recorded segments",18,strong:true));
        var last=records.LastOrDefault();
        var contents=new StackPanel{Spacing=9};
        if(last==null)
        {
            contents.Children.Add(Text("No recorded segments yet",14,strong:true));
            var row=new Grid{ColumnDefinitions=new("144,16,*")};
            row.Children.Add(Column("Estimated start",start));
            var hint=Text("Editing the estimated start adjusts the timer’s elapsed time.",12,Muted);
            Grid.SetColumn(hint,2);row.Children.Add(hint);contents.Children.Add(row);
        }
        else
        {
            bool active=last.EndUtc==null;
            var status=new StackPanel{Orientation=Orientation.Horizontal,Spacing=6,VerticalAlignment=VerticalAlignment.Center,
                Children={new Border{Background=active?Teal:Muted,CornerRadius=new(pixel?0:4),Width=7,Height=7,VerticalAlignment=VerticalAlignment.Center},Text(active?"Running":"Completed",12,active?Teal:Muted,true)}};
            var title=new Grid{ColumnDefinitions=new("*,Auto"),Children={Text($"Segment {records.Count} of {records.Count}",15,strong:true),status}};
            Grid.SetColumn(status,1);
            var end=last.EndUtc?.ToLocalTime()??DateTime.Now;
            var duration=Text(SegmentDuration(end-last.StartUtc.ToLocalTime()),18,Navy,true);
            var values=new Grid{ColumnDefinitions=new("1.25*,18,*,12,*")};
            values.Children.Add(Column("Start time",start));
            var ended=Column("End time",Text(active?"Now":end.ToString("HH:mm:ss"),17,strong:true));
            var took=Column("Duration",duration);
            Grid.SetColumn(ended,2);Grid.SetColumn(took,4);values.Children.Add(ended);values.Children.Add(took);
            contents.Children.Add(title);contents.Children.Add(values);contents.Children.Add(Text("Editing the start adjusts elapsed time.",11,Muted));
            _segmentDuration = duration;
            start.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) RefreshSegmentDuration(); };
        }
        detail.FontSize=12; detail.TextWrapping=TextWrapping.Wrap;
        void UpdateError()=>detail.IsVisible=start.Classes.Contains("invalidTime");
        start.Classes.CollectionChanged+=(_,_)=>UpdateError();UpdateError();contents.Children.Add(detail);
        section.Children.Add(new TodoFrame{Pixel=pixel,Background=Brush.Parse("#F6E7C7"),BorderBrush=Rule,
            BorderThickness=new(1),CornerRadius=new(5),Padding=new(13,10),Child=contents});
        if(records.Count>1)
        {
            var list=new StackPanel();
            for(int i=records.Count-2;i>=0;i--)
            {
                var r=records[i];var end=r.EndUtc ?? DateTime.UtcNow;
                var line=new Grid{ColumnDefinitions=new("82,*,Auto"),Children={
                    Text($"Segment {i+1}",12,strong:true),Text($"{r.StartUtc.ToLocalTime():HH:mm:ss} – {end.ToLocalTime():HH:mm:ss}",13),
                    Text(SegmentDuration(end-r.StartUtc),13,strong:true)}};
                Grid.SetColumn(line.Children[1],1);Grid.SetColumn(line.Children[2],2);
                list.Children.Add(new Border{BorderBrush=Rule,BorderThickness=new(0,0,0,1),Padding=new(3,5),Child=line});
            }
            earlier.Margin=default;earlier.IsVisible=true;earlier.Header=Text($"Earlier segments ({records.Count-1})",13,strong:true);
            earlier.Content = list;
            earlier.IsExpanded = false;
            section.Children.Add(earlier);
        }
    }
}
