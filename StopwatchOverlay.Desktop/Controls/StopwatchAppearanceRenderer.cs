using Avalonia;
using Avalonia.Media;

namespace StopwatchOverlay.Desktop.Controls;

public sealed record StopwatchPresentationState(bool IsActive,bool IsRunning,bool IsClockMode);

/// <summary>Layer rendering shared by the native clock and its appearance inspector.</summary>
public sealed class StopwatchAppearanceRenderer(StopwatchAppearanceAssets assets)
{
    private StopwatchFrameLayout? maskLayout;
    private Rect maskBounds;
    private Geometry? mask;
    private static readonly IBrush Navy=Brush.Parse("#071C2A"),Ivory=Brush.Parse("#FFF2D3"),Gold=Brush.Parse("#D4A44C"),
        Running=Brush.Parse("#087480"),Paused=Brush.Parse("#A45A12");

    public Size Measure(ThemedClockFace face,StopwatchAppearanceSettings profile,StopwatchPresentationState state)
    {
        var type=StopwatchTypography.Resolve(profile.Typography);
        double scale=face.TextSize/48;
        // The name, state and current digits never move the overlay anchor.
        double width=Math.Max(8.6*face.TextSize,StopwatchTypography.Width(face.Text,type,face.TextSize)+3.8*face.TextSize);
        var frame=assets.Get("frame",profile.Frame);
        var layout=StopwatchFrameRenderer.GetLayout(frame,new Size(width,132*scale),.18*scale);
        // Heavy scrolls/crests reserve their real depth without reducing the chosen font size.
        double height=Math.Max(132*scale,layout.ContentInsets.Top+layout.ContentInsets.Bottom+80*scale);
        return new Size(width,height+12*scale);
    }

    public void Render(DrawingContext context,Rect bounds,ThemedClockFace face,StopwatchAppearanceSettings profile,StopwatchPresentationState state)
    {
        if(bounds.Width<=0||bounds.Height<=0)return;
        double scale=face.TextSize/48;
        var frame=assets.Get("frame",profile.Frame);
        var background=assets.Get("background",profile.Background);
        var frameRect=new Rect(0,12*scale,bounds.Width,bounds.Height-12*scale);
        var layout=StopwatchFrameRenderer.GetLayout(frame,frameRect.Size,.18*scale);
        var content=frameRect.Deflate(layout.ContentInsets);
        if(mask==null||!ReferenceEquals(maskLayout,layout)||maskBounds!=frameRect)
        {
            mask=StopwatchFrameMask.Create(frame,frameRect,scale,profile.Frame=="open-corners");
            maskLayout=layout;maskBounds=frameRect;
        }
        using(context.PushOpacity(profile.BackgroundOpacity/100))
        using(context.PushGeometryClip(mask))
            StopwatchAppearanceAssets.DrawCover(context,background,frameRect);
        if(profile.Artwork!="clean")
        {
            bool corner=profile.Artwork=="corner-stamp";
            var artwork=corner?new Rect(28*scale,33*scale,48*scale,48*scale):new Rect(-30*scale,30*scale,116*scale,116*scale);
            using(context.PushOpacityMask(StopwatchOpacity.Mask(StopwatchAppearancePolicy.LayerOpacity(profile,StopwatchOpaqueLayers.Artwork)*.58),bounds))
            using(context.PushGeometryClip(mask))
            using(context.PushClip(new Rect(0,0,82*scale,bounds.Height)))
                context.Custom(new StopwatchInkArtwork(artwork,background.IsDark));
        }
        using(context.PushOpacityMask(StopwatchOpacity.Mask(StopwatchAppearancePolicy.LayerOpacity(profile,StopwatchOpaqueLayers.Frame)),bounds))
            StopwatchAppearanceAssets.DrawFrame(context,frame,frameRect,.18*scale);

        var typography=StopwatchTypography.Resolve(profile.Typography);
        IBrush text=face.UsesThemeTextColor?(background.IsDark?Ivory:Navy):face.TimerBrush;
        double timeWidth=StopwatchTypography.Width(face.Text,typography,face.TextSize);
        using(context.PushOpacityMask(StopwatchOpacity.Mask(StopwatchAppearancePolicy.LayerOpacity(profile,StopwatchOpaqueLayers.TimeText)),bounds))
        // ContentInsets already fit a safe rectangle inside the border. Text
        // needs only this cheap clip, not another full decorative stencil.
        using(context.PushClip(content))
            StopwatchTypography.DrawTime(context,face.Text,new Point((bounds.Width-timeWidth)/2,content.Top),typography,face.TextSize,text);

        var name=StopwatchTypography.Format(face.ProjectName,typography.NameFont,typography.NameWeight,(typography.DigitStyle==StopwatchDigitStyle.Pixel?14:20)*scale,text);
        name.MaxTextWidth=Math.Max(1,Math.Min(bounds.Width-100*scale,content.Width-23*scale));name.MaxTextHeight=32*scale;
        name.Trimming=TextTrimming.CharacterEllipsis;
        double nameWidth=Math.Min(name.WidthIncludingTrailingWhitespace,name.MaxTextWidth);
        double nameX=(bounds.Width-nameWidth-23*scale)/2+23*scale,nameY=content.Top+48*scale;
        using(context.PushOpacityMask(StopwatchOpacity.Mask(StopwatchAppearancePolicy.LayerOpacity(profile,StopwatchOpaqueLayers.ProjectName)),bounds))
        using(context.PushClip(content))
            context.DrawText(name,new Point(nameX,nameY));
        using(context.PushOpacityMask(StopwatchOpacity.Mask(StopwatchAppearancePolicy.LayerOpacity(profile,StopwatchOpaqueLayers.StateMarkers)),bounds))
        {
            bool running=StopwatchAppearancePolicy.MarkerFor(state.IsRunning,state.IsClockMode)==StopwatchMarker.Running;
            var rect=new Rect(nameX-23*scale,nameY+7*scale,13*scale,14*scale);
            if(running)
            {
                var triangle=new StreamGeometry();using(var g=triangle.Open())
                {g.BeginFigure(rect.TopLeft,true);g.LineTo(new Point(rect.Right,rect.Center.Y));g.LineTo(rect.BottomLeft);g.EndFigure(true);}
                context.DrawGeometry(Running,null,triangle);
            }
            else
            {
                context.DrawRectangle(Paused,null,new Rect(rect.X,rect.Y,4*scale,rect.Height),.6*scale,.6*scale);
                context.DrawRectangle(Paused,null,new Rect(rect.X+8*scale,rect.Y,4*scale,rect.Height),.6*scale,.6*scale);
            }
            if(state.IsActive)DrawActive(context,bounds.Width,scale,StopwatchTypography.Resolve("navigator-bold").NameFont);
        }
        if(face.ShowRec)
        {
            using(context.PushOpacityMask(StopwatchOpacity.Mask(StopwatchAppearancePolicy.LayerOpacity(profile,StopwatchOpaqueLayers.StateMarkers)),bounds))
            {
                context.DrawEllipse(Brush.Parse("#BD4637"),null,new Point(content.Right-33*scale,content.Y+10*scale),2*scale,2*scale);
                context.DrawText(StopwatchTypography.Format("REC",FontFamily.Default,FontWeight.SemiBold,7*scale,text),new Point(content.Right-27*scale,content.Y+5*scale));
            }
        }
    }

    private static void DrawActive(DrawingContext context,double width,double scale,FontFamily font)
    {
        var rect=new Rect(width/2-42*scale,8*scale,84*scale,22*scale);
        context.DrawRectangle(Navy,new Pen(Gold,1.5*scale),rect,5*scale,5*scale);
        context.DrawRectangle(null,new Pen(Ivory,.45*scale),rect.Deflate(3*scale),3*scale,3*scale);
        var text=StopwatchTypography.Format("ACTIVE",font,FontWeight.Bold,13*scale,Ivory);
        context.DrawText(text,new Point(rect.Center.X-text.Width/2,rect.Center.Y-text.Height/2));
    }
}
