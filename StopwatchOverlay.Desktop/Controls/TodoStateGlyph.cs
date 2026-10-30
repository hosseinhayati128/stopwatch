using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace StopwatchOverlay.Desktop.Controls;

/// <summary>Resolution-independent symbols for the approved empty and completed states.</summary>
public sealed class TodoStateGlyph : Control
{
    public bool Complete { get; init; }
    public bool Pixel { get; init; }
    public IBrush Ink { get; init; } = TodoVisuals.Navy;
    public override void Render(DrawingContext context)
    {
        var rect=new Rect(Bounds.Size).Deflate(3);
        var pen=new Pen(Ink,2.5);
        if(Complete)
        {
            if(Pixel)context.DrawGeometry(null,pen,TodoFrame.Outline(rect));
            else context.DrawEllipse(null,pen,rect);
            var mark=new StreamGeometry();using(var c=mark.Open())
            {c.BeginFigure(new Point(rect.Left+rect.Width*.27,rect.Top+rect.Height*.53),false);c.LineTo(new Point(rect.Left+rect.Width*.44,rect.Top+rect.Height*.69));c.LineTo(new Point(rect.Left+rect.Width*.75,rect.Top+rect.Height*.32));c.EndFigure(false);}
            context.DrawGeometry(null,new Pen(Ink,4),mark);
        }
        else
        {
            if(Pixel)context.DrawGeometry(null,pen,TodoFrame.Outline(rect));
            else context.DrawRectangle(null,pen,rect,5,5);
            for(int i=0;i<3;i++)
            {
                double y=rect.Top+rect.Height*(.20+i*.26);
                context.DrawRectangle(null,new Pen(Ink,2),new Rect(rect.Left+rect.Width*.17,y,rect.Width*.15,rect.Height*.15));
                context.DrawLine(pen,new Point(rect.Left+rect.Width*.43,y+rect.Height*.075),new Point(rect.Right-rect.Width*.17,y+rect.Height*.075));
            }
        }
    }
}
