using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public sealed partial class TodoEmblemPickerWindow : Window
{
    private readonly bool _launcher;
    private double _pageScale = 1;
    private RadioButton? _selected;

    public string SelectedEmblem { get; private set; } = "luffy";
    public string? Result { get; private set; }

    public TodoEmblemPickerWindow() : this("luffy", true, new TodoAppearanceProfile()) { }

    public TodoEmblemPickerWindow(string current, bool launcher, TodoAppearanceProfile look,
        bool pixel = false, double scalePercent = 100)
    {
        InitializeComponent();
        _launcher = launcher;
        Resources["EmblemPickerPixel"] = pixel;
        Resources["EmblemCheckRadius"] = new CornerRadius(pixel ? 1 : 9);
        Title = PickerHeading.Text = launcher ? "Choose launcher emblem" : "Choose header emblem";
        PickerHeading.FontFamily = pixel ? TodoVisuals.StrongFont : TodoVisuals.HeadingFont;
        PreviewLabel.Text = launcher ? "Launcher preview" : "Header preview";
        PreviewSurface.Padding = new Thickness(launcher ? 8 : 1, 1);
        PreviewContext.Background = launcher ? Brushes.Transparent : TodoVisuals.Navy;
        PreviewTitle.IsVisible = !launcher;
        HeaderEngraving.IsVisible = !launcher && look.EngravedHeader;
        DesktopTypography.SetScope(PreviewTitle, "Notes");
        ApplyPageScale(AppUiScale.Normalize(scalePercent) / 100);
        SizeChanged += (_, e) => FitShell(e.NewSize);

        var theme = (ControlTheme)this.FindResource("EmblemChoice")!;
        foreach (var option in TodoAppearanceCatalog.Emblems)
        {
            var button = new RadioButton { Name = "Emblem_" + option.Id, DataContext = option,
                Theme = theme, GroupName = "Emblems" };
            AutomationProperties.SetName(button, option.Label + " emblem");
            button.Click += (_, _) => Select(option.Id);
            EmblemGallery.Children.Add(button);
        }
        GalleryScroll.SizeChanged += (_, _) => EmblemGallery.Columns = GalleryScroll.Bounds.Width < 458 ? 3 : 4;
        Select(current);
        Opened += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (!IsVisible) return;
            _selected?.Focus();
            _selected?.BringIntoView();
        }, DispatcherPriority.Loaded);

        TodoVisuals.StyleButton(CancelEmblem, look, pixel: pixel);
        CancelEmblem.Background = TodoVisuals.Navy;
        CancelEmblem.BorderBrush = Brush.Parse("#8CA1A8");
        CancelEmblem.Foreground = TodoVisuals.Ivory;
        CancelEmblem.Click += (_, _) => Close();
        TodoVisuals.StyleButton(UseEmblem, look, pixel: pixel);
        UseEmblem.Background = UseEmblem.BorderBrush = TodoVisuals.Gold;
        UseEmblem.Foreground = TodoVisuals.Navy;
        UseEmblem.Click += (_, _) => { Result = SelectedEmblem; Close(Result); };
        AddHandler(KeyDownEvent, PickerKeyDown, RoutingStrategies.Tunnel);
    }

    internal void ApplyPageScale(double scale)
    {
        double ratio = scale / _pageScale;
        _pageScale = scale;
        if (Math.Abs(ratio - 1) > .0001)
        {
            MinWidth = 420 * scale;
            MinHeight = 480 * scale;
            Width *= ratio;
            Height *= ratio;
        }

        // The shell follows the page zoom. These native controls show the overlay at its own size.
        double emblemSize = (_launcher ? 72 : 64) / scale;
        PreviewEmblem.Width = PreviewEmblem.Height = emblemSize;
        PreviewTitle.FontSize = 34 / scale;
        PreviewTitle.Margin = new Thickness(12 / scale, 0, 5 / scale, 0);
        HeaderEngraving.Width = 382 / scale;
        HeaderEngraving.Height = emblemSize;
        FitShell(IsVisible ? ClientSize : new Size(Width, Height));
    }

    private void FitShell(Size client)
    {
        if (client.Width <= 0 || client.Height <= 0) return;
        // A scrollable gallery can measure narrower than the client after a live resize.
        PickerShell.Width = client.Width / _pageScale;
        PickerShell.Height = client.Height / _pageScale;
        if (Content is LayoutTransformControl layout) layout.InvalidateArrange();
    }

    public void Select(string id)
    {
        SelectedEmblem = TodoAppearanceCatalog.Normalize(id, TodoAppearanceCatalog.Emblems, "luffy");
        var button = EmblemGallery.Children.OfType<RadioButton>().First(b => ((TodoAppearanceOption)b.DataContext!).Id == SelectedEmblem);
        if (_selected != null && _selected != button) _selected.IsChecked = false;
        _selected = button;
        button.IsChecked = true;
        SelectionName.Text = ((TodoAppearanceOption)button.DataContext!).Label;
        PreviewEmblem.Choice = SelectedEmblem;
        if (IsVisible)
            Dispatcher.UIThread.Post(() =>
            {
                if (IsVisible && button == _selected) button.BringIntoView();
            }, DispatcherPriority.Loaded);
    }

    private void PickerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
            return;
        }
        if (e.Source is not RadioButton button) return;
        int current = EmblemGallery.Children.IndexOf(button);
        int next = e.Key switch
        {
            Key.Left => current - 1,
            Key.Right => current + 1,
            Key.Up => current - EmblemGallery.Columns,
            Key.Down => current + EmblemGallery.Columns,
            _ => current
        };
        if (next == current) return;
        e.Handled = true;
        if (next < 0 || next >= EmblemGallery.Children.Count) return;
        var target = (RadioButton)EmblemGallery.Children[next];
        Select(((TodoAppearanceOption)target.DataContext!).Id);
        target.Focus(NavigationMethod.Directional);
        target.BringIntoView();
    }

    private void DragTitle(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }

    private void ResizeCorner(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginResizeDrag(WindowEdge.SouthEast, e);
    }
}
