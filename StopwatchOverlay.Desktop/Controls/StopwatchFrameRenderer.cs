using Avalonia;
using Avalonia.Media;

namespace StopwatchOverlay.Desktop.Controls;

/// <summary>One assembled frame, reusable across paints and background choices.</summary>
public sealed class StopwatchFrameLayout
{
    internal StopwatchFrameLayout(Action<DrawingContext> paint,Geometry clip,Thickness insets,Size minimum,double scale,bool exactMask)
    {Paint=paint;BackgroundClip=clip;ContentInsets=insets;MinimumSize=minimum;ArtworkScale=scale;HasExactInteriorMask=exactMask;}
    internal Action<DrawingContext> Paint {get;}
    public Geometry BackgroundClip {get;}
    public Thickness ContentInsets {get;}
    public Size MinimumSize {get;}
    public double ArtworkScale {get;}
    public bool HasExactInteriorMask {get;}
}

/// <summary>Shared modular artwork and matching clips. No files are decoded or masks rebuilt on timer ticks.</summary>
public static class StopwatchFrameRenderer
{
    private readonly record struct Key(StopwatchAsset Asset,Size Size,double Scale,bool PanelFrame);
    private static readonly Dictionary<Key,LinkedListNode<(Key Key,StopwatchFrameLayout Layout)>> Cache=new();
    private static readonly LinkedList<(Key Key,StopwatchFrameLayout Layout)> Recent=new();
    private static readonly object Gate=new();
    private const int Capacity=32;
    public static int CachedLayoutCount {get{lock(Gate)return Cache.Count;}}

    public static StopwatchFrameLayout GetLayout(StopwatchAsset asset,Size size,double artworkScale,bool panelFrame=false)
    {
        if(!double.IsFinite(size.Width)||!double.IsFinite(size.Height)||size.Width<=0||size.Height<=0||!double.IsFinite(artworkScale)||artworkScale<=0)
            throw new ArgumentOutOfRangeException(nameof(size),"Frame dimensions and scale must be positive and finite.");
        panelFrame&=asset.Id=="engraved-filigree";
        var key=new Key(asset,size,artworkScale,panelFrame);
        lock(Gate)
        {
            if(Cache.TryGetValue(key,out var existing))
            {Recent.Remove(existing);Recent.AddFirst(existing);return existing.Value.Layout;}
            var minimum=panelFrame?new Size(104.4,131.4):Minimum(asset);
            double requestedRatio=artworkScale/.18;
            double ratio=Math.Min(requestedRatio,Math.Min(size.Width/minimum.Width,size.Height/minimum.Height));
            var logical=new Size(size.Width/ratio,size.Height/ratio);
            var plan=panelFrame?StopwatchFrameRecipe.PanelFiligree(asset,logical):StopwatchFrameRecipe.Create(asset.Id,asset,logical);
            StopwatchFrameRecipe.Validate(plan,asset);
            void Paint(DrawingContext dc)
            {
                using var scale=dc.PushTransform(Matrix.CreateScale(ratio,ratio));
                StopwatchFrameRecipe.Draw(dc,asset,plan,logical);
            }
            var padding=Padding(asset.Id);
            padding=new Thickness(padding.Left*ratio,padding.Top*ratio,padding.Right*ratio,padding.Bottom*ratio);
            var clip=StopwatchFrameMask.FromDrawing(Paint,size,padding,asset.Id=="open-corners",out bool exact);
            padding=FitContent(clip,size,padding,ratio);
            var layout=new StopwatchFrameLayout(Paint,clip,padding,new Size(minimum.Width*requestedRatio,minimum.Height*requestedRatio),.18*ratio,exact);
            var node=Recent.AddFirst((key,layout));Cache.Add(key,node);
            if(Cache.Count>Capacity){var oldest=Recent.Last!;Cache.Remove(oldest.Value.Key);Recent.RemoveLast();}
            return layout;
        }
    }

    public static void Draw(DrawingContext context,StopwatchAsset asset,Rect destination,double artworkScale)
    {
        if(destination.Width<=0||destination.Height<=0)return;
        var layout=GetLayout(asset,destination.Size,artworkScale);
        using var offset=context.PushTransform(Matrix.CreateTranslation(destination.X,destination.Y));
        layout.Paint(context);
    }

    private static Size Minimum(StopwatchAsset asset)=>asset.Id switch
    {
        "engraved-filigree"=>new(104.4,106.92),
        "slim-capsule"=>new(132.84,130.32),
        "cloud-crest"=>new(195.84,130.86),
        "tide-curve"=>new(254.16,130.32),
        "vine-filigree"=>new(183.6,130.32),
        "royal-cartouche"=>new(217.8,130.32),
        "admiralty"=>new(101.52,130.32),
        _=>new((asset.SliceInsets.Left+asset.SliceInsets.Right)*.18,(asset.SliceInsets.Top+asset.SliceInsets.Bottom)*.18)
    };

    private static Thickness Padding(string id)=>id switch
    {
        "royal-cartouche"=>new(44,38,44,38),
        "vine-filigree"=>new(48,28,48,40),
        "tide-curve"=>new(34,30,34,30),
        "cloud-crest"=>new(32,28,32,28),
        "slim-capsule"=>new(32,24,32,24),
        "admiralty"=>new(34,28,34,28),
        "chart-scroll"=>new(38,28,38,28),
        "rope-binding"=>new(30,28,30,28),
        "open-corners"=>new(18,18,18,18),
        _=>new(28,24,28,26)
    };

    private static Thickness FitContent(Geometry clip,Size size,Thickness initial,double unit)
    {
        double left=initial.Left,top=initial.Top,right=initial.Right,bottom=initial.Bottom;
        for(int step=0;step<256;step++)
        {
            var rect=new Rect(left,top,Math.Max(0,size.Width-left-right),Math.Max(0,size.Height-top-bottom));
            if(rect.Width<2*unit||rect.Height<2*unit)break;
            bool l=!clip.FillContains(rect.TopLeft)||!clip.FillContains(rect.BottomLeft);
            bool r=!clip.FillContains(rect.TopRight)||!clip.FillContains(rect.BottomRight);
            bool t=!clip.FillContains(new Point(rect.Center.X,rect.Top));
            bool b=!clip.FillContains(new Point(rect.Center.X,rect.Bottom));
            foreach(double fraction in new[]{.25,.75})
            {
                t|=!clip.FillContains(new Point(rect.X+rect.Width*fraction,rect.Top));
                b|=!clip.FillContains(new Point(rect.X+rect.Width*fraction,rect.Bottom));
            }
            if(!l&&!r&&!t&&!b)
            {
                for(int sample=1;sample<32;sample++)
                {
                    double x=rect.X+rect.Width*sample/32,y=rect.Y+rect.Height*sample/32;
                    t|=!clip.FillContains(new Point(x,rect.Top));b|=!clip.FillContains(new Point(x,rect.Bottom));
                    l|=!clip.FillContains(new Point(rect.Left,y));r|=!clip.FillContains(new Point(rect.Right,y));
                }
                if(!l&&!r&&!t&&!b)return new Thickness(left,top,right,bottom);
            }
            // Preserve vertical space when only decorative corners intrude.
            if(l)left+=unit;if(r)right+=unit;if(t)top+=unit;if(b)bottom+=unit;
        }
        return new Thickness(left,top,right,bottom);
    }
}
