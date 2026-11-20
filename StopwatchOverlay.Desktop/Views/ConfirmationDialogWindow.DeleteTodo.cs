using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class ConfirmationDialogWindow
{
    internal void ConfigureDeleteTodo(string task, bool linked, AppSettings settings)
    {
        const double WidthAtDefaultScale = 540;
        const string localEffect = "Removes this task from your Obsidian todo file.";
        const string habiticaEffect = "If linked, its Habitica task is deleted when sync runs.";
        const string undoWarning = "This cannot be undone.";
        string linkStatus = linked ? "Linked message" : "No linked message";
        string telegramEffect = linked ? "Deletes its Telegram message and cancels unsent copies." : "Cancels any unsent Telegram copy.";
        if (!PrepareTodoDeletion(settings, "Delete Todo", "Delete this todo?", "Delete todo"))
        {
            MessageText.Text = $"{task}\n\nTelegram: {linkStatus}\n{localEffect}\n{telegramEffect}\n{habiticaEffect}\n\n{undoWarning}";
            return;
        }

        var profile = settings.TodoAppearance.Current;
        bool dark = profile.Background == "midnight-chart";
        var muted = TodoVisuals.Muted(profile);
        var rule = TodoDeletionRule(profile);
        var red = TodoDeletionDanger(profile);
        var teal = Brush.Parse("#21586A");
        TextBlock Text(string value, double size, IBrush? brush = null) => TodoDeletionText(value, size, brush ?? TodoVisuals.Ink(profile));
        var name = Text(task, 17);
        name.FontWeight = FontWeight.SemiBold;
        AutoTextDirection.Apply(name, task);
        name.Measure(new Size(WidthAtDefaultScale - 92, double.PositiveInfinity));
        double height = name.DesiredSize.Height > 42 ? 500 : 430;
        var taskScroll = new ScrollViewer
        {
            Content = name, MaxHeight = 116, Margin = new(0, 5, 0, 0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        AutomationProperties.SetName(taskScroll, "Task text");
        var linkLabel = Text(linkStatus, 11, linked ? TodoVisuals.Ivory : muted);
        linkLabel.FontWeight = FontWeight.SemiBold;
        var linkBadge = new Border
        {
            Padding = new(9, 4), CornerRadius = new(4), Background = linked ? teal : Brushes.Transparent,
            BorderBrush = linked ? teal : rule, BorderThickness = new(1), Child = linkLabel, HorizontalAlignment = HorizontalAlignment.Right
        };
        var telegramRow = new Grid { ColumnDefinitions = new("*,Auto"), Children = { Text("Telegram", 12, muted), linkBadge } };
        Grid.SetColumn(linkBadge, 1);
        var summary = new Border
        {
            Padding = new(14), CornerRadius = new(5), Background = dark ? TodoVisuals.Card(profile) : Brush.Parse("#EDFDF0D1"), BorderBrush = rule, BorderThickness = new(1),
            Child = new StackPanel { Children =
            {
                Text("TASK", 10, muted), taskScroll,
                new Border { Margin = new(0, 12, 0, 0), Padding = new(0, 10, 0, 0), BorderBrush = rule, BorderThickness = new(0, 1, 0, 0), Child = telegramRow }
            } }
        };
        var warning = Text(undoWarning, 12, red); warning.FontWeight = FontWeight.SemiBold;
        var explanation = new StackPanel { Spacing = 6, Margin = new(0, 14, 0, 0), Children =
        {
            Text(localEffect, 12),
            Text(telegramEffect, 12),
            Text(habiticaEffect, 12, muted),
            new Border { Margin = new(0, 4, 0, 0), Child = warning }
        } };
        var body = new ScrollViewer
        {
            Margin = new(22, 18, 22, 0), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new StackPanel { Children = { summary, explanation } }
        };
        DesktopTypography.SetPreserveColor(linkBadge, linked);
        DesktopTypography.SetPreserveColor(warning, true);
        SetTodoDeletionFrame(settings, "Delete Todo", "Review this task before deleting.", "Delete todo", body, WidthAtDefaultScale, height);
    }

    private bool PrepareTodoDeletion(AppSettings settings, string title, string heading, string action)
    {
        ConfigureNoteDeletionActions();
        Title = title;
        HeadingText.Text = heading;
        ConfirmButton.Classes.Remove("primary");
        ConfirmButton.Classes.Add("danger");
        ConfirmButton.Content = action;
        if (AppThemeCatalog.Normalize(settings.ThemeMode) != AppThemeCatalog.Nika) return false;
        settings.TodoAppearance ??= new();
        settings.TodoAppearance.Normalize();
        return true;
    }

    private static IBrush TodoDeletionRule(TodoAppearanceProfile profile) =>
        Brush.Parse(profile.Background == "midnight-chart" ? "#688391" : "#C4AB77");

    private static IBrush TodoDeletionDanger(TodoAppearanceProfile profile) =>
        Brush.Parse(profile.Background == "midnight-chart" ? "#FFB3A9" : "#8D3F35");

    private static TextBlock TodoDeletionText(string value, double size, IBrush brush) => new()
    {
        Text = value, FontSize = size, FontFamily = TodoVisuals.BodyFont, Foreground = brush,
        VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap
    };

    private void SetTodoDeletionFrame(AppSettings settings, string heading, string? subtitle, string action,
        Control body, double width, double? height)
    {
        var appearance = settings.TodoAppearance;
        var profile = appearance.Current;
        bool dark = profile.Background == "midnight-chart";
        var muted = TodoVisuals.Muted(profile);
        var red = TodoDeletionDanger(profile);
        var rule = TodoDeletionRule(profile);
        TodoVisuals.Palette(this, profile);
        FontFamily = TodoVisuals.BodyFont;
        foreach (var button in new[] { CancelButton, ConfirmButton })
            ((Panel)button.Parent!).Children.Remove(button);
        Content = null;

        void StyleAction(Button button, string label, bool safe)
        {
            var text = TodoDeletionText(label, 13, safe ? TodoVisuals.Ivory : red);
            text.FontWeight = FontWeight.SemiBold;
            button.Classes.Clear();
            button.Content = text;
            TodoVisuals.StyleButton(button, profile, pixel: profile.Toolbar == "pixel-controls");
            button.MinWidth = safe ? 94 : 124;
            button.MinHeight = 36;
            button.Padding = new(12, 8);
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.Background = safe ? TodoVisuals.Navy : dark ? TodoVisuals.Paper(profile) : Brush.Parse("#F8EACA");
            button.Foreground = text.Foreground;
            button.BorderBrush = safe ? TodoVisuals.Gold : Brush.Parse("#B67A60");
            DesktopTypography.SetPreserveColor(button, true);
            AutomationProperties.SetName(button, label);
        }
        var title = TodoDeletionText(heading, 26, TodoVisuals.Ivory); title.FontFamily = TodoVisuals.HeadingFont;
        var titles = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center, Children = { title } };
        if (subtitle != null) titles.Children.Add(TodoDeletionText(subtitle, 12, TodoVisuals.Ivory));
        var close = new Button
        {
            Width = 30, Height = 30, MinHeight = 0, Padding = new(0), Background = Brushes.Transparent, BorderThickness = new(0),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            Content = new Avalonia.Controls.Shapes.Path { Width = 15, Height = 15, Stretch = Stretch.Uniform, Data = Geometry.Parse("M1,1 L15,15 M15,1 L1,15"), Stroke = TodoVisuals.Ivory, StrokeThickness = 1.7 }
        };
        close.Click += OnCancelClicked;
        AutomationProperties.SetName(close, "Cancel deletion"); ToolTip.SetTip(close, "Cancel (Esc)");
        var header = new Grid { MinHeight = 74, ColumnDefinitions = new("68,*,46"), Background = TodoVisuals.Navy };
        var engraving = new TodoArtwork { Kind = "Header", Opacity = .65 }; Grid.SetColumnSpan(engraving, 3);
        if (profile.EngravedHeader) header.Children.Add(engraving); header.Children.Add(new TodoArtwork { Choice = profile.HeaderEmblem, Margin = new(10) });
        Grid.SetColumn(titles, 1); header.Children.Add(titles); Grid.SetColumn(close, 2); header.Children.Add(close);

        DesktopTypography.SetPreserveColor(header, true);
        header.PointerPressed += (_, e) =>
        {
            if (!e.Handled && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        };
        StyleAction(CancelButton, "Cancel", true);
        StyleAction(ConfirmButton, action, false);
        var footerRow = new Grid { ColumnDefinitions = new("*,Auto,10,Auto"), Children = { TodoDeletionText("Esc · cancel", 11, muted), CancelButton, ConfirmButton } };
        Grid.SetColumn(CancelButton, 1);
        Grid.SetColumn(ConfirmButton, 3);
        bool stacked = false;
        footerRow.SizeChanged += (_, _) =>
        {
            bool narrow = footerRow.Bounds.Width < Math.Max(94, CancelButton.DesiredSize.Width) + Math.Max(124, ConfirmButton.DesiredSize.Width) + 76;
            if (narrow == stacked) return;
            stacked = narrow;
            footerRow.ColumnDefinitions = new(narrow ? "*" : "*,Auto,10,Auto");
            footerRow.RowDefinitions = new(narrow ? "Auto,Auto,Auto" : "*");
            Grid.SetColumn(CancelButton, narrow ? 0 : 1);
            Grid.SetColumn(ConfirmButton, narrow ? 0 : 3);
            Grid.SetRow(CancelButton, narrow ? 1 : 0);
            Grid.SetRow(ConfirmButton, narrow ? 2 : 0);
            CancelButton.Margin = ConfirmButton.Margin = narrow ? new Thickness(0, 5, 0, 0) : default;
        };
        var footer = new Border { Margin = new(22, 12, 22, 20), Padding = new(0, 12, 0, 0), BorderBrush = rule, BorderThickness = new(0, 1, 0, 0), Child = footerRow };
        var layout = new Grid { RowDefinitions = new("Auto,*,Auto") };
        var map = new TodoArtwork { Kind = "Background", Choice = profile.Background, Opacity = .65 };
        Grid.SetRow(map, 1); Grid.SetRowSpan(map, 2); layout.Children.Add(map);
        layout.Children.Add(header); Grid.SetRow(body, 1); layout.Children.Add(body); Grid.SetRow(footer, 2); layout.Children.Add(footer);
        TodoVisuals.AddShellTrim(layout, appearance.IsPixel);
        var shell = new TodoFrame
        {
            Margin = new(8), Child = layout, Background = TodoVisuals.Paper(profile), BorderBrush = TodoVisuals.Gold,
            BorderThickness = new(1), CornerRadius = new(9), ClipToBounds = true, Pixel = appearance.IsPixel
        };
        Content = new Grid { Children = { shell } };
        SizeNoteDeletion(settings, height, width);
    }
}
