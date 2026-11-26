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
    private (string Text,StopwatchTypographyProfile Type,double Scale,double Width,IBrush Brush)? nameKey;
    private FormattedText? cachedName;
    private Geometry? nameEdge;
    private static readonly IBrush Navy=Brush.Parse("#071C2A"),Ivory=Brush.Parse("#FFF2D3"),Gold=Brush.Parse("#D4A44C");

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
        var parts=StopwatchFaceLayout.Create(bounds.Size,frame,scale,out var layout);
        var frameRect=parts.Frame;
        var content=parts.Content;
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
            var artwork=parts.Artwork(corner);
            using(context.PushOpacityMask(StopwatchOpacity.Mask(StopwatchAppearancePolicy.LayerOpacity(profile,StopwatchOpaqueLayers.Artwork)*.58),bounds))
            using(context.PushGeometryClip(mask))
            using(context.PushClip(parts.ArtworkClip))
                context.Custom(new StopwatchInkArtwork(artwork,background.IsDark));
        }
        using(context.PushOpacityMask(StopwatchOpacity.Mask(StopwatchAppearancePolicy.LayerOpacity(profile,StopwatchOpaqueLayers.Frame)),bounds))
            StopwatchAppearanceAssets.DrawFrame(context,frame,frameRect,.18*scale);

        var typography=StopwatchTypography.Resolve(profile.Typography);
        // This independently styled overlay owns its palette, including when an
        // older installation still has the shared text setting fixed to White.
        var palette=StopwatchTextPalette.For(profile.Background);
        var timeBrush=face.TypographyBrush??palette.Time;
        var nameBrush=face.TypographyBrush??palette.Name;
        double timeWidth=StopwatchTypography.Width(face.Text,typography,face.TextSize);
        var timeOrigin=state.IsClockMode
            ? StopwatchTypography.CenteredTimeOrigin(face.Text,frameRect.Center,typography,face.TextSize)
            : parts.Time(timeWidth).TopLeft;
        using(context.PushOpacityMask(StopwatchOpacity.Mask(StopwatchAppearancePolicy.LayerOpacity(profile,StopwatchOpaqueLayers.TimeText)),bounds))
        // ContentInsets already fit a safe rectangle inside the border. Text
        // needs only this cheap clip, not another full decorative stencil.
        using(context.PushClip(content))
            StopwatchTypography.DrawTime(context,face.Text,timeOrigin,typography,face.TextSize,timeBrush,palette.Edge);

        if(!state.IsClockMode)
        {
            double nameMaxWidth=parts.NameMaxWidth;
            var key=(face.ProjectName,typography,scale,nameMaxWidth,nameBrush);
            if(cachedName==null||nameKey!=key)
            {
                cachedName=parts.FormatName(face.ProjectName,typography,nameBrush);
                nameEdge=palette.Edge==null?null:cachedName.BuildGeometry(default);nameKey=key;
            }
            var name=cachedName;
            var nameBounds=parts.ProjectName(name);
            using(context.PushOpacityMask(StopwatchOpacity.Mask(StopwatchAppearancePolicy.LayerOpacity(profile,StopwatchOpaqueLayers.ProjectName)),bounds))
            using(context.PushClip(content))
            using(context.PushTransform(Matrix.CreateTranslation(nameBounds.X,nameBounds.Y)))
            {
                if(nameEdge!=null)context.DrawGeometry(null,new Pen(palette.Edge,1.6*scale,lineJoin:PenLineJoin.Round),nameEdge);
                context.DrawText(name,default);
            }
            using(context.PushOpacityMask(StopwatchOpacity.Mask(StopwatchAppearancePolicy.LayerOpacity(profile,StopwatchOpaqueLayers.StateMarkers)),bounds))
            {
                bool running=StopwatchAppearancePolicy.MarkerFor(state.IsRunning,state.IsClockMode)==StopwatchMarker.Running;
                var rect=parts.Marker(nameBounds);
                if(running)
                {
                    var triangle=new StreamGeometry();using(var g=triangle.Open())
                    {g.BeginFigure(rect.TopLeft,true);g.LineTo(new Point(rect.Right,rect.Center.Y));g.LineTo(rect.BottomLeft);g.EndFigure(true);}
                    context.DrawGeometry(palette.Running,null,triangle);
                }
                else
                {
                    context.DrawRectangle(palette.Paused,null,new Rect(rect.X,rect.Y,4*scale,rect.Height),.6*scale,.6*scale);
                    context.DrawRectangle(palette.Paused,null,new Rect(rect.X+8*scale,rect.Y,4*scale,rect.Height),.6*scale,.6*scale);
                }
            }
        }
        if(state.IsActive)
        {
            using(context.PushOpacityMask(StopwatchOpacity.Mask(StopwatchAppearancePolicy.LayerOpacity(profile,StopwatchOpaqueLayers.StateMarkers)),bounds))
                DrawActive(context,parts.ActiveBadge,scale,StopwatchTypography.Resolve("navigator-bold").NameFont);
        }
        if(face.ShowRec)
        {
            using(context.PushOpacityMask(StopwatchOpacity.Mask(StopwatchAppearancePolicy.LayerOpacity(profile,StopwatchOpaqueLayers.StateMarkers)),bounds))
            {
                context.DrawEllipse(palette.Recording,null,new Point(content.Right-33*scale,content.Y+10*scale),2*scale,2*scale);
                context.DrawText(StopwatchTypography.Format("REC",FontFamily.Default,FontWeight.SemiBold,7*scale,palette.Name),new Point(content.Right-27*scale,content.Y+5*scale));
            }
        }
    }

    private static void DrawActive(DrawingContext context,Rect rect,double scale,FontFamily font)
    {
        context.DrawRectangle(Navy,new Pen(Gold,1.5*scale),rect,5*scale,5*scale);
        context.DrawRectangle(null,new Pen(Ivory,.45*scale),rect.Deflate(3*scale),3*scale,3*scale);
        var text=StopwatchTypography.Format("ACTIVE",font,FontWeight.Bold,13*scale,Ivory);
        context.DrawText(text,new Point(rect.Center.X-text.Width/2,rect.Center.Y-text.Height/2));
    }
}
