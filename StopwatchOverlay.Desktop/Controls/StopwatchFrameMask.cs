using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;

namespace StopwatchOverlay.Desktop.Controls;

/// <summary>Interior silhouettes follow the actual transparent frame, including curved ends.
/// Source pixels are inspected once; no bitmap is edited or decoded on timer ticks.</summary>
internal static class StopwatchFrameMask
{
    private static readonly Dictionary<StopwatchAsset,Point[]> Outlines=new();

    internal static Geometry Create(StopwatchAsset asset,Rect destination,double scale,bool openCorners)
    {
        if(openCorners)return new RectangleGeometry(destination.Deflate(new Thickness(9*scale,12*scale)),6*scale,6*scale);
        if(!Outlines.TryGetValue(asset,out var points))Outlines[asset]=points=ReadInterior(asset);
        var source=asset.Source;var inset=asset.SliceInsets;
        double factor=Math.Min(.18*scale,Math.Min(destination.Width/(inset.Left+inset.Right),destination.Height/(inset.Top+inset.Bottom)));
        double Map(double value,double length,double leading,double trailing,double target)
        {
            if(inset==default)return value/length*target;
            if(value<=leading)return value*factor;
            if(value>=length-trailing)return target-(length-value)*factor;
            return leading*factor+(value-leading)/(length-leading-trailing)*(target-(leading+trailing)*factor);
        }
        var geometry=new StreamGeometry();using(var path=geometry.Open())
        {
            for(int i=0;i<points.Length;i++)
            {
                var point=new Point(destination.X+Map(points[i].X-source.X,source.Width,inset.Left,inset.Right,destination.Width),
                    destination.Y+Map(points[i].Y-source.Y,source.Height,inset.Top,inset.Bottom,destination.Height));
                if(i==0)path.BeginFigure(point,true);else path.LineTo(point);
            }
            path.EndFigure(true);
        }
        return geometry;
    }

    private static Point[] ReadInterior(StopwatchAsset asset)
    {
        int width=asset.Bitmap.PixelSize.Width,height=asset.Bitmap.PixelSize.Height;
        var pixels=new byte[width*height*4];var buffer=Marshal.AllocHGlobal(pixels.Length);
        try{asset.Bitmap.CopyPixels(new PixelRect(0,0,width,height),buffer,pixels.Length,width*4);Marshal.Copy(buffer,pixels,0,pixels.Length);}
        finally{Marshal.FreeHGlobal(buffer);}
        var seen=new bool[width*height];var queue=new int[seen.Length];int read=0,write=0;
        int center=(int)asset.Source.Center.Y*width+(int)asset.Source.Center.X;
        if(pixels[center*4+3]>32)throw new InvalidOperationException("Frame interior is not transparent.");
        queue[write++]=center;seen[center]=true;
        var left=Enumerable.Repeat(width,height).ToArray();var right=new int[height];
        while(read<write)
        {
            int index=queue[read++],x=index%width,y=index/width;
            if(x==0||y==0||x==width-1||y==height-1)throw new InvalidOperationException("Frame has an open inner boundary.");
            left[y]=Math.Min(left[y],x);right[y]=Math.Max(right[y],x);
            Enqueue(index-1);Enqueue(index+1);Enqueue(index-width);Enqueue(index+width);
        }
        void Enqueue(int next){if(!seen[next]&&pixels[next*4+3]<=32){seen[next]=true;queue[write++]=next;}}
        var result=new List<Point>();
        for(int y=0;y<height;y++)if(left[y]<width)result.Add(new Point(left[y]-.8,y));
        for(int y=height-1;y>=0;y--)if(left[y]<width)result.Add(new Point(right[y]+.8,y));
        return result.ToArray();
    }
}
