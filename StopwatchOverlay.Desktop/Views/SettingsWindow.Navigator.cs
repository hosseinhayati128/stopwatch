using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class SettingsWindow
{
    private static readonly IBrush SettingsInk=Brush.Parse("#102B3B"),SettingsMuted=Brush.Parse("#655539"),SettingsRule=Brush.Parse("#509A7B48");
    private DockPanel? _legacySettingsRoot;
    private Grid? _navigatorSettingsBody,_navigatorPage;
    private ScrollViewer? _navigatorSettingsScroll;
    private StackPanel? _navigatorFields,_navigatorPreview;
    private Control? _navigatorHeading,_navigatorPreviewTitle,_navigatorPreviewSubtitle,_navigatorStates;
    private Border? _navigatorPreviewCard,_navigatorClockStage;
    private OverlayDisplaySelector? _displaySelector;
    private OverlayDisplay[] _displays = [];
    private OverlayPositionPicker? _positionPicker;
    private TextBlock? _navigatorSaveStatus,_navigatorClickStatus,_navigatorCaptureStatus;
    private ThemedClockFace? _navigatorClock;
    private Button? _navigatorShow;
    private readonly List<(Control Control,Panel Parent,int Index,object? Content,Thickness Margin)> _navigatorMovedControls=new();
    private readonly List<(Button Button,TextBlock Text,string Key)> _navigatorCategories=new();
    private bool? _navigatorCompact;
    private bool _navigatorStylesAdded;
    private bool _settingsSaved=true;

    private static TextBlock SettingsLabel(string text,double size,IBrush? brush=null)=>new(){Text=text,FontSize=size,FontFamily=ClockPickerPresentation.Body,Foreground=brush??SettingsMuted,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};
    private static Border SettingsCard(Control content)=>new(){Padding=new(14),CornerRadius=new(7),Background=Brush.Parse("#40FFF6E4"),BorderBrush=SettingsRule,BorderThickness=new(1),Child=content};
    private static Button SettingsAction(string text,string name="")
    {
        var button=new Button{Name=name,Content=text,Height=38,Padding=new(10,0)};NavigatorFormStyles.StyleButton(button);return button;
    }

    private void UpdateNavigatorSettingsTheme()
    {
        bool nika=AppThemeCatalog.Normalize(_settings.ThemeMode)==AppThemeCatalog.Nika;
        if(nika&&_navigatorSettingsBody==null)BuildNavigatorSettings();
        else if(!nika&&_navigatorSettingsBody!=null)RestoreLegacySettings();
        if (nika != _usesStyleGalleries) LoadStopwatchAppearance();
    }

    private void BuildNavigatorSettings()
    {
        _legacySettingsRoot=(DockPanel)DesktopPageAppearance.Content(this)!;
        _legacySettingsRoot.Children.Remove(SettingsColumns);Content=null;
        foreach(var control in new Control[]{ClickThroughCheck,HideOverlayCaptureCheck})
            _navigatorMovedControls.Add((control,(Panel)control.Parent!,((Panel)control.Parent!).Children.IndexOf(control),(control as CheckBox)?.Content,control.Margin));
        foreach(var item in _navigatorMovedControls)item.Parent.Children.Remove(item.Control);
        _displaySelector = new OverlayDisplaySelector { Name = "SettingsDisplaySelector" };
        _displaySelector.SetDisplays(_displays, ScreenCombo.SelectedIndex);
        _displaySelector.DisplayChosen += index => ScreenCombo.SelectedIndex = index;
        _navigatorHeading=new StackPanel{Spacing=4,Children={SettingsLabel("Overlay & position",23,SettingsInk),SettingsLabel("Choose where your clock appears.",12)}};
        _positionPicker = new OverlayPositionPicker { Name = "SettingsPositionPicker" };
        _positionPicker.SetPosition(_settings.Position);
        _positionPicker.PositionChosen += position => PositionCombo.SelectedItem = position;
        var display = new StackPanel { Spacing = 6, Children = { SettingsLabel("Display",12,SettingsInk), _displaySelector } };
        var position = new StackPanel { Spacing = 6, Children = { SettingsLabel("Clock position",12,SettingsInk), _positionPicker } };
        var placementFields = new Grid { RowDefinitions = new("Auto,12,Auto"), ColumnDefinitions = new("*"), Children = { display, position } };
        Grid.SetRow(position, 2);
        bool sideBySide = false;
        placementFields.SizeChanged += (_, e) =>
        {
            bool fitsBeside = _navigatorCompact == true && e.NewSize.Width >= 470;
            if (sideBySide == fitsBeside) return;
            sideBySide = fitsBeside;
            placementFields.ColumnDefinitions = new(fitsBeside ? "2*,12,3*" : "*");
            placementFields.RowDefinitions = new(fitsBeside ? "Auto" : "Auto,12,Auto");
            Grid.SetRow(position, fitsBeside ? 0 : 2);
            Grid.SetColumn(position, fitsBeside ? 2 : 0);
        };
        var placement = SettingsCard(new StackPanel { Spacing = 8, Children = { SettingsLabel("PLACEMENT",10), placementFields } });
        Control Choice(CheckBox check,string title,string detail)
        {
            check.Margin=new(0);check.Content=new StackPanel{Spacing=3,Children={SettingsLabel(title,13,SettingsInk),SettingsLabel(detail,11)}};
            check.HorizontalContentAlignment=HorizontalAlignment.Stretch;check.VerticalContentAlignment=VerticalAlignment.Top;AutomationProperties.SetName(check,title);return check;
        }
        var interaction=SettingsCard(new StackPanel{Spacing=12,Children={SettingsLabel("INTERACTION",10),Choice(ClickThroughCheck,"Click-through","Let clicks pass to the windows underneath."),new Border{Height=1,Background=SettingsRule},Choice(HideOverlayCaptureCheck,"Hide from screen capture","The clock stays visible on your screen.")}});
        _navigatorFields=new StackPanel{Spacing=12,Children={placement,interaction}};
        _navigatorClock=new ThemedClockFace{Text="00:05:10",ProjectName="Programming"};
        _navigatorClock.SetStopwatchPresentationState(new(true,true,false));
        _navigatorClockStage=new Border{CornerRadius=new(6),Background=Brush.Parse("#192F3E"),Child=new Viewbox{Child=_navigatorClock,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,MaxWidth=286,Margin=new(12),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
        _navigatorClickStatus=SettingsLabel("",10,Brush.Parse("#2D7169"));_navigatorCaptureStatus=SettingsLabel("",10,SettingsInk);
        _navigatorStates=new StackPanel{Spacing=3,Children={_navigatorClickStatus,_navigatorCaptureStatus}};
        _navigatorShow=SettingsAction("Show / hide active overlay","SettingsShowOverlay");_navigatorShow.HorizontalAlignment=HorizontalAlignment.Stretch;_navigatorShow.Click+=ShowOverlayButton_Click;
        _navigatorPreviewTitle=SettingsLabel("Live preview",18,SettingsInk);_navigatorPreviewSubtitle=SettingsLabel("Sample clock · current appearance",11);
        _navigatorPreview=new StackPanel{Spacing=10};
        _navigatorPreviewCard=SettingsCard(_navigatorPreview);_navigatorPreviewCard.VerticalAlignment=VerticalAlignment.Top;
        _navigatorPage=new Grid{Children={_navigatorFields,_navigatorPreviewCard}};
        _navigatorSettingsScroll=new ScrollViewer{Name="NavigatorSettingsScroll",Content=_navigatorPage,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Padding=new(0,0,5,0)};
        var nav=new StackPanel{Spacing=2};
        var groups=new[]{("OVERLAY",new[]{"Overlay","Appearance","StopwatchAppearance","TodoAppearance","Typography"}),("APPLICATION",new[]{"Behavior","Application"}),("CONNECTIONS",new[]{"Obsidian","Telegram","Habitica","ActivityWatch","Internet"})};
        foreach(var (heading,keys) in groups)
        {
            var label=SettingsLabel(heading,9);label.Margin=new(10,nav.Children.Count==0?0:12,0,5);nav.Children.Add(label);
            foreach(string key in keys)
            {
                var item=NavigationList.Items.OfType<ListBoxItem>().First(i=>i.Tag?.ToString()==key);
                string title=key switch{"Overlay"=>"Overlay & position","Typography"=>"Fonts & colors","StopwatchAppearance"=>"Stopwatch appearance","TodoAppearance"=>"Todo appearance",_=>item.Content?.ToString()??key};
                var text=SettingsLabel(title,12,SettingsInk);text.TextWrapping=TextWrapping.NoWrap;text.TextTrimming=TextTrimming.CharacterEllipsis;text.HorizontalAlignment=HorizontalAlignment.Left;
                var row=new Grid{Children={text}};
                var button=SettingsAction(title,"SettingsNav_"+key);button.Content=row;button.Height=28;button.BorderThickness=new(0);button.HorizontalAlignment=HorizontalAlignment.Stretch;button.SizeChanged+=(_,_)=>row.Width=Math.Max(0,button.Bounds.Width-20);
                AutomationProperties.SetName(button,title);button.Click+=(_,_)=>NavigationList.SelectedItem=item;
                nav.Children.Add(button);_navigatorCategories.Add((button,text,key));
            }
        }
        var navigation=SettingsCard(new ScrollViewer{Content=nav,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});navigation.Padding=new(8,12);navigation.Background=Brush.Parse("#26FFF6E4");
        ((Control)NavigationList.Parent!).IsVisible=false;
        var content=new Grid{Children={SettingsColumns,_navigatorSettingsScroll}};Grid.SetColumn(content,2);
        _navigatorSettingsBody=new Grid{Children={navigation,content}};
        var typography=StopwatchTypography.Resolve("navigator-bold");
        var windowTitle=new TextBlock{Text="Settings & Overlay Inspector",FontFamily=typography.NameFont,FontWeight=typography.NameWeight,FontSize=29,Foreground=SettingsInk,TextWrapping=TextWrapping.Wrap};
        var headerText=new StackPanel{Spacing=5,Children={windowTitle,SettingsLabel("Your overlay, arranged for the way you work.",12)}};
        var close=SettingsAction("","SettingsClose");close.Width=28;close.Height=28;close.BorderThickness=new(0);close.Padding=new(5);close.VerticalAlignment=VerticalAlignment.Top;close.Margin=new(0,8,0,0);
        close.Content=new Avalonia.Controls.Shapes.Path{Width=16,Height=16,Data=Geometry.Parse("M2,2 L14,14 M14,2 L2,14"),Stroke=SettingsInk,StrokeThickness=1.6,StrokeLineCap=PenLineCap.Round,Stretch=Stretch.Uniform};AutomationProperties.SetName(close,"Close settings");close.Click+=DoneButton_Click;
        var header=new Grid{ColumnDefinitions=new("*,20,28"),Children={headerText,close}};Grid.SetColumn(close,2);
        header.PointerPressed+=(_,e)=>{if(e.GetCurrentPoint(header).Properties.IsLeftButtonPressed&&e.Source is TextBlock)BeginMoveDrag(e);};
        var done=SettingsAction("Done","SettingsDone");done.Width=124;done.Classes.Add("editorPrimary");done.Click+=DoneButton_Click;
        _navigatorSaveStatus=SettingsLabel("Changes apply immediately",11);_navigatorSaveStatus.Name="SettingsSaveStatus";
        _navigatorSaveStatus.TextWrapping=TextWrapping.NoWrap;_navigatorSaveStatus.TextTrimming=TextTrimming.CharacterEllipsis;
        var footerRow=new Grid{ColumnDefinitions=new("*,20,124"),Children={_navigatorSaveStatus,done}};Grid.SetColumn(done,2);
        var footer=new Grid{RowDefinitions=new("16,38"),Margin=new(0,10,0,0),Children={new Border{Height=1,Background=SettingsRule,VerticalAlignment=VerticalAlignment.Top},footerRow}};Grid.SetRow(footerRow,1);
        header.MinHeight=76;
        var layout=new Grid{RowDefinitions=new("Auto,*,64"),Children={header,_navigatorSettingsBody,footer}};Grid.SetRow(_navigatorSettingsBody,1);Grid.SetRow(footer,2);
        Content=new StopwatchFrameSurface{ArtworkScale=.16,Padding=new(10),Background=Brush.Parse("#EBD3A5"),Child=layout};
        Width=1120;Height=720;SystemDecorations=SystemDecorations.None;Background=Brushes.Transparent;TransparencyLevelHint=[WindowTransparencyLevel.Transparent];RequestedThemeVariant=ThemeVariant.Light;
        if(!_navigatorStylesAdded){NavigatorFormStyles.Apply(this);_navigatorStylesAdded=true;}
        Resources["SystemAccentColor"]=Color.Parse("#2D7169");
        ReportPersistenceResult(_settingsSaved);UpdateNavigatorCategory();
    }

    private void RestoreLegacySettings()
    {
        RestoreTypographyPage();
        RestoreAppearanceControls();
        RestoreBehaviorPage();
        RestoreApplicationPage();
        RestoreObsidianPage();
        RestoreTelegramPage();
        RestoreHabiticaPage();
        RestoreActivityWatchPage();
        RestoreInternetPage();
        ((Grid)SettingsColumns.Parent!).Children.Remove(SettingsColumns);
        foreach(var item in _navigatorMovedControls)((Panel)item.Control.Parent!).Children.Remove(item.Control);
        foreach(var item in _navigatorMovedControls.OrderBy(i=>i.Index))
        {
            item.Parent.Children.Insert(item.Index,item.Control);item.Control.Margin=item.Margin;
            if(item.Control is CheckBox check)
            {
                check.Content=item.Content;check.ClearValue(HorizontalContentAlignmentProperty);check.ClearValue(VerticalContentAlignmentProperty);
            }

        }
        _navigatorMovedControls.Clear();_navigatorCategories.Clear();
        Content=null;_legacySettingsRoot!.Children.Add(SettingsColumns);Content=_legacySettingsRoot;
        ((Control)NavigationList.Parent!).IsVisible=true;SettingsColumns.IsVisible=true;SettingsScrollViewer.Margin=new(0);
        _navigatorSettingsBody=null;_displaySelector=null;_positionPicker=null;_navigatorClock=null;_navigatorSaveStatus=null;_navigatorCompact=null;
        SystemDecorations=SystemDecorations.Full;TransparencyLevelHint=[];RequestedThemeVariant=ThemeVariant.Default;
        this.Bind(BackgroundProperty,this.GetResourceObservable("AppBackgroundBrush"));Resources.Remove("SystemAccentColor");
        _appearanceColumnLayout="";UpdateTodoResponsiveLayout();
    }

    private void UpdateNavigatorCategory()
    {
        if(_navigatorSettingsBody==null)return;
        bool overlay=CurrentCategory=="Overlay";
        bool appearance=CurrentCategory=="Appearance";
        bool typography=CurrentCategory=="Typography";
        bool behavior=CurrentCategory=="Behavior";
        bool application=CurrentCategory=="Application";
        bool obsidian=CurrentCategory=="Obsidian";
        bool telegram=CurrentCategory=="Telegram";
        bool habitica=CurrentCategory=="Habitica";
        bool activityWatch=CurrentCategory=="ActivityWatch";
        bool internet=CurrentCategory=="Internet";
        if(typography&&_typographyPage==null)BuildTypographyPage((Grid)SettingsColumns.Parent!);
        if(appearance&&_appearancePage==null)BuildAppearancePage((Grid)SettingsColumns.Parent!);
        if(behavior&&_behaviorPage==null)BuildBehaviorPage((Grid)SettingsColumns.Parent!);
        if(application&&_applicationPage==null)BuildApplicationPage((Grid)SettingsColumns.Parent!);
        if(obsidian&&_obsidianPage==null)BuildObsidianPage((Grid)SettingsColumns.Parent!);
        if(telegram&&_telegramPage==null)BuildTelegramPage((Grid)SettingsColumns.Parent!);
        if(habitica&&_habiticaPage==null)BuildHabiticaPage((Grid)SettingsColumns.Parent!);
        if(activityWatch&&_activityWatchPage==null)BuildActivityWatchPage((Grid)SettingsColumns.Parent!);
        if(internet&&_internetPage==null)BuildInternetPage((Grid)SettingsColumns.Parent!);
        SettingsColumns.IsVisible=!overlay&&!appearance&&!typography&&!behavior&&!application&&!obsidian&&!telegram&&!habitica&&!activityWatch&&!internet;_navigatorSettingsScroll!.IsVisible=overlay;
        if(_typographyPage!=null)_typographyPage.IsVisible=typography;
        if(_appearancePage!=null)_appearancePage.IsVisible=appearance;
        if(_behaviorPage!=null)_behaviorPage.IsVisible=behavior;
        if(_applicationPage!=null)_applicationPage.IsVisible=application;
        if(_obsidianPage!=null)_obsidianPage.IsVisible=obsidian;
        if(_telegramPage!=null)_telegramPage.IsVisible=telegram;
        if(_habiticaPage!=null)_habiticaPage.IsVisible=habitica;
        if(_activityWatchPage!=null)_activityWatchPage.IsVisible=activityWatch;
        if(_internetPage!=null)_internetPage.IsVisible=internet;
        ReportPersistenceResult(_settingsSaved);
        foreach(var (button,text,key) in _navigatorCategories){button.Classes.Set("editorPrimary",key==CurrentCategory);text.Foreground=key==CurrentCategory?ClockPickerPresentation.Cream:SettingsInk;}
        ResizeNavigatorSettings();
        RefreshAppearancePreview();
        RefreshTypographyPreview();
        RefreshBehaviorPreview();
        RefreshApplicationChoices();
        ResizeObsidianPage();
        ResizeTelegramPage();
        ResizeActivityWatchPage();
        ResizeInternetPage();
        RefreshInternetConnection();
        RefreshInternetExport();
    }

    private void ResizeNavigatorSettings()
    {
        if(_navigatorSettingsBody==null)return;
        bool compact=(ClientSize.Width>0?ClientSize.Width:Width)/DesktopPageAppearance.Scale(this)<1000;
        ResizeAppearancePage(compact);
        SettingsColumns.ColumnDefinitions=new(compact?"200,0,*":"240,0,*");
        if(_navigatorCompact==compact)return;_navigatorCompact=compact;
        _navigatorSettingsBody.ColumnDefinitions=new(compact?"184,14,*":"200,18,*");
        _navigatorFields!.Children.Remove(_navigatorHeading!);_navigatorPage!.Children.Remove(_navigatorHeading!);
        _navigatorPage.ColumnDefinitions=new(compact?"*":"1.13*,16,1*");_navigatorPage.RowDefinitions=new(compact?"Auto,12,Auto,12,Auto":"Auto");
        Grid.SetRow(_navigatorFields,compact?2:0);Grid.SetColumn(_navigatorPreviewCard!,compact?0:2);Grid.SetRow(_navigatorPreviewCard!,compact?4:0);
        if(compact)_navigatorPage.Children.Add(_navigatorHeading!);else _navigatorFields.Children.Insert(0,_navigatorHeading!);
        _navigatorPreview!.Children.Clear();
        // Detach the compact action row before moving its children to the wide card.
        if(_navigatorShow!.Parent is Grid oldRow)oldRow.Children.Clear();
        _navigatorClockStage!.Height=compact?84:118;
        if(compact)
        {
            _navigatorShow.Height=30;_navigatorShow.Width=220;Grid.SetColumn(_navigatorShow,2);
            var actionRow=new Grid{ColumnDefinitions=new("*,12,220"),Children={_navigatorPreviewTitle!,_navigatorShow}};
            _navigatorPreview.Children.Add(actionRow);_navigatorPreview.Children.Add(_navigatorClockStage);
        }
        else
        {
            _navigatorShow.Height=34;_navigatorShow.Width=double.NaN;
            foreach(var child in new[]{_navigatorPreviewTitle!,_navigatorPreviewSubtitle!,_navigatorClockStage,_navigatorStates!,_navigatorShow})_navigatorPreview.Children.Add(child);
        }
    }

    private void RefreshNavigatorSettingsPreview()
    {
        if(_navigatorSettingsBody==null)return;
        _navigatorClock!.ApplySettings(_settings);_navigatorClock.Text=PreviewClock.Text;_navigatorClock.ProjectName=PreviewClock.ProjectName;
        UpdateNavigatorInteraction();
    }

    private void UpdateNavigatorInteraction()
    {
        if(_navigatorSettingsBody==null)return;
        _navigatorClickStatus!.Text=_settings.ClickThrough?"Click-through on · clicks pass through":"Click-through off · clock is interactive";
        _navigatorCaptureStatus!.Text=_settings.HideOverlayFromCapture?"Screen capture · clock hidden":"Screen capture · clock included";
        _positionPicker?.SetPosition(_settings.Position);
        _displaySelector?.SetSelection(ScreenCombo.SelectedIndex);
    }
}
