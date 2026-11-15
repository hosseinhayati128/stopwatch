using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Habitica;
using System.Text.Json;

internal static partial class SettingsHabiticaNativeChecks
{
    private const string RecoveryOutput = "design/one-piece-v2/settings/habitica-recovery-implemented";
    private static void OpenRecovery(Fixture f)
    {
        Click(Visual<Button>(f.Window, "HabiticaRecoveryTab"));
        Wait(() => !Field<bool>(f.Panel, "_recoveryCounting"));
    }
    private static void AddRecoveryItems(Fixture f, int count = 12)
    {
        for (int i = 0; i < count; i++)
        {
            var result = ObsidianNotesSync.AppendEntry(f.Model.ObsidianVaultFolder, NoteType.Todo,
                "Sample task " + (i + 1), f.Model.NotesSubfolder, new DateTime(2026, 11, 15, 10, i, 0));
            Require(result.Success, "Sample item could not be written: " + result);
        }
    }
    internal static void RecoveryActions()
    {
        using var f = new Fixture(); f.Save(); Click(Field<CheckBox>(f.Panel, "_enabled")); OpenRecovery(f);
        var review = Field<Button>(f.Panel, "_syncExisting"); var confirm = Visual<Button>(f.Window, "ConfirmHabiticaActionButton");
        var cancel = Visual<Button>(f.Window, "CancelHabiticaActionButton");
        Require(!review.IsEnabled && Text(f.Panel).Contains("No existing items"), "Empty vault enables bulk sync or hides its reason.");
        Click(Visual<Button>(f.Window, "HabiticaConnectionTab")); AddRecoveryItems(f);
        var token = Field<TextBox>(f.Panel, "_token"); token.Text = "unsaved-sample";
        Click(Visual<Button>(f.Window, "HabiticaRevealToken")); int saves = f.Saves; OpenRecovery(f);
        Require(token.Text == "unsaved-sample" && token.PasswordChar != '\0' && f.Saves == saves && !review.IsEnabled,
            "Tab switch saved/lost/exposed a draft or permits recovery with unsaved credentials.");
        Click(Visual<Button>(f.Window, "HabiticaConnectionTab")); token.Text = ""; OpenRecovery(f);
        Require(review.IsEnabled && Text(f.Panel).Contains("12 local items"), $"Eligible inventory: enabled={review.IsEnabled}, count={Field<int?>(f.Panel, "_recoveryExistingCount")}, counting={Field<bool>(f.Panel, "_recoveryCounting")}, queueing={Field<bool>(f.Panel, "_queueing")}, model enabled={f.Model.HabiticaEnabled}, status={f.Integration.GetStatus(f.Model)}, feedback={Field<TextBlock>(f.Panel, "_feedback").Text}, title={Field<TextBlock>(f.Panel, "_recoveryReady").Text}");
        Click(review); Wait(() => cancel.IsFocused);
        Require(Field<StackPanel>(f.Panel, "_confirmPanel").IsEffectivelyVisible && cancel.IsFocused &&
            Visual<Border>(f.Window, "HabiticaRecoveryBulk").GetVisualDescendants().Contains(confirm) &&
            f.Integration.GetStatus(f.Model).ImportProgress == null, $"Import confirmation: visible={Field<StackPanel>(f.Panel, "_confirmPanel").IsEffectivelyVisible}, focus={cancel.IsFocused}, focusable={cancel.Focusable}, enabled={cancel.IsEffectivelyEnabled}, focused={f.Window.FocusManager?.GetFocusedElement()}, in bulk={Visual<Border>(f.Window, "HabiticaRecoveryBulk").GetVisualDescendants().Contains(confirm)}, batch={f.Integration.GetStatus(f.Model).ImportProgress}; {Field<TextBlock>(f.Panel, "_feedback").Text}");
        Click(cancel); Require(!Field<StackPanel>(f.Panel, "_confirmPanel").IsVisible && review.IsFocused, "Cancel did not restore review focus.");
        void TraceIo(object? sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            if (e.Exception is IOException or UnauthorizedAccessException) Console.WriteLine("Sample import I/O: " + e.Exception);
        }
        AppDomain.CurrentDomain.FirstChanceException += TraceIo;
        try { Click(review); Click(confirm); Wait(() => !Field<bool>(f.Panel, "_queueing")); }
        finally { AppDomain.CurrentDomain.FirstChanceException -= TraceIo; }
        Require(f.Integration.GetStatus(f.Model).ImportProgress is { Total: 12, Queued: 12, Completed: 0 },
            $"Confirmed import did not queue real local items: {f.Integration.GetStatus(f.Model).ImportProgress}; {Field<string?>(f.Panel, "_importError")}; {Field<TextBlock>(f.Panel, "_feedback").Text}");
        Require(Field<ProgressBar>(f.Panel, "_importProgressBar").Value == 0, "Queued items were counted as completed.");
        string metadata = Path.Combine(f.Model.ObsidianVaultFolder, f.Model.NotesSubfolder, ".stopwatch-habitica.json");
        var vault = JsonSerializer.Deserialize<HabiticaIntegration.Vault>(File.ReadAllText(metadata))!;
        foreach (var item in vault.Items.Take(2))
        {
            var target = item.Targets[Account]; target.RemoteId = "sample-remote-" + item.Id;
            target.BlockKind = HabiticaErrorKind.NotFound; target.Block = "Task missing in Habitica."; target.Attempted = true;
        }
        File.WriteAllText(metadata, JsonSerializer.Serialize(vault)); Call(f.Panel, "RefreshStatus");
        var restore = Visual<Button>(f.Window, "RestoreMissingHabiticaButton");
        Require(restore.IsEnabled, "Missing linked tasks cannot be reviewed."); Click(restore); Pump(100);
        Require(confirm.Content!.ToString() == "Recreate 2 tasks" && Visual<Border>(f.Window, "HabiticaRecoveryRestore").GetVisualDescendants().Contains(confirm), "Restore confirmation is misplaced or mislabeled.");
        Visual<Border>(f.Window, "HabiticaRecoveryRestore").BringIntoView(); Pump(); Save(f.Window, "restore", RecoveryOutput);
        Click(cancel); Require(f.Integration.CountMissing(f.Model) == 2, "Cancel recreated missing tasks.");
        Click(restore); Click(confirm); Pump();
        Require(f.Integration.CountMissing(f.Model) == 0 && !restore.IsEnabled, "Confirmed recreation did not clear the missing targets.");
        var transfer = Visual<Button>(f.Window, "TransferHabiticaVaultButton"); Click(transfer); Pump();
        Require(confirm.Content!.ToString() == "Confirm transfer" && Visual<Border>(f.Window, "HabiticaRecoveryTransfer").GetVisualDescendants().Contains(confirm), "Transfer confirmation is mislabeled or misplaced.");
        string oldVault = f.Model.ObsidianVaultFolder; f.Model.ObsidianVaultFolder = Path.Combine(oldVault, "other"); Directory.CreateDirectory(f.Model.ObsidianVaultFolder);
        Click(confirm); Pump();
        Require(!f.Integration.GetStatus(f.Model).IsOwner && Text(f.Panel).Contains("changed"), "Stale confirmation transferred a different vault or hid the error.");
        f.Model.ObsidianVaultFolder = oldVault; Call(f.Panel, "RefreshStatus");
        Click(transfer); Click(cancel); Require(transfer.IsFocused, "Transfer cancel lost focus.");
        vault = JsonSerializer.Deserialize<HabiticaIntegration.Vault>(File.ReadAllText(metadata))!;
        vault.Owners[Account] = Guid.NewGuid(); File.WriteAllText(metadata, JsonSerializer.Serialize(vault)); Call(f.Panel, "RefreshStatus");
        Require(!f.Integration.GetStatus(f.Model).IsOwner, "Copied-vault fixture did not change owner.");
        Click(transfer); Click(cancel); Require(!f.Integration.GetStatus(f.Model).IsOwner, "Cancel took over the copied vault.");
        Click(transfer); Click(confirm); Pump(); Require(f.Integration.GetStatus(f.Model).IsOwner, "Confirmed transfer did not enable ownership here.");
        Click(Visual<Button>(f.Window, "HabiticaConnectionTab")); Click(Visual<Button>(f.Window, "HabiticaRecoveryTab"));
        f.Model.ObsidianVaultFolder = Path.Combine(oldVault, "empty-inventory"); Directory.CreateDirectory(f.Model.ObsidianVaultFolder);
        Call(f.Panel, "RefreshStatus"); Wait(() => !Field<bool>(f.Panel, "_recoveryCounting")); Pump(100);
        Require(!review.IsEnabled && Text(f.Panel).Contains("No existing items"), "Inventory from a previous vault enabled bulk sync in the empty vault.");
        f.Model.ObsidianVaultFolder = oldVault; Call(f.Panel, "RefreshStatus"); Wait(() => !Field<bool>(f.Panel, "_recoveryCounting"));
        foreach (string theme in new[] { AppThemeCatalog.Midnight, AppThemeCatalog.Nika })
        {
            f.Model.ThemeMode = theme; Desktop("ThemeResources", "ApplyApplication", theme); Call(f.Window, "ReloadFromSettings"); Call(f.Window, "UpdatePreviewSafely"); Pump();
        }
        OpenRecovery(f); Require(transfer.IsEffectivelyVisible && Field<StackPanel>(f.Panel, "_importProgress").IsEffectivelyVisible, "Theme restoration lost recovery actions or batch progress.");
        Click(Visual<Button>(f.Window, "SettingsNav_Application")); Pump();
        Require(!Field<Avalonia.Threading.DispatcherTimer>(f.Panel, "_refresh").IsEnabled && f.Http.Requests == 0, "Hidden recovery keeps polling or sent HTTP.");
    }
    internal static void RecoveryLegacyAvailability()
    {
        using var f = new Fixture(); f.Save(); Click(Field<CheckBox>(f.Panel, "_enabled")); OpenRecovery(f);
        var restore = Field<Button>(f.Panel, "_restoreMissing"); var transfer = Field<Button>(f.Panel, "_transferVault");
        Require(!restore.IsEnabled, "Empty navigator vault should disable recreation.");
        Click(Visual<Button>(f.Window, "HabiticaConnectionTab")); Field<TextBox>(f.Panel, "_token").Text = "unsaved-sample"; OpenRecovery(f);
        Require(!transfer.IsEnabled, "Unsaved navigator draft should disable transfer.");
        f.Model.ThemeMode = AppThemeCatalog.Midnight; Desktop("ThemeResources", "ApplyApplication", f.Model.ThemeMode);
        Call(f.Window, "ReloadFromSettings"); Call(f.Window, "UpdatePreviewSafely"); Pump(); Click(Field<Button>(f.Panel, "_saveConnection"));
        AddRecoveryItems(f, 1); f.Integration.ImportExisting(f.Model);
        string metadata = Path.Combine(f.Model.ObsidianVaultFolder, f.Model.NotesSubfolder, ".stopwatch-habitica.json");
        var vault = JsonSerializer.Deserialize<HabiticaIntegration.Vault>(File.ReadAllText(metadata))!;
        var target = vault.Items.Single().Targets[Account]; target.BlockKind = HabiticaErrorKind.NotFound; target.Block = "Missing task.";
        File.WriteAllText(metadata, JsonSerializer.Serialize(vault)); Call(f.Panel, "RefreshStatus");
        Visual<Expander>(f.Window, "HabiticaCopiedVaultOptions").IsExpanded = true; Pump(300);
        Require(restore.IsEffectivelyVisible && transfer.IsEffectivelyVisible && restore.IsEnabled && transfer.IsEnabled,
            $"Legacy actions retained navigator disabled states: restore={restore.IsEnabled}, transfer={transfer.IsEnabled}.");
        Click(restore); Pump(); Require(Field<StackPanel>(f.Panel, "_confirmPanel").IsEffectivelyVisible, "Legacy recovery confirmation was lost.");
    }
    internal static void RecoveryLayout()
    {
        foreach (int scale in new[] { 70, 90, 125 }) foreach (bool compact in new[] { false, true })
        {
            using var f = new Fixture(scale, compact); f.Save(); Click(Field<CheckBox>(f.Panel, "_enabled")); AddRecoveryItems(f); OpenRecovery(f);
            var scroll = Visual<ScrollViewer>(f.Window, "HabiticaScroll"); var done = Visual<Button>(f.Window, "SettingsDone");
            Field<Avalonia.Threading.DispatcherTimer>(f.Panel, "_refresh").Stop();
            var footer = done.TranslatePoint(default, f.Window); Capture("ready");
            Click(Field<Button>(f.Panel, "_syncExisting")); Pump(); Capture("confirmation");
            Click(Visual<Button>(f.Window, "CancelHabiticaActionButton"));
            foreach (string state in new[] { "progress", "paused", "blocked", "complete", "interrupted" })
            {
                f.Model.HabiticaEnabled = state != "paused";
                var p = state switch { "blocked" => new HabiticaImportProgress(12, 4, 6, 2, false),
                    "complete" => new(12, 0, 12, 0, false), "interrupted" => new(12, 4, 0, 0, false, "Queuing was interrupted. Queued changes are saved."), _ => new(12, 4, 8, 0, false) };
                var s = new HabiticaSyncStatus(state == "blocked" ? "Linked tasks need attention." : "", p.Queued + p.Blocked, null, true, p, DateTime.UtcNow.AddSeconds(25));
                typeof(StopwatchOverlay.Desktop.Controls.HabiticaSettingsPanel).GetMethod("UpdateImportProgress", Hidden)!.Invoke(f.Panel, new object[] { s });
                typeof(StopwatchOverlay.Desktop.Controls.HabiticaSettingsPanel).GetMethod("UpdateNavigatorStatus", Hidden)!.Invoke(f.Panel, new object[] { s });
                Require(Field<ProgressBar>(f.Panel, "_importProgressBar").Value == p.Completed, "Progress does not reflect acknowledged items.");
                Require(Field<Button>(f.Panel, "_syncExisting").IsVisible == (state is "complete" or "interrupted"), "Batch state permits a duplicate review or prevents reviewing a finished/interrupted batch: " + state);
                Require(Field<Button>(f.Panel, "_syncExisting").IsEnabled == (state is "complete" or "interrupted"), "Batch state enables a duplicate import: " + state);
                Capture(state);
            }
            f.Model.HabiticaEnabled = true; Call(f.Panel, "RefreshStatus"); Click(Visual<Button>(f.Window, "TransferHabiticaVaultButton")); Pump();
            Visual<Border>(f.Window, "HabiticaRecoveryTransfer").BringIntoView(); Pump(); Capture("transfer", false);
            if (compact && scale == 125)
            {
                f.Model.Typography.Global.SizePercent = 175; Desktop("DesktopTypography", "Apply", f.Model.Typography); Pump(80);
                Visual<Button>(f.Window, "CancelHabiticaActionButton").BringIntoView(); Pump();
                foreach (var b in new[] { Visual<Button>(f.Window, "ConfirmHabiticaActionButton"), Visual<Button>(f.Window, "CancelHabiticaActionButton") })
                {
                    var text = b.GetVisualDescendants().OfType<TextBlock>().Single(); var presenter = b.GetVisualDescendants().OfType<ContentPresenter>().Single();
                    Require(text.TextLayout.WidthIncludingTrailingWhitespace <= presenter.Bounds.Width + 1, "Enlarged confirmation caption clips.");
                }
                footer = done.TranslatePoint(default, f.Window); CheckFit(); Save(f.Window, "large-text", RecoveryOutput);
            }
            void CheckFit() => Require(scroll.Viewport.Height > 65 && scroll.Extent.Width <= scroll.Viewport.Width + 1 && done.TranslatePoint(default, f.Window) == footer, "Recovery overflow or moving footer.");
            void Capture(string name, bool top = true)
            {
                if (top) scroll.Offset = default; Pump(50); CheckFit();
                if (scale == 90) Save(f.Window, (compact ? "compact-" : "") + name, RecoveryOutput);
            }
        }
    }
}
