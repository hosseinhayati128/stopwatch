using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using StopwatchOverlay.Desktop.Controls;

internal static class HoldCompletionChecks
{
    public static void Run()
    {
        var box = new Probe { Width = 200, Height = 50, IsChecked = false };
        var panel = new StackPanel();
        panel.Children.Add(box);
        var other = new Button { Content = "Other" };
        panel.Children.Add(other);
        var window = new Window { Content = panel, Width = 240, Height = 140, ShowInTaskbar = false, ShowActivated = false };
        window.Show();
        Pump(150);
        try
        {
            box.InvokeClick();
            Check(box.IsChecked == false, "A plain click toggled a todo.");
            box.Press(); box.Release();
            Check(box.IsChecked == false && box.HoldProgress == 0, "A short pointer press toggled a todo.");
            box.Press(); Pump(250);
            Check(box.IsHolding && box.HoldProgress > 0 && box.HoldProgress < 1 && box.IsChecked == false,
                "Hold progress did not advance before completion.");
            box.Release(); Pump(850);
            Check(box.IsChecked == false && !box.IsHolding && box.HoldProgress == 0, "Early release did not cancel.");
            box.Press(); Pump(1100);
            Check(box.IsChecked == true && box.HoldProgress == 1 && !box.IsHolding, "One-second pointer hold did not complete.");
            Pump(1100); box.Release();
            Check(box.IsChecked == true, "One pointer hold toggled repeatedly or toggled again on release.");
            box.Press(); Pump(1100); box.Release();
            Check(box.IsChecked == false, "Unchecking did not require and accept its own hold.");

            int progressRequests = 0;
            box.ProgressToggleRequested += (_, _) => progressRequests++;
            box.Press(); box.Release(); box.Press(clickCount: 2); Pump(1100); box.Release();
            Check(box.IsChecked == true && progressRequests == 0, "Long second press requested progress instead of completion.");
            box.IsChecked = false;

            Cancelled(() => box.MoveOutside(), "pointer leaving");
            Cancelled(() => box.Wheel(), "scrolling");
            Cancelled(() => box.LoseCapture(), "pointer capture loss");
            Cancelled(() => { box.IsEnabled = false; box.IsEnabled = true; }, "disabling");
            Cancelled(() => { box.IsVisible = false; box.IsVisible = true; }, "hiding");
            Cancelled(() => { panel.IsVisible = false; panel.IsVisible = true; }, "hiding an ancestor");
            Cancelled(() => { panel.IsEnabled = false; panel.IsEnabled = true; }, "disabling an ancestor");
            Cancelled(() => { panel.Children.Remove(box); panel.Children.Insert(0, box); }, "detaching");
            Cancelled(() => other.Focus(), "losing focus");
            box.Press(handled: true); Pump(1100); box.Release();
            Check(!box.IsHolding && box.IsChecked == false, "A handled rich-text press began completion.");

            box.Focus(); box.SpaceDown(); Pump(1100);
            Check(box.IsChecked == true, "Focused Space hold did not complete.");
            box.SpaceDown(); Pump(1100); box.SpaceUp();
            Check(box.IsChecked == true, "Key repeat toggled a todo again.");
            box.SpaceDown(); Pump(150); box.SpaceUp(); Pump(1000);
            Check(box.IsChecked == true, "A short Space press toggled a todo.");
            box.SpaceDown(); Pump(150); box.CancelHold(); box.SpaceDown(); Pump(1100); box.SpaceUp();
            Check(box.IsChecked == true, "Key repeat restarted a cancelled hold.");
            box.SpaceDown(); Pump(1100); box.SpaceUp();
            Check(box.IsChecked == false, "A second Space hold did not uncheck.");
            // Space can be released in another control/app, so this row never sees key-up.
            box.SpaceDown(); Pump(80); other.Focus(); box.Focus();
            box.Press(); Pump(1100); box.Release();
            Check(box.IsChecked == true, "Lost-focus Space latch blocked the next pointer hold.");
            box.SpaceDown(); Pump(1100); box.SpaceUp();
            Check(box.IsChecked == false, "Lost-focus Space latch blocked the next keyboard hold.");
            box.SpaceDown(); Pump(80); box.CancelHold(resetKeyboard: true);
            box.Press(); Pump(1100); box.Release();
            Check(box.IsChecked == true, "Window-deactivation Space latch blocked the next pointer hold.");
            box.IsChecked = true;
            Check(box.IsChecked == true, "Programmatic reload was blocked.");
            box.HoldProgress = 2;
            Check(box.HoldProgress == 1, "Progress exceeded one.");
            box.HoldProgress = -1;
            Check(box.HoldProgress == 0, "Progress became negative.");
        }
        finally { box.CancelHold(); window.Close(); }

        void Cancelled(Action cancel, string reason)
        {
            box.Focus(); box.Press(); Pump(80); cancel(); Pump(1100); box.Release();
            Check(box.IsChecked == false && !box.IsHolding && box.HoldProgress == 0, "Hold was not cancelled after " + reason + ".");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Pump(int milliseconds)
    {
        using var cancellation = new CancellationTokenSource(milliseconds);
        Dispatcher.UIThread.MainLoop(cancellation.Token);
    }

    private sealed class Probe : HoldToCompleteCheckBox
    {
        private readonly Pointer _pointer = new(321, PointerType.Mouse, true);
        private readonly PointerPointProperties _down = new(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        private readonly PointerPointProperties _up = new(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased);
        public void InvokeClick() => OnClick();
        public void Press(bool handled = false, int clickCount = 1) => OnPointerPressed(new PointerPressedEventArgs(this, _pointer, this,
            new Point(10, 10), 0, _down, KeyModifiers.None, clickCount) { Handled = handled });
        public void Release() => OnPointerReleased(new PointerReleasedEventArgs(this, _pointer, this,
            new Point(10, 10), 0, _up, KeyModifiers.None, MouseButton.Left));
        public void MoveOutside() => OnPointerMoved(new PointerEventArgs(InputElement.PointerMovedEvent, this,
            _pointer, this, new Point(-10, -10), 0, _down, KeyModifiers.None));
        public void Wheel() => OnPointerWheelChanged(new PointerWheelEventArgs(this, _pointer, this,
            new Point(10, 10), 0, _down, KeyModifiers.None, new Vector(0, -1)));
        public void LoseCapture() => _pointer.Capture(null);
        public void SpaceDown() => OnKeyDown(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        public void SpaceUp() => OnKeyUp(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }
}
