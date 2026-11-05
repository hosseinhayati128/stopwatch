using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace StopwatchOverlay.Desktop.Controls;

/// <summary>Interior of the assembled modular frame, computed only on layout cache misses.</summary>
internal static class StopwatchFrameMask
{
    internal static Geometry Create(StopwatchAsset asset,Rect destination,double scale,bool openCorners)
    {
        var clip=StopwatchFrameRenderer.GetLayout(asset,destination.Size,.18*scale).BackgroundClip;
        return new GeometryGroup{FillRule=FillRule.NonZero,Children=new GeometryCollection{clip},Transform=new TranslateTransform(destination.X,destination.Y)};
    }

    internal static Geometry FromDrawing(Action<DrawingContext> paint,Size size,Thickness padding,bool openCorners,out bool exact)
    {
        exact=true;
        if(openCorners)return new RectangleGeometry(new Rect(size).Deflate(new Thickness(padding.Left/2,padding.Top*2/3)),6,6);
        double scale=Math.Min(2,Math.Min(2048/Math.Max(size.Width,size.Height),Math.Sqrt(2000000/(size.Width*size.Height))));
        int width=Math.Max(1,(int)Math.Ceiling(size.Width*scale)),height=Math.Max(1,(int)Math.Ceiling(size.Height*scale));
        using var bitmap=new RenderTargetBitmap(new PixelSize(width,height),new Vector(96,96));
        using(var dc=bitmap.CreateDrawingContext())
        {using var transform=dc.PushTransform(Matrix.CreateScale(scale,scale));paint(dc);}
        var pixels=new byte[width*height*4];var pin=GCHandle.Alloc(pixels,GCHandleType.Pinned);
        try{bitmap.CopyPixels(new PixelRect(0,0,width,height),pin.AddrOfPinnedObject(),pixels.Length,width*4);}finally{pin.Free();}
        var barrier=new bool[width*height];
        // Close subpixel sampling seams without changing the visible artwork.
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(pixels[(y*width+x)*4+3]>12)
            for(int yy=Math.Max(0,y-1);yy<=Math.Min(height-1,y+1);yy++)
            for(int xx=Math.Max(0,x-1);xx<=Math.Min(width-1,x+1);xx++)barrier[yy*width+xx]=true;
        var seen=new bool[barrier.Length];var queue=new int[barrier.Length];int read=0,write=0;
        int center=(height/2)*width+width/2;
        bool escaped=barrier[center];
        if(!escaped){seen[center]=true;queue[write++]=center;}
        while(read<write&&!escaped)
        {
            int index=queue[read++],x=index%width,y=index/width;
            if(x==0||y==0||x==width-1||y==height-1){escaped=true;break;}
            Enqueue(index-1);Enqueue(index+1);Enqueue(index-width);Enqueue(index+width);
        }
        void Enqueue(int index){if(!seen[index]&&!barrier[index]){seen[index]=true;queue[write++]=index;}}
        if(escaped||write==0)
        {
            // Tiny frames can lose closure to sampling; keep the fill inside
            // their conservative content bounds instead of leaking outside.
            exact=false;
            return new RectangleGeometry(new Rect(size).Deflate(padding));
        }
        using var spans=new SKPath{FillType=SKPathFillType.Winding};
        var geometry=new StreamGeometry();using(var path=geometry.Open())
        {
            // A wavy border can split a scanline into several interior runs.
            // Preserve each run instead of connecting its outermost extrema.
            // Coalesce identical runs vertically; NonZero unions their small
            // under-border overlap without introducing EvenOdd holes.
            path.SetFillRule(FillRule.NonZero);
            var active=new Dictionary<(int Left,int Right),int>();
            void Rectangle((int Left,int Right) span,int top,int bottom)
            {
                double x0=(span.Left-.75)/scale,x1=(span.Right+.75)/scale;
                double y0=(top-.75)/scale,y1=(bottom+.75)/scale;
                spans.AddRect(new SKRect((float)x0,(float)y0,(float)x1,(float)y1));
                path.BeginFigure(new Point(x0,y0),true);path.LineTo(new Point(x1,y0));
                path.LineTo(new Point(x1,y1));path.LineTo(new Point(x0,y1));path.EndFigure(true);
            }
            for(int y=0;y<height;y++)
            {
                var next=new Dictionary<(int Left,int Right),int>();
                for(int x=0;x<width;)
                {
                    if(!seen[y*width+x]){x++;continue;}
                    int start=x;while(x<width&&seen[y*width+x])x++;
                    var span=(start,x);next.Add(span,active.TryGetValue(span,out int top)?top:y);
                }
                foreach(var run in active)if(!next.ContainsKey(run.Key))Rectangle(run.Key,run.Value,y);
                active=next;
            }
            foreach(var run in active)Rectangle(run.Key,run.Value,height);
        }
        // The scanline union can contain hundreds of overlapping rectangles.
        // Flatten that union once on the layout cache miss: replaying all of
        // those intersections as a GPU clip on every tick is very expensive.
        using var simplified=spans.Simplify();
        using var winding=simplified?.ToWinding();
        return winding==null?geometry:StreamGeometry.Parse(winding.ToSvgPathData());
    }
}
