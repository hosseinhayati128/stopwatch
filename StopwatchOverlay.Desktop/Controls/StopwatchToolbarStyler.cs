using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace StopwatchOverlay.Desktop.Controls;

/// <summary>Decorates existing action Buttons without replacing their handlers or content.</summary>
public static class StopwatchToolbarStyler
{
    private sealed record ButtonHome(Button Button,Panel Parent,int Index,IControlTemplate? Template,bool ClipToBounds);
    private sealed record SavedToolbar(Control? OriginalChild,List<ButtonHome> Buttons,
        StopwatchToolbarArtwork Artwork,StopwatchToolbarPanel Panel);
    private static readonly ConditionalWeakTable<Border,SavedToolbar> Saved=new();
    private static readonly FuncControlTemplate<Button> Template=new((button,scope)=>new Border
    {
        Background=Brushes.Transparent,
        Child=new StopwatchActionGlyph(button)
    });

    public static void Apply(Control toolbarRoot,IReadOnlyList<Button> actionButtons,StopwatchAppearanceSettings? profile,
        StopwatchPresentationState state,double selectionOpacity)
    {
        if(toolbarRoot is not Border surface)return;
        if(profile==null)
        {
            if(!Saved.TryGetValue(surface,out var previous))return;
            surface.Child=null;
            foreach(var home in previous.Buttons)
            {
                previous.Panel.Children.Remove(home.Button);
                ReleasePresenter(home.Button);
                if(home.Template!=null)home.Button.Template=home.Template;else home.Button.ClearValue(Button.TemplateProperty);
                home.Button.ClipToBounds=home.ClipToBounds;
                home.Parent.Children.Insert(Math.Min(home.Index,home.Parent.Children.Count),home.Button);
            }
            surface.Child=previous.OriginalChild;StopwatchOpacity.Apply(surface,1);
            Saved.Remove(surface);return;
        }
        if(!Saved.TryGetValue(surface,out var saved))
        {
            var homes=actionButtons.Where(b=>b.Parent is Panel).Select(b=>new ButtonHome(b,(Panel)b.Parent!,((Panel)b.Parent!).Children.IndexOf(b),b.Template,b.ClipToBounds)).ToList();
            var old=surface.Child;surface.Child=null;
            var panel=new StopwatchToolbarPanel();var artwork=new StopwatchToolbarArtwork();
            foreach(var home in homes){home.Parent.Children.Remove(home.Button);ReleasePresenter(home.Button);panel.Children.Add(home.Button);home.Button.Template=Template;}
            var layers=new Grid();layers.Children.Add(artwork);layers.Children.Add(panel);surface.Child=layers;
            saved=new SavedToolbar(old,homes,artwork,panel);Saved.Add(surface,saved);
        }
        surface.Background=Brushes.Transparent;surface.BorderThickness=new Thickness(0);surface.Padding=new Thickness(0);
        StopwatchOpacity.Apply(surface,StopwatchAppearancePolicy.LayerOpacity(profile,StopwatchOpaqueLayers.HoverControls)*selectionOpacity);
        saved.Artwork.Choice=profile.HoverControls;
        saved.Panel.Choice=profile.HoverControls;saved.Panel.InvalidateArrange();
        for(int index=0;index<actionButtons.Count;index++)
        {
            var button=actionButtons[index];
            button.Width=button.Height=34;button.MinWidth=button.MinHeight=0;button.Padding=new Thickness(0);
            button.Background=Brushes.Transparent;button.BorderThickness=new Thickness(0);button.Opacity=1;
            // The glyph already stays inside its hit target. A Button clip nested under
            // the preview's Viewbox and opacity mask can incorrectly clip the entire glyph.
            button.ClipToBounds=false;
            bool dark=profile.HoverControls is "compass-dials" or "bronze-keys" or "porcelain-keys" ||
                index==1&&profile.HoverControls is "classic-brass" or "floating-buttons" or "pixel-controls";
            button.Foreground=Brush.Parse(dark?"#091B25":profile.HoverControls=="pixel-controls"?"#F6D77A":"#FFF2D3");
            StopwatchActionGlyph.SetState(button,index,state.IsRunning||state.IsClockMode,profile.HoverControls=="pixel-controls",
                profile.HoverControls is "vine-filigree" or "royal-cartouche");
        }
        if(actionButtons.Count>=3)
        {
            AutomationProperties.SetName(actionButtons[0],"Close timer");
            AutomationProperties.SetName(actionButtons[1],state.IsClockMode?"Clock mode cannot be paused":state.IsRunning?"Pause timer":"Resume timer");
            AutomationProperties.SetName(actionButtons[2],"Reset timer");
        }
    }

    private static void ReleasePresenter(Button button)
    {
        foreach(var presenter in button.GetVisualDescendants().OfType<ContentPresenter>().ToArray())
            if(ReferenceEquals(presenter.Content,button.Content)){presenter.Content=null;presenter.UpdateChild();}
    }
}

internal sealed class StopwatchActionGlyph(Button owner) : Control
{
    private sealed record GlyphState(int Action,bool Running,bool Pixel,bool Compact);
    private static readonly ConditionalWeakTable<Button,GlyphState> States=new();
    private static readonly Geometry Play=StreamGeometry.Parse("M6,3 L17,10 L6,17 Z");
    private static readonly Geometry Reset=StreamGeometry.Parse("M4,6 A7,7 0 1 1 3,12 M4,2 L4,6 L8,6");
    private static readonly Geometry PixelClose=Pixels("10000001","01000010","00100100","00011000","00011000","00100100","01000010","10000001");
    private static readonly Geometry PixelPause=Pixels("01100110","01100110","01100110","01100110","01100110","01100110","01100110","01100110");
    private static readonly Geometry PixelPlay=Pixels("01000000","01100000","01111000","01111110","01111110","01111000","01100000","01000000");
    private static readonly Geometry PixelReset=Pixels("00011100","00100010","01000001","11110001","01000001","00100010","00011100","00000000");
    private static Geometry Pixels(params string[] rows)
    {
        var geometry=new StreamGeometry();
        using(var path=geometry.Open())
        {
            path.SetFillRule(FillRule.NonZero);
            for(int y=0;y<rows.Length;y++)for(int x=0;x<rows[y].Length;x++)
            {
                if(rows[y][x]!='1')continue;
                path.BeginFigure(new Point(x*2,y*2),true);
                path.LineTo(new Point(x*2+2,y*2));path.LineTo(new Point(x*2+2,y*2+2));
                path.LineTo(new Point(x*2,y*2+2));path.EndFigure(true);
            }
        }
        return geometry;
    }
    internal static void SetState(Button button,int action,bool running,bool pixel,bool compact)
    {
        States.Remove(button);States.Add(button,new(action,running,pixel,compact));
        foreach(var glyph in button.GetVisualDescendants().OfType<StopwatchActionGlyph>())glyph.InvalidateVisual();
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {base.OnAttachedToVisualTree(e);owner.PropertyChanged+=OwnerChanged;}
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {owner.PropertyChanged-=OwnerChanged;base.OnDetachedFromVisualTree(e);}
    private void OwnerChanged(object? sender,AvaloniaPropertyChangedEventArgs e)=>InvalidateVisual();
    public override void Render(DrawingContext context)
    {
        if(!States.TryGetValue(owner,out var state))return;
        var brush=owner.Foreground??Brushes.White;var area=new Rect(Bounds.Size);
        double size=state.Pixel||state.Compact?16:20;
        if(owner.IsPointerOver||owner.IsFocused)context.DrawRectangle(new SolidColorBrush(Colors.White,.13),null,area.Deflate(2),5,5);
        using(context.PushOpacity(owner.IsEffectivelyEnabled?1:.35))
        using(context.PushTransform(Matrix.CreateTranslation((area.Width-size)/2,(area.Height-size)/2)))
        {
            if(state.Pixel)
            {
                var pixels=state.Action==0?PixelClose:state.Action==1?(state.Running?PixelPause:PixelPlay):PixelReset;
                // Fill the whole glyph once so adjacent blocks have no antialias seams.
                using(context.PushTransform(Matrix.CreateTranslation(8-pixels.Bounds.Center.X,8-pixels.Bounds.Center.Y)))
                    context.DrawGeometry(brush,null,pixels);
                return;
            }
            // Compact ornate keys use 16 DIP; larger faces retain readable 20 DIP icons.
            // Cache the paths; repainting hover/focus never reparses SVG geometry.
            using(context.PushTransform(Matrix.CreateScale(size/20,size/20)))
            {
                var pen=new Pen(brush,2.5,lineCap:PenLineCap.Round,lineJoin:PenLineJoin.Round);
                if(state.Action==0){context.DrawLine(pen,new Point(4,4),new Point(16,16));context.DrawLine(pen,new Point(16,4),new Point(4,16));}
                else if(state.Action==1)
                {
                    if(state.Running){context.DrawRectangle(brush,null,new Rect(4.5,3,3,14),.6,.6);context.DrawRectangle(brush,null,new Rect(12.5,3,3,14),.6,.6);}
                    // Center the ink, with a small optical correction for the triangle's weight.
                    else using(context.PushTransform(Matrix.CreateTranslation(10.5-Play.Bounds.Center.X,10-Play.Bounds.Center.Y)))
                        context.DrawGeometry(brush,null,Play);
                }
                else using(context.PushTransform(Matrix.CreateTranslation(10-Reset.Bounds.Center.X,10-Reset.Bounds.Center.Y)))
                    context.DrawGeometry(null,pen,Reset);
            }
        }
    }
}

internal sealed class StopwatchToolbarPanel : Panel
{
    public string Choice {get;set;}="filigree-keys";
    protected override Size MeasureOverride(Size availableSize)
    {
        foreach(var child in Children)child.Measure(new Size(34,34));
        return new Size(236,56);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        var buttons=Children.Where(c=>c.IsVisible).ToArray();
        var asset=StopwatchAppearanceAssets.Shared.Get("control",Choice);var target=StopwatchAppearanceAssets.Fit(asset,finalSize);
        for(int i=0;i<buttons.Length;i++)
        {
            var anchor=asset.ControlAnchors[Math.Min(i,2)];
            buttons[i].Arrange(new Rect(target.X+target.Width*anchor.X-17,target.Y+target.Height*anchor.Y-17,34,34));
        }
        return finalSize;
    }
}

internal sealed class StopwatchToolbarArtwork : Control
{
    private string choice="filigree-keys";
    public string Choice {get=>choice;set{choice=value;InvalidateVisual();}}
    public StopwatchToolbarArtwork(){IsHitTestVisible=false;}
    public override void Render(DrawingContext context)
    {
        if(Bounds.Width<=0||Bounds.Height<=0)return;
        var asset=StopwatchAppearanceAssets.Shared.Get("control",choice);
        context.DrawImage(asset.Bitmap,asset.Source,StopwatchAppearanceAssets.Fit(asset,Bounds.Size));
    }
}
