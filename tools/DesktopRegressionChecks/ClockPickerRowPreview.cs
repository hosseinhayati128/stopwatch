using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using StopwatchOverlay.Desktop.Controls;

// Static design comparison only. No application settings, windows or command handlers.
internal static class ClockPickerRowPreview
{
    private static readonly string Output=Path.GetFullPath("design/one-piece-v2/clock-picker/native-row-options");
    private static readonly IBrush Navy=Brush.Parse("#102B3B"),Cream=Brush.Parse("#F6E8C9"),
        Muted=Brush.Parse("#655539"),Gold=Brush.Parse("#AF7C29"),Teal=Brush.Parse("#087480"),Amber=Brush.Parse("#995817");
    private static readonly FontFamily Inter=StopwatchTypography.Resolve("modern-deck").NameFont;
    private sealed record Option(string Name,string Caption,FontFamily NameFont,FontWeight NameWeight,double NameSize,
        FontFamily TimeFont,FontWeight TimeWeight,double TimeSize);
    private static readonly Option[] Options=
    {
        new("Navigator","Serif names + bold condensed times",StopwatchTypography.Resolve("navigator-bold").NameFont,FontWeight.Bold,20,
            StopwatchTypography.Resolve("navigator-bold").TimerFont,FontWeight.Black,22),
        new("Clean","Simple sans-serif + quiet dividers",Inter,FontWeight.SemiBold,16,
            StopwatchTypography.Resolve("modern-deck").TimerFont,FontWeight.ExtraBold,18),
        new("Logbook","Serif names + monospaced times",StopwatchTypography.Resolve("captains-serif").TimerFont,FontWeight.ExtraBold,16,
            StopwatchTypography.Resolve("typewriter-log").TimerFont,FontWeight.SemiBold,16)
    };
    private static readonly string[] Names={"Illustration","Coding","Reading","Study","Writing","Planning"};
    private static readonly string[] Times={"00:44:27","01:12:08","00:18:42","00:36:10","00:08:15","00:05:32"};

    internal static void Run()
    {
        Directory.CreateDirectory(Output);
        var board=new Canvas{Width=1520,Height=824,Background=Brush.Parse("#10232F")};
        Add(board,Label("CLOCK PICKER / ROWS & TYPE",32),42,32);
        Add(board,Label("Compact list · same shared frame · same six clocks",17,Brush.Parse("#B6CAD1")),44,83);
        var metrics=new List<object>();
        for(int i=0;i<Options.Length;i++)
        {
            var option=Options[i];double x=42+i*488;
            Add(board,Label($"{i+1:00}  {option.Name}",26),x+6,133);
            Add(board,Label(option.Caption,14,Brush.Parse("#B6CAD1")),x+6,170);
            var content=new PickerContent(option,i);
            var surface=new StopwatchFrameSurface{Width=460,Height=480,Padding=new Thickness(8),
                Background=Brush.Parse("#EBD3A5"),Child=content};
            surface.Measure(new Size(460,480));surface.Arrange(new Rect(0,0,460,480));
            using(var image=Capture(surface))image.Save(Path.Combine(Output,$"option-{i+1:00}.png"));
            if(content.Bounds.Width<340||content.Bounds.Height-78<66+6*44)
                throw new Exception($"Insufficient room for six rows and the footer: {content.Bounds.Size}.");
            metrics.Add(new{number=i+1,option.Name,nameFont=option.NameFont.Name,option.NameSize,timeFont=option.TimeFont.Name,option.TimeSize,
                rowHeight=44,contentWidth=content.Bounds.Width,contentHeight=content.Bounds.Height,clocks=6,activeRow=1,focusedRow=3});
            Add(board,surface,x,202);
            Add(board,Label(i==0?"Closest to the stopwatch's character":i==1?"Calmest for frequent switching":"More of a printed ledger feel",15),x+8,705);
        }
        Add(board,Label("ACTIVE = current clock     ·     Gold outline = keyboard selection",16,Brush.Parse("#E1C58E")),48,757);
        Add(board,Label("Native design previews · row treatments are proposals",12,Brush.Parse("#B6CAD1")),48,790);
        board.Measure(new Size(1520,824));board.Arrange(new Rect(0,0,1520,824));
        using(var image=Capture(board))image.Save(Path.Combine(Output,"row-options-01-03.png"));
        File.WriteAllText(Path.Combine(Output,"verification.json"),JsonSerializer.Serialize(new{
            status="design-options-not-production-UI",renderer="Avalonia native text and shared production frame surface",frame="engraved-filigree",
            panelWidth=460,panelHeight=480,names=Names,times=Times,options=metrics,
            execution="Private Windows desktop; no application lifetime, settings, windows or hotkeys.",newRuntimeImages=0},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Exported three native compact-list typography proposals, 18 fixture rows, shared 460x480 frame. No production picker changes.");
    }

    private sealed class PickerContent(Option option,int index):Control
    {
        public override void Render(DrawingContext dc)
        {
            double w=Bounds.Width,h=Bounds.Height;
            Text(dc,"Choose a clock",StopwatchTypography.Resolve("navigator-bold").NameFont,FontWeight.Bold,27,Navy,0,0);
            Text(dc,"6 clocks",Inter,FontWeight.Normal,11,Muted,1,36);
            Text(dc,"×",Inter,FontWeight.Normal,23,Navy,w-21,2);
            dc.DrawLine(new Pen(Brush.Parse("#7A9B773C"),1),new Point(0,57),new Point(w,57));
            for(int row=0;row<6;row++)DrawRow(dc,new Rect(0,66+row*44,w,42),row);
            double footer=h-68;
            dc.DrawLine(new Pen(Brush.Parse("#7A9B773C"),1),new Point(0,footer-10),new Point(w,footer-10));
            Text(dc,"1–9 select   ·   ↑↓ move   ·   Enter confirm   ·   Esc close",Inter,FontWeight.Normal,10,Muted,0,footer);
            var button=new Rect(w-123,h-38,123,32);
            dc.DrawRectangle(Navy,new Pen(Gold,1),button,4,4);
            Center(dc,"Select clock",Inter,FontWeight.SemiBold,12,Cream,button);
        }

        private void DrawRow(DrawingContext dc,Rect row,int number)
        {
            bool focused=number==2,active=number==0,running=number is 0 or 1 or 4;
            if(focused)dc.DrawRectangle(Brush.Parse("#F1D796"),new Pen(Gold,1.5),row.Deflate(.75),4,4);
            else if(index==2&&number%2==0)dc.DrawRectangle(Brush.Parse("#18FFFFFF"),null,row);
            if(!focused&&number!=5)dc.DrawLine(new Pen(Brush.Parse(index==1?"#289A7B48":"#499A7B48"),.7),
                new Point(5,row.Bottom),new Point(row.Right-5,row.Bottom));
            var key=new Rect(7,row.Center.Y-11,22,22);
            if(index==0)dc.DrawRectangle(Navy,new Pen(Gold,.8),key,3,3);
            else if(index==1)dc.DrawRectangle(Brush.Parse("#24FFFFFF"),new Pen(Brush.Parse("#569C814D"),.6),key,3,3);
            Center(dc,(number+1).ToString(),Inter,FontWeight.SemiBold,12,index==0?Cream:Muted,key);
            var state=new Rect(40,row.Center.Y-5,9,10);
            if(running)
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
            var time=Format(Times[number],option.TimeFont,option.TimeWeight,option.TimeSize,Navy);
            time.SetFontFeatures(new FontFeatureCollection{FontFeature.Parse("tnum"),FontFeature.Parse("lnum")});
            double timeX=row.Right-7-time.Width;
            double nameEnd=active?timeX-58:timeX-13;
            var name=Format(Names[number],option.NameFont,option.NameWeight,option.NameSize,Navy);
            name.MaxTextWidth=nameEnd-58;name.Trimming=TextTrimming.CharacterEllipsis;
            dc.DrawText(name,new Point(58,row.Center.Y-name.Height/2));
            dc.DrawText(time,new Point(timeX,row.Center.Y-time.Height/2));
            if(active)
            {
                var badge=new Rect(timeX-54,row.Center.Y-8,45,16);
                dc.DrawRectangle(Navy,new Pen(Gold,.8),badge,3,3);
                Center(dc,"ACTIVE",Inter,FontWeight.SemiBold,8,Cream,badge);
            }
        }
    }

    private static FormattedText Format(string text,FontFamily family,FontWeight weight,double size,IBrush brush)
        =>new(text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface(family,FontStyle.Normal,weight),size,brush);
    private static void Text(DrawingContext dc,string text,FontFamily family,FontWeight weight,double size,IBrush brush,double x,double y)
        =>dc.DrawText(Format(text,family,weight,size,brush),new Point(x,y));
    private static void Center(DrawingContext dc,string text,FontFamily family,FontWeight weight,double size,IBrush brush,Rect box)
    {var value=Format(text,family,weight,size,brush);dc.DrawText(value,new Point(box.Center.X-value.Width/2,box.Center.Y-value.Height/2));}
    private static TextBlock Label(string text,double size,IBrush? brush=null)=>new(){Text=text,FontFamily=Inter,FontSize=size,Foreground=brush??Cream};
    private static void Add(Canvas canvas,Control control,double x,double y)
    {Canvas.SetLeft(control,x);Canvas.SetTop(control,y);canvas.Children.Add(control);}
    private static RenderTargetBitmap Capture(Control control)
    {
        var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(control.Bounds.Width),(int)Math.Ceiling(control.Bounds.Height)),new Vector(96,96));
        bitmap.Render(control);return bitmap;
    }
}
