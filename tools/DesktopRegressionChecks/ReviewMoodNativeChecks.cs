using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using System.Reflection;
using System.Text.Json;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;
using StopwatchOverlay.PeriodicReview;
using Fixture=ReviewProjectsNativeChecks.Fixture;

internal static class ReviewMoodNativeChecks
{
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Click(Button button){button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));ClockOverlayChecks.Pump(20);}
    private static void EnterMood(Fixture f){f.Add("Programming");Click(f.Next);Click(f.Named<Button>("Step2ContinueButton"));}
    private static void Key(Control control,Key key)
    {
        control.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=key});
        control.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyUpEvent,Key=key});ClockOverlayChecks.Pump(20);
    }
    private static object? Invoke(Fixture f,string method,params object[] args)=>typeof(PeriodicReviewWindow).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(f.Window,args);
    private static Button Tag(Fixture f,string keyword)=>f.Named<WrapPanel>("FeelingsKeywordsContainer").Children.OfType<Button>().Single(b=>string.Equals(b.Tag as string,keyword,StringComparison.OrdinalIgnoreCase));
    private static Button Preset(Fixture f,double score)=>f.Named<WrapPanel>("MoodPresetsContainer").Children.OfType<Button>().Single(b=>b.Tag is double value&&value==score);
    private static bool Selected(Fixture f,string word)=>Tag(f,word).Classes.Contains("editorPrimary");
    private static void AddTag(Fixture f,string text){var input=f.Named<TextBox>("NewKeywordTextBox");input.Focus();input.Text=text;Key(input,Avalonia.Input.Key.Enter);}
    private static ReviewProjectSelectionItem Item(Fixture f,string name,int start,int minutes,bool recorded=false,bool isBreak=false)=>new(){ProjectName=name,OriginalProjectName=name,StartUtc=f.Start.AddMinutes(start),EndUtc=f.Start.AddMinutes(start+minutes),AllocatedMinutes=minutes,IsFixedTracked=recorded,IsBreak=isBreak};
    private const string Output="design/one-piece-v2/activity-review/mood-native-implementation";
    private static void Capture(Fixture f,string name)
    {
        Directory.CreateDirectory(Output);ClockOverlayChecks.Pump(25);
        var frame=f.Window.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single(c=>c.IsEffectivelyVisible);
        using var image=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(frame.Bounds.Width),(int)Math.Ceiling(frame.Bounds.Height)),new Vector(96,96));image.Render(frame);image.Save(System.IO.Path.Combine(Output,name+".png"));
    }
    internal static void Layout()
    {
        using var f=new Fixture(feelings:true);EnterMood(f);
        Require(f.Step==3&&f.Window.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(x=>x.IsEffectivelyVisible),"Mood step is missing the approved shared frame.");
        var mood=f.Named<ScrollViewer>("ReviewMoodScroll");var feelings=f.Named<ScrollViewer>("ReviewFeelingsScroll");
        Require(mood.Extent.Height<=mood.Viewport.Height+1&&feelings.Extent.Height<=feelings.Viewport.Height+1,$"Default mood controls must fit without scrolling: mood {mood.Extent.Height}/{mood.Viewport.Height}; feelings {feelings.Extent.Height}/{feelings.Viewport.Height}.");
        f.Window.Height=520;ClockOverlayChecks.Pump(25);var save=f.Named<Button>("Step3SaveButton");double y=save.TranslatePoint(default,f.Window)!.Value.Y;
        mood.Offset=new(0,10000);feelings.Offset=new(0,10000);ClockOverlayChecks.Pump(20);
        Require(mood.Extent.Height>mood.Viewport.Height&&feelings.Extent.Height>feelings.Viewport.Height,"Short mood panes must scroll independently.");
        Require(Math.Abs(save.TranslatePoint(default,f.Window)!.Value.Y-y)<1&&y+save.Bounds.Height<f.Window.Bounds.Height-15,"Scrolling moved or clipped the mood footer.");
        f.Window.Width=850;ClockOverlayChecks.Pump(20);
        foreach(var button in f.Named<WrapPanel>("MoodPresetsContainer").Children.OfType<Button>())Require(button.Bounds.Width>=26,"Preset buttons collapsed at minimum width.");
        Capture(f,"short-window");
    }
    internal static void Scores()
    {
        using var f=new Fixture(feelings:true,moodScore:4.8);EnterMood(f);var slider=f.Named<Slider>("MoodSlider");
        Require(Math.Abs(slider.Value-4.8)<.001&&f.Named<TextBlock>("MoodDescriptorText").Text=="Down / Struggling","Configured default score or descriptor lost.");
        Require(f.Named<TextBlock>("MoodScoreBigText").Foreground is ISolidColorBrush scoreInk&&scoreInk.Color==Color.Parse("#8B5C24")&&f.Named<TextBlock>("MoodDescriptorText").Foreground is ISolidColorBrush descriptorInk&&descriptorInk.Color==scoreInk.Color,"Initial score/descriptor lost their mood-band color when attached to the frame.");
        Click(f.Named<Button>("IncreaseMoodButton"));Require(Math.Abs(slider.Value-4.9)<.001,"Mood increase is not 0.1.");
        Click(f.Named<Button>("DecreaseMoodButton"));Require(Math.Abs(slider.Value-4.8)<.001,"Mood decrease is not 0.1.");
        foreach(double value in new[]{1d,3,5,7,8.5,10})
        {
            Click(Preset(f,value));Require(slider.Value==value&&Preset(f,value).Classes.Contains("editorPrimary"),"Mood preset or active state failed.");
            Require(f.Named<WrapPanel>("MoodPresetsContainer").Children.OfType<Button>().Count(b=>b.Classes.Contains("editorPrimary"))==1,"Multiple mood presets appear active.");
        }
        Require(!f.Named<Button>("IncreaseMoodButton").IsEnabled,"Upper score limit remains enabled.");Click(Preset(f,1));Require(!f.Named<Button>("DecreaseMoodButton").IsEnabled,"Lower score limit remains enabled.");
        slider.Value=7.63;ClockOverlayChecks.Pump(15);Require(f.Named<TextBlock>("MoodScoreBigText").Text==7.6.ToString("0.0"),"Score display lost one-decimal precision.");
        var preset=Preset(f,8.5);preset.Focus();Key(preset,Avalonia.Input.Key.Enter);
        Require(slider.Value==8.5&&f.Step==3&&f.Saves==0&&f.MoodSaves==0,"Preset Enter did not activate once without saving.");
    }
    internal static void FeelingsAndNotes()
    {
        using var f=new Fixture(feelings:true);EnterMood(f);var tags=f.Named<WrapPanel>("FeelingsKeywordsContainer");int original=tags.Children.Count;
        var focused=Tag(f,"Focused");focused.Focus();Require(focused.IsFocused,"Feeling button could not receive keyboard focus.");
        Key(focused,Avalonia.Input.Key.Enter);Require(Selected(f,"Focused")&&focused.IsFocused&&ReferenceEquals(focused,Tag(f,"Focused"))&&f.Saves==0,"Feeling keyboard activation lost focus or saved.");
        Key(focused,Avalonia.Input.Key.Space);Require(!Selected(f,"Focused")&&focused.IsFocused&&f.Saves==0,"Space did not deselect the focused feeling.");
        Click(Tag(f,"Calm"));Click(Tag(f,"Motivated"));Require(f.Named<TextBlock>("ReviewMoodSelectedCount").Text=="2 selected","Selected feeling count incorrect.");
        AddTag(f,"  grateful  ");AddTag(f,"GRATEFUL");Require(tags.Children.Count==original+1&&Selected(f,"Grateful"),"Custom feeling duplicate/case handling changed.");
        AddTag(f,"  ");Require(tags.Children.Count==original+1,"Blank custom feeling was added.");
        AddTag(f,"آرام");Require(Tag(f,"آرام").GetVisualDescendants().OfType<TextBlock>().Any(t=>t.FlowDirection==FlowDirection.RightToLeft),"Persian feeling lost RTL direction.");
        var input=f.Named<TextBox>("NewKeywordTextBox");input.Text="Creative";var add=f.Named<Button>("AddNewKeywordButton");add.Focus();Key(add,Avalonia.Input.Key.Enter);
        Require(Selected(f,"Creative")&&f.Step==3&&f.Saves==0,"Add-button Enter saved or failed to add.");
        var note=f.Named<TextBox>("MoodNoteTextBox");note.Focus();note.Text="First line";note.CaretIndex=note.Text.Length;Key(note,Avalonia.Input.Key.Enter);
        Require(note.Text!.Contains('\n')&&f.Step==3&&f.Saves==0,"Reflection Enter did not insert a newline without saving.");
        note.Text="  بعد از استراحت بهتر بودم.\nتمرکز بیشتری داشتم.  ";ClockOverlayChecks.Pump(15);Require(note.FlowDirection==FlowDirection.RightToLeft,"Persian note lost RTL direction.");
        f.Window.Width=850;f.Window.Height=520;AddTag(f,new string('W',180));
        Require(tags.Children.OfType<Button>().All(b=>b.Bounds.Width<=tags.Bounds.Width+1),"Long keyword exceeds the feeling pane.");
        Require(f.Saves==0&&f.MoodSaves==0&&f.SettingsWrites==0,"Editing feelings wrote live-like data prematurely.");
    }
    internal static void NavigationAndSummary()
    {
        using var f=new Fixture(feelings:true);f.Add("Programming");Click(f.Next);
        var a=Item(f,"Programming",0,12);var b=Item(f,"Illustration",12,10,true);var rest=Item(f,"Break / Empty",22,3,isBreak:true);
        f.Selections.Clear();f.Selections.AddRange([a,b,rest]);Invoke(f,"RenderAllocatedCards");Invoke(f,"SyncAllViews",true);Click(f.Named<Button>("Step2ContinueButton"));
        Require(f.Named<TextBlock>("ReviewMoodProjectTime").Text=="22 min of project time · 2 projects"&&f.Named<TextBlock>("ReviewMoodRemainingTime").Text=="3 min break · 5 min unallocated","Mood summary did not reflect project time, breaks and gaps.");
        f.Named<Slider>("MoodSlider").Value=7.6;Click(Tag(f,"Calm"));var note=f.Named<TextBox>("MoodNoteTextBox");note.Text="Keep this note";
        Key(f.Window,Avalonia.Input.Key.Escape);Require(f.Step==2&&f.Window.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(x=>x.IsEffectivelyVisible),"Mood back lost allocation frame.");
        a.AllocatedMinutes=8;a.EndUtc=f.Start.AddMinutes(8);Invoke(f,"SyncAllViews",true);Click(f.Named<Button>("Step2ContinueButton"));
        Require(f.Named<Slider>("MoodSlider").Value==7.6&&Selected(f,"Calm")&&note.Text=="Keep this note","Back/forward lost mood edits.");
        Require(f.Named<TextBlock>("ReviewMoodProjectTime").Text=="18 min of project time · 2 projects"&&f.Named<TextBlock>("ReviewMoodRemainingTime").Text=="3 min break · 9 min unallocated","Returning to mood left a stale time summary.");
        f.Selections.Clear();Invoke(f,"UpdateNavigatorMoodSummary");Require(f.Named<TextBlock>("ReviewMoodProjectTime").Text=="0 min of project time · 0 projects"&&f.Named<TextBlock>("ReviewMoodProjectNames").Text=="No project time allocated","Empty summary is misleading.");
    }
    internal static void SaveAndCancel()
    {
        using(var f=new Fixture(feelings:true,tracked:true))
        {
            EnterMood(f);f.Named<Slider>("MoodSlider").Value=7.63;Click(Tag(f,"Focused"));AddTag(f,"آرام");
            f.Named<TextBox>("MoodNoteTextBox").Text="  One\nTwo  ";var save=f.Named<Button>("Step3SaveButton");Click(save);Click(save);
            var mood=f.SavedMood;
            Require(f.Saves==1&&f.MoodSaves==1&&mood!=null,"Save and complete did not emit both results exactly once.");
            Require(mood!.Score==7.6&&mood.PeriodStartUtc==f.Start&&mood.PeriodEndUtc==f.Start.AddMinutes(30)&&mood.Note=="One\nTwo"&&mood.Keywords.Count==2&&mood.Keywords.Contains("Focused")&&mood.Keywords.Contains("آرام"),"Mood save payload changed.");
            Require(mood.AssociatedProjects.Contains("Programming")&&mood.AssociatedProjects.Contains("Illustration")&&f.Saved!.All(s=>!s.ExistingIntervalId.HasValue),"Mood save lost associated projects or rewrote a retained stopwatch interval.");
        }
        using(var f=new Fixture(feelings:true)){EnterMood(f);Click(Tag(f,"Happy"));Click(f.Named<Button>("Step3SkipButton"));Require(f.Saves==1&&f.MoodSaves==0&&f.Window.SavedMood==null,"Save time only emitted mood.");}
        using(var f=new Fixture(feelings:true)){EnterMood(f);f.Window.Focus();Key(f.Window,Avalonia.Input.Key.Enter);Require(f.Saves==1&&f.MoodSaves==1&&f.SavedMood!.Keywords.Count==0&&f.SavedMood.Note==null,"Optional empty feelings/note blocked keyboard save.");}
        using(var f=new Fixture(feelings:true)){EnterMood(f);Click(f.Named<Button>("ReviewMoodCloseButton"));Require(f.Saves==0&&f.MoodSaves==0&&!f.Window.IsVisible,"Close saved a cancelled review.");}
        foreach(var theme in new[]{AppThemeCatalog.Midnight,AppThemeCatalog.Daylight})
        {
            using var f=new Fixture(feelings:true,theme:theme);Invoke(f,"AddProjectInstance","Programming",false);Click(f.Next);Click(f.Named<Button>("Step2ContinueButton"));
            Require(f.Step==3&&!f.Window.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(),"Other themes received Navigator mood layout.");
            Click(f.Named<Button>("Step3SkipButton"));Require(f.Saves==1&&f.MoodSaves==0,"Legacy time-only save changed.");
        }
    }
    internal static void Evidence()
    {
        using var f=new Fixture(feelings:true);f.Add("Programming");Click(f.Next);f.Selections.Clear();f.Selections.AddRange([Item(f,"Programming",0,12),Item(f,"Illustration",12,10,true),Item(f,"Break / Empty",22,3,isBreak:true)]);
        Invoke(f,"RenderAllocatedCards");Invoke(f,"SyncAllViews",true);Click(f.Named<Button>("Step2ContinueButton"));Capture(f,"neutral");
        var slider=f.Named<Slider>("MoodSlider");var note=f.Named<TextBox>("MoodNoteTextBox");slider.Value=7.6;Click(Tag(f,"Calm"));Click(Tag(f,"Focused"));Click(Tag(f,"Motivated"));
        note.Text="A short break helped. I came back focused and made steady progress.";Capture(f,"selected-feelings");
        foreach(var word in new[]{"Calm","Focused","Motivated"})Click(Tag(f,word));slider.Value=2.4;Click(Tag(f,"Tired"));Click(Tag(f,"Overwhelmed"));note.Text="Felt drained after switching between tasks. Needed a longer break.";Capture(f,"low-mood");
        Click(Tag(f,"Tired"));Click(Tag(f,"Overwhelmed"));slider.Value=8.5;Click(Tag(f,"Focused"));AddTag(f,"آرام");f.Named<TextBox>("NewKeywordTextBox").Text="خلاق";note.Text="بعد از یک استراحت کوتاه، با آرامش و تمرکز به کار برگشتم.";f.Window.Focus();Capture(f,"custom-feeling-rtl");
        for(int i=0;i<25;i++)AddTag(f,$"Custom feeling {i:D2}");f.Window.Height=520;f.Window.Width=850;ClockOverlayChecks.Pump(20);
        var feelings=f.Named<ScrollViewer>("ReviewFeelingsScroll");feelings.Offset=new(0,10000);ClockOverlayChecks.Pump(20);Require(feelings.Offset.Y>0,"Many feelings cannot scroll to note.");Capture(f,"many-feelings");
        File.WriteAllText(System.IO.Path.Combine(Output,"verification.json"),JsonSerializer.Serialize(new{productionControls=true,privateDesktop=true,syntheticData=true,
            checks=new[]{"frame and fixed footer","configured score, limits, presets and decimals","keyboard-safe tags and notes","duplicate and RTL custom feelings","long tags and short windows","navigation state and live summary","save/skip/cancel payloads","legacy themes"}},new JsonSerializerOptions{WriteIndented=true}));
    }
}
