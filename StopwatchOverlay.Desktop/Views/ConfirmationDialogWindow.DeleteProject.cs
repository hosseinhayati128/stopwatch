using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class ConfirmationDialogWindow
{
    private bool _deleteProjectConfigured, _deleteProjectBlocked, _deleteProjectCompleted;
    private static readonly IBrush ProjectRule = Brush.Parse("#CDB784"), ProjectCream = Brush.Parse("#F8EACA"),
        ProjectRed = Brush.Parse("#8D3F35"), ProjectRedRule = Brush.Parse("#B67A60"), ProjectAmber = Brush.Parse("#865719"), ProjectTeal = Brush.Parse("#17626B");
    private static TextBlock ProjectText(string text, double size = 13, IBrush? brush = null) => new()
    {
        Text = text, FontSize = size, FontFamily = ClockPickerPresentation.Body, Foreground = brush ?? ClockPickerPresentation.Navy,
        VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap
    };
    internal void ConfigureDeleteProject(string project, bool blocked, AppSettings? settings)
    {
        _deleteProjectConfigured = true; _deleteProjectBlocked = blocked;
        Title = blocked ? "Cannot Delete Project" : "Delete Project";
        HeadingText.Text = blocked ? "Cannot Delete Project" : $"Delete \"{project}\"?";
        MessageText.Text = blocked ? $"Cannot delete '{project}' because a timer is currently tracking this project. Pause or stop the timer first."
            : "Permanently delete this project and all its recorded sessions? This action cannot be undone.";
        ConfirmButton.Content = "Delete project"; ConfirmButton.IsVisible = !blocked; CancelButton.Content = blocked ? "Got it" : "Cancel";
        Closed += (_, _) => _deleteProjectCompleted = true;
        Opened += (_, _) => Dispatcher.UIThread.Post(() => { if (IsVisible) CancelButton.Focus(); });
        AddHandler(KeyDownEvent, (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Complete(false); } }, RoutingStrategies.Tunnel);
        if (AppThemeCatalog.Normalize(settings?.ThemeMode) != AppThemeCatalog.Nika) return;

        FontFamily = ClockPickerPresentation.Body; Resources["AppFontFamily"] = ClockPickerPresentation.Body;
        foreach (var control in new Control[] { HeadingText, MessageText, CancelButton, ConfirmButton }) ((Panel)control.Parent!).Children.Remove(control);
        Content = null;
        var ink = ClockPickerPresentation.Navy; var muted = ClockPickerPresentation.Muted; var gold = ClockPickerPresentation.Gold;
        HeadingText.Classes.Clear(); HeadingText.Text = Title; HeadingText.FontSize = 28;
        HeadingText.FontFamily = StopwatchTypography.Resolve("navigator-bold").NameFont; HeadingText.FontWeight = FontWeight.Bold;
        HeadingText.Foreground = ink; HeadingText.HorizontalAlignment = HorizontalAlignment.Stretch; HeadingText.TextAlignment = TextAlignment.Left;
        var close = new Button
        {
            Name = "DeleteProjectCancel", Width = 30, Height = 30, MinHeight = 0, Padding = new(0), Background = Brushes.Transparent, BorderThickness = new(0),
            VerticalAlignment = VerticalAlignment.Top, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            Content = new Avalonia.Controls.Shapes.Path { Width = 15, Height = 15, Stretch = Stretch.Uniform, Data = Geometry.Parse("M1,1 L15,15 M15,1 L1,15"), Stroke = ink, StrokeThickness = 1.7 }
        };
        AutomationProperties.SetName(close, blocked ? "Close active timer warning" : "Cancel deleting this project"); ToolTip.SetTip(close, blocked ? "Close (Esc)" : "Cancel (Esc)"); close.Click += OnCancelClicked;
        var titleRow = new Grid { ColumnDefinitions = new("*,30"), Children = { HeadingText, close } }; Grid.SetColumn(close, 1);
        var header = new StackPanel { Spacing = 3, Children = { titleRow, ProjectText(blocked ? "A timer is still tracking this project." : "Review the project before deleting.", 12, muted) } };
        var name = ProjectText(project, 19); name.Name = "DeleteProjectName"; name.FontWeight = FontWeight.SemiBold; AutoTextDirection.Apply(name, name.Text);
        var scope = ProjectText(blocked ? "Running" : "All sessions", 13, blocked ? ProjectAmber : ink); scope.FontWeight = FontWeight.SemiBold; DesktopTypography.SetPreserveColor(scope, true);
        var scopeRow = new Grid { ColumnDefinitions = new("*,Auto"), Children = { ProjectText(blocked ? "Timer status" : "Time records", 12, muted), scope } }; Grid.SetColumn(scope, 1);
        var summary = new Border
        {
            Padding = new(14), CornerRadius = new(5), Background = ProjectCream, BorderBrush = ProjectRule, BorderThickness = new(1),
            Child = new StackPanel { Children =
            {
                ProjectText("PROJECT", 10, muted), new Border { Margin = new(0, 4, 0, 0), Child = name },
                new Border { Margin = new(0, 12, 0, 0), Padding = new(0, 10, 0, 0), BorderBrush = ProjectRule, BorderThickness = new(0, 1, 0, 0), Child = scopeRow }
            } }
        };
        MessageText.Text = blocked ? "Pause or stop the timer, then try deleting this project again." : "Permanently deletes this project and all its recorded sessions.";
        MessageText.FontSize = 13; MessageText.FontFamily = ClockPickerPresentation.Body; MessageText.Foreground = ink; MessageText.Margin = new(0);
        var warning = ProjectText(blocked ? "Nothing has been deleted." : "This cannot be undone.", 12, blocked ? ProjectTeal : ProjectRed);
        warning.Name = "DeleteProjectWarning"; warning.FontWeight = FontWeight.SemiBold; DesktopTypography.SetPreserveColor(warning, true);
        var explanation = new StackPanel { Spacing = 6, Margin = new(0, 14, 0, 0), Children = { MessageText, warning } };
        var body = new ScrollViewer
        {
            Name = "DeleteProjectBody", Margin = new(0, 16, 0, 0), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = new StackPanel { Children = { summary, explanation } }
        };
        foreach (var button in new[] { CancelButton, ConfirmButton })
        {
            bool safe = button == CancelButton; var color = safe ? ProjectCream : ProjectRed;
            var label = ProjectText(safe ? blocked ? "Got it" : "Cancel" : "Delete project", 13, color); label.FontWeight = FontWeight.SemiBold;
            button.Classes.Clear(); button.MinWidth = safe ? 104 : 128; button.MinHeight = 36; button.Padding = new(12, 7); button.CornerRadius = new(4);
            button.HorizontalAlignment = HorizontalAlignment.Stretch; button.HorizontalContentAlignment = HorizontalAlignment.Center; button.VerticalContentAlignment = VerticalAlignment.Center;
            button.Background = safe ? ink : ProjectCream; button.Foreground = color; button.BorderBrush = safe ? gold : ProjectRedRule; button.BorderThickness = new(1); button.Content = label;
            button.Resources["ButtonBackgroundPointerOver"] = Brush.Parse(safe ? "#214655" : "#F0DCC0"); button.Resources["ButtonBackgroundPressed"] = Brush.Parse(safe ? "#081E2A" : "#E7C7AB");
            button.Resources["ButtonForegroundPointerOver"] = button.Resources["ButtonForegroundPressed"] = color;
            button.Resources["ButtonBorderBrushPointerOver"] = button.Resources["ButtonBorderBrushPressed"] = button.BorderBrush;
            DesktopTypography.SetPreserveColor(button, true); AutomationProperties.SetName(button, safe ? blocked ? "Close active timer warning" : "Cancel deleting this project" : "Delete this project and all recorded sessions");
        }
        var footerHint = ProjectText(blocked ? "Esc · close" : "Esc · cancel", 11, muted);
        var footerRow = new Grid { ColumnDefinitions = new(blocked ? "*,10,Auto" : "*,10,Auto,10,Auto"), Children = { footerHint, CancelButton } }; Grid.SetColumn(CancelButton, 2);
        if (!blocked) { Grid.SetColumn(ConfirmButton, 4); footerRow.Children.Add(ConfirmButton); }
        bool stacked = false;
        footerRow.SizeChanged += (_, _) =>
        {
            if (blocked) return;
            double required = footerHint.DesiredSize.Width + 20
                + Math.Max(CancelButton.MinWidth, ((Control)CancelButton.Content!).DesiredSize.Width + 26)
                + Math.Max(ConfirmButton.MinWidth, ((Control)ConfirmButton.Content!).DesiredSize.Width + 26);
            bool narrow = footerRow.Bounds.Width < required;
            if (narrow == stacked) return;
            stacked = narrow;
            footerRow.ColumnDefinitions = new(narrow ? "*" : "*,10,Auto,10,Auto");
            footerRow.RowDefinitions = new(narrow ? "Auto,Auto,Auto" : "*");
            Grid.SetColumn(CancelButton, narrow ? 0 : 2); Grid.SetColumn(ConfirmButton, narrow ? 0 : 4);
            Grid.SetRow(CancelButton, narrow ? 1 : 0); Grid.SetRow(ConfirmButton, narrow ? 2 : 0);
            CancelButton.Margin = ConfirmButton.Margin = narrow ? new Thickness(0, 8, 0, 0) : new Thickness(0);
        };
        var footer = new Border { Margin = new(0, 14, 0, 0), Padding = new(0, 12, 0, 0), BorderBrush = ProjectRule, BorderThickness = new(0, 1, 0, 0), Child = footerRow };
        var layout = new Grid { RowDefinitions = new("Auto,*,Auto"), Children = { header, body, footer } }; Grid.SetRow(body, 1); Grid.SetRow(footer, 2);
        Content = new StopwatchFrameSurface { ArtworkScale = .16, Padding = new(10), Background = Brush.Parse("#EBD3A5"), Child = layout };
        SystemDecorations = SystemDecorations.None; Background = Brushes.Transparent; TransparencyLevelHint = [WindowTransparencyLevel.Transparent]; CanResize = false;
        MinWidth = MinHeight = 0; MaxHeight = double.PositiveInfinity; SizeToContent = SizeToContent.Manual;
        double scale = AppUiScale.Normalize(settings!.UiScalePercent) / 100; Width = 540 * scale; Height = 420 * scale;
        Opened += (_, _) => { if (Screens.ScreenFromWindow(this) is { } screen) FitDeleteProjectToScreen(screen); else FitDeleteProjectContent(); };
        SizeChanged += (_, _) => FitDeleteProjectContent();
    }
    internal void FitDeleteProjectToScreen(Screen screen)
    {
        MaxWidth = Math.Floor(screen.WorkingArea.Width / screen.Scaling); MaxHeight = Math.Floor(screen.WorkingArea.Height / screen.Scaling);
        Width = Math.Min(Width, MaxWidth); Height = Math.Min(Height, MaxHeight);
        var area = screen.WorkingArea; double width = Math.Ceiling(Width * screen.Scaling), height = Math.Ceiling(Height * screen.Scaling);
        double centerX = area.X + area.Width / 2d, centerY = area.Y + area.Height / 2d;
        if (Owner is Window owner)
        {
            var size = owner.FrameSize ?? owner.ClientSize;
            centerX = owner.Position.X + size.Width * owner.RenderScaling / 2; centerY = owner.Position.Y + size.Height * owner.RenderScaling / 2;
        }
        Position = new PixelPoint((int)Math.Clamp(centerX - width / 2, area.X, area.Right - width), (int)Math.Clamp(centerY - height / 2, area.Y, area.Bottom - height));
        FitDeleteProjectContent();
    }
    internal void FitDeleteProjectContent(bool resizeWindow = false)
    {
        if (!_deleteProjectConfigured || DesktopPageAppearance.Content(this) is not StopwatchFrameSurface frame) return;
        double scale = DesktopPageAppearance.Scale(this);
        if (resizeWindow)
        {
            Width = 540 * scale; Height = 420 * scale;
            if (Screens.ScreenFromWindow(this) is { } screen) { FitDeleteProjectToScreen(screen); return; }
        }
        frame.Width = (ClientSize.Width > 0 ? ClientSize.Width : Width) / scale; frame.Height = (ClientSize.Height > 0 ? ClientSize.Height : Height) / scale;
        if (Content is LayoutTransformControl transform) transform.InvalidateArrange();
    }
}
