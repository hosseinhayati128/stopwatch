using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class TodoTelegramLinkEditor : UserControl
{
    public TodoTelegramLinkEditor()
    {
        InitializeComponent();
        Reference.TemplateApplied += (_, e) =>
        {
            // The surrounding TodoFrame supplies the field's fill and validation outline.
            if (e.NameScope.Find<Border>("PART_BorderElement") is { } border)
            {
                border.Background = Brushes.Transparent;
                border.BorderThickness = default;
            }
            if (e.NameScope.Find<TextBlock>("PART_Watermark") is { } watermark)
            {
                watermark.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("LinkPlaceholderInk"));
                watermark.Opacity = 1;
            }
        };
    }

    internal void Configure(AppSettings settings, NoteEntry entry, int priority, TelegramSentNoteRecord? current)
    {
        var look = settings.TodoAppearance.Current;
        bool pixel = settings.TodoAppearance.IsPixel;
        bool dark = look.Background == "midnight-chart";
        TaskFrame.Pixel = InputFrame.Pixel = pixel;
        TaskFrame.Background = dark ? TodoVisuals.Paper(look) : Brush.Parse("#FBEBCD");
        InputFrame.Background = dark ? TodoVisuals.Paper(look) : Brush.Parse("#FFF4DB");
        Resources["LinkDisabledInk"] = Brush.Parse(dark ? "#C6D1CF" : "#55482F");
        Resources["LinkPlaceholderInk"] = Brush.Parse(dark ? "#C6D1CF" : "#65573F");
        TodoVisuals.PriorityBadge(Rank, priority, look);
        Rank.Margin = default;
        TaskName.Text = string.Concat(FormattedTodoText.Parse(TodoDetails.Parse(entry.Text).Body).Select(part => part.Text));
        ToolTip.SetTip(TaskName, TaskName.Text);
        Title.Text = current == null ? "Link Telegram message" : "Relink Telegram message";
        BindingRow.IsVisible = current != null;
        CurrentLink.Text = current == null ? "" : $"{current.ChatId} · #{current.MessageId}";
        ToolTip.SetTip(CurrentLink, CurrentLink.Text);
        FieldLabel.Text = current == null ? "Message link or ID" : "New message link or ID";
        SaveAction.Content = current == null ? "Link message" : "Replace link";
        TodoVisuals.StyleButton(CancelAction, look, pixel: pixel);
        TodoVisuals.StyleButton(SaveAction, look, primary: true, pixel: pixel);
    }

    internal void UpdateFeedback(AppSettings settings, string? saveError = null)
    {
        var look = settings.TodoAppearance.Current;
        string text = Reference.Text ?? "";
        var target = string.IsNullOrWhiteSpace(text) ? null : TelegramNotesSync.ParseTodoMessageReference(settings, text);
        string? error = saveError ?? target?.ErrorMessage;
        Hint.Text = error ?? "A message ID uses your configured Todo chat.";
        Hint.Foreground = InputFrame.BorderBrush = error == null ? TodoVisuals.Ink(look) :
            Brush.Parse(look.Background == "midnight-chart" ? "#FFAAA0" : "#8F2929");
        bool enabled = target?.Success == true;
        SaveAction.IsEnabled = enabled;
        SaveAction.Background = enabled ? TodoVisuals.Navy : Brush.Parse(look.Background == "midnight-chart" ? "#244050" : "#E6D3AC");
        SaveAction.Foreground = enabled ? TodoVisuals.Ivory : (IBrush)Resources["LinkDisabledInk"]!;
        SaveAction.BorderBrush = enabled ? TodoVisuals.Gold : TodoVisuals.Brass;
        if (enabled) SaveAction.ClearValue(OpacityProperty);
        else SaveAction.Opacity = 1;
    }
}
