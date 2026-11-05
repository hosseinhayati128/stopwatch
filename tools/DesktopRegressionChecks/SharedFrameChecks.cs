using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;

internal static class SharedFrameChecks
{
    internal static void WaveRecesses()
    {
        var asset=StopwatchAppearanceAssets.Shared.Get("frame","tide-curve");
        var layout=StopwatchFrameRenderer.GetLayout(asset,new Size(820,650),.18);
        // These points lie outside the transparent interior between separate
        // wave crests. A min/max span per row wrongly bridges across them.
        Require(!layout.BackgroundClip.FillContains(new Point(145,23)),"Top wave recess was filled by joining separate interior spans.");
        Require(!layout.BackgroundClip.FillContains(new Point(272,625)),"Bottom wave recess was filled by joining separate interior spans.");
        foreach(double scale in new[]{1d,1.5,2})
        {
            var offset=new Point(17,23);
            using var bitmap=Capture(new Size(854*scale,696*scale),dc=>
            {
                using var zoom=dc.PushTransform(Matrix.CreateScale(scale,scale));
                using var shift=dc.PushTransform(Matrix.CreateTranslation(offset.X,offset.Y));
                using var clip=dc.PushGeometryClip(layout.BackgroundClip);
                dc.DrawRectangle(Brushes.Magenta,null,new Rect(0,0,820,650));
            });
            var pixels=Pixels(bitmap);
            byte Alpha(Point point)
            {
                int x=(int)((point.X+offset.X)*scale),y=(int)((point.Y+offset.Y)*scale);
                return pixels[(y*bitmap.PixelSize.Width+x)*4+3];
            }
            Require(Alpha(new Point(145,23))==0&&Alpha(new Point(272,625))==0,$"Wave fill leaked at {scale}x export scale.");
            Require(Alpha(new Point(410,325))==255,$"Translated wave interior disappeared at {scale}x export scale.");
        }
    }
    internal static void Layouts()
    {
        var issues=new List<string>();
        foreach(var option in StopwatchAppearanceCatalog.Frames)
        foreach(var size in new[]{new Size(420,132),new Size(460,480),new Size(820,650)})
        {
            var asset=StopwatchAppearanceAssets.Shared.Get("frame",option.Id);
            var layout=StopwatchFrameRenderer.GetLayout(asset,size,.18);
            if(!layout.HasExactInteriorMask)issues.Add($"{option.Id} at {size} fell back from the assembled frame mask.");
            Require(layout.BackgroundClip.FillContains(new Point(size.Width/2,size.Height/2)),"Mask omitted the center: "+option.Id);
            Require(!layout.BackgroundClip.FillContains(new Point(0,0)),"Mask leaked into an outer corner: "+option.Id);
            Require(ReferenceEquals(layout,StopwatchFrameRenderer.GetLayout(asset,size,.18)),"Identical frame geometry was rebuilt.");
            var inner=new Rect(size).Deflate(layout.ContentInsets);
            foreach(var point in new[]{inner.TopLeft,inner.TopRight,inner.BottomLeft,inner.BottomRight})
                if(!layout.BackgroundClip.FillContains(point))issues.Add($"{option.Id} {size}: content corner outside frame at {point}.");
            using var bitmap=Capture(size,dc=>
            {
                using(dc.PushGeometryClip(layout.BackgroundClip))dc.DrawRectangle(Brushes.Magenta,null,new Rect(size));
                StopwatchFrameRenderer.Draw(dc,asset,new Rect(size),.18);
            });
            Require(Pixels(bitmap).Any(value=>value>0),"Empty shared frame.");
        }
        foreach(var option in StopwatchAppearanceCatalog.Frames)
        foreach(double scale in new[]{.06,.18,.45})
        {
            var asset=StopwatchAppearanceAssets.Shared.Get("frame",option.Id);
            var small=StopwatchFrameRenderer.GetLayout(asset,new Size(48,24),scale);
            Require(small.ArtworkScale<=scale&&small.ArtworkScale>0,"Small frame did not uniformly fit its artwork.");
        }
        Require(StopwatchFrameRenderer.CachedLayoutCount<=32,"Resizing grew the frame cache without a bound.");
        Require(issues.Count==0,string.Join("\n",issues));
    }
    internal static void ReviewedArtwork()
    {
        foreach(string id in new[]{"tide-curve","admiralty","vine-filigree","royal-cartouche"})
        foreach(var size in new[]{new Size(420,132),new Size(460,480),new Size(820,650)})
        {
            var asset=StopwatchAppearanceAssets.Shared.Get("frame",id);
            var rect=new Rect(new Point(17,23),size);
            using var actual=Capture(new Size(size.Width+34,size.Height+46),dc=>StopwatchAppearanceAssets.DrawFrame(dc,asset,rect,.18));
            using var expected=Capture(actual.Size,dc=>
            {
                using var shift=dc.PushTransform(Matrix.CreateTranslation(rect.X,rect.Y));
                var plan=ModularFrameCatalogPreview.Create(id,asset,size);
                ModularFrameCatalogPreview.Draw(dc,asset,plan,size);
            });
            var a=Pixels(actual);var b=Pixels(expected);
            long difference=0;int covered=0;
            for(int i=0;i<a.Length;i+=4)
            {
                if(a[i+3]>16||b[i+3]>16)covered++;
                for(int c=0;c<4;c++)difference+=Math.Abs(a[i+c]-b[i+c]);
            }
            double mean=covered==0?255:(double)difference/(covered*4);
            Require(mean<2,$"Production {id} at {size} differs from the reviewed modular frame (mean visible-pixel error {mean:0.00}).");
        }
    }

    internal static RenderTargetBitmap Capture(Size size,Action<DrawingContext> draw)
    {
        var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(size.Width),(int)Math.Ceiling(size.Height)),new Vector(96,96));
        using(var dc=bitmap.CreateDrawingContext())draw(dc);
        return bitmap;
    }
    internal static byte[] Pixels(Bitmap bitmap)
    {
        int stride=bitmap.PixelSize.Width*4;var bytes=new byte[stride*bitmap.PixelSize.Height];
        var pin=GCHandle.Alloc(bytes,GCHandleType.Pinned);
        try{bitmap.CopyPixels(new PixelRect(bitmap.PixelSize),pin.AddrOfPinnedObject(),bytes.Length,stride);}finally{pin.Free();}
        return bytes;
    }
    internal static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
