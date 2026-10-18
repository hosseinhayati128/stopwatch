using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class TodoProgressChecks
{
    public static void Core(string directory)
    {
        string vault=Path.Combine(directory,"progress-core");
        var time=new DateTime(2026,10,18,10,0,0);
        ObsidianNotesSync.AppendEntry(vault,NoteType.Todo,"Work **item**",timestamp:time);
        var entry=ObsidianNotesSync.LoadAllNotes(vault).Single();
        string path=Path.Combine(vault,"Notes","Todos.md"), original=File.ReadAllText(path);
        Check(!TodoOrganizationStore.Load(vault).IsInProgress(entry),"Existing todo is marked in progress by default.");
        Check(TodoOrganizationStore.SetInProgress(vault,entry,true).Success,"Could not mark a task in progress.");
        Check(TodoOrganizationStore.Load(vault).IsInProgress(entry) && File.ReadAllText(path)==original,"Progress did not persist independently of Markdown completion.");
        Check(TodoOrganizationStore.SaveTodo(vault,entry,"Renamed **item**",2,time.AddDays(1)).Success,"Progress task edit failed.");
        var renamed=ObsidianNotesSync.LoadAllNotes(vault).Single();
        Check(TodoOrganizationStore.Load(vault).IsInProgress(renamed),"Editing lost the in-progress flag.");
        Check(!TodoOrganizationStore.SetInProgress(vault,entry,false).Success,"Stale task toggled progress after an edit.");
        Check(TodoOrganizationStore.SetInProgress(vault,renamed,false).Success && !TodoOrganizationStore.Load(vault).IsInProgress(renamed),"Progress did not toggle off.");
        TodoOrganizationStore.SetInProgress(vault,renamed,true);
        ObsidianNotesSync.SetTodoCompleted(vault,renamed,true);
        var done=ObsidianNotesSync.LoadAllNotes(vault).Single();
        Check(!TodoOrganizationStore.Load(vault).IsInProgress(done) && !TodoOrganizationStore.SetInProgress(vault,done,true).Success,"Completed todos can show an in-progress badge.");
        Check(!TodoOrganizationStore.SetInProgress(vault,entry with {Type=NoteType.Note},true).Success,"Regular notes accepted todo progress.");
        string metadata=Path.Combine(vault,"Notes",TodoOrganizationStore.FileName), saved=File.ReadAllText(metadata);
        File.WriteAllText(metadata,"{broken");
        ObsidianNotesSync.SetTodoCompleted(vault,done,false);
        Check(!TodoOrganizationStore.SetInProgress(vault,renamed,true).Success && File.ReadAllText(metadata)=="{broken","Progress overwrote corrupt metadata.");
        File.WriteAllText(metadata,saved);
    }

    public static void Ui(string directory)
    {
        string vault=Path.Combine(directory,"progress-ui");
        ObsidianNotesSync.AppendEntry(vault,NoteType.Todo,"Double click **task**",timestamp:new DateTime(2026,10,18,11,0,0));
        var settings=new AppSettings {ObsidianVaultFolder=vault,TodoOverlayShowCompleted=true,ThemeMode=AppThemeCatalog.Pirate};
        var window=new TodoOverlayWindow(settings);window.Show();window.ToggleExpanded();Pump(150);
        try
        {
            Check(Badges()==0,"Normal todos have a status label.");
            double normalHeight=Rows().Children.OfType<Grid>().Single().Bounds.Height;
            var row=Row();Press(row,1);Release(row);Pump(20);
            Check(Badges()==0 && row.IsChecked==false,"Single click changed task status.");
            Press(row,2);Release(row);Pump(30);
            Check(Badges()==1 && Row().IsChecked==false,"Double click did not mark in progress independently of completion.");
            window.UpdateLayout();
            var icon=Rows().GetVisualDescendants().OfType<Border>().Single(b=>b.Name=="InProgressBadge");
            Check(Math.Abs(Rows().Children.OfType<Grid>().Single().Bounds.Height-normalHeight)<.5,"Progress icon adds a vertical row.");
            Check(!icon.GetVisualDescendants().OfType<TextBlock>().Any(),"Progress icon still contains the full status text.");
            Check(Grid.GetColumn(icon)==3 && Grid.GetRow(icon)==0,"Progress icon is not at the right of the task text.");
            Check(!Rows().GetVisualDescendants().OfType<Button>().Any(b=>b.Content?.ToString()?.Contains("progress",StringComparison.OrdinalIgnoreCase)==true),"Progress added an unwanted button.");
            var viewer=new NotesViewerWindow(settings);viewer.Show();Pump(100);
            Check(viewer.GetVisualDescendants().OfType<Border>().Count(b=>b.Name=="InProgressBadge")==1,"Notes viewer did not show the progress badge.");viewer.Close();window.Activate();Pump(250);
            row=Row();Press(row,1);Release(row);Press(row,2);Release(row);Pump(30);
            Check(Badges()==0 && Row().IsChecked==false,"Double click did not remove the badge.");
            for (int repeat=0;repeat<100;repeat++)
            {
                row=Row();Press(row,1);Release(row);Press(row,2);Release(row);Pump(1);
                Check(Badges()==(repeat%2==0?1:0) && Row().IsChecked==false,"Repeated double-click lost status or changed completion at iteration "+repeat);
            }
            Row().IsChecked=true;Pump(30);
            row=Row();Press(row,1);Release(row);Press(row,2);Release(row);Pump(30);
            Check(row.IsChecked==true && Badges()==0,"Double click changed a completed todo.");
            // Hold cancellation and a handled link press must never trigger progress.
            row.IsChecked=false;Pump(30);row=Row();Press(row,2);row.CancelHold();Release(row);Pump(30);
            Check(Badges()==0,"A cancelled second click toggled progress.");
        }
        finally {window.Close();}
        StackPanel Rows()=>window.FindControl<StackPanel>("TodoRows")!;
        HoldToCompleteCheckBox Row()=>Rows().GetVisualDescendants().OfType<HoldToCompleteCheckBox>().Single();
        int Badges()=>Rows().GetVisualDescendants().OfType<Border>().Count(b=>b.Name=="InProgressBadge");
    }
    static readonly Avalonia.Input.Pointer Pointer=new(767,PointerType.Mouse,true);
    static void Press(HoldToCompleteCheckBox row,int count)
        => typeof(HoldToCompleteCheckBox).GetMethod("OnPointerPressed",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(row,new object[]{new PointerPressedEventArgs(row,Pointer,row,new Point(10,10),0,new PointerPointProperties(RawInputModifiers.LeftMouseButton,PointerUpdateKind.LeftButtonPressed),KeyModifiers.None,count)});
    static void Release(HoldToCompleteCheckBox row)
        => typeof(HoldToCompleteCheckBox).GetMethod("OnPointerReleased",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(row,new object[]{new PointerReleasedEventArgs(row,Pointer,row,new Point(10,10),0,new PointerPointProperties(RawInputModifiers.None,PointerUpdateKind.LeftButtonReleased),KeyModifiers.None,MouseButton.Left)});
    static void Pump(int ms){using var cancel=new CancellationTokenSource();DispatcherTimer.RunOnce(cancel.Cancel,TimeSpan.FromMilliseconds(ms));Dispatcher.UIThread.MainLoop(cancel.Token);Dispatcher.UIThread.RunJobs();}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
}
