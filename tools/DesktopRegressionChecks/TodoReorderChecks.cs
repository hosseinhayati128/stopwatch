using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;
using Pointer = Avalonia.Input.Pointer;

internal static class TodoReorderChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly PointerPointProperties Down=new(RawInputModifiers.LeftMouseButton,PointerUpdateKind.LeftButtonPressed);
    static readonly PointerPointProperties Up=new(RawInputModifiers.None,PointerUpdateKind.LeftButtonReleased);
    static void Require(bool value,string message){if(!value)throw new Exception(message);}

    internal static void Run()
    {
        string vault=Path.GetFullPath(Path.Combine(".superpowers/sdd/2026-10-30-todo-card-reordering/fixtures",Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(vault);
        string settingsPath=SettingsStore.SettingsPath;
        byte[]? settingsBefore=File.Exists(settingsPath)?File.ReadAllBytes(settingsPath):null;
        TodoOverlayWindow? window=null;
        try
        {
            for(int i=0;i<36;i++)
            {
                string title=$"Task {i:00}: arrange the voyage"+(i%5==0?" and check the provisions before sailing":"");
                if(i==0)title=TodoDetails.Append(TodoDetails.Append(title,TodoDetailKind.Subtask,"Inspect the map"),TodoDetailKind.Note,"Keep supplies aboard.");
                Require(ObsidianNotesSync.AppendEntry(vault,NoteType.Todo,title,timestamp:new DateTime(2026,10,30,9,0,0).AddMinutes(i)).Success,"Could not create isolated drag fixture.");
            }
            var entries=ObsidianNotesSync.LoadAllNotes(vault).Where(e=>e.Type==NoteType.Todo).OrderBy(e=>e.Timestamp).ToArray();
            Require(TodoOrganizationStore.SetManualOrder(vault,entries).Success,"Cannot seed task order.");
            Require(TodoOrganizationStore.SetSortMode(vault,"Manual").Success,"Cannot select Manual sort.");
            Require(ObsidianNotesSync.SetTodoCompleted(vault,entries[^1],true).Success,"Cannot seed completed task.");
            entries=ObsidianNotesSync.LoadAllNotes(vault).Where(e=>e.Type==NoteType.Todo).OrderBy(e=>e.Timestamp).ToArray();
            var markdown=Directory.GetFiles(vault,"*.md",SearchOption.AllDirectories).ToDictionary(file=>file,File.ReadAllText);
            var settings=new AppSettings {ThemeMode=AppThemeCatalog.Nika,ObsidianVaultFolder=vault,TelegramEnabled=false,HabiticaEnabled=false};
            window=new TodoOverlayWindow(settings){ShowActivated=false,ShowInTaskbar=false,Topmost=false};
            window.Show();ClockOverlayChecks.Pump(100);window.ToggleExpanded();ClockOverlayChecks.Pump(120);
            var scroll=window.FindControl<ScrollViewer>("TodoScrollViewer")!;
            scroll.Offset=new Vector(0,400);ClockOverlayChecks.Pump(60);
            var row=VisibleRow(window);
            var handle=row.GetVisualDescendants().OfType<Button>().Single(b=>b.Classes.Contains("todoDragHandle"));
            var pointer=new Pointer(930,PointerType.Mouse,true);
            Press(handle,pointer,window);
            Point bottom=scroll.TranslatePoint(new Point(scroll.Bounds.Width/2,scroll.Bounds.Height-2),window)!.Value;
            Move(handle,pointer,window,bottom);
            double before=scroll.Offset.Y;ClockOverlayChecks.Pump(250);double first=scroll.Offset.Y;ClockOverlayChecks.Pump(250);
            Require(first>before+25&&scroll.Offset.Y>first+25,"A stationary held card at the bottom edge did not keep scrolling.");
            Point top=scroll.TranslatePoint(new Point(scroll.Bounds.Width/2,2),window)!.Value;
            Move(handle,pointer,window,top);before=scroll.Offset.Y;ClockOverlayChecks.Pump(350);
            Require(scroll.Offset.Y<before-40,"A stationary held card at the top edge did not scroll upward.");
            Release(handle,pointer,window,new Point(-30,-30));ClockOverlayChecks.Pump(60);
            before=scroll.Offset.Y;ClockOverlayChecks.Pump(100);Require(scroll.Offset.Y==before,"Scrolling continued after release.");
            Require(TodoOrganizationStore.Load(vault).Arrange(entries).SequenceEqual(entries),"Outside drop changed saved order.");
            Console.WriteLine("PASS stationary bottom/top scrolling and outside release");
            CheckCardInput(window,settings,entries);
            CheckExpandedAndChangedTasks(window,settings,entries[0]);
            CheckLongDistanceAndOtherThemes(window,settings);
            foreach(var pair in markdown)Require(File.ReadAllText(pair.Key)==pair.Value,"Dragging changed task text or completion.");
            Require((settingsBefore==null&&!File.Exists(settingsPath))||(settingsBefore!=null&&File.ReadAllBytes(settingsPath).SequenceEqual(settingsBefore)),"Drag checks wrote real settings.");
        }
        finally{window?.Close();Directory.Delete(vault,true);}
    }

    static void CheckCardInput(TodoOverlayWindow window,AppSettings settings,NoteEntry[] entries)
    {
        string vault=settings.ObsidianVaultFolder;
        string originalSubfolder=settings.NotesSubfolder;
        var scroll=window.FindControl<ScrollViewer>("TodoScrollViewer")!;
        var rows=window.FindControl<StackPanel>("TodoRows")!;
        var pointer=new Pointer(931,PointerType.Mouse,true);
        var source=VisibleRow(window);
        var title=Title(source);
        var check=source.Children.OfType<HoldToCompleteCheckBox>().Single();
        Press(title,pointer,window);ClockOverlayChecks.Pump(1100);
        Require(!check.IsHolding&&check.IsChecked!=true,"Holding a card title started task completion.");
        var target=rows.Children.OfType<Grid>().SkipWhile(r=>r!=source).Skip(2).First();
        Point drop=target.TranslatePoint(new Point(target.Bounds.Width/2,target.Bounds.Height+2),window)!.Value;
        Move(title,pointer,window,drop);ClockOverlayChecks.Pump(25);
        Require(window.FindControl<Border>("TodoDropLine")!.IsVisible&&window.FindControl<Border>("TodoDragPreview")!.IsVisible,"Drag has no insertion line or floating card preview.");
        Capture(window);
        var sourceEntry=(NoteEntry)source.Tag!;var targetEntry=(NoteEntry)target.Tag!;
        Release(title,pointer,window,drop);ClockOverlayChecks.Pump(80);
        var ordered=TodoOrganizationStore.Load(vault).Arrange(ObsidianNotesSync.LoadAllNotes(vault));
        Require(Array.FindIndex(ordered,e=>e.Timestamp==sourceEntry.Timestamp)==Array.FindIndex(ordered,e=>e.Timestamp==targetEntry.Timestamp)+1,"Dropping in a row gap did not save the expected position.");
        Require(ordered[^1].IsCompleted,"Reordering crossed completion groups.");
        Console.WriteLine("PASS card title drag, gap insertion and persisted order");

        // A short press must not move a card; Escape cancels a drag without collapsing.
        source=VisibleRow(window);title=Title(source);
        string orderFile=Directory.GetFiles(vault,TodoOrganizationStore.FileName,SearchOption.AllDirectories).Single();
        string before=File.ReadAllText(orderFile);
        Press(title,pointer,window);
        Release(title,pointer,window,title.TranslatePoint(new Point(3,3),window)!.Value);
        Require(File.ReadAllText(orderFile)==before,"A short title press reordered tasks.");
        Press(title,pointer,window);
        var bottom=scroll.TranslatePoint(new Point(120,scroll.Bounds.Height-2),window)!.Value;
        Move(title,pointer,window,bottom);ClockOverlayChecks.Pump(80);
        window.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Avalonia.Input.Key.Escape});
        Require(window.IsExpanded&&pointer.Captured==null&&!window.FindControl<Border>("TodoDropLine")!.IsVisible,"Escape failed to cancel the drag without collapsing.");
        double offset=scroll.Offset.Y;ClockOverlayChecks.Pump(100);
        Require(scroll.Offset.Y==offset&&File.ReadAllText(orderFile)==before,"Escape left scrolling or changed order.");

        source=VisibleRow(window);title=Title(source);Press(title,pointer,window);Move(title,pointer,window,bottom);pointer.Capture(null);
        offset=scroll.Offset.Y;ClockOverlayChecks.Pump(80);
        Require(source.Opacity==1&&scroll.Offset.Y==offset&&File.ReadAllText(orderFile)==before,"Lost capture did not cancel cleanly.");
        Console.WriteLine("PASS short presses, Escape and capture-loss cleanup");

        // The neutral middle and positions outside the list must not auto-scroll.
        source=VisibleRow(window);title=Title(source);Press(title,pointer,window);
        var middle=scroll.TranslatePoint(new Point(150,scroll.Bounds.Height/2),window)!.Value;
        Move(title,pointer,window,middle);offset=scroll.Offset.Y;ClockOverlayChecks.Pump(120);
        Require(scroll.Offset.Y==offset,"Dragging in the list middle auto-scrolled.");
        Move(title,pointer,window,new Point(window.Width+50,bottom.Y));ClockOverlayChecks.Pump(120);
        Require(scroll.Offset.Y==offset,"Dragging outside the window kept scrolling.");
        Release(title,pointer,window,new Point(-50,-50));

        // The enlarged handle retains keyboard reordering.
        source=VisibleRow(window);var sourceKey=TodoOrganizationStore.GetKey((NoteEntry)source.Tag!);
        var handle=source.GetVisualDescendants().OfType<Button>().Single(b=>b.Classes.Contains("todoDragHandle"));
        Require(handle.Bounds.Width>=24,"The drag handle is still too narrow to grab comfortably.");
        ordered=TodoOrganizationStore.Load(vault).Arrange(ObsidianNotesSync.LoadAllNotes(vault));
        int index=Array.FindIndex(ordered,e=>TodoOrganizationStore.GetKey(e)==sourceKey);
        handle.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Avalonia.Input.Key.Down});ClockOverlayChecks.Pump(50);
        ordered=TodoOrganizationStore.Load(vault).Arrange(ObsidianNotesSync.LoadAllNotes(vault));
        Require(Array.FindIndex(ordered,e=>TodoOrganizationStore.GetKey(e)==sourceKey)==index+1,"Keyboard handle reordering regressed.");

        // Changing sort or settings must release the pointer before rows are rebuilt.
        source=VisibleRow(window);title=Title(source);Press(title,pointer,window);Move(title,pointer,window,bottom);
        window.FindControl<ComboBox>("TodoSortCombo")!.SelectedItem="Priority";ClockOverlayChecks.Pump(50);
        Require(pointer.Captured==null&&!rows.GetVisualDescendants().OfType<Button>().Any(b=>b.Classes.Contains("todoDragHandle")),"Changing sort did not end the drag.");
        source=VisibleRow(window);title=source.Children.OfType<HoldToCompleteCheckBox>().Single().Content is Grid grid?grid.Children.OfType<TextBlock>().Single():(TextBlock)source.Children.OfType<HoldToCompleteCheckBox>().Single().Content!;
        Press(title,pointer,window);
        Require(typeof(TodoOverlayWindow).GetField("_draggedEntry",Private)!.GetValue(window)==null,"Priority mode started card reordering.");
        source.Children.OfType<HoldToCompleteCheckBox>().Single().CancelHold();
        window.FindControl<ComboBox>("TodoSortCombo")!.SelectedItem="Manual";ClockOverlayChecks.Pump(50);
        source=VisibleRow(window);title=Title(source);Press(title,pointer,window);Move(title,pointer,window,bottom);
        before=File.ReadAllText(orderFile);settings.NotesSubfolder="Different notes";window.ApplySettings();ClockOverlayChecks.Pump(50);
        Require(pointer.Captured==null&&File.ReadAllText(orderFile)==before,"A settings change committed the old drag.");
        settings.NotesSubfolder=originalSubfolder;window.ApplySettings();ClockOverlayChecks.Pump(50);
        Console.WriteLine("PASS neutral/outside positions, keyboard reordering, mode and settings changes");
    }

    static TextBlock Title(Grid row)
    {
        var check=row.Children.OfType<HoldToCompleteCheckBox>().Single();
        return check.Content is Grid content?content.Children.OfType<TextBlock>().Single():(TextBlock)check.Content!;
    }

    static void CheckLongDistanceAndOtherThemes(TodoOverlayWindow window,AppSettings settings)
    {
        var scroll=window.FindControl<ScrollViewer>("TodoScrollViewer")!;
        var pointer=new Pointer(932,PointerType.Mouse,true);
        foreach(string theme in new[]{AppThemeCatalog.Nika,AppThemeCatalog.Pirate,AppThemeCatalog.Daylight})
        {
            settings.ThemeMode=theme;settings.TodoAppearance.Profile="pixel";window.ApplySettings();ClockOverlayChecks.Pump(60);
            scroll.Offset=new Vector(0,scroll.Extent.Height);ClockOverlayChecks.Pump(70);
            var source=VisibleRow(window);var title=Title(source);var entry=(NoteEntry)source.Tag!;
            var before=TodoOrganizationStore.Load(settings.ObsidianVaultFolder).Arrange(ObsidianNotesSync.LoadAllNotes(settings.ObsidianVaultFolder));
            int start=Array.FindIndex(before,item=>item.Timestamp==entry.Timestamp);
            Press(title,pointer,window);
            var top=scroll.TranslatePoint(new Point(140,2),window)!.Value;
            Move(title,pointer,window,top);ClockOverlayChecks.Pump(650);
            Release(title,pointer,window,top);ClockOverlayChecks.Pump(70);
            var after=TodoOrganizationStore.Load(settings.ObsidianVaultFolder).Arrange(ObsidianNotesSync.LoadAllNotes(settings.ObsidianVaultFolder));
            Require(Array.FindIndex(after,item=>item.Timestamp==entry.Timestamp)<start-3,"Edge-scroll drop did not move several cards upward in "+theme);
            Require(after.Length==before.Length&&after[^1].IsCompleted,"Long-distance drop lost a task or crossed completed groups.");
        }
        settings.ThemeMode=AppThemeCatalog.Nika;settings.TodoAppearance.Profile="classic";window.ApplySettings();
        scroll.Offset=default;ClockOverlayChecks.Pump(60);
        var first=window.FindControl<StackPanel>("TodoRows")!.Children.OfType<Grid>().First();
        var handle=first.GetVisualDescendants().OfType<Button>().Single(b=>b.Classes.Contains("todoDragHandle"));
        Press(handle,pointer,window);Move(handle,pointer,window,scroll.TranslatePoint(new Point(140,1),window)!.Value);ClockOverlayChecks.Pump(100);
        Require(scroll.Offset.Y==0,"Top-edge scrolling exceeded the start of the list.");
        window.Hide();ClockOverlayChecks.Pump(50);
        Require(pointer.Captured==null,"Hiding the overlay left a drag captured.");
        Console.WriteLine("PASS saved long-distance drops in Classic/Pixel and existing themes, top limit and hide cleanup");
    }

    static void CheckExpandedAndChangedTasks(TodoOverlayWindow window,AppSettings settings,NoteEntry expandedTask)
    {
        var expanded=(HashSet<string>)typeof(TodoOverlayWindow).GetField("_expandedDetails",Private)!.GetValue(window)!;
        expanded.Add(TodoOrganizationStore.GetKey(expandedTask));window.ApplySettings();
        var scroll=window.FindControl<ScrollViewer>("TodoScrollViewer")!;scroll.Offset=default;ClockOverlayChecks.Pump(80);
        var rows=window.FindControl<StackPanel>("TodoRows")!;
        var tall=rows.Children.OfType<Grid>().Single(row=>((NoteEntry)row.Tag!).Timestamp==expandedTask.Timestamp);
        Require(tall.GetVisualDescendants().OfType<TodoFrame>().Any(frame=>frame.Name=="TodoDetailsFrame"),"Expanded-card fixture did not expand.");
        var source=rows.Children.OfType<Grid>().First(row=>row!=tall);var title=Title(source);var entry=(NoteEntry)source.Tag!;
        var pointer=new Pointer(933,PointerType.Mouse,true);
        Press(title,pointer,window);
        var drop=tall.TranslatePoint(new Point(tall.Bounds.Width/2,10),window)!.Value;
        Move(title,pointer,window,drop);Release(title,pointer,window,drop);ClockOverlayChecks.Pump(60);
        var ordered=TodoOrganizationStore.Load(settings.ObsidianVaultFolder).Arrange(ObsidianNotesSync.LoadAllNotes(settings.ObsidianVaultFolder));
        Require(ordered[0].Timestamp==entry.Timestamp,"Dropping above an expanded card chose the wrong insertion position.");

        source=VisibleRow(window);entry=(NoteEntry)source.Tag!;title=Title(source);
        var target=rows.Children.OfType<Grid>().SkipWhile(row=>row!=source).Skip(1).First();
        drop=target.TranslatePoint(new Point(target.Bounds.Width/2,target.Bounds.Height-2),window)!.Value;
        Press(title,pointer,window);Move(title,pointer,window,drop);
        string orderFile=Directory.GetFiles(settings.ObsidianVaultFolder,TodoOrganizationStore.FileName,SearchOption.AllDirectories).Single();
        string before=File.ReadAllText(orderFile);
        Require(ObsidianNotesSync.SetTodoCompleted(settings.ObsidianVaultFolder,entry,true).Success,"Could not simulate an external completion change.");
        Release(title,pointer,window,drop);ClockOverlayChecks.Pump(60);
        Require(File.ReadAllText(orderFile)==before,"Drop saved stale ordering after the source task changed completion group.");
        Require(ObsidianNotesSync.SetTodoCompleted(settings.ObsidianVaultFolder,entry with{IsCompleted=true},false).Success,"Could not restore the external-change fixture.");
        expanded.Clear();window.ApplySettings();ClockOverlayChecks.Pump(50);
        Console.WriteLine("PASS expanded cards and stale completion changes during dragging");
    }

    static void Capture(Window window)
    {
        window.UpdateLayout();foreach(var visual in window.GetVisualDescendants())visual.InvalidateVisual();
        window.InvalidateVisual();ClockOverlayChecks.Pump(30);
        var root=(Avalonia.Rendering.IRenderRoot)window;
        typeof(Avalonia.Rendering.IRenderer).GetMethod("Paint",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(root.Renderer,new object[]{new Rect(root.ClientSize)});
        using var image=new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width,(int)window.ClientSize.Height),new Vector(96,96));
        image.Render(window);image.Save(Path.GetFullPath(".superpowers/sdd/2026-10-30-todo-card-reordering/card-drag.png"));
    }

    static Grid VisibleRow(TodoOverlayWindow window)
    {
        var scroll=window.FindControl<ScrollViewer>("TodoScrollViewer")!;
        return window.FindControl<StackPanel>("TodoRows")!.Children.OfType<Grid>().First(row=>
        {var point=row.TranslatePoint(default,scroll)!.Value;return point.Y>30&&point.Y+row.Bounds.Height<scroll.Bounds.Height-30;});
    }
    static void Press(Control source,Pointer pointer,Window window,int clicks=1)
    {
        var point=source.TranslatePoint(new Point(source.Bounds.Width/2,source.Bounds.Height/2),window)!.Value;
        source.RaiseEvent(new PointerPressedEventArgs(source,pointer,window,point,0,Down,KeyModifiers.None,clicks));
    }
    static void Move(Control source,Pointer pointer,Window window,Point point)
        => (pointer.Captured as Control??source).RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent,pointer.Captured??source,pointer,window,point,0,Down,KeyModifiers.None));
    static void Release(Control source,Pointer pointer,Window window,Point point)
        => (pointer.Captured as Control??source).RaiseEvent(new PointerReleasedEventArgs(pointer.Captured??source,pointer,window,point,0,Up,KeyModifiers.None,MouseButton.Left));
}
