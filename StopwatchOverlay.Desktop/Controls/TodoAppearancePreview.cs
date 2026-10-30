using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace StopwatchOverlay.Desktop.Controls;

/// <summary>Pure sample UI. Never opens a vault, writes settings, or starts synchronization.</summary>
public sealed class TodoAppearancePreview : UserControl
{
    private RenderTargetBitmap? _snapshot;
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _snapshot?.Dispose();_snapshot=null;
    }
    public void Apply(TodoAppearanceSettings appearance)
    {
        appearance.Normalize();
        var p=appearance.Current;
        var root=new Grid { RowDefinitions=new RowDefinitions("64,*,64"), Background=TodoVisuals.Paper(p), Width=384,Height=544, IsHitTestVisible=false };
        var map=new TodoArtwork { Kind="Background",Choice=p.Background,Opacity=.65 }; Grid.SetRow(map,1); Grid.SetRowSpan(map,2); root.Children.Add(map);
        var mural=new TodoMuralArtwork();mural.Apply(p);Grid.SetRow(mural,1);Grid.SetRowSpan(mural,2);root.Children.Add(mural);
        var header=new Grid { Background=TodoVisuals.Navy, ColumnDefinitions=new ColumnDefinitions("64,*,Auto") };
        if(p.EngravedHeader) {var art=new TodoArtwork {Kind="Header",Opacity=.65}; Grid.SetColumnSpan(art,3);header.Children.Add(art);}
        header.Children.Add(new TodoArtwork { Choice=p.HeaderEmblem,Margin=new Thickness(6) });
        var title=new StackPanel {Orientation=Orientation.Horizontal,Spacing=10,VerticalAlignment=VerticalAlignment.Center};
        title.Children.Add(new TextBlock {Text="Todos",FontFamily=TodoVisuals.HeadingFont,FontSize=34,Foreground=TodoVisuals.Ivory});
        title.Children.Add(new TextBlock {Text="3 open",FontFamily=TodoVisuals.BodyFont,FontSize=13,Foreground=TodoVisuals.Ivory,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,5,0,0)});
        Grid.SetColumn(title,1); header.Children.Add(title);
        var collapse=new TextBlock {Text="⌃",Foreground=TodoVisuals.Ivory,FontSize=28,Margin=new Thickness(0,14,16,0)};Grid.SetColumn(collapse,2);header.Children.Add(collapse);root.Children.Add(header);
        var body=new StackPanel {Margin=new Thickness(14,12,14,0)};
        var tools=new Grid {ColumnDefinitions=new ColumnDefinitions("*,Auto"),Background=Brushes.Transparent,Margin=new Thickness(0,0,0,10)};
        var sort=new Button {Content="Sort: Priority  ▾",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(10,5)};
        TodoVisuals.StyleButton(sort,p,pixel:p.Toolbar=="pixel-controls");tools.Children.Add(sort);
        var show=new CheckBox {Content="Show completed",IsChecked=true,FontSize=14};TodoVisuals.Checkbox(show,p,child:true);Grid.SetColumn(show,1);tools.Children.Add(show);body.Children.Add(tools);
        var labels=new[] {"Finish illustration","Read 20 pages","Review project notes","Plan tomorrow"};int[] priorities={1,3,6,10};
        for(int i=0;i<labels.Length;i++)
        {
            var row=new Grid {ColumnDefinitions=new ColumnDefinitions("Auto,Auto,*,20"),RowDefinitions=new RowDefinitions("Auto,Auto,Auto,Auto,Auto")};TodoVisuals.RowSurface(row,p);
            var check=new CheckBox {IsChecked=i==3,MinHeight=42,Padding=new Thickness(8,7,0,7)};TodoVisuals.Checkbox(check,p,p.Rows=="pixel-voyage");row.Children.Add(check);
            var badge=new Button {VerticalAlignment=VerticalAlignment.Center};TodoVisuals.PriorityBadge(badge,priorities[i],p);Grid.SetColumn(badge,1);row.Children.Add(badge);
            var text=new TextBlock {Text=labels[i],FontSize=17,FontFamily=TodoVisuals.StrongFont,FontWeight=FontWeight.ExtraBold,Foreground=TodoVisuals.Ink(p),VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap};
            if(i==3){text.TextDecorations=TextDecorations.Strikethrough;text.Opacity=.7;}
            Grid.SetColumn(text,2);row.Children.Add(text);
            if(i==0)
            {
                var arrow=new TextBlock {Text="⌄",FontSize=22,Foreground=TodoVisuals.Ink(p),VerticalAlignment=VerticalAlignment.Top};Grid.SetColumn(arrow,3);row.Children.Add(arrow);
                var summary=new TextBlock {Text="1/3 subtasks · 1 note",FontFamily=TodoVisuals.BodyFont,Foreground=TodoVisuals.Muted(p),FontSize=12,Margin=new Thickness(78,0,0,5)};
                Grid.SetColumnSpan(summary,3);Grid.SetRow(summary,3);row.Children.Add(summary);
                var details=new StackPanel {Spacing=6};
                foreach(var childText in new[]{"Sketch layout","Add colors","Export PNG"}) {bool done=childText=="Sketch layout";var label=TodoRichText.Create(childText,done);label.FontFamily=TodoVisuals.BodyFont;label.FontSize=14;label.Foreground=TodoVisuals.Ink(p);var child=new CheckBox {Content=label,FontSize=14,IsChecked=done};TodoVisuals.Checkbox(child,p,child:true);child.MinHeight=22;details.Children.Add(child);}
                details.Children.Add(new TextBlock {Text="NOTE",FontFamily=TodoVisuals.BodyFont,FontSize=10,FontWeight=FontWeight.Bold,Foreground=TodoVisuals.Muted(p),Margin=new Thickness(0,6,0,0)});
                details.Children.Add(new TextBlock {Text="Warm palette; keep map subtle.",FontFamily=TodoVisuals.BodyFont,FontSize=14,TextWrapping=TextWrapping.Wrap,Foreground=TodoVisuals.Ink(p)});
                bool fold=p.Details=="foldout-card";
                var detailFrame=new TodoFrame {Child=details,Pixel=fold&&appearance.IsPixel,Margin=new Thickness(fold?10:42,3,8,10),Padding=new Thickness(12,4,8,6),BorderBrush=TodoVisuals.Brass,BorderThickness=fold?new Thickness(1):new Thickness(1.5,0,0,0),Background=fold?TodoVisuals.Card(p):Brushes.Transparent,CornerRadius=new CornerRadius(4)};
                Grid.SetRow(detailFrame,4);Grid.SetColumnSpan(detailFrame,4);row.Children.Add(detailFrame);
            }
            body.Children.Add(row);
        }
        var sampleBody=new Border {Child=body};Grid.SetRow(sampleBody,1);root.Children.Add(sampleBody);
        var add=new Button {Content="＋  Add todo",FontSize=20,Height=42,Padding=new Thickness(12,4),Margin=new Thickness(14,8,14,14),HorizontalAlignment=HorizontalAlignment.Stretch};TodoVisuals.StyleButton(add,p,primary:true,pixel:p.Toolbar=="pixel-controls");Grid.SetRow(add,2);root.Children.Add(add);
        TodoVisuals.AddShellTrim(root);
        var stack=new StackPanel {Spacing=16};
        stack.Children.Add(new Border {Child=root,BorderBrush=TodoVisuals.Gold,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),ClipToBounds=true});
        stack.Children.Add(new TodoArtwork {Choice=p.EffectiveLauncherEmblem,Width=68,Height=68,HorizontalAlignment=HorizontalAlignment.Center});
        // Render the native sample at a stable scale before the inspector shrinks it.
        // This avoids nested control draw-list clipping inside a small Viewbox.
        // It is a sample-only preview: the actual overlay keeps all native inputs.
        stack.Width=386;stack.Height=630;
        Content=stack;
        stack.Measure(new Size(386,630));stack.Arrange(new Rect(0,0,386,630));
        var snapshot=new RenderTargetBitmap(new PixelSize(386,630),new Vector(96,96));
        snapshot.Render(stack);
        Content=new Image {Source=snapshot,MaxWidth=386,MaxHeight=630,Stretch=Stretch.Uniform,IsHitTestVisible=false};
        _snapshot?.Dispose();_snapshot=snapshot;
    }
}
