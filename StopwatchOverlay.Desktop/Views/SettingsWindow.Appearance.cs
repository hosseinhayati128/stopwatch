using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class SettingsWindow
{
    internal void RefreshPageScale(){if(!_closed)ResizeNavigatorSettings();}
    private Grid? _appearancePage,_appearanceBody,_appearanceForms,_appearanceSample;
    private ScrollViewer? _appearanceFormScroll,_appearancePreviewScroll,_appearanceCompactScroll;
    private StackPanel? _appearanceAppFields,_appearanceClockFields;
    private Border? _appearanceNikaClock,_appearanceClassicText,_appearanceClassicVisibility,_appearanceParts,_appearanceAppStage,_appearanceClockStage,_appearanceSpecimen;
    private Button? _appearanceAppTab,_appearanceClockTab;
    private TextBlock? _appearancePreviewTitle,_appearancePreviewCaption,_appearanceTypeHeading,_appearanceTypeDigits;
    private TextBlock? _appearanceBackgroundWarning;
    private Border? _appearanceNikaSummary;
    private TextBlock? _appearanceStyleSummary,_appearanceOpacitySummary;
    private Image? _appearanceApplicationImage;
    private ThemedClockFace? _appearanceClock;
    private ControllerWindow? _appearanceController;
    private RenderTargetBitmap? _appearanceSnapshot;
    private string? _appearanceSnapshotKey;
    private bool _appearanceShowingClock;
    private bool? _appearanceCompact;
    private readonly List<(Control Control,Panel Parent,int Index,Thickness Margin,int Column,int Row)> _appearanceMoved=new();

    private Control AppearanceField(string title,Control control)
    {
        AutomationProperties.SetName(control,title);
        return new StackPanel{Spacing=5,Children={SettingsLabel(title,12,SettingsInk),control}};
    }
    private Control AppearanceSlider(string title,Slider slider,string unit)
    {
        var value=SettingsLabel("",12,SettingsInk);
        value.Bind(TextBlock.TextProperty,new Binding(nameof(Slider.Value)){Source=slider,StringFormat="{0:0}"+unit});
        var header=new Grid{ColumnDefinitions=new("*,Auto"),Children={SettingsLabel(title,12,SettingsInk),value}};Grid.SetColumn(value,1);
        AutomationProperties.SetName(slider,title);
        return new StackPanel{Spacing=3,Children={header,slider}};
    }
    private static Border AppearanceCard(string title,params Control[] controls)
    {
        var stack=new StackPanel{Spacing=8,Children={SettingsLabel(title,10)}};
        foreach(var control in controls)stack.Children.Add(control);
        return SettingsCard(stack);
    }
    private void BuildAppearancePage(Grid host)
    {
        foreach(var control in new Control[]{ThemeCombo,NikaBackgroundCombo,NikaTypographyCombo,NikaArtworkCombo,UiScaleSlider,OverlayThemeCombo,FormatCombo,TextSizeSlider,TextColorCombo,FontCombo,BorderColorCombo,BorderWidthSlider,BackgroundOpacitySlider,InactiveSeparatedOpacitySlider,TransparencyDetailsExpander,BackgroundCombo,AddBackgroundButton,RemoveBackgroundButton,BackgroundStrengthSlider})
        {
            var parent=(Panel)control.Parent!;
            _appearanceMoved.Add((control,parent,parent.Children.IndexOf(control),control.Margin,Grid.GetColumn(control),Grid.GetRow(control)));
        }
        foreach(var item in _appearanceMoved){item.Parent.Children.Remove(item.Control);item.Control.Margin=default;Grid.SetColumn(item.Control,0);Grid.SetRow(item.Control,0);}
        _appearanceAppTab=SettingsAction("App & panels","AppearanceAppTab");_appearanceClockTab=SettingsAction("Floating clock","AppearanceClockTab");
        foreach(var tab in new[]{_appearanceAppTab,_appearanceClockTab}){tab.Width=146;tab.Height=32;}
        _appearanceAppTab.Click+=(_,_)=>ShowAppearanceSection(false);_appearanceClockTab.Click+=(_,_)=>ShowAppearanceSection(true);
        var tabs=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8,Children={_appearanceAppTab,_appearanceClockTab}};
        var importRow=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8,Children={AddBackgroundButton,RemoveBackgroundButton}};
        AddBackgroundButton.Content="Add image…";
        _appearanceBackgroundWarning=SettingsLabel("",11,Brush.Parse("#AD3434"));
        _appearanceAppFields=new StackPanel{Spacing=12,Children={
            AppearanceCard("APPLICATION",AppearanceField("Theme",ThemeCombo),SettingsLabel("Sets the look of the controller and app pages.",11)),
            AppearanceCard("NIKA’S CHRONICLE",AppearanceField("Background",NikaBackgroundCombo),AppearanceField("Typography",NikaTypographyCombo),AppearanceField("Sidebar artwork",NikaArtworkCombo)),
            AppearanceCard("SIZE & BACKGROUND",AppearanceSlider("Page scale",UiScaleSlider,"%"),SettingsLabel("Applied when you finish adjusting. Floating clock text has its own size.",11),AppearanceField("Application background",BackgroundCombo),importRow,AppearanceSlider("Background strength",BackgroundStrengthSlider,"%"),_appearanceBackgroundWarning)}};
        var open=SettingsAction("Open Stopwatch appearance","AppearanceOpenStopwatch");open.HorizontalAlignment=HorizontalAlignment.Stretch;
        open.Click+=(_,_)=>NavigationList.SelectedItem=NavigationList.Items.OfType<ListBoxItem>().First(i=>Equals(i.Tag,"StopwatchAppearance"));
        _appearanceNikaClock=AppearanceCard("NIKA CLOCK STYLE",SettingsLabel("Frame, background, typography, artwork and hover controls.",12),open,SettingsLabel("Text colors follow your clock background automatically.",11,Brush.Parse("#2D7169")));
        _appearanceClassicText=AppearanceCard("TEXT & OUTLINE",AppearanceField("Text color",TextColorCombo),AppearanceField("Timer font",FontCombo),AppearanceField("Outline color",BorderColorCombo),AppearanceSlider("Outline thickness",BorderWidthSlider," px"));
        _appearanceClassicVisibility=AppearanceCard("VISIBILITY",AppearanceSlider("Timer surface opacity",BackgroundOpacitySlider,"%"),AppearanceSlider("Inactive separated clock",InactiveSeparatedOpacitySlider,"%"));
        _appearanceParts=SettingsCard(TransparencyDetailsExpander);
        _appearanceClockFields=new StackPanel{Spacing=12,Children={AppearanceCard("FLOATING CLOCK",AppearanceField("Theme",OverlayThemeCombo),AppearanceField("Time format",FormatCombo),AppearanceSlider("Text size",TextSizeSlider," px")),_appearanceNikaClock,_appearanceClassicText,_appearanceClassicVisibility,_appearanceParts}};
        _appearanceForms=new Grid{Children={_appearanceAppFields,_appearanceClockFields}};
        _appearanceFormScroll=new ScrollViewer{Name="AppearanceFormScroll",Content=_appearanceForms,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new(0,0,5,0)};
        _appearanceApplicationImage=new Image{Name="AppearanceApplicationImage",Stretch=Stretch.Uniform};
        _appearanceAppStage=new Border{Child=_appearanceApplicationImage,Height=210,Background=Brush.Parse("#D5BA89"),BorderBrush=SettingsRule,BorderThickness=new(1),CornerRadius=new(5),ClipToBounds=true};
        _appearanceClock=new ThemedClockFace{ProjectName="Programming"};_appearanceClock.SetStopwatchPresentationState(new(true,true,false));
        _appearanceClockStage=new Border{Height=154,CornerRadius=new(6),Background=Brush.Parse("#192F3E"),Child=new Viewbox{Child=_appearanceClock,Margin=new(16),MaxWidth=300,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly}};
        _appearanceTypeHeading=SettingsLabel("A page from your journal",22,SettingsInk);_appearanceTypeDigits=SettingsLabel("00:44:27",26,SettingsInk);
        _appearanceSpecimen=SettingsCard(new StackPanel{Spacing=4,Children={_appearanceTypeHeading,_appearanceTypeDigits,SettingsLabel("Typography sample",10)}});
        _appearanceSample=new Grid{Children={_appearanceAppStage,_appearanceClockStage,_appearanceSpecimen}};
        _appearancePreviewTitle=SettingsLabel("",18,SettingsInk);_appearancePreviewCaption=SettingsLabel("",11);
        _appearanceStyleSummary=SettingsLabel("",11);_appearanceOpacitySummary=SettingsLabel("",11);
        _appearanceNikaSummary=AppearanceCard("CURRENT NIKA STYLE",_appearanceStyleSummary,_appearanceOpacitySummary);
        var preview=SettingsCard(new StackPanel{Spacing=10,Children={_appearancePreviewTitle,_appearancePreviewCaption,_appearanceSample,_appearanceNikaSummary,SettingsLabel("App appearance and floating clock style are independent.",11,Brush.Parse("#2D7169")),SettingsLabel("Sample content · preview fitted to this panel",10)}});preview.VerticalAlignment=VerticalAlignment.Top;
        _appearancePreviewScroll=new ScrollViewer{Content=preview,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        _appearanceBody=new Grid{Children={_appearanceFormScroll,_appearancePreviewScroll}};
        _appearanceCompactScroll=new ScrollViewer{Name="AppearanceCompactScroll",Content=_appearanceBody,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        _appearancePage=new Grid{Classes={"appearancePage"},RowDefinitions=new("48,40,12,*"),Children={new StackPanel{Spacing=3,Children={SettingsLabel("Appearance",23,SettingsInk),SettingsLabel("Choose how your app and floating clock look.",12)}},tabs,_appearanceCompactScroll}};
        Grid.SetRow(tabs,1);Grid.SetRow(_appearanceCompactScroll,3);
        // Scoped setters leave every original theme's control values intact when the page is restored.
        var comboStyle=new Style(s=>s.OfType<Grid>().Class("appearancePage").Descendant().OfType<ComboBox>());
        foreach(var setter in new[]{new Setter(FontSizeProperty,12d),new Setter(TemplatedControl.FontFamilyProperty,ClockPickerPresentation.Body),new Setter(MinHeightProperty,0d),new Setter(MinWidthProperty,0d),new Setter(HeightProperty,34d),new Setter(TemplatedControl.PaddingProperty,new Thickness(10,4))})comboStyle.Setters.Add(setter);
        _appearancePage.Styles.Add(comboStyle);host.Children.Add(_appearancePage);
        ShowAppearanceSection(_appearanceShowingClock);
        ResizeAppearancePage((ClientSize.Width>0?ClientSize.Width:Width)/DesktopPageAppearance.Scale(this)<1000);
    }
    private void ShowAppearanceSection(bool clock)
    {
        _appearanceShowingClock=clock;
        if(_appearancePage==null)return;
        _appearanceAppTab!.Classes.Set("editorPrimary",!clock);_appearanceClockTab!.Classes.Set("editorPrimary",clock);
        _appearanceAppFields!.IsVisible=!clock;_appearanceClockFields!.IsVisible=clock;
        _appearanceAppStage!.IsVisible=!clock;_appearanceClockStage!.IsVisible=clock;_appearanceSpecimen!.IsVisible=!clock;
        _appearancePreviewTitle!.Text=clock?"Clock preview":"Application preview";
        _appearanceFormScroll!.Offset=default;_appearanceCompactScroll!.Offset=default;
        RefreshAppearancePreview();
    }
    private void ResizeAppearancePage(bool compact)
    {
        if(_appearancePage==null||_appearanceCompact==compact)return;
        _appearanceCompact=compact;
        _appearanceBody!.ColumnDefinitions=new(compact?"*":"1.08*,16,1*");_appearanceBody.RowDefinitions=new(compact?"Auto,12,Auto":"*");
        Grid.SetColumn(_appearancePreviewScroll!,compact?0:2);Grid.SetRow(_appearanceFormScroll!,compact?2:0);
        _appearanceCompactScroll!.VerticalScrollBarVisibility=compact?ScrollBarVisibility.Auto:ScrollBarVisibility.Disabled;
        _appearanceFormScroll!.VerticalScrollBarVisibility=_appearancePreviewScroll!.VerticalScrollBarVisibility=compact?ScrollBarVisibility.Disabled:ScrollBarVisibility.Auto;
        _appearanceSample!.ColumnDefinitions=new(compact?"1.05*,12,*":"*");_appearanceSample.RowDefinitions=new(compact?"Auto":"Auto,10,Auto");
        Grid.SetColumn(_appearanceSpecimen!,compact?2:0);Grid.SetRow(_appearanceSpecimen!,compact?0:2);Grid.SetColumnSpan(_appearanceClockStage!,compact?3:1);
        _appearanceAppStage!.Height=compact?114:210;_appearanceClockStage!.Height=compact?90:154;
        _appearanceTypeHeading!.FontSize=compact?18:22;_appearanceTypeDigits!.FontSize=compact?24:26;
        _appearancePreviewCaption!.IsVisible=!compact;
        RefreshAppearancePreview();
    }
    private void RefreshAppearancePreview()
    {
        if(_closed||_appearancePage==null||CurrentCategory!="Appearance")return;
        _appearanceBackgroundWarning!.Text=DesktopPageAppearance.BackgroundWarning;_appearanceBackgroundWarning.IsVisible=DesktopPageAppearance.BackgroundWarning!=null;
        string theme=OverlayThemeCatalog.Resolve(_settings.OverlayTheme,_settings.ThemeMode);
        bool nika=theme==OverlayThemeCatalog.Nika;
        _appearanceNikaSummary!.IsVisible=_appearanceShowingClock&&nika&&_appearanceCompact!=true;
        if(_appearanceNikaSummary.IsVisible)
        {
            var style=_settings.StopwatchAppearance.Copy();style.Normalize();
            _appearanceStyleSummary!.Text=StopwatchAppearanceCatalog.Frames.First(c=>c.Id==style.Frame).Label+" · "+StopwatchAppearanceCatalog.Backgrounds.First(c=>c.Id==style.Background).Label;
            _appearanceOpacitySummary!.Text=$"Background {style.BackgroundOpacity:0}% · Unselected {style.UnselectedOpacity:0}%";
        }
        _appearanceNikaClock!.IsVisible=nika;_appearanceClassicText!.IsVisible=_appearanceClassicVisibility!.IsVisible=!nika;
        _appearanceParts!.IsVisible=theme==OverlayThemeCatalog.Pirate;
        if(_appearanceShowingClock)
        {
            _appearanceClock!.ApplySettings(_settings);_appearanceClock.Text=PreviewClock.Text;
            _appearancePreviewCaption!.Text=theme+" · active clock";
            return;
        }
        var controllerText=_settings.Typography.Resolve("Controller");
        string key=$"{_settings.ThemeMode}|{_settings.NikaBackground}|{_settings.NikaTypography}|{_settings.NikaSidebarArtwork}|{_settings.PanelBackgroundId}|{_settings.PanelBackgroundStrength}|{controllerText.SizePercent}|{controllerText.Color}";
        if(key==_appearanceSnapshotKey)return;
        _appearanceController??=new ControllerWindow(new AppSettings{ThemeMode=AppThemeCatalog.Nika});
        var snapshot=_appearanceController.RenderAppearancePreview(_settings);
        _appearanceApplicationImage!.Source=snapshot;_appearanceSnapshot?.Dispose();_appearanceSnapshot=snapshot;_appearanceSnapshotKey=key;
        if(_appearanceController.TryFindResource("NikaHeadingFont",out var heading)&&heading is FontFamily font)_appearanceTypeHeading!.FontFamily=font;
        if(_appearanceController.TryFindResource("NikaDigitsFont",out var digits)&&digits is FontFamily digitFont)_appearanceTypeDigits!.FontFamily=digitFont;
        _appearancePreviewCaption!.Text=NikaThemeCatalog.Backgrounds.First(b=>b.Id==NikaThemeCatalog.NormalizeBackground(_settings.NikaBackground)).Name;
    }
    private void ReleaseAppearancePreview()
    {
        if(_appearanceApplicationImage!=null)_appearanceApplicationImage.Source=null;
        _appearanceSnapshot?.Dispose();_appearanceSnapshot=null;_appearanceSnapshotKey=null;
        _appearanceController?.Close();_appearanceController=null;
    }
    private void RestoreAppearanceControls()
    {
        ReleaseAppearancePreview();
        foreach(var item in _appearanceMoved)
        {
            if(item.Control.Parent is Panel parent)parent.Children.Remove(item.Control);
            else if(item.Control.Parent is Decorator decorator)decorator.Child=null;
        }
        foreach(var item in _appearanceMoved.OrderBy(i=>i.Index))
        {
            item.Parent.Children.Insert(item.Index,item.Control);item.Control.Margin=item.Margin;Grid.SetColumn(item.Control,item.Column);Grid.SetRow(item.Control,item.Row);
        }
        _appearanceMoved.Clear();
        AddBackgroundButton.Content="Add";
        if(_appearancePage?.Parent is Panel host)host.Children.Remove(_appearancePage);
        _appearancePage=null;_appearanceCompact=null;
    }
}
