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

if(args.Contains("--settings-typography-regressions")||args.Contains("--settings-typography-native")||args.Contains("--settings-appearance-native")||args.Contains("--controller-sizing"))
{
    AppBuilder.Configure<FrameSizePreviewApp>().UsePlatformDetect()
        .With(new Win32PlatformOptions { RenderingMode = new[] { Win32RenderingMode.Software } })
        .WithInterFont().SetupWithoutStarting();
    Application.Current!.Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
    Application.Current.Styles.Add(new StyleInclude(new Uri("avares://StopwatchOverlay.Desktop/")){Source=new Uri("avares://StopwatchOverlay.Desktop/Styles/AppStyles.axaml")});
    if(args.Contains("--settings-typography-regressions"))
    {
        var typography=typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop.DesktopTypography")!;
        typography.GetMethod("Initialize",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[new TypographySettings()]);
        Check("Typography default preserves Settings shell",SettingsShellNativeChecks.Layout);
        Check("Typography default preserves Settings controls",SettingsShellNativeChecks.Controls);
        Check("Typography default preserves Appearance actions",SettingsAppearanceNativeChecks.LayoutAndActions);
        Check("Typography default preserves Appearance previews",SettingsAppearanceNativeChecks.PreviewIsolation);
        Check("Typography default preserves controller sizing",ControllerSizingChecks.Run);
        Check("Typography default preserves overlay resource caching",StopwatchPerformanceChecks.RepeatedPresentation);
        Check("Typography default preserves dashboard layout",DashboardOverviewNativeChecks.Layout);
        typography.GetMethod("Shutdown",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null);
        Environment.Exit(failures==0?0:1);
    }
    if(args.Contains("--settings-typography-native"))
    {
        Application.Current.Styles.Add(new StyleInclude(new Uri("avares://StopwatchOverlay.Desktop/")){Source=new Uri("avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml")});
        Check("Desktop typography sources and scope inheritance",SettingsTypographyNativeChecks.Application);
        Check("Fonts and colors approved panel",SettingsTypographyNativeChecks.Panel);
        Check("Typography popup ownership and painted overlays",SettingsTypographyNativeChecks.PopupsAndClocks);
        Check("Typography production controls and scale captures",SettingsTypographyNativeChecks.ProductionEvidence);
        Check("Typography selection, deferred work and cleanup",SettingsTypographyNativeChecks.SelectionAndLifecycle);
        Check("Typography preview scope matches live clocks",SettingsTypographyNativeChecks.PreviewScopes);
        Check("Typography persisted custom color selection",SettingsTypographyNativeChecks.ReopenedCustomColor);
        Check("Typography real routed gesture callbacks",SettingsTypographyNativeChecks.GestureRouting);
        Check("Typography updates cached application previews",SettingsTypographyNativeChecks.ApplicationPreview);
        Environment.Exit(failures==0?0:1);
    }
    if(args.Contains("--controller-sizing"))
    {
        Check("Controller scaled panel fills the native window",ControllerSizingChecks.Run);
        Environment.Exit(failures==0?0:1);
    }
    Check("Appearance controls, independent themes and navigation",SettingsAppearanceNativeChecks.LayoutAndActions);
    Check("Appearance native page scale and background application",SettingsAppearanceNativeChecks.Presentation);
    Check("Appearance cached backgrounds and theme restoration",SettingsAppearanceNativeChecks.BackgroundsAndRestoration);
    Check("Appearance bounded image decoding",SettingsAppearanceNativeChecks.BoundedImages);
    Check("Appearance production metadata and selected-image lookup",SettingsAppearanceNativeChecks.ProductionBackgroundLookup);
    Check("Appearance closes during pending scale without recreating previews",SettingsAppearanceNativeChecks.CloseDuringScaleAdjustment);
    Check("Appearance preview isolation and compact footer",SettingsAppearanceNativeChecks.PreviewIsolation);
    Environment.Exit(failures==0?0:1);
}

if(args.Contains("--settings-shell-native")||args.Contains("--settings-shell-regressions"))
{
    AppBuilder.Configure<FrameSizePreviewApp>().UsePlatformDetect()
        .With(new Win32PlatformOptions { RenderingMode = new[] { Win32RenderingMode.Software } })
        .WithInterFont().SetupWithoutStarting();
    Application.Current!.Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
    Application.Current.Styles.Add(new StyleInclude(new Uri("avares://StopwatchOverlay.Desktop/"))
        { Source = new Uri("avares://StopwatchOverlay.Desktop/Styles/AppStyles.axaml") });
    if(args.Contains("--settings-shell-regressions"))
    {
        StopwatchAppearanceChecks.OutputOverride=".superpowers/sdd/2026-11-12-settings-shell-native/render-regression";
        Check("retained stopwatch inspector controls and preview isolation",StopwatchAppearanceUiChecks.Run);
        Check("retained Todo settings and dialogs",TodoAppearanceChecks.InspectorAndDialogs);
        Check("controller mode selections",ControllerModeChecks.Run);
        Check("unchanged overlay state keeps cached resources",StopwatchPerformanceChecks.RepeatedPresentation);
        Environment.Exit(failures==0?0:1);
    }
    Check("settings shell layout and retained navigation",SettingsShellNativeChecks.Layout);
    Check("settings immediate changes and persistence feedback",SettingsShellNativeChecks.Controls);
    Check("settings active overlay visibility wiring",SettingsShellNativeChecks.OverlayActions);
    Check("settings monitor work-area, DPI and custom placement",SettingsShellNativeChecks.Placement);
    Check("settings display mirrors, retention and cleanup",SettingsShellNativeChecks.Mirrors);
    Console.WriteLine($"Settings native checks: {failures} failed");
    Environment.Exit(failures == 0 ? 0 : 1);
}

if(args.Contains("--dashboard-overview-native") || args.Contains("--dashboard-charts-native") || args.Contains("--dashboard-timeline-native"))
{
    AppBuilder.Configure<FrameSizePreviewApp>().UsePlatformDetect()
        .With(new Win32PlatformOptions { RenderingMode = new[] { Win32RenderingMode.Software } })
        .WithInterFont().SetupWithoutStarting();
    Application.Current!.Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
    Application.Current.Styles.Add(new StyleInclude(new Uri("avares://StopwatchOverlay.Desktop/"))
        { Source = new Uri("avares://StopwatchOverlay.Desktop/Styles/AppStyles.axaml") });
    if(args.Contains("--dashboard-timeline-native"))
    {
        Check("dashboard timeline identity, overlap and clipped records",DashboardTimelineNativeChecks.DataAndLayout);
        Check("dashboard timeline record actions and confirmation",DashboardTimelineNativeChecks.Actions);
        Check("dashboard timeline paging, live retention and bounded history",DashboardTimelineNativeChecks.RefreshAndPaging);
        Check("dashboard timeline compact, RTL, readonly and native captures",DashboardTimelineNativeChecks.StatesAndEvidence);
        Check("dashboard charts regression data",DashboardChartsNativeChecks.DataAndSelection);
        Check("dashboard charts regression refresh",DashboardChartsNativeChecks.LongHistoryAndRefresh);
        Check("dashboard charts regression layout",DashboardChartsNativeChecks.LayoutAndStates);
        Check("dashboard overview regression layout",DashboardOverviewNativeChecks.Layout);
        Check("dashboard overview regression calculations",DashboardOverviewNativeChecks.CalculationsAndFilters);
        Check("dashboard overview regression editing",DashboardOverviewNativeChecks.LiveEditingAndLifecycle);
        Check("dashboard overview regression dialogs",DashboardOverviewNativeChecks.DialogsAndThemes);
        Environment.Exit(failures==0?0:1);
    }
    if(args.Contains("--dashboard-charts-native"))
    {
        Check("dashboard chart totals, project colors and calendar selection",DashboardChartsNativeChecks.DataAndSelection);
        Check("dashboard bounded long-history rendering and live scroll retention",DashboardChartsNativeChecks.LongHistoryAndRefresh);
        Check("dashboard charts compact, RTL, empty and other themes",DashboardChartsNativeChecks.LayoutAndStates);
        Check("dashboard charts native production captures",DashboardChartsNativeChecks.Evidence);
        Check("dashboard overview regression layout",DashboardOverviewNativeChecks.Layout);
        Check("dashboard overview regression calculations",DashboardOverviewNativeChecks.CalculationsAndFilters);
        Check("dashboard overview regression editing and lifecycle",DashboardOverviewNativeChecks.LiveEditingAndLifecycle);
        Check("dashboard overview regression dialogs",DashboardOverviewNativeChecks.DialogsAndThemes);
        Environment.Exit(failures==0?0:1);
    }
    Check("dashboard standalone layout and fixed actions",DashboardOverviewNativeChecks.Layout);
    Check("dashboard totals, filters and score actions",DashboardOverviewNativeChecks.CalculationsAndFilters);
    Check("dashboard live editing and lifecycle",DashboardOverviewNativeChecks.LiveEditingAndLifecycle);
    Check("dashboard existing dialogs and other themes",DashboardOverviewNativeChecks.DialogsAndThemes);
    Check("dashboard native production captures",DashboardOverviewNativeChecks.Evidence);
    Environment.Exit(failures==0?0:1);
}

// Review fixtures load production styles without initializing live settings, hooks or tray.
if(args.Contains("--review-projects-native") || args.Contains("--review-allocation-native") || args.Contains("--review-mood-native"))
{
    AppBuilder.Configure<FrameSizePreviewApp>().UsePlatformDetect()
        .With(new Win32PlatformOptions { RenderingMode = new[] { Win32RenderingMode.Software } })
        .WithInterFont().SetupWithoutStarting();
    Application.Current!.Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
    Application.Current.Styles.Add(new StyleInclude(new Uri("avares://StopwatchOverlay.Desktop/"))
        { Source = new Uri("avares://StopwatchOverlay.Desktop/Styles/AppStyles.axaml") });
    if(args.Contains("--review-mood-native"))
    {
        Check("review mood shared frame and fixed actions",ReviewMoodNativeChecks.Layout);
        Check("review mood score defaults, limits and presets",ReviewMoodNativeChecks.Scores);
        Check("review mood feelings, keyboard and RTL notes",ReviewMoodNativeChecks.FeelingsAndNotes);
        Check("review mood navigation and time summary",ReviewMoodNativeChecks.NavigationAndSummary);
        Check("review mood save, time-only, cancellation and themes",ReviewMoodNativeChecks.SaveAndCancel);
        Check("review mood production native captures",ReviewMoodNativeChecks.Evidence);
        Environment.Exit(failures==0?0:1);
    }
    if(args.Contains("--review-allocation-native"))
    {
        Check("review allocation consistent project colors",ReviewAllocationNativeChecks.ProjectColors);
        Check("review allocation shared frame and actions",ReviewAllocationNativeChecks.Layout);
        Check("review allocation duration and session actions",ReviewAllocationNativeChecks.Actions);
        Check("review allocation collision policies and recorded intervals",ReviewAllocationNativeChecks.CollisionsAndFixedRecords);
        Check("review allocation dragging and save results",ReviewAllocationNativeChecks.DragAndSave);
        Check("review allocation navigation, themes and production captures",ReviewAllocationNativeChecks.NavigationAndEvidence);
        Environment.Exit(failures==0?0:1);
    }
    Check("review projects shared frame and fixed actions",ReviewProjectsNativeChecks.Layout);
    Check("review repeated selection, registration and keyboard",ReviewProjectsNativeChecks.SelectionAndRegistration);
    Check("review chronological removal and wizard navigation",ReviewProjectsNativeChecks.ChronologicalRemovalAndNavigation);
    Check("review activity grouping, threshold and offline retry",ReviewProjectsNativeChecks.ActivityAndRetry);
    Check("review pending loads and closed-window cancellation",ReviewProjectsNativeChecks.LateCompletion);
    Check("review production native captures",ReviewProjectsNativeChecks.Evidence);
    Environment.Exit(failures==0?0:1);
}

// Record fixtures load production styles without initializing the app's settings or tray.
if(args.Contains("--time-record-native"))
{
    AppBuilder.Configure<FrameSizePreviewApp>().UsePlatformDetect()
        .With(new Win32PlatformOptions { RenderingMode = new[] { Win32RenderingMode.Software } })
        .WithInterFont().SetupWithoutStarting();
    Application.Current!.Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
    Application.Current.Styles.Add(new StyleInclude(new Uri("avares://StopwatchOverlay.Desktop/"))
        { Source = new Uri("avares://StopwatchOverlay.Desktop/Styles/AppStyles.axaml") });
    Check("record fixture does not load live settings",ProjectRecordNativeChecks.Isolation);
    Check("record native frame, expansion and fixed footer",ProjectRecordNativeChecks.Layout);
    Check("record add/edit results and project drafts",ProjectRecordNativeChecks.Results);
    Check("record validation, RTL and cancellation",ProjectRecordNativeChecks.Validation);
    Check("record commit failures and ownerless cancellation",ProjectRecordNativeChecks.FailuresAndCancellation);
    Check("record native popup input and theme compatibility",ProjectRecordNativeChecks.InputAndThemes);
    Check("record typed dates and long duration layout",ProjectRecordNativeChecks.TypedDatesAndLongRanges);
    Check("record short-height validation and commit feedback",ProjectRecordNativeChecks.ErrorVisibility);
    Check("record actual native captures",ProjectRecordNativeChecks.Evidence);
    Environment.Exit(failures==0?0:1);
}

if(args.Contains("--settings-appearance-preview") || args.Contains("--settings-typography-preview"))
{
    AppBuilder.Configure<FrameSizePreviewApp>().UsePlatformDetect()
        .With(new Win32PlatformOptions { RenderingMode = new[] { Win32RenderingMode.Software } })
        .WithInterFont().SetupWithoutStarting();
    if(args.Contains("--settings-typography-preview"))Check("settings Fonts and colors native design proposal",SettingsTypographyPreview.Run);
    else Check("settings Appearance native design proposal",SettingsAppearancePreview.Run);
    Environment.Exit(failures==0?0:1);
}

// This visual spike needs only Avalonia drawing services, without application settings or hooks.
if (args.Contains("--settings-shell-preview") || args.Contains("--dashboard-timeline-preview") || args.Contains("--dashboard-charts-preview") || args.Contains("--dashboard-overview-preview") || args.Contains("--review-mood-preview") || args.Contains("--review-allocation-preview") || args.Contains("--review-projects-preview") || args.Contains("--time-record-preview") || args.Contains("--project-chooser-preview") || args.Contains("--timer-editor-states") || args.Contains("--timer-editor-preview") || args.Contains("--running-clocks-state-preview") || args.Contains("--running-clocks-preview") || args.Contains("--clock-picker-state-preview") || args.Contains("--clock-picker-row-preview") || args.Contains("--frame-size-preview") || args.Contains("--frame-modules-preview") || args.Contains("--frame-catalog-audit") || args.Contains("--frame-join-refinement") || args.Contains("--frame-production-audit") || args.Contains("--shared-frames-only"))
{
    AppBuilder.Configure<FrameSizePreviewApp>().UsePlatformDetect()
        .With(new Win32PlatformOptions { RenderingMode = new[] { Win32RenderingMode.Software } })
        .WithInterFont().SetupWithoutStarting();
    if(args.Contains("--settings-shell-preview"))Check("settings first-layer native design proposal",SettingsShellPreview.Run);
    else if(args.Contains("--dashboard-timeline-preview"))Check("dashboard timeline and records native design proposal",DashboardTimelinePreview.Run);
    else if(args.Contains("--dashboard-charts-preview"))Check("dashboard charts and heatmap native design proposal",DashboardChartsPreview.Run);
    else if(args.Contains("--dashboard-overview-preview"))Check("dashboard overview native design proposal",DashboardOverviewPreview.Run);
    else if(args.Contains("--review-mood-preview"))Check("review feelings and mood native design proposals",ReviewMoodPreview.Run);
    else if(args.Contains("--review-allocation-preview"))Check("review allocation native design proposals",ReviewAllocationPreview.Run);
    else if(args.Contains("--review-projects-preview"))Check("review projects native design proposals",ReviewProjectsPreview.Run);
    else if(args.Contains("--time-record-preview"))Check("time record native design proposals",ProjectRecordPreview.Run);
    else if(args.Contains("--project-chooser-preview"))Check("project chooser native design proposals",ProjectChooserPreview.Run);
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
