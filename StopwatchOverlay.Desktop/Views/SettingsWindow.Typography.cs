using System;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class SettingsWindow
{
    private Grid? _typographyPage,_typographyBody;
    private StackPanel? _typographyFields,_typographyHeading,_typographyTabs;
    private Border? _typographySpecimen;
    private ScrollViewer? _typographyScroll;
    private TypographyEditor? _typographyGlobal;
    private TypographyEditor[] _typographySections=[];
    private ComboBox? _typographyPanel;
    private Button? _typographyGlobalTab,_typographyPanelsTab,_typographyCustomize;
    private TextBlock? _typographyScopeStatus,_typographySampleStatus;
    private bool _typographyOverrides,_typographyScrollHeading,_typographyResizePending;

    private void BuildTypographyPage(Grid host)
    {
        _typographyGlobal=(TypographyEditor)GlobalTypographyEditor.Children.Single();GlobalTypographyEditor.Children.Clear();
        _typographySections=SectionTypographyEditors.Children.Cast<TypographyEditor>().ToArray();SectionTypographyEditors.Children.Clear();
        foreach(var editor in _typographySections.Prepend(_typographyGlobal))editor.UseNavigatorStyle(true);
        _typographyGlobalTab=SettingsAction("Global text","TypographyGlobalTab");_typographyPanelsTab=SettingsAction("Panel overrides","TypographyPanelsTab");
        _typographyGlobalTab.Height=_typographyPanelsTab.Height=34;
        _typographyGlobalTab.Click+=(_,_)=>ShowTypographySection(false);_typographyPanelsTab.Click+=(_,_)=>ShowTypographySection(true);
        _typographyTabs=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8,Children={_typographyGlobalTab,_typographyPanelsTab}};
        _typographyPanel=new ComboBox{Name="TypographyPanelChoice",ItemsSource=TypographySettings.Scopes.Select(s=>s.Label).ToArray(),SelectedIndex=1,HorizontalAlignment=HorizontalAlignment.Stretch,MinWidth=0,Height=36,FontFamily=ClockPickerPresentation.Body,FontSize=12,Classes={"editorField"}};
        AutomationProperties.SetName(_typographyPanel,"Panel to customize");
        _typographyPanel.SelectionChanged+=(_,_)=>{if(_typographyOverrides)ShowTypographySection(true);};
        _typographyScopeStatus=SettingsLabel("",11,Brush.Parse("#2D7169"));_typographySampleStatus=SettingsLabel("",11);
        _typographyCustomize=SettingsAction("Customize panels","TypographyCustomize");_typographyCustomize.HorizontalAlignment=HorizontalAlignment.Stretch;
        _typographyCustomize.Click+=(_,_)=>ShowTypographySection(true);
        var heading=SettingsLabel("Today's progress",23,SettingsInk);heading.FontFamily=StopwatchTypography.Resolve("navigator-bold").NameFont;
        var time=SettingsLabel("00:44:27",32,SettingsInk);time.Name="TypographySampleTime";time.FontFamily=StopwatchTypography.Resolve("navigator-bold").TimerFont;
        var selected=new Border{Background=SettingsInk,CornerRadius=new(4),Padding=new(10),Child=SettingsLabel("Programming  ·  Selected",12,ClockPickerPresentation.Cream)};
        DesktopTypography.SetPreserveColor(selected,true);
        _typographySpecimen=SettingsCard(new StackPanel{Spacing=11,Children={heading,new Border{Height=1,Background=SettingsRule},SettingsLabel("Programming",16,SettingsInk),time,SettingsLabel("2 sessions · Today",11),selected,SettingsLabel("View time records",12,Brush.Parse("#2D7169"))}});
        _typographySpecimen.Name="TypographySpecimen";_typographySpecimen.Background=Brush.Parse("#F7E8C7");
        var example=SettingsCard(new StackPanel{Spacing=10,Children={SettingsLabel("Live text sample",18,SettingsInk),_typographySampleStatus,_typographySpecimen,SettingsLabel("Text keeps its proportions. Selection colors stay readable.",11)}});
        example.VerticalAlignment=VerticalAlignment.Top;
        _typographyFields=new StackPanel{Spacing=12};
        _typographyBody=new Grid{ColumnDefinitions=new("1.03*,16,1*"),Children={_typographyFields,example}};Grid.SetColumn(example,2);
        _typographyScroll=new ScrollViewer{Name="TypographyScroll",Content=_typographyBody,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Padding=new(0,0,5,0)};
        _typographyHeading=new StackPanel{Spacing=4,Children={SettingsLabel("Fonts & colors",23,SettingsInk),SettingsLabel("Keep your text clear, from timer digits to everyday labels.",12)}};
        _typographyScrollHeading=false;
        _typographyPage=new Grid{Name="TypographyPage",Classes={"typographyPage"},RowDefinitions=new("Auto,12,34,14,*"),Children={_typographyHeading,_typographyTabs,_typographyScroll}};
        Grid.SetRow(_typographyTabs,2);Grid.SetRow(_typographyScroll,4);
        foreach(var type in new[]{typeof(ComboBox),typeof(TextBox)})
        {
            var style=new Style(s=>s.OfType<TypographyEditor>().Class("navigatorTypography").Descendant().OfType(type));
            style.Setters.Add(new Setter(TemplatedControl.FontFamilyProperty,ClockPickerPresentation.Body));style.Setters.Add(new Setter(TemplatedControl.FontSizeProperty,12d));
            style.Setters.Add(new Setter(MinWidthProperty,0d));style.Setters.Add(new Setter(MinHeightProperty,0d));style.Setters.Add(new Setter(HeightProperty,36d));
            _typographyPage.Styles.Add(style);
        }
        _typographyPage.Styles.Add(new Style(s=>s.OfType<TypographyEditor>().Class("navigatorTypography").Descendant().OfType<TextBlock>()){Setters={new Setter(TextBlock.FontFamilyProperty,ClockPickerPresentation.Body)}});
        _typographyPage.Styles.Add(new Style(s=>s.OfType<TypographyEditor>().Class("navigatorTypography").Descendant().OfType<CheckBox>()){Setters={new Setter(FontSizeProperty,12d)}});
        _typographyHeading.SizeChanged+=(_,_)=>QueueTypographyResize();
        _typographyPage.SizeChanged+=(_,_)=>QueueTypographyResize();host.Children.Add(_typographyPage);ShowTypographySection(_typographyOverrides);
    }
    private void ShowTypographySection(bool overrides)
    {
        if(_typographyPage==null)return;_typographyOverrides=overrides;
        foreach(var editor in _typographySections.Prepend(_typographyGlobal!))if(editor.Parent is Panel parent)parent.Children.Remove(editor);
        foreach(var control in new Control[]{_typographyPanel!,_typographyScopeStatus!,_typographyCustomize!})if(control.Parent is Panel parent)parent.Children.Remove(control);
        _typographyFields!.Children.Clear();_typographyGlobalTab!.Classes.Set("editorPrimary",!overrides);_typographyPanelsTab!.Classes.Set("editorPrimary",overrides);
        if(overrides)_typographyFields.Children.Add(SettingsCard(new StackPanel{Spacing=10,Children={SettingsLabel("PANEL OVERRIDE",10),_typographyPanel!,_typographySections[_typographyPanel!.SelectedIndex],SettingsLabel("Turn off the override to follow global text. Your panel choices are kept.",11)}}));
        else
        {
            _typographyFields.Children.Add(SettingsCard(new StackPanel{Spacing=10,Children={SettingsLabel("GLOBAL TEXT",10),_typographyGlobal!,SettingsLabel("Theme default keeps the theme's readable text and selection colors.",11,Brush.Parse("#2D7169"))}}));
            _typographyFields.Children.Add(SettingsCard(new StackPanel{Spacing=9,Children={SettingsLabel("Make an exception",16,SettingsInk),_typographyScopeStatus!,_typographyCustomize!}}));
        }
        _typographyFields.Children.Add(SettingsLabel("Text size changes the lettering. Page scale is in Appearance; font styles stay with your theme.",11));
        RefreshTypographyPreview();_typographyScroll!.Offset=default;
    }
    private void RefreshTypographyPreview()
    {
        if(_typographyPage==null||_closed)return;
        string scope=_typographyOverrides?TypographySettings.Scopes[_typographyPanel!.SelectedIndex].Key:"Global";
        var style=_settings.Typography.Resolve(scope);
        _typographySampleStatus!.Text=$"{(_typographyOverrides?_typographyPanel!.SelectedItem:"All panels")} · {style.SizePercent:0}%";
        _typographyScopeStatus!.Text=$"{TypographySettings.Scopes.Count(s=>!_settings.Typography.Sections[s.Key].Enabled)} panels follow global text";
        DesktopTypography.SetScope(_typographySpecimen!,scope);DesktopTypography.Refresh(_typographySpecimen!);ResizeTypographyPage();
    }
    private void QueueTypographyResize()
    {
        if(_typographyResizePending)return;_typographyResizePending=true;
        // Reparent after Grid finishes arranging its current children.
        Dispatcher.UIThread.Post(()=>{_typographyResizePending=false;ResizeTypographyPage();},DispatcherPriority.Background);
    }
    private void ResizeTypographyPage()
    {
        if(_typographyPage==null||_closed||_typographyPage.Bounds.Height<=0)return;
        double textScale=((TextBlock)_typographyHeading!.Children[0]).FontSize/23;
        bool compact=_typographyPage.Bounds.Width<700*textScale;
        _typographyBody!.ColumnDefinitions=new(compact?"*":"1.03*,16,1*");_typographyBody.RowDefinitions=new(compact?"Auto,12,Auto":"Auto");
        Grid.SetColumn(_typographyBody.Children[1],compact?0:2);Grid.SetRow(_typographyBody.Children[1],compact?2:0);
        bool scrollHeading=_typographyPage.Bounds.Height<260*textScale;
        if(scrollHeading==_typographyScrollHeading)return;
        _typographyScrollHeading=scrollHeading;
        if(_typographyScroll!.Content is StackPanel scrolled)scrolled.Children.Clear();
        _typographyScroll.Content=null;
        _typographyPage.Children.Remove(_typographyHeading);_typographyPage.Children.Remove(_typographyTabs!);
        _typographyPage.RowDefinitions=new(scrollHeading?"*":"Auto,12,34,14,*");Grid.SetRow(_typographyScroll,scrollHeading?0:4);
        if(scrollHeading)_typographyScroll.Content=new StackPanel{Spacing=12,Children={_typographyHeading,_typographyTabs!,_typographyBody}};
        else
        {
            _typographyPage.Children.Add(_typographyHeading);_typographyPage.Children.Add(_typographyTabs!);
            _typographyScroll.Content=_typographyBody;
        }
    }
    private void RestoreTypographyPage()
    {
        if(_typographyPage==null)return;
        foreach(var editor in _typographySections.Prepend(_typographyGlobal!))
        {if(editor.Parent is Panel parent)parent.Children.Remove(editor);editor.UseNavigatorStyle(false);}
        GlobalTypographyEditor.Children.Add(_typographyGlobal!);foreach(var editor in _typographySections)SectionTypographyEditors.Children.Add(editor);
        if(_typographyPage.Parent is Panel host)host.Children.Remove(_typographyPage);
        _typographyPage=null;_typographySections=[];_typographyGlobal=null;
    }
}
