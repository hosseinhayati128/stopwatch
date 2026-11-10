using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
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
    private TextBlock? _hint;
    private Grid? _layout;
    private bool _framed;
    private OverlayWindow? _anchor;
    private sealed record Row(Button Select,TextBlock Time,Button Pause,TextBlock? Name=null,Border? Badge=null,Border? Chrome=null);
    private static readonly StopwatchTypographyProfile Typography=StopwatchTypography.Resolve("navigator-bold");
    private static readonly IBrush ActiveWash=Brush.Parse("#56F7E6B2"),Rule=Brush.Parse("#499A7B48");
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

    internal void ApplyTheme(string theme)
    {
        bool framed=AppThemeCatalog.Normalize(theme)==AppThemeCatalog.Nika;
        if(framed==_framed)return;
        _framed=framed;
        BuildLayout();
    }

    private void BuildLayout()
    {
        Content=null;_controls.Clear();_runningIds=null;_count=null;_hint=null;_layout=null;
        _rows=new StackPanel{Spacing=_framed?0:4};
        _scroll=new ScrollViewer{Content=_rows,MaxHeight=220,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        _hide=new Button{Name="RunningClocksHide",Content="Hide",HorizontalAlignment=HorizontalAlignment.Right};
        _hide.Click+=(_,_)=>Hide();AutomationProperties.SetName(_hide,"Hide running clocks");
        if(_framed)
        {
            Background=Brushes.Transparent;TransparencyLevelHint=new[]{WindowTransparencyLevel.Transparent};
            SizeToContent=SizeToContent.Manual;Height=194;
            var header=new Grid{Height=49};
            header.Children.Add(new TextBlock{Text="Running clocks",FontFamily=Typography.NameFont,FontWeight=FontWeight.Bold,
                FontSize=24,Foreground=ClockPickerPresentation.Navy,VerticalAlignment=VerticalAlignment.Top,Margin=new(0,-1,0,0)});
            _count=new TextBlock{FontFamily=ClockPickerPresentation.Body,FontSize=10,Foreground=ClockPickerPresentation.Muted,
                VerticalAlignment=VerticalAlignment.Top,Margin=new(1,30,0,0)};
            header.Children.Add(_count);
            _hide.Width=61;_hide.Height=34;_hide.Classes.Add("shelfAction");_hide.Classes.Add("shelfHide");
            ToolTip.SetTip(_hide,"Hide this panel; timers keep running");
            var footer=new Grid{ColumnDefinitions=new("*,Auto")};
            _hint=new TextBlock{Text="Select a clock to switch",FontFamily=ClockPickerPresentation.Body,FontSize=9,
                Foreground=ClockPickerPresentation.Muted,VerticalAlignment=VerticalAlignment.Bottom,Margin=new(1,0,0,11)};
            footer.Children.Add(_hint);Grid.SetColumn(_hide,1);_hide.VerticalAlignment=VerticalAlignment.Bottom;footer.Children.Add(_hide);
            _layout=new Grid{RowDefinitions=new("49,*,57")};_layout.Children.Add(header);
            Grid.SetRow(_scroll,1);_layout.Children.Add(_scroll);Grid.SetRow(footer,2);_layout.Children.Add(footer);
            Content=new StopwatchFrameSurface{ArtworkScale=.14,Padding=new(7),Background=Brush.Parse("#EBD3A5"),Child=_layout};
            return;
        }
        ClearValue(BackgroundProperty);TransparencyLevelHint=Array.Empty<WindowTransparencyLevel>();
        Height=double.NaN;SizeToContent=SizeToContent.Height;
        var layout = new StackPanel { Margin = new Thickness(10), Spacing = 6 };
        layout.Children.Add(new TextBlock { Text = "Running clocks" });
        layout.Children.Add(_scroll);
        layout.Children.Add(_hide);
        Content = layout;
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
                Height=running.Length==0?194:Math.Min(5,running.Length)*44+160;
                _count!.Text=$"{running.Length} running";_hint!.IsVisible=running.Length>0;
                _layout!.RowDefinitions[2].Height=new GridLength(running.Length==0?31:57);
                if(running.Length==0)
                {
                    var empty=new StackPanel{Spacing=7,Margin=new(0,6,0,0)};
                    empty.Children.Add(new TextBlock{Text="No running timers",FontFamily=Typography.NameFont,FontSize=18,
                        FontWeight=FontWeight.Bold,Foreground=ClockPickerPresentation.Navy});
                    empty.Children.Add(new TextBlock{Text="Start a timer to see it here.",FontFamily=ClockPickerPresentation.Body,
                        FontSize=10,Foreground=ClockPickerPresentation.Muted});_rows.Children.Add(empty);
                }
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
            if(!_framed)
            {
                controls.Select.Content = timer == active ? $"{timer.DisplayName} · Active" : timer.DisplayName;
                controls.Time.Text=time;continue;
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
                name.FontFamily=rtl?new FontFamily("Segoe UI"):Typography.NameFont;
                name.FontSize=rtl?16:18;name.FontWeight=rtl?FontWeight.SemiBold:FontWeight.Bold;
                AutomationProperties.SetName(controls.Select,"Select "+timer.DisplayName);ToolTip.SetTip(controls.Select,timer.DisplayName);
                AutomationProperties.SetName(controls.Pause,"Pause "+timer.DisplayName);ToolTip.SetTip(controls.Pause,"Pause "+timer.DisplayName);
            }
            if(controls.Time.Text!=time)
            {
                var measured=new FormattedText(time,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,
                    new Typeface(Typography.TimerFont,FontStyle.Normal,FontWeight.Black),20,ClockPickerPresentation.Navy);
                controls.Time.FontSize=Math.Min(20,20*129/Math.Max(1,measured.WidthIncludingTrailingWhitespace));
                controls.Time.Text=time;
            }
            bool current=timer==active;
            controls.Badge!.IsVisible=current;controls.Chrome!.Background=current?ActiveWash:Brushes.Transparent;
            controls.Name.VerticalAlignment=current?VerticalAlignment.Top:VerticalAlignment.Center;
        }
    }

    private void AddFramedRow(TimerSession timer)
    {
        var name=new TextBlock{Name="RunningClockName",Foreground=ClockPickerPresentation.Navy,
            TextTrimming=TextTrimming.CharacterEllipsis,TextWrapping=TextWrapping.NoWrap,MaxLines=1};
        var badge=new Border{Width=39,Height=13,Background=ClockPickerPresentation.Navy,BorderBrush=ClockPickerPresentation.Gold,
            BorderThickness=new(.6),CornerRadius=new(3),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,
            Margin=new(1,25,0,0),Child=new TextBlock{Text="ACTIVE",FontFamily=ClockPickerPresentation.Body,FontSize=7,
                Foreground=ClockPickerPresentation.Cream,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
        var label=new Grid{Height=40};label.Children.Add(name);label.Children.Add(badge);
        var select=new Button{Name="RunningClockSelect",Content=label,HorizontalAlignment=HorizontalAlignment.Stretch,Height=44};
        select.Classes.Add("shelfSelect");select.Click+=(_,_)=>_select(timer);
        var time=new TextBlock{Name="RunningClockTime",FontFamily=Typography.TimerFont,FontWeight=FontWeight.Black,
            Foreground=ClockPickerPresentation.Navy,VerticalAlignment=VerticalAlignment.Center,FlowDirection=FlowDirection.LeftToRight,TextWrapping=TextWrapping.NoWrap};
        var bars=new StackPanel{Orientation=Orientation.Horizontal,Spacing=3,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        for(int i=0;i<2;i++)bars.Children.Add(new Border{Width=3,Height=10,CornerRadius=new(.5),Background=ClockPickerPresentation.Cream});
        var pause=new Button{Name="RunningClockPause",Content=bars,Width=34,Height=34,VerticalAlignment=VerticalAlignment.Center};
        pause.Classes.Add("shelfAction");pause.Click+=(_,_)=>_pause(timer);
        var grid=new Grid{Height=44,ColumnDefinitions=new("*,12,Auto,6,34")};grid.Children.Add(select);
        Grid.SetColumn(time,2);grid.Children.Add(time);Grid.SetColumn(pause,4);grid.Children.Add(pause);
        var rule=new Border{BorderBrush=Rule,BorderThickness=new(0,0,0,.7),Margin=new(5,0,5,0),IsHitTestVisible=false};
        Grid.SetColumnSpan(rule,5);grid.Children.Add(rule);
        var chrome=new Border{CornerRadius=new(4),Child=grid};_rows.Children.Add(chrome);
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
            var border=new Border{CornerRadius=new(4),Child=presenter,Padding=select?new Thickness(4,0):new Thickness(0)};
            border.Bind(Border.BackgroundProperty,button.GetObservable(Button.BackgroundProperty));
            border.Bind(Border.BorderBrushProperty,button.GetObservable(Button.BorderBrushProperty));
            border.Bind(Border.BorderThicknessProperty,button.GetObservable(Button.BorderThicknessProperty));
            if(!select){border.Width=hide?53:26;border.Height=26;border.HorizontalAlignment=HorizontalAlignment.Center;border.VerticalAlignment=VerticalAlignment.Center;}
            return new Border{Background=Brushes.Transparent,Child=border};
        });
        foreach(string kind in new[]{"shelfSelect","shelfAction"})
        {
            bool select=kind=="shelfSelect";
            Styles.Add(new Style(s=>s.OfType<Button>().Class(kind)){Setters={new Setter(TemplatedControl.TemplateProperty,template),
                new Setter(TemplatedControl.BackgroundProperty,select?Brushes.Transparent:ClockPickerPresentation.Navy),
                new Setter(TemplatedControl.BorderBrushProperty,select?Brushes.Transparent:ClockPickerPresentation.Gold),
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
