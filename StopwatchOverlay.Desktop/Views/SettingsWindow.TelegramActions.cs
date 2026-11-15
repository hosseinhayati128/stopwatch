using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace StopwatchOverlay.Desktop.Views;

public partial class SettingsWindow
{
    private readonly HttpClient? _telegramClient;
    private (bool Enabled, string Token, string Chat, string Notes, string Todos, string Reminders, string Vault, string Folder)? _telegramConfiguration;
    private int _telegramRevision, _telegramPendingCount = -1;
    private bool _telegramBusy, _telegramConnectionFailed, _telegramDeliveryFailed;
    private string _telegramConnectionHeading = "", _telegramDeliveryMessage = "";

    private AppSettings CaptureTelegramSettings() => new()
    {
        TelegramEnabled = _settings.TelegramEnabled, TelegramBotToken = _settings.TelegramBotToken,
        TelegramChatId = _settings.TelegramChatId, TelegramNotesTopicId = _settings.TelegramNotesTopicId,
        TelegramTodosTopicId = _settings.TelegramTodosTopicId, TelegramRemindersTopicId = _settings.TelegramRemindersTopicId,
        ObsidianVaultFolder = _settings.ObsidianVaultFolder, NotesSubfolder = _settings.NotesSubfolder
    };

    private void RefreshTelegramConfiguration()
    {
        var configuration = (_settings.TelegramEnabled, _settings.TelegramBotToken, _settings.TelegramChatId,
            _settings.TelegramNotesTopicId, _settings.TelegramTodosTopicId, _settings.TelegramRemindersTopicId,
            _settings.ObsidianVaultFolder, _settings.NotesSubfolder);
        if (_telegramConfiguration != configuration)
        {
            _telegramConfiguration = configuration; _telegramRevision++;
            SetTelegramTokenVisible(false);
            bool token = !string.IsNullOrWhiteSpace(_settings.TelegramBotToken);
            _telegramConnectionHeading = token ? "Ready to test" : "Add your bot";
            TelegramStatusText.Text = token ? "Check the bot and your chosen destination." : "Enter a bot token and destination to get started.";
            _telegramConnectionFailed = _telegramDeliveryFailed = false; _telegramDeliveryMessage = "";
        }
        UpdateTelegramOutboxUi();
    }

    private void UpdateTelegramActions()
    {
        bool token = !string.IsNullOrWhiteSpace(_settings.TelegramBotToken);
        bool destination = token && !string.IsNullOrWhiteSpace(_settings.TelegramChatId);
        TestTelegramButton.IsEnabled = !_telegramBusy && token;
        SendPendingNotesButton.IsEnabled = !_telegramBusy && destination && _settings.TelegramEnabled && TelegramOutboxStore.PendingCount > 0;
        ScanVaultNotesButton.IsEnabled = !_telegramBusy && destination && !string.IsNullOrWhiteSpace(_settings.ObsidianVaultFolder);
        if (_telegramTestHint != null) _telegramTestHint.Text = string.IsNullOrWhiteSpace(_settings.TelegramChatId)
            ? "Checks the bot token. Add a chat ID to test delivery." : "Sends a test message to the Quick notes destination.";
    }

    private string TelegramSafeMessage(string message, string operationToken)
    {
        foreach (string token in new[] { operationToken, _settings.TelegramBotToken })
            if (!string.IsNullOrEmpty(token)) message = message.Replace(token, "[hidden token]", StringComparison.Ordinal);
        return message;
    }

    private bool TelegramResultIsCurrent(int revision) => !_closed && revision == _telegramRevision;

    public async void TestTelegramButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_closed || _telegramBusy || !TestTelegramButton.IsEnabled) return;
        var settings = CaptureTelegramSettings(); int revision = _telegramRevision;
        _telegramBusy = true; _telegramConnectionHeading = "Testing connection"; _telegramConnectionFailed = false;
        TelegramStatusText.Text = "Checking Telegram…"; ApplyTelegramFeedback();
        try
        {
            var result = await TelegramNotesSync.TestConnectionAsync(settings, _telegramClient);
            if (!TelegramResultIsCurrent(revision)) return;
            _telegramConnectionHeading = result.Success ? "Last test succeeded" : "Connection test failed";
            _telegramConnectionFailed = !result.Success;
            TelegramStatusText.Text = TelegramSafeMessage(result.Message, settings.TelegramBotToken);
        }
        catch (Exception ex)
        {
            if (!TelegramResultIsCurrent(revision)) return;
            _telegramConnectionHeading = "Connection test failed"; _telegramConnectionFailed = true;
            TelegramStatusText.Text = TelegramSafeMessage(ex.Message, settings.TelegramBotToken);
        }
        finally { _telegramBusy = false; if (!_closed) UpdateTelegramOutboxUi(); }
    }

    private void UpdateTelegramOutboxUi()
    {
        if (_closed) return;
        int count = TelegramOutboxStore.PendingCount;
        if (!_telegramBusy && count != _telegramPendingCount && (!_telegramDeliveryFailed || count == 0))
        { _telegramDeliveryMessage = ""; _telegramDeliveryFailed = false; }
        _telegramPendingCount = count;
        TelegramOutboxStatusText.Text = count == 0 ? "All caught up" : $"{count} item{(count == 1 ? "" : "s")} pending";
        TelegramOutboxDetailText.Text = _telegramDeliveryMessage.Length > 0 ? _telegramDeliveryMessage : count == 0
            ? "No items waiting to be sent." : !_settings.TelegramEnabled ? "Sync is off. Enable it to send pending items."
            : "Items stay saved locally until delivery succeeds.";
        ApplyTelegramFeedback();
    }

    private void ApplyTelegramFeedback()
    {
        UpdateTelegramActions();
        TelegramStatusText.Foreground = _telegramPage != null ? SettingsMuted : _telegramConnectionFailed ? Brushes.OrangeRed : Brushes.DeepSkyBlue;
        TelegramOutboxDetailText.Foreground = _telegramPage != null ? SettingsMuted : _telegramDeliveryFailed ? Brushes.OrangeRed : Brushes.DeepSkyBlue;
        TelegramOutboxStatusText.Foreground = _telegramPage != null ? (_telegramDeliveryFailed ? ObsidianError : ObsidianTeal) : TelegramOutboxDetailText.Foreground;
        if (_telegramConnectionTitle != null)
        {
            _telegramConnectionTitle.Text = _telegramConnectionHeading;
            _telegramConnectionTitle.Foreground = _telegramConnectionFailed ? ObsidianError : ObsidianTeal;
        }
        Tint(_telegramConnectionCard, _telegramConnectionFailed); Tint(_telegramDeliveryCard, _telegramDeliveryFailed);
        static void Tint(Border? card, bool failed)
        {
            if (card == null) return;
            card.Background = Brush.Parse(failed ? "#11913F32" : "#112D7169");
            card.BorderBrush = Brush.Parse(failed ? "#55913F32" : "#552D7169");
        }
    }

    private void OnOutboxChanged()
    {
        if (!_closed) Dispatcher.UIThread.Post(UpdateTelegramOutboxUi);
    }

    private void SetTelegramDelivery(string message, bool failed = false)
    {
        _telegramDeliveryMessage = message; _telegramDeliveryFailed = failed;
        _telegramPendingCount = TelegramOutboxStore.PendingCount; UpdateTelegramOutboxUi();
    }

    private async void SendPendingNotesButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_closed || _telegramBusy || !SendPendingNotesButton.IsEnabled) return;
        var settings = CaptureTelegramSettings(); int revision = _telegramRevision;
        _telegramBusy = true; SetTelegramDelivery("Sending pending items…");
        try
        {
            var (sent, remaining, lastError) = await TelegramNotesSync.FlushOutboxAsync(settings, _telegramClient);
            if (TelegramResultIsCurrent(revision)) SetTelegramDelivery(
                TelegramSafeMessage($"Last send: {sent} sent." + (lastError == null ? $" {remaining} remaining." : " " + lastError), settings.TelegramBotToken), lastError != null);
        }
        catch (Exception ex)
        {
            if (TelegramResultIsCurrent(revision)) SetTelegramDelivery(TelegramSafeMessage("Delivery failed: " + ex.Message, settings.TelegramBotToken), true);
        }
        finally { _telegramBusy = false; if (!_closed) UpdateTelegramOutboxUi(); }
    }

    private async void ScanVaultNotesButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_closed || _telegramBusy || !ScanVaultNotesButton.IsEnabled) return;
        var settings = CaptureTelegramSettings(); int revision = _telegramRevision;
        _telegramBusy = true; SetTelegramDelivery("Scanning your vault’s last 3 days…");
        try
        {
            var unsentNotes = TelegramNotesSync.GetUnsentVaultNotes(settings, lookbackDays: 3);
            if (unsentNotes.Count == 0)
            {
                SetTelegramDelivery("No unsent notes found in your vault from the last 3 days.");
                await ConfirmationDialogWindow.ShowAsync(this, "Vault Scan Complete", "Scan Result", _telegramDeliveryMessage, "OK");
                return;
            }
            var sb = new StringBuilder();
            sb.AppendLine($"Found {unsentNotes.Count} note(s) from the last 3 days that are not in your Telegram sent history:\n");
            foreach (var note in unsentNotes.Take(5))
            {
                string preview = note.Text.Length > 60 ? note.Text[..57] + "..." : note.Text;
                sb.AppendLine($"• [{note.Type}] {note.Timestamp:yyyy-MM-dd HH:mm} - {preview}");
            }
            if (unsentNotes.Count > 5) sb.AppendLine($"... and {unsentNotes.Count - 5} more.");
            sb.AppendLine("\nWould you like to add these notes to the Telegram outbox and send them now?");
            SetTelegramDelivery($"{unsentNotes.Count} unsent note(s) found. Review before sending.");
            bool confirmed = await ConfirmationDialogWindow.ShowAsync(this, "Unsent Notes Found", "Sync Notes to Telegram", sb.ToString(), "Send Now");
            // A confirmation belongs to the credentials and vault that produced its preview.
            if (!TelegramResultIsCurrent(revision)) return;
            if (!confirmed) { SetTelegramDelivery("Scan canceled. No notes were queued or sent."); return; }
            int queued = TelegramNotesSync.QueueVaultNotes(unsentNotes);
            SetTelegramDelivery("Sending scanned notes…");
            var (sent, remaining, lastError) = await TelegramNotesSync.FlushOutboxAsync(settings, _telegramClient);
            if (!TelegramResultIsCurrent(revision)) return;
            SetTelegramDelivery(TelegramSafeMessage($"Last scan: {queued} queued, {sent} sent." +
                (lastError == null ? $" {remaining} remaining." : " " + lastError), settings.TelegramBotToken), lastError != null);
            await ConfirmationDialogWindow.ShowAsync(this, "Notes Queued & Sent", "Delivery Status", _telegramDeliveryMessage, "OK", destructive: remaining > 0);
        }
        catch (Exception ex)
        {
            if (!TelegramResultIsCurrent(revision)) return;
            SetTelegramDelivery(TelegramSafeMessage("Failed to scan vault: " + ex.Message, settings.TelegramBotToken), true);
            await ConfirmationDialogWindow.ShowAsync(this, "Error", "Scan Failed", _telegramDeliveryMessage, "OK", destructive: true);
        }
        finally { _telegramBusy = false; if (!_closed) UpdateTelegramOutboxUi(); }
    }
}
