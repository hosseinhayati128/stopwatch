using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using StopwatchOverlay.Desktop.Views;

namespace StopwatchOverlay.Desktop.Controls;

public sealed partial class HabiticaSettingsPanel
{
    private StackPanel? _legacyContent, _navigatorRoot;
    private Grid? _navigatorCards;
    private Expander? _navigatorRecovery;
    private Button? _navigatorReveal;
    private TextBlock? _navigatorFeedbackTitle, _navigatorCredentialTitle, _navigatorCredentialDetail,
        _navigatorSyncTitle, _navigatorSyncDetail, _navigatorPending, _navigatorLastSync;
    private Border? _navigatorFeedbackCard;
    private bool? _navigatorStacked;
    private readonly List<(Control Control, Panel Parent, int Index, Thickness Margin)> _navigatorMoved = new();
    private static readonly IBrush Ink = Brush.Parse("#102B3B"), Muted = Brush.Parse("#655539"), Rule = Brush.Parse("#509A7B48"),
        Teal = Brush.Parse("#2D7169"), Error = Brush.Parse("#913F32"), Amber = Brush.Parse("#865C15");

    internal void SetNavigatorLayout(bool enabled)
    {
        HideConnectionDraft();
        if (!enabled) { RestoreNavigatorLayout(); return; }
        if (_navigatorRoot != null) return;
        _legacyContent = (StackPanel)Child!;
        var recovery = _legacyContent.Children.Skip(_legacyContent.Children.IndexOf(_syncExisting)).TakeWhile(c => c != _feedback).ToArray();
        foreach (var control in new Control[] { _account, _client, _token, _saveConnection, _test, _enabled, _feedback }.Concat(recovery))
        {
            var parent = (Panel)control.Parent!;
            _navigatorMoved.Add((control, parent, parent.Children.IndexOf(control), control.Margin));
        }
        foreach (var item in _navigatorMoved) item.Parent.Children.Remove(item.Control);
        _token.InnerRightContent = null;
        foreach (var field in new[] { _account, _client, _token }) field.Classes.Add("editorField");
        AutomationProperties.SetName(_account, "Habitica User ID"); AutomationProperties.SetName(_client, "Public client identifier");
        AutomationProperties.SetName(_token, "Habitica API token");
        _navigatorReveal = new Button { Name = "HabiticaRevealToken", Content = "Show", MinWidth = 60 };
        _navigatorReveal.Click += (_, _) => SetTokenVisible(_token.PasswordChar != '\0');
        foreach (var button in new[] { _saveConnection, _test, _pasteToken, _navigatorReveal })
        {
            NavigatorFormStyles.StyleButton(button, button == _saveConnection);
            button.Classes.Add("habiticaAction"); button.MinHeight = 36;
        }
        _navigatorReveal.MinWidth = 60;
        SetTokenVisible(false);
        var tokenRow = new Grid { ColumnDefinitions = new("*,8,Auto,8,Auto"), Children = { _token, _pasteToken, _navigatorReveal } };
        Grid.SetColumn(_pasteToken, 2); Grid.SetColumn(_navigatorReveal, 4);
        _saveConnection.Margin = _test.Margin = new(0, 0, 8, 4);
        var actions = new WrapPanel { Children = { _saveConnection, _test } };
        var connection = Card("Connection", Field("User ID", _account), Field("Public client identifier", _client),
            Text("Author’s Habitica User ID + -StopwatchOverlay. This is a public app identifier.", 12),
            Field("API token", tokenRow), Text("Blank keeps the current token. The field clears after saving.", 12),
            actions, Text("Test connection saves these details before checking your account.", 12));
        _navigatorFeedbackTitle = Text("", 13); _feedback.Classes.Add("habiticaDetail");
        _navigatorFeedbackCard = new Border { BorderThickness = new(1), CornerRadius = new(5), Padding = new(12),
            Child = new StackPanel { Spacing = 4, Children = { _navigatorFeedbackTitle, _feedback } } };
        _navigatorCredentialTitle = Text("", 13); _navigatorCredentialDetail = Text("", 12);
        var status = Card("Connection status", _navigatorFeedbackCard,
            new StackPanel { Spacing = 4, Children = { _navigatorCredentialTitle, _navigatorCredentialDetail } });
        _enabled.Content = new StackPanel { Spacing = 4, Children = { Text("Enable Habitica sync", 14, Ink), Text("For this computer and its local vault.", 12) } };
        _enabled.HorizontalContentAlignment = HorizontalAlignment.Stretch; _enabled.VerticalContentAlignment = VerticalAlignment.Top;
        AutomationProperties.SetName(_enabled, "Enable Habitica sync on this computer");
        _navigatorSyncTitle = Text("", 13); _navigatorSyncDetail = Text("", 12);
        _navigatorPending = Text("", 22, Ink); _navigatorLastSync = Text("", 14, Ink);
        var metrics = new Grid { ColumnDefinitions = new("0.7*,14,1.3*"), Children =
        {
            new StackPanel { Spacing = 4, Children = { Text("PENDING CHANGES", 9), _navigatorPending } },
            new StackPanel { Spacing = 4, Children = { Text("LAST SUCCESSFUL SYNC", 9), _navigatorLastSync } }
        } };
        Grid.SetColumn(metrics.Children[1], 2);
        var sync = Card("Sync on this computer", _enabled,
            new StackPanel { Spacing = 4, Children = { _navigatorSyncTitle, _navigatorSyncDetail } },
            new Border { Height = 1, Background = Rule }, metrics,
            Text("Sync runs while Stopwatch is open, including in the tray. Todos, reminders and checklist items sync; standalone notes stay local.", 12));
        var right = new StackPanel { Spacing = 14, Children = { status, sync } };
        _navigatorCards = new Grid { Children = { connection, right } };
        _navigatorRecovery = new Expander { Name = "HabiticaRecovery", Header = "Sync & recovery", IsExpanded = false,
            FontFamily = ClockPickerPresentation.Body, FontSize = 13, Foreground = Ink,
            HorizontalAlignment = HorizontalAlignment.Stretch, Content = new StackPanel { Spacing = 10 } };
        foreach (var control in recovery) ((StackPanel)_navigatorRecovery.Content).Children.Add(control);
        _navigatorRoot = new StackPanel { Spacing = 14, Children =
        {
            new StackPanel { Spacing = 4, Children = { Text("Habitica", 23, Ink), Text("Connect your account and follow sync on this computer.", 12) } },
            _navigatorCards, _navigatorRecovery
        } };
        _navigatorRoot.Styles.Add(new Style(s => s.OfType<TextBox>().Class("editorField")) { Setters =
        {
            new Setter(TemplatedControl.FontFamilyProperty, ClockPickerPresentation.Body), new Setter(TemplatedControl.FontSizeProperty, 13d),
            new Setter(MinWidthProperty, 0d), new Setter(MinHeightProperty, 36d), new Setter(TemplatedControl.PaddingProperty, new Thickness(10, 6))
        } });
        _navigatorRoot.Styles.Add(new Style(s => s.OfType<Button>().Class("habiticaAction")) { Setters =
        { new Setter(TemplatedControl.FontSizeProperty, 13d), new Setter(MinHeightProperty, 36d), new Setter(TemplatedControl.PaddingProperty, new Thickness(12, 6)) } });
        _navigatorRoot.Styles.Add(new Style(s => s.OfType<TextBlock>().Class("habiticaDetail")) { Setters =
        { new Setter(TextBlock.FontFamilyProperty, ClockPickerPresentation.Body), new Setter(TextBlock.FontSizeProperty, 12d), new Setter(TextBlock.ForegroundProperty, Muted) } });
        _navigatorRoot.SizeChanged += (_, _) => ResizeNavigatorLayout();
        Classes.Remove("settingsCard"); Child = _navigatorRoot;
        RefreshStatus(); UpdateConnectionFeedback();
    }

    private void ResizeNavigatorLayout()
    {
        double width = _navigatorRoot?.Bounds.Width ?? 0;
        if (_navigatorCards == null || width <= 0) return;
        bool stacked = width < 720 * (_settings?.Typography.Resolve("Settings").SizePercent ?? 100) / 100;
        if (_navigatorStacked == stacked) return; _navigatorStacked = stacked;
        _navigatorCards.ColumnDefinitions = new(stacked ? "*" : "1.1*,16,1*");
        _navigatorCards.RowDefinitions = new(stacked ? "Auto,14,Auto" : "Auto");
        Grid.SetColumn(_navigatorCards.Children[1], stacked ? 0 : 2); Grid.SetRow(_navigatorCards.Children[1], stacked ? 2 : 0);
    }

    private void RestoreNavigatorLayout()
    {
        if (_navigatorRoot == null) return;
        if (_pasteToken.Parent is Panel tokenRow) tokenRow.Children.Remove(_pasteToken);
        foreach (var item in _navigatorMoved.OrderBy(item => item.Index))
        {
            ((Panel)item.Control.Parent!).Children.Remove(item.Control);
            item.Control.Margin = item.Margin; Grid.SetColumn(item.Control, 0);
            item.Control.Classes.Remove("editorField"); item.Control.Classes.Remove("habiticaDetail");
            item.Parent.Children.Insert(item.Index, item.Control);
        }
        foreach (var button in new[] { _saveConnection, _test, _pasteToken })
        {
            button.Classes.Remove("habiticaAction"); button.Classes.Remove("editorAction"); button.Classes.Remove("editorPrimary"); button.Classes.Add("modern");
            button.ClearValue(TemplatedControl.FontFamilyProperty); button.ClearValue(MinWidthProperty); button.ClearValue(MinHeightProperty);
        }
        _enabled.Content = "Enable Habitica sync on this computer";
        _enabled.ClearValue(ContentControl.HorizontalContentAlignmentProperty); _enabled.ClearValue(ContentControl.VerticalContentAlignmentProperty);
        _token.InnerRightContent = _pasteToken;
        Child = _legacyContent; Classes.Add("settingsCard"); _navigatorMoved.Clear();
        _navigatorRoot = null; _navigatorCards = null; _navigatorRecovery = null; _navigatorReveal = null; _navigatorStacked = null;
        _navigatorFeedbackCard = null; _navigatorFeedbackTitle = null;
        _navigatorCredentialTitle = _navigatorCredentialDetail = _navigatorSyncTitle = _navigatorSyncDetail = _navigatorPending = _navigatorLastSync = null;
    }

    private void SetTokenVisible(bool visible)
    {
        _token.PasswordChar = visible ? '\0' : '●';
        if (_navigatorReveal == null) return;
        _navigatorReveal.Content = visible ? "Hide" : "Show";
        AutomationProperties.SetName(_navigatorReveal, visible ? "Hide entered API token" : "Show entered API token");
    }

    internal void HideConnectionDraft()
    {
        SetTokenVisible(false);
        if (_testing && _connectionHeading == "Testing connection") SetFeedback("Connection test canceled. Test again when ready.", "Ready to test");
        _testCancellation?.Cancel();
    }
    private static TextBlock Text(string text, double size, IBrush? color = null) => new()
    { Text = text, FontFamily = ClockPickerPresentation.Body, FontSize = size, Foreground = color ?? Muted, TextWrapping = TextWrapping.Wrap };
    private static StackPanel Field(string title, Control field) => new() { Spacing = 6, Children = { Text(title, 12, Ink), field } };
    private static Border Card(string title, params Control[] controls)
    {
        var body = new StackPanel { Spacing = 10, Children = { Text(title, 18, Ink) } };
        foreach (var control in controls) body.Children.Add(control);
        return new Border { Background = Brush.Parse("#40FFF6E4"), BorderBrush = Rule, BorderThickness = new(1), CornerRadius = new(7),
            Padding = new(16), VerticalAlignment = VerticalAlignment.Top, Child = body };
    }
}
