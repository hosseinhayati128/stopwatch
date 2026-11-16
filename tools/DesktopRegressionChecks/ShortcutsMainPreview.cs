using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;

// Isolated visual proposal: no global hotkeys, persisted settings or live application.
internal static class ShortcutsMainPreview
{
    const string Output = "design/one-piece-v2/shortcuts/main-native-preview";
    static readonly IBrush Ink = Brush.Parse("#102B3B"), Muted = Brush.Parse("#655539"), Gold = Brush.Parse("#B78329");
    static readonly FontFamily Body = StopwatchTypography.Resolve("modern-deck").NameFont;
    static TextBlock Text(string text, double size = 13, IBrush? color = null) => new() { Text = text, FontSize = size, FontFamily = Body, Foreground = color ?? Ink, TextWrapping = TextWrapping.Wrap };
    static Button Button(string title, bool primary = false) => new() { Content = Text(title, 13, primary ? Brush.Parse("#FFF4DE") : Ink), Background = primary ? Ink : Brush.Parse("#F7E9C9"), BorderBrush = Gold, BorderThickness = new(1), CornerRadius = new(4), Padding = new(14, 9), MinHeight = 38 };
    static Border Card(params Control[] children) => new Border() { Background = Brush.Parse("#40FFF6E4"), BorderBrush = Brush.Parse("#509A7B48"), BorderThickness = new(1), CornerRadius = new(7), Padding = new(18), VerticalAlignment = VerticalAlignment.Top, Child = new StackPanel { Spacing = 12, Children = { } } }.WithChildren(children);
    static TextBlock EndLabel() { var label = Text("2.0 s",11,Muted); Grid.SetColumn(label,1); return label; }
    static Border WithChildren(this Border card, Control[] children) { foreach (var c in children) ((StackPanel)card.Child!).Children.Add(c); return card; }
    internal static void Run()
    {
        if (Application.Current is not FrameSizePreviewApp) throw new Exception("Isolated preview app required.");
        Application.Current.Styles.Add(new FluentTheme()); Directory.CreateDirectory(Output);
        foreach (int percent in new[] { 70, 90, 125 }) Render(percent);
    }
    static void Render(int percent)
    {
        var bindings = new[] { AppSettings.DefaultLeaderShortcut(), AppSettings.DefaultNoteLeaderShortcut(), AppSettings.DefaultShowActiveOverlayShortcut(), AppSettings.DefaultOpenControllerShortcut() };
        string[] titles = ["Timer command leader", "Note command leader", "Show active overlay", "Open controller"];
        string[] descriptions = ["Open timer commands, then press a command key.", "Open note, Todo and reminder commands.", "Bring the active timer overlay into view.", "Bring the stopwatch controller into view."];
        var rows = new StackPanel { Spacing = 8 };
        var keyButtons = new List<Button>(); var hints = new List<TextBlock>();
        for (int i = 0; i < titles.Length; i++)
        {
            int index = i; var key = Button(bindings[i].Format()); key.MinWidth = 180; key.HorizontalAlignment = HorizontalAlignment.Stretch;
            var hint = Text(descriptions[i], 12, Muted); hints.Add(hint); keyButtons.Add(key);
            var clear = Button("Clear");
            var controls = new Grid { ColumnDefinitions = new("*,10,Auto"), Children = { key, clear } }; Grid.SetColumn(clear, 2);
            var row = new StackPanel { Spacing = 4, Children = { Text(titles[i], 16), hint, controls } };
            rows.Children.Add(row);
            if (i < 3) rows.Children.Add(new Border { Height = 1, Background = Brush.Parse("#359A7B48") });
            key.Click += (_, _) => { key.Content = Text("Press shortcut…", 13); hint.Text = "Listening · Esc cancels. Sample capture only."; };
            key.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Escape) { key.Content = Text(bindings[index].Format(), 13); hint.Text = descriptions[index]; e.Handled = true; } };
            clear.Click += (_, _) => { key.Content = Text("Not assigned", 13, Muted); hint.Text = "This action has no direct shortcut in this sample."; };
        }
        var timeout = new Slider { Minimum = .2, Maximum = 2, Value = .5, TickFrequency = .1, IsSnapToTickEnabled = true, Foreground = Brush.Parse("#278498") };
        var value = Text("0.5 seconds", 27); timeout.ValueChanged += (_, _) => value.Text = $"{timeout.Value:0.0} seconds";
        var shortcutCard = Card(Text("Main shortcuts", 21), Text("Select a shortcut to record a new combination.", 12, Muted), rows);
        var timingCard = Card(Text("Command chaining", 21), Text("Time between commands", 13, Muted), value, timeout,
            new Grid { ColumnDefinitions = new("*,Auto"), Children = { Text("0.2 s", 11, Muted), EndLabel() } },
            Text("After eligible timer commands, keep command mode open for another key. A longer delay gives you more time to chain actions.", 13, Muted),
            new Border { Height = 1, Background = Brush.Parse("#359A7B48") },
            Text("How it works", 16), Text("Win + F2  →  command key  →  next key", 14),
            Text("The leader opens command mode. Escape leaves it. Individual command keys belong to the next layer.", 12, Muted));
        var cards = new Grid { Children = { shortcutCard, timingCard } };
        var scroll = new ScrollViewer { Content = cards, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        void Resize() { bool stack = scroll.Bounds.Width < 800; cards.ColumnDefinitions = new(stack ? "*" : "1.25*,20,1*"); cards.RowDefinitions = new(stack ? "Auto,18,Auto" : "Auto"); Grid.SetColumn(timingCard, stack ? 0 : 2); Grid.SetRow(timingCard, stack ? 2 : 0); }
        scroll.SizeChanged += (_, _) => Resize();
        var heading = Text("Keyboard Shortcuts", 34); heading.FontWeight = FontWeight.Bold; heading.FontFamily = new FontFamily("avares://StopwatchOverlay.Desktop/Assets/Fonts/Pirate/#Almendra");
        var header = new StackPanel { Spacing = 8, Margin = new(0,0,0,18), Children = { heading, Text("Your everyday controls, one combination away.", 14, Muted),
            new WrapPanel { Children = { new Border { Background = Ink, Padding = new(12,7), CornerRadius = new(4), Margin = new(0,7,12,0), Child = Text("01  Main shortcuts", 13, Brushes.White) }, new Border { Padding = new(12,7), Margin = new(0,7,0,0), Child = Text("02  Command mappings", 13, Muted) } } } } };
        var cancel = Button("Cancel"); var save = Button("Save shortcuts", true); var reset = Button("Reset defaults");
        reset.Click += (_, _) => { for (int i = 0; i < 4; i++) { keyButtons[i].Content = Text(bindings[i].Format()); hints[i].Text = descriptions[i]; } timeout.Value = .5; };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { cancel, save }, HorizontalAlignment = HorizontalAlignment.Right };
        var footer = new Grid { ColumnDefinitions = new("Auto,*,Auto"), Margin = new(0,18,0,0), Children = { reset, actions } }; Grid.SetColumn(actions,2);
        var layout = new Grid { RowDefinitions = new("Auto,*,Auto,Auto"), Children = { header,scroll,Text("Design preview · sample shortcuts only · changes apply with Save",11,Muted),footer } };
        Grid.SetRow(scroll,1); Grid.SetRow(layout.Children[2],2); layout.Children[2].Margin = new(0,12,0,0); Grid.SetRow(footer,3);
        var frame = new StopwatchFrameSurface { ArtworkScale = .16, Padding = new(22), Background = Brush.Parse("#EBD3A5"), Child = layout };
        double scale = percent / 100d;
        var w = new Window { Width = 1120, Height = 720, ShowActivated = false, ShowInTaskbar = false, SystemDecorations = SystemDecorations.None, Background = Brushes.Transparent,
            Content = new LayoutTransformControl { LayoutTransform = new ScaleTransform(scale,scale), Child = frame } };
        try
        {
            w.Show(); ClockOverlayChecks.Pump(100); Resize(); ClockOverlayChecks.Pump(60);
            Check(); Save("main-" + percent); var footerAt = footer.TranslatePoint(default,w);
            scroll.ScrollToEnd(); ClockOverlayChecks.Pump(60); Check();
            if (footerAt != footer.TranslatePoint(default,w)) throw new Exception("Footer moved while scrolling.");
            if (percent == 125) { scroll.Offset = new Vector(0,timingCard.Bounds.Y); ClockOverlayChecks.Pump(50); } Save("timing-" + percent);
            if (!keyButtons[0].Focus()) throw new Exception("Shortcut field cannot receive focus.");
            if (percent == 90) { scroll.ScrollToHome(); keyButtons[0].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent)); ClockOverlayChecks.Pump(50); Save("recording"); }
            Console.WriteLine($"PASS {percent}% native layout, scrolling, fixed footer and focus; no hotkeys registered.");
            void Check() { if (scroll.Viewport.Height < 100 || scroll.Extent.Width > scroll.Viewport.Width + 1) throw new Exception("Invalid scroll layout."); }
            void Save(string name) { using var bitmap = new RenderTargetBitmap(new PixelSize((int)w.ClientSize.Width,(int)w.ClientSize.Height),new Vector(96,96)); if (scale >= 1) bitmap.Render(w); else { using var context = bitmap.CreateDrawingContext(); context.DrawRectangle(new VisualBrush(frame) { Stretch = Stretch.Fill, SourceRect = new RelativeRect(frame.Bounds, RelativeUnit.Absolute) }, null, new Rect(frame.Bounds.Size)); } bitmap.Save(Path.Combine(Output,name+".png")); }
        }
        finally { w.Close(); }
    }
}
