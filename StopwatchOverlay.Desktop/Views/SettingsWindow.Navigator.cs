using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
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
    private Grid? _navigatorSettingsBody,_navigatorPlacement,_navigatorPage,_navigatorPreviewGrid;
    private ScrollViewer? _navigatorSettingsScroll;
    private StackPanel? _navigatorFields,_navigatorPreview,_navigatorDiagram;
    private Control? _navigatorHeading,_navigatorDisplayField,_navigatorPositionField,_navigatorPreviewTitle,_navigatorPreviewSubtitle,_navigatorGuideTitle,_navigatorStates,_navigatorGuideHint;
    private Border? _navigatorPreviewCard,_navigatorClockStage;
    private Canvas? _navigatorGuide;
    private TextBlock? _navigatorSaveStatus,_navigatorGuideLabel,_navigatorGuideCaption,_navigatorClickStatus,_navigatorCaptureStatus;
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
    }

    private void BuildNavigatorSettings()
    {
        _legacySettingsRoot=(DockPanel)DesktopPageAppearance.Content(this)!;
        _legacySettingsRoot.Children.Remove(SettingsColumns);Content=null;
        foreach(var control in new Control[]{ScreenCombo,PositionCombo,ClickThroughCheck,HideOverlayCaptureCheck})
            _navigatorMovedControls.Add((control,(Panel)control.Parent!,((Panel)control.Parent!).Children.IndexOf(control),(control as CheckBox)?.Content,control.Margin));
        foreach(var item in _navigatorMovedControls)item.Parent.Children.Remove(item.Control);
        foreach(var combo in new[]{ScreenCombo,PositionCombo})
        {
            combo.Height=36;combo.MinHeight=0;combo.MinWidth=0;combo.Padding=new(10,4);combo.FontSize=12;combo.Margin=new(0);combo.Classes.Add("editorField");
            combo.ItemTemplate=new FuncDataTemplate<string>((text,_)=>new TextBlock{Text=text,FontFamily=ClockPickerPresentation.Body,FontSize=12,TextTrimming=TextTrimming.CharacterEllipsis});combo.SelectionBoxItemTemplate=combo.ItemTemplate;
        }
        AutomationProperties.SetName(ScreenCombo,"Display");AutomationProperties.SetName(PositionCombo,"Position");
        _navigatorHeading=new StackPanel{Spacing=4,Children={SettingsLabel("Overlay & position",23,SettingsInk),SettingsLabel("Choose where your clock appears.",12)}};
        _navigatorDisplayField=new StackPanel{Spacing=6,Children={SettingsLabel("Display",12,SettingsInk),ScreenCombo}};
        _navigatorPositionField=new StackPanel{Spacing=6,Children={SettingsLabel("Position",12,SettingsInk),PositionCombo}};
        _navigatorPlacement=new Grid{Children={_navigatorDisplayField,_navigatorPositionField}};
        var placement=SettingsCard(new StackPanel{Spacing=8,Children={SettingsLabel("PLACEMENT",10),_navigatorPlacement,SettingsLabel("Choose a preset, or keep your custom placement.",11)}});
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
        _navigatorGuide=new Canvas{Name="SettingsPlacementGuide",ClipToBounds=true};_navigatorGuide.SizeChanged+=(_,_)=>DrawSettingsPlacement();
        _navigatorGuideLabel=SettingsLabel("",11,SettingsInk);_navigatorGuideCaption=SettingsLabel("",10);
        _navigatorGuideTitle=SettingsLabel("PLACEMENT GUIDE",10);
        _navigatorDiagram=new StackPanel{Spacing=6,Children={_navigatorGuideTitle,_navigatorGuide,_navigatorGuideLabel,_navigatorGuideCaption}};
        _navigatorClickStatus=SettingsLabel("",10,Brush.Parse("#2D7169"));_navigatorCaptureStatus=SettingsLabel("",10,SettingsInk);
        _navigatorStates=new StackPanel{Spacing=3,Children={_navigatorClickStatus,_navigatorCaptureStatus}};
        _navigatorShow=SettingsAction("Show / hide active overlay","SettingsShowOverlay");_navigatorShow.HorizontalAlignment=HorizontalAlignment.Stretch;_navigatorShow.Click+=ShowOverlayButton_Click;
        _navigatorPreviewTitle=SettingsLabel("Live preview",18,SettingsInk);_navigatorPreviewSubtitle=SettingsLabel("Sample clock · current appearance",11);
        _navigatorGuideHint=SettingsLabel("The guide shows placement, not screen scale.",10);
        _navigatorPreviewGrid=new Grid();_navigatorPreview=new StackPanel{Spacing=10};
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
        ((Grid)SettingsColumns.Parent!).Children.Remove(SettingsColumns);
        foreach(var item in _navigatorMovedControls)((Panel)item.Control.Parent!).Children.Remove(item.Control);
        foreach(var item in _navigatorMovedControls.OrderBy(i=>i.Index))
        {
            item.Parent.Children.Insert(item.Index,item.Control);item.Control.Margin=item.Margin;
            if(item.Control is CheckBox check)
            {
                check.Content=item.Content;check.ClearValue(HorizontalContentAlignmentProperty);check.ClearValue(VerticalContentAlignmentProperty);
            }
            if(item.Control is ComboBox combo)
            {
                // The original XAML leaves these properties to the current theme.
                combo.Classes.Remove("editorField");combo.ClearValue(HeightProperty);combo.ClearValue(MinHeightProperty);combo.ClearValue(MinWidthProperty);
                combo.ClearValue(PaddingProperty);combo.ClearValue(FontSizeProperty);combo.ClearValue(ItemsControl.ItemTemplateProperty);combo.ClearValue(ComboBox.SelectionBoxItemTemplateProperty);combo.ClearValue(ToolTip.TipProperty);
            }
        }
        _navigatorMovedControls.Clear();_navigatorCategories.Clear();
        Content=null;_legacySettingsRoot!.Children.Add(SettingsColumns);Content=_legacySettingsRoot;
        ((Control)NavigationList.Parent!).IsVisible=true;SettingsColumns.IsVisible=true;SettingsScrollViewer.Margin=new(0);
        _navigatorSettingsBody=null;_navigatorClock=null;_navigatorSaveStatus=null;_navigatorCompact=null;
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
        if(typography&&_typographyPage==null)BuildTypographyPage((Grid)SettingsColumns.Parent!);
        if(appearance&&_appearancePage==null)BuildAppearancePage((Grid)SettingsColumns.Parent!);
        if(behavior&&_behaviorPage==null)BuildBehaviorPage((Grid)SettingsColumns.Parent!);
        if(application&&_applicationPage==null)BuildApplicationPage((Grid)SettingsColumns.Parent!);
        if(obsidian&&_obsidianPage==null)BuildObsidianPage((Grid)SettingsColumns.Parent!);
        SettingsColumns.IsVisible=!overlay&&!appearance&&!typography&&!behavior&&!application&&!obsidian;_navigatorSettingsScroll!.IsVisible=overlay;
        if(_typographyPage!=null)_typographyPage.IsVisible=typography;
        if(_appearancePage!=null)_appearancePage.IsVisible=appearance;
        if(_behaviorPage!=null)_behaviorPage.IsVisible=behavior;
        if(_applicationPage!=null)_applicationPage.IsVisible=application;
        if(_obsidianPage!=null)_obsidianPage.IsVisible=obsidian;
        foreach(var (button,text,key) in _navigatorCategories){button.Classes.Set("editorPrimary",key==CurrentCategory);text.Foreground=key==CurrentCategory?ClockPickerPresentation.Cream:SettingsInk;}
        ResizeNavigatorSettings();
        RefreshAppearancePreview();
        RefreshTypographyPreview();
        RefreshBehaviorPreview();
        RefreshApplicationChoices();
        ResizeObsidianPage();
    }

    private void ResizeNavigatorSettings()
    {
        if(_navigatorSettingsBody==null)return;
        bool compact=(ClientSize.Width>0?ClientSize.Width:Width)/DesktopPageAppearance.Scale(this)<1000;
        ResizeAppearancePage(compact);
        SettingsColumns.ColumnDefinitions=new(compact?"200,0,*":"240,0,*");
        if(_navigatorCompact==compact)return;_navigatorCompact=compact;
        _navigatorSettingsBody.ColumnDefinitions=new(compact?"184,14,*":"200,18,*");
        _navigatorPlacement!.ColumnDefinitions=new(compact?"*,12,*":"*");_navigatorPlacement.RowDefinitions=new(compact?"Auto":"Auto,12,Auto");Grid.SetColumn(_navigatorPositionField!,compact?2:0);Grid.SetRow(_navigatorPositionField!,compact?0:2);
        _navigatorFields!.Children.Remove(_navigatorHeading!);_navigatorPage!.Children.Remove(_navigatorHeading!);
        _navigatorPage.ColumnDefinitions=new(compact?"*":"1.13*,16,1*");_navigatorPage.RowDefinitions=new(compact?"Auto,12,Auto,12,Auto":"Auto");
        Grid.SetRow(_navigatorFields,compact?4:0);Grid.SetColumn(_navigatorPreviewCard!,compact?0:2);Grid.SetRow(_navigatorPreviewCard!,compact?2:0);
        if(compact)_navigatorPage.Children.Add(_navigatorHeading!);else _navigatorFields.Children.Insert(0,_navigatorHeading!);
        _navigatorPreview!.Children.Clear();_navigatorPreviewGrid!.Children.Clear();
        // Detach the compact action row before moving its children to the wide card.
        if(_navigatorShow!.Parent is Grid oldRow)oldRow.Children.Clear();
        _navigatorClockStage!.Height=compact?84:118;_navigatorGuide!.Height=compact?62:96;
        _navigatorGuideTitle!.IsVisible=_navigatorGuideCaption!.IsVisible=!compact;
        if(compact)
        {
            _navigatorShow.Height=30;_navigatorShow.Width=220;Grid.SetColumn(_navigatorShow,2);
            var actionRow=new Grid{ColumnDefinitions=new("*,12,220"),Children={_navigatorPreviewTitle!,_navigatorShow}};
            _navigatorPreviewGrid.ColumnDefinitions=new("1.05*,14,*");_navigatorPreviewGrid.Children.Add(_navigatorClockStage);_navigatorPreviewGrid.Children.Add(_navigatorDiagram!);Grid.SetColumn(_navigatorDiagram!,2);
            _navigatorPreview.Children.Add(actionRow);_navigatorPreview.Children.Add(_navigatorPreviewGrid);
        }
        else
        {
            _navigatorShow.Height=34;_navigatorShow.Width=double.NaN;
            foreach(var child in new[]{_navigatorPreviewTitle!,_navigatorPreviewSubtitle!,_navigatorClockStage,_navigatorDiagram!,_navigatorStates!,_navigatorShow,_navigatorGuideHint!})_navigatorPreview.Children.Add(child);
        }
        DrawSettingsPlacement();
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
        DrawSettingsPlacement();
    }

    private void DrawSettingsPlacement()
    {
        if(_navigatorSettingsBody==null||_navigatorGuide==null)return;
        _navigatorGuide.Children.Clear();double width=_navigatorGuide.Bounds.Width;if(width<=0)return;
        var screens=Screens.All;int index=SettingsChangePolicy.ResolveScreenComboIndex(_settings.ScreenIndex,screens.Count);
        int count=index==0?Math.Max(1,screens.Count):1;double gap=10,screenWidth=(width-gap*(count-1))/count,screenHeight=_navigatorGuide.Height-10;
        string position=_settings.Position;
        for(int i=0;i<count;i++)
        {
            double left=i*(screenWidth+gap);
            var monitor=new Border{Width=Math.Max(1,screenWidth),Height=screenHeight,CornerRadius=new(4),BorderThickness=new(1),BorderBrush=Brush.Parse("#69818A"),Background=Brush.Parse("#DBE1D9")};Canvas.SetLeft(monitor,left);_navigatorGuide.Children.Add(monitor);
            if(screenWidth>=52)
            {
                var strip=new Border{Width=screenWidth-10,Height=4,CornerRadius=new(1),Background=Brush.Parse("#ADBDB8")};Canvas.SetLeft(strip,left+5);Canvas.SetTop(strip,screenHeight-8);_navigatorGuide.Children.Add(strip);
                var label=SettingsLabel($"DISPLAY {(index==0?i+1:index)}",8,Brush.Parse("#647D80"));Canvas.SetLeft(label,left+7);Canvas.SetTop(label,screenHeight/2-5);_navigatorGuide.Children.Add(label);
            }
            if(position=="Custom"||screenWidth<52)continue;
            var marker=new Border{Width=42,Height=17,CornerRadius=new(3),Background=SettingsInk,BorderBrush=ClockPickerPresentation.Gold,BorderThickness=new(1),Child=SettingsLabel("Clock",8,ClockPickerPresentation.Cream)};((TextBlock)marker.Child).HorizontalAlignment=HorizontalAlignment.Center;
            Canvas.SetLeft(marker,left+(position.EndsWith("Left")?7:position.EndsWith("Right")?screenWidth-49:(screenWidth-42)/2));Canvas.SetTop(marker,position.StartsWith("Bottom")?screenHeight-24:7);_navigatorGuide.Children.Add(marker);
        }
        _navigatorGuideLabel!.Text=position=="Custom"?"Custom placement":position;
        var screen=index>0&&index<=screens.Count?screens[index-1]:null;
        _navigatorGuideCaption!.Text=position=="Custom"?"Uses your saved overlay position.":index==0?"Same preset on all connected displays.":$"{screen?.Bounds.Width} × {screen?.Bounds.Height}"+(screen?.IsPrimary==true?" · Primary display":"");
        ToolTip.SetTip(ScreenCombo,ScreenCombo.SelectedItem?.ToString());
    }
}
