using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class SettingsWindow
{
    private ScrollViewer? _telegramPage;
    private Grid? _telegramCards;
    private Button? _telegramReveal;
    private TextBlock? _telegramConnectionTitle, _telegramTestHint;
    private Border? _telegramConnectionCard, _telegramDeliveryCard;
    private bool? _telegramStacked;
    private readonly List<(Control Control, Panel Parent, int Index, Thickness Margin, double Height,
        int Column, int Row, object? Content, Thickness Padding)> _telegramMoved = new();

    private void BuildTelegramPage(Grid host)
    {
        foreach (var control in new Control[] { TelegramEnabledCheck, TelegramBotTokenTextBox, TelegramChatIdTextBox,
            TelegramNotesTopicTextBox, TelegramTodosTopicTextBox, TelegramRemindersTopicTextBox,
            TestTelegramButton, TelegramStatusText, TelegramOutboxStatusText, TelegramOutboxDetailText,
            SendPendingNotesButton, ScanVaultNotesButton })
        {
            var parent = (Panel)control.Parent!;
            _telegramMoved.Add((control, parent, parent.Children.IndexOf(control), control.Margin, control.Height,
                Grid.GetColumn(control), Grid.GetRow(control), (control as ContentControl)?.Content,
                (control as TemplatedControl)?.Padding ?? default));
        }
        foreach (var item in _telegramMoved)
        {
            item.Parent.Children.Remove(item.Control); item.Control.Margin = default;
            Grid.SetColumn(item.Control, 0); Grid.SetRow(item.Control, 0);
        }
        ObsidianChoice(TelegramEnabledCheck, "Enable Telegram sync", "Send notes, todos, and reminders to Telegram.");
        foreach (var field in new[] { TelegramBotTokenTextBox, TelegramChatIdTextBox, TelegramNotesTopicTextBox,
            TelegramTodosTopicTextBox, TelegramRemindersTopicTextBox })
        {
            field.Classes.Add("editorField"); field.Height = double.NaN; field.Padding = new(10, 6);
        }
        AutomationProperties.SetName(TelegramBotTokenTextBox, "Bot token");
        AutomationProperties.SetName(TelegramChatIdTextBox, "Group or chat ID");
        TelegramBotTokenTextBox.Watermark = "Bot token from @BotFather";
        TelegramChatIdTextBox.Watermark = "Group or chat ID";
        _telegramReveal = SettingsAction("Show", "TelegramRevealToken");
        _telegramReveal.MinWidth = 60; _telegramReveal.Height = 36;
        _telegramReveal.Click += (_, _) => SetTelegramTokenVisible(TelegramBotTokenTextBox.PasswordChar != '\0');
        SetTelegramTokenVisible(false);
        var tokenRow = new Grid { ColumnDefinitions = new("*,8,Auto"), Children = { TelegramBotTokenTextBox, _telegramReveal } };
        Grid.SetColumn(_telegramReveal, 2);
        foreach (var button in new[] { TestTelegramButton, SendPendingNotesButton, ScanVaultNotesButton })
        {
            NavigatorFormStyles.StyleButton(button, button != ScanVaultNotesButton);
            button.Height = 36; button.Padding = new(12, 0);
        }
        TestTelegramButton.Content = "Test connection"; TestTelegramButton.HorizontalAlignment = HorizontalAlignment.Left;
        SendPendingNotesButton.Content = "Send pending"; ScanVaultNotesButton.Content = "Scan vault";
        _telegramConnectionTitle = SettingsLabel("", 13);
        _telegramConnectionCard = TelegramStatusCard(_telegramConnectionTitle, TelegramStatusText);
        _telegramTestHint = SettingsLabel("", 12);
        var connection = TelegramCard("Connection", TelegramEnabledCheck, ObsidianField("Bot token", tokenRow),
            ObsidianField("Group / chat ID", TelegramChatIdTextBox), SettingsLabel("Add your bot to the group before testing.", 12),
            TestTelegramButton, _telegramTestHint, _telegramConnectionCard);
        var topics = new StackPanel { Spacing = 8 };
        foreach (var (title, field) in new[] { ("Quick notes", TelegramNotesTopicTextBox),
            ("Todos", TelegramTodosTopicTextBox), ("Reminders", TelegramRemindersTopicTextBox) })
        {
            field.Watermark = "Main chat"; AutomationProperties.SetName(field, title + " topic ID");
            var row = new Grid { ColumnDefinitions = new("*,12,130"), Children = { SettingsLabel(title, 13, SettingsInk), field } };
            Grid.SetColumn(field, 2); topics.Children.Add(row);
        }
        var topicCard = TelegramCard("Topics", SettingsLabel("Choose a topic ID for each type of message.", 12), topics,
            SettingsLabel("Leave blank to use General / the main chat.", 12));
        _telegramDeliveryCard = TelegramStatusCard(TelegramOutboxStatusText, TelegramOutboxDetailText);
        SendPendingNotesButton.Margin = ScanVaultNotesButton.Margin = new(0, 0, 8, 0);
        var actions = new WrapPanel { Orientation = Orientation.Horizontal, Children = { SendPendingNotesButton, ScanVaultNotesButton } };
        var delivery = TelegramCard("Delivery", _telegramDeliveryCard, actions,
            SettingsLabel("Scan your vault’s last 3 days. Review unsent notes before sending.", 12));
        var right = new StackPanel { Spacing = 14, Children = { topicCard, delivery } };
        _telegramCards = new Grid { Children = { connection, right } };
        var heading = new StackPanel { Spacing = 4, Margin = new(0, 0, 0, 16), Children =
        { SettingsLabel("Telegram", 23, SettingsInk), SettingsLabel("Connect your notes and keep delivery in view.", 12) } };
        _telegramPage = new ScrollViewer { Name = "TelegramScroll", HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new(0, 0, 5, 0),
            Content = new StackPanel { Children = { heading, _telegramCards } } };
        _telegramPage.Styles.Add(new Style(s => s.OfType<TextBox>().Class("editorField")) { Setters =
        {
            new Setter(FontFamilyProperty, ClockPickerPresentation.Body), new Setter(FontSizeProperty, 13d),
            new Setter(MinWidthProperty, 0d), new Setter(MinHeightProperty, 36d)
        } });
        foreach (var text in new[] { TelegramStatusText, TelegramOutboxStatusText, TelegramOutboxDetailText }) text.Classes.Add("telegramFeedback");
        _telegramPage.Styles.Add(new Style(s => s.OfType<TextBlock>().Class("telegramFeedback")) { Setters =
        {
            new Setter(TextBlock.FontFamilyProperty, ClockPickerPresentation.Body), new Setter(TextBlock.FontSizeProperty, 12d),
            new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap)
        } });
        _telegramPage.SizeChanged += (_, _) => ResizeTelegramPage();
        heading.SizeChanged += (_, _) => ResizeTelegramPage();
        host.Children.Add(_telegramPage); ApplyTelegramFeedback();
    }

    private void ResizeTelegramPage()
    {
        if (_closed || _telegramPage == null || _telegramPage.Bounds.Width <= 0) return;
        bool stacked = _telegramPage.Bounds.Width < 720 * _settings.Typography.Resolve("Settings").SizePercent / 100;
        if (_telegramStacked == stacked) return;
        _telegramStacked = stacked;
        _telegramCards!.ColumnDefinitions = new(stacked ? "*" : "1.05*,16,1*");
        _telegramCards.RowDefinitions = new(stacked ? "Auto,14,Auto" : "Auto");
        Grid.SetColumn(_telegramCards.Children[1], stacked ? 0 : 2); Grid.SetRow(_telegramCards.Children[1], stacked ? 2 : 0);
    }

    private void RestoreTelegramPage()
    {
        SetTelegramTokenVisible(false);
        if (_telegramPage == null) return;
        foreach (var item in _telegramMoved.OrderBy(item => item.Index))
        {
            if (item.Control.Parent is Panel panel) panel.Children.Remove(item.Control);
            item.Control.Margin = item.Margin; item.Control.Height = item.Height;
            Grid.SetColumn(item.Control, item.Column); Grid.SetRow(item.Control, item.Row);
            item.Control.Classes.Remove("editorField"); item.Control.Classes.Remove("telegramFeedback");
            if (item.Control is ContentControl content) content.Content = item.Content;
            if (item.Control is TemplatedControl templated) templated.Padding = item.Padding;
            if (item.Control is TextBox field) field.ClearValue(TextBox.WatermarkProperty);
            if (item.Control is CheckBox check)
            {
                check.ClearValue(HorizontalContentAlignmentProperty); check.ClearValue(VerticalContentAlignmentProperty);
            }
            else if (item.Control is Button button)
            {
                button.Classes.Remove("editorAction"); button.Classes.Remove("editorPrimary"); button.Classes.Add("modern");
                button.ClearValue(FontFamilyProperty); button.ClearValue(MinWidthProperty); button.ClearValue(MinHeightProperty);
                button.ClearValue(HorizontalAlignmentProperty);
            }
            item.Parent.Children.Insert(item.Index, item.Control);
        }
        ((Panel)_telegramPage.Parent!).Children.Remove(_telegramPage);
        _telegramMoved.Clear(); _telegramPage = null; _telegramCards = null; _telegramStacked = null;
        _telegramReveal = null; _telegramConnectionTitle = null; _telegramTestHint = null;
        _telegramConnectionCard = null; _telegramDeliveryCard = null;
        ApplyTelegramFeedback();
    }

    private void SetTelegramTokenVisible(bool visible)
    {
        TelegramBotTokenTextBox.PasswordChar = visible ? '\0' : '●';
        if (_telegramReveal == null) return;
        _telegramReveal.Content = visible ? "Hide" : "Show";
        AutomationProperties.SetName(_telegramReveal, visible ? "Hide bot token" : "Show bot token");
    }

    private static Border TelegramCard(string title, params Control[] controls)
    {
        var body = new StackPanel { Spacing = 10, Children = { SettingsLabel(title, 18, SettingsInk) } };
        foreach (var control in controls) body.Children.Add(control);
        var card = SettingsCard(body); card.Padding = new(16); card.VerticalAlignment = VerticalAlignment.Top; return card;
    }

    private static Border TelegramStatusCard(TextBlock title, TextBlock detail) => new()
    { BorderThickness = new(1), CornerRadius = new(5), Padding = new(12), Child = new StackPanel { Spacing = 4, Children = { title, detail } } };
}
