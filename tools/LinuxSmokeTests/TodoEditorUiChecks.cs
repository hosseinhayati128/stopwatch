using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Views;

internal static class TodoEditorUiChecks
{
    public static void Run(string directory)
    {
        string vault=Path.Combine(directory,"todo-editor-ui");Directory.CreateDirectory(vault);
        var settings=new AppSettings {ObsidianVaultFolder=vault,ThemeMode=AppThemeCatalog.Pirate,TodoOverlayShowCompleted=true};
        var editor=new NoteEntryWindow(NoteType.Todo,settings);editor.Show();Pump();
        Check(editor.FindControl<CheckBox>("DueEnabledToggle")!.IsChecked!=true && !editor.FindControl<StackPanel>("DueFields")!.IsVisible,"Due time is not optional by default.");
        editor.FindControl<TextBox>("NoteInputBox")!.Text="Deadline **task**\n\n- first";
        editor.FindControl<CheckBox>("DueEnabledToggle")!.IsChecked=true;
        editor.FindControl<DatePicker>("TodoDueDate")!.SelectedDate=DateTimeOffset.Now.Date;
        var due=DateTime.Now.AddHours(1);due=new DateTime(due.Year,due.Month,due.Day,due.Hour,due.Minute,0);
        editor.FindControl<DatePicker>("TodoDueDate")!.SelectedDate=new DateTimeOffset(due);
        editor.FindControl<TimePicker>("TodoDueTime")!.SelectedTime=due.TimeOfDay;
        editor.FindControl<ComboBox>("TodoPriorityCombo")!.SelectedIndex=1;
        Save(editor);Check(!editor.IsVisible,"Ctrl+Enter did not close the new todo editor.");
        var entry=ObsidianNotesSync.LoadAllNotes(vault).Single();
        Check(TodoOrganizationStore.Load(vault).GetDueAt(entry)==due,"Editor did not persist the selected deadline.");
        var overlay=new TodoOverlayWindow(settings);overlay.Show();overlay.ToggleExpanded();Pump();
        var rows=overlay.FindControl<StackPanel>("TodoRows")!;
        Check(rows.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Name=="TodoDueLabel" && t.Text!.StartsWith("Due ·")),"Overlay has no due-time label.");
        var soon=rows.GetVisualDescendants().OfType<Border>().Single(b=>b.Name=="DeadlineSurface").Background;
        overlay.FindControl<Button>("EditTodosButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
        rows.GetVisualDescendants().OfType<Button>().Single(b=>b.Content?.ToString()=="Edit task").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
        var editing=(NoteEntryWindow?)typeof(TodoOverlayWindow).GetField("_taskEditor",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(overlay);
        Check(editing!=null && editing.FindControl<TextBox>("NoteInputBox")!.Text==entry.Text && editing.FindControl<ComboBox>("TodoPriorityCombo")!.SelectedIndex==1 && editing!.FindControl<CheckBox>("DueEnabledToggle")!.IsChecked==true,"Edit task did not open the prefilled editor.");
        editing!.FindControl<TextBox>("NoteInputBox")!.Text="Edited **task**\n\n- second";
        editing!.FindControl<CheckBox>("DueEnabledToggle")!.IsChecked=false;
        Save(editing!);Pump();
        entry=ObsidianNotesSync.LoadAllNotes(vault).Single();
        Check(entry.Text=="Edited **task**\n\n- second" && TodoOrganizationStore.Load(vault).GetDueAt(entry)==null,"Editing text or clearing the deadline failed.");
        Check(!rows.GetVisualDescendants().OfType<Border>().Any(b=>b.Name=="DeadlineSurface"),"Clearing deadline did not remove the highlight.");
        TodoOrganizationStore.SaveTodo(vault,entry,entry.Text,2,DateTime.Now.AddMinutes(-1));overlay.ReloadTodos();Pump();
        var overdue=rows.GetVisualDescendants().OfType<Border>().Single(b=>b.Name=="DeadlineSurface").Background;
        Check(soon?.ToString()!=overdue?.ToString() && rows.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text?.StartsWith("Overdue ·")==true),"Overdue tasks do not have a distinct red state.");
        ObsidianNotesSync.SetTodoCompleted(vault,entry,true);overlay.ReloadTodos();Pump();
        Check(!rows.GetVisualDescendants().OfType<Border>().Any(b=>b.Name=="DeadlineSurface"),"Completed todo still shows deadline urgency.");
        overlay.Close();
        entry=ObsidianNotesSync.LoadAllNotes(vault).Single();
        var stale=new NoteEntryWindow(NoteType.Todo,settings,entry);stale.Show();Pump();
        TodoOrganizationStore.SaveTodo(vault,entry,"Changed externally",3,null);
        stale.FindControl<TextBox>("NoteInputBox")!.Text="Stale overwrite";Save(stale);
        Check(stale.IsVisible && stale.FindControl<TextBlock>("SaveError")!.IsVisible && ObsidianNotesSync.LoadAllNotes(vault).Single().Text=="Changed externally","Stale editor overwrote a changed task or hid its error.");
        stale.Close();
    }
    static void Save(NoteEntryWindow editor) {editor.FindControl<TextBox>("NoteInputBox")!.RaiseEvent(new KeyEventArgs {RoutedEvent=InputElement.KeyDownEvent,Key=Key.Enter,KeyModifiers=KeyModifiers.Control});Dispatcher.UIThread.RunJobs();}
    static void Pump() {using var cancel=new CancellationTokenSource();DispatcherTimer.RunOnce(cancel.Cancel,TimeSpan.FromMilliseconds(180));Dispatcher.UIThread.MainLoop(cancel.Token);Dispatcher.UIThread.RunJobs();}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
}
