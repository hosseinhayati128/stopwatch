using Avalonia;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Controls;

// Fixed ornaments and repeated rails from the existing Engraved Filigree PNG.
// Regions refer to the existing PNG. No new/cropped runtime image files are produced.
internal static class StopwatchFiligreeRecipe
{
    internal const double Scale=.18;
    internal sealed record Piece(string Role,Rect Source,Rect Destination,bool Rotated,Rect? Clip=null);

    internal static IReadOnlyList<Piece> Pieces(Size size)
    {
        const double sw=2172,sh=724,cornerWidth=290,topHeight=300,bottomY=430;
        double w=size.Width,h=size.Height,cw=cornerWidth*Scale,th=topHeight*Scale,bh=(sh-bottomY)*Scale;
        if(w<2*cw||h<th+bh)throw new ArgumentException("Panel is too small for fixed frame pieces.");
        var pieces=new List<Piece>();
        var top=new Rect(900,78,64,44);
        var bottom=new Rect(900,570,64,44);
        void Horizontal(string role,Rect source,Rect area)
        {
            double step=source.Width*Scale;
            for(double x=area.X;x<area.Right-.0001;x+=step)
                pieces.Add(new(role,source,new Rect(x,area.Y,step,source.Height*Scale),false,area));
        }
        void Vertical(string role,Rect area)
        {
            double step=top.Width*Scale;
            for(double y=area.Y;y<area.Bottom-.0001;y+=step)
                pieces.Add(new(role,top,new Rect(area.X,y,top.Height*Scale,step),true,area));
        }
        // Rails are tiled first; full-size ornaments are painted over the joins.
        Horizontal("top-rail",top,new Rect(cw,78*Scale,w-2*cw,44*Scale));
        Horizontal("bottom-rail",bottom,new Rect(cw,h-(sh-570)*Scale,w-2*cw,44*Scale));
        Vertical("left-rail",new Rect(8.1,24,44*Scale,h-54));
        Vertical("right-rail",new Rect(w-8.1-44*Scale,24,44*Scale,h-54));
        pieces.Add(new("top-left",new Rect(0,0,cornerWidth,topHeight),new Rect(0,0,cw,th),false));
        pieces.Add(new("top-right",new Rect(sw-cornerWidth,0,cornerWidth,topHeight),new Rect(w-cw,0,cw,th),false));
        pieces.Add(new("bottom-left",new Rect(0,bottomY,cornerWidth,sh-bottomY),new Rect(0,h-bh,cw,bh),false));
        pieces.Add(new("bottom-right",new Rect(sw-cornerWidth,bottomY,cornerWidth,sh-bottomY),new Rect(w-cw,h-bh,cw,bh),false));
        double badgeHeight=(bottomY-topHeight)*Scale;
        pieces.Add(new("left-ornament",new Rect(0,topHeight,140,bottomY-topHeight),new Rect(0,(h-badgeHeight)/2,140*Scale,badgeHeight),false));
        pieces.Add(new("right-ornament",new Rect(sw-140,topHeight,140,bottomY-topHeight),new Rect(w-140*Scale,(h-badgeHeight)/2,140*Scale,badgeHeight),false));
        return pieces;
    }

    internal static Geometry BackgroundMask(Size size)
        =>new RectangleGeometry(new Rect(16,21,size.Width-32,size.Height-48),8,8);

    internal static void Draw(DrawingContext dc,StopwatchAsset frame,Size size)
    {
        foreach(var piece in Pieces(size))
        {
            // Every piece has equal X/Y scale, including the clipped last rail tile.
            double sx=piece.Destination.Width/(piece.Rotated?piece.Source.Height:piece.Source.Width);
            double sy=piece.Destination.Height/(piece.Rotated?piece.Source.Width:piece.Source.Height);
            if(Math.Abs(sx-Scale)>1e-10||Math.Abs(sy-Scale)>1e-10)
                throw new InvalidOperationException("An artwork piece would stretch.");
            using var clip=dc.PushClip(piece.Clip??new Rect(size));
            if(piece.Rotated)
            {
                using var transform=dc.PushTransform(new Matrix(0,1,-1,0,piece.Destination.Right,piece.Destination.Y));
                dc.DrawImage(frame.Bitmap,piece.Source,new Rect(0,0,piece.Source.Width*Scale,piece.Source.Height*Scale));
            }
            else dc.DrawImage(frame.Bitmap,piece.Source,piece.Destination);
        }
    }
}
