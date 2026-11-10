using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;

// Design spike only: fixture values, drawn controls, no live timers or command handlers.
internal static class RunningClocksPreview
{
    private const string Output="design/one-piece-v2/running-clocks/native-layout-preview";
    private static readonly IBrush Navy=Brush.Parse("#102B3B"),Cream=Brush.Parse("#F6E8C9"),
        Muted=Brush.Parse("#655539"),Gold=Brush.Parse("#AF7C29"),Paper=Brush.Parse("#EBD3A5");
    private static readonly StopwatchTypographyProfile Type=StopwatchTypography.Resolve("navigator-bold");
    private static readonly FontFamily Body=StopwatchTypography.Resolve("modern-deck").NameFont;
    private sealed record Clock(string Name,string Time,bool Active=false,bool Hovered=false,bool Focused=false,bool Rtl=false);
    private static readonly Clock[] Clocks=[new("Illustration","00:44:27",true),new("Coding","01:12:08"),
        new("Reading","00:18:42"),new("Study","00:36:10"),new("Writing — chapter two","00:08:15")];

    internal static void Run()
    {
        Directory.CreateDirectory(Output);
        var board=new Canvas{Width=1200,Height=720,Background=Brush.Parse("#10232F")};
        Add(board,Label("RUNNING CLOCKS / COMPACT LAYOUT",28),34,26);
        Add(board,Label("One proposal · same frame and typography as the clock picker",16,Brush.Parse("#B6CAD1")),36,72);
        string[] titles=["Beneath the stopwatch","A longer list","Nothing running"];
        string[] captions=["Select a name to make that clock active.\nPause stays beside its time.",
            "Five rows fit before scrolling.\nThe heading and Hide stay in place.",
            "The panel shrinks when it is empty.\nHide dismisses only this panel."];
        var metrics=new List<object>();
        for(int i=0;i<3;i++)
        {
            double x=34+i*394;
            Add(board,Label(titles[i],18),x,129);
            int total=i==0?3:i==1?8:0;
            var rows=Clocks.Take(Math.Min(total,5)).ToArray();
            double top=174;
            if(i==0)
            {
                var face=new ThemedClockFace{Text=Clocks[0].Time,ProjectName=Clocks[0].Name};
                face.ApplySettings(new AppSettings{ThemeMode=AppThemeCatalog.Nika,TextSize=27,ShowRecIndicator=false,
                    StopwatchAppearance=new(){Background="navigator-parchment",BackgroundOpacity=70}});
                face.SetStopwatchPresentationState(new(true,true,false));face.Measure(Size.Infinity);
                Add(board,face,x,top);top+=face.DesiredSize.Height+8;
            }
            var surface=Panel(rows,total);double height=surface.Height;
            Save(surface,$"shelf-{(i==0?"three":i==1?"many":"empty")}.png");
            Add(board,surface,x,top);
            var caption=Label(captions[i],13,Brush.Parse("#B6CAD1"));caption.Width=350;caption.TextWrapping=TextWrapping.Wrap;
            Add(board,caption,x+2,top+height+19);
            metrics.Add(new{scenario=titles[i],total,visibleRows=rows.Length,width=350,height,
                contentWidth=surface.Child!.Bounds.Width,contentHeight=surface.Child.Bounds.Height});
        }
        Add(board,Label("Win+F2 → H   ·   Only running timers appear here",14,Brush.Parse("#E1C58E")),36,658);
        Add(board,Label("Layout preview · Native Avalonia rendering · Proposed controls are not connected to the app",11,Brush.Parse("#B6CAD1")),36,690);
        board.Measure(new Size(1200,720));board.Arrange(new Rect(0,0,1200,720));Save(board,"running-clocks-layout.png");
        File.WriteAllText(Path.Combine(Output,"verification.json"),JsonSerializer.Serialize(new{
            status="layout-proposal-not-production",renderer="Avalonia text and production StopwatchFrameSurface / ThemedClockFace",
            frame="engraved-filigree",artworkScale=.14,scenarios=metrics,rowHeight=44,maximumVisibleRows=5,newRuntimeArtwork=0,
            execution="Private desktop; no application lifetime, live settings, hotkeys or input injection."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Exported one native shelf layout in three conditions; all content-fit checks passed.");
    }

    internal static void RunStates()
    {
        Directory.CreateDirectory(Output);
        var board=new Canvas{Width=1200,Height=850,Background=Brush.Parse("#10232F")};
        Add(board,Label("RUNNING CLOCKS / STATES & READABILITY",28),34,26);
        Add(board,Label("Approved compact layout · proposed interaction states · native fixture renders",16,Brush.Parse("#B6CAD1")),36,72);
        Clock[][] samples=[
            [Clocks[0],Clocks[1] with{Hovered=true},Clocks[2] with{Focused=true}],
            [Clocks[0],Clocks[2]],
            [new("Illustration — The Grand Voyage","123:44:27.6",true),new("برنامه‌نویسی پروژه","01:12:08.4",Rtl:true),
             new("Tea countdown","00:08:15.2"),new("Editing / timecode","00:20:09.8"),new("Long-running timer","256204778:48:05.4")]];
        string[] titles=["Active, hover and focus","After pausing Coding","Names and timer formats"];
        string[] captions=["Illustration stays ACTIVE. Coding is hovered.\nThe outline gives Reading keyboard focus.",
            "Coding leaves this running-only list.\nIts saved time remains; other timers keep running.",
            "Long and Persian names keep their own space.\nComplete times fit, including tenths and long hours."];
        var metrics=new List<object>();
        for(int i=0;i<samples.Length;i++)
        {
            double x=34+i*394;var panel=Panel(samples[i],samples[i].Length);
            Add(board,Label(titles[i],18),x,129);Add(board,panel,x,174);
            var caption=Label(captions[i],13,Brush.Parse("#B6CAD1"));caption.Width=350;caption.TextWrapping=TextWrapping.Wrap;
            Add(board,caption,x+2,174+panel.Height+19);
            Save(panel,$"state-{i+1}.png");
            metrics.Add(new{scenario=titles[i],rows=samples[i],width=panel.Width,height=panel.Height});
        }
        Add(board,Label("Pause",16),36,644);Add(board,Label("Hide",16),430,644);
        Add(board,new ButtonStates{Width=350,Height=76},36,674);
        Add(board,new ButtonStates(true){Width=350,Height=76},430,674);
        var keys=Label("Tab moves between controls.\nEnter / Space uses the focused control.\nHide leaves every timer running.",13,Brush.Parse("#B6CAD1"));
        keys.Width=350;keys.TextWrapping=TextWrapping.Wrap;Add(board,keys,824,674);
        Add(board,Label("ACTIVE never moves just because you hover or focus another clock.",14,Brush.Parse("#E1C58E")),36,782);
        Add(board,Label("Simulated states and pause result · Live input and timer changes are not connected in this preview",11,Brush.Parse("#B6CAD1")),36,818);
        board.Measure(new Size(1200,850));board.Arrange(new Rect(0,0,1200,850));Save(board,"running-clocks-states.png");
        File.WriteAllText(Path.Combine(Output,"state-verification.json"),JsonSerializer.Serialize(new{
            status="state-proposal-not-production",scenarios=metrics,rowHeight=44,buttonStates=new[]{"normal","hover","pressed","keyboard focus"},
            checks="Shared-frame content fit, reserved name space, full time width and minimum time font size; visual inspection still required.",
            execution="Private desktop; simulated input states, no user data or application lifecycle."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Exported state, simulated pause-result, RTL and long-time fixtures with content-fit assertions.");
    }

    private static StopwatchFrameSurface Panel(Clock[] rows,int total)
    {
        double height=total==0?194:rows.Length*44+160;
        var content=new ShelfContent(rows,total);
        var surface=new StopwatchFrameSurface{Width=350,Height=height,ArtworkScale=.14,Padding=new Thickness(7),Background=Paper,Child=content};
        surface.Measure(new Size(350,height));surface.Arrange(new Rect(0,0,350,height));
        if(content.Bounds.Width<260||content.Bounds.Height<98+rows.Length*44)
            throw new Exception("The shelf content does not fit inside the shared frame: "+content.Bounds);
        return surface;
    }

    private sealed class ShelfContent(Clock[] rows,int total):Control
    {
        public override void Render(DrawingContext dc)
        {
            double width=Bounds.Width;
            Text(dc,"Running clocks",Type.NameFont,FontWeight.Bold,24,Navy,0,-1);
            Text(dc,total==0?"0 running":$"{total} running",Body,FontWeight.Normal,10,Muted,1,30);
            if(total==0)
            {
                Text(dc,"No running timers",Type.NameFont,FontWeight.Bold,18,Navy,0,55);
                Text(dc,"Start a timer to see it here.",Body,FontWeight.Normal,10,Muted,1,81);
            }
            for(int i=0;i<rows.Length;i++)
            {
                var row=new Rect(0,49+i*44,width-(total>5?10:0),44);var clock=rows[i];
                if(clock.Active)dc.DrawRectangle(Brush.Parse("#56F7E6B2"),null,row,4,4);
                if(i<rows.Length-1)dc.DrawLine(new Pen(Brush.Parse("#499A7B48"),.7),new Point(5,row.Bottom),new Point(row.Right-5,row.Bottom));
                var key=new Rect(row.Right-29,row.Center.Y-13,26,26);
                var time=Format(clock.Time,Type.TimerFont,FontWeight.Black,20,Navy);
                // Leave two pixels for font-size rounding while preserving the name's minimum space.
                double timeLimit=key.X-10-16-84;
                double timeSize=Math.Min(20,20*timeLimit/Math.Max(1,time.WidthIncludingTrailingWhitespace));
                if(timeSize<20)time=Format(clock.Time,Type.TimerFont,FontWeight.Black,timeSize,Navy);
                if(timeSize<14||time.WidthIncludingTrailingWhitespace>timeLimit+.1)throw new Exception("Complete time does not fit readably.");
                double timeX=key.X-10-time.WidthIncludingTrailingWhitespace;
                var nameArea=new Rect(1,row.Y+1,timeX-12,row.Height-2);
                if(clock.Hovered)dc.DrawRectangle(Brush.Parse("#66FFFFFF"),null,nameArea,4,4);
                if(clock.Focused)dc.DrawRectangle(null,new Pen(Gold,1.5),nameArea,4,4);
                var name=Format(clock.Name,clock.Rtl?new FontFamily("Segoe UI"):Type.NameFont,
                    clock.Rtl?FontWeight.SemiBold:FontWeight.Bold,clock.Rtl?16:18,Navy,clock.Rtl?FlowDirection.RightToLeft:FlowDirection.LeftToRight);
                name.MaxTextWidth=timeX-16;name.MaxTextHeight=24;name.Trimming=TextTrimming.CharacterEllipsis;
                if(name.MaxTextWidth<82)throw new Exception("Clock names have insufficient room.");
                dc.DrawText(name,new Point(5,clock.Active?row.Y+1:row.Center.Y-name.Height/2));
                dc.DrawText(time,new Point(timeX,row.Center.Y-time.Height/2));
                if(clock.Active)
                {
                    var badge=new Rect(6,row.Y+26,39,13);
                    dc.DrawRectangle(Navy,new Pen(Gold,.6),badge,3,3);Center(dc,"ACTIVE",7,Cream,badge);
                }
                DrawButton(dc,key,false,0);
            }
            if(total>5)
            {
                dc.DrawRectangle(Brush.Parse("#249A7B48"),null,new Rect(width-4,51,3,216),1.5,1.5);
                dc.DrawRectangle(Gold,null,new Rect(width-4,51,3,216*5d/total),1.5,1.5);
            }
            var hide=new Rect(width-53,Bounds.Height-27,53,26);
            DrawButton(dc,hide,true,0);
            if(total>0)Text(dc,"Select a clock to switch",Body,FontWeight.Normal,9,Muted,1,hide.Y+7);
        }
    }

    private sealed class ButtonStates(bool hide=false):Control
    {
        public override void Render(DrawingContext dc)
        {
            dc.DrawRectangle(Paper,null,new Rect(0,20,Bounds.Width,56),5,5);
            string[] labels=["Normal","Hover","Pressed","Focus"];
            for(int i=0;i<4;i++)
            {
                double x=i*86;Text(dc,labels[i],Body,FontWeight.Normal,11,Cream,x,0);
                DrawButton(dc,new Rect(x+3,26,hide?53:26,26),hide,i);
            }
        }
    }
    private static void DrawButton(DrawingContext dc,Rect rect,bool hide,int state)
    {
        IBrush? fill=hide?(state==1?Brush.Parse("#66FFFFFF"):state==2?Brush.Parse("#F1D796"):null)
            :state==1?Brush.Parse("#214A5C"):state==2?Brush.Parse("#071E29"):Navy;
        dc.DrawRectangle(fill,new Pen(Gold,1),rect,4,4);
        if(state==3)dc.DrawRectangle(null,new Pen(hide?Navy:Gold,1.5),rect.Inflate(3),5,5);
        if(hide)Center(dc,"Hide",11,Navy,rect);
        else
        {
            dc.DrawRectangle(Cream,null,new Rect(rect.Center.X-4.5,rect.Center.Y-5,3,10),.5,.5);
            dc.DrawRectangle(Cream,null,new Rect(rect.Center.X+1.5,rect.Center.Y-5,3,10),.5,.5);
        }
    }
    private static FormattedText Format(string text,FontFamily font,FontWeight weight,double size,IBrush brush,FlowDirection flow=FlowDirection.LeftToRight)
        =>new(text,CultureInfo.InvariantCulture,flow,new Typeface(font,FontStyle.Normal,weight),size,brush);
    private static void Text(DrawingContext dc,string text,FontFamily font,FontWeight weight,double size,IBrush brush,double x,double y)
        =>dc.DrawText(Format(text,font,weight,size,brush),new Point(x,y));
    private static void Center(DrawingContext dc,string text,double size,IBrush brush,Rect rect)
    {var value=Format(text,Body,FontWeight.SemiBold,size,brush);dc.DrawText(value,new Point(rect.Center.X-value.Width/2,rect.Center.Y-value.Height/2));}
    private static TextBlock Label(string text,double size,IBrush? brush=null)=>new(){Text=text,FontFamily=Body,FontSize=size,Foreground=brush??Cream};
    private static void Add(Canvas board,Control child,double x,double y)
    {Canvas.SetLeft(child,x);Canvas.SetTop(child,y);board.Children.Add(child);}
    private static void Save(Control control,string name)
    {
        using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(control.Bounds.Width),(int)Math.Ceiling(control.Bounds.Height)),new Vector(96,96));
        bitmap.Render(control);bitmap.Save(Path.Combine(Output,name));
    }
}
