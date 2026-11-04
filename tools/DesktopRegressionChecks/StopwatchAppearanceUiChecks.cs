using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class StopwatchAppearanceUiChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    private static T Named<T>(Control root,string name) where T:Control=>root.GetLogicalDescendants().OfType<T>().FirstOrDefault(c=>c.Name==name)
        ?? throw new Exception("Control is not in the current logical tree: "+name);
    private static void Click(Button button)=>button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    internal static void Run()
    {
        string path=SettingsStore.SettingsPath;byte[]? before=File.Exists(path)?File.ReadAllBytes(path):null;
        try
        {
            var settings=new AppSettings{ThemeMode=AppThemeCatalog.Midnight,OverlayTheme=OverlayThemeCatalog.Pirate,TextColor="Theme default"};
            var window=new SettingsWindow(settings){ShowActivated=false,ShowInTaskbar=false};
            try
            {
                window.Show();ClockOverlayChecks.Pump(70);
                var nav=window.FindControl<ListBox>("NavigationList")!;
                var category=nav.Items.OfType<ListBoxItem>().SingleOrDefault(x=>Equals(x.Tag,"StopwatchAppearance"));
                Require(category!=null,"Stopwatch appearance category has not been implemented.");
                nav.SelectedItem=category;ClockOverlayChecks.Pump(60);
                var previewHost=window.FindControl<ContentControl>("StopwatchPreviewHost")!;
                Require(previewHost.GetVisualDescendants().OfType<Button>().Count(b=>b.IsEffectivelyVisible&&b.Bounds.Width>0)==3,
                    "Preview toolbar did not retain its three action buttons.");
                var glyphs=previewHost.GetVisualDescendants().OfType<Control>().Where(c=>c.GetType().Name=="StopwatchActionGlyph").ToArray();
                Require(glyphs.Length==3&&glyphs.All(g=>g.Bounds.Width>=20&&g.Bounds.Height>=20),
                    "Preview toolbar action glyphs are missing or collapsed: "+string.Join("; ",glyphs.Select(g=>g.Bounds)));
                for(int i=0;i<glyphs.Length;i++)
                {
                    var glyph=glyphs[i];
                    var pixels=StopwatchAppearanceChecks.Render(glyph,null);
                    Require(Enumerable.Range(0,pixels.Length/4).Any(p=>pixels[p*4+3]>240),"Preview action glyph rendered empty: "+i);
                }
                foreach(var (name,count) in new[]{("Background",12),("Frame",16),("Typography",10),("Artwork",3),("HoverControls",12)})
                {
                    var choice=Named<ComboBox>(window,"Stopwatch"+name+"Choice");
                    Require(choice.ItemCount==count,"Incorrect selector count: "+name);
                    Require(!string.IsNullOrWhiteSpace(AutomationProperties.GetName(choice)),"Unnamed selector: "+name);
                }
                Require(settings.StopwatchAppearance.BackgroundOpacity==70&&settings.StopwatchAppearance.UnselectedOpacity==40,"Incorrect fresh opacity defaults.");
                int changes=0;Action<SettingsChangeKind> changed=k=>{Require(k is SettingsChangeKind.OverlayAppearance or SettingsChangeKind.OverlayGeometry,"Wrong appearance event.");changes++;};
                typeof(SettingsWindow).GetEvent("SettingsChanged",Private)!.GetAddMethod(true)!.Invoke(window,new object[]{changed});
                string serialized=JsonSerializer.Serialize(settings);
                Named<ComboBox>(window,"StopwatchPreviewState").SelectedIndex=1;
                Named<ComboBox>(window,"StopwatchPreviewBackdrop").SelectedIndex=1;
                Require(changes==0&&serialized==JsonSerializer.Serialize(settings),"Preview controls changed application settings.");
                Named<ComboBox>(window,"StopwatchFrameChoice").SelectedItem=StopwatchAppearanceCatalog.Frames[0];
                Require(changes==1&&settings.StopwatchAppearance.Frame=="classic-brass","Style did not issue one immediate event.");
                Require(settings.StopwatchAppearance.Background=="navigator-parchment"&&settings.StopwatchAppearance.HoverControls=="filigree-keys","Frame selection changed independent choices.");
                typeof(SettingsWindow).GetMethod("ReportPersistenceResult",Private)!.Invoke(window,new object[]{false});
                Require(Named<TextBlock>(window,"StopwatchSaveStatus").Text!.Contains("could not be saved"),"Save failure is not visible.");
                Require(settings.StopwatchAppearance.Frame=="classic-brass","Save failure rolled back the session choice.");
                Click(Named<Button>(window,"UseNikaStopwatchOverlay"));
                Require(settings.OverlayTheme==OverlayThemeCatalog.Nika&&settings.ThemeMode==AppThemeCatalog.Midnight,"Use Nika changed the application theme.");
                Require(!window.FindControl<ComboBox>("FontCombo")!.IsEnabled&&!window.FindControl<Slider>("BackgroundOpacitySlider")!.IsEnabled,"Legacy controls remain editable for Nika.");
                var tabs=Named<TabControl>(window,"StopwatchAppearanceTabs");tabs.SelectedIndex=1;ClockOverlayChecks.Pump(40);
                var background=Named<Slider>(window,"StopwatchBackgroundOpacity");var unselected=Named<Slider>(window,"StopwatchUnselectedOpacity");
                Require(background.Minimum==0&&background.Maximum==100&&unselected.Minimum==10&&unselected.Maximum==100,"Wrong opacity ranges.");
                var art=Named<CheckBox>(window,"StopwatchOpaqueArtwork");Require(!art.IsEnabled,"Clean artwork enables its opacity checkbox.");
                settings.StopwatchAppearance.OpaqueLayers|=StopwatchOpaqueLayers.Artwork;
                tabs.SelectedIndex=0;Named<ComboBox>(window,"StopwatchArtworkChoice").SelectedItem=StopwatchAppearanceCatalog.Artworks[1];
                tabs.SelectedIndex=1;Require(art.IsEnabled&&art.IsChecked==true,"Artwork opacity preference was not remembered.");
                int prior=changes;background.Value=0;Require(changes==prior+1&&settings.StopwatchAppearance.BackgroundOpacity==0,"Slider lost immediate persistence event.");
                background.Value=70;unselected.Value=40;
                ClockOverlayChecks.Pump(60);
                glyphs=previewHost.GetVisualDescendants().OfType<Control>().Where(c=>c.GetType().Name=="StopwatchActionGlyph").ToArray();
                var previewWithGlyphs=StopwatchAppearanceChecks.Render(previewHost,null);
                foreach(var glyph in glyphs)glyph.Opacity=0;
                var previewWithoutGlyphs=StopwatchAppearanceChecks.Render(previewHost,null);
                foreach(var glyph in glyphs)glyph.Opacity=1;
                Require(!previewWithGlyphs.SequenceEqual(previewWithoutGlyphs),"Toolbar action glyphs disappeared from the composed preview after editing settings.");
                var summary=Named<TextBlock>(window,"StopwatchStateSummary");Require(summary.Text!.Contains("ACTIVE")&&summary.Text.Contains("project name"),"Fixed state behavior summary is missing.");
                foreach(var size in new[]{new Size(840,600),new Size(1060,740)})
                {
                    window.Width=size.Width;window.Height=size.Height;ClockOverlayChecks.Pump(60);
                    var host=window.FindControl<ContentControl>("StopwatchPreviewHost")!;
                    Require(host.IsEffectivelyVisible&&host.Bounds.Width>120&&host.Bounds.Height>200,"Preview is clipped or hidden.");
                    var scroller=window.FindControl<ScrollViewer>("SettingsScrollViewer")!;
                    Require(scroller.Extent.Width<=scroller.Viewport.Width+1,"Inspector overflows horizontally.");
                    var done=window.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Content,"Done"));
                    var donePoint=done.TranslatePoint(default,window)!.Value;
                    Require(done.IsEffectivelyVisible&&donePoint.X>=0&&donePoint.X+done.Bounds.Width<=window.ClientSize.Width+1,"Done button is clipped.");
                    foreach(int tab in new[]{0,1}){tabs.SelectedIndex=tab;ClockOverlayChecks.Pump(40);StopwatchAppearanceChecks.Render(window,$"settings-stopwatch-{size.Width:0}-tab-{tab}");}
                }
                tabs.SelectedIndex=0;ClockOverlayChecks.Pump(40);
                foreach(string kind in new[]{"Background","Frame","Typography","Artwork","HoverControls"})
                {
                    var choice=Named<ComboBox>(window,"Stopwatch"+kind+"Choice");
                    choice.BringIntoView();choice.Focus(NavigationMethod.Tab);
                    Require(choice.IsFocused,"Selector cannot receive keyboard focus: "+kind);
                    choice.IsDropDownOpen=true;ClockOverlayChecks.Pump(40);
                    Require(choice.IsDropDownOpen,"Selector popup did not open: "+kind);
                    choice.SelectedIndex=choice.ItemCount-1;ClockOverlayChecks.Pump(40);
                    Require(choice.SelectedItem is StopwatchAppearanceOption,"Last dropdown choice is unreachable: "+kind);
                    choice.IsDropDownOpen=false;
                }
                var frameChoice=Named<ComboBox>(window,"StopwatchFrameChoice");frameChoice.SelectedIndex=0;
                frameChoice.Focus();frameChoice.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Avalonia.Input.Key.Down});
                Require(frameChoice.SelectedIndex==1,"Keyboard arrow navigation did not change the frame.");
                frameChoice.SelectedIndex=0;
                var sizeChoice=Named<Slider>(window,"StopwatchTextSize");
                var legacySize=window.FindControl<Slider>("TextSizeSlider")!;
                nav.SelectedItem=nav.Items.OfType<ListBoxItem>().Single(x=>Equals(x.Tag,"Appearance"));
                legacySize.Value=80;
                nav.SelectedItem=category;ClockOverlayChecks.Pump(40);
                Require(settings.TextSize==80&&sizeChoice.Value==80,"Shared text size is stale after changing the existing appearance slider.");
                int priorSizeChanges=changes;sizeChoice.Value=81;
                Require(settings.TextSize==81&&legacySize.Value==81&&changes==priorSizeChanges+1,"New text-size slider did not synchronize exactly once.");
                Console.WriteLine($"Inspector verified at native render scaling {window.RenderScaling:P0}; live settings bytes unchanged.");
            }
            finally{window.Close();}
            var restored=JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;restored.NormalizeForRuntime();
            var reopened=new SettingsWindow(restored){ShowActivated=false,ShowInTaskbar=false};
            try
            {
                reopened.Show();
                var navigation=reopened.FindControl<ListBox>("NavigationList")!;
                navigation.SelectedItem=navigation.Items.OfType<ListBoxItem>().Single(x=>Equals(x.Tag,"StopwatchAppearance"));
                ClockOverlayChecks.Pump(60);
                Require(((StopwatchAppearanceOption)Named<ComboBox>(reopened,"StopwatchFrameChoice").SelectedItem!).Id=="classic-brass","Reopened inspector lost style.");
            }
            finally{reopened.Close();}
        }
        finally{Require(before==null?!File.Exists(path):File.ReadAllBytes(path).SequenceEqual(before),"Fixture modified real settings.");}
    }
}
