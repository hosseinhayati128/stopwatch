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
    catch (Exception ex) { failures++; Console.WriteLine("FAIL " + name + ": " + ex.GetBaseException().Message); Console.WriteLine(ex); }
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

// This visual spike needs only Avalonia drawing services, without application settings or hooks.
if (args.Contains("--project-chooser-preview") || args.Contains("--timer-editor-states") || args.Contains("--timer-editor-preview") || args.Contains("--running-clocks-state-preview") || args.Contains("--running-clocks-preview") || args.Contains("--clock-picker-state-preview") || args.Contains("--clock-picker-row-preview") || args.Contains("--frame-size-preview") || args.Contains("--frame-modules-preview") || args.Contains("--frame-catalog-audit") || args.Contains("--frame-join-refinement") || args.Contains("--frame-production-audit") || args.Contains("--shared-frames-only"))
{
    AppBuilder.Configure<FrameSizePreviewApp>().UsePlatformDetect()
        .With(new Win32PlatformOptions { RenderingMode = new[] { Win32RenderingMode.Software } })
        .WithInterFont().SetupWithoutStarting();
    if(args.Contains("--project-chooser-preview"))Check("project chooser native design proposals",ProjectChooserPreview.Run);
    else if(args.Contains("--timer-editor-states"))Check("timer editor native state proposals",()=>TimerEditorPreview.Run(true));
    else if(args.Contains("--timer-editor-preview"))Check("timer editor native layout proposal",()=>TimerEditorPreview.Run());
    else if(args.Contains("--running-clocks-state-preview"))Check("running clocks state and format proposals",RunningClocksPreview.RunStates);
    else if(args.Contains("--running-clocks-preview"))Check("running clocks native layout proposal",RunningClocksPreview.Run);
    else if(args.Contains("--clock-picker-state-preview"))Check("clock picker state design proposal",ClockPickerStatePreview.Run);
    else if(args.Contains("--clock-picker-row-preview"))Check("clock picker row design proposals",ClockPickerRowPreview.Run);
    else if(args.Contains("--shared-frames-only"))
    {
        Check("production frames match reviewed artwork",SharedFrameChecks.ReviewedArtwork);
        Check("shared frame masks, padding, small sizes and cache",SharedFrameChecks.Layouts);
        Check("wave mask preserves separate interior spans",SharedFrameChecks.WaveRecesses);
    }
    else if(args.Contains("--frame-production-audit"))Check("production frame resolution audit",FrameCatalogAudit.RunProduction);
    else if(args.Contains("--frame-join-refinement"))Check("refined joins and all sixteen modular frame recipes",FrameCatalogAudit.RunRefinement);
    else if(args.Contains("--frame-catalog-audit"))Check("all sixteen modular frame recipes",FrameCatalogAudit.Run);
    else Check("shared frame at stopwatch, picker and large panel sizes", () => FrameSizePreview.Run(args.Contains("--frame-modules-preview")));
    Environment.Exit(failures == 0 ? 0 : 1);
}

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
    .With(new Win32PlatformOptions { RenderingMode = args.Any(a=>a.StartsWith("--stopwatch-performance-"))
        ? new[] { Win32RenderingMode.AngleEgl,Win32RenderingMode.Software } : new[] { Win32RenderingMode.Software } })
    .With(new X11PlatformOptions { RenderingMode = new[] { X11RenderingMode.Software } })
    .WithInterFont().SetupWithoutStarting();
if(args.Contains("--project-chooser-native"))
{
    Check("project chooser native layout and fixed footer",ProjectChooserNativeChecks.Layout);
    Check("project chooser results and project defaults",ProjectChooserNativeChecks.ResultsAndDefaults);
    Check("project chooser draft validation and cancellation",ProjectChooserNativeChecks.DraftsAndValidation);
    Check("project chooser bounded scores, themes and RTL",ProjectChooserNativeChecks.ScoresAndThemes);
    Check("project chooser real pointer and dropdown keys",ProjectChooserNativeChecks.NativeInput);
    Check("project chooser actual native captures",ProjectChooserNativeChecks.Evidence);
    Environment.Exit(failures==0?0:1);
}
if(args.Contains("--timer-editor-native"))
{
    Check("editor duration/start validation for click and keyboard",TimerEditorNativeChecks.Validation);
    Check("editor duration bounds and extreme elapsed values",TimerEditorNativeChecks.DurationBounds);
    Check("editor shared frame, scrolling and fixed footer",TimerEditorNativeChecks.Layout);
    Check("editor fields preserve save results",TimerEditorNativeChecks.FieldsAndResults);
    Check("editor pointer, keyboard and dropdown input",TimerEditorNativeChecks.InputStates);
    Check("editor pending discard, undo and confirmed deletion",TimerEditorNativeChecks.RecordActions);
    Check("editor themes, modes and closed timer lifecycle",TimerEditorNativeChecks.ThemesModesAndLifecycle);
    Check("editor hidden and visible ownership",TimerEditorChecks.HiddenOwner);
    Check("editor actual native captures",TimerEditorNativeChecks.Evidence);
    Environment.Exit(failures==0?0:1);
}
if(args.Contains("--running-clocks-only"))
{
    Check("running shelf approved layout and timer filtering",RunningClocksChecks.Layout);
    Check("running shelf actions and safe focus after pause",RunningClocksChecks.ActionsAndFocus);
    Check("running shelf keyboard and pointer states",RunningClocksChecks.InputStates);
    Check("running shelf long times, RTL and scrolling",RunningClocksChecks.ReadabilityAndScrolling);
    Check("running shelf stable updates and theme isolation",RunningClocksChecks.StableUpdatesAndThemes);
    Check("running shelf monitor-edge placement",RunningClocksChecks.Placement);
    Check("running shelf actual native captures",RunningClocksChecks.Evidence);
    Environment.Exit(failures==0?0:1);
}
if(args.Contains("--stopwatch-performance-palettes"))
{
    foreach(string background in new[]{"navigator-parchment","sky-island"})
    {
        var settings=new AppSettings{ThemeMode=AppThemeCatalog.Nika,TextSize=27,TextColor="White",TimeFormat=1,
            StopwatchAppearance=new(){Background=background,BackgroundOpacity=70,Typography="navigator-bold"}};
        Check(background+" palette redraw sample",()=>StopwatchPerformanceChecks.Benchmark(background,OverlayLegibilityChecks.Output,settings));
    }
    Environment.Exit(failures==0?0:1);
}
if(args.Contains("--overlay-legibility-baseline")||args.Contains("--overlay-legibility"))
{
    bool baseline=args.Contains("--overlay-legibility-baseline");
    Check("actual backgrounds and toolbar capture",()=>OverlayLegibilityChecks.Capture(baseline?"before":"after"));
    Check("button hit centers match source artwork",OverlayLegibilityChecks.ButtonCenters);
    Check("Nika palette replaces legacy fixed white",OverlayLegibilityChecks.BackgroundColors);
    if(!baseline)
    {
        StopwatchAppearanceChecks.OutputOverride=OverlayLegibilityChecks.Output+"/native";
        Check("live overlay behavior and alpha",StopwatchAppearanceChecks.LiveOverlay);
        Check("layout retained between ticks",StopwatchPerformanceChecks.StableDigitLayout);
        Check("cached toolbar state",StopwatchPerformanceChecks.RepeatedPresentation);
        Check("settings preview and theme isolation",StopwatchAppearanceUiChecks.Run);
    }
    Environment.Exit(failures==0?0:1);
}
if(args.Contains("--toolbar-glyphs-baseline")||args.Contains("--toolbar-glyphs"))
{
    Check("all hover-control symbols at native and double scale",()=>ToolbarGlyphChecks.Capture(args.Contains("--toolbar-glyphs-baseline")?"before":"after"));
    if(!args.Contains("--toolbar-glyphs-baseline"))
    {
        StopwatchAppearanceChecks.OutputOverride=".superpowers/sdd/2026-11-05-hover-symbols/native";
        Check("toolbar actions, disabled clock mode and theme restoration",StopwatchAppearanceChecks.LiveOverlay);
        Check("unchanged presentation retains cached toolbar",StopwatchPerformanceChecks.RepeatedPresentation);
        Check("direct settings retain inactive toolbar opacity",StopwatchPerformanceChecks.DirectSettingsPreserveOpacity);
        Check("appearance inspector state and preview",StopwatchAppearanceUiChecks.Run);
    }
    Environment.Exit(failures==0?0:1);
}
if(args.Contains("--picker-stability-baseline")||args.Contains("--picker-stability-only"))
{
    bool baseline=args.Contains("--picker-stability-baseline");
    Check("cold artwork load budget",()=>PickerStabilityChecks.ColdArtwork(baseline?"baseline":"final",true));
    Check("closed overlays recover through shortcut visibility actions",PickerStabilityChecks.ReopenClosedOverlays);
    Check("standalone native picker chrome and exterior",PickerStabilityChecks.StandalonePicker);
    Environment.Exit(failures==0?0:1);
}
if(args.Contains("--navigator-picker-only")||args.Contains("--navigator-picker-regression"))
{
    Check("Navigator native row presentation",NavigatorPickerChecks.Presentation);
    Check("Navigator native navigation and confirmation",NavigatorPickerChecks.Navigation);
    Check("Navigator native hover, click and cancellation",NavigatorPickerChecks.PointerStates);
    Check("Navigator native button states",NavigatorPickerChecks.ButtonStates);
    Check("Navigator close-button focus and press",NavigatorPickerChecks.CloseButtonStates);
    Check("Navigator maximum hours, recycling and automation",NavigatorPickerChecks.ExtremeHoursAndRecycling);
    Check("Navigator actual control captures",NavigatorPickerChecks.Evidence);
    if(!args.Contains("--navigator-picker-regression"))Environment.Exit(failures==0?0:1);
}
if(args.Contains("--stopwatch-performance-baseline")||args.Contains("--stopwatch-performance-final"))
{
    Check("native overlay performance sample",()=>StopwatchPerformanceChecks.Benchmark(args.Contains("--stopwatch-performance-baseline")?"baseline":"final"));
    Environment.Exit(failures==0?0:1);
}
if(args.Contains("--stopwatch-invalidation-only"))
{
    Check("unchanged face values retain layout",StopwatchPerformanceChecks.UnchangedProperties);
    Check("fixed-width Nika ticks retain layout and repaint",StopwatchPerformanceChecks.StableDigitLayout);
    Check("unchanged overlay presentation reuses masks and toolbar",StopwatchPerformanceChecks.RepeatedPresentation);
    Check("direct settings preserve inactive toolbar opacity",StopwatchPerformanceChecks.DirectSettingsPreserveOpacity);
    Environment.Exit(failures==0?0:1);
}
if(args.Contains("--stopwatch-redraw-regression")||args.Contains("--navigator-picker-regression"))
{
    StopwatchAppearanceChecks.OutputOverride=".superpowers/sdd/2026-11-05-stopwatch-redraw-cost/render-checks";
    Check("unchanged face values retain layout",StopwatchPerformanceChecks.UnchangedProperties);
    Check("fixed-width Nika ticks retain layout and repaint",StopwatchPerformanceChecks.StableDigitLayout);
    Check("unchanged overlay presentation reuses masks and toolbar",StopwatchPerformanceChecks.RepeatedPresentation);
    Check("direct settings preserve inactive toolbar opacity",StopwatchPerformanceChecks.DirectSettingsPreserveOpacity);
    Check("performance repair visual parity",StopwatchPerformanceChecks.VisualParity);
    Check("production frames match reviewed artwork",SharedFrameChecks.ReviewedArtwork);
    Check("shared frame masks, padding, small sizes and cache",SharedFrameChecks.Layouts);
    Check("wave mask preserves separate interior spans",SharedFrameChecks.WaveRecesses);
    Check("shared frame picker and keyboard behavior",SharedFrameUiChecks.Picker);
    Check("overlay layout and state",StopwatchAppearanceChecks.Renderer);
    Check("native state, popup alpha and theme restoration",StopwatchAppearanceChecks.LiveOverlay);
    Check("complete asset and typography coverage",StopwatchAppearanceChecks.CatalogAssets);
    Check("inspector and preview isolation",StopwatchAppearanceUiChecks.Run);
    Check("controller routing, native flags and resources",StopwatchAppearanceChecks.ControllerAndResources);
    Check("timer shortcut actions",ShortcutChecks.Timers);
    Environment.Exit(failures==0?0:1);
}
if (args.Contains("--shortcuts-only"))
{
    Check("timer shortcut actions", ShortcutChecks.Timers);
    Check("note shortcut actions and cancellation", ShortcutChecks.Notes);
    Environment.Exit(failures == 0 ? 0 : 1);
}
if (args.Contains("--stopwatch-renderer-only"))
{
    Check("Stopwatch shared renderer and live state", StopwatchAppearanceChecks.Renderer);
    Environment.Exit(failures == 0 ? 0 : 1);
}
if (args.Contains("--shared-frames-ui"))
{
    StopwatchAppearanceChecks.OutputOverride=".superpowers/sdd/2026-11-05-shared-modular-frames/render-checks";
    Check("shared frame picker and keyboard behavior",SharedFrameUiChecks.Picker);
    Check("shared frame overlay layout and state",StopwatchAppearanceChecks.Renderer);
    Environment.Exit(failures == 0 ? 0 : 1);
}
if (args.Contains("--shared-frames-regression"))
{
    StopwatchAppearanceChecks.OutputOverride=".superpowers/sdd/2026-11-05-shared-modular-frames/render-checks";
    Check("production frames match reviewed artwork",SharedFrameChecks.ReviewedArtwork);
    Check("shared frame masks, padding, small sizes and cache",SharedFrameChecks.Layouts);
    Check("wave mask preserves separate interior spans",SharedFrameChecks.WaveRecesses);
    Check("shared frame picker and keyboard behavior",SharedFrameUiChecks.Picker);
    Check("shared frame overlay layout and state",StopwatchAppearanceChecks.Renderer);
    Check("Stopwatch native state, popup alpha and theme restoration",StopwatchAppearanceChecks.LiveOverlay);
    Check("Stopwatch complete asset and typography coverage",StopwatchAppearanceChecks.CatalogAssets);
    Check("Stopwatch appearance inspector and preview isolation",StopwatchAppearanceUiChecks.Run);
    Check("Stopwatch controller routing, native flags and cached resources",StopwatchAppearanceChecks.ControllerAndResources);
    Check("timer shortcut actions",ShortcutChecks.Timers);
    Check("production shared frame visual evidence",SharedFrameUiChecks.Evidence);
    Environment.Exit(failures == 0 ? 0 : 1);
}
if (args.Contains("--stopwatch-appearance-only"))
{
    Check("Stopwatch shared renderer and live state", StopwatchAppearanceChecks.Renderer);
    Check("Stopwatch native state, popup alpha and theme restoration", StopwatchAppearanceChecks.LiveOverlay);
    Check("Stopwatch complete asset and typography coverage", StopwatchAppearanceChecks.CatalogAssets);
    Check("Stopwatch appearance inspector and preview isolation", StopwatchAppearanceUiChecks.Run);
    Check("Stopwatch controller routing, native flags and cached resources", StopwatchAppearanceChecks.ControllerAndResources);
    Environment.Exit(failures == 0 ? 0 : 1);
}
if (args.Contains("--stopwatch-inspector-only"))
{
    Check("Stopwatch appearance inspector and preview isolation", StopwatchAppearanceUiChecks.Run);
    Environment.Exit(failures == 0 ? 0 : 1);
}
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
