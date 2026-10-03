using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace StopwatchOverlay.Desktop.Views;

public partial class ShortcutsWindow : Window
{
    private Shortcut _pendingLeader;
    private Shortcut _pendingNoteLeader;
    private Shortcut _pendingShowActiveOverlay;
    private Shortcut _pendingOpenController;
    private double _pendingChainingTimeoutSeconds;

    public Shortcut ResultLeader => _pendingLeader;
    public Shortcut ResultNoteLeader => _pendingNoteLeader;
    public Shortcut ResultShowActiveOverlay => _pendingShowActiveOverlay;
    public Shortcut ResultOpenController => _pendingOpenController;
    public double ResultChainingTimeoutSeconds => _pendingChainingTimeoutSeconds;

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
        double currentChainingTimeout = AppSettings.DefaultCommandChainingTimeoutSeconds)
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

        ChainingTimeoutSlider.Value = _pendingChainingTimeoutSeconds;
        UpdateChainingTimeoutLabel();
        RenderAllBoxes();
    }

    public ShortcutsWindow(Dictionary<ShortcutAction, Shortcut> current, double currentChainingTimeout = AppSettings.DefaultCommandChainingTimeoutSeconds)
        : this(ExtractLeader(current), ExtractNoteLeader(current), ExtractShowActiveOverlay(current), ExtractOpenController(current), currentChainingTimeout)
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

    private void RenderAllBoxes()
    {
        RenderLeaderBox();
        RenderNoteLeaderBox();
        RenderShowActiveOverlayBox();
        RenderOpenControllerBox();
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
        ChainingTimeoutSlider.Value = _pendingChainingTimeoutSeconds;
        UpdateChainingTimeoutLabel();
        RenderAllBoxes();
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
