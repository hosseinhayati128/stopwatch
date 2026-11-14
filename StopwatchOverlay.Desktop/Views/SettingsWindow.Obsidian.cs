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
    private ScrollViewer? _obsidianPage;
    private Grid? _obsidianCards;
    private Border? _obsidianStatusCard;
    private TextBlock? _obsidianStatusHeading;
    private bool? _obsidianStacked;
    private string _obsidianStatusTitle = "";
    private bool _obsidianStatusFailed;
    private readonly List<(Control Control, Panel Parent, int Index, Thickness Margin, double Height,
        int Column, int Row, object? Content, Thickness Padding)> _obsidianMoved = new();
    private static readonly IBrush ObsidianPaper = Brush.Parse("#F7E8C7"),
        ObsidianTeal = Brush.Parse("#2D7169"), ObsidianError = Brush.Parse("#913F32");

    private void BuildObsidianPage(Grid host)
    {
        // Capture sibling indexes before reparenting so theme round trips restore the original tree.
        foreach (var control in new Control[] { ObsidianAutoSyncCheck, ObsidianLogUnnamedCheck,
            ObsidianFolderTextBox, BrowseObsidianFolderButton, ObsidianFileNameTextBox,
            ObsidianPathPreviewText, ExportNowButton, ExportStatusText })
        {
            var parent = (Panel)control.Parent!;
            _obsidianMoved.Add((control, parent, parent.Children.IndexOf(control), control.Margin, control.Height,
                Grid.GetColumn(control), Grid.GetRow(control), (control as ContentControl)?.Content,
                (control as TemplatedControl)?.Padding ?? default));
        }
        foreach (var item in _obsidianMoved)
        {
            item.Parent.Children.Remove(item.Control);
            item.Control.Margin = default;
            Grid.SetColumn(item.Control, 0); Grid.SetRow(item.Control, 0);
        }
        ObsidianChoice(ObsidianAutoSyncCheck, "Automatic export", "Keep completed timer sessions up to date in your Markdown folder.");
        ObsidianChoice(ObsidianLogUnnamedCheck, "Include unnamed timers", "Use names such as “Timer 1” and “Timer 2”.");
        var preferences = ObsidianCard("Export preferences", ObsidianAutoSyncCheck,
            new Border { Height = 1, Background = SettingsRule }, ObsidianLogUnnamedCheck,
            SettingsLabel("You can export all history manually even when automatic export is off.", 12));
        foreach (var field in new[] { ObsidianFolderTextBox, ObsidianFileNameTextBox })
        {
            field.Classes.Add("editorField");
            field.Height = double.NaN; field.Padding = new(10, 6);
        }
        AutomationProperties.SetName(ObsidianFolderTextBox, "Target folder");
        AutomationProperties.SetName(ObsidianFileNameTextBox, "File name");
        foreach (var button in new[] { BrowseObsidianFolderButton, ExportNowButton })
        {
            NavigatorFormStyles.StyleButton(button, button == ExportNowButton);
            button.Height = 36; button.Padding = new(12, 0);
        }
        ExportNowButton.Content = "Export all history";
        ExportNowButton.HorizontalAlignment = HorizontalAlignment.Left;
        var folderRow = new Grid { ColumnDefinitions = new("*,8,Auto"), Children = { ObsidianFolderTextBox, BrowseObsidianFolderButton } };
        Grid.SetColumn(BrowseObsidianFolderButton, 2);
        ObsidianPathPreviewText.Classes.Add("obsidianPath");
        ExportStatusText.Classes.Add("obsidianDetail");
        var pathBox = new Border { Background = ObsidianPaper, BorderBrush = SettingsRule, BorderThickness = new(1),
            CornerRadius = new(5), Padding = new(12), Child = new StackPanel { Spacing = 6,
                Children = { SettingsLabel("FULL FILE PATH", 10), ObsidianPathPreviewText } } };
        _obsidianStatusHeading = SettingsLabel(_obsidianStatusTitle, 13, ObsidianTeal);
        _obsidianStatusHeading.Name = "ObsidianStatusTitle";
        _obsidianStatusCard = new Border { BorderThickness = new(1), CornerRadius = new(5), Padding = new(12),
            Child = new StackPanel { Spacing = 4, Children = { _obsidianStatusHeading, ExportStatusText } } };
        var destination = ObsidianCard("Destination & export", ObsidianField("Target folder", folderRow),
            SettingsLabel("Choose your Obsidian vault, a subfolder, or any Markdown folder.", 12),
            ObsidianField("File name", ObsidianFileNameTextBox), pathBox, ExportNowButton, _obsidianStatusCard);
        _obsidianCards = new Grid { Children = { preferences, destination } };
        var heading = new StackPanel { Spacing = 4, Margin = new(0, 0, 0, 16), Children =
        {
            SettingsLabel("Obsidian / Markdown", 23, SettingsInk), SettingsLabel("Keep your time history in your notes.", 12)
        } };
        _obsidianPage = new ScrollViewer { Name = "ObsidianScroll", HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new(0, 0, 5, 0),
            Content = new StackPanel { Children = { heading, _obsidianCards } } };
        _obsidianPage.Styles.Add(new Style(s => s.OfType<TextBox>().Class("editorField")) { Setters =
        {
            new Setter(FontFamilyProperty, ClockPickerPresentation.Body), new Setter(FontSizeProperty, 13d),
            new Setter(MinWidthProperty, 0d), new Setter(MinHeightProperty, 36d)
        } });
        foreach (string name in new[] { "obsidianPath", "obsidianDetail" })
            _obsidianPage.Styles.Add(new Style(s => s.OfType<TextBlock>().Class(name)) { Setters =
            {
                new Setter(TextBlock.FontFamilyProperty, ClockPickerPresentation.Body), new Setter(TextBlock.FontSizeProperty, 12d),
                new Setter(TextBlock.ForegroundProperty, name == "obsidianPath" ? SettingsInk : SettingsMuted)
            } });
        _obsidianPage.SizeChanged += (_, _) => ResizeObsidianPage();
        heading.SizeChanged += (_, _) => ResizeObsidianPage();
        host.Children.Add(_obsidianPage);
        ApplyObsidianStatus();
    }

    private void ResizeObsidianPage()
    {
        if (_closed || _obsidianPage == null || _obsidianPage.Bounds.Width <= 0) return;
        bool stacked = _obsidianPage.Bounds.Width < 680 * _settings.Typography.Resolve("Settings").SizePercent / 100;
        if (_obsidianStacked == stacked) return;
        _obsidianStacked = stacked;
        _obsidianCards!.ColumnDefinitions = new(stacked ? "*" : "0.85*,16,1.15*");
        _obsidianCards.RowDefinitions = new(stacked ? "Auto,14,Auto" : "Auto");
        Grid.SetColumn(_obsidianCards.Children[1], stacked ? 0 : 2);
        Grid.SetRow(_obsidianCards.Children[1], stacked ? 2 : 0);
    }

    private void RestoreObsidianPage()
    {
        if (_obsidianPage == null) return;
        foreach (var item in _obsidianMoved.OrderBy(item => item.Index))
        {
            if (item.Control.Parent is Panel panel) panel.Children.Remove(item.Control);
            item.Control.Margin = item.Margin; item.Control.Height = item.Height;
            Grid.SetColumn(item.Control, item.Column); Grid.SetRow(item.Control, item.Row);
            item.Control.Classes.Remove("editorField");
            item.Control.Classes.Remove("obsidianPath"); item.Control.Classes.Remove("obsidianDetail");
            if (item.Control is ContentControl content) content.Content = item.Content;
            if (item.Control is TemplatedControl templated) templated.Padding = item.Padding;
            if (item.Control is CheckBox check)
            {
                check.ClearValue(HorizontalContentAlignmentProperty); check.ClearValue(VerticalContentAlignmentProperty);
            }
            if (item.Control is Button button && item.Control is not CheckBox)
            {
                button.Classes.Remove("editorAction"); button.Classes.Remove("editorPrimary"); button.Classes.Add("modern");
                button.ClearValue(FontFamilyProperty); button.ClearValue(MinWidthProperty); button.ClearValue(MinHeightProperty);
                button.ClearValue(HorizontalAlignmentProperty);
            }
            item.Parent.Children.Insert(item.Index, item.Control);
        }
        ((Panel)_obsidianPage.Parent!).Children.Remove(_obsidianPage);
        _obsidianMoved.Clear(); _obsidianPage = null; _obsidianCards = null;
        _obsidianStatusCard = null; _obsidianStatusHeading = null; _obsidianStacked = null;
        ApplyObsidianStatus();
    }

    private void RefreshObsidianDestination()
    {
        bool selected = !string.IsNullOrWhiteSpace(ObsidianFolderTextBox.Text);
        ExportNowButton.IsEnabled = selected;
        SetObsidianStatus(selected ? "Destination selected" : "Choose a destination", selected
            ? "Export completed sessions to this Markdown file." : "Select a folder before exporting your history.");
    }

    private void SetObsidianStatus(string title, string detail, bool failed = false)
    {
        _obsidianStatusTitle = title; _obsidianStatusFailed = failed;
        ExportStatusText.Text = detail;
        ApplyObsidianStatus();
    }

    private void ApplyObsidianStatus()
    {
        ExportStatusText.Foreground = _obsidianPage != null ? SettingsMuted : _obsidianStatusFailed ? Brushes.OrangeRed : Brushes.DeepSkyBlue;
        if (_obsidianStatusCard == null) return;
        _obsidianStatusHeading!.Text = _obsidianStatusTitle;
        _obsidianStatusHeading.Foreground = _obsidianStatusFailed ? ObsidianError : ObsidianTeal;
        _obsidianStatusCard.Background = Brush.Parse(_obsidianStatusFailed ? "#11913F32" : "#112D7169");
        _obsidianStatusCard.BorderBrush = Brush.Parse(_obsidianStatusFailed ? "#55913F32" : "#552D7169");
    }

    private static void ObsidianChoice(CheckBox check, string title, string detail)
    {
        check.Content = new StackPanel { Spacing = 4, Children = { SettingsLabel(title, 14, SettingsInk), SettingsLabel(detail, 12) } };
        check.HorizontalContentAlignment = HorizontalAlignment.Stretch; check.VerticalContentAlignment = VerticalAlignment.Top;
        AutomationProperties.SetName(check, title);
    }

    private static StackPanel ObsidianField(string title, Control field) => new()
    { Spacing = 6, Children = { SettingsLabel(title, 12, SettingsInk), field } };

    private static Border ObsidianCard(string title, params Control[] controls)
    {
        var body = new StackPanel { Spacing = 12, Children = { SettingsLabel(title, 18, SettingsInk) } };
        foreach (var control in controls) body.Children.Add(control);
        var card = SettingsCard(body); card.Padding = new(16); card.VerticalAlignment = VerticalAlignment.Top;
        return card;
    }
}
