using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;
using Avalonia.Threading;

namespace StopwatchOverlay.Desktop.Views;

internal sealed class TimerPickerWindow : Window
{
    private readonly ListBox _clocks;
    private readonly TimerSession[] _timers;

    internal TimerSession? SelectedTimer { get; private set; }

    internal TimerPickerWindow(IReadOnlyList<TimerSession> timers, TimerSession? active, AppSettings? settings=null)
    {
        bool framed=settings?.ThemeMode==AppThemeCatalog.Nika;
        Title = "Choose a clock";
        Width = 460;
        Height = framed?480:410;
        if(framed){MinWidth=360;MinHeight=300;}
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        _timers = timers.ToArray();
        _clocks = new ListBox
        {
            Focusable = true,
            ItemsSource = _timers.Select((timer, index) =>
                $"{index + 1}. {timer.DisplayName}{(timer == active ? " · Active" : "")}\n{timer.DisplaySummary}").ToArray(),
            ItemTemplate = new FuncDataTemplate<string>((label, _) => new TextBlock
            {
                Text = label, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5)
            }),
            SelectedIndex = Math.Max(0, Array.IndexOf(_timers, active))
        };
        _clocks.DoubleTapped += (_, _) => SelectClock(_clocks.SelectedIndex);
        var choose = new Button { Content = "Select clock", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        choose.Click += (_, _) => SelectClock(_clocks.SelectedIndex);
        var help = new TextBlock
        {
            Text = "Press 1–9 to select directly, or use the arrow keys and Enter.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        };
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Margin = framed?new Thickness(0):new Thickness(16) };
        Grid.SetRow(_clocks, 1);
        Grid.SetRow(choose, 2);
        layout.Children.Add(help);
        layout.Children.Add(_clocks);
        layout.Children.Add(choose);
        if(framed)
        {
            var surface=new StopwatchFrameSurface{Child=layout,Padding=new Thickness(8)};
            // Keep the existing dialog material; reuse of artwork does not link preferences.
            surface.Bind(StopwatchFrameSurface.BackgroundProperty,this.GetResourceObservable("DialogSurfaceBrush"));
            Content=surface;
        }
        else Content = layout;
        AddHandler(KeyDownEvent, HandlePickerKey, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Opened += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if(!IsVisible)return;
            _clocks.ScrollIntoView(_clocks.SelectedIndex);
            if(_clocks.ContainerFromIndex(_clocks.SelectedIndex) is Control item)item.Focus();
            else _clocks.Focus();
        },DispatcherPriority.Loaded);
    }

    private void HandlePickerKey(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.Escape)
        {
            args.Handled = true;
            Close();
        }
        else if (args.Key == Key.Enter && _clocks.IsKeyboardFocusWithin)
        {
            args.Handled = true;
            SelectClock(_clocks.SelectedIndex);
        }
        else if (args.KeyModifiers == KeyModifiers.None)
        {
            int index = args.Key switch
            {
                >= Key.D1 and <= Key.D9 => args.Key - Key.D1,
                >= Key.NumPad1 and <= Key.NumPad9 => args.Key - Key.NumPad1,
                _ => -1
            };
            if (index < 0) return;
            args.Handled = true;
            SelectClock(index);
        }
    }

    private void SelectClock(int index)
    {
        if (index < 0 || index >= _timers.Length) return;
        SelectedTimer = _timers[index];
        Close();
    }
}
