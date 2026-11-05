using Avalonia;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;

// A per-frame design recipe catalog, deliberately outside the shipping application.
internal static class ModularFrameCatalogPreview
{
    internal const double Scale=.18;
    internal sealed record Piece(string Role,Rect Source,Rect Destination,bool Rotated=false,Rect? Clip=null,
        bool MirrorX=false,double FadeTop=0,double FadeBottom=0,double FadeLeft=0,double FadeRight=0);
    internal sealed record Plan(string Id,string Method,Size MinimumSize,string ReviewConcern,IReadOnlyList<Piece> Pieces);

    private sealed class Builder(Size size,Size sourceSize)
    {
        internal readonly List<Piece> Pieces=new();
        internal double W=>size.Width;
        internal double H=>size.Height;
        internal double SW=>sourceSize.Width;
        internal double SH=>sourceSize.Height;
        internal void Fixed(string role,Rect src,double x,double y)
            =>Pieces.Add(new(role,src,new Rect(x,y,src.Width*Scale,src.Height*Scale)));
        internal void Horizontal(string role,Rect src,double x,double end,double y)
        {
            var clip=new Rect(x,y,Math.Max(0,end-x),src.Height*Scale);
            for(double p=x;p<end-.00001;p+=src.Width*Scale)
                Pieces.Add(new(role,src,new Rect(p,y,src.Width*Scale,src.Height*Scale),false,clip));
        }
        internal void Vertical(string role,Rect src,double x,double y,double end,bool rotate=false)
        {
            double width=(rotate?src.Height:src.Width)*Scale,height=(rotate?src.Width:src.Height)*Scale;
            var clip=new Rect(x,y,width,Math.Max(0,end-y));
            for(double p=y;p<end-.00001;p+=height)
                Pieces.Add(new(role,src,new Rect(x,p,width,height),rotate,clip));
        }
        internal void Corners(double left,double top,double right,double bottom)
        {
            Fixed("top-left",new Rect(0,0,left,top),0,0);
            Fixed("top-right",new Rect(SW-right,0,right,top),W-right*Scale,0);
            Fixed("bottom-left",new Rect(0,SH-bottom,left,bottom),0,H-bottom*Scale);
            Fixed("bottom-right",new Rect(SW-right,SH-bottom,right,bottom),W-right*Scale,H-bottom*Scale);
        }
        internal void FeatherLast(double top=0,double bottom=0,double left=0,double right=0)
            =>Pieces[^1]=Pieces[^1] with {FadeTop=top,FadeBottom=bottom,FadeLeft=left,FadeRight=right};

        // Whole forward/reflected half-waves meet at identical source columns.
        // Width remainder goes into level trough sections, never a cut mid-wave.
        internal void Waves(string role,Rect halfWave,double left,double right,double y)
        {
            double width=W-left-right,half=halfWave.Width*Scale;
            int cycles=(int)Math.Floor(width/(2*half));
            double gap=(width-cycles*2*half)/(cycles+1),x=left;
            var trough=new Rect(halfWave.X,halfWave.Y,1,halfWave.Height);
            for(int i=0;i<=cycles;i++)
            {
                Horizontal(role+"-level",trough,x,x+gap,y);x+=gap;
                if(i==cycles)break;
                Pieces.Add(new(role+"-rise",halfWave,new Rect(x,y,half,halfWave.Height*Scale)));x+=half;
                Pieces.Add(new(role+"-fall",halfWave,new Rect(x,y,half,halfWave.Height*Scale),MirrorX:true));x+=half;
            }
        }
        internal void CenteredHorizontal(string role,Rect rail,Rect ornament,double left,double right,bool bottom)
        {
            double ow=ornament.Width*Scale,ox=(W-ow)/2;
            double railY=bottom?H-(SH-rail.Y)*Scale:rail.Y*Scale;
            Horizontal(role+"-left",rail,left,ox,railY);
            Horizontal(role+"-right",rail,ox+ow,W-right,railY);
            Fixed(role+"-ornament",ornament,ox,bottom?H-(SH-ornament.Y)*Scale:ornament.Y*Scale);
        }
    }

    internal static Plan Create(string id,StopwatchAsset asset,Size size)
    {
        double sw=asset.Bitmap.PixelSize.Width,sh=asset.Bitmap.PixelSize.Height;
        var b=new Builder(size,new Size(sw,sh));
        string method="Fixed corners and repeating edge strips";
        string concern="Inspect source texture joins and content padding before production integration.";
        double left=asset.SliceInsets.Left,right=asset.SliceInsets.Right,top=asset.SliceInsets.Top,bottom=asset.SliceInsets.Bottom;
        Size minimum=new((left+right)*Scale,(top+bottom)*Scale);
        if(id=="engraved-filigree")
        {
            var pieces=ModularFiligreePreview.Pieces(size).Select(p=>new Piece(p.Role,p.Source,p.Destination,p.Rotated,p.Clip)).ToArray();
            return new(id,"Previously accepted fixed ornaments and repeated rails",new Size(104.4,106.92),"Accepted visual direction; production integration remains pending.",pieces);
        }
        if(id is "slim-capsule" or "cloud-crest" or "tide-curve" or "vine-filigree")
        {
            left=right=id=="vine-filigree"?440:369;top=bottom=sh/2;
            minimum=new Size(2*left*Scale,sh*Scale);
            method="Fixed curved quarters; short connector strips";
            concern=id=="tide-curve"?"Wave phase at horizontal joins needs manual inspection; do not stretch the wave to hide a seam.":
                id=="vine-filigree"?"Leaf-to-rail joins need a tailored connector; top and bottom seals stay fixed.":
                "Quarter-curve joins and inner clipping need validation with real content.";
        }
        if(id=="royal-cartouche")
        {
            left=right=369;top=286;bottom=286;
            minimum=new Size((738+472)*Scale,724*Scale);
            method="Four fixed corners, four fixed crests, repeated rails";
            concern="Overlapping rails soften ornament exits; reserve header/ACTIVE-label space under the top crest.";
        }
        if(id=="admiralty")
        {
            top=232;bottom=280;minimum=new Size((left+right)*Scale,724*Scale);
            method="Fixed fan corners and side diamonds; repeated rails";
            concern="Fan tips are separated from the diamond flare; side ornaments require explicit content insets.";
        }
        if(id=="vine-filigree")
        {
            top=284;bottom=sh-top;
            method="Complete upper leaf curls; overlapping stem connectors";
            concern="Stem cuts move between curls, with feathered overlap onto fixed-width rails; validate content clearance.";
        }
        if(id=="tide-curve")
        {
            minimum=new Size((720+692)*Scale,sh*Scale);
            method="Whole reflected wave repeats; level trough connectors";
            concern="Wave length and thickness stay fixed; extra width is shared between level trough sections.";
        }
        if(id=="cloud-crest")minimum=new Size((2*left+350)*Scale,sh*Scale);
        if(id=="rope-binding")
        { method="Fixed knots and repeated rope sections";concern="Check braid phase and final clipped join at each knot."; }
        if(id=="chart-scroll")
        { method="Fixed roll caps; repeating paper roll and ragged edges";concern="Paper texture joins may need a dedicated seamless strip; caps must never be elongated."; }
        if(id=="open-corners")
        { method="Four fixed brackets; intentionally no connecting rails";concern="Keep the frame open; content clipping must not depend on a closed raster outline."; }
        if(id=="pixel-voyage")
        { concern="Snap native geometry to whole device pixels at production DPI; no fractional pixel-art stretching."; }
        if(id=="bound-logbook")
        { concern="Keep stitch spacing constant and conceal the partial repeat under corner fittings."; }
        if(size.Width<minimum.Width-.001||size.Height<minimum.Height-.001)
            throw new ArgumentException($"{id} needs at least {minimum.Width:0.##} x {minimum.Height:0.##} for its fixed artwork.");

        if(id is "royal-cartouche" or "vine-filigree")
        {
            bool royal=id=="royal-cartouche";
            var rail=new Rect(600,royal?130:96,96,48);
            var bottomRail=new Rect(600,royal?534:552,96,48);
            var topCrest=royal?new Rect(850,0,472,232):new Rect(1016,0,140,190);
            var bottomCrest=royal?new Rect(940,492,292,232):new Rect(1016,500,140,224);
            b.CenteredHorizontal("top",rail,topCrest,left*Scale,right*Scale,false);
            b.CenteredHorizontal("bottom",bottomRail,bottomCrest,left*Scale,right*Scale,true);
            double x=royal?15.3:13.5;
            double overlap=royal?8:7;
            b.Vertical("left-rail",rail,x,top*Scale-overlap,b.H-bottom*Scale+overlap,true);
            b.Vertical("right-rail",rail,b.W-x-rail.Height*Scale,top*Scale-overlap,b.H-bottom*Scale+overlap,true);
            b.Fixed("top-left",new Rect(0,0,left,top),0,0);b.FeatherLast(bottom:4);
            b.Fixed("top-right",new Rect(sw-right,0,right,top),b.W-right*Scale,0);b.FeatherLast(bottom:4);
            b.Fixed("bottom-left",new Rect(0,sh-bottom,left,bottom),0,b.H-bottom*Scale);b.FeatherLast(top:4);
            b.Fixed("bottom-right",new Rect(sw-right,sh-bottom,right,bottom),b.W-right*Scale,b.H-bottom*Scale);b.FeatherLast(top:4);
            if(royal)
            {
                var side=new Rect(0,286,170,152);
                b.Fixed("left-crest",side,0,(b.H-side.Height*Scale)/2);b.FeatherLast(top:2,bottom:2);
                b.Fixed("right-crest",new Rect(sw-170,286,170,152),b.W-170*Scale,(b.H-side.Height*Scale)/2);b.FeatherLast(top:2,bottom:2);
            }
        }
        else if(id=="admiralty")
        {
            b.Horizontal("top-rail",new Rect(500,0,160,232),left*Scale,b.W-right*Scale,0);
            b.Horizontal("bottom-rail",new Rect(500,492,160,232),left*Scale,b.W-right*Scale,b.H-232*Scale);
            // Outer gold comes from the straight upper side; the inner gold from
            // the straight section beside the diamond, avoiding both diagonals.
            b.Vertical("left-outer-rail",new Rect(80,160,32,60),80*Scale,top*Scale,b.H-bottom*Scale);
            b.Vertical("left-inner-rail",new Rect(112,290,38,60),112*Scale,top*Scale,b.H-bottom*Scale);
            b.Vertical("right-inner-rail",new Rect(2022,290,38,60),b.W-150*Scale,top*Scale,b.H-bottom*Scale);
            b.Vertical("right-outer-rail",new Rect(2060,160,32,60),b.W-112*Scale,top*Scale,b.H-bottom*Scale);
            b.Corners(left,top,right,bottom);
            b.Fixed("left-fan-tip",new Rect(120,232,left-120,32),120*Scale,232*Scale);
            b.Fixed("right-fan-tip",new Rect(sw-right,232,right-120,32),b.W-right*Scale,232*Scale);
            b.Fixed("left-diamond",new Rect(0,232,150,200),0,(b.H-200*Scale)/2);
            b.Fixed("right-diamond",new Rect(2022,232,150,200),b.W-150*Scale,(b.H-200*Scale)/2);
        }
        else if(id=="tide-curve")
        {
            const double cut=362;
            b.Waves("top-wave",new Rect(720,0,420,200),720*Scale,652*Scale,0);
            b.Waves("bottom-wave",new Rect(720,500,380,224),720*Scale,692*Scale,b.H-224*Scale);
            b.Vertical("left-edge",new Rect(0,355,180,14),0,cut*Scale,b.H-cut*Scale);
            b.Vertical("right-edge",new Rect(sw-180,355,180,14),b.W-180*Scale,cut*Scale,b.H-cut*Scale);
            b.Fixed("top-left",new Rect(0,0,720,cut),0,0);
            b.Fixed("bottom-left",new Rect(0,cut,720,cut),0,b.H-cut*Scale);
            // A short overlap blends the original right trough into the matching
            // repeat profile; the scroll ornament itself stays fully opaque.
            b.Horizontal("top-right-trough",new Rect(720,0,1,200),b.W-652*Scale,b.W-632*Scale,0);
            b.Horizontal("bottom-right-trough",new Rect(720,500,1,224),b.W-692*Scale,b.W-672*Scale,b.H-224*Scale);
            b.Fixed("top-right",new Rect(1520,0,652,cut),b.W-652*Scale,0);b.FeatherLast(left:3.6);
            b.Fixed("bottom-right",new Rect(1480,cut,692,cut),b.W-692*Scale,b.H-cut*Scale);b.FeatherLast(left:3.6);
        }
        else
        {
            if(id!="open-corners")
            {
                if(id=="cloud-crest")
                {
                    b.CenteredHorizontal("top",new Rect(500,0,180,232),new Rect(sw/2-175,0,350,232),left*Scale,right*Scale,false);
                    b.CenteredHorizontal("bottom",new Rect(500,sh-232,180,232),new Rect(sw/2-175,sh-232,350,232),left*Scale,right*Scale,true);
                }
                else
                {
                    b.Horizontal("top-edge",new Rect(left,0,sw-left-right,Math.Min(top,232)),left*Scale,b.W-right*Scale,0);
                    b.Horizontal("bottom-edge",new Rect(left,sh-Math.Min(bottom,232),sw-left-right,Math.Min(bottom,232)),left*Scale,b.W-right*Scale,b.H-Math.Min(bottom,232)*Scale);
                }
                bool curved=top+bottom>=sh-.001;
                var leftStrip=new Rect(0,curved?sh/2-7:top,left,curved?14:sh-top-bottom);
                var rightStrip=new Rect(sw-right,curved?sh/2-7:top,right,leftStrip.Height);
                b.Vertical("left-edge",leftStrip,0,top*Scale,b.H-bottom*Scale);
                b.Vertical("right-edge",rightStrip,b.W-right*Scale,top*Scale,b.H-bottom*Scale);
            }
            b.Corners(left,top,right,bottom);
        }
        return new(id,method,minimum,concern,b.Pieces);
    }

    internal static void Validate(Plan plan,StopwatchAsset asset)
    {
        foreach(var p in plan.Pieces)
        {
            if(p.Source.X<0||p.Source.Y<0||p.Source.Right>asset.Bitmap.PixelSize.Width||p.Source.Bottom>asset.Bitmap.PixelSize.Height)
                throw new Exception($"{plan.Id}/{p.Role}: source region outside artwork.");
            double sx=p.Destination.Width/(p.Rotated?p.Source.Height:p.Source.Width);
            double sy=p.Destination.Height/(p.Rotated?p.Source.Width:p.Source.Height);
            if(Math.Abs(sx-Scale)>1e-9||Math.Abs(sy-Scale)>1e-9)
                throw new Exception($"{plan.Id}/{p.Role}: artwork stretching detected.");
        }
    }
    internal static void Draw(DrawingContext dc,StopwatchAsset asset,Plan plan,Size size)
    {
        Validate(plan,asset);
        foreach(var p in plan.Pieces)
        {
            using var clip=dc.PushClip(p.Clip??new Rect(size));
            using var verticalFade=p.FadeTop+p.FadeBottom>0?dc.PushOpacityMask(Fade(p.FadeTop/p.Destination.Height,p.FadeBottom/p.Destination.Height,false),p.Destination):default;
            using var horizontalFade=p.FadeLeft+p.FadeRight>0?dc.PushOpacityMask(Fade(p.FadeLeft/p.Destination.Width,p.FadeRight/p.Destination.Width,true),p.Destination):default;
            if(p.Rotated)
            {
                using var transform=dc.PushTransform(new Matrix(0,1,-1,0,p.Destination.Right,p.Destination.Y));
                dc.DrawImage(asset.Bitmap,p.Source,new Rect(0,0,p.Source.Width*Scale,p.Source.Height*Scale));
            }
            else if(p.MirrorX)
            {
                using var transform=dc.PushTransform(new Matrix(-1,0,0,1,p.Destination.Right,p.Destination.Y));
                dc.DrawImage(asset.Bitmap,p.Source,new Rect(0,0,p.Destination.Width,p.Destination.Height));
            }
            else dc.DrawImage(asset.Bitmap,p.Source,p.Destination);
        }
    }
    private static LinearGradientBrush Fade(double start,double end,bool horizontal)=>new()
    {
        StartPoint=new RelativePoint(0,0,RelativeUnit.Relative),
        EndPoint=new RelativePoint(horizontal?1:0,horizontal?0:1,RelativeUnit.Relative),
        GradientStops=new GradientStops {new(start>0?Colors.Transparent:Colors.White,0),new(Colors.White,start),new(Colors.White,1-end),new(end>0?Colors.Transparent:Colors.White,1)}
    };
}
