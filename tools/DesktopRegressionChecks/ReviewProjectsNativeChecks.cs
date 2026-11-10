using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Automation;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using System.Reflection;
using System.Text.Json;
using StopwatchOverlay;
using StopwatchOverlay.Desktop;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;
using StopwatchOverlay.PeriodicReview;

internal static class ReviewProjectsNativeChecks
{
    private const string Output="design/one-piece-v2/activity-review/native-implementation";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Click(Button button){button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));ClockOverlayChecks.Pump(15);}
    private static void Key(Control control,Key key){control.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=key});ClockOverlayChecks.Pump(15);}
    private sealed class Fixture:IDisposable
    {
        internal readonly PeriodicReviewWindow Window;
        internal readonly AppSettings Settings;
        internal readonly PeriodicReviewModel Model;
        internal readonly DateTime Start=DateTime.Today.AddHours(14).ToUniversalTime();
        internal int Registrations,SettingsWrites,Loads,Saves;
        internal List<PeriodicReviewStopwatchSlot>? Saved;
        internal bool Cancelled;
        internal Func<CancellationToken,Task<PeriodicReviewModel>>? Loader;
        internal T Named<T>(string name) where T:Control=>Window.FindControl<T>(name)??Window.GetVisualDescendants().OfType<T>().First(c=>c.Name==name);
        internal List<ReviewProjectSelectionItem> Selections=>(List<ReviewProjectSelectionItem>)typeof(PeriodicReviewWindow).GetField("_selectedItems",Private)!.GetValue(Window)!;
        internal int Step=>(int)typeof(PeriodicReviewWindow).GetField("_currentStep",Private)!.GetValue(Window)!;
        internal Button Next=>Named<Button>("ContinueToStep2Button");
        internal Fixture(bool offline=false,bool tracked=false,bool feelings=false,string theme=AppThemeCatalog.Nika,Task<PeriodicReviewModel>? pending=null,int extraProjects=0)
        {
            Require(Application.Current is not App,"Review fixture loaded live application settings.");
            typeof(App).Assembly.GetType("StopwatchOverlay.Desktop.ThemeResources")!.GetMethod("ApplyApplication",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[theme]);
            Settings=new AppSettings{ThemeMode=theme,ActivityWatchEnabled=false,PeriodicReviewMinActivityPercent=3,PeriodicReviewFeelingsEnabled=feelings};
            var history=new ProjectTimeHistory();
            foreach(var name in new[]{"Programming","Illustration","Reading","Learning","Writing","Art","Research"})history.RegisterProject(name);
            for(int i=0;i<extraProjects;i++)history.RegisterProject($"Long project {i:D2} — detailed research notes and additional context");
            if(tracked)history.AddManualInterval("Illustration",Start.AddMinutes(10),Start.AddMinutes(20));
            Model=new PeriodicReviewModel{StartUtc=Start,EndUtc=Start.AddMinutes(30),ActivityWatchAvailable=!offline,
                StopwatchSlots=PeriodicReviewDataAggregator.SliceStopwatchIntervals(Start,Start.AddMinutes(30),history.CreateView(Start.AddMinutes(30))),
                RawActivitySummaries=offline?[]:[Activity("VS Code","App",900,50),Activity("Chrome","Web",540,30),Activity("Away from keyboard","Idle",240,13.333),
                    Activity("Terminal","App",45,2.5),Activity("Files","App",45,2.5),Activity("Music","App",30,1.667)]};
            if(pending!=null)Loader=_=>pending;
            Window=new PeriodicReviewWindow(Start,Start.AddMinutes(30),Settings,history.CreateView(Start.AddMinutes(30)),null,
                slots=>{Saves++;Saved=slots;},name=>{Registrations++;return name.Trim();},loadReviewData:ct=>{Loads++;ct.Register(()=>Cancelled=true);return Loader?.Invoke(ct)??Task.FromResult(Model);},
                persistSettings:_=>SettingsWrites++);
            Window.ShowActivated=false;Window.ShowInTaskbar=false;Window.Show();ClockOverlayChecks.Pump(35);
        }
        internal Button Project(string name)=>Named<Grid>("ReviewProjectsGrid").Children.OfType<Button>().Single(b=>b.Tag is ReviewProjectSelectionItem p&&p.ProjectName==name);
        internal void Add(string name)=>Click(Project(name));
        internal Button Remove(string name,int occurrence=0)=>Named<WrapPanel>("SelectedSequenceChipsContainer").GetVisualDescendants().OfType<Button>()
            .Where(b=>b.Tag is ReviewProjectSelectionItem item&&item.ProjectName==name).ElementAt(occurrence);
        internal Button Other()=>Named<StackPanel>("ActivitySummariesContainer").GetVisualDescendants().OfType<Button>().Single();
        internal void Seed(){Add("Programming");Add("Break / Empty");Add("Illustration");Add("Programming");}
        public void Dispose()=>Window.Close();
    }
    private static ActivitySummaryGroup Activity(string name,string category,int seconds,double percentage)=>new(){App=name,Title=name,Category=category,TotalDuration=TimeSpan.FromSeconds(seconds),Percentage=percentage};
    internal static void Layout()
    {
        using var f=new Fixture(extraProjects:24);f.Seed();
        Require(f.Window.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(),"Review first step is missing the approved shared native frame.");
        Require(f.Window.SystemDecorations==SystemDecorations.None,"Review has system window chrome.");
        f.Window.Height=520;ClockOverlayChecks.Pump(25);
        var projects=f.Named<ScrollViewer>("ReviewProjectsScroll");var activities=f.Named<ScrollViewer>("ReviewActivityScroll");
        var sequence=f.Named<ScrollViewer>("ReviewSequenceScroll");double footerY=f.Next.TranslatePoint(default,f.Window)!.Value.Y;double sequenceY=sequence.TranslatePoint(default,f.Window)!.Value.Y;
        projects.Offset=new(0,10000);activities.Offset=new(0,10000);ClockOverlayChecks.Pump(25);
        Require(projects.Extent.Height>projects.Viewport.Height&&activities.Extent.Height>activities.Viewport.Height,"Short review panes are not independently scrollable.");
        Require(f.Next.TranslatePoint(default,f.Window)!.Value.Y==footerY&&sequence.TranslatePoint(default,f.Window)!.Value.Y==sequenceY&&footerY+f.Next.Bounds.Height<f.Window.Bounds.Height-15,"Scrolling moves or clips sequence/actions.");
        for(int i=0;i<10;i++)f.Add("Programming");
        Require(sequence.Extent.Width>sequence.Viewport.Width&&sequence.Bounds.Height<=57,$"Long sequence geometry: extent={sequence.Extent}, viewport={sequence.Viewport}, bounds={sequence.Bounds}.");
        sequence.Offset=new(10000,0);ClockOverlayChecks.Pump(20);Require(sequence.Offset.X>0,"Long sequence cannot reach its last segment.");
    }
    internal static void SelectionAndRegistration()
    {
        using var f=new Fixture();Require(!f.Next.IsEnabled,"Empty selection enables Continue.");f.Seed();
        Require(f.Selections.Select(p=>p.ProjectName).SequenceEqual(new[]{"Programming","Break / Empty","Illustration","Programming"}),"Repeated project selection changed order.");
        var first=f.Selections[0];Click(f.Remove("Programming",1));Require(f.Selections.Count==3&&ReferenceEquals(f.Selections[0],first),"Removing the second repeated segment removed the first.");
        var input=f.Named<TextBox>("NewProjectTextBox");input.Text="  برنامه نویسی  ";Key(input,Avalonia.Input.Key.Enter);
        Require(f.Registrations==1&&f.Selections.Last().ProjectName=="برنامه نویسی"&&f.Step==1&&input.Text=="","New project Enter did not register/select once in step 1.");
        var rtl=f.Project("برنامه نویسی").GetVisualDescendants().OfType<TextBlock>().First(t=>t.Text=="برنامه نویسی");Require(rtl.FlowDirection==FlowDirection.RightToLeft,"Persian project direction lost.");
        input.Text="  ";Click(f.Named<Button>("AddNewProjectButton"));Require(f.Registrations==1,"Blank project reached registration.");
        Key(f.Project("Reading"),Avalonia.Input.Key.Enter);Require(f.Step==1&&f.Selections.Last().ProjectName=="Reading","Enter on project also advanced the wizard.");
        Key(f.Remove("Reading"),Avalonia.Input.Key.Enter);Require(f.Step==1&&!f.Selections.Any(p=>p.ProjectName=="Reading"),"Enter on remove also advanced the wizard.");
        while(f.Selections.Count>0)Click(f.Remove(f.Selections[0].ProjectName));Require(!f.Next.IsEnabled,"Removing all segments leaves Continue enabled.");
        Key(f.Window,Avalonia.Input.Key.Escape);Require(!f.Window.IsVisible&&f.Saves==0,"Escape saved a review.");
    }
    internal static void ChronologicalRemovalAndNavigation()
    {
        using(var f=new Fixture(tracked:true))
        {
            f.Add("Programming");f.Add("Reading");
            Click(f.Remove("Programming"));Require(f.Selections.Select(p=>p.ProjectName).SequenceEqual(new[]{"Illustration","Reading"}),"Chronologically reordered chip removed a different selected project.");
            Click(f.Next);Require(f.Step==2&&f.Selections.Any(p=>p.IsFixedTracked&&p.StartUtc==f.Start.AddMinutes(10)&&p.EndUtc==f.Start.AddMinutes(20)),"Continue changed recorded stopwatch time.");
            Key(f.Window,Avalonia.Input.Key.Escape);Require(f.Step==1&&f.Window.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(v=>v.IsEffectivelyVisible),"Back did not restore the native first step.");
            Click(f.Next);Click(f.Named<Button>("Step2VerifyButton"));Require(f.Saves==1&&f.Saved!.All(s=>!s.ExistingIntervalId.HasValue),"Saving after back/forward deleted or rewrote the tracked interval.");
        }
        using(var f=new Fixture(feelings:true)){f.Add("Programming");Click(f.Next);Click(f.Named<Button>("Step2ContinueButton"));Require(f.Step==3,"Optional mood step lost.");Key(f.Window,Avalonia.Input.Key.Escape);Require(f.Step==2,"Mood back navigation changed.");}
        foreach(var theme in new[]{AppThemeCatalog.Midnight,AppThemeCatalog.Daylight})
        {using var f=new Fixture(theme:theme);Require(!f.Window.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(),"Other theme received the Navigator layout.");Require(f.Named<WrapPanel>("ProjectCardsContainer").Children.Count==8,"Legacy project choices lost.");}
    }
    internal static void ActivityAndRetry()
    {
        using(var f=new Fixture())
        {
            var list=f.Named<StackPanel>("ActivitySummariesContainer");Require(list.Children.Count==4,"Threshold grouping changed.");
            Click(f.Other());Require(list.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="Terminal"),"Other activities did not expand.");
            Require(list.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="45s · 2.5%"),"Sub-minute activity durations were rounded into whole minutes.");
            Click(f.Other());Require(!list.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="Terminal"),"Other activities did not collapse.");
            for(int i=0;i<3;i++)Click(f.Named<Button>("DecreaseThresholdButton"));Require(f.Settings.PeriodicReviewMinActivityPercent==0&&list.Children.Count==6&&f.SettingsWrites==3,"Threshold Off failed to reveal individual activities or persist preference.");
            Click(f.Named<Button>("IncreaseThresholdButton"));Require(f.Settings.PeriodicReviewMinActivityPercent==1,"Threshold increase skipped the first value.");
        }
        using(var f=new Fixture(offline:true))
        {
            f.Add("Programming");var selected=f.Selections[0];Require(f.Named<Border>("ActivityWatchStatusCard").IsVisible&&f.Next.IsEnabled,"Offline state blocks manual continuation.");
            var pending=new TaskCompletionSource<PeriodicReviewModel>();f.Loader=_=>pending.Task;
            Click(f.Named<Button>("RetryActivityWatchButton"));Click(f.Named<Button>("RetryActivityWatchButton"));Require(f.Loads==2,"Repeated retry launched overlapping loads.");
            pending.SetResult(f.Model);ClockOverlayChecks.Pump(30);Require(ReferenceEquals(selected,f.Selections[0])&&f.Selections.Count==1&&f.Named<Button>("RetryActivityWatchButton").IsEnabled,"Retry changed selection or remained disabled.");
            Click(f.Next);Require(f.Step==2,"Offline review cannot continue.");
        }
        using(var f=new Fixture()){f.Model.RawActivitySummaries.Clear();Click(f.Named<Button>("DecreaseThresholdButton"));Require(f.Named<Border>("ActivityWatchStatusCard").IsVisible&&!f.Named<Button>("RetryActivityWatchButton").IsVisible,"No-data state incorrectly reports a connection failure.");}
    }
    internal static void LateCompletion()
    {
        var pending=new TaskCompletionSource<PeriodicReviewModel>();using var f=new Fixture(tracked:true,pending:pending.Task);
        f.Add("Programming");Click(f.Next);var selected=f.Selections.ToArray();pending.SetResult(f.Model);ClockOverlayChecks.Pump(35);
        Require(f.Step==2&&f.Selections.SequenceEqual(selected),"Late activity response rewrote allocation selections.");
        var closing=new TaskCompletionSource<PeriodicReviewModel>();using var g=new Fixture(pending:closing.Task);
        Click(g.Named<Button>("ReviewCloseButton"));Require(g.Cancelled&&!g.Window.IsVisible,"Closing did not cancel pending activity loading.");
        closing.SetResult(g.Model);ClockOverlayChecks.Pump(25);Require(g.Saves==0&&!g.Window.IsVisible,"Late completion reopened or saved a closed review.");
    }
    internal static void Evidence()
    {
        Directory.CreateDirectory(Output);
        using(var f=new Fixture()){f.Seed();Capture(f,"selected");Click(f.Other());f.Named<ScrollViewer>("ReviewActivityScroll").Offset=new(0,10000);ClockOverlayChecks.Pump(20);Capture(f,"expanded-activities");}
        using(var f=new Fixture(offline:true)){f.Seed();Capture(f,"activity-offline");}
        using(var f=new Fixture()){Capture(f,"empty-selection");}
        using(var f=new Fixture(extraProjects:30)){f.Seed();f.Window.Height=520;ClockOverlayChecks.Pump(25);Capture(f,"short-window");}
        File.WriteAllText(System.IO.Path.Combine(Output,"verification.json"),JsonSerializer.Serialize(new{productionWindow=true,privateDesktop=true,syntheticData=true,
            checks=new[]{"fixed sequence and actions","independent scrolling","repeated selection and targeted removal","project registration and RTL","keyboard actions","chronological tracked slots","allocation and mood navigation","threshold persistence and expansion","offline retry","late completion and cancellation","legacy themes"}},new JsonSerializerOptions{WriteIndented=true}));
    }
    private static void Capture(Fixture f,string name)
    {
        var frame=f.Window.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single();
        if(name=="selected"){var scroll=f.Named<ScrollViewer>("ReviewSequenceScroll");Console.WriteLine($"Sequence geometry: extent={scroll.Extent}, viewport={scroll.Viewport}, bounds={scroll.Bounds}.");}
        using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(frame.Bounds.Width),(int)Math.Ceiling(frame.Bounds.Height)),new Vector(96,96));bitmap.Render(frame);bitmap.Save(System.IO.Path.Combine(Output,name+".png"));
    }
}
