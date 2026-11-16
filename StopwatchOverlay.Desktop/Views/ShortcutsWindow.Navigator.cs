using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class ShortcutsWindow
{
    private Grid? _mainShortcutCards;
    private ScrollViewer? _shortcutScroll;
    private Button? _mainShortcutTab, _mappingShortcutTab;
    private TextBlock? _shortcutExample;

    internal void ApplyNavigatorLayout(string? theme)
    {
        if (theme != AppThemeCatalog.Nika || _mainShortcutCards != null) return;
        var ink = ClockPickerPresentation.Navy; var muted = ClockPickerPresentation.Muted;
        TextBlock Label(string text, double size=13, IBrush? brush=null) => new() { Text=text, FontSize=size,
            FontFamily=ClockPickerPresentation.Body, Foreground=brush??ink, TextWrapping=TextWrapping.Wrap };
        Button Action(string text,string name,bool primary=false)
        {
            var button=new Button {Content=text,Name=name,Padding=new(14,9),MinHeight=38};
            NavigatorFormStyles.StyleButton(button,primary); button.MinHeight=38; return button;
        }
        Border Rule()=>new(){Height=1,Background=Brush.Parse("#359A7B48")};
        Border Card(params Control[] controls)
        {
            var body=new StackPanel {Spacing=12}; foreach(var c in controls)body.Children.Add(c);
            return new Border {Background=Brush.Parse("#40FFF6E4"),BorderBrush=Brush.Parse("#509A7B48"),BorderThickness=new(1),
                CornerRadius=new(7),Padding=new(18),VerticalAlignment=VerticalAlignment.Top,Child=body};
        }
        var fields=new[]{LeaderShortcutBox,NoteLeaderShortcutBox,ShowActiveOverlayShortcutBox,OpenControllerShortcutBox};
        foreach(var control in fields.Cast<Control>().Concat([ChainingTimeoutSlider,ChainingTimeoutText,CommandsTabControl,PortalRegistrationText]))
            ((Panel)control.Parent!).Children.Remove(control);
        Content=null; NavigatorFormStyles.Apply(this);
        FontFamily=ClockPickerPresentation.Body; Foreground=ink;
        Resources["PrimaryTextBrush"]=ink; Resources["SecondaryTextBrush"]=muted;
        Resources["SurfaceBrush"]=Brush.Parse("#F7E8C7"); Resources["AccentBrush"]=ink;
        Width=1120;Height=720;MinWidth=700;MinHeight=500;
        SystemDecorations=SystemDecorations.None;Background=Brushes.Transparent;TransparencyLevelHint=[WindowTransparencyLevel.Transparent];
        string[] titles=["Timer command leader","Note command leader","Show active overlay","Open controller"];
        string[] details=["Open timer commands, then press a command key.","Open note, Todo and reminder commands.","Bring the active timer overlay into view.","Bring the stopwatch controller into view."];
        var rows=new StackPanel {Spacing=8};
        for(int i=0;i<fields.Length;i++)
        {
            var field=fields[i];field.Margin=default;field.MinWidth=0;field.MinHeight=38;field.Height=double.NaN;
            Grid.SetColumn(field,0);
            field.Padding=new(12,8);field.FontFamily=ClockPickerPresentation.Body;field.FontSize=13;field.Classes.Add("editorField");
            AutomationProperties.SetName(field,titles[i]); ToolTip.SetTip(field,"Press a combination. Escape cancels capture; Tab moves to the next control.");
            var clear=Action("Clear","ShortcutsClear"+i);clear.Tag=field.Tag;clear.Click+=ClearShortcut_Click;
            AutomationProperties.SetName(clear,"Clear "+titles[i]);
            var controls=new Grid {ColumnDefinitions=new("*,10,Auto"),Children={field,clear}};Grid.SetColumn(clear,2);
            rows.Children.Add(new StackPanel {Spacing=4,Children={Label(titles[i],16),Label(details[i],12,muted),controls}});
            if(i<3)rows.Children.Add(Rule());
        }
        ChainingTimeoutSlider.Margin=default;ChainingTimeoutSlider.TickFrequency=.1;ChainingTimeoutSlider.IsSnapToTickEnabled=true;
        ChainingTimeoutSlider.Foreground=Brush.Parse("#278498"); AutomationProperties.SetName(ChainingTimeoutSlider,"Time between commands in seconds");
        ChainingTimeoutText.FontFamily=ClockPickerPresentation.Body;ChainingTimeoutText.FontSize=27;ChainingTimeoutText.Foreground=ink;ChainingTimeoutText.TextAlignment=TextAlignment.Left;
        _shortcutExample=Label("",14); UpdateShortcutExample();
        var end=Label("2.0 s",11,muted);Grid.SetColumn(end,1);
        var timing=Card(Label("Command chaining",21),Label("Time between commands",13,muted),ChainingTimeoutText,ChainingTimeoutSlider,
            new Grid {ColumnDefinitions=new("*,Auto"),Children={Label("0.2 s",11,muted),end}},
            Label("After eligible timer commands, keep command mode open for another key. A longer delay gives you more time to chain actions.",13,muted),Rule(),
            Label("How it works",16),_shortcutExample,Label("The leader opens command mode. Escape leaves it. Individual command keys are on the Command mappings tab.",12,muted));
        _mainShortcutCards=new Grid {Name="MainShortcutCards",Children={Card(Label("Main shortcuts",21),Label("Select a shortcut to record a new combination.",12,muted),rows),timing}};
        _mainShortcutTab=Action("01  Main shortcuts","ShortcutsMainTab",true);_mappingShortcutTab=Action("02  Command mappings","ShortcutsMappingsTab");
        _mainShortcutTab.Click+=(_,_)=>SelectShortcutPage(true);_mappingShortcutTab.Click+=(_,_)=>SelectShortcutPage(false);
        _mainShortcutTab.Margin=new(0,0,12,0);
        var heading=Label("Keyboard Shortcuts",34);heading.FontFamily=StopwatchTypography.Resolve("navigator-bold").NameFont;heading.FontWeight=FontWeight.Bold;
        var title=new StackPanel {Spacing=8,Background=Brushes.Transparent,Children={heading,Label("Your everyday controls, one combination away.",14,muted)}};
        title.PointerPressed+=(_,e)=>{if(e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)BeginMoveDrag(e);};
        var header=new StackPanel {Spacing=16,Margin=new(0,0,0,18),Children={title,new WrapPanel {Children={_mainShortcutTab,_mappingShortcutTab}}}};
        PortalRegistrationText.MaxWidth=double.PositiveInfinity;PortalRegistrationText.Foreground=Brush.Parse("#913F32");PortalRegistrationText.Margin=default;
        CommandsTabControl.Margin=default;
        _shortcutScroll=new ScrollViewer {Name="MainShortcutsScroll",HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            Content=new StackPanel {Spacing=12,Children={PortalRegistrationText,_mainShortcutCards,CommandsTabControl}}};
        _shortcutScroll.SizeChanged+=(_,_)=>
        {
            bool stack=_shortcutScroll.Bounds.Width<800;
            _mainShortcutCards.ColumnDefinitions=new(stack?"*":"1.25*,20,1*");_mainShortcutCards.RowDefinitions=new(stack?"Auto,18,Auto":"Auto");
            Grid.SetColumn(timing,stack?0:2);Grid.SetRow(timing,stack?2:0);
        };
        var cancel=Action("Cancel","ShortcutsCancel");cancel.Click+=Cancel_Click;
        var save=Action("Save shortcuts","ShortcutsSave",true);save.Click+=Save_Click;
        var reset=Action("Reset all shortcuts","ShortcutsReset");reset.Click+=ResetDefaults_Click;
        ToolTip.SetTip(reset,"Reset main shortcuts, command mappings and chaining timeout. Save applies the changes.");
        var actions=new StackPanel {Orientation=Orientation.Horizontal,Spacing=10,Children={cancel,save},HorizontalAlignment=HorizontalAlignment.Right};
        var footer=new Grid {ColumnDefinitions=new("Auto,*,Auto"),Margin=new(0,18,0,0),Children={reset,actions}};Grid.SetColumn(actions,2);
        var hint=Label("Changes apply with Save · Reset includes both shortcut pages",11,muted);hint.Margin=new(0,12,0,0);
        var layout=new Grid {RowDefinitions=new("Auto,*,Auto,Auto"),Children={header,_shortcutScroll,hint,footer}};
        Grid.SetRow(_shortcutScroll,1);Grid.SetRow(hint,2);Grid.SetRow(footer,3);
        Content=new StopwatchFrameSurface {ArtworkScale=.16,Padding=new(22),Background=Brush.Parse("#EBD3A5"),Child=layout};
        SelectShortcutPage(true);
    }

    private void SelectShortcutPage(bool main)
    {
        if(_mainShortcutCards==null)return;
        _mainShortcutCards.IsVisible=main;CommandsTabControl.IsVisible=!main;
        _mainShortcutTab!.Classes.Set("editorPrimary",main);_mappingShortcutTab!.Classes.Set("editorPrimary",!main);
        _shortcutScroll!.Offset=default;
    }
    private void UpdateShortcutExample()
    {
        if(_shortcutExample!=null)_shortcutExample.Text=_pendingLeader.VirtualKey==0?"Assign a timer leader to open command mode.":$"{_pendingLeader.Format()}  →  command key  →  next key";
    }
}
