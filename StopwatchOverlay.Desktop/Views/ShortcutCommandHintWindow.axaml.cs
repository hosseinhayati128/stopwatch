using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class ShortcutCommandHintWindow : Window
{
    private readonly IReadOnlyDictionary<ShortcutAction, uint> _keys;
    private readonly double _scale;
    private readonly bool _nika;
    private readonly IBrush _ink, _muted, _gold, _paper, _badgeFill;
    private readonly Border _frame, _footerHint;
    private readonly ScrollViewer _body;
    private readonly TextBlock _context, _expansionPrompt;
    private readonly LayoutTransformControl _scaled;
    private bool _expanded;
    private int _leaderPresses = 1;

    public ShortcutCommandHintWindow() : this(AppSettings.DefaultCommandModeKeys()) { }

    internal ShortcutCommandHintWindow(IReadOnlyDictionary<ShortcutAction, uint> keys,
        AppSettings? settings = null, string? activeTimer = null)
    {
        InitializeComponent();
        _keys = new Dictionary<ShortcutAction, uint>(keys);
        _scale = AppUiScale.Normalize(settings?.UiScalePercent ?? AppUiScale.DefaultPercent) / 100;
        _nika = AppThemeCatalog.Normalize(settings?.ThemeMode) == AppThemeCatalog.Nika;
        IBrush Color(string resource, string nika, string fallback) => !_nika && this.TryFindResource(resource, out var found) && found is IBrush brush
            ? brush : Brush.Parse(_nika ? nika : fallback);
        _ink = Color("PrimaryTextBrush", "#102B3B", "#FFFFFF");
        _muted = Color("SecondaryTextBrush", "#655539", "#CCCCCC");
        _gold = Color("BorderBrush", "#B78329", "#666666");
        _paper = Color("SurfaceRaisedBrush", "#EBD3A5", "#202020");
        _badgeFill = Color("SurfaceBrush", "#F8EACA", "#303030");
        if (_nika) FontFamily = ClockPickerPresentation.Body;

        var title = Text("Timer Commands", 18);
        if (_nika) title.FontFamily = StopwatchTypography.Resolve("navigator-bold").NameFont;
        title.FontWeight = FontWeight.Bold;
        string leader = (settings?.LeaderShortcut ?? AppSettings.DefaultLeaderShortcut()).Format().Replace("+", " + ");
        var leaderBadge = Badge(leader, true);
        leaderBadge.VerticalAlignment = VerticalAlignment.Top;
        var header = new Grid { ColumnDefinitions = new("*,Auto"), Margin = new(0,0,0,7), Children = { title, leaderBadge } };
        Grid.SetColumn(leaderBadge, 1);
        _context = Text("", 11, _muted);
        _context.TextWrapping = TextWrapping.NoWrap;
        _context.TextTrimming = TextTrimming.CharacterEllipsis;
        UpdateActiveTimer(activeTimer);
        _expansionPrompt = Text("3 more · All commands", 11);
        _footerHint = new Border { Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6,
            Children = { Badge(keys.Values.Contains(0x71u) ? leader : "F2", true), _expansionPrompt } } };
        var footer = new Grid { ColumnDefinitions = new("*,Auto"), Margin = new(0,7,0,0), Children = { _context, _footerHint } };
        Grid.SetColumn(_footerHint, 1);
        _body = new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        var layout = new Grid { RowDefinitions = new("Auto,*,Auto"), Children = { header, _body, footer } };
        Grid.SetRow(_body, 1); Grid.SetRow(footer, 2);
        _frame = new Border { Name = "TimerCommandFrame", Padding = new(12,10), CornerRadius = new(6), BorderThickness = new(1),
            BorderBrush = _gold, Background = _paper, Child = layout };
        _scaled = new LayoutTransformControl { LayoutTransform = new ScaleTransform(_scale, _scale), Child = _frame };
        Content = _scaled;
        SizeToContent = SizeToContent.Manual;
        FitToScreen();
        Opened += (_, _) => FitToScreen();
    }

    internal void UpdateActiveTimer(string? name)
    {
        _context.Text = (string.IsNullOrWhiteSpace(name) ? "Active timer" : name) + " · Esc cancels";
        ToolTip.SetTip(_context, _context.Text);
    }

    internal bool AdvanceExpansion()
    {
        if (_expanded) return true;
        if (++_leaderPresses < 4)
        {
            _expansionPrompt.Text = $"{4 - _leaderPresses} more · All commands";
            return false;
        }
        _expanded = true;
        _footerHint.Child = Text("Press a key · Esc cancels", 11, _muted);
        FitToScreen();
        return true;
    }

    private TextBlock Text(string text, double size = 13, IBrush? brush = null) => new()
    {
        [DesktopTypography.ScopeProperty] = "Controller",
        Text = text, FontSize = size, Foreground = brush ?? _ink,
        TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center
    };

    private Border Badge(string text, bool dark = false)
    {
        var label = Text(text, 11, dark && _nika ? Brushes.White : _ink);
        label.TextWrapping = TextWrapping.NoWrap;
        label.FontWeight = FontWeight.SemiBold;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        // Badges keep their contrast when the user overrides general text colors.
        label.SetValue(DesktopTypography.PreserveColorProperty, true);
        return new Border { Background = dark && _nika ? _ink : _badgeFill, BorderBrush = _gold,
            BorderThickness = new(1), CornerRadius = new(3), Padding = new(4,1), Child = label };
    }

    private IEnumerable<Control> Rows(params (ShortcutAction Action, string Label)[] actions)
    {
        foreach (var (action, label) in actions)
        {
            if (!_keys.TryGetValue(action, out uint key) || key == 0) continue;
            var name = Text(label);
            var badge = Badge(Shortcut.FormatKeyName(key));
            badge.MinWidth = 44;
            var row = new Grid { Name = "TimerCommandRow" + action, ColumnDefinitions = new("Auto,8,*"), MinHeight = 21,
                Children = { badge, name } };
            Grid.SetColumn(name, 2);
            yield return row;
        }
    }

    private Control Group(string title, params (ShortcutAction Action, string Label)[] actions)
    {
        var heading = Text(title, 14);
        heading.FontWeight = FontWeight.SemiBold;
        heading.Margin = new(0,0,0,5);
        var list = new StackPanel { Spacing = 1, Children = { heading } };
        foreach (var row in Rows(actions)) list.Children.Add(row);
        return list;
    }

    private void FitToScreen()
    {
        var screen = Screens.ScreenFromVisual(this) ?? Screens.Primary;
        double availableWidth = screen == null ? 1920 : screen.WorkingArea.Width / screen.Scaling - 48;
        double availableHeight = screen == null ? 1080 : screen.WorkingArea.Height / screen.Scaling - 48;
        double width = Math.Min(_expanded ? 800 : 620, availableWidth / _scale);
        int columns = Math.Clamp((int)(width / (_expanded ? 250 : 190)), 1, 3);
        Control[] items = _expanded ?
        [
            Group("Timer controls", (ShortcutAction.StartStop,"Start / stop"), (ShortcutAction.Reset,"Reset timer"),
                (ShortcutAction.Lap,"Record lap"), (ShortcutAction.ToggleClock,"Clock mode"), (ShortcutAction.NewTimer,"New timer"),
                (ShortcutAction.CloseTimer,"Close timer"), (ShortcutAction.EditTimer,"Edit timer"), (ShortcutAction.UndoTimerEdit,"Undo timer edit")),
            Group("Projects & review", (ShortcutAction.RenameTimer,"Choose project"), (ShortcutAction.OpenDashboard,"Project dashboard"),
                (ShortcutAction.AddRecord,"Add time record"), (ShortcutAction.PeriodicReview,"Activity & Time Review"), (ShortcutAction.SyncActivityWatch,"Sync ActivityWatch")),
            Group("Overlay & navigation", (ShortcutAction.ToggleOverlay,"Show / hide overlay"), (ShortcutAction.NextTimer,"Next timer"),
                (ShortcutAction.OpenController,"Open controller"), (ShortcutAction.ShowAllClocks,"Choose a clock"), (ShortcutAction.ToggleActiveClocks,"Running clocks"),
                (ShortcutAction.SeparateOverlay,"Separate overlay"), (ShortcutAction.MergeOverlay,"Merge overlay"), (ShortcutAction.NextSeparatedOverlay,"Next overlay"))
        ] : Rows((ShortcutAction.StartStop,"Start / stop"), (ShortcutAction.Reset,"Reset timer"), (ShortcutAction.ToggleClock,"Clock / stopwatch"),
            (ShortcutAction.ToggleOverlay,"Show / hide overlay"), (ShortcutAction.NextTimer,"Next timer"), (ShortcutAction.ShowAllClocks,"Choose a clock")).ToArray();
        var body = new Grid { ColumnDefinitions = new(string.Join(",12,", Enumerable.Repeat("*", columns))),
            RowDefinitions = new(string.Join(",4,", Enumerable.Repeat("Auto", Math.Max(1, (items.Length + columns - 1) / columns)))) };
        for (int i = 0; i < items.Length; i++)
        {
            Control item = items[i];
            if (_expanded && i % columns != 0) item = new Border { BorderBrush = _gold, BorderThickness = new(1,0,0,0), Padding = new(12,0,0,0), Child = item };
            Grid.SetColumn(item, i % columns * 2); Grid.SetRow(item, i / columns * 2);
            body.Children.Add(item);
        }
        _body.Content = body;
        var measureSize = new Size(Math.Max(1, width - 26), double.PositiveInfinity);
        body.Measure(measureSize);
        var layout = (Grid)_frame.Child!;
        layout.Children[0].Measure(measureSize);
        layout.Children[2].Measure(measureSize);
        // Include measured chrome and a rounding allowance at fractional desktop DPI.
        double contentHeight = body.DesiredSize.Height + layout.Children[0].DesiredSize.Height + layout.Children[2].DesiredSize.Height + 28;
        double height = Math.Min(Math.Max(_expanded ? 280 : 128, contentHeight), availableHeight / _scale);
        _frame.Width = width; _frame.Height = height;
        Width = width * _scale; Height = height * _scale;
        _scaled.InvalidateArrange();
        if (screen != null)
            Position = new PixelPoint(screen.WorkingArea.X + (screen.WorkingArea.Width - (int)Math.Round(Width * screen.Scaling)) / 2,
                screen.WorkingArea.Y + (int)Math.Round(24 * screen.Scaling));
    }
}
