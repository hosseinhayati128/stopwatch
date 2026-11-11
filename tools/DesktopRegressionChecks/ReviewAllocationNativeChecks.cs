using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
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

internal static class ReviewAllocationNativeChecks
{
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Click(Button button){button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));ClockOverlayChecks.Pump(20);}
    private static void Key(Control control,Key key){control.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=key});ClockOverlayChecks.Pump(15);}
    private static object? Invoke(Fixture f,string method,params object[] args)=>typeof(PeriodicReviewWindow).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(f.Window,args);
    private static Border Card(Fixture f,ReviewProjectSelectionItem item)=>f.Named<StackPanel>("AllocatedProjectsContainer").Children.OfType<Border>().Single(b=>ReferenceEquals(b.Tag,item));
    private static T Named<T>(Control card,string name) where T:Control=>card.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    private static Button Button(Control card,string content)=>card.GetVisualDescendants().OfType<Button>().Single(b=>b.Content?.ToString()==content);
    private static ReviewProjectSelectionItem Item(Fixture f,string name,int start,int minutes,bool fixedTime=false,bool isBreak=false)=>new(){ProjectName=name,OriginalProjectName=name,StartUtc=f.Start.AddMinutes(start),EndUtc=f.Start.AddMinutes(start+minutes),AllocatedMinutes=minutes,IsFixedTracked=fixedTime,IsBreak=isBreak};
    private static void Seed(Fixture f,params ReviewProjectSelectionItem[] items)
    {
        f.Add("Programming");Click(f.Next);f.Selections.Clear();f.Selections.AddRange(items);Refresh(f);
    }
    private static void Refresh(Fixture f){Invoke(f,"RenderAllocatedCards");Invoke(f,"SyncAllViews",true);ClockOverlayChecks.Pump(20);}
    private const string Output="design/one-piece-v2/activity-review/allocation-native-implementation";
    private static void Capture(Fixture f,string name,string output=Output)
    {
        Directory.CreateDirectory(output);ClockOverlayChecks.Pump(30);
        var frame=f.Window.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single(c=>c.IsEffectivelyVisible);
        using var image=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(frame.Bounds.Width),(int)Math.Ceiling(frame.Bounds.Height)),new Vector(96,96));image.Render(frame);image.Save(System.IO.Path.Combine(output,name+".png"));
    }
    private static Color Ink(IBrush? brush)=>((ISolidColorBrush)brush!).Color;
    private static Color SessionInk(Fixture f,ReviewProjectSelectionItem item)=>Ink(Card(f,item).GetVisualDescendants().OfType<Border>()
        .Single(b=>b.Child is TextBlock text&&text.Text==(f.Selections.IndexOf(item)+1).ToString()).Background);
    private static void MatchingProjectColors(Fixture f,Dictionary<string,Color> expected)
    {
        var slots=PeriodicReviewDataAggregator.BuildFullTimeline(f.Start,f.Start.AddMinutes(30),f.Selections);
        var blocks=f.Named<Grid>("DistributionBarContainer").Children.OfType<Border>().ToArray();
        var rows=f.Named<StackPanel>("TimelinePreviewContainer").Children.OfType<Border>().ToArray();
        Require(blocks.Length==slots.Count&&rows.Length==slots.Count(s=>s.DurationMinutes>0),"Color fixture lost a timeline block or row.");
        int rowIndex=0;
        for(int i=0;i<slots.Count;i++)
        {
            var row=slots[i].DurationMinutes>0?rows[rowIndex++]:null;
            var item=slots[i].Item;if(item==null||item.IsBreak)continue;
            var color=expected[item.ProjectName.Trim()];
            Require(SessionInk(f,item)==color&&Ink(blocks[i].Background)==color,"Project color differs between bar and session marker, or changed during editing.");
            if(row==null)continue;
            var marker=row.GetVisualDescendants().OfType<Border>().Single(b=>b.Width==24&&b.Height==26);
            Require(Ink(marker.Background)==color&&Ink(row.BorderBrush)==color,"Timeline color differs from its project.");
        }
    }
    internal static void ProjectColors()
    {
        const string output="design/one-piece-v2/activity-review/project-colors-native-implementation";
        using var f=new Fixture(feelings:true);
        Seed(f,Item(f,"Arash",0,6),Item(f,"Art",6,6),Item(f,"Learning",12,6),Item(f,"Apply",18,6),Item(f,"Apply",24,6));
        var colors=f.Selections.GroupBy(x=>x.ProjectName,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>SessionInk(f,g.First()),StringComparer.OrdinalIgnoreCase);
        Require(colors.Values.Distinct().Count()==4,"Different projects still share one session color.");
        MatchingProjectColors(f,colors);f.Window.Height=720;Capture(f,"project-colors",output);
        f.Named<ScrollViewer>("ReviewSessionsScroll").Offset=new(0,10000);f.Named<ScrollViewer>("ReviewTimelineScroll").Offset=new(0,10000);Capture(f,"repeated-project",output);
        var arash=f.Selections[0];Click(Named<Button>(Card(f,arash),"AllocationExpand"));Click(Button(Card(f,arash),"Split"));
        Require(f.Selections.Count==6,"Split did not create the color-check continuation.");MatchingProjectColors(f,colors);
        f.Selections.Reverse();Refresh(f);MatchingProjectColors(f,colors);
        var apply=f.Selections.First(x=>x.ProjectName=="Apply");apply.ProjectName="apply";apply.IsFixedTracked=true;Refresh(f);MatchingProjectColors(f,colors);
        f.Selections.Add(Item(f," APPLY ",30,0));Refresh(f);MatchingProjectColors(f,colors);
        Click(f.Named<Button>("Step2BackButton"));Click(f.Next);MatchingProjectColors(f,colors);
        f.Selections.Clear();f.Selections.AddRange([Item(f,"Arash",0,5),Item(f,"Apply",5,5,true),Item(f,"Apply",10,5),Item(f,"Break / Empty",15,3,isBreak:true)]);Refresh(f);MatchingProjectColors(f,colors);
        var slots=PeriodicReviewDataAggregator.BuildFullTimeline(f.Start,f.Start.AddMinutes(30),f.Selections);
        var blocks=f.Named<Grid>("DistributionBarContainer").Children.OfType<Border>().ToArray();
        for(int i=0;i<slots.Count;i++)if(slots[i].IsBreak||slots[i].IsUnallocated)Require(!colors.Values.Contains(Ink(blocks[i].Background)),"Break/gap borrowed a project color.");
        Require(f.Named<StackPanel>("TimelinePreviewContainer").GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Any(),"Recorded time lost its lock marker.");
        f.Named<ScrollViewer>("ReviewSessionsScroll").Offset=default;f.Named<ScrollViewer>("ReviewTimelineScroll").Offset=default;Capture(f,"recorded-break-gap",output);
        Require(f.Saves==0&&f.SettingsWrites==0,"Changing project presentation wrote review data or settings.");
    }
    internal static void Layout()
    {
        using var f=new ReviewProjectsNativeChecks.Fixture();f.Seed();Click(f.Next);
        Require(f.Window.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(c=>c.IsEffectivelyVisible),"Allocation step is missing the approved shared frame.");
        var sessions=f.Named<ScrollViewer>("ReviewSessionsScroll");var timeline=f.Named<ScrollViewer>("ReviewTimelineScroll");
        f.Window.Height=520;ClockOverlayChecks.Pump(25);
        var save=f.Named<Button>("Step2VerifyButton");double y=save.TranslatePoint(default,f.Window)!.Value.Y;
        sessions.Offset=new(0,10000);timeline.Offset=new(0,10000);ClockOverlayChecks.Pump(20);
        Require(sessions.Extent.Height>sessions.Viewport.Height&&timeline.Extent.Height>timeline.Viewport.Height,"Short allocation panes must scroll independently.");
        Require(Math.Abs(save.TranslatePoint(default,f.Window)!.Value.Y-y)<1&&y+save.Bounds.Height<f.Window.Bounds.Height-15,"Allocation footer moved or clipped.");
        f.Window.Width=850;ClockOverlayChecks.Pump(20);
        foreach(var row in f.Window.GetVisualDescendants().OfType<Grid>().Where(g=>g.Name=="AllocationDurationControls"))
            for(int i=1;i<row.Children.Count;i++)Require(row.Children[i-1].Bounds.Right<=row.Children[i].Bounds.Left+1,"Duration controls overlap at minimum width.");
        Capture(f,"short-window");
    }
    internal static void Actions()
    {
        using var f=new Fixture();var a=Item(f,"Programming",0,12);var b=Item(f,"Reading",12,8);Seed(f,a,b);
        Click(Button(Card(f,a),"+1"));Require(a.AllocatedMinutes==13&&f.Step==2&&f.Saves==0,"Minute button did not edit without advancing.");
        Card(f,a).GetVisualDescendants().OfType<Slider>().Single().Value=8;ClockOverlayChecks.Pump(15);Require(a.AllocatedMinutes==8,"Slider did not update allocation.");
        var input=Named<TextBox>(Card(f,a),"AllocationMinutes");input.Text="6";Key(input,Avalonia.Input.Key.Enter);Require(a.AllocatedMinutes==6&&f.Step==2&&f.Saves==0,"Minute Enter saved or failed to apply.");
        input.Text="invalid";Key(input,Avalonia.Input.Key.Enter);Require(a.AllocatedMinutes==6&&f.Saves==0,"Invalid minutes changed allocation or saved.");input.RaiseEvent(new RoutedEventArgs(InputElement.LostFocusEvent));Require(input.Text=="6","Invalid minutes did not reset on blur.");
        Key(Button(Card(f,a),"+1"),Avalonia.Input.Key.Enter);Require(f.Step==2&&f.Saves==0,"Enter on a duration button also advanced/saved.");
        Click(Named<Button>(Card(f,a),"AllocationExpand"));
        var end=Named<TextBox>(Card(f,a),"AllocationEnd");end.Text=f.Start.AddMinutes(7).ToLocalTime().ToString("HH:mm");Key(end,Avalonia.Input.Key.Enter);Require(a.AllocatedMinutes==7&&f.Step==2,"Exact end did not apply without saving.");
        var start=Named<TextBox>(Card(f,a),"AllocationStart");start.Text=f.Start.AddMinutes(1).ToLocalTime().ToString("HH:mm");Key(start,Avalonia.Input.Key.Enter);Require(a.StartUtc==f.Start.AddMinutes(1)&&a.AllocatedMinutes==7,"Exact start lost duration.");
        Click(Button(Card(f,a),"+1 min"));Require(a.StartUtc==f.Start.AddMinutes(2),"Shift action lost its handler.");
        var down=Card(f,a).GetVisualDescendants().OfType<Button>().Single(x=>AutomationProperties.GetName(x)=="Swap order with next project");Click(down);Require(ReferenceEquals(f.Selections[1],a),"Order action lost its handler.");
        // Use a fresh unclipped interval to exercise split, add-part and remove independently of order packing.
        a.StartUtc=f.Start;a.EndUtc=f.Start.AddMinutes(8);a.AllocatedMinutes=8;f.Selections.Clear();f.Selections.Add(a);Refresh(f);
        Click(Button(Card(f,a),"Split"));Require(f.Selections.Count==2&&f.Selections.Sum(x=>x.AllocatedMinutes)==8,"Split did not preserve eight minutes.");
        Click(Button(Card(f,a),"Add part"));Require(f.Selections.Count==3&&f.Selections.All(x=>x.ProjectName=="Programming"),"Add part changed project identity.");
        Click(Button(Card(f,a),"Remove"));Require(f.Selections.Count==2&&!f.Selections.Contains(a),"Remove targeted the wrong repeated session.");
        ContextMenu? menu=null;
        using var opened=ContextMenu.OpenedEvent.AddClassHandler<ContextMenu>((sender,_)=>menu=sender);
        var add=f.Named<Button>("Step2AddSessionButton");Click(add);
        Require(menu?.IsOpen==true,"Add session did not open project choices.");
        var reading=menu!.Items.OfType<MenuItem>().Single(x=>x.Header?.ToString()?.Contains("Reading")==true);reading.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));menu.Close();ClockOverlayChecks.Pump(20);
        Require(f.Selections.Any(x=>x.ProjectName=="Reading"),"Add session menu did not add the chosen project.");
    }
    internal static void CollisionsAndFixedRecords()
    {
        using var f=new Fixture(tracked:true,feelings:true);var a=Item(f,"Programming",0,8);var fixedItem=Item(f,"Illustration",10,10,true);fixedItem.StopwatchIntervalId=f.Model.StopwatchSlots.Single(s=>s.ExistingIntervalId.HasValue).ExistingIntervalId;
        Seed(f,a,fixedItem);var input=Named<TextBox>(Card(f,a),"AllocationMinutes");input.Focus();Require(input.IsFocused,"Collision test input did not receive focus.");input.Text="18";Key(input,Avalonia.Input.Key.Enter);
        Require(f.Selections.Count==3&&f.Selections.Where(x=>!x.IsFixedTracked).Sum(x=>x.AllocatedMinutes)==18,"Collision split lost requested minutes.");
        Require(f.Named<StackPanel>("AllocatedProjectsContainer").Children.Count==3&&f.Selections.Any(x=>x.IsAutoContinuation&&x.StartUtc==f.Start.AddMinutes(20)),"Continuation has no editable card or wrong time.");
        Capture(f,"split-around-recorded");
        var policy=f.Named<ComboBox>("ReviewCollisionSelector");policy.SelectedIndex=1;ClockOverlayChecks.Pump(20);
        Require(f.Settings.PeriodicReviewFixedCollisionBehavior=="StopAtFixed"&&f.SettingsWrites==1&&f.Selections.Count==2&&a.AllocatedMinutes==10,"Stop policy failed to persist/repack.");
        Capture(f,"stop-at-recorded");Click(Named<Button>(Card(f,fixedItem),"AllocationExpand"));
        Require(!Named<TextBox>(Card(f,fixedItem),"AllocationStart").IsEnabled&&!Named<TextBox>(Card(f,fixedItem),"AllocationEnd").IsEnabled,"Recorded exact fields are editable.");
        Invoke(f,"ShiftItem",fixedItem,1);Invoke(f,"SetItemStartTime",fixedItem,f.Start.AddMinutes(11));Invoke(f,"SetItemEndTime",fixedItem,f.Start.AddMinutes(25));
        Require(fixedItem.StartUtc==f.Start.AddMinutes(10)&&fixedItem.EndUtc==f.Start.AddMinutes(20)&&fixedItem.AllocatedMinutes==10,"Recorded interval moved through edit/drag handler.");
        Click(Button(Card(f,fixedItem),"Remove"));Click(f.Named<Button>("Step2SkipFeelingsButton"));
        Require(f.Saves==1&&f.Saved!.Any(s=>s.ExistingIntervalId==fixedItem.StopwatchIntervalId&&s.SelectedProjectName==null),"Removing a recorded session lost its existing delete-save behavior.");
    }
    internal static void DragAndSave()
    {
        using var f=new Fixture();var a=Item(f,"Programming",0,5);var b=Item(f,"Break / Empty",15,3,isBreak:true);Seed(f,a,b);
        var bar=f.Named<Grid>("DistributionBarContainer");var source=bar.Children.OfType<Border>().First();var point=source.TranslatePoint(new Point(5,12),f.Window)!.Value;
        using var pointer=new Avalonia.Input.Pointer(960,PointerType.Mouse,true);var down=new PointerPointProperties(RawInputModifiers.LeftMouseButton,PointerUpdateKind.LeftButtonPressed);
        source.RaiseEvent(new PointerPressedEventArgs(source,pointer,f.Window,point,0,down,KeyModifiers.None,1));
        bar.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent,bar,pointer,f.Window,point+new Vector(bar.Bounds.Width/30*2,0),0,down,KeyModifiers.None));
        bar.RaiseEvent(new PointerReleasedEventArgs(bar,pointer,f.Window,point,0,new PointerPointProperties(RawInputModifiers.None,PointerUpdateKind.LeftButtonReleased),KeyModifiers.None,MouseButton.Left));ClockOverlayChecks.Pump(20);
        Require(a.StartUtc==f.Start.AddMinutes(2)&&a.EndUtc==f.Start.AddMinutes(7),"Dragging manual block lost duration or did not move.");
        Click(f.Named<Button>("Step2VerifyButton"));Require(f.Saves==1&&f.Saved!.Count==1&&f.Saved[0].SelectedProjectName=="Programming"&&f.Saved[0].StartUtc==a.StartUtc&&f.Saved[0].EndUtc==a.EndUtc,"Saving included breaks/gaps or lost dragged timing.");
    }
    internal static void NavigationAndEvidence()
    {
        using(var f=new Fixture(feelings:true))
        {
            var a=Item(f,"Programming",0,12);var b=Item(f,"Illustration",12,10,true);var rest=Item(f,"Break / Empty",22,3,isBreak:true);Seed(f,a,b,rest);Capture(f,"allocation-overview");
            Click(Named<Button>(Card(f,a),"AllocationExpand"));Capture(f,"exact-time-expanded");
            Click(f.Named<Button>("Step2ContinueButton"));Require(f.Step==3&&f.Window.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(c=>c.IsEffectivelyVisible),"Feelings step did not retain the shared frame.");
            Key(f.Window,Avalonia.Input.Key.Escape);Require(f.Step==2&&f.Window.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(c=>c.IsEffectivelyVisible),"Mood back failed to restore allocation frame.");
            Key(f.Window,Avalonia.Input.Key.Escape);Require(f.Step==1&&f.Saves==0,"Allocation back saved or failed.");Click(f.Next);Require(f.Step==2,"Forward navigation failed after returning to projects.");
        }
        using(var f=new Fixture())
        {
            var a=Item(f,"برنامه نویسی — یک نام طولانی برای بررسی خوانایی و چیدمان",0,0);Seed(f,a);Click(Named<Button>(Card(f,a),"AllocationExpand"));
            Require(Card(f,a).GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text==a.ProjectName&&t.FlowDirection==FlowDirection.RightToLeft),"RTL project name lost direction.");
            Require(!Button(Card(f,a),"Split").IsEnabled,"Zero-minute session can split.");Capture(f,"rtl-zero-minute");
            Key(f.Window,Avalonia.Input.Key.Enter);Require(f.Saves==1&&f.Saved!.Count==0,"Empty allocation cannot save without creating records.");
        }
        foreach(var theme in new[]{AppThemeCatalog.Midnight,AppThemeCatalog.Daylight})
        {
            using var f=new Fixture(theme:theme);Invoke(f,"AddProjectInstance","Programming",false);Click(f.Next);
            Require(f.Step==2&&!f.Window.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(),"Other themes received Navigator allocation layout.");
        }
        File.WriteAllText(System.IO.Path.Combine(Output,"verification.json"),JsonSerializer.Serialize(new{productionControls=true,privateDesktop=true,installedAppChanged=false,syntheticData=true,
            checks=new[]{"layout and independent scrolling","duration and exact time controls","order split add remove","collision policies","fixed-record guards and removal","manual drag","keyboard navigation","save callbacks excluding gaps and breaks","RTL and zero minutes","other themes"}},new JsonSerializerOptions{WriteIndented=true}));
    }
}
