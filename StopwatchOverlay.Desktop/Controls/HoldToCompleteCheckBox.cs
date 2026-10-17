using System;
using System.Diagnostics;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace StopwatchOverlay.Desktop.Controls;

/// <summary>A todo checkbox whose user input requires an uninterrupted one-second hold.</summary>
public class HoldToCompleteCheckBox : CheckBox
{
    public static readonly StyledProperty<double> HoldProgressProperty =
        AvaloniaProperty.Register<HoldToCompleteCheckBox, double>(nameof(HoldProgress),
            coerce: (_, value) => double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1));

    private readonly Stopwatch _elapsed = new();
    private readonly DispatcherTimer _timer;
    private readonly List<Visual> _ancestors = new();
    private IPointer? _pointer;
    private bool _spaceDown;
    private bool _allowClick;

    public HoldToCompleteCheckBox()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => UpdateHold();
    }

    protected override Type StyleKeyOverride => typeof(CheckBox);

    public double HoldProgress
    {
        get => GetValue(HoldProgressProperty);
        set => SetValue(HoldProgressProperty, value);
    }

    public bool IsHolding { get; private set; }

    // Reloading IsChecked remains supported. Only user-triggered clicks are gated.
    protected override void OnClick()
    {
        if (_allowClick) base.OnClick();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (e.Handled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
            _pointer != null || _spaceDown || !CanHold()) return;
        Focus();
        _pointer = e.Pointer;
        _pointer.Capture(this);
        PseudoClasses.Set(":pressed", true);
        BeginHold();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (e.Pointer != _pointer || e.InitialPressMouseButton != MouseButton.Left) return;
        // The final input event may arrive before the next timer tick.
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this))) UpdateHold();
        CancelHold();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (e.Pointer == _pointer && !new Rect(Bounds.Size).Contains(e.GetPosition(this))) CancelHold();
        base.OnPointerMoved(e);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        if (e.Pointer == _pointer) CancelHold();
        base.OnPointerCaptureLost(e);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        CancelHold();
        base.OnPointerWheelChanged(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key != Key.Space) { base.OnKeyDown(e); return; }
        if (!e.Handled && !_spaceDown && _pointer == null && IsFocused && CanHold())
        {
            _spaceDown = true;
            PseudoClasses.Set(":pressed", true);
            BeginHold();
        }
        e.Handled = true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key != Key.Space) { base.OnKeyUp(e); return; }
        if (_spaceDown) UpdateHold();
        _spaceDown = false;
        CancelHold();
        e.Handled = true;
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        CancelHold(resetKeyboard: true);
        base.OnLostFocus(e);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        foreach (var ancestor in this.GetVisualAncestors())
        {
            _ancestors.Add(ancestor);
            ancestor.PropertyChanged += OnAncestorPropertyChanged;
        }
    }

    private void OnAncestorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == IsVisibleProperty && !change.GetNewValue<bool>()) CancelHold(resetKeyboard: true);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CancelHold(resetKeyboard: true);
        foreach (var ancestor in _ancestors) ancestor.PropertyChanged -= OnAncestorPropertyChanged;
        _ancestors.Clear();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if ((change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled) ||
            (change.Property == IsVisibleProperty && !IsVisible)) CancelHold(resetKeyboard: true);
    }

    /// <summary>Cancel a hold. Reset keyboard state when the window deactivates because key-up may happen elsewhere.</summary>
    public void CancelHold(bool resetKeyboard = false)
    {
        if (resetKeyboard) _spaceDown = false;
        IsHolding = false;
        _timer?.Stop();
        _elapsed.Reset();
        HoldProgress = 0;
        PseudoClasses.Set(":pressed", false);
        var pointer = _pointer;
        _pointer = null;
        if (pointer?.Captured == this) pointer.Capture(null);
        // Same-focused cancellation retains the latch until key-up to prevent a repeat from restarting.
    }

    private bool CanHold() => IsEffectivelyEnabled && IsEffectivelyVisible &&
        this.GetVisualRoot() != null;

    private void BeginHold()
    {
        HoldProgress = 0;
        IsHolding = true;
        _elapsed.Restart();
        _timer.Start();
    }

    private void UpdateHold()
    {
        if (!IsHolding) return;
        if (!CanHold() || (_spaceDown && !IsFocused)) { CancelHold(); return; }
        HoldProgress = Math.Min(1, _elapsed.Elapsed.TotalSeconds);
        if (HoldProgress < 1) return;
        IsHolding = false;
        _timer.Stop();
        _allowClick = true;
        try { OnClick(); }
        finally { _allowClick = false; }
    }
}
