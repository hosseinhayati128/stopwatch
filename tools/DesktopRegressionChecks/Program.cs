using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Avalonia.LogicalTree;
using Avalonia.Input;
using Avalonia.Controls.Primitives;
using Avalonia.Rendering;
using SharpHook;
using SharpHook.Native;
using StopwatchOverlay;
using StopwatchOverlay.Desktop;
using StopwatchOverlay.Desktop.Platform;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Platform;
using StopwatchOverlay.Desktop.Views;

const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
int failures = 0;
void Check(string name, Action test)
{
    try { test(); Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failures++; Console.WriteLine("FAIL " + name + ": " + ex.GetBaseException().Message); if (args.Contains("--todo-appearance-only")) Console.WriteLine(ex); }
}
void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
SharpHookHotKeyService Service() => (SharpHookHotKeyService)Activator.CreateInstance(
    typeof(SharpHookHotKeyService), Private, null, new object[] { false }, null)!;
KeyboardHookEventArgs Key(KeyCode key, ModifierMask mask = ModifierMask.None) => new(new UioHookEvent {
    Type = EventType.KeyPressed, Mask = mask, Keyboard = new KeyboardEventData { KeyCode = key }
});
void Press(SharpHookHotKeyService service, KeyboardHookEventArgs key) =>
    typeof(SharpHookHotKeyService).GetMethod("OnKeyPressed", Private)!.Invoke(service, new object?[] { null, key });
void Release(SharpHookHotKeyService service, KeyCode key) =>
    typeof(SharpHookHotKeyService).GetMethod("OnKeyReleased", Private)?.Invoke(service, new object?[] { null, Key(key) });

// Tests feed hook events directly. They never generate OS keyboard input.
PlatformServices.HotKey.Dispose();
Check("left and right modifiers each register shortcuts", () => {
    foreach (var (mask, mods) in new[] { (ModifierMask.LeftMeta, Shortcut.MOD_WIN), (ModifierMask.RightMeta, Shortcut.MOD_WIN),
        (ModifierMask.LeftCtrl, Shortcut.MOD_CONTROL), (ModifierMask.RightAlt, Shortcut.MOD_ALT), (ModifierMask.LeftShift, Shortcut.MOD_SHIFT) })
    {
        using var service = Service(); int count = 0;
        service.RegisterHotKey(ShortcutAction.CommandLeader, mods, 0x71);
        service.HotKeyPressed += (_, _) => count++;
        Press(service, Key(KeyCode.VcF2, mask));
        Require(count == 1, $"{mask} did not match its modifier.");
    }
});
Check("held leader and command keys run once until release", () => {
    using var service = Service(); int leaders = 0, commands = 0;
    service.RegisterHotKey(ShortcutAction.CommandLeader, Shortcut.MOD_WIN, 0x71);
    service.HotKeyPressed += (_, _) => { leaders++; service.IsInCommandMode = true; };
    service.CommandKeyPressed += (_, _) => commands++;
    Press(service, Key(KeyCode.VcF2, ModifierMask.Meta)); Press(service, Key(KeyCode.VcF2, ModifierMask.Meta));
    Require(leaders == 1, "Holding the leader toggled command mode repeatedly.");
    Press(service, Key(KeyCode.VcSpace)); Press(service, Key(KeyCode.VcSpace));
    Require(commands == 1, "Holding Space repeatedly started/paused the timer.");
    Release(service, KeyCode.VcSpace); Press(service, Key(KeyCode.VcSpace));
    Require(commands == 2, "A new command press after release was lost.");
});
Check("fast command is captured before the UI handles its leader", () => {
    using var service = Service(); int commands = 0;
    service.RegisterHotKey(ShortcutAction.NoteCommandLeader, Shortcut.MOD_WIN, 0x72);
    service.CommandKeyPressed += (_, _) => commands++;
    Press(service, Key(KeyCode.VcF3, ModifierMask.Meta)); Press(service, Key(KeyCode.VcO));
    Require(commands == 1, "Fast O disappeared while the UI thread was busy.");
});
Check("shortcut editor navigation keys are recognized by the hook", () => {
    using var service = Service(); var received = new List<uint>(); service.IsInCommandMode = true;
    service.CommandKeyPressed += (_, key) => received.Add(key);
    Press(service, Key(KeyCode.VcLeft)); Press(service, Key(KeyCode.VcDelete));
    Require(received.SequenceEqual(new uint[] { 0x25, 0x2E }), "Accepted shortcut keys could not be triggered.");
});
Check("only command keys are consumed on Windows", () => {
    using var service = Service();
    var normal = Key(KeyCode.VcN); Press(service, normal);
    Require(!normal.SuppressEvent, "Normal typing was swallowed."); Release(service, KeyCode.VcN);
    service.IsInCommandMode = true;
    var command = Key(KeyCode.VcN); Press(service, command);
    Require(command.SuppressEvent == OperatingSystem.IsWindows(), "Command suppression changed for the wrong platform.");
    var repeat = Key(KeyCode.VcN); Press(service, repeat);
    Require(repeat.SuppressEvent == OperatingSystem.IsWindows(), "A held command leaked into the foreground app.");
});

if (args.Contains("--todo-settings-only"))
{
    Check("Todo appearance compatibility and independent persistence", TodoAppearanceChecks.Settings);
    Environment.Exit(failures == 0 ? 0 : 1);
}

AppBuilder.Configure<App>().UsePlatformDetect()
    .With(new Win32PlatformOptions { RenderingMode = new[] { Win32RenderingMode.Software } })
    .With(new X11PlatformOptions { RenderingMode = new[] { X11RenderingMode.Software } })
    .WithInterFont().SetupWithoutStarting();
if (args.Contains("--nika-only"))
{
    Check("Nika scoped controller, resources and theme restoration", NikaThemeChecks.Run);
    Check("controller mode selections survive refresh", ControllerModeChecks.Run);
    Environment.Exit(failures == 0 ? 0 : 1);
}
if (args.Contains("--todo-appearance-only"))
{
    Check("Todo appearance settings", TodoAppearanceChecks.Settings);
    Check("Todo appearance native overlay", TodoAppearanceChecks.Overlay);
    Check("Todo appearance inspector and dialogs", TodoAppearanceChecks.InspectorAndDialogs);
    Check("Todo appearance actual-size controls and isolated fixtures", TodoAppearanceUiChecks.Run);
    Environment.Exit(failures == 0 ? 0 : 1);
}
if (args.Contains("--todo-reorder-only"))
{
    Check("Manual Todo card dragging and stationary edge scrolling", TodoReorderChecks.Run);
    Environment.Exit(failures == 0 ? 0 : 1);
}
if (args.Contains("--todo-mural-only"))
{
    Check("Todo artwork settings compatibility", TodoAppearanceChecks.Settings);
    Check("Todo sidebar artwork layers", () => TodoMuralChecks.Run());
    Environment.Exit(failures == 0 ? 0 : 1);
}
if (args.Contains("--todo-mural-audit"))
{
    Check("Todo mural visual audit", TodoMuralChecks.CaptureAudit);
    Environment.Exit(failures == 0 ? 0 : 1);
}
if (args.Contains("--todo-mural-dropdown-only"))
{
    Check("Todo artwork dropdown recycling", () => TodoMuralChecks.Run(dropdownOnly:true));
    Environment.Exit(failures == 0 ? 0 : 1);
}
if (args.Contains("--controller-modes-only"))
{
    Check("controller mode selections survive refresh", ControllerModeChecks.Run);
    Environment.Exit(failures == 0 ? 0 : 1);
}
if (args.Contains("--bidi-only"))
{
    Check("mixed Persian-English task text", BidiTextChecks.Run);
    Environment.Exit(failures == 0 ? 0 : 1);
}
if (args.Contains("--timer-editor-only"))
{
    Check("timer editor opens with a hidden controller", TimerEditorChecks.HiddenOwner);
    Environment.Exit(failures == 0 ? 0 : 1);
}
if (args.Contains("--todo-add-only"))
{
    Check("todo add icon and label are centered", ThemeAndBoundsChecks.TodoAddAlignment);
    Environment.Exit(failures == 0 ? 0 : 1);
}
Check("clock toolbar has only Close, Start/Pause and Reset", () => {
    var overlay = new OverlayWindow();
    overlay.ApplyTheme(OverlayThemeCatalog.Pirate, AppThemeCatalog.Pirate);
    var toolbar = overlay.FindControl<Border>("ActionSurface")!;
    var actions = toolbar.GetLogicalDescendants().OfType<Button>().Where(button => button.IsVisible).Select(button => button.Name).ToArray();
    Require(actions.SequenceEqual(new[] { "CloseActionButton", "PauseResumeActionButton", "ResetActionButton" }), "Clock toolbar still has extra actions.");
    var panel = toolbar.GetVisualDescendants().OfType<NavigatorToolbarPanel>().Single();
    panel.Measure(new Size(520, 250)); panel.Arrange(new Rect(0, 0, 520, panel.DesiredSize.Height));
    Require(Math.Abs(panel.Children[0].Bounds.Width - 520 * .24) < 1, "Three dials were shrunk to fit hidden actions.");
    try
    {
        overlay.Show(); overlay.FindControl<Popup>("ActionPopup")!.IsOpen = true;
        ClockOverlayChecks.Pump(150);
        var start = overlay.FindControl<Button>("PauseResumeActionButton")!;
        // SetupWithoutStarting has no application lifetime driving native paint.
        var renderRoot = (IRenderRoot)start.GetVisualRoot()!;
        typeof(IRenderer).GetMethod("Paint", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .Invoke(renderRoot.Renderer, new object[] { new Rect(renderRoot.ClientSize) });
        var hit = start.InputHitTest(new Point(start.Bounds.Width / 2, start.Bounds.Height / 2));
        Require(hit is Visual visual && (ReferenceEquals(hit, start) || visual.GetVisualAncestors().Contains(start)), $"Navigator dial lost its clickable surface: bounds={start.Bounds}, visible={start.IsEffectivelyVisible}, enabled={start.IsEffectivelyEnabled}, hit={hit?.GetType().Name ?? "null"}, popup={overlay.FindControl<Popup>("ActionPopup")!.IsOpen}.");
        overlay.SetRunning(true); start.ApplyTemplate();
        var artwork = start.GetVisualDescendants().OfType<NavigatorDialArtwork>().Single();
        Require(artwork.Action == "Pause", "Running timer did not update the engraved pause symbol.");
        overlay.SetRunning(false);
        Require(artwork.Action == "Play", "Paused timer did not restore the engraved play symbol.");
    }
    finally { overlay.Close(); }
});
Check("clock stays topmost after show, hide and native demotion without taking focus", ClockOverlayChecks.Run);
Check("clock and both notes overlays stay topmost together without stealing focus", ClockOverlayChecks.RunTogether);
Check("close-to-tray dialog uses the One Piece buttons", ThemeAndBoundsChecks.CloseDialogTheme);
Check("clock cannot remain outside the left or right screen edge", ThemeAndBoundsChecks.ClockBounds);
Check("overlay bounds preserve negative monitors and mixed DPI", ThemeAndBoundsChecks.MonitorBounds);
Check("mixed Persian-English task text", BidiTextChecks.Run);
Check("timer editor opens with a hidden controller", TimerEditorChecks.HiddenOwner);
Check("todo add icon and label are centered", ThemeAndBoundsChecks.TodoAddAlignment);
Check("narrow controller gives action labels a full row", () => {
    var panel = new PirateActionPanel { Pirate = true };
    for (int i = 0; i < 4; i++) panel.Children.Add(new Button { Content = "Hide overlay" });
    panel.Measure(new Size(320, 1000)); panel.Arrange(new Rect(0, 0, 320, panel.DesiredSize.Height));
    Require(panel.Children.All(child => child.Bounds.Width >= 300), "Narrow action buttons squeezed labels into half-width columns.");
});
Check("themed button text stays readable in normal hover and pressed states", () => {
    var apply = typeof(App).Assembly.GetType("StopwatchOverlay.Desktop.ThemeResources")!.GetMethod("ApplyApplication", BindingFlags.Static | BindingFlags.NonPublic)!;
    foreach (var theme in new[] { AppThemeCatalog.Pirate, AppThemeCatalog.Midnight, AppThemeCatalog.Daylight })
    {
        apply.Invoke(null, new object[] { theme });
        foreach (bool pirateTemplate in theme == AppThemeCatalog.Pirate ? new[] { false, true } : new[] { false })
        foreach (string kind in new[] { "modern", "primary", "danger" })
        {
            var button = new Button { Content = "Start / Pause", Classes = { kind } };
            var host = new Window { Content = button };
            if (pirateTemplate)
            {
                host.Classes.Add("pirate");
                host.Styles.Add(new StyleInclude(new Uri("avares://StopwatchOverlay.Desktop/")) {
                    Source = new Uri("avares://StopwatchOverlay.Desktop/Styles/PirateControllerStyles.axaml")
                });
            }
            host.ApplyTemplate(); button.ApplyTemplate();
            button.Measure(new Size(300, 60)); button.Arrange(new Rect(0, 0, 300, 60));
            var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().First();
            string color = button.Foreground!.ToString()!;
            foreach (string state in new[] { ":pointerover", ":pressed" })
            {
                ((IPseudoClasses)button.Classes).Set(state, true);
                Require(presenter.Foreground?.ToString() == color, $"{theme} {kind} custom={pirateTemplate} {state} changed text contrast.");
                ((IPseudoClasses)button.Classes).Set(state, false);
            }
        }
    }
});
PlatformServices.HotKey.Dispose();
Environment.Exit(failures == 0 ? 0 : 1);
