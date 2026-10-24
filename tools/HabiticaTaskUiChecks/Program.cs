using System.Reflection;
using StopwatchOverlay;
using StopwatchOverlay.Habitica;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using StopwatchOverlay.Desktop.Views;

int failures = 0;
void Require(bool value, string message) { if (!value) throw new Exception(message); }
void Check(string name, Action test) { try { test(); Console.WriteLine("PASS " + name); } catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + ": " + e.GetBaseException().Message); } }
NoteSyncResult Invoke(string method, params object?[] args)
{
    var target = typeof(ObsidianNotesSync).GetMethod(method);
    Require(target != null, "Missing validated task mutation: " + method);
    var result = (NoteSyncResult)target!.Invoke(null, args)!;
    if (!result.Success) Console.WriteLine("  mutation: " + result.Message);
    return result;
}
var root = Path.Combine(Path.GetTempPath(), "HabiticaTaskChecks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(root, "Notes"));
var path = Path.Combine(root, "Notes", "Reminders.md");
var entry = new NoteEntry(NoteType.Reminder, new DateTime(2026, 10, 24, 9, 30, 0), "Call doctor");
const string markdown = "---\r\ntags: [private]\r\n---\r\n# Reminders\r\n\r\n## 2026-10-24\r\n- [ ] 09:30 ⏰ Call doctor\r\n\r\nPersonal paragraph\r\n";
try
{
    Check("reminder completion preserves all unrelated Markdown", () => {
        File.WriteAllText(path, markdown);
        Require(Invoke("SetEntryCompleted", root, entry, true, "Notes").Success, "Completion failed");
        Require(File.ReadAllText(path) == markdown.Replace("[ ]", "[x]"), "Surrounding Markdown changed");
        Require(!Invoke("SetEntryCompleted", root, entry, false, "Notes").Success, "Stale completion accepted");
    });
    Check("reminder edit keeps timestamp and reminder marker", () => {
        File.WriteAllText(path, markdown);
        Require(Invoke("UpdateEntryText", root, entry, "Call dentist", "Notes", false).Success, "Edit failed");
        Require(File.ReadAllText(path) == markdown.Replace("Call doctor", "Call dentist"), "Edit changed metadata or time");
        Require(!Invoke("DeleteEntry", root, entry, "Notes").Success, "Stale delete accepted");
    });
    Check("reminder delete removes only exact task", () => {
        File.WriteAllText(path, markdown);
        Require(Invoke("DeleteEntry", root, entry, "Notes").Success, "Delete failed");
        Require(File.ReadAllText(path) == markdown.Replace("- [ ] 09:30 ⏰ Call doctor\r\n", ""), "Unrelated content deleted");
    });
    Check("ambiguous reminders and standalone notes cannot mutate", () => {
        var duplicated = markdown.Replace("Personal paragraph", "- [ ] 09:30 ⏰ Call doctor");
        File.WriteAllText(path, duplicated);
        Require(!Invoke("DeleteEntry", root, entry, "Notes").Success, "Duplicate accepted");
        Require(!Invoke("SetEntryCompleted", root, entry with { Type = NoteType.Note }, true, "Notes").Success, "Standalone note mutated");
        Require(File.ReadAllText(path) == duplicated, "Rejected mutation wrote content");
    });
}
finally { Directory.Delete(root, true); }
AppBuilder.Configure<Application>().UsePlatformDetect()
    .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] })
    .With(new X11PlatformOptions { RenderingMode = [X11RenderingMode.Software] }).SetupWithoutStarting();
Check("checklist editor preserves child IDs through text edits and reorder", () => {
    var type = typeof(NoteEntryWindow).Assembly.GetType("StopwatchOverlay.Desktop.Controls.HabiticaSubtaskEditor");
    Require(type != null, "Missing explicit checklist editor");
    dynamic editor = Activator.CreateInstance(type!)!;
    var first = new HabiticaSubtask(Guid.NewGuid(), "First", false);
    var second = new HabiticaSubtask(Guid.NewGuid(), "Second", true);
    editor.SetSubtasks(new[] { first, second });
    var panel = (StackPanel)editor;
    var rows = (StackPanel)panel.Children[1];
    var row = (Grid)rows.Children[0];
    row.Children.OfType<TextBox>().Single().Text = "Edited";
    ((Button)row.Children.Single(c => c is Button b && Equals(b.Content, "↓"))).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    IReadOnlyList<HabiticaSubtask> edited = editor.GetSubtasks();
    Require(edited[0] == second && edited[1] == first with { Text = "Edited" }, "Child identity or state changed");
});
Check("reminder editor supports edit and optional due fields without todo checklist", () => {
    var editor = new NoteEntryWindow(NoteType.Reminder, new AppSettings(), entry);
    Require(editor.FindControl<TextBox>("NoteInputBox")!.Text == entry.Text, "Existing reminder text not loaded");
    Require(editor.FindControl<StackPanel>("TodoDetailsPanel")!.IsVisible, "Reminder schedule unavailable");
    Require(!editor.FindControl<ContentControl>("SubtaskEditorHost")!.IsVisible, "Reminder shows todo checklist");
    editor.Close();
});
Check("editor persists reminder due time without duplicate creation on edit", () => {
    var vault = Path.Combine(Path.GetTempPath(), "HabiticaReminderEditor-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(vault);
    try
    {
        var settings = new AppSettings { ObsidianVaultFolder = vault, NotesSubfolder = "Notes", TelegramEnabled = false, HabiticaEnabled = false };
        ObsidianNotesSync.AppendEntry(vault, NoteType.Reminder, entry.Text, "Notes", entry.Timestamp);
        var editor = new NoteEntryWindow(NoteType.Reminder, settings, entry);
        editor.FindControl<TextBox>("NoteInputBox")!.Text = "Edited reminder";
        editor.FindControl<DatePicker>("TodoDueDate")!.SelectedDate = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);
        editor.FindControl<TimePicker>("TodoDueTime")!.SelectedTime = new TimeSpan(14, 45, 0);
        editor.FindControl<CheckBox>("DueEnabledToggle")!.IsChecked = true;
        ((Task)typeof(NoteEntryWindow).GetMethod("SaveAndCloseAsync", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(editor, null)!).GetAwaiter().GetResult();
        var saved = ObsidianNotesSync.LoadAllNotes(vault, "Notes").Single();
        Require(saved.Text == "Edited reminder" && saved.Timestamp == entry.Timestamp, "Edit appended or lost original identity");
        Require(HabiticaIntegration.Current.GetDetails(settings, saved).DueAt == new DateTime(2026, 11, 2, 14, 45, 0), "Reminder due time lost");
        editor.Close();
    }
    finally { Directory.Delete(vault, true); }
});
Check("todo editor saves explicit checklist without interpreting Markdown body", () => {
    var vault = Path.Combine(Path.GetTempPath(), "HabiticaTodoEditor-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(vault);
    try
    {
        var settings = new AppSettings { ObsidianVaultFolder = vault, NotesSubfolder = "Notes", TelegramEnabled = false, HabiticaEnabled = false };
        var editor = new NoteEntryWindow(NoteType.Todo, settings);
        editor.FindControl<TextBox>("NoteInputBox")!.Text = "Parent\n- Ordinary body list";
        var checklist = (StopwatchOverlay.Desktop.Controls.HabiticaSubtaskEditor)editor.FindControl<ContentControl>("SubtaskEditorHost")!.Content!;
        var child = new HabiticaSubtask(Guid.NewGuid(), "Explicit child", true);
        checklist.SetSubtasks(new[] { child });
        ((Task)typeof(NoteEntryWindow).GetMethod("SaveAndCloseAsync", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(editor, null)!).GetAwaiter().GetResult();
        var saved = ObsidianNotesSync.LoadAllNotes(vault, "Notes").Single();
        Require(saved.Text == "Parent\n- Ordinary body list", "Body altered");
        Require(!saved.IsCompleted, "Completed child completed parent");
        Require(HabiticaIntegration.Current.GetDetails(settings, saved).Children.Single() == child, "Explicit child not persisted");
        Require(!File.ReadAllText(Path.Combine(vault, "Notes", "Todos.md")).Contains("Explicit child"), "Checklist inserted into original Markdown");
        editor.Close();
    }
    finally { Directory.Delete(vault, true); }
});
Check("standalone note save preserves its body and creates no Habitica metadata", () => {
    var vault = Path.Combine(Path.GetTempPath(), "HabiticaNoteEditor-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(vault);
    try
    {
        var settings = new AppSettings { ObsidianVaultFolder = vault, NotesSubfolder = "Notes", TelegramEnabled = false, HabiticaEnabled = false };
        var editor = new NoteEntryWindow(NoteType.Note, settings);
        editor.FindControl<TextBox>("NoteInputBox")!.Text = "Original\r\nParagraph";
        ((Task)typeof(NoteEntryWindow).GetMethod("SaveAndCloseAsync", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(editor, null)!).GetAwaiter().GetResult();
        Require(File.ReadAllText(Path.Combine(vault, "Notes", "Notes.md")).Contains("Original\r\nParagraph"), "Standalone body changed");
        Require(!File.Exists(Path.Combine(vault, "Notes", ".stopwatch-habitica.json")), "Standalone note created sync metadata");
        editor.Close();
    }
    finally { Directory.Delete(vault, true); }
});
Check("metadata failure keeps local save and shows warning without duplicate retry", () => {
    var vault = Path.Combine(Path.GetTempPath(), "HabiticaWarningEditor-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(Path.Combine(vault, "Notes"));
    try
    {
        File.WriteAllText(Path.Combine(vault, "Notes", ".stopwatch-habitica.json"), "invalid json");
        var settings = new AppSettings { ObsidianVaultFolder = vault, NotesSubfolder = "Notes", TelegramEnabled = false, HabiticaEnabled = false };
        var editor = new NoteEntryWindow(NoteType.Todo, settings);
        editor.FindControl<TextBox>("NoteInputBox")!.Text = "Keep this local task";
        var save = typeof(NoteEntryWindow).GetMethod("SaveAndCloseAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        ((Task)save.Invoke(editor, null)!).GetAwaiter().GetResult();
        Require(ObsidianNotesSync.LoadAllNotes(vault, "Notes").Count == 1, "Local task lost");
        Require(editor.FindControl<TextBlock>("SaveError")!.IsVisible && !string.IsNullOrWhiteSpace(editor.FindControl<TextBlock>("SaveError")!.Text), "No visible sync warning");
        Require(Equals(editor.FindControl<Button>("SaveButton")!.Content, "Close"), "Save can be submitted twice");
        ((Task)save.Invoke(editor, null)!).GetAwaiter().GetResult();
        Require(ObsidianNotesSync.LoadAllNotes(vault, "Notes").Count == 1, "Warning acknowledgement duplicated task");
        editor.Close();
    }
    finally { Directory.Delete(vault, true); }
});
Check("editor rejects task save after Habitica account changes", () => {
    var vault = Path.Combine(Path.GetTempPath(), "HabiticaAccountEditor-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(vault);
    try
    {
        var settings = new AppSettings { ObsidianVaultFolder = vault, NotesSubfolder = "Notes", TelegramEnabled = false, HabiticaEnabled = false, HabiticaUserId = Guid.NewGuid().ToString() };
        ObsidianNotesSync.AppendEntry(vault, NoteType.Reminder, entry.Text, "Notes", entry.Timestamp);
        var editor = new NoteEntryWindow(NoteType.Reminder, settings, entry);
        settings.HabiticaUserId = Guid.NewGuid().ToString();
        editor.FindControl<TextBox>("NoteInputBox")!.Text = "Do not redirect";
        ((Task)typeof(NoteEntryWindow).GetMethod("SaveAndCloseAsync", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(editor, null)!).GetAwaiter().GetResult();
        Require(ObsidianNotesSync.LoadAllNotes(vault, "Notes").Single().Text == entry.Text, "Task changed under a different sync account");
        Require(editor.FindControl<TextBlock>("SaveError")!.IsVisible, "Account change was not explained");
        editor.Close();
    }
    finally { Directory.Delete(vault, true); }
});
Check("Habitica has its own settings category and token is not serialized", () => {
    var settings = new AppSettings();
    var window = new SettingsWindow(settings);
    var navigation = window.FindControl<ListBox>("NavigationList")!;
    navigation.SelectedItem = navigation.Items.OfType<ListBoxItem>().Single(item => Equals(item.Tag, "Habitica"));
    Require(window.FindControl<StopwatchOverlay.Desktop.Controls.HabiticaSettingsPanel>("HabiticaPanel")!.IsVisible, "Habitica category did not open");
    Require(!window.FindControl<StackPanel>("ApplicationPanel")!.IsVisible, "Application panel obscures Habitica settings");
    Require(!typeof(AppSettings).GetProperties().Any(p => p.Name.Contains("Habitica", StringComparison.OrdinalIgnoreCase) && p.Name.Contains("Token", StringComparison.OrdinalIgnoreCase)), "API token could enter ordinary settings");
    window.Close();
});
Check("masked Habitica token supports keyboard paste and a right-click Paste action", () => {
    Application.Current!.Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
    int pastes = 0;
    var panel = new StopwatchOverlay.Desktop.Controls.HabiticaSettingsPanel(() => {
        pastes++; return Task.FromResult<string?>("\r\n synthetic-token-for-paste-check \r\n");
    });
    var window = new Window { Content = panel, Width = 650, Height = 700 };
    try
    {
        window.Show(); window.UpdateLayout();
        var token = (TextBox)panel.GetType().GetField("_token", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(panel)!;
        token.Focus();
        token.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.V, KeyModifiers = Avalonia.Input.KeyModifiers.Control });
        Require(pastes == 1 && token.Text == "synthetic-token-for-paste-check", "Ctrl+V did not accept the token with surrounding line breaks");
        Require(token.PasswordChar != '\0' && !token.RevealPassword, "Pasting revealed token text");
        Require(token.ContextMenu?.Items.OfType<MenuItem>().Any(item => Equals(item.Header, "Paste")) == true,
            "The token field has no right-click Paste action");
        var paste = token.ContextMenu!.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "Paste"));
        token.SelectAll();
        paste.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Require(pastes == 2 && token.Text == "synthetic-token-for-paste-check", "Menu paste failed or duplicated selected text");
        var button = token.InnerRightContent as Button;
        Require(button != null && !button.Focusable, "Visible Paste button missing or steals the text selection");
        token.SelectAll(); button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(pastes == 3 && token.Text == "synthetic-token-for-paste-check" && !token.RevealPassword, "Paste button failed or revealed token");
    }
    finally { window.Close(); }
});
Check("clipboard errors and empty text keep the token and show safe feedback", () => {
    foreach (bool fails in new[] { false, true })
    {
        var panel = new StopwatchOverlay.Desktop.Controls.HabiticaSettingsPanel(() => fails
            ? Task.FromException<string?>(new TimeoutException("synthetic clipboard error"))
            : Task.FromResult<string?>(null));
        var window = new Window { Content = panel, Width = 650, Height = 700 };
        try
        {
            window.Show(); window.UpdateLayout();
            var token = (TextBox)panel.GetType().GetField("_token", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(panel)!;
            var feedback = (TextBlock)panel.GetType().GetField("_feedback", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(panel)!;
            token.Text = "synthetic-existing-token"; token.SelectAll(); token.Paste();
            Require(token.Text == "synthetic-existing-token", "Failed paste erased the token");
            Require(!string.IsNullOrWhiteSpace(feedback.Text) && feedback.Text.Contains("clipboard", StringComparison.OrdinalIgnoreCase), "Clipboard failure stayed silent");
            Require(!feedback.Text!.Contains("synthetic"), "Clipboard contents or error details reached feedback");
        }
        finally { window.Close(); }
    }
});
Check("invalid client identifier is explained before saving connection settings", () => {
    var settings = new AppSettings { HabiticaUserId = "11111111-1111-4111-8111-111111111111", HabiticaClientId = "StopwatchOverlay" };
    var panel = new StopwatchOverlay.Desktop.Controls.HabiticaSettingsPanel();
    int saves = 0; panel.Bind(settings, () => saves++);
    bool explained = false;
    try { panel.GetType().GetMethod("SaveConnection", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(panel, null); }
    catch (TargetInvocationException e) { explained = e.InnerException is InvalidOperationException && e.InnerException.Message.Contains("client", StringComparison.OrdinalIgnoreCase); }
    Require(explained && saves == 0, "Malformed client identifier was accepted without a useful explanation");
});
Check("connection feedback separates missing token, authentication, network and storage errors", () => {
    var settings = new AppSettings { HabiticaUserId = "11111111-1111-4111-8111-111111111111", HabiticaClientId = "22222222-2222-4222-8222-222222222222-StopwatchOverlay" };
    var panel = new StopwatchOverlay.Desktop.Controls.HabiticaSettingsPanel(); panel.Bind(settings, () => { });
    var method = panel.GetType().GetMethod("ConnectionTestMessage", BindingFlags.NonPublic | BindingFlags.Instance)!;
    string Message(HabiticaErrorKind kind, int? code = null) => (string)method.Invoke(panel, new object[] { new HabiticaApiResult<string>(null, kind, code) })!;
    Require(Message(HabiticaErrorKind.Configuration).Contains("No API token"), "Missing token reported as wrong password");
    Require(Message(HabiticaErrorKind.Authentication, 401).Contains("HTTP 401"), "Authentication response hidden");
    Require(Message(HabiticaErrorKind.Network).Contains("DNS"), "Network failure reported as wrong password");
    Require(Message(HabiticaErrorKind.Storage).Contains("folder"), "Storage failure reported as wrong password");
});
Check("independent computers are supported and copied-vault transfer is advanced", () => {
    var panel = new StopwatchOverlay.Desktop.Controls.HabiticaSettingsPanel();
    var content = (StackPanel)panel.Child!;
    Require(content.Children.OfType<TextBlock>().Any(t => t.Text?.Contains("separate local vaults", StringComparison.OrdinalIgnoreCase) == true), "Settings still imply only one syncing computer");
    var advanced = content.Children.OfType<Expander>().SingleOrDefault(e => e.Name == "HabiticaCopiedVaultOptions");
    Require(advanced != null && !advanced.IsExpanded, "Copied-vault migration must be collapsed by default");
});
Environment.Exit(failures == 0 ? 0 : 1);
