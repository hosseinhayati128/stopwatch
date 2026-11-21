using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;
using IconPath = Avalonia.Controls.Shapes.Path;

namespace StopwatchOverlay.Desktop.Views;

public partial class TodoOverlayWindow
{
    private enum NoticeState { Current, Pending, Failed }
    private sealed record Notice(NoticeState State, string Title, string? Detail = null);
    private Notice? _localNotice, _telegramNotice;
    private string? _telegramHint;
    private (Notice? Notice, bool Pixel, double Zoom, bool Dark) _renderedNotice;
    private (bool Enabled, string Bot, string Chat, string Vault, string Folder, string Account) NoticeContext() =>
        (_settings.TelegramEnabled, _settings.TelegramBotToken, _settings.TelegramChatId,
            _settings.ObsidianVaultFolder, _settings.NotesSubfolder, DetailAccount());
    private (bool Enabled, string Bot, string Chat, string Vault, string Folder, string Account) _noticeContext;

    private void ShowError(string message, string title = "Todo needs attention")
    {
        _localNotice = new(NoticeState.Failed, title, message);
        RefreshNotice();
    }

    internal void ShowSaveResult(string? warning = null)
    {
        if (_closed) return;
        _localNotice = warning == null ? null : new(NoticeState.Pending, "Saved locally", warning);
        RefreshNotice();
    }

    private (HashSet<NoteEntry> Linked, bool Pending) ReadTelegramState(NoteEntry[] entries, bool configured)
    {
        var links = new HashSet<NoteEntry>();
        if (!_settings.TelegramEnabled || !configured) return (links, false);
        string bot = TelegramNotesSync.GetBotIdentity(_settings), vault = TelegramNotesSync.GetTodoVaultKey(_settings);
        var receipts = TelegramOutboxStore.GetTrackedTodos(bot, vault).ToLookup(r => r.TimestampMinute);
        bool pending = false;
        foreach (var entry in entries)
        {
            var matches = receipts[entry.Timestamp.ToString("yyyy-MM-dd HH:mm")]
                .Where(r => TelegramNotesSync.MatchesTodo(r, entry, _organization)).Take(2).ToArray();
            if (matches.Length != 1) continue;
            links.Add(entry);
            if (pending) continue;
            var receipt = matches[0];
            pending = receipt.PendingCompleted.HasValue || receipt.IsCompleted != entry.IsCompleted ||
                (receipt.ContentHash ?? TelegramNotesSync.TodoContentHash(receipt.Text, null)) !=
                TelegramNotesSync.TodoContentHash(entry.Text, _organization?.GetDueAt(entry));
        }
        // A first delivery has an outbox entry before it has a Telegram receipt.
        if (!pending)
        {
            foreach (var item in TelegramOutboxStore.GetAll().Where(i => i.NoteType == NoteType.Todo && i.BotIdentity == bot && i.VaultKey == vault))
            {
                var identity = new TelegramSentNoteRecord(NoteType.Todo, item.Text, item.Timestamp.ToString("yyyy-MM-dd HH:mm"), default, VaultKey: vault);
                if (!entries.Any(e => TelegramNotesSync.MatchesTodo(identity, e, _organization)) ||
                    TelegramOutboxStore.IsDeletedTodo(bot, vault, item.Text, item.Timestamp)) continue;
                pending = true;
                break;
            }
        }
        return (links, pending);
    }

    private void UpdateTelegramNotice(bool configured, bool pending, bool unlinked)
    {
        _telegramHint = null;
        _telegramNotice = null;
        if (_settings.TelegramEnabled)
        {
            if (!configured) _telegramHint = _syncError ?? "Choose a notes folder and configure Telegram in Settings.";
            else if (pending) _telegramNotice = new(NoticeState.Pending, "Saved locally",
                _syncError ?? "Telegram sync is pending. Stopwatch will retry automatically.");
            else if (_syncError != null) _telegramHint = _syncError;
            else if (unlinked) _telegramHint = "Right-click a task → Link to connect older Telegram messages.";
            else _telegramNotice = new(NoticeState.Current, "Telegram sync is up to date");
        }
        RefreshNotice();
    }

    private void RefreshNotice()
    {
        var notice = IsNikaTodo && _linkEntry == null ? _localNotice ?? _telegramNotice : null;
        ErrorMessage.Text = _localNotice?.Detail;
        ErrorMessage.IsVisible = !IsNikaTodo && _localNotice != null;
        TelegramSyncStatus.Text = _telegramHint ?? _telegramNotice?.Detail ?? _telegramNotice?.Title;
        TelegramSyncStatus.IsVisible = _settings.TelegramEnabled && _linkEntry == null && notice == null && TelegramSyncStatus.Text != null;
        bool pixel = _settings.TodoAppearance.IsPixel, dark = TodoLook.Background == "midnight-chart";
        double zoom = AppUiScale.Normalize(_settings.UiScalePercent) / 100;
        if (_renderedNotice == (notice, pixel, zoom, dark)) return;
        _renderedNotice = (notice, pixel, zoom, dark);
        TodoSaveNotice.Content = notice == null ? null : BuildNotice(notice, pixel, zoom, dark);
        TodoSaveNotice.Offset = default;
        TodoSaveNotice.IsVisible = notice != null;
        AutomationProperties.SetName(TodoSaveNotice, notice == null ? "" : (notice.Title + " " + notice.Detail).TrimEnd());
    }

    private static TodoFrame BuildNotice(Notice notice, bool pixel, double zoom, bool dark)
    {
        bool quiet = notice.State == NoticeState.Current;
        bool failed = notice.State == NoticeState.Failed;
        var ink = Brush.Parse(quiet ? (dark ? "#ACDBB0" : "#28563F") : failed ? "#872E28" : "#70430D");
        var icon = new Canvas { Width = 20, Height = 20 };
        string glyph = notice.State switch {
            NoticeState.Current => pixel ? "M2,10 H4 V12 H6 V14 H8 V12 H10 V10 H12 V8 H14 V6 H16 V4 H18" : "M2.5,10 L7.2,14.6 L17.5,4.6",
            NoticeState.Pending => (pixel ? "M6,2 H14 V4 H17 V7 H18 V13 H16 V16 H13 V18 H7 V16 H4 V14 H2 V6 H4 V4 H6 Z" :
                "M10,2 A8,8 0 1 1 10,18 A8,8 0 1 1 10,2 Z") + " M10,5.5 V10.5 H14",
            _ => (pixel ? "M9,2 H11 V5 H13 V8 H15 V11 H17 V14 H19 V17 H1 V14 H3 V11 H5 V8 H7 V5 H9 Z" :
                "M10,2 L19,17 H1 Z") + " M10,7.5 V11 M10,14 V14.2"
        };
        icon.Children.Add(new IconPath { Data = Geometry.Parse(glyph), Stroke = ink, StrokeThickness = pixel ? 1.7 : 1.85,
            StrokeLineCap = pixel ? PenLineCap.Square : PenLineCap.Round, StrokeJoin = pixel ? PenLineJoin.Miter : PenLineJoin.Round });
        double iconSize = Math.Max(15, (quiet ? 17 : 21) * zoom);
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        layout.Children.Add(new Viewbox { Child = icon, Width = iconSize, Height = iconSize,
            Margin = new Thickness(0, quiet ? 0 : zoom, 8 * zoom, 0), VerticalAlignment = quiet ? VerticalAlignment.Center : VerticalAlignment.Top });
        var copy = new StackPanel { Spacing = 3 * zoom, VerticalAlignment = VerticalAlignment.Center };
        copy.Children.Add(new TextBlock { Text = notice.Title,
            FontFamily = TodoVisuals.BodyFont, FontSize = Math.Max(12, (quiet ? 14 : 16) * zoom),
            FontWeight = quiet ? FontWeight.Medium : FontWeight.SemiBold, Foreground = ink, TextWrapping = TextWrapping.Wrap });
        if (!quiet)
            copy.Children.Add(new TextBlock { Text = notice.Detail,
                FontFamily = TodoVisuals.BodyFont, FontSize = Math.Max(12, 14 * zoom), Foreground = TodoVisuals.Navy, TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(copy, 1);
        layout.Children.Add(copy);
        return new TodoFrame { Pixel = pixel, Child = layout, CornerRadius = new CornerRadius(6 * zoom), Margin = new Thickness(0, 7 * zoom, 0, 0),
            Padding = quiet ? new Thickness(3 * zoom, 2 * zoom) : new Thickness(10 * zoom, 8 * zoom),
            BorderThickness = new Thickness(quiet ? 0 : Math.Max(1, (pixel ? 2 : 1) * zoom)),
            BorderBrush = quiet ? null : Brush.Parse(failed ? "#AF6252" : "#AA7A37"),
            Background = quiet ? null : Brush.Parse(failed ? "#FDE4D5" : "#FFF0C9") };
    }
}
