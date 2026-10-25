using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Habitica;

internal static class HabiticaImportUiChecks
{
    public static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "HabiticaImportUi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var settings = new AppSettings { ObsidianVaultFolder = root, NotesSubfolder = "Notes",
            HabiticaUserId = "11111111-1111-4111-8111-111111111111", HabiticaClientId = "22222222-2222-4222-8222-222222222222-Stopwatch" };
        var integration = new HabiticaIntegration(new HabiticaApiClient(new HttpClient(new NoNetwork())), new Secrets(), Path.Combine(root, "local.json"));
        integration.EnableHere(settings);
        for (int i = 0; i < 43; i++) ObsidianNotesSync.AppendEntry(root, NoteType.Todo, "Task " + i, "Notes", new DateTime(2026, 10, 25, 10, i, 0));
        var panel = new HabiticaSettingsPanel(null, integration);
        panel.Bind(settings, () => { });
        var window = new Window { Content = new ScrollViewer { Content = panel }, Width = 540, Height = 680, ShowActivated = false };
        window.Show(); Pump();
        try
        {
            var content = (StackPanel)panel.Child!;
            var import = Find<Button>(panel, "SyncExistingHabiticaButton");
            import.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            var confirm = Find<StackPanel>(panel, "HabiticaImportConfirmation");
            Require(confirm.IsVisible, "Bulk import did not show confirmation.");
            Require(content.Children.IndexOf(confirm) == content.Children.IndexOf(import) + 1, "Confirmation is hidden away from the import action.");
            Require(integration.GetStatus(settings).PendingCount == 0, "Preview queued tasks before confirmation.");
            Find<Button>(panel, "ConfirmHabiticaActionButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(Find<StackPanel>(panel, "HabiticaImportProgressPanel").IsVisible, "Preparing feedback did not appear immediately.");
            var end = DateTime.UtcNow.AddSeconds(8);
            while (integration.GetStatus(settings).ImportProgress?.IsQueuing != false && DateTime.UtcNow < end) Pump();
            Pump(); Refresh(panel);
            var viewport = (ScrollViewer)window.Content!;
            var progressPanel = Find<StackPanel>(panel, "HabiticaImportProgressPanel");
            var progressPosition = progressPanel.TranslatePoint(default, viewport)!.Value;
            Require(progressPosition.Y >= -1 && progressPosition.Y + progressPanel.Bounds.Height <= viewport.Bounds.Height + 1, "Import progress is still outside the visible viewport after confirming.");
            Require(integration.GetStatus(settings).ImportProgress?.Total == 43, "Confirmed import did not enroll all items.");
            Require(Find<TextBlock>(panel, "HabiticaImportProgressText").Text!.Contains("0 / 43"), "Queued import looks complete before server acknowledgement.");
            Display(panel, new("", 41, null, true, new(43, 41, 2, 0, false), DateTime.UtcNow.AddSeconds(24)));
            Require(Find<TextBlock>(panel, "HabiticaImportProgressText").Text!.Contains("2 / 43"), "Completed/total feedback missing.");
            Require(Find<TextBlock>(panel, "HabiticaImportProgressDetail").Text!.Contains("Next request"), "Rate-limit wait is invisible.");
            var bar = Find<ProgressBar>(panel, "HabiticaImportProgressBar");
            Require(!bar.IsIndeterminate && bar.Maximum == 43 && bar.Value == 2, "Determinate progress does not reflect acknowledgements.");
            Display(panel, new("Enter your Habitica user ID, API token and author client ID.", 41, null, true, new(43, 41, 2, 0, false)));
            Require(Find<TextBlock>(panel, "HabiticaImportProgressDetail").Text!.Contains("API token"), "Global connection problem is hidden behind waiting progress.");
            Display(panel, new("", 1, null, true, new(43, 0, 42, 1, false), DateTime.UtcNow.AddSeconds(24)));
            Require(Find<TextBlock>(panel, "HabiticaImportProgressDetail").Text!.Contains("attention"), "Blocked item looks like silent progress.");
            Require(!Find<TextBlock>(panel, "HabiticaImportProgressDetail").Text!.Contains("Next request"), "Blocked batch claims a request is about to run.");
            Display(panel, new("", 0, null, true, new(43, 0, 43, 0, false)));
            Require(Find<TextBlock>(panel, "HabiticaImportProgressText").Text!.Contains("43 / 43") && Find<TextBlock>(panel, "HabiticaImportProgressDetail").Text!.Contains("complete", StringComparison.OrdinalIgnoreCase), "Finished import has no completion feedback.");
            settings.HabiticaEnabled = false;
            Display(panel, new("", 41, null, true, new(43, 41, 2, 0, false)));
            Require(Find<TextBlock>(panel, "HabiticaImportProgressDetail").Text!.Contains("Paused"), "Paused import falsely appears to be sending.");
            settings.HabiticaEnabled = true;
            Display(panel, new("", 0, null, true, new(0, 0, 0, 0, false)));
            Require(Find<TextBlock>(panel, "HabiticaImportProgressText").Text!.Contains("No existing"), "Empty import has no clear feedback.");
        }
        finally { window.Close(); Directory.Delete(root, true); }
    }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static T Find<T>(Control root, string name) where T : Control => root.GetVisualDescendants().OfType<T>().Single(item => item.Name == name);
    private static void Display(HabiticaSettingsPanel panel, HabiticaSyncStatus status) => typeof(HabiticaSettingsPanel).GetMethod("UpdateImportProgress", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(panel, new object[] { status });
    private static void Refresh(HabiticaSettingsPanel panel) => typeof(HabiticaSettingsPanel).GetMethod("RefreshStatus", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(panel, null);
    private static void Pump() { using var cancel = new CancellationTokenSource(); DispatcherTimer.RunOnce(cancel.Cancel, TimeSpan.FromMilliseconds(100)); Dispatcher.UIThread.MainLoop(cancel.Token); Dispatcher.UIThread.RunJobs(); }
    private sealed class NoNetwork : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken c) => throw new Exception("No network allowed in UI checks."); }
    private sealed class Secrets : IHabiticaSecretStore { public string Status => "Test memory"; public string? Read(string a) => null; public bool Write(string a, string t) => true; public void Delete(string a) { } }
}
