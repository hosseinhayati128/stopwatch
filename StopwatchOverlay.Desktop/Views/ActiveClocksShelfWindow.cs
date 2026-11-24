using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

internal sealed class ActiveClocksShelfWindow : Window
{
    private StackPanel _rows = new();
    private ScrollViewer _scroll = new();
    private Button _hide = new();
    private TextBlock? _count;
    private Control? _empty;
    private StopwatchFrameSurface? _frame;
    private LayoutTransformControl? _scaleHost;
    private const int RowHeight = 51;
    private double _scale = 1;
    private bool _pixel;
    private bool _framed;
    private OverlayWindow? _anchor;
    private sealed record Row(Button Select,Control Time,Button Pause,TextBlock? Name=null,TextBlock? Badge=null,Border? Chrome=null);
    private static readonly StopwatchTypographyProfile Typography=StopwatchTypography.Resolve("navigator-bold");
    private static readonly IBrush ActiveWash=Brush.Parse("#55FFF1C5"), Teal=Brush.Parse("#1C655D");
    private readonly Action<TimerSession> _select;
    private readonly Action<TimerSession> _pause;
    private readonly Dictionary<Guid,Row> _controls = new();
    private Guid[]? _runningIds;

    internal ActiveClocksShelfWindow(Action<TimerSession> select, Action<TimerSession> pause)
    {
        _select = select;
        _pause = pause;
        Title = "Running clocks";
        Width = 350;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowActivated = false;
        ShowInTaskbar = false;
        SystemDecorations = SystemDecorations.None;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        AddButtonStyles();
        BuildLayout();
        Opened+=(_,_)=>RepositionAtAnchor();
        SizeChanged+=(_,_)=>RepositionAtAnchor();
    }

    internal void ApplyAppearance(AppSettings settings)
    {
        bool framed = AppThemeCatalog.Normalize(settings.ThemeMode) == AppThemeCatalog.Nika;
        bool pixel = framed && settings.NikaControllerPixel;
        double scale = framed ? AppUiScale.Normalize(settings.UiScalePercent) / 100 : 1;
        if (framed != _framed || pixel != _pixel)
        {
            _framed = framed;
            _pixel = pixel;
            _scale = scale;
            BuildLayout();
        }
        else if (_scale != scale && _scaleHost != null)
        {
            _scale = scale;
            _scaleHost.LayoutTransform = new ScaleTransform(scale, scale);
            ResizeShelf(_runningIds?.Length ?? 0);
        }
    }

    private void BuildLayout()
    {
        Content=null;_controls.Clear();_runningIds=null;_count=null;_empty=null;_frame=null;_scaleHost=null;
        _rows=new StackPanel{Spacing=_framed?0:4};
        _scroll=new ScrollViewer{Content=_rows,MaxHeight=_framed?5*RowHeight:220,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        _hide=new Button{Name="RunningClocksHide",Content="Hide",HorizontalAlignment=HorizontalAlignment.Right};
        _hide.Click+=(_,_)=>Hide();AutomationProperties.SetName(_hide,"Hide running clocks");
        if(_framed)
        {
            Background=Brushes.Transparent;TransparencyLevelHint=new[]{WindowTransparencyLevel.Transparent};
            SizeToContent=SizeToContent.Manual;
            var header = new Grid { ColumnDefinitions = new("*,Auto") };
            header.Children.Add(new TextBlock { Text="Running clocks", FontFamily=Typography.NameFont, FontWeight=FontWeight.Bold,
                FontSize=24, Foreground=ClockPickerPresentation.Navy, VerticalAlignment=VerticalAlignment.Center });
            _hide.Width=38;_hide.Height=32;_hide.Classes.Add("shelfAction");_hide.Classes.Add("shelfHide");
            ToolTip.SetTip(_hide,"Hide this panel; timers keep running");
            Grid.SetColumn(_hide,1); header.Children.Add(_hide);
            var panel = new Grid { RowDefinitions = new("Auto,7,Auto,*") };
            panel.Children.Add(header);
            _count = new TextBlock { FontFamily=ClockPickerPresentation.Body, FontSize=11,
                Foreground=ClockPickerPresentation.Muted, Margin=new(0,0,0,8) };
            Grid.SetRow(_count,2); panel.Children.Add(_count);
            Grid.SetRow(_scroll,3); panel.Children.Add(_scroll);
            _empty = EmptyState();
            Grid.SetRow(_empty,3); panel.Children.Add(_empty);
            _frame = new StopwatchFrameSurface { ArtworkScale=.14, Padding=new(7), Width=410,
                Background=Brush.Parse("#EBD3A5"), Child=panel };
            _scaleHost = new LayoutTransformControl { Child=_frame, LayoutTransform=new ScaleTransform(_scale,_scale) };
            Content = _scaleHost;
            ResizeShelf(0);
            return;
        }
        ClearValue(BackgroundProperty);TransparencyLevelHint=Array.Empty<WindowTransparencyLevel>();
        Width=350;Height=double.NaN;SizeToContent=SizeToContent.Height;
        var layout = new StackPanel { Margin = new Thickness(10), Spacing = 6 };
        layout.Children.Add(new TextBlock { Text = "Running clocks" });
        layout.Children.Add(_scroll);
        layout.Children.Add(_hide);
        Content = layout;
    }

    private void ResizeShelf(int count)
    {
        double height = count == 0 ? 159 : 119 + Math.Min(5, count) * RowHeight;
        _frame!.Height = height;
        Width = _frame.Width * _scale;
        Height = height * _scale;
    }

    private Control EmptyState()
    {
        var group = new Grid { ColumnDefinitions = new("32,12,*"), VerticalAlignment = VerticalAlignment.Center };
        var glyph = new Avalonia.Controls.Shapes.Path { Stroke=Teal, StrokeThickness=2,
            HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center,
            Data=Geometry.Parse(_pixel
                ? "M10,1 H18 M14,1 V5 M8,5 H20 V8 H23 V11 H26 V21 H23 V24 H20 V27 H8 V24 H5 V21 H2 V11 H5 V8 H8 Z M14,10 V16 H19"
                : "M10,1 H18 M14,1 V5 M14,5 A11,11 0 1 1 13.99,5 Z M14,10 V16 L19,18") };
        group.Children.Add(glyph);
        var copy = new StackPanel { Spacing=4 };
        copy.Children.Add(new TextBlock { Text="No running timers", FontFamily=_pixel?ClockPickerPresentation.Body:Typography.NameFont,
            FontSize=_pixel?16:19, FontWeight=FontWeight.Bold, Foreground=ClockPickerPresentation.Navy });
        copy.Children.Add(new TextBlock { Text="Start or resume a timer to see it here.", FontFamily=ClockPickerPresentation.Body,
            FontSize=12, Foreground=ClockPickerPresentation.Muted });
        Grid.SetColumn(copy,2); group.Children.Add(copy);
        return new Border { BorderBrush=ClockPickerPresentation.Rule, BorderThickness=new(0,1,0,0), Child=group };
    }

    internal void UpdateClocks(IReadOnlyList<TimerSession> timers, TimerSession? active, Func<TimerSession, string> format)
    {
        var running = timers.Where(timer => timer.IsRunning && timer.Mode != 1).ToArray();
        var ids = running.Select(timer => timer.Id).ToArray();
        if (_runningIds == null || !_runningIds.SequenceEqual(ids))
        {
            var focused=FocusManager?.GetFocusedElement();
            int focusIndex=Array.FindIndex(_runningIds??Array.Empty<Guid>(),id=>_controls.TryGetValue(id,out var row)&&
                (ReferenceEquals(focused,row.Select)||ReferenceEquals(focused,row.Pause)));
            Guid? focusedId=focusIndex<0?null:_runningIds![focusIndex];
            bool focusPause=focusedId.HasValue&&ReferenceEquals(focused,_controls[focusedId.Value].Pause);
            var offset=_scroll.Offset;
            _runningIds = ids;
            _rows.Children.Clear();
            _controls.Clear();
            foreach (var timer in running)
            {
                if(_framed){AddFramedRow(timer);continue;}
                var select = new Button { HorizontalAlignment = HorizontalAlignment.Stretch };
                select.Click += (_, _) => _select(timer);
                var time = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0) };
                var pause = new Button { Content = "Pause" };
                pause.Click += (_, _) => _pause(timer);
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
                Grid.SetColumn(time, 1);
                Grid.SetColumn(pause, 2);
                row.Children.Add(select);
                row.Children.Add(time);
                row.Children.Add(pause);
                _rows.Children.Add(row);
                _controls.Add(timer.Id,new(select,time,pause));
            }
            if(_framed)
            {
                ResizeShelf(running.Length);
                _count!.Text=$"{running.Length} running  ·  Select a name to switch";
                _count.IsVisible=_scroll.IsVisible=running.Length>0;
                _empty!.IsVisible=running.Length==0;
            }
            else if (running.Length == 0) _rows.Children.Add(new TextBlock { Text = "No running clocks" });
            _scroll.Offset=offset;
            if(focusIndex>=0)
            {
                Button next=focusedId.HasValue&&_controls.TryGetValue(focusedId.Value,out var retained)
                    ?(focusPause?retained.Pause:retained.Select)
                    :running.Length>0?_controls[running[Math.Min(focusIndex,running.Length-1)].Id].Select:_hide;
                next.Focus(NavigationMethod.Tab);
                // Rebuilt rows have no position until layout; reveal the retained focus afterwards.
                Dispatcher.UIThread.Post(()=>{if(IsVisible&&next.IsFocused)next.BringIntoView();},DispatcherPriority.Loaded);
            }
        }
        foreach (var timer in running)
        {
            var controls = _controls[timer.Id];
            string time=format(timer);
            if (controls.Time is ControllerPixelDigits digits)
            {
                if (digits.Text != time) digits.Text = time;
            }
            else if (controls.Time is TextBlock clock && clock.Text != time) clock.Text = time;
            if(!_framed)
            {
                controls.Select.Content = timer == active ? $"{timer.DisplayName} · Active" : timer.DisplayName;
                continue;
            }
            if(controls.Name!.Text!=timer.DisplayName)
            {
                var name=controls.Name;name.Text=timer.DisplayName;
                bool rtl=false;
                foreach(char c in timer.DisplayName)
                {
                    if(c is >= '\u0590' and <= '\u08FF' or >= '\uFB1D' and <= '\uFEFF'){rtl=true;break;}
                    if(char.IsLetter(c))break;
                }
                name.FlowDirection=rtl?FlowDirection.RightToLeft:FlowDirection.LeftToRight;
                name.FontFamily=rtl?new FontFamily("Segoe UI"):_pixel?ClockPickerPresentation.Body:Typography.NameFont;
                name.FontSize=rtl?16:_pixel?15:18;name.FontWeight=rtl?FontWeight.SemiBold:FontWeight.Bold;
                AutomationProperties.SetName(controls.Select,"Select "+timer.DisplayName);ToolTip.SetTip(controls.Select,timer.DisplayName);
                AutomationProperties.SetName(controls.Pause,"Pause "+timer.DisplayName);ToolTip.SetTip(controls.Pause,"Pause "+timer.DisplayName);
            }
            bool current=timer==active;
            controls.Badge!.IsVisible=current;controls.Chrome!.Background=current?ActiveWash:Brushes.Transparent;
        }
    }

    private void AddFramedRow(TimerSession timer)
    {
        var name=new TextBlock{Name="RunningClockName",Foreground=ClockPickerPresentation.Navy,
            TextTrimming=TextTrimming.CharacterEllipsis,TextWrapping=TextWrapping.Wrap,MaxLines=2};
        var badge=new TextBlock{Name="RunningClockActive",Text="Active",FontFamily=ClockPickerPresentation.Body,FontSize=11,
            FontWeight=FontWeight.Bold,Foreground=Teal,HorizontalAlignment=HorizontalAlignment.Right};
        var select=new Button{Name="RunningClockSelect",Content=name,HorizontalAlignment=HorizontalAlignment.Stretch,Height=RowHeight};
        select.Classes.Add("shelfSelect");select.Click+=(_,_)=>_select(timer);
        Control time=_pixel?new ControllerPixelDigits { FontSize=22, Foreground=ClockPickerPresentation.Navy }
            : new TextBlock{FontFamily=Typography.TimerFont,FontWeight=FontWeight.Black,
                FontSize=22,Foreground=ClockPickerPresentation.Navy};
        time.Name="RunningClockTime";time.FlowDirection=FlowDirection.LeftToRight;
        var values=new StackPanel { VerticalAlignment=VerticalAlignment.Center };
        values.Children.Add(new Viewbox { MaxWidth=145, Stretch=Stretch.Uniform, StretchDirection=StretchDirection.DownOnly,
            HorizontalAlignment=HorizontalAlignment.Right, Child=time });
        values.Children.Add(badge);
        var bars=new StackPanel{Orientation=Orientation.Horizontal,Spacing=3,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        for(int i=0;i<2;i++)bars.Children.Add(new Border{Width=3,Height=10,CornerRadius=new(.5),Background=ClockPickerPresentation.Cream});
        var pause=new Button{Name="RunningClockPause",Content=bars,Width=34,Height=34,VerticalAlignment=VerticalAlignment.Center};
        pause.Classes.Add("shelfAction");pause.Click+=(_,_)=>_pause(timer);
        var grid=new Grid{Height=RowHeight,ColumnDefinitions=new("*,9,Auto,9,34")};grid.Children.Add(select);
        Grid.SetColumn(values,2);grid.Children.Add(values);Grid.SetColumn(pause,4);grid.Children.Add(pause);
        var rule=new Border{Background=ClockPickerPresentation.Rule,Height=1,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false};
        Grid.SetColumnSpan(rule,5);grid.Children.Add(rule);
        var chrome=new Border{Name="RunningClockRow",Child=grid};_rows.Children.Add(chrome);
        _controls.Add(timer.Id,new(select,time,pause,name,badge,chrome));
    }

    private void AddButtonStyles()
    {
        var template=new FuncControlTemplate<Button>((button,scope)=>
        {
            bool select=button.Classes.Contains("shelfSelect"),hide=button.Classes.Contains("shelfHide");
            var presenter=new ContentPresenter{Name="PART_ContentPresenter",HorizontalContentAlignment=select?HorizontalAlignment.Stretch:HorizontalAlignment.Center,
                VerticalContentAlignment=VerticalAlignment.Center,Background=Brushes.Transparent,BorderThickness=new(0),Padding=new(0),RenderTransform=new MatrixTransform(Matrix.Identity)};
            scope.Register(presenter.Name,presenter);
            presenter.Bind(ContentPresenter.ContentProperty,new TemplateBinding(ContentControl.ContentProperty),BindingPriority.Template);
            presenter.Bind(ContentPresenter.ForegroundProperty,button.GetObservable(Button.ForegroundProperty));
            var border=new TodoFrame{Pixel=_pixel&&!select&&!hide,CornerRadius=new(4),Child=presenter,Padding=select?new Thickness(4,0):new Thickness(0)};
            border.Bind(TodoFrame.BackgroundProperty,button.GetObservable(Button.BackgroundProperty));
            border.Bind(TodoFrame.BorderBrushProperty,button.GetObservable(Button.BorderBrushProperty));
            border.Bind(TodoFrame.BorderThicknessProperty,button.GetObservable(Button.BorderThicknessProperty));
            if(!select){border.Width=hide?38:25;border.Height=hide?30:25;border.HorizontalAlignment=HorizontalAlignment.Center;border.VerticalAlignment=VerticalAlignment.Center;}
            return new Border{Background=Brushes.Transparent,Child=border};
        });
        foreach(string kind in new[]{"shelfSelect","shelfAction"})
        {
            bool select=kind=="shelfSelect";
            Styles.Add(new Style(s=>s.OfType<Button>().Class(kind)){Setters={new Setter(TemplatedControl.TemplateProperty,template),
                new Setter(TemplatedControl.BackgroundProperty,select?Brushes.Transparent:ClockPickerPresentation.Navy),
                new Setter(TemplatedControl.BorderBrushProperty,Brushes.Transparent),
                new Setter(TemplatedControl.BorderThicknessProperty,new Thickness(1)),new Setter(TemplatedControl.PaddingProperty,new Thickness(0)),
                new Setter(Layoutable.MinWidthProperty,0d),new Setter(Layoutable.MinHeightProperty,0d),
                new Setter(TemplatedControl.FontFamilyProperty,ClockPickerPresentation.Body),new Setter(TemplatedControl.FontSizeProperty,11d),
                new Setter(TemplatedControl.ForegroundProperty,ClockPickerPresentation.Cream)}});
            Styles.Add(new Style(s=>s.OfType<Button>().Class(kind).Class(":pointerover")){Setters={new Setter(TemplatedControl.BackgroundProperty,Brush.Parse(select?"#66FFFFFF":"#214A5C"))}});
            Styles.Add(new Style(s=>s.OfType<Button>().Class(kind).Class(":pressed")){Setters={new Setter(TemplatedControl.BackgroundProperty,Brush.Parse(select?"#F1D796":"#071E29"))}});
            Styles.Add(new Style(s=>s.OfType<Button>().Class(kind).Class(":focus-visible")){Setters={new Setter(TemplatedControl.BorderBrushProperty,select?ClockPickerPresentation.Gold:ClockPickerPresentation.Cream)}});
        }
        Styles.Add(new Style(s=>s.OfType<Button>().Class("shelfHide")){Setters={new Setter(TemplatedControl.BackgroundProperty,Brushes.Transparent),new Setter(TemplatedControl.ForegroundProperty,ClockPickerPresentation.Navy)}});
        Styles.Add(new Style(s=>s.OfType<Button>().Class("shelfHide").Class(":pointerover")){Setters={new Setter(TemplatedControl.BackgroundProperty,Brush.Parse("#66FFFFFF"))}});
        Styles.Add(new Style(s=>s.OfType<Button>().Class("shelfHide").Class(":pressed")){Setters={new Setter(TemplatedControl.BackgroundProperty,Brush.Parse("#F1D796"))}});
        Styles.Add(new Style(s=>s.OfType<Button>().Class("shelfHide").Class(":focus-visible")){Setters={new Setter(TemplatedControl.BorderBrushProperty,ClockPickerPresentation.Navy)}});
    }

    private void RepositionAtAnchor(){if(_anchor?.IsVisible==true)RepositionUnder(_anchor);}

    internal void RepositionUnder(OverlayWindow overlay)
    {
        _anchor=overlay;
        double scale = overlay.RenderScaling;
        int x = overlay.Position.X;
        int y = overlay.Position.Y + (int)Math.Ceiling((overlay.Bounds.Height + 4) * scale);
        if (Screens.ScreenFromVisual(overlay) is { WorkingArea: var area })
        {
            int width = (int)Math.Ceiling(Width * scale);
            int height = (int)Math.Ceiling((_framed?Height:Bounds.Height) * scale);
            x = Math.Clamp(x, area.X, Math.Max(area.X, area.Right - width));
            if (y + height > area.Bottom)
                y = Math.Max(area.Y, overlay.Position.Y - height - (int)(4 * scale));
        }
        var position = new PixelPoint(x, y);
        if (position != Position) Position = position;
    }
}
