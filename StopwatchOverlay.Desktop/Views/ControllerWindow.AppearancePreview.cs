using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using System.Linq;

namespace StopwatchOverlay.Desktop.Views;

public partial class ControllerWindow
{
    private Size? _appearancePreviewSize;
    internal RenderTargetBitmap RenderAppearancePreview(AppSettings settings)
    {
        if(!_previewMode)throw new InvalidOperationException("Only synthetic controllers can render settings previews.");
        _updatingUi=true;
        _settings.ThemeMode=settings.ThemeMode;
        _settings.NikaBackground=settings.NikaBackground;
        _settings.NikaTypography=settings.NikaTypography;
        _settings.NikaSidebarArtwork=settings.NikaSidebarArtwork;
        _appearancePreviewSize=new Size(1120,736);
        Width=1120;Height=736;
        ApplyControllerTheme();ApplyTemplate();
        var root=(Control)Content!;
        TimerRailList.ItemsPanel=new FuncTemplate<Panel?>(()=>new StackPanel());
        root.Measure(new Size(1120,736));root.Arrange(new Rect(0,0,1120,736));
        // Scroll presenters bind their owner's content on visual attachment; this window is never shown.
        for(int pass=0;pass<3;pass++)
        {
            foreach(var presenter in root.GetVisualDescendants().OfType<ScrollContentPresenter>().ToArray())
                if(presenter.Content==null&&presenter.TemplatedParent is ScrollViewer owner)
                {
                    presenter.Content=owner.Content;presenter.ContentTemplate=owner.ContentTemplate;presenter.UpdateChild();
                }
            foreach(var control in root.GetVisualDescendants().OfType<Control>().Reverse())control.InvalidateMeasure();
            root.InvalidateMeasure();
            root.Measure(new Size(1120,736));root.Arrange(new Rect(0,0,1120,736));
        }
        var frame=ControllerFrame;
        frame.Measure(new Size(frame.Width,frame.Height));frame.Arrange(new Rect(0,0,frame.Width,frame.Height));
        // Snapshot before the Viewbox shrinks it, avoiding nested draw-list clipping.
        var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(frame.Width),(int)Math.Ceiling(frame.Height)),new Vector(96,96));
        bitmap.Render(frame);
        return bitmap;
    }
}
