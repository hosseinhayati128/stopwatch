using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay;
using StopwatchOverlay.Desktop;
using StopwatchOverlay.Desktop.Views;

var checkDirectory = Path.Combine(Path.GetTempPath(), "stopwatch-check-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(checkDirectory);
Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", checkDirectory);
var structuredVault=Path.Combine(checkDirectory,"structured");
var structuredText="Installed and restarted.\n\n- **Edit** reveals Link/Delete; **Done** hides them.\n- **Delete** asks for confirmation.\n\nTests passed. See [documentation](https://example.com/docs).";
var structuredTime=new DateTime(2026,10,15,10,0,0);
ObsidianNotesSync.AppendEntry(structuredVault,NoteType.Todo,structuredText,timestamp:structuredTime);
ObsidianNotesSync.AppendEntry(structuredVault,NoteType.Todo,"Neighbor",timestamp:structuredTime.AddMinutes(1));
var structuredEntry=ObsidianNotesSync.LoadAllNotes(structuredVault).Single(e=>e.Timestamp==structuredTime);
if(structuredEntry.Text!=structuredText)throw new Exception("Todo paragraphs and list structure were lost on save/load.");
var structuredPath=Path.Combine(structuredVault,"Notes","Todos.md");
var structuredOriginal=File.ReadAllText(structuredPath);
if(!ObsidianNotesSync.SetTodoCompleted(structuredVault,structuredEntry,true).Success)throw new Exception("Structured todo could not complete.");
var structuredDone=ObsidianNotesSync.LoadAllNotes(structuredVault).Single(e=>e.Timestamp==structuredTime);
if(structuredDone.Text!=structuredText || !structuredDone.IsCompleted)throw new Exception("Completion changed formatted todo text.");
if(!ObsidianNotesSync.SetTodoCompleted(structuredVault,structuredDone,false).Success || File.ReadAllText(structuredPath)!=structuredOriginal)throw new Exception("Uncheck did not restore structured todo.");
if(!ObsidianNotesSync.DeleteTodo(structuredVault,structuredEntry).Success || File.ReadAllText(structuredPath).Contains("documentation") || ObsidianNotesSync.LoadAllNotes(structuredVault).Single().Text!="Neighbor")throw new Exception("Delete did not remove the whole structured todo safely.");
var hardBreak="First  \nNext";
ObsidianNotesSync.AppendEntry(structuredVault,NoteType.Todo,hardBreak,timestamp:structuredTime.AddMinutes(2));
if(ObsidianNotesSync.LoadAllNotes(structuredVault).First().Text!=hardBreak)throw new Exception("Markdown hard-break spaces were lost.");
var parenthesisUrl="https://en.wikipedia.org/wiki/Function_(mathematics)";
if(!FormattedTodoText.ToTelegramHtml("[Article]("+parenthesisUrl+")").Contains("href=\""+parenthesisUrl+"\""))throw new Exception("Parentheses in Markdown link were truncated.");
var structuredMessage=TelegramNotesSync.FormatMessage(NoteType.Todo,structuredText,structuredTime);
if(!structuredMessage.Contains("<b>Edit</b>") || !structuredMessage.Contains("<a href=\"https://example.com/docs\">documentation</a>") || !structuredMessage.Contains("\n\n"))throw new Exception("Telegram lost todo formatting.");
var startupConfig=Path.Combine(checkDirectory,"startup-config");
var startupLauncher=Path.Combine(checkDirectory,"stopwatch.desktop");
var startupExec="/tmp/test stopwatch";
File.WriteAllText(startupLauncher,"[Desktop Entry]\nType=Application\nExec=env HTTP_PROXY=http://127.0.0.1:10808 \"/tmp/test stopwatch\"\n[Desktop Action Other]\nExec=/wrong\n");
var linuxStartup=new StopwatchOverlay.Platform.Linux.LinuxStartupService(startupConfig,startupExec,startupLauncher);
linuxStartup.SetEnabled(true);
var startupFile=Path.Combine(startupConfig,"autostart","stopwatchoverlay.desktop");
if(!linuxStartup.IsEnabled() || !File.ReadAllText(startupFile).Contains("HTTP_PROXY=http://127.0.0.1:10808"))throw new Exception("Linux startup did not retain the installed launch command and proxy.");
File.AppendAllText(startupFile,"Hidden=true\n");
if(linuxStartup.IsEnabled())throw new Exception("Hidden startup entry was treated as enabled.");
linuxStartup.SetEnabled(false);
if(File.Exists(startupFile) || linuxStartup.IsEnabled())throw new Exception("Disabling Linux startup left its registration behind.");
linuxStartup.SetEnabled(true);
File.AppendAllText(startupFile,"X-GNOME-Autostart-enabled=false\n");
if(linuxStartup.IsEnabled())throw new Exception("GNOME-disabled startup entry was treated as enabled.");
linuxStartup.SetEnabled(false);
var fallbackStartup=new StopwatchOverlay.Platform.Linux.LinuxStartupService(startupConfig,"/tmp/test $app%name",Path.Combine(checkDirectory,"missing.desktop"));
fallbackStartup.SetEnabled(true);
if(!fallbackStartup.IsEnabled() || !File.ReadAllText(startupFile).Contains("%%name"))throw new Exception("Fallback startup executable was not safely quoted.");
fallbackStartup.SetEnabled(false);
TodoTelegramChecks.Run(checkDirectory);
TodoDeletionChecks.Run(checkDirectory);
AppBuilder.Configure<App>().UsePlatformDetect().With(new X11PlatformOptions { RenderingMode = new[] { X11RenderingMode.Software } }).WithInterFont().SetupWithoutStarting();
var formattedView=TodoRichText.Create(structuredText,false);
var formattedRuns=formattedView.Inlines!.OfType<Avalonia.Controls.Documents.Run>().ToArray();
if(!formattedRuns.Any(run=>run.Text=="Edit" && run.FontWeight==FontWeight.Bold) ||
   !formattedRuns.Any(run=>run.Text=="documentation" && run.TextDecorations==TextDecorations.Underline) ||
   !string.Concat(formattedRuns.Select(run=>run.Text)).Contains("\n\n• "))throw new Exception("Native todo rendering lost paragraphs, bullets, bold or links.");
foreach(var width in new[]{240d,180d,240d})
{
 formattedView.Measure(new Size(width,double.PositiveInfinity));
 formattedView.Arrange(new Rect(0,0,width,formattedView.DesiredSize.Height));
 formattedView.Foreground = width == 180 ? Brushes.White : Brushes.Black;
 formattedView.InvalidateMeasure();
 // Render/hit testing can ask for this before a new measure after scrolling or hover.
 if(formattedView.TextLayout.Width<=0 || formattedView.TextLayout.Height<=formattedView.FontSize*2)
     throw new Exception("Formatted todo vanished between invalidation and measure.");
 formattedView.Measure(new Size(width,double.PositiveInfinity));
 if(formattedView.DesiredSize.Width<=0)throw new Exception("Formatted todo stayed blank after remeasure.");
}
var completedRuns=TodoRichText.Create(structuredText,true).Inlines!.OfType<Avalonia.Controls.Documents.Run>();
if(completedRuns.Any(run=>run.TextDecorations!=TextDecorations.Strikethrough))throw new Exception("Completed formatted todo is not fully struck through.");
if(FormattedTodoText.ToTelegramHtml("<b>raw</b> [bad](javascript:alert)").Contains("href="))throw new Exception("Unsafe Markdown link became active.");
// The floating todo uses the existing Markdown file, including duplicate/stale-write protection.
var completionMethod = typeof(ObsidianNotesSync).GetMethod("SetTodoCompleted")
    ?? throw new Exception("Todo completion is not implemented.");
var todoVault = Path.Combine(checkDirectory, "overlay-vault");
Directory.CreateDirectory(Path.Combine(todoVault, "Notes"));
var todoPath = Path.Combine(todoVault, "Notes", "Todos.md");
var todoDocument = "# Todos\r\n## 2026-10-15\r\n- [ ] 09:10 Read chapter\r\n- [ ] 09:11 Review notes\r\n## 2026-10-14\r\n- [ ] 09:10 Read chapter\r\n";
File.WriteAllText(todoPath, todoDocument);
var todoEntry = ObsidianNotesSync.LoadAllNotes(todoVault).First();
var completion = (NoteSyncResult)completionMethod.Invoke(null, new object?[] { todoVault, todoEntry, true, "Notes" })!;
if (!completion.Success || File.ReadAllText(todoPath) != todoDocument.Replace("- [ ] 09:11 Review notes", "- [x] 09:11 Review notes"))
    throw new Exception("Completing a task changed unrelated Markdown or did not persist.");
var snapshot = File.ReadAllText(todoPath);
completion = (NoteSyncResult)completionMethod.Invoke(null, new object?[] { todoVault, todoEntry, false, "Notes" })!;
if (completion.Success || File.ReadAllText(todoPath) != snapshot) throw new Exception("Stale todo completion overwrote an external change.");
var latest = ObsidianNotesSync.LoadAllNotes(todoVault).First();
completion = (NoteSyncResult)completionMethod.Invoke(null, new object?[] { todoVault, latest, false, "Notes" })!;
if (!completion.Success || File.ReadAllText(todoPath) != todoDocument) throw new Exception("Todo could not be unchecked.");
File.AppendAllText(todoPath, "- [ ] 09:10 Read chapter\r\n");
var ambiguous = ObsidianNotesSync.LoadAllNotes(todoVault).Last();
snapshot = File.ReadAllText(todoPath);
completion = (NoteSyncResult)completionMethod.Invoke(null, new object?[] { todoVault, ambiguous, true, "Notes" })!;
if (completion.Success || File.ReadAllText(todoPath) != snapshot) throw new Exception("Ambiguous duplicate task was changed.");
File.WriteAllText(todoPath, todoDocument);
File.WriteAllText(todoPath, "## 2026-10-15\n- [ ] 09:10 ⏰ Same task\n- [ ] 09:10 Same task\n");
var markedDuplicate = ObsidianNotesSync.LoadAllNotes(todoVault).First();
snapshot = File.ReadAllText(todoPath);
completion = (NoteSyncResult)completionMethod.Invoke(null, new object?[] { todoVault, markedDuplicate, true, "Notes" })!;
if (completion.Success || File.ReadAllText(todoPath) != snapshot) throw new Exception("Marked duplicate changed the wrong todo.");
File.WriteAllText(todoPath, todoDocument);
var positionResolver = typeof(TodoOverlayWindow).GetMethod("ClampToScreens", BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new Exception("Monitor-aware todo positioning is missing.");
var primaryScreen = new Avalonia.Platform.Screen(1, new PixelRect(0,0,1920,1080), new PixelRect(0,32,1920,1048), true);
var secondScreen = new Avalonia.Platform.Screen(1, new PixelRect(1920,0,1920,1080), new PixelRect(1920,32,1920,1048), false);
var fakeScreens = new[] { primaryScreen,secondScreen };
PixelPoint ClampTodo(PixelPoint anchor, Avalonia.Platform.Screen[] screens) =>
    (PixelPoint)positionResolver.Invoke(null,new object[]{anchor,new Size(360,430),screens})!;
if(ClampTodo(new PixelPoint(1920,0),fakeScreens)!=new PixelPoint(1920,32) ||
    ClampTodo(new PixelPoint(3752,0),fakeScreens)!=new PixelPoint(3480,32))
    throw new Exception("Expanding at the second monitor's top corners switched to the primary screen.");
var leftScreen = new Avalonia.Platform.Screen(1,new PixelRect(-1280,0,1280,1024),new PixelRect(-1280,32,1280,992),false);
if(ClampTodo(new PixelPoint(-1280,0),new[]{primaryScreen,leftScreen})!=new PixelPoint(-1280,32))
    throw new Exception("Negative-coordinate monitor positioning failed.");
var scaledSecond = new Avalonia.Platform.Screen(1.5,new PixelRect(1920,0,1920,1080),new PixelRect(1920,32,1920,1048),false);
if(ClampTodo(new PixelPoint(3752,0),new[]{primaryScreen,scaledSecond})!=new PixelPoint(3300,32))
    throw new Exception("Mixed-DPI monitor positioning failed.");
var todoType = typeof(App).Assembly.GetType("StopwatchOverlay.Desktop.Views.TodoOverlayWindow")
    ?? throw new Exception("Floating todo overlay is missing.");
var todoSettings = new AppSettings { ObsidianVaultFolder = todoVault, TelegramEnabled = false, ThemeMode = AppThemeCatalog.Pirate };
var todoWindow = (Window)Activator.CreateInstance(todoType, todoSettings)!;
var expanded = todoType.GetProperty("IsExpanded")!;
var toggleTodo = todoType.GetMethod("ToggleExpanded")!;
if ((bool)expanded.GetValue(todoWindow)!) throw new Exception("Todo overlay did not start collapsed.");
todoWindow.Show();
AssertNativeTopmost(todoWindow,"initial show");
var pendingDragTimer=(DispatcherTimer)todoType.GetField("_positionTimer",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(todoWindow)!;
pendingDragTimer.Start();
toggleTodo.Invoke(todoWindow, null);
if(pendingDragTimer.IsEnabled)throw new Exception("Expand did not commit a pending drag position before resizing.");
todoWindow.UpdateLayout();
if (!(bool)expanded.GetValue(todoWindow)!) throw new Exception("Todo overlay did not expand.");
var openingFocus = todoWindow.FocusManager!.GetFocusedElement() as Avalonia.Interactivity.Interactive
    ?? throw new Exception("Expanded todo did not receive keyboard focus.");
openingFocus.RaiseEvent(new KeyEventArgs { RoutedEvent=InputElement.KeyDownEvent, Key=Key.Space, PhysicalKey=PhysicalKey.Space });
openingFocus.RaiseEvent(new KeyEventArgs { RoutedEvent=InputElement.KeyUpEvent, Key=Key.Space, PhysicalKey=PhysicalKey.Space });
Dispatcher.UIThread.RunJobs();
if (!(bool)expanded.GetValue(todoWindow)!) throw new Exception("The opening Space press immediately collapsed Todos again.");
// A hover state change must not erase formatted text inside a checkbox template.
var hoverRows=todoWindow.FindControl<StackPanel>("TodoRows")!.GetVisualDescendants().OfType<CheckBox>().ToArray();
foreach(var hoverRow in hoverRows)
{
 var rich=(TextBlock)hoverRow.Content!;
 string originalRuns=string.Concat(rich.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Select(run=>run.Text));
 var pseudo=(Avalonia.Controls.IPseudoClasses)typeof(StyledElement).GetProperty("PseudoClasses",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(hoverRow)!;
 pseudo.Set(":pointerover",true);
 // Scrolling can render the invalidated text before the next queued measure pass.
 if(rich.TextLayout.Width<=0)throw new Exception("Hover invalidation produced an empty todo text layout before measure.");
 todoWindow.UpdateLayout();Dispatcher.UIThread.RunJobs();
 string hoveredRuns=string.Concat(rich.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Select(run=>run.Text));
 if(hoveredRuns!=originalRuns || rich.TextLayout.Width<=0)throw new Exception("Hover erased a formatted todo row.");
 pseudo.Set(":pointerover",false);
}
var addRequested = false;
todoType.GetEvent("AddTodoRequested")!.AddEventHandler(todoWindow, (Action)(() => addRequested = true));
todoWindow.FindControl<Button>("AddTodoButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
if (!addRequested) throw new Exception("Add todo button did not open the entry flow.");
var todoRows = todoWindow.FindControl<StackPanel>("TodoRows")!;
var editButton=todoWindow.FindControl<Button>("EditTodosButton") ?? throw new Exception("Todo Edit mode is missing.");
if(todoWindow.FindControl<StackPanel>("TodoRows")!.GetVisualDescendants().OfType<Button>().Any(button=>button.Content is string && button.IsEffectivelyVisible))
    throw new Exception("Task actions are visible outside Edit mode.");
editButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
Dispatcher.UIThread.RunJobs();
if(!todoRows.GetVisualDescendants().OfType<Button>().Any(button=>button.Content?.ToString()=="Delete" && button.IsEffectivelyVisible))
    throw new Exception("Delete controls did not appear in Edit mode.");
// Opening Delete must not change a file until its task-specific confirmation is accepted.
var firstDelete=todoRows.GetVisualDescendants().OfType<Button>().First(button=>button.Content?.ToString()=="Delete");
var beforeCancel=File.ReadAllText(todoPath);
firstDelete.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
Dispatcher.UIThread.RunJobs();
var confirmation=(Window?)todoType.GetField("_deleteDialog",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(todoWindow);
if(confirmation==null || File.ReadAllText(todoPath)!=beforeCancel)throw new Exception("Delete did not wait for confirmation.");
confirmation.GetVisualDescendants().OfType<Button>().First(button=>button.Content?.ToString()=="Cancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
Dispatcher.UIThread.RunJobs();
if(File.ReadAllText(todoPath)!=beforeCancel)throw new Exception("Cancelling confirmation changed the todo file.");
editButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
Dispatcher.UIThread.RunJobs();
if(todoRows.GetVisualDescendants().OfType<Button>().Any(button=>button.Content is string && button.IsEffectivelyVisible))
    throw new Exception("Done did not hide task actions.");

var checkbox = todoRows.GetVisualDescendants().OfType<CheckBox>().First();
checkbox.IsChecked = true;
Dispatcher.UIThread.RunJobs();
if (!ObsidianNotesSync.LoadAllNotes(todoVault).Any(item => item.IsCompleted)) throw new Exception("Overlay checkbox did not save completion.");
var showCompleted = todoWindow.FindControl<CheckBox>("ShowCompletedToggle")
    ?? throw new Exception("Completed-task filter is missing.");
snapshot = File.ReadAllText(todoPath);
showCompleted.IsChecked = false;
Dispatcher.UIThread.RunJobs();
if (todoRows.GetVisualDescendants().OfType<CheckBox>().Count() != 2 || File.ReadAllText(todoPath) != snapshot)
    throw new Exception("Hiding completed tasks changed the file or left completed rows visible.");
var filterSetting = typeof(AppSettings).GetProperty("TodoOverlayShowCompleted")!;
if ((bool)filterSetting.GetValue(SettingsStore.Load())!) throw new Exception("Completed filter preference was not saved.");
showCompleted.IsChecked = true;
Dispatcher.UIThread.RunJobs();
if (todoRows.GetVisualDescendants().OfType<CheckBox>().Count() != 3) throw new Exception("Completed tasks did not reappear.");
todoSettings.ThemeMode = AppThemeCatalog.Daylight;
todoType.GetMethod("ApplySettings")!.Invoke(todoWindow, null);
if (todoWindow.FindControl<Control>("SkullArtwork")!.IsVisible) throw new Exception("One Piece logo remained in another theme.");
toggleTodo.Invoke(todoWindow, null);
if ((bool)expanded.GetValue(todoWindow)! || todoWindow.FindControl<Border>("TodoPanel")!.IsVisible) throw new Exception("Todo overlay did not collapse.");
if (!((DispatcherTimer)todoType.GetField("_refreshTimer", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(todoWindow)!).IsEnabled)
    throw new Exception("Obsidian polling stopped while the todo overlay was collapsed.");
todoWindow.Close();
// Exercise the real focused multiline editor; save only to a temporary local vault.
foreach (var noteType in new[] { NoteType.Note, NoteType.Todo, NoteType.Reminder })
{
    var vault = Path.Combine(checkDirectory, "vault-" + noteType);
    Directory.CreateDirectory(vault);
    var noteSettings = new AppSettings { ObsidianVaultFolder = vault, TelegramEnabled = false };
    var editor = new NoteEntryWindow(noteType, noteSettings);
    editor.Show();
    editor.UpdateLayout();
    var input = editor.FindControl<TextBox>("NoteInputBox")!;
    input.Focus();
    Dispatcher.UIThread.RunJobs();
    if (!input.IsFocused) throw new Exception("Note editor test did not receive keyboard focus.");
    var editorBorder = input.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_BorderElement");
    if ((editorBorder.Background as ISolidColorBrush)?.Color != Colors.Transparent)
        throw new Exception("Focused note input replaced the theme background: " + editorBorder.Background);
    // Theme changes must also remain readable while the editor keeps focus.
    var applyTheme = typeof(App).Assembly.GetType("StopwatchOverlay.Desktop.ThemeResources")!
        .GetMethod("ApplyApplication", BindingFlags.Static | BindingFlags.NonPublic)!;
    foreach (var theme in new[] { AppThemeCatalog.Pirate, AppThemeCatalog.Daylight, AppThemeCatalog.Midnight })
    {
        applyTheme.Invoke(null, new object[] { theme });
        Dispatcher.UIThread.RunJobs();
        Application.Current!.TryFindResource("PrimaryTextBrush", out var primaryText);
        if ((editorBorder.Background as ISolidColorBrush)?.Color != Colors.Transparent ||
            (input.Foreground as ISolidColorBrush)?.Color != ((ISolidColorBrush)primaryText!).Color)
            throw new Exception("Focused note editor stopped following the theme: " + theme);
    }
    input.Text = "shortcut save test";
    input.CaretIndex = input.Text.Length;
    input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
    if (!input.Text.Contains('\n')) throw new Exception("Plain Enter no longer inserts a newline.");
    input.Text = "shortcut save test";
    var closed = false;
    editor.Closed += (_, _) => closed = true;
    input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, KeyModifiers = KeyModifiers.Control });
    Dispatcher.UIThread.RunJobs();
    var notePath = Path.Combine(vault, "Notes", ObsidianNotesSync.GetFileName(noteType));
    if (!File.Exists(notePath) || !File.ReadAllText(notePath).Contains("shortcut save test") || !closed)
        throw new Exception("Ctrl+Enter from the multiline editor did not save and close " + noteType);
}
var noteKeyHint = new NoteCommandHintWindow();
noteKeyHint.Show();
Dispatcher.UIThread.RunJobs();
if (!noteKeyHint.Focus()) throw new Exception("The note shortcut menu cannot receive keyboard focus.");
noteKeyHint.Close();
var timerKeyHint = new ShortcutCommandHintWindow();
timerKeyHint.Show();
Dispatcher.UIThread.RunJobs();
if (!timerKeyHint.Focus()) throw new Exception("The timer shortcut menu cannot receive keyboard focus.");
timerKeyHint.Close();
// Exercise the actual routed key handler used by the Wayland menu, without registering global shortcuts.
StopwatchOverlay.Platform.PlatformServices.HotKey = new StopwatchOverlay.Desktop.Platform.PortalHotKeyService();
var controller = new ControllerWindow();
var flags = BindingFlags.NonPublic | BindingFlags.Instance;
var settingsField = typeof(ControllerWindow).GetField("_settings", flags)!;
var active = (AppSettings)settingsField.GetValue(controller)!;
var routeEnter = typeof(ControllerWindow).GetMethod("EnterShortcutCommandMode", flags)!;
routeEnter.Invoke(controller,new object[]{true});
var routedHint=(Window)typeof(ControllerWindow).GetField("_commandHintWindow",flags)!.GetValue(controller)!;
if(!routedHint.IsFocused)throw new Exception("Wayland menu was displayed without keyboard focus.");
routedHint.RaiseEvent(new KeyEventArgs { RoutedEvent=InputElement.KeyDownEvent, Key=Key.N, PhysicalKey=PhysicalKey.N });
Dispatcher.UIThread.RunJobs();
if((bool)typeof(ControllerWindow).GetField("_isCommandModeActive",flags)!.GetValue(controller)!)throw new Exception("N did not reach the note menu's routed command handler.");
routeEnter.Invoke(controller,new object[]{false});
var timerRouteHint=(Window)typeof(ControllerWindow).GetField("_commandHintWindow",flags)!.GetValue(controller)!;
var routedTimer=(TimerSession)typeof(ControllerWindow).GetProperty("CurrentTimer",flags)!.GetValue(controller)!;
bool originalRunning=routedTimer.IsRunning;
timerRouteHint.RaiseEvent(new KeyEventArgs { RoutedEvent=InputElement.KeyDownEvent,Key=Key.Space,PhysicalKey=PhysicalKey.Space });
Dispatcher.UIThread.RunJobs();
if(routedTimer.IsRunning==originalRunning)throw new Exception("Space did not reach the timer menu's routed command handler.");
timerRouteHint.RaiseEvent(new KeyEventArgs { RoutedEvent=InputElement.KeyDownEvent,Key=Key.Space,PhysicalKey=PhysicalKey.Space });
Dispatcher.UIThread.RunJobs();
if(routedTimer.IsRunning!=originalRunning)throw new Exception("Timer command chaining stopped receiving follow-up keys.");
typeof(ControllerWindow).GetMethod("ExitShortcutCommandMode",flags)!.Invoke(controller,null);
var expectedNoteKeys = new Dictionary<NoteCommandAction, uint> {
    [NoteCommandAction.AddTodo] = 0x54, [NoteCommandAction.AddNote] = 0x4E,
    [NoteCommandAction.ViewNotes] = 0x56, [NoteCommandAction.AddReminder] = 0x52
};
foreach (var pair in expectedNoteKeys)
    if (active.NoteCommandModeKeys[pair.Key] != pair.Value) throw new Exception("Note default is not the requested letter: " + pair.Key);
var enterCommandMode = typeof(ControllerWindow).GetMethod("EnterShortcutCommandMode", flags)!;
var commandKey = typeof(ControllerWindow).GetMethod("OnCommandKeyPressed", flags)!;
var modeActive = typeof(ControllerWindow).GetField("_isCommandModeActive", flags)!;
enterCommandMode.Invoke(controller, new object[] { true });
commandKey.Invoke(controller, new object?[] { null, active.NoteLeaderShortcut.VirtualKey });
Dispatcher.UIThread.RunJobs();
if (!(bool)modeActive.GetValue(controller)!) throw new Exception("Leader repeat cancelled the note menu.");
var hint = (Window)typeof(ControllerWindow).GetField("_commandHintWindow", flags)!.GetValue(controller)!;
if (hint.FindControl<TextBlock>("NoteOptions")?.Text?.Contains("N Quick Note") != true)
    throw new Exception("Note menu does not display the configured letters.");
if (((DispatcherTimer)typeof(ControllerWindow).GetField("_commandModeTimer", flags)!.GetValue(controller)!).Interval < TimeSpan.FromSeconds(5))
    throw new Exception("Note menu times out sooner than the Windows version.");
enterCommandMode.Invoke(controller, new object[] { true });
commandKey.Invoke(controller, new object?[] { null, (uint)0x20 });
Dispatcher.UIThread.RunJobs();
var shortcutTodo = (Window)typeof(ControllerWindow).GetField("_todoOverlayWindow", flags)!.GetValue(controller)!;
if (!(bool)expanded.GetValue(shortcutTodo)!) throw new Exception("Note leader then Space did not expand the todo overlay.");
enterCommandMode.Invoke(controller,new object[]{true});
commandKey.Invoke(controller,new object?[]{null,(uint)0x4F});
Dispatcher.UIThread.RunJobs();
if(shortcutTodo.IsVisible || !(bool)expanded.GetValue(shortcutTodo)! || (bool)modeActive.GetValue(controller)!)throw new Exception("Note leader then O did not hide the overlay while retaining expansion state.");
enterCommandMode.Invoke(controller,new object[]{true});
commandKey.Invoke(controller,new object?[]{null,(uint)0x4F});
Dispatcher.UIThread.RunJobs();
if(!shortcutTodo.IsVisible || !(bool)expanded.GetValue(shortcutTodo)!)throw new Exception("Note leader then O did not restore the overlay.");
AssertNativeTopmost(shortcutTodo,"hide/show shortcut");
var ordinaryWindow=new Window{Title="Stopwatch topmost regression",Width=120,Height=80};
ordinaryWindow.Show();
AssertNativeTopmost(shortcutTodo,"mapping another window");
ordinaryWindow.Activate();
AssertNativeTopmost(shortcutTodo,"switching to another window");
if(OperatingSystem.IsLinux() && File.Exists("/usr/bin/xprop"))
{
 var psi=new System.Diagnostics.ProcessStartInfo("/usr/bin/xprop"){RedirectStandardOutput=true,UseShellExecute=false};
 foreach(var arg in new[]{"-root","_NET_CLIENT_LIST_STACKING","_NET_ACTIVE_WINDOW"})psi.ArgumentList.Add(arg);
 using var process=System.Diagnostics.Process.Start(psi)!;
 var state=process.StandardOutput.ReadToEnd();process.WaitForExit();
 var stacking=state.Split('\n')[0];
 string overlayId="0x"+shortcutTodo.TryGetPlatformHandle()!.Handle.ToInt64().ToString("x");
 string ordinaryId="0x"+ordinaryWindow.TryGetPlatformHandle()!.Handle.ToInt64().ToString("x");
 if(stacking.IndexOf(overlayId,StringComparison.Ordinal)<stacking.IndexOf(ordinaryId,StringComparison.Ordinal))throw new Exception("Todo overlay fell below the selected ordinary window.");
 if(!state.Split('\n').Skip(1).Any(line=>line.Contains(ordinaryId)))throw new Exception("Focus check: expected "+ordinaryId+" active="+ordinaryWindow.IsActive+"; "+state);
}
ordinaryWindow.Close();
if(!active.NoteCommandModeKeys.Values.Contains(0x4Fu))throw new Exception("O overlay shortcut has no configurable default.");

enterCommandMode.Invoke(controller, new object[] { true });
commandKey.Invoke(controller, new object?[] { null, (uint)0x20 });
Dispatcher.UIThread.RunJobs();
if ((bool)expanded.GetValue(shortcutTodo)!) throw new Exception("Note leader then Space did not collapse the todo overlay.");
enterCommandMode.Invoke(controller,new object[]{true});
commandKey.Invoke(controller,new object?[]{null,(uint)0x4F});
Dispatcher.UIThread.RunJobs();
if(shortcutTodo.IsVisible || (bool)expanded.GetValue(shortcutTodo)!)throw new Exception("O failed to hide the collapsed logo.");
enterCommandMode.Invoke(controller,new object[]{true});
commandKey.Invoke(controller,new object?[]{null,(uint)0x20});
Dispatcher.UIThread.RunJobs();
if(!shortcutTodo.IsVisible || !(bool)expanded.GetValue(shortcutTodo)!)throw new Exception("Space did not reveal and expand the hidden overlay.");
shortcutTodo.Close();
foreach (var pair in expectedNoteKeys) {
    enterCommandMode.Invoke(controller, new object[] { true });
    commandKey.Invoke(controller, new object?[] { null, pair.Value });
    Dispatcher.UIThread.RunJobs();
    if ((bool)modeActive.GetValue(controller)!) throw new Exception("Note letter was not handled: " + pair.Key);
}
var startupProbe = new StartupProbe();
StopwatchOverlay.Platform.PlatformServices.Startup = startupProbe;
active.StartWithWindows = true; // A stale saved setting must not override the system.
active.TextSize = 61;
typeof(ControllerWindow).GetMethod("SettingsButton_Click", flags)!.Invoke(controller, new object?[] { null, null });
var window = (SettingsWindow)typeof(ControllerWindow).GetField("_settingsWindow", flags)!.GetValue(controller)!;
var startupCheck = window.FindControl<CheckBox>("StartWithWindowsCheck")!;
if(startupCheck.IsChecked == true) throw new Exception("Startup checkbox does not reflect system registration.");
startupCheck.IsChecked = true;
if(!startupProbe.Enabled || !SettingsStore.Load().StartWithWindows) throw new Exception("Startup checkbox did not enable login startup.");
startupCheck.IsChecked = false;
if(startupProbe.Enabled || SettingsStore.Load().StartWithWindows) throw new Exception("Startup checkbox did not disable login startup.");
startupProbe.AllowChanges=false;
startupCheck.IsChecked=true;
if(startupCheck.IsChecked==true || active.StartWithWindows || SettingsStore.Load().StartWithWindows)throw new Exception("Failed startup registration left the checkbox falsely enabled.");
startupProbe.AllowChanges=true;
var slider = window.FindControl<Slider>("TextSizeSlider")!;
if (slider.Value != 61) throw new Exception("Settings window did not load the active settings.");
slider.Value = 72;
if (!ReferenceEquals(active, settingsField.GetValue(controller))) throw new Exception("Controller replaced the live settings object.");
if (active.TextSize != 72) throw new Exception("Live settings did not change.");
if (SettingsStore.Load().TextSize != 72) throw new Exception("Settings change was not persisted.");
typeof(SettingsWindow).GetMethod("DoneButton_Click", flags)!.Invoke(window, new object?[] { null, null });
typeof(ControllerWindow).GetMethod("SettingsButton_Click", flags)!.Invoke(controller, new object?[] { null, null });
window = (SettingsWindow)typeof(ControllerWindow).GetField("_settingsWindow", flags)!.GetValue(controller)!;
if (window.FindControl<Slider>("TextSizeSlider")!.Value != 72) throw new Exception("Reopened settings lost the change.");
var format = window.FindControl<ComboBox>("FormatCombo")!;
format.SelectedIndex = 1;
var timer = (TimerSession)typeof(ControllerWindow).GetProperty("CurrentTimer", flags)!.GetValue(controller)!;
var display = (string)typeof(ControllerWindow).GetMethod("FormatDisplayTime", flags | BindingFlags.Static)!.Invoke(controller, new object[] { timer })!;
if (display.Contains('.')) throw new Exception("HH:MM:SS still displays tenths.");
window.FindControl<ComboBox>("ThemeCombo")!.SelectedItem = AppThemeCatalog.Daylight;
if (!Application.Current!.Resources.TryGetResource("AppBackgroundBrush", Application.Current.ActualThemeVariant, out var resource)) throw new Exception("Theme resource missing.");
var color = ((Avalonia.Media.SolidColorBrush)resource!).Color;
if (color.ToString() != "#fff4f7f8") throw new Exception("Daylight palette did not apply.");
foreach (var theme in AppThemeCatalog.All)
{
    window.FindControl<ComboBox>("ThemeCombo")!.SelectedItem = theme;
    if (active.ThemeMode != theme || SettingsStore.Load().ThemeMode != theme) throw new Exception("Theme not saved: " + theme);
    if (!Application.Current.Resources.TryGetResource("AccentBrush", Application.Current.ActualThemeVariant, out _)) throw new Exception("Palette missing: " + theme);
}
var overlay = new OverlayWindow();
if (!overlay.TransparencyLevelHint.Contains(Avalonia.Controls.WindowTransparencyLevel.Transparent))
    throw new Exception("The stopwatch and hover toolbar do not request transparent native rendering.");
foreach (var theme in OverlayThemeCatalog.All)
{
    overlay.ApplyTheme(theme, active.ThemeMode);
    if (overlay.EffectiveOverlayTheme != OverlayThemeCatalog.Resolve(theme, active.ThemeMode)) throw new Exception("Overlay theme did not resolve: " + theme);
}
if (!controller.Classes.Contains("pirate")) throw new Exception("One Piece controller styling was not enabled.");
if (controller.FindControl<PirateActionPanel>("ControllerActions")?.Pirate != true) throw new Exception("One Piece action layout was not enabled.");
window.FindControl<ComboBox>("ThemeCombo")!.SelectedItem = AppThemeCatalog.Daylight;
if (controller.Classes.Contains("pirate")) throw new Exception("One Piece artwork remained after changing theme.");
overlay.ApplyTheme(OverlayThemeCatalog.AcanthusLight, AppThemeCatalog.Midnight);
var face = overlay.FindControl<ThemedClockFace>("ClockFace") ?? throw new Exception("Overlay themed face is missing.");
if (face.ClockPadding != new Thickness(18,10,18,12)) throw new Exception("Acanthus clock padding is not applied.");
overlay.ApplyTheme(OverlayThemeCatalog.PixelDeckNight, AppThemeCatalog.Daylight);
if (face.ClockCornerRadius != new CornerRadius(3)) throw new Exception("Pixel Deck clock geometry is not applied.");
// Exercise the actual settings controls through persistence and live hover layers.
window.FindControl<ComboBox>("OverlayThemeCombo")!.SelectedItem = OverlayThemeCatalog.Pirate;
window.FindControl<Slider>("BackgroundOpacitySlider")!.Value = 31;
foreach (var name in new[]{"ControlBoardOpaqueCheck","ControlMapOpaqueCheck","ControlDialsOpaqueCheck"})
    window.FindControl<CheckBox>(name)!.IsChecked = false;
var liveOverlay=(OverlayWindow)typeof(ControllerWindow).GetField("_herdOverlay",flags)!.GetValue(controller)!;
var liveFace=liveOverlay.FindControl<ThemedClockFace>("ClockFace")!;
var hover=liveOverlay.FindControl<Border>("ActionSurface")!;
var hoverArt=hover.GetVisualDescendants().OfType<NavigatorToolbarArtwork>().Single();
if(Math.Abs(hoverArt.SurfaceOpacity-.31)>.001 || hoverArt.OpaqueParts.HasFlag(NavigatorOpaqueParts.ControlMap) ||
   Math.Abs(liveOverlay.FindControl<Button>("CloseActionButton")!.Opacity-.31)>.001)
    throw new Exception("Hover transparency controls did not reach the live toolbar.");
window.FindControl<CheckBox>("ControlMapOpaqueCheck")!.IsChecked=true;
window.FindControl<CheckBox>("ControlDialsOpaqueCheck")!.IsChecked=true;
if(!hoverArt.OpaqueParts.HasFlag(NavigatorOpaqueParts.ControlMap) || liveOverlay.FindControl<Button>("CloseActionButton")!.Opacity!=1 ||
   !SettingsStore.Load().OpaqueOverlayParts.HasFlag(NavigatorOpaqueParts.ControlMap))
    throw new Exception("Opaque hover layers did not update or persist.");
var preview = window.FindControl<ThemedClockFace>("PreviewClock") ?? throw new Exception("Preview does not use the shared renderer.");
active.OverlayTheme = OverlayThemeCatalog.Pirate;
active.BackgroundOpacity = 23;
active.OpaqueOverlayParts = NavigatorOpaqueParts.ClockFrame | NavigatorOpaqueParts.MetalBorder;
preview.ApplySettings(active);
overlay.ApplyTheme(active.OverlayTheme, active.ThemeMode);
overlay.ApplySettings(Avalonia.Media.Colors.White, Avalonia.Media.Colors.Black, 72, 2, "Consolas", .23, false, active.OpaqueOverlayParts);
if (!face.IsNavigator || !preview.IsNavigator) throw new Exception("Navigator artwork was not selected.");
if (face.LayerOpacity(NavigatorOpaqueParts.ClockFrame) != 1 || Math.Abs(face.LayerOpacity(NavigatorOpaqueParts.ClockMap) - .23) > .001)
    throw new Exception("Navigator opacity layers are not independent.");
if (preview.ClockPadding != face.ClockPadding || !preview.TimerFont.Equals(face.TimerFont))
    throw new Exception("Preview and overlay typography or layout differ.");
typeof(SettingsWindow).GetMethod("UpdatePreviewSafely", flags)!.Invoke(window, null);
window.UpdateLayout();
Dispatcher.UIThread.RunJobs();
using (var previewRender = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height), new Vector(96,96)))
    previewRender.Render(window);
var clockPeer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(face);
if (clockPeer?.GetName()?.Contains(face.Text) != true) throw new Exception("Themed clock lost its accessible timer value.");
var engraved = new PirateTimerText { Text = "01:23:45" };
if (Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(engraved)?.GetName() != "01:23:45")
    throw new Exception("Engraved controller timer lost its accessible value.");
active.OverlayTheme = OverlayThemeCatalog.Midnight;
typeof(SettingsWindow).GetMethod("UpdatePreviewSafely", flags)!.Invoke(window, null);
window.UpdateLayout();
if (window.FindControl<Button>("PreviewCloseButton")!.Template is null)
    throw new Exception("Normal toolbar button template was lost after switching from Navigator.");
var portalType = typeof(App).Assembly.GetType("StopwatchOverlay.Desktop.Platform.PortalHotKeyService") ?? throw new Exception("Wayland portal hotkey service is missing.");
var trigger = portalType.GetMethod("FormatTrigger", BindingFlags.Static | BindingFlags.NonPublic)!;
if ((string?)trigger.Invoke(null, new object[] { Shortcut.MOD_WIN, (uint)0x71 }) != "LOGO+F2") throw new Exception("Portal leader trigger is incorrect.");
if ((string?)trigger.Invoke(null, new object[] { Shortcut.MOD_WIN | Shortcut.MOD_SHIFT, (uint)0x76 }) != "SHIFT+LOGO+F7") throw new Exception("Portal direct trigger is incorrect.");
Console.WriteLine("PASS: Native always-on-top survives hide/show and window switching without stealing focus; F3→O hides/restores expanded todos and collapsed logo; F3→Space reveals hidden todos; Hover/scroll inline text remains visible before the queued layout pass and across width changes; Linux login startup enable/disable, system-state checkbox, failed registration rollback, proxy preservation and disabled entries verified; Structured todos preserve paragraphs, lists, bold and links through storage, completion, deletion, native rendering and Telegram edits; Edit/Done hides actions, delete confirmation cancellation and coordinated Telegram/Obsidian deletion are safe; shortcut menus focus and route N/Space; multi-monitor corners and DPI hold position; Telegram completion edits, restore, retries, old links, account/destination isolation and receipt recovery; completed filter persists and polling stays active when collapsed; floating todos start collapsed, toggle by Space, keep theme and save checkboxes safely; Ctrl+Enter saves all three note types, plain Enter inserts a newline, focused editor follows three themes; note letters, leader repeats, menu labels and timeout verified; settings persisted, HH:MM:SS applied, all palettes loaded, controller artwork switches, clock geometry and preview match, independent opacity layers apply, portal triggers formatted.");

Directory.Delete(checkDirectory, recursive: true);
Environment.Exit(0);

static void AssertNativeTopmost(Window view,string scenario)
{
 if(!OperatingSystem.IsLinux() || !File.Exists("/usr/bin/xprop"))return;
 using var cancel=new CancellationTokenSource();
 DispatcherTimer.RunOnce(cancel.Cancel,TimeSpan.FromMilliseconds(150));
 Dispatcher.UIThread.MainLoop(cancel.Token);
 var psi=new System.Diagnostics.ProcessStartInfo("/usr/bin/xprop"){RedirectStandardOutput=true,UseShellExecute=false};
 foreach(var arg in new[]{"-id",view.TryGetPlatformHandle()!.Handle.ToInt64().ToString(),"_NET_WM_STATE"})psi.ArgumentList.Add(arg);
 using var process=System.Diagnostics.Process.Start(psi)!;
 var state=process.StandardOutput.ReadToEnd();process.WaitForExit();
 if(!state.Contains("_NET_WM_STATE_ABOVE"))throw new Exception("Todo overlay lost native always-on-top state after "+scenario+": "+state);
}

sealed class StartupProbe : StopwatchOverlay.Platform.IStartupService
{
    public bool IsSupported => true;
    public bool Enabled { get; private set; }
    public bool IsEnabled() => Enabled;
    public bool AllowChanges { get; set; } = true;
    public void SetEnabled(bool enabled) { if(AllowChanges) Enabled = enabled; }
}
