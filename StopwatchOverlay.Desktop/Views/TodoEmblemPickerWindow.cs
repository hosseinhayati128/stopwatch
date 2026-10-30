using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public sealed class TodoEmblemPickerWindow : Window
{
    public string SelectedEmblem { get; private set; }
    public string? Result { get; private set; }
    private readonly List<(Button Button,string Id)> _choices=new();
    public TodoEmblemPickerWindow(string current, bool launcher, TodoAppearanceProfile look)
    {
        SelectedEmblem=TodoAppearanceCatalog.Normalize(current,TodoAppearanceCatalog.Emblems,"luffy");
        Title=launcher?"Choose launcher emblem":"Choose header emblem";
        Width=600; Height=650; MinWidth=420; MinHeight=480; CanResize=true; WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Background=TodoVisuals.Navy; FontFamily=TodoVisuals.BodyFont; ShowInTaskbar=false;
        var root=new Grid {RowDefinitions=new RowDefinitions("Auto,*,Auto"),Margin=new Thickness(20)};
        root.Children.Add(new TextBlock {Text=Title,FontFamily=TodoVisuals.HeadingFont,FontSize=26,Foreground=TodoVisuals.Ivory,Margin=new Thickness(0,0,0,16)});
        var grid=new UniformGrid {Columns=4};
        foreach(var option in TodoAppearanceCatalog.Emblems)
        {
            var stack=new StackPanel {Spacing=4};stack.Children.Add(new TodoArtwork {Choice=option.Id,Height=78,Width=96});
            stack.Children.Add(new TextBlock {Text=option.Label,Foreground=TodoVisuals.Ivory,FontSize=12,HorizontalAlignment=HorizontalAlignment.Center});
            var button=new Button {Name="Emblem_"+option.Id,Content=stack,Margin=new Thickness(4),Padding=new Thickness(6),HorizontalContentAlignment=HorizontalAlignment.Center};
            TodoVisuals.StyleButton(button,look);button.Background=TodoVisuals.Navy;button.BorderBrush=TodoVisuals.Brass;
            AutomationProperties.SetName(button,option.Label+" emblem");button.Click+=(_,_)=>Select(option.Id);
            _choices.Add((button,option.Id));grid.Children.Add(button);
        }
        var scroll=new ScrollViewer {Content=grid,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(scroll,1);root.Children.Add(scroll);
        var footer=new Grid {ColumnDefinitions=new ColumnDefinitions("*,Auto,Auto"),Margin=new Thickness(0,16,0,0)};
        var label=new TextBlock {Text="Choose a symbol",Foreground=TodoVisuals.Ivory,VerticalAlignment=VerticalAlignment.Center};footer.Children.Add(label);
        var cancel=new Button {Name="CancelEmblem",Content="Cancel",Padding=new Thickness(18,8),Margin=new Thickness(0,0,10,0)};TodoVisuals.StyleButton(cancel,look);Grid.SetColumn(cancel,1);footer.Children.Add(cancel);cancel.Click+=(_,_)=>Close();
        var use=new Button {Name="UseEmblem",Content="Use emblem",Padding=new Thickness(18,8)};TodoVisuals.StyleButton(use,look,primary:true);Grid.SetColumn(use,2);footer.Children.Add(use);use.Click+=(_,_)=>{Result=SelectedEmblem;Close(Result);};
        Grid.SetRow(footer,2);root.Children.Add(footer);Content=root;Select(SelectedEmblem);
        KeyDown+=(_,e)=>{if(e.Key==Key.Escape){e.Handled=true;Close();}};
    }
    public void Select(string id)
    {
        SelectedEmblem=TodoAppearanceCatalog.Normalize(id,TodoAppearanceCatalog.Emblems,"luffy");
        foreach(var (button,choice) in _choices)
        {
            button.BorderThickness=new Thickness(choice==SelectedEmblem?2:1);
            button.Background=Brush.Parse(choice==SelectedEmblem?"#16485C":"#03283E");
            AutomationProperties.SetHelpText(button,choice==SelectedEmblem?"Selected. Use emblem to apply.":"Select emblem");
        }
    }
}
