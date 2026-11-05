using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using StopwatchOverlay.Desktop.Controls;

// A static design study: simulated state, never OS pointer or keyboard input.
internal static class ClockPickerStatePreview
{
    private static readonly string Output=Path.GetFullPath("design/one-piece-v2/clock-picker/native-state-preview");
    private static readonly IBrush Navy=Brush.Parse("#102B3B"),Cream=Brush.Parse("#F6E8C9"),Muted=Brush.Parse("#655539"),
        Gold=Brush.Parse("#AF7C29"),Teal=Brush.Parse("#087480"),Amber=Brush.Parse("#995817"),Paper=Brush.Parse("#EBD3A5");
    private static readonly StopwatchTypographyProfile Navigator=StopwatchTypography.Resolve("navigator-bold");
    private static readonly FontFamily Inter=StopwatchTypography.Resolve("modern-deck").NameFont;
    private sealed record Row(int Number,string Name,string Time,string Mode="Stopwatch",bool Running=false,
        bool Active=false,bool Selected=false,bool Hovered=false,bool Rtl=false);
    private sealed record Scenario(string Title,string Subtitle,string Caption,int Total,Row[] Rows,bool Scrolled=false);
    private static readonly Scenario[] Scenarios=
    {
        new("01  Row states","ACTIVE, hover and keyboard selection","Hover adds a pale wash; the gold outline stays on Reading.",6,new[]{
            new Row(1,"Illustration","00:44:27",Running:true,Active:true),
            new Row(2,"Coding","01:12:08",Running:true,Hovered:true),
            new Row(3,"Reading","00:18:42",Selected:true),
            new Row(4,"Study","00:36:10"),new Row(5,"Writing","00:08:15",Running:true),new Row(6,"Planning","00:05:32")}),
        new("02  Names & modes","Long names, long hours and right-to-left text","Times stay complete; names shorten within their own space.",6,new[]{
            new Row(1,"Illustration — The Grand Voyage","123:44:27",Running:true,Active:true),
            new Row(2,"Office clock","14:28:06","Clock",true),
            new Row(3,"برنامه‌نویسی پروژه","00:18:42",Selected:true,Rtl:true),
            new Row(4,"Tea break","00:08:15","Countdown",true),
            new Row(5,"Editing / episode 12","01:12:08","Timecode"),
            new Row(6,"Timer 6","00:05:32")}),
        new("03  More than nine","12 clocks, scrolled to entries 7–12","10–12 use arrows or the mouse; 1–9 remain direct shortcuts.",12,new[]{
            new Row(7,"Music","00:22:08",Running:true),new Row(8,"Research","00:34:12"),
            new Row(9,"Exercise","00:15:06",Running:true),new Row(10,"Sketching","00:28:41"),
            new Row(11,"Planning","00:05:32",Active:true,Selected:true),new Row(12,"Review","00:11:20")},true)
    };

    internal static void Run()
    {
        Directory.CreateDirectory(Output);
        var board=new Canvas{Width=1520,Height=880,Background=Brush.Parse("#10232F")};
        Add(board,Label("NAVIGATOR / STATES & REAL USE",32),42,30);
        Add(board,Label("Approved row style · same frame and size · simulated native previews",17,Brush.Parse("#B6CAD1")),44,80);
        var metrics=new List<object>();
        for(int i=0;i<Scenarios.Length;i++)
        {
            var scenario=Scenarios[i];double x=42+i*488;
            Add(board,Label(scenario.Title,25),x+6,130);
            Add(board,Label(scenario.Subtitle,14,Brush.Parse("#B6CAD1")),x+6,168);
            var content=new Contents(scenario);
            var surface=new StopwatchFrameSurface{Width=460,Height=480,Padding=new Thickness(8),Background=Paper,Child=content};
            surface.Measure(new Size(460,480));surface.Arrange(new Rect(0,0,460,480));
            using(var image=Capture(surface))image.Save(Path.Combine(Output,$"scenario-{i+1:00}.png"));
            Add(board,surface,x,200);
            var caption=Label(scenario.Caption,14);caption.Width=440;caption.TextWrapping=TextWrapping.Wrap;
            Add(board,caption,x+8,703);
            metrics.Add(new{scenario.Title,scenario.Total,scenario.Scrolled,scenario.Rows,contentSize=content.Bounds.Size});
        }
        Add(board,Label("ACTIVE follows the current clock. The outline follows the selection. Both can appear on the same row.",15,Brush.Parse("#E1C58E")),48,768);
        Add(board,new ButtonSamples{Width=440,Height=66},48,805);
        Add(board,Label("Button treatments: normal / hover / pressed\nFull names, category and separated status stay available in row details.",13,Brush.Parse("#B6CAD1")),516,812);
        board.Measure(new Size(1520,880));board.Arrange(new Rect(0,0,1520,880));
        using(var image=Capture(board))image.Save(Path.Combine(Output,"states-and-edge-cases.png"));
        File.WriteAllText(Path.Combine(Output,"verification.json"),JsonSerializer.Serialize(new{
            status="static-design-preview-not-implemented",selectedRowStyle="Navigator",frame="engraved-filigree",panelSize="460 x 480",
            scenarios=metrics,newRuntimeImages=0,
            execution="Native rendering on private desktop. State and scrolling are fixtures, not input automation.",
            limitations="This is a visual design study. Pointer behavior, keyboard navigation, scrolling and accessible row details require subsequent implementation verification."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Rendered three Navigator state scenarios with 18 rows, long and RTL names, four modes, 12-clock scrolling fixture and three button states. No app settings or production UI changed.");
    }

    private sealed class Contents(Scenario scenario):Control
    {
        public override void Render(DrawingContext dc)
        {
            double w=Bounds.Width,h=Bounds.Height;
            Text(dc,"Choose a clock",Navigator.NameFont,FontWeight.Bold,27,Navy,0,0);
            Text(dc,scenario.Total+" clocks"+(scenario.Scrolled?" · Showing 7–12":""),Inter,FontWeight.Normal,11,Muted,1,36);
            Text(dc,"×",Inter,FontWeight.Normal,23,Navy,w-21,2);
            dc.DrawLine(new Pen(Brush.Parse("#7A9B773C"),1),new Point(0,57),new Point(w,57));
            for(int i=0;i<scenario.Rows.Length;i++)
                DrawRow(dc,new Rect(0,66+i*44,w-(scenario.Scrolled?12:0),42),scenario.Rows[i]);
            if(scenario.Scrolled)
            {
                dc.DrawRectangle(Brush.Parse("#309B773C"),null,new Rect(w-5,66,3,262),1.5,1.5);
                dc.DrawRectangle(Gold,null,new Rect(w-5,197,3,131),1.5,1.5);
            }
            double footer=h-68;
            dc.DrawLine(new Pen(Brush.Parse("#7A9B773C"),1),new Point(0,footer-10),new Point(w,footer-10));
            Text(dc,"1–9 select   ·   ↑↓ move   ·   Enter confirm   ·   Esc close",Inter,FontWeight.Normal,10,Muted,0,footer);
            Button(dc,new Rect(w-123,h-38,123,32),0);
        }
    }

    private static void DrawRow(DrawingContext dc,Rect rect,Row row)
    {
        if(row.Selected)dc.DrawRectangle(Brush.Parse("#F1D796"),new Pen(Gold,1.5),rect.Deflate(.75),4,4);
        else if(row.Hovered)dc.DrawRectangle(Brush.Parse("#55FFFFFF"),null,rect,4,4);
        if(!row.Selected)dc.DrawLine(new Pen(Brush.Parse("#499A7B48"),.7),new Point(5,rect.Bottom),new Point(rect.Right-5,rect.Bottom));
        var key=new Rect(7,rect.Center.Y-11,22,22);
        if(row.Number<=9)dc.DrawRectangle(Navy,new Pen(Gold,.8),key,3,3);
        Center(dc,row.Number.ToString(),Inter,FontWeight.SemiBold,12,row.Number<=9?Cream:Muted,key);
        var state=new Rect(40,rect.Center.Y-5,9,10);
        if(row.Mode=="Clock")
        {
            dc.DrawEllipse(null,new Pen(Teal,1.3),state.Center,5,5);
            dc.DrawLine(new Pen(Teal,1.2),state.Center,new Point(state.Center.X,state.Y+2));
            dc.DrawLine(new Pen(Teal,1.2),state.Center,new Point(state.Right-1,state.Center.Y+2));
        }
        else if(row.Running)
        {
            var triangle=new StreamGeometry();using(var p=triangle.Open())
            {p.BeginFigure(state.TopLeft,true);p.LineTo(new Point(state.Right,state.Center.Y));p.LineTo(state.BottomLeft);p.EndFigure(true);}
            dc.DrawGeometry(Teal,null,triangle);
        }
        else
        {
            dc.DrawRectangle(Amber,null,new Rect(state.X,state.Y,3,state.Height));
            dc.DrawRectangle(Amber,null,new Rect(state.X+6,state.Y,3,state.Height));
        }
        var time=Format(row.Time,Navigator.TimerFont,FontWeight.Black,22,Navy);
        time.SetFontFeatures(new FontFeatureCollection{FontFeature.Parse("tnum"),FontFeature.Parse("lnum")});
        double timeX=rect.Right-7-time.Width;
        var nameBox=new Rect(58,rect.Y+4,Math.Max(12,(row.Active?timeX-58:timeX-13)-58),rect.Height-8);
        var name=Format(row.Name,row.Rtl?new FontFamily("Segoe UI"):Navigator.NameFont,row.Rtl?FontWeight.SemiBold:FontWeight.Bold,row.Rtl?17:20,Navy,
            row.Rtl?FlowDirection.RightToLeft:FlowDirection.LeftToRight);
        name.MaxTextWidth=nameBox.Width;name.MaxTextHeight=nameBox.Height;name.Trimming=TextTrimming.CharacterEllipsis;
        using(dc.PushClip(nameBox))dc.DrawText(name,new Point(nameBox.X,rect.Center.Y-name.Height/2));
        dc.DrawText(time,new Point(timeX,rect.Center.Y-time.Height/2-5));
        var mode=Format(row.Mode,Inter,FontWeight.Normal,9,Muted);
        dc.DrawText(mode,new Point(rect.Right-7-mode.Width,rect.Bottom-13));
        if(row.Active)
        {
            var badge=new Rect(timeX-54,rect.Center.Y-8,45,16);
            dc.DrawRectangle(Navy,new Pen(Gold,.8),badge,3,3);
            Center(dc,"ACTIVE",Inter,FontWeight.SemiBold,8,Cream,badge);
        }
    }

    private sealed class ButtonSamples:Control
    {
        public override void Render(DrawingContext dc)
        {for(int i=0;i<3;i++)Button(dc,new Rect(i*143,0,123,32),i);}
    }
    private static void Button(DrawingContext dc,Rect rect,int state)
    {
        dc.DrawRectangle(state==1?Brush.Parse("#214A5C"):state==2?Brush.Parse("#071E29"):Navy,new Pen(state==1?Brush.Parse("#D7AE5C"):Gold,state==1?1.5:1),rect,4,4);
        Center(dc,"Select clock",Inter,FontWeight.SemiBold,12,Cream,rect);
    }
    private static FormattedText Format(string text,FontFamily font,FontWeight weight,double size,IBrush brush,FlowDirection flow=FlowDirection.LeftToRight)
        =>new(text,CultureInfo.InvariantCulture,flow,new Typeface(font,FontStyle.Normal,weight),size,brush);
    private static void Text(DrawingContext dc,string text,FontFamily font,FontWeight weight,double size,IBrush brush,double x,double y)
        =>dc.DrawText(Format(text,font,weight,size,brush),new Point(x,y));
    private static void Center(DrawingContext dc,string text,FontFamily font,FontWeight weight,double size,IBrush brush,Rect rect)
    {var value=Format(text,font,weight,size,brush);dc.DrawText(value,new Point(rect.Center.X-value.Width/2,rect.Center.Y-value.Height/2));}
    private static TextBlock Label(string text,double size,IBrush? brush=null)=>new(){Text=text,FontFamily=Inter,FontSize=size,Foreground=brush??Cream};
    private static void Add(Canvas canvas,Control control,double x,double y)
    {Canvas.SetLeft(control,x);Canvas.SetTop(control,y);canvas.Children.Add(control);}
    private static RenderTargetBitmap Capture(Control control)
    {
        var image=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(control.Bounds.Width),(int)Math.Ceiling(control.Bounds.Height)),new Vector(96,96));
        image.Render(control);return image;
    }
}
