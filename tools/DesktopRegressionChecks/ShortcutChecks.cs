using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using SharpHook;
using SharpHook.Native;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Platform;
using StopwatchOverlay.Desktop.Views;
using StopwatchOverlay.Platform;

internal static class ShortcutChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    private sealed class Fixture : IDisposable
    {
        private readonly IDisposable _openedHandler;
        private readonly List<Window> _opened = new();
        private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), $"stopwatch-shortcuts-{Guid.NewGuid():N}");
        private readonly SharpHookHotKeyService _hook = (SharpHookHotKeyService)PlatformServices.HotKey;
        internal AppSettings Settings { get; } = new() { ActivityWatchEnabled = false };
        internal ControllerWindow Controller { get; }
        internal TimerSessionManager Timers { get; }
        internal ProjectTimeStore HistoryStore { get; }

        internal Fixture()
        {
            Settings.EnsureAllActions();
            var constructor = typeof(ControllerWindow).GetConstructor(Private, null, new[] { typeof(AppSettings) }, null)!;
            Controller = (ControllerWindow)constructor.Invoke(new object[] { Settings });
            HistoryStore = new ProjectTimeStore(Path.Combine(_dataDirectory, "project-history.json"));
            typeof(ControllerWindow).GetField("_projectTimeStore", Private)!.SetValue(Controller, HistoryStore);
            Timers = (TimerSessionManager)typeof(ControllerWindow).GetField("_timerManager", Private)!.GetValue(Controller)!;
            _openedHandler = Avalonia.Controls.Window.WindowOpenedEvent.AddClassHandler<Window>((window, _) => _opened.Add(window));
            Call("InitializePlatformServices");
        }

        internal void Call(string name, params object?[] arguments) =>
            typeof(ControllerWindow).GetMethod(name, Private)!.Invoke(Controller, arguments);

        internal void Key(KeyCode code, ModifierMask modifiers = ModifierMask.None)
        {
            foreach (var (method, type) in new[] { ("OnKeyPressed", EventType.KeyPressed), ("OnKeyReleased", EventType.KeyReleased) })
            {
                var args = new KeyboardHookEventArgs(new UioHookEvent
                {
                    Type = type, Mask = modifiers, Keyboard = new KeyboardEventData { KeyCode = code }
                });
                typeof(SharpHookHotKeyService).GetMethod(method, Private)!.Invoke(_hook, new object?[] { null, args });
            }
        }

        internal void Command(KeyCode key, bool notes = false)
        {
            Call("ExitShortcutCommandMode");
            Key(notes ? KeyCode.VcF3 : KeyCode.VcF2, ModifierMask.LeftMeta);
            Key(key);
            ClockOverlayChecks.Pump(35);
        }

        internal Window Window(string type) => _opened.LastOrDefault(window => window.IsVisible && window.GetType().Name == type)
            ?? throw new Exception($"Shortcut did not open {type}.");

        public void Dispose()
        {
            Call("ExitShortcutCommandMode");
            foreach (var window in _opened.ToArray().Reverse())
                if (window.IsVisible && window != Controller) window.Close();
            ClockOverlayChecks.Pump(10);
            Controller.Close();
            // Preview controllers skip live-service disposal; detach after child dialogs can restore shortcuts.
            _hook.HotKeyPressed -= typeof(ControllerWindow).GetMethod("OnHotKeyPressed", Private)!.CreateDelegate<EventHandler<ShortcutAction>>(Controller);
            _hook.CommandKeyPressed -= typeof(ControllerWindow).GetMethod("OnCommandKeyPressed", Private)!.CreateDelegate<EventHandler<uint>>(Controller);
            _openedHandler.Dispose();
            if (Directory.Exists(_dataDirectory)) Directory.Delete(_dataDirectory, recursive: true);
        }
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    internal static void Timers()
    {
        var errors = new List<string>();
        void Check(string name, Action<Fixture> test)
        {
            using var fixture = new Fixture();
            try { test(fixture); Console.WriteLine("  PASS " + name); }
            catch (Exception exception) { errors.Add(name + " — " + exception.GetBaseException().Message); }
        }
        Check("K opens a picker and digit selects directly", fixture =>
        {
            var active = fixture.Timers.Active;
            fixture.Command(KeyCode.VcK);
            var picker = fixture.Window("TimerPickerWindow");
            Require(fixture.Timers.Active == active, "Opening the picker changed the active timer.");
            picker.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.D1 });
            ClockOverlayChecks.Pump(35);
            Require(fixture.Timers.Active == fixture.Timers.Sessions[0] && !picker.IsVisible, "Digit did not select the first clock.");
        });
        Check("H shows and hides the running-clock shelf", fixture =>
        {
            var active = fixture.Timers.Active!;
            fixture.Command(KeyCode.VcSpace);
            fixture.Command(KeyCode.VcH);
            var shelf = fixture.Window("ActiveClocksShelfWindow");
            shelf.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Pause"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(!active.IsRunning, "Shelf pause did not pause the selected row.");
            fixture.Command(KeyCode.VcH);
            Require(!shelf.IsVisible, "H did not toggle the shelf off.");
        });
        Check("N opens the project chooser and cancel preserves timers", fixture =>
        {
            int count = fixture.Timers.Count;
            fixture.Command(KeyCode.VcN);
            fixture.Window("TimerNameWindow").Close();
            ClockOverlayChecks.Pump(30);
            Require(fixture.Timers.Count == count, "Cancel created a timer.");
            fixture.Command(KeyCode.VcN);
            fixture.Window("TimerNameWindow").FindControl<Button>("SaveButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ClockOverlayChecks.Pump(35);
            Require(fixture.Timers.Count == count + 1 && fixture.Timers.Active == fixture.Timers.Sessions.Last(), "Confirm did not create and select a timer.");
        });
        Check("T includes separated sessions in creation order", fixture =>
        {
            var expected = fixture.Timers.Sessions[3];
            fixture.Timers.Separate(expected);
            fixture.Command(KeyCode.VcT);
            Require(fixture.Timers.Active == expected, "T skipped a separated timer.");
        });
        Check("P works with a hidden controller", fixture =>
        {
            fixture.Command(KeyCode.VcP);
            Require(fixture.Window("TimerNameWindow").IsVisible && !fixture.Controller.IsVisible, "Project chooser required the controller.");
        });
        Check("X confirms closing a timer from the tray", fixture =>
        {
            int count = fixture.Timers.Count;
            fixture.Command(KeyCode.VcX);
            fixture.Window("ConfirmationDialogWindow").FindControl<Button>("ConfirmButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ClockOverlayChecks.Pump(35);
            Require(fixture.Timers.Count == count - 1, "Confirm did not close the timer.");
        });
        Check("A opens the add-record editor", fixture =>
        {
            fixture.Command(KeyCode.VcA);
            var editor = fixture.Window("ProjectRecordEditorWindow");
            editor.FindControl<TextBox>("NewProjectBox")!.Text = "Shortcut verification";
            editor.FindControl<Button>("SaveButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ClockOverlayChecks.Pump(35);
            Require(fixture.HistoryStore.TryLoad(out var history) && history != null &&
                history.CreateView(DateTime.UtcNow).Intervals.Any(interval => interval.ProjectName == "Shortcut verification" && interval.EndUtc != null),
                "A did not save a completed record to the fixture's history.");
        });
        Check("V opens periodic review", fixture =>
        {
            fixture.Command(KeyCode.VcV);
            fixture.Window("PeriodicReviewWindow");
        });
        Check("Space, L, R and C target the active timer", fixture =>
        {
            var active = fixture.Timers.Active!;
            var other = fixture.Timers.Sessions[0];
            fixture.Command(KeyCode.VcSpace);
            Require(active.IsRunning && !other.IsRunning, "Space did not start only the active timer.");
            int laps = active.LapTimes.Count;
            fixture.Command(KeyCode.VcL);
            Require(active.LapTimes.Count == laps + 1, "L did not record a lap.");
            fixture.Command(KeyCode.VcSpace);
            Require(!active.IsRunning, "Space did not pause the timer.");
            fixture.Command(KeyCode.VcR);
            Require(active.Elapsed == TimeSpan.Zero && active.LapTimes.Count == 0, "R did not reset time and laps.");
            active.Mode = 3;
            fixture.Command(KeyCode.VcC);
            Require(active.Mode == 1, "C did not switch to Clock.");
            fixture.Command(KeyCode.VcSpace);
            Require(!active.IsRunning, "Space started a real-time clock.");
            fixture.Command(KeyCode.VcC);
            Require(active.Mode == 3, "C did not restore Timecode mode.");
        });
        Check("B, J, M, O and the dedicated show shortcut route the correct overlay", fixture =>
        {
            var active = fixture.Timers.Active!;
            fixture.Command(KeyCode.VcB);
            Require(active.IsSeparated, "B did not separate the timer.");
            var overlays = (Dictionary<Guid, OverlayWindow>)typeof(ControllerWindow).GetField("_overlays", Private)!.GetValue(fixture.Controller)!;
            var separate = overlays[active.Id];
            var herd = (OverlayWindow)typeof(ControllerWindow).GetField("_herdOverlay", Private)!.GetValue(fixture.Controller)!;
            fixture.Command(KeyCode.VcO);
            Require(!separate.IsVisible && herd.IsVisible, "O hid more than the active overlay.");
            fixture.Call("ExitShortcutCommandMode");
            fixture.Key(KeyCode.VcF7, ModifierMask.LeftMeta | ModifierMask.LeftShift);
            ClockOverlayChecks.Pump(35);
            Require(separate.IsVisible && herd.IsVisible && !active.IsRunning, "Dedicated show did not reveal the active overlay without starting it.");
            fixture.Command(KeyCode.VcJ);
            Require(fixture.Timers.Active != active && !fixture.Timers.Active!.IsSeparated, "J did not move to the herd.");
            fixture.Command(KeyCode.VcJ);
            Require(fixture.Timers.Active == active, "J did not return to the separated timer.");
            fixture.Command(KeyCode.VcM);
            Require(!active.IsSeparated && !overlays.ContainsKey(active.Id), "M did not merge the timer.");
        });
        Check("W and dedicated controller shortcut restore the controller; D opens the dashboard", fixture =>
        {
            fixture.Command(KeyCode.VcD);
            fixture.Window("ProjectDashboardWindow").Close();
            fixture.Command(KeyCode.VcW);
            Require(fixture.Controller.IsVisible, "W did not show the controller.");
            fixture.Controller.WindowState = WindowState.Minimized;
            fixture.Key(KeyCode.VcF2, ModifierMask.LeftMeta | ModifierMask.LeftShift);
            ClockOverlayChecks.Pump(35);
            Require(fixture.Controller.WindowState != WindowState.Minimized, "Direct controller shortcut did not restore it.");
        });
        Check("E edits the timer and U restores it", fixture =>
        {
            var active = fixture.Timers.Active!;
            active.RestoreElapsed(TimeSpan.FromMinutes(5), false);
            fixture.Command(KeyCode.VcE);
            var editor = fixture.Window("TimerEditorWindow");
            editor.FindControl<TextBox>("TimeValueBox")!.Text = "07:30";
            ClockOverlayChecks.Pump(35);
            editor.FindControl<Button>("SaveButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ClockOverlayChecks.Pump(35);
            Require(active.Elapsed == TimeSpan.FromMinutes(7.5), "E did not commit the edited value.");
            fixture.Command(KeyCode.VcU);
            Require(active.Elapsed == TimeSpan.FromMinutes(5), "U did not restore the pre-edit value.");
        });
        Check("S routes to sync and explains disabled integration", fixture =>
        {
            fixture.Command(KeyCode.VcS);
            Require(fixture.Controller.FindControl<TextBlock>("StatusText")!.Text?.Contains("ActivityWatch is disabled") == true, "S did not reach the ActivityWatch handler.");
        });
        Check("Custom timer bindings update the command hint and route numpad input", fixture =>
        {
            fixture.Settings.CommandModeKeys[ShortcutAction.ShowAllClocks] = 0x31;
            fixture.Key(KeyCode.VcF2, ModifierMask.LeftMeta);
            ClockOverlayChecks.Pump(35);
            var hint = fixture.Window("ShortcutCommandHintWindow");
            Require(hint.FindControl<TextBlock>("TimerCommandsThird")!.Text?.Contains("1 Clocks") == true, "Hint showed a stale picker binding.");
            fixture.Key(KeyCode.VcNumPad1);
            ClockOverlayChecks.Pump(35);
            fixture.Window("TimerPickerWindow");
        });
        Check("Leader refresh, chaining, Escape and timeout", fixture =>
        {
            var active = fixture.Timers.Active!;
            fixture.Key(KeyCode.VcF2, ModifierMask.LeftMeta);
            fixture.Key(KeyCode.VcF2, ModifierMask.LeftMeta);
            fixture.Key(KeyCode.VcSpace);
            fixture.Key(KeyCode.VcL);
            ClockOverlayChecks.Pump(35);
            Require(active.IsRunning && active.LapTimes.Count == 4, "Leader refresh or chaining lost a command.");
            fixture.Key(KeyCode.VcEscape);
            ClockOverlayChecks.Pump(35);
            Require(!PlatformServices.HotKey.IsInCommandMode, "Escape left timer capture active.");
            fixture.Command(KeyCode.VcL);
            ClockOverlayChecks.Pump(620);
            Require(!PlatformServices.HotKey.IsInCommandMode, "Chaining timeout did not end capture.");
        });
        Check("Shortcut editor suspends capture and rejects duplicate global bindings", fixture =>
        {
            fixture.Controller.Show();
            fixture.Call("ShortcutsButton_Click", null, new RoutedEventArgs());
            ClockOverlayChecks.Pump(35);
            var editor = fixture.Window("ShortcutsWindow");
            fixture.Key(KeyCode.VcF2, ModifierMask.LeftMeta);
            ClockOverlayChecks.Pump(35);
            Require(!PlatformServices.HotKey.IsInCommandMode, "Global leader intercepted editing.");
            var noteBox = editor.FindControl<TextBox>("NoteLeaderShortcutBox")!;
            typeof(ShortcutsWindow).GetMethod("ShortcutBox_KeyDown", Private)!.Invoke(editor, new object[]
            {
                noteBox, new KeyEventArgs { Key = Avalonia.Input.Key.F2, KeyModifiers = KeyModifiers.Meta }
            });
            editor.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Save"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(editor.IsVisible && editor.FindControl<TextBlock>("PortalRegistrationText")!.Text?.Contains("unique combination") == true,
                "Editor accepted a duplicate global shortcut.");
            editor.Close();
            ClockOverlayChecks.Pump(35);
            fixture.Command(KeyCode.VcK);
            fixture.Window("TimerPickerWindow");
        });
        Require(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    internal static void Notes()
    {
        using var fixture = new Fixture();
        foreach (KeyCode key in new[] { KeyCode.VcT, KeyCode.VcN, KeyCode.VcR })
        {
            fixture.Command(key, notes: true);
            fixture.Window("NoteEntryWindow").Close();
        }
        fixture.Command(KeyCode.VcV, notes: true);
        fixture.Window("NotesViewerWindow").Close();
        fixture.Command(KeyCode.VcNumPad2, notes: true);
        fixture.Window("NoteEntryWindow").Close();
        fixture.Command(KeyCode.VcO, notes: true);
        var todo = (TodoOverlayWindow)fixture.Window("TodoOverlayWindow");
        fixture.Command(KeyCode.VcO, notes: true);
        Require(!todo.IsVisible, "O did not hide the Todo overlay.");
        fixture.Command(KeyCode.VcSpace, notes: true);
        Require(todo.IsVisible && todo.IsExpanded, "Space did not show and expand Todos.");
        fixture.Command(KeyCode.VcSpace, notes: true);
        Require(!todo.IsExpanded, "Space did not collapse Todos.");
        fixture.Settings.NoteCommandModeKeys[NoteCommandAction.AddNote] = 0;
        fixture.Command(KeyCode.VcN, notes: true);
        Require(!PlatformServices.HotKey.IsInCommandMode, "An unbound note key left command capture active.");
        fixture.Command(KeyCode.VcEscape, notes: true);
        Require(!PlatformServices.HotKey.IsInCommandMode, "Escape left note command capture active.");
    }
}
