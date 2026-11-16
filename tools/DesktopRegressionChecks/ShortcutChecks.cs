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
            KeyEvent(code, true, modifiers);
            KeyEvent(code, false, modifiers);
        }

        internal void KeyEvent(KeyCode code, bool pressed, ModifierMask modifiers = ModifierMask.None)
        {
            var args = new KeyboardHookEventArgs(new UioHookEvent
            {
                Type = pressed ? EventType.KeyPressed : EventType.KeyReleased, Mask = modifiers,
                Keyboard = new KeyboardEventData { KeyCode = code }
            });
            typeof(SharpHookHotKeyService).GetMethod(pressed ? "OnKeyPressed" : "OnKeyReleased", Private)!
                .Invoke(_hook, new object?[] { null, args });
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
            var pickerRow = hint.GetVisualDescendants().OfType<Grid>().Single(row => row.Name == "TimerCommandRowShowAllClocks");
            Require(pickerRow.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "1"), "Hint showed a stale picker binding.");
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

    internal static void TimerCommandsPopup()
    {
        using var fixture = new Fixture();
        fixture.Settings.ThemeMode = AppThemeCatalog.Nika;
        fixture.KeyEvent(KeyCode.VcF2, true, ModifierMask.LeftMeta);
        fixture.KeyEvent(KeyCode.VcF2, true, ModifierMask.LeftMeta);
        fixture.KeyEvent(KeyCode.VcF2, false, ModifierMask.LeftMeta);
        ClockOverlayChecks.Pump(60);
        var hint = fixture.Window("ShortcutCommandHintWindow");
        string[] Actions() => hint.GetVisualDescendants().OfType<Grid>()
            .Where(row => row.Name?.StartsWith("TimerCommandRow") == true).Select(row => row.Name!).ToArray();
        Require(Actions().SequenceEqual(new[] { "StartStop", "Reset", "ToggleClock", "ToggleOverlay", "NextTimer", "ShowAllClocks" }
            .Select(name => "TimerCommandRow" + name)), "Quick hint must show only Space, R, C, O, T and K in order.");
        var top = hint.Position.Y;
        fixture.Key(KeyCode.VcF2);
        ClockOverlayChecks.Pump(60);
        Require(hint.IsVisible && Actions().Length == 6, "Second F2 must keep the compact hint.");
        fixture.Key(KeyCode.VcF2);
        ClockOverlayChecks.Pump(35);
        Require(hint.IsVisible && Actions().Length == 6, "Third F2 must keep the compact hint.");
        fixture.Key(KeyCode.VcF2);
        ClockOverlayChecks.Pump(35);
        Require(hint.IsVisible && Actions().Length == 21, "Fourth F2 did not expand the same hint.");
        Require(hint.Position.Y == top, "Expansion moved the top edge.");
        fixture.Key(KeyCode.VcF2, ModifierMask.LeftMeta);
        ClockOverlayChecks.Pump(35);
        Require(Actions().Length == 21, "Further leader presses must keep the full list open.");
        var timer = (Avalonia.Threading.DispatcherTimer)typeof(ControllerWindow).GetField("_commandModeTimer", Private)!.GetValue(fixture.Controller)!;
        Require(timer.Interval == TimeSpan.FromSeconds(5), "Expanded list needs the five-second reading timeout.");
        fixture.Key(KeyCode.VcEscape);
        ClockOverlayChecks.Pump(35);
        Require(!hint.IsVisible && !PlatformServices.HotKey.IsInCommandMode, "Escape did not dismiss expanded mode.");
        fixture.Key(KeyCode.VcF2, ModifierMask.LeftMeta);
        ClockOverlayChecks.Pump(35);
        hint = fixture.Window("ShortcutCommandHintWindow");
        Require(Actions().Length == 6, "Reopening must start compact.");
        fixture.Key(KeyCode.VcL);
        ClockOverlayChecks.Pump(35);
        Require(fixture.Timers.Active!.LapTimes.Count == 4, "An action omitted from the quick hint stopped working.");
        Require(!hint.IsVisible, "Executing an omitted command left the popup visible.");

        fixture.Call("ExitShortcutCommandMode");
        fixture.Settings.CommandModeKeys[ShortcutAction.StartStop] = 0x71;
        fixture.Settings.CommandModeKeys[ShortcutAction.Reset] = 0;
        fixture.Key(KeyCode.VcF2, ModifierMask.LeftMeta);
        ClockOverlayChecks.Pump(35);
        hint = fixture.Window("ShortcutCommandHintWindow");
        Require(Actions().Length == 5, "Unassigned quick actions should be omitted.");
        Require(hint.GetVisualDescendants().OfType<TextBlock>().Count(t => t.Text == "Win + F2") == 2,
            "An assigned F2 needs a configured-leader expansion hint.");
        fixture.Key(KeyCode.VcF2);
        ClockOverlayChecks.Pump(35);
        Require(fixture.Timers.Active!.IsRunning && !hint.IsVisible, "Custom F2 must run its command and dismiss the popup.");
        fixture.Call("ExitShortcutCommandMode");
        fixture.Settings.CommandModeKeys[ShortcutAction.StartStop] = 0x20;
        fixture.Settings.LeaderShortcut = new Shortcut(Shortcut.MOD_CONTROL, 0x77);
        fixture.Call("InitializePlatformServices");
        fixture.Key(KeyCode.VcF8, ModifierMask.LeftCtrl);
        ClockOverlayChecks.Pump(35);
        hint = fixture.Window("ShortcutCommandHintWindow");
        Require(Actions().Length == 5 && timer.Interval == TimeSpan.FromSeconds(2), "Custom leader did not open a fresh quick view.");
        fixture.Key(KeyCode.VcF8, ModifierMask.LeftCtrl);
        ClockOverlayChecks.Pump(35);
        Require(Actions().Length == 5 && timer.Interval == TimeSpan.FromSeconds(2), "Second custom leader must keep the compact view and refresh its timeout.");
        fixture.Key(KeyCode.VcF8, ModifierMask.LeftCtrl);
        ClockOverlayChecks.Pump(35);
        Require(Actions().Length == 5, "Third custom leader must keep the compact view.");
        fixture.Key(KeyCode.VcF8, ModifierMask.LeftCtrl);
        ClockOverlayChecks.Pump(35);
        Require(Actions().Length == 20, "Fourth custom leader did not expand.");
        ClockOverlayChecks.Pump(2100);
        Require(hint.IsVisible, "Expanded view retained the short quick timeout.");
        ClockOverlayChecks.Pump(3100);
        Require(!hint.IsVisible && !PlatformServices.HotKey.IsInCommandMode, "Expanded timeout did not release command capture.");
        fixture.Key(KeyCode.VcF8, ModifierMask.LeftCtrl);
        ClockOverlayChecks.Pump(35);
        hint = fixture.Window("ShortcutCommandHintWindow");
        fixture.Key(KeyCode.VcT);
        ClockOverlayChecks.Pump(35);
        Require(!hint.IsVisible, "Next timer did not dismiss the popup.");
        fixture.Key(KeyCode.VcF2);
        ClockOverlayChecks.Pump(35);
        Require(hint.IsVisible && Actions().Length == 5, "Second F2 did not restore the compact hint during chaining.");
        fixture.Key(KeyCode.VcF2);
        ClockOverlayChecks.Pump(35);
        Require(Actions().Length == 5, "Third F2 expanded too early after reopening during chaining.");
        fixture.Key(KeyCode.VcF2);
        ClockOverlayChecks.Pump(35);
        Require(Actions().Length == 20, "Fourth F2 did not expand after reopening during chaining.");
        Require(hint.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == fixture.Timers.Active!.DisplayName + " · Esc cancels"),
            "Next timer left stale active context in the hint.");
        fixture.Key(KeyCode.VcF3, ModifierMask.LeftMeta);
        ClockOverlayChecks.Pump(35);
        var noteHint = fixture.Window("NoteCommandHintWindow");
        fixture.Key(KeyCode.VcF2);
        ClockOverlayChecks.Pump(35);
        Require(!noteHint.IsVisible && !PlatformServices.HotKey.IsInCommandMode, "Timer expansion intercepted note mode.");
        fixture.Key(KeyCode.VcF8, ModifierMask.LeftCtrl);
        ClockOverlayChecks.Pump(2100);
        Require(!PlatformServices.HotKey.IsInCommandMode, "Quick timeout did not release command capture.");
        Console.WriteLine("PASS quick actions, held keys, F2 expansion, custom leader/F2, actual timeouts, Escape, chaining, active context and note isolation");
    }

    internal static void TimerCommandsDismissal()
    {
        foreach (bool expanded in new[] { false, true })
        foreach (var key in new[] { KeyCode.VcSpace, KeyCode.VcR, KeyCode.VcC, KeyCode.VcE, KeyCode.VcO, KeyCode.VcT, KeyCode.VcK })
        {
            using var fixture = new Fixture();
            fixture.Key(KeyCode.VcF2, ModifierMask.LeftMeta);
            ClockOverlayChecks.Pump(35);
            var hint = fixture.Window("ShortcutCommandHintWindow");
            if (expanded) { fixture.Key(KeyCode.VcF2); fixture.Key(KeyCode.VcF2); fixture.Key(KeyCode.VcF2); ClockOverlayChecks.Pump(35); }
            fixture.Key(key);
            ClockOverlayChecks.Pump(35);
            Require(!hint.IsVisible, $"{key} left the {(expanded ? "expanded" : "compact")} popup visible.");
            if (key is KeyCode.VcE or KeyCode.VcK)
            {
                fixture.Window(key == KeyCode.VcE ? "TimerEditorWindow" : "TimerPickerWindow");
                Require(!PlatformServices.HotKey.IsInCommandMode, "Opening a panel kept command capture active.");
            }
            else Require(PlatformServices.HotKey.IsInCommandMode, "Hiding the popup disabled shortcut chaining.");
            if (key == KeyCode.VcC)
            {
                Require(fixture.Timers.Active!.Mode == 1, "C did not switch to clock mode.");
                fixture.Key(KeyCode.VcC); ClockOverlayChecks.Pump(35);
                Require(fixture.Timers.Active.Mode == 0 && !hint.IsVisible, "C did not switch back to stopwatch mode with the popup hidden.");
            }
            if (key == KeyCode.VcSpace)
            {
                Require(fixture.Timers.Active!.IsRunning, "Space did not start the timer.");
                fixture.Key(KeyCode.VcSpace); ClockOverlayChecks.Pump(35);
                Require(!fixture.Timers.Active.IsRunning && !hint.IsVisible, "Hidden chaining did not pause the timer.");
                fixture.Key(KeyCode.VcF2, ModifierMask.LeftMeta); ClockOverlayChecks.Pump(35);
                if (!expanded)
                {
                    Require(hint.IsVisible && hint.GetVisualDescendants().OfType<Grid>().Count(row => row.Name?.StartsWith("TimerCommandRow") == true) == 6,
                        "Second leader must reopen compact during chaining.");
                    fixture.Key(KeyCode.VcF2, ModifierMask.LeftMeta); ClockOverlayChecks.Pump(35);
                    fixture.Key(KeyCode.VcF2, ModifierMask.LeftMeta); ClockOverlayChecks.Pump(35);
                }
                Require(hint.IsVisible && hint.GetVisualDescendants().OfType<Grid>().Count(row => row.Name?.StartsWith("TimerCommandRow") == true) == 21,
                    "Repeated leader did not reopen the full hint during chaining.");
                fixture.Key(KeyCode.VcSpace); ClockOverlayChecks.Pump(35);
                Require(!hint.IsVisible, "Reopened full hint did not disappear after a command.");
            }
        }
        Console.WriteLine("PASS Space/R/C/O/T/K and E dismiss hints, C toggles clock/stopwatch, hidden chaining and four-press reopening");
    }

    internal static void TimerCommandsFourthPress()
    {
        using var fixture = new Fixture();
        fixture.Key(KeyCode.VcF2, ModifierMask.LeftMeta);
        ClockOverlayChecks.Pump(35);
        var hint = fixture.Window("ShortcutCommandHintWindow");
        int Rows() => hint.GetVisualDescendants().OfType<Grid>().Count(row => row.Name?.StartsWith("TimerCommandRow") == true);
        fixture.KeyEvent(KeyCode.VcF2, true);
        fixture.KeyEvent(KeyCode.VcF2, true);
        ClockOverlayChecks.Pump(35);
        bool stillCompact = Rows() == 6;
        fixture.KeyEvent(KeyCode.VcF2, false);
        Require(stillCompact, "Second press or a held repeat expanded before four deliberate F2 presses.");
        fixture.Key(KeyCode.VcF2);
        ClockOverlayChecks.Pump(35);
        Require(Rows() == 6, "Third F2 press must remain compact.");
        fixture.Key(KeyCode.VcF2);
        ClockOverlayChecks.Pump(35);
        Require(Rows() == 21, "Fourth deliberate F2 press did not expand.");
        fixture.Key(KeyCode.VcEscape);
        fixture.Key(KeyCode.VcF2, ModifierMask.LeftMeta);
        fixture.Key(KeyCode.VcF2);
        fixture.Key(KeyCode.VcF2);
        ClockOverlayChecks.Pump(35);
        hint = fixture.Window("ShortcutCommandHintWindow");
        Require(Rows() == 6, "Expansion press count leaked into the next command session.");
    }

    internal static void TimerCommandsPortalDismissal()
    {
        using var fixture = new Fixture();
        var hook = PlatformServices.HotKey;
        using var portal = new PortalHotKeyService();
        try
        {
            PlatformServices.HotKey = portal;
            fixture.Call("EnterShortcutCommandMode", false);
            ClockOverlayChecks.Pump(35);
            var hint = fixture.Window("ShortcutCommandHintWindow");
            fixture.Call("OnCommandKeyPressed", null, 0x20u);
            ClockOverlayChecks.Pump(35);
            Require(fixture.Timers.Active!.IsRunning && !hint.IsVisible && !portal.IsInCommandMode,
                "Local portal command must execute and release capture when its focused popup disappears.");
        }
        finally
        {
            fixture.Call("ExitShortcutCommandMode");
            PlatformServices.HotKey = hook;
        }
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
