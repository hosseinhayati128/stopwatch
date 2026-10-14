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
AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().SetupWithoutStarting();
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
var controller = new ControllerWindow();
var flags = BindingFlags.NonPublic | BindingFlags.Instance;
var settingsField = typeof(ControllerWindow).GetField("_settings", flags)!;
var active = (AppSettings)settingsField.GetValue(controller)!;
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
foreach (var pair in expectedNoteKeys) {
    enterCommandMode.Invoke(controller, new object[] { true });
    commandKey.Invoke(controller, new object?[] { null, pair.Value });
    Dispatcher.UIThread.RunJobs();
    if ((bool)modeActive.GetValue(controller)!) throw new Exception("Note letter was not handled: " + pair.Key);
}
active.TextSize = 61;
typeof(ControllerWindow).GetMethod("SettingsButton_Click", flags)!.Invoke(controller, new object?[] { null, null });
var window = (SettingsWindow)typeof(ControllerWindow).GetField("_settingsWindow", flags)!.GetValue(controller)!;
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
Console.WriteLine("PASS: Ctrl+Enter saves all three note types, plain Enter inserts a newline, focused editor follows three themes; note letters, leader repeats, menu labels and timeout verified; settings persisted, HH:MM:SS applied, all palettes loaded, controller artwork switches, clock geometry and preview match, independent opacity layers apply, portal triggers formatted.");

Directory.Delete(checkDirectory, recursive: true);
Environment.Exit(0);
