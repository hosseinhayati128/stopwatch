using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace StopwatchOverlay.Desktop.Controls;

public sealed class TodoHoldMeter : Control
{
    public static readonly StyledProperty<double> ValueProperty=AvaloniaProperty.Register<TodoHoldMeter,double>(nameof(Value));
    public double Value {get=>GetValue(ValueProperty);set=>SetValue(ValueProperty,value);}
    public bool Pixel {get;set;}
    public IBrush Ink {get;set;}=TodoVisuals.Navy;
    static TodoHoldMeter()=>AffectsRender<TodoHoldMeter>(ValueProperty);
    public override void Render(DrawingContext c)
    {
        double value=Math.Clamp(Value,0,1);
        if(Pixel)
        {
            double width=Math.Max(0,(Bounds.Width-18)/10);
            for(int i=0;i<10;i++)c.DrawRectangle(value>i/10.0?Ink:null,new Pen(Ink,.7),new Rect(i*(width+2),0,width,Bounds.Height));
        }
        else
        {
            c.DrawRectangle(Brush.Parse("#40876D40"),null,new Rect(Bounds.Size),3,3);
            if(value>0)c.DrawRectangle(Ink,null,new Rect(0,0,Bounds.Width*value,Bounds.Height),3,3);
        }
    }
}
