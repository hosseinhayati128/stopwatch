using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace StopwatchOverlay.Desktop.Views;

public partial class ShortcutsWindow : Window
{
    private static readonly (ShortcutAction Action, string Description)[] WinF2Actions =
    [
        (ShortcutAction.StartStop, "Start or stop the active timer"),
        (ShortcutAction.Reset, "Reset the active timer"),
        (ShortcutAction.ToggleOverlay, "Show or hide the active timer overlay"),
        (ShortcutAction.Lap, "Record a lap"),
        (ShortcutAction.ToggleClock, "Toggle between current mode and Clock"),
        (ShortcutAction.NewTimer, "Create a new timer and make it active"),
        (ShortcutAction.NextTimer, "Select the next active timer"),
        (ShortcutAction.CloseTimer, "Close the active timer"),
        (ShortcutAction.RenameTimer, "Select, create, change, or clear project"),
        (ShortcutAction.OpenDashboard, "Open project time dashboard"),
        (ShortcutAction.OpenController, "Open stopwatch controller"),
        (ShortcutAction.EditTimer, "Edit active timer (adjust value, start time)"),
        (ShortcutAction.UndoTimerEdit, "Undo timer edit"),
        (ShortcutAction.AddRecord, "Add a new completed project record"),
        (ShortcutAction.SyncActivityWatch, "Sync ActivityWatch log to Obsidian now"),
        (ShortcutAction.PeriodicReview, "Open periodic review window"),
        (ShortcutAction.SeparateOverlay, "Separate active timer to dedicated overlay"),
        (ShortcutAction.MergeOverlay, "Merge active separated overlay back into herd"),
        (ShortcutAction.NextSeparatedOverlay, "Switch focus between separated overlays and herd"),
        (ShortcutAction.ShowAllClocks, "Show all clocks to choose directly"),
        (ShortcutAction.ToggleActiveClocks, "Show running clocks bar under overlay"),
    ];

    private static readonly (NoteCommandAction Action, string Description)[] WinF3Actions =
    [
        (NoteCommandAction.AddTodo, "Add a new Todo item"),
        (NoteCommandAction.AddNote, "Add a quick note"),
        (NoteCommandAction.AddReminder, "Add a reminder"),
        (NoteCommandAction.ViewNotes, "Open notes viewer"),
    ];

    private Shortcut _pendingLeader;
    private Shortcut _pendingNoteLeader;
    private Shortcut _pendingShowActiveOverlay;
    private Shortcut _pendingOpenController;
    private double _pendingChainingTimeoutSeconds;
    private Dictionary<ShortcutAction, uint> _pendingCommandModeKeys;
    private Dictionary<NoteCommandAction, uint> _pendingNoteCommandModeKeys;

    private readonly Dictionary<ShortcutAction, TextBox> _winF2TextBoxes = new();
    private readonly Dictionary<NoteCommandAction, TextBox> _winF3TextBoxes = new();

    public Shortcut ResultLeader => _pendingLeader;
    public Shortcut ResultNoteLeader => _pendingNoteLeader;
    public Shortcut ResultShowActiveOverlay => _pendingShowActiveOverlay;
    public Shortcut ResultOpenController => _pendingOpenController;
    public double ResultChainingTimeoutSeconds => _pendingChainingTimeoutSeconds;
    public Dictionary<ShortcutAction, uint> ResultCommandModeKeys => new(_pendingCommandModeKeys);
    public Dictionary<NoteCommandAction, uint> ResultNoteCommandModeKeys => new(_pendingNoteCommandModeKeys);

    public bool? DialogResult { get; private set; }

    public ShortcutsWindow() : this(
        AppSettings.DefaultLeaderShortcut(),
        AppSettings.DefaultNoteLeaderShortcut(),
        AppSettings.DefaultShowActiveOverlayShortcut(),
        AppSettings.DefaultOpenControllerShortcut(),
        AppSettings.DefaultCommandChainingTimeoutSeconds)
    {
    }

    public ShortcutsWindow(
        Shortcut currentLeader,
        Shortcut? currentNoteLeader = null,
        Shortcut? currentShowActiveOverlay = null,
        Shortcut? currentOpenController = null,
        double currentChainingTimeout = AppSettings.DefaultCommandChainingTimeoutSeconds,
        Dictionary<ShortcutAction, uint>? commandModeKeys = null,
        Dictionary<NoteCommandAction, uint>? noteCommandModeKeys = null)
    {
        InitializeComponent();

        _pendingLeader = currentLeader ?? AppSettings.DefaultLeaderShortcut();
        _pendingNoteLeader = currentNoteLeader ?? AppSettings.DefaultNoteLeaderShortcut();
        _pendingShowActiveOverlay = currentShowActiveOverlay ?? AppSettings.DefaultShowActiveOverlayShortcut();
        _pendingOpenController = currentOpenController ?? AppSettings.DefaultOpenControllerShortcut();
        _pendingChainingTimeoutSeconds = Math.Clamp(
            currentChainingTimeout,
            AppSettings.MinimumCommandChainingTimeoutSeconds,
            AppSettings.MaximumCommandChainingTimeoutSeconds);

        _pendingCommandModeKeys = commandModeKeys != null
            ? new Dictionary<ShortcutAction, uint>(commandModeKeys)
            : AppSettings.DefaultCommandModeKeys();

        // Sanitize: eliminate Tab for NextSeparatedOverlay
        if (_pendingCommandModeKeys.TryGetValue(ShortcutAction.NextSeparatedOverlay, out var switchVk) && switchVk == ShortcutCommandMap.VK_TAB)
        {
            _pendingCommandModeKeys[ShortcutAction.NextSeparatedOverlay] = ShortcutCommandMap.VK_KEY_J;
        }

        _pendingNoteCommandModeKeys = noteCommandModeKeys != null
            ? new Dictionary<NoteCommandAction, uint>(noteCommandModeKeys)
            : AppSettings.DefaultNoteCommandModeKeys();

        ChainingTimeoutSlider.Value = _pendingChainingTimeoutSeconds;
        UpdateChainingTimeoutLabel();

        BuildCommandPanels();
        RenderAllBoxes();
    }

    public ShortcutsWindow(
        Dictionary<ShortcutAction, Shortcut> current,
        double currentChainingTimeout = AppSettings.DefaultCommandChainingTimeoutSeconds,
        Dictionary<ShortcutAction, uint>? commandModeKeys = null,
        Dictionary<NoteCommandAction, uint>? noteCommandModeKeys = null)
        : this(ExtractLeader(current), ExtractNoteLeader(current), ExtractShowActiveOverlay(current), ExtractOpenController(current), currentChainingTimeout, commandModeKeys, noteCommandModeKeys)
    {
    }

    private static Shortcut ExtractLeader(Dictionary<ShortcutAction, Shortcut> shortcuts)
    {
        if (shortcuts != null && shortcuts.TryGetValue(ShortcutAction.CommandLeader, out var leader))
            return leader;
        return AppSettings.DefaultLeaderShortcut();
    }

    private static Shortcut ExtractNoteLeader(Dictionary<ShortcutAction, Shortcut> shortcuts)
    {
        if (shortcuts != null && shortcuts.TryGetValue(ShortcutAction.NoteCommandLeader, out var leader))
            return leader;
        return AppSettings.DefaultNoteLeaderShortcut();
    }

    private static Shortcut ExtractShowActiveOverlay(Dictionary<ShortcutAction, Shortcut> shortcuts)
    {
        if (shortcuts != null && shortcuts.TryGetValue(ShortcutAction.ShowActiveOverlay, out var s))
            return s;
        return AppSettings.DefaultShowActiveOverlayShortcut();
    }

    private static Shortcut ExtractOpenController(Dictionary<ShortcutAction, Shortcut> shortcuts)
    {
        if (shortcuts != null && shortcuts.TryGetValue(ShortcutAction.OpenController, out var s))
            return s;
        return AppSettings.DefaultOpenControllerShortcut();
    }

    private void BuildCommandPanels()
    {
        BuildWinF2Panel();
        BuildWinF3Panel();
    }

    private void BuildWinF2Panel()
    {
        WinF2CommandsPanel.Children.Clear();
        _winF2TextBoxes.Clear();

        foreach (var (action, description) in WinF2Actions)
        {
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(80, GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var textBlock = new TextBlock
            {
                Text = description,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(textBlock, 0);

            var textBox = new TextBox
            {
                IsReadOnly = true,
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeight.SemiBold,
                Tag = action,
                Margin = new Thickness(0, 0, 6, 0)
            };
            textBox.GotFocus += CommandKeyBox_GotFocus;
            textBox.LostFocus += CommandKeyBox_LostFocus;
            textBox.KeyDown += CommandKeyBox_KeyDown;
            Grid.SetColumn(textBox, 1);
            _winF2TextBoxes[action] = textBox;

            var clearButton = new Button
            {
                Content = "Clear",
                Tag = action,
                Classes = { "modern" },
                Padding = new Thickness(8, 2, 8, 2)
            };
            clearButton.Click += ClearWinF2CommandKey_Click;
            Grid.SetColumn(clearButton, 2);

            grid.Children.Add(textBlock);
            grid.Children.Add(textBox);
            grid.Children.Add(clearButton);

            WinF2CommandsPanel.Children.Add(grid);
        }
    }

    private void BuildWinF3Panel()
    {
        WinF3CommandsPanel.Children.Clear();
        _winF3TextBoxes.Clear();

        foreach (var (action, description) in WinF3Actions)
        {
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(80, GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var textBlock = new TextBlock
            {
                Text = description,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(textBlock, 0);

            var textBox = new TextBox
            {
                IsReadOnly = true,
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeight.SemiBold,
                Tag = action,
                Margin = new Thickness(0, 0, 6, 0)
            };
            textBox.GotFocus += CommandKeyBox_GotFocus;
            textBox.LostFocus += CommandKeyBox_LostFocus;
            textBox.KeyDown += CommandKeyBox_KeyDown;
            Grid.SetColumn(textBox, 1);
            _winF3TextBoxes[action] = textBox;

            var clearButton = new Button
            {
                Content = "Clear",
                Tag = action,
                Classes = { "modern" },
                Padding = new Thickness(8, 2, 8, 2)
            };
            clearButton.Click += ClearWinF3CommandKey_Click;
            Grid.SetColumn(clearButton, 2);

            grid.Children.Add(textBlock);
            grid.Children.Add(textBox);
            grid.Children.Add(clearButton);

            WinF3CommandsPanel.Children.Add(grid);
        }
    }

    private void RenderAllBoxes()
    {
        RenderLeaderBox();
        RenderNoteLeaderBox();
        RenderShowActiveOverlayBox();
        RenderOpenControllerBox();
        RenderCommandModeBoxes();
    }

    private void RenderLeaderBox()
    {
        if (LeaderShortcutBox == null) return;
        var combo = _pendingLeader != null ? _pendingLeader.Format() : "";
        LeaderShortcutBox.Text = combo.Length > 0 ? combo : "(none)";
    }

    private void RenderNoteLeaderBox()
    {
        if (NoteLeaderShortcutBox == null) return;
        var combo = _pendingNoteLeader != null ? _pendingNoteLeader.Format() : "";
        NoteLeaderShortcutBox.Text = combo.Length > 0 ? combo : "(none)";
    }

    private void RenderShowActiveOverlayBox()
    {
        if (ShowActiveOverlayShortcutBox == null) return;
        var combo = _pendingShowActiveOverlay != null ? _pendingShowActiveOverlay.Format() : "";
        ShowActiveOverlayShortcutBox.Text = combo.Length > 0 ? combo : "(none)";
    }

    private void RenderOpenControllerBox()
    {
        if (OpenControllerShortcutBox == null) return;
        var combo = _pendingOpenController != null ? _pendingOpenController.Format() : "";
        OpenControllerShortcutBox.Text = combo.Length > 0 ? combo : "(none)";
    }

    private void RenderCommandModeBoxes()
    {
        foreach (var (action, box) in _winF2TextBoxes)
        {
            if (_pendingCommandModeKeys.TryGetValue(action, out var vk) && vk != 0)
            {
                box.Text = Shortcut.FormatKeyName(vk);
            }
            else
            {
                box.Text = "(none)";
            }
        }

        foreach (var (action, box) in _winF3TextBoxes)
        {
            if (_pendingNoteCommandModeKeys.TryGetValue(action, out var vk) && vk != 0)
            {
                box.Text = Shortcut.FormatKeyName(vk);
            }
            else
            {
                box.Text = "(none)";
            }
        }
    }

    private void ShortcutBox_GotFocus(object? sender, GotFocusEventArgs e)
    {
        if (sender is TextBox box)
            box.Text = "Press a key combo...";
    }

    private void ShortcutBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox box)
        {
            if (box.Tag as string == "ShowActiveOverlay")
                RenderShowActiveOverlayBox();
            else if (box.Tag as string == "OpenController")
                RenderOpenControllerBox();
            else if (box.Tag as string == "NoteLeader")
                RenderNoteLeaderBox();
            else
                RenderLeaderBox();
        }
    }

    private void ShortcutBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox box)
            return;

        e.Handled = true;

        var key = e.Key;
        // Ignore modifier-only key presses
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.None)
            return;

        uint mods = 0;
        if ((e.KeyModifiers & KeyModifiers.Control) != 0) mods |= Shortcut.MOD_CONTROL;
        if ((e.KeyModifiers & KeyModifiers.Alt) != 0) mods |= Shortcut.MOD_ALT;
        if ((e.KeyModifiers & KeyModifiers.Shift) != 0) mods |= Shortcut.MOD_SHIFT;
        if ((e.KeyModifiers & KeyModifiers.Meta) != 0) mods |= Shortcut.MOD_WIN;

        uint vk = KeyToVirtualKey(key);
        if (vk == 0) return;

        var shortcut = new Shortcut(mods, vk);

        if (box.Tag as string == "ShowActiveOverlay")
        {
            _pendingShowActiveOverlay = shortcut;
            box.Text = _pendingShowActiveOverlay.Format();
        }
        else if (box.Tag as string == "OpenController")
        {
            _pendingOpenController = shortcut;
            box.Text = _pendingOpenController.Format();
        }
        else if (box.Tag as string == "NoteLeader")
        {
            _pendingNoteLeader = shortcut;
            box.Text = _pendingNoteLeader.Format();
        }
        else
        {
            _pendingLeader = shortcut;
            box.Text = _pendingLeader.Format();
        }
    }

    private void CommandKeyBox_GotFocus(object? sender, GotFocusEventArgs e)
    {
        if (sender is TextBox box)
        {
            box.Text = "Press key...";
        }
    }

    private void CommandKeyBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox box)
        {
            if (box.Tag is ShortcutAction action)
            {
                uint vk = _pendingCommandModeKeys.TryGetValue(action, out var val) ? val : 0;
                box.Text = vk != 0 ? Shortcut.FormatKeyName(vk) : "(none)";
            }
            else if (box.Tag is NoteCommandAction noteAction)
            {
                uint vk = _pendingNoteCommandModeKeys.TryGetValue(noteAction, out var val) ? val : 0;
                box.Text = vk != 0 ? Shortcut.FormatKeyName(vk) : "(none)";
            }
        }
    }

    private void CommandKeyBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox box)
            return;

        e.Handled = true;

        var key = e.Key;
        // Ignore modifier-only key presses
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.None)
            return;

        if (key == Key.Escape)
        {
            SetWarning(box.Tag, "Escape is reserved for canceling command mode.");
            return;
        }

        uint vk = KeyToVirtualKey(key);
        if (vk == 0) return;

        // Normalize numpad digits to regular digits
        if (vk >= 0x60 && vk <= 0x69)
        {
            vk = vk - 0x60 + 0x30;
        }

        if (box.Tag is ShortcutAction action)
        {
            // Check if key is already assigned to another Win+F2 action
            foreach (var (otherAction, otherVk) in _pendingCommandModeKeys)
            {
                if (otherAction != action && otherVk != 0)
                {
                    uint normalizedOther = (otherVk >= 0x60 && otherVk <= 0x69) ? (otherVk - 0x60 + 0x30) : otherVk;
                    if (normalizedOther == vk)
                    {
                        SetWarning(action, $"Key '{Shortcut.FormatKeyName(vk)}' is already assigned to '{AppSettings.FormatActionName(otherAction)}'. Duplicate keys are not allowed.");
                        return;
                    }
                }
            }

            ClearWarning(action);
            _pendingCommandModeKeys[action] = vk;
            box.Text = Shortcut.FormatKeyName(vk);
        }
        else if (box.Tag is NoteCommandAction noteAction)
        {
            // Check if key is already assigned to another Win+F3 action
            foreach (var (otherAction, otherVk) in _pendingNoteCommandModeKeys)
            {
                if (otherAction != noteAction && otherVk != 0)
                {
                    uint normalizedOther = (otherVk >= 0x60 && otherVk <= 0x69) ? (otherVk - 0x60 + 0x30) : otherVk;
                    if (normalizedOther == vk)
                    {
                        SetWarning(noteAction, $"Key '{Shortcut.FormatKeyName(vk)}' is already assigned to '{AppSettings.FormatNoteActionName(otherAction)}'. Duplicate keys are not allowed.");
                        return;
                    }
                }
            }

            ClearWarning(noteAction);
            _pendingNoteCommandModeKeys[noteAction] = vk;
            box.Text = Shortcut.FormatKeyName(vk);
        }
    }

    private void SetWarning(object? tag, string message)
    {
        if (tag is ShortcutAction)
        {
            WinF2ValidationWarning.Text = message;
            WinF2ValidationWarningBorder.IsVisible = true;
        }
        else
        {
            WinF3ValidationWarning.Text = message;
            WinF3ValidationWarningBorder.IsVisible = true;
        }
    }

    private void ClearWarning(object? tag)
    {
        if (tag is ShortcutAction)
        {
            WinF2ValidationWarningBorder.IsVisible = false;
        }
        else
        {
            WinF3ValidationWarningBorder.IsVisible = false;
        }
    }

    private void ClearWinF2CommandKey_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ShortcutAction action)
        {
            _pendingCommandModeKeys[action] = 0;
            if (_winF2TextBoxes.TryGetValue(action, out var box))
            {
                box.Text = "(none)";
            }
            ClearWarning(action);
        }
    }

    private void ClearWinF3CommandKey_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is NoteCommandAction action)
        {
            _pendingNoteCommandModeKeys[action] = 0;
            if (_winF3TextBoxes.TryGetValue(action, out var box))
            {
                box.Text = "(none)";
            }
            ClearWarning(action);
        }
    }

    private static uint KeyToVirtualKey(Key key)
    {
        if (key >= Key.A && key <= Key.Z)
            return (uint)(0x41 + (key - Key.A));
        if (key >= Key.D0 && key <= Key.D9)
            return (uint)(0x30 + (key - Key.D0));
        if (key >= Key.NumPad0 && key <= Key.NumPad9)
            return (uint)(0x60 + (key - Key.NumPad0));
        if (key >= Key.F1 && key <= Key.F24)
            return (uint)(0x70 + (key - Key.F1));

        return key switch
        {
            Key.Space => 0x20,
            Key.Escape => 0x1B,
            Key.Tab => 0x09,
            Key.Enter or Key.Return => 0x0D,
            Key.Back => 0x08,
            Key.Insert => 0x2D,
            Key.Delete => 0x2E,
            Key.Home => 0x24,
            Key.End => 0x23,
            Key.PageUp => 0x21,
            Key.PageDown => 0x22,
            Key.Left => 0x25,
            Key.Up => 0x26,
            Key.Right => 0x27,
            Key.Down => 0x28,
            _ => 0
        };
    }

    private void ClearShortcut_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            if (btn.Tag as string == "ShowActiveOverlay")
            {
                _pendingShowActiveOverlay = new Shortcut(0, 0);
                RenderShowActiveOverlayBox();
            }
            else if (btn.Tag as string == "OpenController")
            {
                _pendingOpenController = new Shortcut(0, 0);
                RenderOpenControllerBox();
            }
            else if (btn.Tag as string == "NoteLeader")
            {
                _pendingNoteLeader = new Shortcut(0, 0);
                RenderNoteLeaderBox();
            }
            else
            {
                _pendingLeader = new Shortcut(0, 0);
                RenderLeaderBox();
            }
        }
    }

    private void ChainingTimeoutSlider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        _pendingChainingTimeoutSeconds = Math.Round(ChainingTimeoutSlider.Value, 1);
        UpdateChainingTimeoutLabel();
    }

    private void UpdateChainingTimeoutLabel()
    {
        if (ChainingTimeoutText != null)
            ChainingTimeoutText.Text = $"{_pendingChainingTimeoutSeconds:0.0} s";
    }

    private void ResetDefaults_Click(object? sender, RoutedEventArgs e)
    {
        _pendingLeader = AppSettings.DefaultLeaderShortcut();
        _pendingNoteLeader = AppSettings.DefaultNoteLeaderShortcut();
        _pendingShowActiveOverlay = AppSettings.DefaultShowActiveOverlayShortcut();
        _pendingOpenController = AppSettings.DefaultOpenControllerShortcut();
        _pendingChainingTimeoutSeconds = AppSettings.DefaultCommandChainingTimeoutSeconds;
        _pendingCommandModeKeys = AppSettings.DefaultCommandModeKeys();
        _pendingNoteCommandModeKeys = AppSettings.DefaultNoteCommandModeKeys();

        ChainingTimeoutSlider.Value = _pendingChainingTimeoutSeconds;
        UpdateChainingTimeoutLabel();

        WinF2ValidationWarningBorder.IsVisible = false;
        WinF3ValidationWarningBorder.IsVisible = false;

        RenderAllBoxes();
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (!AppSettings.ValidateCommandModeKeys(_pendingCommandModeKeys, out var f2Error))
        {
            WinF2ValidationWarning.Text = f2Error;
            WinF2ValidationWarningBorder.IsVisible = true;
            CommandsTabControl.SelectedIndex = 0;
            return;
        }

        if (!AppSettings.ValidateNoteCommandModeKeys(_pendingNoteCommandModeKeys, out var f3Error))
        {
            WinF3ValidationWarning.Text = f3Error;
            WinF3ValidationWarningBorder.IsVisible = true;
            CommandsTabControl.SelectedIndex = 1;
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
