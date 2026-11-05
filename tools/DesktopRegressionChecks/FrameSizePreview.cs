using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;

// Isolated, static design spike. No app lifetime, user settings, native windows or hotkeys.
internal sealed class FrameSizePreviewApp : Application { }

internal static class FrameSizePreview
{
    private const string FrameId = "engraved-filigree";
    private const double FrameScale = .18;
    private static string Output = Path.GetFullPath("design/one-piece-v2/clock-picker/native-frame-preview");
    private static readonly IBrush Navy = Brush.Parse("#102938"), Cream = Brush.Parse("#F7E9CA"),
        Gold = Brush.Parse("#BC9553"), Muted = Brush.Parse("#ADC3CD");
    private static readonly StopwatchTypographyProfile Type = StopwatchTypography.Resolve("navigator-bold");

    internal static void Run(bool modular=false)
    {
        Output=Path.GetFullPath("design/one-piece-v2/clock-picker/"+(modular?"native-modular-frame-preview":"native-frame-preview"));
        Directory.CreateDirectory(Output);
        var frame = StopwatchAppearanceAssets.Shared.Get("frame", FrameId);
        var sourcePath = Path.GetFullPath("StopwatchOverlay.Desktop/Assets/Stopwatch/Frames/engraved-filigree.png");
        var sourceHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath)));
        var checks = new List<object>();
        int maximumCornerDifference=0;
        foreach (double dpi in new[] { 96d, 144d, 192d })
        {
            var sizes = new[] { new Size(420,132), new Size(460,480), new Size(820,650) };
            var captures = sizes.Select(size => Capture(new FrameSurface(size,frame,true,modular:modular),dpi)).ToArray();
            try
            {
                int cw = (int)Math.Floor(frame.SliceInsets.Left*FrameScale*dpi/96)-2;
                int ch = (int)Math.Floor(frame.SliceInsets.Top*FrameScale*dpi/96)-2;
                var baseline = Pixels(captures[0]);
                if (!baseline.Where((_,index)=>index%4==3).Any(alpha=>alpha>0))
                    throw new Exception("Frame-only export was empty.");
                for (int target=1;target<captures.Length;target++)
                {
                    var pixels = Pixels(captures[target]);
                    for (int corner=0;corner<4;corner++)
                    {
                        int differences=0,maxDifference=0;long totalDifference=0;
                        for (int y=0;y<ch;y++) for (int x=0;x<cw;x++) for (int c=0;c<4;c++)
                        {
                            int ax=(corner%2==0?x:captures[0].PixelSize.Width-cw+x);
                            int ay=(corner<2?y:captures[0].PixelSize.Height-ch+y);
                            int bx=(corner%2==0?x:captures[target].PixelSize.Width-cw+x);
                            int by=(corner<2?y:captures[target].PixelSize.Height-ch+y);
                            int delta=Math.Abs(baseline[(ay*captures[0].PixelSize.Width+ax)*4+c]-pixels[(by*captures[target].PixelSize.Width+bx)*4+c]);
                            if(delta>0)differences++;
                            totalDifference+=delta;
                            maxDifference=Math.Max(maxDifference,delta);
                        }
                        maximumCornerDifference=Math.Max(maximumCornerDifference,maxDifference);
                        checks.Add(new { dpi, target=sizes[target].ToString(), corner, comparedWidth=cw, comparedHeight=ch, differences, maxDifference,
                            meanChannelDifference=(double)totalDifference/(cw*ch*4) });
                    }
                }
            }
            finally { foreach(var bitmap in captures)bitmap.Dispose(); }
        }

        ThemedClockFace Clock()
        {
            var face = new ThemedClockFace { Text="00:44:27.0", ProjectName="Illustration" };
            face.ApplySettings(new AppSettings { OverlayTheme=OverlayThemeCatalog.Nika, TextColor="Theme default", ShowRecIndicator=false });
            face.SetStopwatchPresentationState(new(true,true,false));
            face.Measure(Size.Infinity);
            face.Width=face.DesiredSize.Width;face.Height=face.DesiredSize.Height;
            return face;
        }
        Control clock=modular?new FrameSurface(new Size(420,144),frame,false,modular:true,clock:true):Clock();
        var picker=new FrameSurface(new Size(460,480),frame,false,false,modular);
        var large=new FrameSurface(new Size(820,650),frame,false,true,modular);
        Save(clock,"stopwatch-actual-renderer",96);
        Save(picker,"clock-picker-frame-preview",96);
        Save(large,"large-panel-frame-preview",96);
        using var clockSnapshot=new Bitmap(Path.Combine(Output,"stopwatch-actual-renderer.png"));
        using var pickerSnapshot=new Bitmap(Path.Combine(Output,"clock-picker-frame-preview.png"));
        using var largeSnapshot=new Bitmap(Path.Combine(Output,"large-panel-frame-preview.png"));

        var board = new Canvas { Width=1480, Height=1030, Background=Brush.Parse("#10232F") };
        void At(Control child,double x,double y) { Canvas.SetLeft(child,x);Canvas.SetTop(child,y);board.Children.Add(child); }
        void Label(string text,double x,double y,double size,IBrush? color=null)
            =>At(new TextBlock { Text=text,FontSize=size,Foreground=color??Cream,FontFamily=FontFamily.Default },x,y);
        Label(modular?"Same pieces. No stretching.":"One frame. Three sizes.",48,36,38);
        Label(modular?"Engraved Filigree  /  Fixed ornaments + repeated border segments":"Engraved Filigree  /  Existing production PNG and nine-slice drawing code",50,96,19,Muted);
        Label("01  STOPWATCH",64,154,18);
        At(new Image { Source=clockSnapshot,Width=clock.Width,Height=clock.Height,Stretch=Stretch.Fill },64,190);
        Label($"{clock.Width:0.#} × {clock.Height-12:0.#} frame"+(modular?" · modular preview":" · existing clock renderer"),64,354,15,Muted);
        Label("02  CLOCK PICKER",64,408,18);
        At(new Image { Source=pickerSnapshot,Width=picker.Width,Height=picker.Height,Stretch=Stretch.Fill },64,450);
        Label("03  LARGER PANEL",592,238,18);
        At(new Image { Source=largeSnapshot,Width=large.Width,Height=large.Height,Stretch=Stretch.Fill },592,280);
        Label("460 × 480",64,940,15,Muted);
        Label("820 × 650",592,940,15,Muted);
        Label(modular?"One existing PNG. Every frame piece keeps the same proportions and scale; extra space uses repeated segments.":"Same corner scale throughout. Panel contents are static design examples; the frame drawing is real.",64,988,16,Muted);
        Save(board,"same-frame-three-sizes",96);

        if(sourceHash!=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath))))
            throw new Exception("Source frame changed during preview.");
        var report=new
        {
            frame=FrameId, source="StopwatchOverlay.Desktop/Assets/Stopwatch/Frames/engraved-filigree.png", sourceHash,
            sourceBytes=new FileInfo(sourcePath).Length, frameScale=FrameScale,
            sampledCornerRegionDip=new { width=frame.SliceInsets.Left*FrameScale,height=frame.SliceInsets.Top*FrameScale },
            renderer=modular?"Native Avalonia modular prototype: ModularFiligreePreview; unchanged source PNG":"StopwatchAppearanceAssets.DrawFrame; StopwatchFrameMask.Create; ThemedClockFace for stopwatch",
            modular, modulePlacements=modular?new[]{new Size(420,132),new Size(460,480),new Size(820,650)}.Select(s=>new{size=s.ToString(),pieces=ModularFiligreePreview.Pieces(s)}).ToArray():null,
            uniformScaleEnforcedForEveryPiece=modular,
            sourceFrameUnchanged=true,newRuntimeArtworkFiles=0,
            checks, maximumCornerDifference, cornerComparison="Diagnostic pixel readback; nonzero differences are reported, not treated as byte-identical corners.",
            exportDpi=new { individual=96,comparison=96 },
            limits="One frame only. Raster exports at 100%, 150%, 200% do not change or test system monitor DPI. Picker and large-panel contents are static examples; behavior and adaptive layouts are not implemented. No native windows or user settings are opened."
        };
        File.WriteAllText(Path.Combine(Output,"verification.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"Exported four native-renderer images. {checks.Count} corner comparisons recorded across 96/144/192 DPI; maximum channel difference {maximumCornerDifference}/255. Source artwork unchanged.");
    }

    private static RenderTargetBitmap Capture(Control control,double dpi)
    {
        var size=new Size(control.Width,control.Height);
        control.Measure(size);control.Arrange(new Rect(size));
        var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(size.Width*dpi/96),(int)Math.Ceiling(size.Height*dpi/96)),new Vector(dpi,dpi));
        bitmap.Render(control);return bitmap;
    }
    private static void Save(Control control,string name,double dpi)
    { using var bitmap=Capture(control,dpi);bitmap.Save(Path.Combine(Output,name+".png")); }
    private static byte[] Pixels(Bitmap bitmap)
    {
        var data=new byte[bitmap.PixelSize.Width*bitmap.PixelSize.Height*4];
        var pointer=Marshal.AllocHGlobal(data.Length);
        try { bitmap.CopyPixels(new PixelRect(bitmap.PixelSize),pointer,data.Length,bitmap.PixelSize.Width*4);Marshal.Copy(pointer,data,0,data.Length);return data; }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    private sealed class FrameSurface : Control
    {
        private readonly StopwatchAsset frame,background;
        private readonly bool frameOnly,large,modular,clock;
        private readonly Geometry mask;
        internal FrameSurface(Size size,StopwatchAsset frame,bool frameOnly,bool large=false,bool modular=false,bool clock=false)
        {
            Width=size.Width;Height=size.Height;this.frame=frame;this.frameOnly=frameOnly;this.large=large;this.modular=modular;this.clock=clock;
            background=StopwatchAppearanceAssets.Shared.Get("background","navigator-parchment");
            // Invoke the production mask too; this design fixture does not copy its algorithm.
            var maskType=typeof(StopwatchAppearanceAssets).Assembly.GetType("StopwatchOverlay.Desktop.Controls.StopwatchFrameMask")!;
            mask=modular?ModularFiligreePreview.BackgroundMask(new Size(size.Width,size.Height-(clock?12:0))):
                (Geometry)maskType.GetMethod("Create",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{frame,new Rect(size),1d,false})!;
        }
        public override void Render(DrawingContext context)
        {
            var rect=new Rect(new Size(Bounds.Width,Bounds.Height-(clock?12:0)));
            using var origin=context.PushTransform(Matrix.CreateTranslation(0,clock?12:0));
            if(!frameOnly)
            {
                using(context.PushGeometryClip(mask))
                {
                    // Match the approved overlay's 70% background over a common navy underlay.
                    context.DrawRectangle(Brush.Parse("#10232F"),null,rect);
                    using(context.PushOpacity(.70))StopwatchAppearanceAssets.DrawCover(context,background,rect);
                    if(clock)ClockContent(context);else if(large)LargeContent(context);else PickerContent(context);
                }
            }
            if(modular)ModularFiligreePreview.Draw(context,frame,rect.Size);
            else StopwatchAppearanceAssets.DrawFrame(context,frame,rect,FrameScale);
            if(clock)Button(context,"ACTIVE",new Rect(Width/2-42,-4,84,22));
        }
        private void ClockContent(DrawingContext dc)
        {
            const string time="00:44:27.0";
            StopwatchTypography.DrawTime(dc,time,new Point((Width-StopwatchTypography.Width(time,Type,48))/2,24),Type,48,Navy);
            Text(dc,"Illustration",169,72,20,null,true);
            var triangle=new StreamGeometry();using(var p=triangle.Open())
            {p.BeginFigure(new Point(146,79),true);p.LineTo(new Point(159,86));p.LineTo(new Point(146,93));p.EndFigure(true);}
            dc.DrawGeometry(Brush.Parse("#087480"),null,triangle);
        }
        private static void Text(DrawingContext dc,string text,double x,double y,double size,IBrush? brush=null,bool serif=false)
        {
            var formatted=new FormattedText(text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,
                new Typeface(serif?Type.NameFont:FontFamily.Default,FontStyle.Normal,serif?FontWeight.Bold:FontWeight.Normal),size,brush??Navy);
            dc.DrawText(formatted,new Point(x,y));
        }
        private static void Button(DrawingContext dc,string label,Rect bounds)
        {
            dc.DrawRectangle(Navy,new Pen(Gold,1),bounds,5,5);
            var formatted=new FormattedText(label,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface(Type.NameFont,FontStyle.Normal,FontWeight.Bold),19,Cream);
            dc.DrawText(formatted,new Point(bounds.Center.X-formatted.Width/2,bounds.Center.Y-formatted.Height/2));
        }
        private void PickerContent(DrawingContext dc)
        {
            dc.DrawRectangle(Navy,null,new Rect(18,15,Width-36,60));
            Text(dc,"Choose a clock",37,27,27,Cream,true);Text(dc,"6 clocks",333,38,13,Cream);
            string[] names={"Illustration","Coding","Reading","Study","Writing","Planning"};
            string[] times={"00:44:27","01:12:08","00:18:42","00:36:10","00:08:15","00:05:32"};
            for(int i=0;i<6;i++)
            {
                double y=91+i*47;
                if(i==2)dc.DrawRectangle(Brush.Parse("#30EBC25C"),new Pen(Gold,1.5),new Rect(29,y-2,Width-58,42),5,5);
                dc.DrawRectangle(Navy,new Pen(Gold,1),new Rect(39,y+4,27,27),4,4);
                Text(dc,(i+1).ToString(),47,y+7,16,Cream);
                if(i is 0 or 1 or 4)
                {
                    var triangle=new StreamGeometry();using(var p=triangle.Open())
                    {p.BeginFigure(new Point(79,y+10),true);p.LineTo(new Point(91,y+17));p.LineTo(new Point(79,y+24));p.EndFigure(true);}
                    dc.DrawGeometry(Brush.Parse("#087480"),null,triangle);
                }
                else
                {
                    dc.DrawRectangle(Brush.Parse("#A45A12"),null,new Rect(79,y+10,4,14));
                    dc.DrawRectangle(Brush.Parse("#A45A12"),null,new Rect(86,y+10,4,14));
                }
                Text(dc,names[i],101,y+4,22,null,true);
                if(i==0){dc.DrawRectangle(Navy,new Pen(Gold,1),new Rect(224,y+7,55,22),4,4);Text(dc,"ACTIVE",231,y+10,10,Cream);}
                StopwatchTypography.DrawTime(dc,times[i],new Point(322,y+4),Type,24,Navy);
                if(i!=2)dc.DrawLine(new Pen(Brush.Parse("#4382744F"),.7),new Point(38,y+41),new Point(Width-38,y+41));
            }
            Text(dc,"1–9 select  ·  Arrows move  ·  Enter confirm  ·  Esc close",38,389,12);
            Button(dc,"Select clock",new Rect(142,416,176,34));
        }
        private void LargeContent(DrawingContext dc)
        {
            dc.DrawRectangle(Navy,null,new Rect(18,15,Width-36,74));
            Text(dc,"Appearance",42,32,32,Cream,true);Text(dc,"Larger panel example",Width-230,44,16,Cream);
            Text(dc,"Style",44,119,26,null,true);Text(dc,"Transparency",158,125,18);
            dc.DrawLine(new Pen(Gold,1),new Point(42,159),new Point(Width-42,159));
            string[] labels={"Background","Frame","Typography","Artwork"};
            string[] values={"Navigator Parchment","Engraved Filigree","Navigator Bold","Clean"};
            for(int i=0;i<labels.Length;i++)
            {
                double y=188+i*76;
                Text(dc,labels[i],44,y+10,23,null,true);
                dc.DrawRectangle(Brush.Parse("#30FFF4DA"),new Pen(Brush.Parse("#847353"),1),new Rect(275,y,Width-320,48),4,4);
                Text(dc,values[i],292,y+13,18);
            }
            Text(dc,"Shared artwork, different panel dimensions.",44,523,22,null,true);
            Text(dc,"The outer frame is drawn from the same existing PNG.",44,557,16);
            Button(dc,"Done",new Rect(Width-171,590,126,34));
        }
    }
}
