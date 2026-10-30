using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class ConfirmationDialogWindow
{
    public void ApplyTodoAppearance(AppSettings settings)
    {
        if(AppThemeCatalog.Normalize(settings.ThemeMode)!=AppThemeCatalog.Nika)return;
        settings.TodoAppearance??=new();settings.TodoAppearance.Normalize();var p=settings.TodoAppearance.Current;
        Classes.Add("nikaTodoDialog");TodoVisuals.Palette(this,p);FontFamily=TodoVisuals.BodyFont;
        foreach(var control in new Control[]{HeadingText,MessageText,CancelButton,ConfirmButton})
            if(control.Parent is Panel parent)parent.Children.Remove(control);
        Content=null;SystemDecorations=SystemDecorations.None;Background=Brushes.Transparent;TransparencyLevelHint=new[]{WindowTransparencyLevel.Transparent};
        var grid=new Grid {RowDefinitions=new RowDefinitions("64,*,Auto")};
        var map=new TodoArtwork {Kind="Background",Choice=p.Background,Opacity=.65};Grid.SetRow(map,1);Grid.SetRowSpan(map,2);grid.Children.Add(map);
        var header=new Grid {ColumnDefinitions=new ColumnDefinitions("60,*,40"),Background=TodoVisuals.Navy};
        if(p.EngravedHeader){var art=new TodoArtwork {Kind="Header",Opacity=.65};Grid.SetColumnSpan(art,3);header.Children.Add(art);}
        header.Children.Add(new TodoArtwork {Choice=p.HeaderEmblem,Margin=new Thickness(8)});
        var title=new TextBlock {Text=Title,FontFamily=TodoVisuals.HeadingFont,FontSize=26,Foreground=TodoVisuals.Ivory,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(title,1);header.Children.Add(title);
        var close=new Button {Content="×",Background=Brushes.Transparent,Foreground=TodoVisuals.Ivory,BorderThickness=new Thickness(0),Padding=new Thickness(6),FontSize=22,VerticalAlignment=VerticalAlignment.Center};close.Click+=(_,_)=>Close(false);Grid.SetColumn(close,2);header.Children.Add(close);
        header.PointerPressed+=(_,e)=>{if(e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)BeginMoveDrag(e);};grid.Children.Add(header);
        var body=new StackPanel {Spacing=14,Margin=new Thickness(22,20)};
        HeadingText.FontFamily=TodoVisuals.BodyFont;HeadingText.FontSize=19;HeadingText.Foreground=TodoVisuals.Ink(p);HeadingText.HorizontalAlignment=HorizontalAlignment.Left;HeadingText.TextAlignment=TextAlignment.Left;
        MessageText.Foreground=TodoVisuals.Ink(p);body.Children.Add(HeadingText);body.Children.Add(MessageText);
        var scroll=new ScrollViewer {Content=body,VerticalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Auto};Grid.SetRow(scroll,1);grid.Children.Add(scroll);
        var footer=new Grid {ColumnDefinitions=new ColumnDefinitions("*,12,*"),Margin=new Thickness(22,0,22,20)};
        TodoVisuals.StyleButton(CancelButton,p,pixel:p.Toolbar=="pixel-controls");TodoVisuals.StyleButton(ConfirmButton,p,pixel:p.Toolbar=="pixel-controls",danger:ConfirmButton.Classes.Contains("danger"));
        CancelButton.HorizontalAlignment=ConfirmButton.HorizontalAlignment=HorizontalAlignment.Stretch;
        footer.Children.Add(CancelButton);Grid.SetColumn(ConfirmButton,2);footer.Children.Add(ConfirmButton);Grid.SetRow(footer,2);grid.Children.Add(footer);
        TodoVisuals.AddShellTrim(grid,settings.TodoAppearance.IsPixel);
        Content=new TodoFrame {Child=grid,Margin=new Thickness(8),BorderBrush=TodoVisuals.Gold,BorderThickness=new Thickness(1),Background=TodoVisuals.Paper(p),CornerRadius=new CornerRadius(9),Pixel=settings.TodoAppearance.IsPixel,ClipToBounds=true};
    }
}
