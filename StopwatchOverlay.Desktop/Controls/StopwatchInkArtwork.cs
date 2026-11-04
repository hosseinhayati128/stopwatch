using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace StopwatchOverlay.Desktop.Controls;

/// <summary>A runtime ink treatment of the original Luffy atlas cell, without replacing its artwork.</summary>
internal sealed class StopwatchInkArtwork(Rect bounds,bool dark) : ICustomDrawOperation
{
    private static readonly Lazy<SKImage> Original=new(()=>
    {
        using var stream=AssetLoader.Open(new Uri("avares://StopwatchOverlay.Desktop/Assets/Pirate/icons.png"));
        return SKImage.FromEncodedData(stream);
    });
    public Rect Bounds {get;}=bounds;
    public void Dispose() { }
    public bool HitTest(Point point)=>false;
    public bool Equals(ICustomDrawOperation? other)=>other is StopwatchInkArtwork ink&&ink.Bounds==Bounds&&ink.darkInk==darkInk;
    private readonly bool darkInk=dark;
    public void Render(ImmediateDrawingContext context)
    {
        var feature=context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if(feature==null)return;
        using var lease=feature.Lease();
        var image=Original.Value;var color=darkInk?new SKColor(220,182,110):new SKColor(107,64,20);
        // Preserve original dark linework as ink; the original light fill becomes translucent.
        using var filter=SKColorFilter.CreateColorMatrix(new float[]{
            0,0,0,0,color.Red/255f,0,0,0,0,color.Green/255f,0,0,0,0,color.Blue/255f,
            -.2126f,-.7152f,-.0722f,1,0});
        using var paint=new SKPaint {ColorFilter=filter,IsAntialias=true,FilterQuality=SKFilterQuality.High};
        lease.SkCanvas.DrawImage(image,new SKRect(image.Width*.25f,image.Height*.5f,image.Width*.5f,image.Height),
            new SKRect((float)Bounds.X,(float)Bounds.Y,(float)Bounds.Right,(float)Bounds.Bottom),paint);
    }
}
