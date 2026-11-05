using System.Globalization;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;

internal static class FrameCatalogAudit
{
    private static readonly string Output=Path.GetFullPath("design/one-piece-v2/clock-picker/native-frame-catalog-audit");
    private static readonly IBrush Backdrop=Brush.Parse("#10232F"),Cream=Brush.Parse("#F5E7C8"),Muted=Brush.Parse("#A6C0CC");
    internal static void Run()=>RunAt(Output,false);
    internal static void RunRefinement()=>RunAt(Path.GetFullPath("design/one-piece-v2/clock-picker/native-frame-join-refinement"),true);
    internal static void RunProduction()=>RunAt(Path.GetFullPath("design/one-piece-v2/clock-picker/shared-renderer-integration/frame-catalog"),true);
    private static void RunAt(string output,bool refinement)
    {
        Directory.CreateDirectory(output);
        string[] first={"rope-binding","chart-scroll","royal-cartouche"};
        string[] refined={"tide-curve","admiralty","vine-filigree","royal-cartouche"};
        var entries=StopwatchAppearanceCatalog.Frames.OrderBy(f=>Array.IndexOf(first,f.Id) is int index&&index>=0?index:100).ToArray();
        var evidence=new List<object>();
        var readme=new StringBuilder("# Modular frame catalog audit\n\nProduction shared-renderer audit using the reviewed recipe metadata. All 16 source PNGs remain unchanged. No new runtime artwork, UI windows, hotkey registration or user settings access.\n\nEvery artwork placement uses uniform 0.18 scale; extra length uses repeated source strips with final-tile clipping. Fixed ornaments have separate placements. Passing geometry checks does not mean a seam or content layout is visually approved.\n\n| Frame | Modular recipe | Minimum artwork size (DIP) | Review concern |\n| --- | --- | --- | --- |\n");
        int exported=0,highDpi=0,totalPieces=0;
        var scaleChecks=new List<object>();
        foreach(var option in entries)
        {
            var asset=StopwatchAppearanceAssets.Shared.Get("frame",option.Id);
            string source=Path.GetFullPath($"StopwatchOverlay.Desktop/Assets/Stopwatch/Frames/{option.Id}.png");
            string before=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source)));
            var sizes=new[]{new Size(420,132),new Size(460,480),new Size(820,650)};
            var views=sizes.Select(s=>new FrameView(option,asset,s)).ToArray();
            foreach(var view in views)
            {
                ModularFrameCatalogPreview.Validate(view.Plan,asset);
                using var render=Capture(view,96);
                if(render.PixelSize.Width==0||render.PixelSize.Height==0)throw new Exception("Empty render.");
                totalPieces+=view.Plan.Pieces.Count;exported++;
                if((refinement?refined:first).Contains(option.Id))foreach(double dpi in new[]{144d,192d})
                {
                    using var scaled=Capture(view,dpi);highDpi++;
                    double agreement=AlphaAgreement(render,scaled,dpi/96);
                    if(agreement<.70)throw new Exception($"{option.Id} {view.Width}x{view.Height} at {dpi/96:0.0}x: pixel coverage agreement {agreement:0.000} indicates displaced artwork/content.");
                    scaleChecks.Add(new{option.Id,width=view.Width,height=view.Height,scale=dpi/96,alphaCoverageAgreement=agreement});
                    if(refinement&&view.Width==460)scaled.Save(Path.Combine(output,$"{option.Id}-picker-{dpi:0}dpi.png"));
                }
            }
            var board=Board(option,views);
            using(var bitmap=Capture(board,96))bitmap.Save(Path.Combine(output,option.Id+"-three-sizes.png"));
            if(refinement&&refined.Contains(option.Id))Comparison(option,asset,output);
            string after=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source)));
            if(before!=after)throw new Exception("Source frame modified: "+option.Id);
            var plan=views[0].Plan;
            readme.AppendLine($"| {option.Label} | {plan.Method} | {plan.MinimumSize.Width:0.##} x {plan.MinimumSize.Height:0.##} | {plan.ReviewConcern} |");
            evidence.Add(new {option.Id,option.Label,sourceHash=before,sourceUnchanged=true,plan.Method,minimumArtworkSize=plan.MinimumSize,plan.ReviewConcern,
                samples=views.Select(v=>new {width=v.Width,height=v.Height,pieces=v.Plan.Pieces}).ToArray()});
        }
        // One gallery for each four styles, retaining the same physical frame scale in every cell.
        var ordered=StopwatchAppearanceCatalog.Frames.ToArray();
        for(int page=0;page<4;page++)
        {
            var sheet=new Canvas{Width=1060,Height=1120,Background=Backdrop};
            Add(sheet,Label($"Modular frame audit / {page*4+1}–{page*4+4}",28),36,20);
            Add(sheet,Label("460 × 480 per panel • fixed artwork scale • no stretching",15,Muted),36,62);
            for(int cell=0;cell<4;cell++)
            {
                var option=ordered[page*4+cell];
                double x=36+(cell%2)*510,y=105+(cell/2)*510;
                Add(sheet,Label($"{page*4+cell+1:00}  {option.Label}",18),x,y);
                Add(sheet,new FrameView(option,StopwatchAppearanceAssets.Shared.Get("frame",option.Id),new Size(460,480),false),x,y+26);
            }
            using var capture=Capture(sheet,96);capture.Save(Path.Combine(output,$"catalog-{page+1:00}.png"));
        }
        File.WriteAllText(Path.Combine(output,"verification.json"),JsonSerializer.Serialize(new {frameCount=entries.Length,sizeSamples=exported,additionalHighDpiRenders=highDpi,
            artworkPlacementsChecked=totalPieces,newRuntimeArtworkFiles=0,allSourcesUnchanged=true,uniformScale=.18,
            exportMethod="Native drawing at explicit 1.5x/2x scale into a 96-DPI target avoids double-scaling in the non-96-DPI offscreen target. Filename DPI values denote equivalent pixel resolution; PNG metadata stays 96 DPI.",scaleChecks,
            limitation="Native rendering and transform audit only. Review joins visually; physical monitor DPI remains untested.",frames=evidence},new JsonSerializerOptions{WriteIndented=true}));
        readme.AppendLine($"\n## Evidence\n\n`verification.json` records all 48 size recipes and their exact source/destination regions. {(refinement?"The four refined frames":"The three complex examples")} also render at explicit 1.5x and 2x resolution, equivalent to 144/192 DPI pixel dimensions. PNG metadata remains 96 DPI. This avoids double-scaling observed in the non-96-DPI offscreen capture path; it does not validate physical monitor DPI. Pixel coverage is compared with the 1x baseline to catch missing borders or displaced text. Final review boards use 1x resolution. Minimum sizes describe fixed-artwork clearance only, not usable content minimums. No performance claim is made.\n\nReproduce after building the harness with `Run-HiddenDesktop.ps1 -Mode '--frame-production-audit'` and a repository-local log path. Native fixtures run on a private desktop; no active-desktop interaction.\n");
        File.WriteAllText(Path.Combine(output,"README.md"),readme.ToString());
        Console.WriteLine($"Audited {entries.Length} frames, {exported} size samples, {highDpi} additional resolution renders and {totalPieces} fixed-scale artwork placements. Source hashes unchanged; no new runtime images.");
    }
    private static void Comparison(StopwatchAppearanceOption option,StopwatchAsset asset,string output)
    {
        using var before=new Bitmap(Path.Combine(Output,option.Id+"-three-sizes.png"));
        var board=new Canvas{Width=1060,Height=620,Background=Backdrop};
        Add(board,Label(option.Label+" / join refinement",28),36,20);
        Add(board,Label("BEFORE",17,Muted),36,76);Add(board,Label("REFINED",17,Muted),546,76);
        Add(board,new PreviousPicker(before){Width=460,Height=480},36,108);
        Add(board,new FrameView(option,asset,new Size(460,480)),546,108);
        using var render=Capture(board,96);render.Save(Path.Combine(output,option.Id+"-before-after.png"));
    }
    private sealed class PreviousPicker(Bitmap bitmap):Control
    {
        public override void Render(DrawingContext dc)=>dc.DrawImage(bitmap,new Rect(64,450,460,480),new Rect(0,0,460,480));
    }
    private static Canvas Board(StopwatchAppearanceOption option,FrameView[] views)
    {
        var board=new Canvas{Width=1480,Height=1030,Background=Backdrop};
        Add(board,Label(option.Label,38),48,36);
        Add(board,Label(views[0].Plan.Method,19,Muted),50,96);
        Add(board,Label("01  STOPWATCH · 420 × 132",18),64,154);Add(board,views[0],64,190);
        Add(board,Label("02  CLOCK PICKER · 460 × 480",18),64,408);Add(board,views[1],64,450);
        Add(board,Label("03  LARGER PANEL · 820 × 650",18),592,238);Add(board,views[2],592,280);
        Add(board,Label("Production shared renderer. Same source PNG at every size; no artwork stretching.",16,Muted),64,980);
        return board;
    }
    private static TextBlock Label(string text,double fontSize,IBrush? brush=null)=>new(){Text=text,FontSize=fontSize,Foreground=brush??Cream};
    private static void Add(Canvas canvas,Control child,double x,double y){Canvas.SetLeft(child,x);Canvas.SetTop(child,y);canvas.Children.Add(child);}
    private static RenderTargetBitmap Capture(Control control,double dpi)
    {
        var size=new Size(control.Width,control.Height);control.Measure(size);control.Arrange(new Rect(size));
        // The software offscreen backend double-applies non-96 target DPI to
        // vector/text operations and misplaces transformed image pieces. Apply
        // export scale once in the drawing context, keeping target DPI at 96.
        var target=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(size.Width*dpi/96),(int)Math.Ceiling(size.Height*dpi/96)),new Vector(96,96));
        if(control is FrameView view)
        {
            using var drawing=target.CreateDrawingContext();
            using var scale=drawing.PushTransform(Matrix.CreateScale(dpi/96,dpi/96));
            view.Render(drawing);
        }
        else target.Render(control);
        return target;
    }
    private static double AlphaAgreement(RenderTargetBitmap baseline,RenderTargetBitmap scaled,double scale)
    {
        byte[] Read(RenderTargetBitmap bitmap)
        {
            int stride=bitmap.PixelSize.Width*4,length=stride*bitmap.PixelSize.Height;
            var bytes=new byte[length];var pinned=GCHandle.Alloc(bytes,GCHandleType.Pinned);
            try{bitmap.CopyPixels(new PixelRect(bitmap.PixelSize),pinned.AddrOfPinnedObject(),length,stride);}finally{pinned.Free();}
            return bytes;
        }
        var a=Read(baseline);var b=Read(scaled);int intersection=0,union=0;
        for(int y=0;y<baseline.PixelSize.Height;y++)for(int x=0;x<baseline.PixelSize.Width;x++)
        {
            bool first=a[(y*baseline.PixelSize.Width+x)*4+3]>32;
            int sx=Math.Min(scaled.PixelSize.Width-1,(int)((x+.5)*scale));
            int sy=Math.Min(scaled.PixelSize.Height-1,(int)((y+.5)*scale));
            bool second=b[(sy*scaled.PixelSize.Width+sx)*4+3]>32;
            if(first||second)union++;if(first&&second)intersection++;
        }
        return union==0?0:(double)intersection/union;
    }
    private sealed class FrameView : Control
    {
        internal readonly ModularFrameCatalogPreview.Plan Plan;
        private readonly StopwatchAsset asset;
        private readonly bool content;
        internal FrameView(StopwatchAppearanceOption option,StopwatchAsset asset,Size size,bool content=true)
        {Width=size.Width;Height=size.Height;this.asset=asset;this.content=content;Plan=ModularFrameCatalogPreview.Create(option.Id,asset,size);}
        public override void Render(DrawingContext dc)
        {
            StopwatchAppearanceAssets.DrawFrame(dc,asset,new Rect(Bounds.Size),ModularFrameCatalogPreview.Scale);
            if(!content)return;
            void Text(string text,double x,double y,double fontSize,IBrush? color=null)
                =>dc.DrawText(new FormattedText(text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface(FontFamily.Default),fontSize,color??Cream),new Point(x,y));
            if(Height<200)
            {Text("00:44:27",132,49,29);return;}
            Text(Width<500?"Choose a clock":"Larger panel",50,60,27);
            Text("Fixed ornaments · repeated edges",50,102,15,Muted);
            string[] names={"Illustration","Coding","Reading","Study","Writing","Planning"};
            string[] times={"00:44:27","01:12:08","00:18:42","00:36:10","00:08:15","00:05:32"};
            for(int i=0;i<6;i++)
            {
                double y=157+i*43;Text((i+1).ToString(),50,y,17,Muted);Text(names[i],82,y,19);Text(times[i],Width-145,y,19);
                dc.DrawLine(new Pen(Brush.Parse("#26455865"),1),new Point(50,y+33),new Point(Width-50,y+33));
            }
        }
    }
}
