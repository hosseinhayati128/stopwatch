using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace StopwatchOverlay.Desktop.Controls;

/// <summary>Reusable panel chrome. Frame selection is local; it does not synchronize overlay settings.</summary>
public sealed class StopwatchFrameSurface:Decorator
{
    public static readonly StyledProperty<string> FrameProperty=AvaloniaProperty.Register<StopwatchFrameSurface,string>(nameof(Frame),"engraved-filigree");
    public static readonly StyledProperty<double> ArtworkScaleProperty=AvaloniaProperty.Register<StopwatchFrameSurface,double>(nameof(ArtworkScale),.18,
        validate:value=>double.IsFinite(value)&&value>0);
    public static readonly StyledProperty<IBrush?> BackgroundProperty=Border.BackgroundProperty.AddOwner<StopwatchFrameSurface>();
    public static readonly StyledProperty<IBrush?> PatternProperty=AvaloniaProperty.Register<StopwatchFrameSurface,IBrush?>(nameof(Pattern));
    public IBrush? Pattern{get=>GetValue(PatternProperty);set=>SetValue(PatternProperty,value);}
    public StopwatchFrameSurface()=>this.Bind(PatternProperty,this.GetResourceObservable("DesktopPanelPattern"));
    public string Frame{get=>GetValue(FrameProperty);set=>SetValue(FrameProperty,value);}
    public double ArtworkScale{get=>GetValue(ArtworkScaleProperty);set=>SetValue(ArtworkScaleProperty,value);}
    public IBrush? Background{get=>GetValue(BackgroundProperty);set=>SetValue(BackgroundProperty,value);}
    static StopwatchFrameSurface()
    {
        AffectsMeasure<StopwatchFrameSurface>(FrameProperty,ArtworkScaleProperty,PaddingProperty);
        AffectsRender<StopwatchFrameSurface>(FrameProperty,ArtworkScaleProperty,BackgroundProperty,PatternProperty);
    }
    private StopwatchFrameLayout Layout(Size size)=>StopwatchFrameRenderer.GetLayout(StopwatchAppearanceAssets.Shared.Get("frame",Frame),size,ArtworkScale);
    private Thickness Insets(StopwatchFrameLayout layout)=>new(layout.ContentInsets.Left+Padding.Left,layout.ContentInsets.Top+Padding.Top,
        layout.ContentInsets.Right+Padding.Right,layout.ContentInsets.Bottom+Padding.Bottom);
    protected override Size MeasureOverride(Size availableSize)
    {
        var probe=new Size(double.IsFinite(availableSize.Width)&&availableSize.Width>0?availableSize.Width:Bounds.Width>0?Bounds.Width:460,
            double.IsFinite(availableSize.Height)&&availableSize.Height>0?availableSize.Height:Bounds.Height>0?Bounds.Height:480);
        var layout=Layout(probe);var insets=Insets(layout);
        Child?.Measure(probe.Deflate(insets));
        var desired=(Child?.DesiredSize??default).Inflate(insets);
        return new Size(Math.Max(desired.Width,layout.MinimumSize.Width),Math.Max(desired.Height,layout.MinimumSize.Height));
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        if(finalSize.Width>0&&finalSize.Height>0)
        {
            // Window content can be measured without finite constraints and
            // later arranged at a smaller client size. Grids must remeasure
            // their stars/auto rows for that real interior before arranging.
            var interior=new Rect(finalSize).Deflate(Insets(Layout(finalSize)));
            Child?.Measure(interior.Size);
            Child?.Arrange(interior);
            if(Child!=null)Child.ClipToBounds=true;
        }
        return finalSize;
    }
    public override void Render(DrawingContext context)
    {
        if(Bounds.Width<=0||Bounds.Height<=0)return;
        var layout=Layout(Bounds.Size);
        using(context.PushGeometryClip(layout.BackgroundClip))
        {
            context.DrawRectangle(Background,null,new Rect(Bounds.Size));
            context.DrawRectangle(Pattern,null,new Rect(Bounds.Size));
        }
        StopwatchFrameRenderer.Draw(context,StopwatchAppearanceAssets.Shared.Get("frame",Frame),new Rect(Bounds.Size),ArtworkScale);
    }
}
