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
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

internal sealed partial class TimerPickerWindow
{
    private Control NavigatorLayout(Button choose,TimerSession? active)
    {
        var now=DateTime.Now;
        _clocks.ItemsSource=_timers.Select((timer,index)=>ClockPickerItem.Create(timer,index,active,now)).ToArray();
        _clocks.ItemTemplate=new FuncDataTemplate<ClockPickerItem>((item,_)=>item==null?new Panel():ClockPickerPresentation.CreateRow(item));
        _clocks.Padding=new(0);_clocks.BorderThickness=new(0);_clocks.Background=Brushes.Transparent;
        ScrollViewer.SetHorizontalScrollBarVisibility(_clocks,ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(_clocks,ScrollBarVisibility.Auto);
        _clocks.Styles.Add(new Style(s=>s.OfType<ListBoxItem>())
        {
            Setters={new Setter(TemplatedControl.TemplateProperty,new FuncControlTemplate<ListBoxItem>((item,scope)=>
            {
                var presenter=new ContentPresenter{Name="PART_ContentPresenter",HorizontalContentAlignment=HorizontalAlignment.Stretch,
                    Background=Brushes.Transparent,BorderThickness=new(0),Padding=new(0)};
                scope.Register(presenter.Name,presenter);
                presenter.Bind(ContentPresenter.ContentProperty,new TemplateBinding(ContentControl.ContentProperty),BindingPriority.Template);
                presenter.Bind(ContentPresenter.ContentTemplateProperty,new TemplateBinding(ContentControl.ContentTemplateProperty),BindingPriority.Template);
                var border=new Border{Name="ClockPickerRowChrome",CornerRadius=new(4),Child=presenter};
                border.Bind(Border.BackgroundProperty,item.GetObservable(ListBoxItem.BackgroundProperty));
                border.Bind(Border.BorderBrushProperty,item.GetObservable(ListBoxItem.BorderBrushProperty));
                border.Bind(Border.BorderThicknessProperty,item.GetObservable(ListBoxItem.BorderThicknessProperty));
                border.Bind(Border.PaddingProperty,item.GetObservable(ListBoxItem.PaddingProperty));
                return border;
            })),new Setter(Layoutable.HeightProperty,44d),new Setter(TemplatedControl.PaddingProperty,new Thickness(6,0)),
                new Setter(TemplatedControl.BackgroundProperty,Brushes.Transparent),
                new Setter(TemplatedControl.BorderBrushProperty,Brushes.Transparent),
                new Setter(TemplatedControl.BorderThicknessProperty,new Thickness(1.5)),
                new Setter(ContentControl.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch)}
        });
        _clocks.Styles.Add(new Style(s=>s.OfType<ListBoxItem>().Class(":pointerover"))
            {Setters={new Setter(TemplatedControl.BackgroundProperty,Brush.Parse("#55FFFFFF"))}});
        _clocks.Styles.Add(new Style(s=>s.OfType<ListBoxItem>().Class(":selected"))
            {Setters={new Setter(TemplatedControl.BackgroundProperty,Brush.Parse("#F1D796")),
                new Setter(TemplatedControl.BorderBrushProperty,ClockPickerPresentation.Gold),new Setter(TemplatedControl.BorderThicknessProperty,new Thickness(1.5))}});
        _clocks.SelectedIndex=_timers.Length==0?-1:Math.Max(0,Array.IndexOf(_timers,active));
        choose.Name="ClockPickerSelect";choose.Classes.Add("clockPickerAction");choose.Margin=new(0,10,0,0);
        choose.Width=123;choose.Height=32;choose.IsEnabled=_clocks.SelectedIndex>=0;
        _clocks.SelectionChanged+=(_,_)=>
        {choose.IsEnabled=_clocks.SelectedIndex>=0;if(_clocks.SelectedIndex>=0)_clocks.ScrollIntoView(_clocks.SelectedIndex);};
        AddNavigatorButtonStyles();
        var close=new Button{Name="ClockPickerClose",Content="×",Width=28,Height=28,FontSize=23,Padding=new(0)};
        close.Classes.Add("clockPickerAction");close.Classes.Add("clockPickerClose");close.Click+=(_,_)=>Close();
        AutomationProperties.SetName(close,"Close clock picker");ToolTip.SetTip(close,"Close (Esc)");
        var header=new Grid{ColumnDefinitions=new("*,Auto"),Margin=new(0,0,0,10)};
        var heading=new StackPanel{Name="ClockPickerDragHeader",Spacing=3,Background=Brushes.Transparent};
        heading.PointerPressed+=(_,e)=>
        {
            if(!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)return;
            BeginMoveDrag(e);e.Handled=true;
        };
        heading.Children.Add(new TextBlock{Text="Choose a clock",FontFamily=StopwatchTypography.Resolve("navigator-bold").NameFont,
            FontWeight=FontWeight.Bold,FontSize=27,Foreground=ClockPickerPresentation.Navy});
        heading.Children.Add(new TextBlock{Text=$"{_timers.Length} clocks",FontFamily=ClockPickerPresentation.Body,FontSize=11,Foreground=ClockPickerPresentation.Muted});
        header.Children.Add(heading);Grid.SetColumn(close,1);header.Children.Add(close);
        var footer=new StackPanel{Spacing=0,Margin=new(0,10,0,0)};
        footer.Children.Add(new TextBlock{Text="1–9 select  ·  ↑↓ move  ·  Enter confirm  ·  Esc close",FontSize=10,
            FontFamily=ClockPickerPresentation.Body,Foreground=ClockPickerPresentation.Muted,TextWrapping=TextWrapping.Wrap});
        footer.Children.Add(choose);
        var layout=new Grid{RowDefinitions=new("Auto,*,Auto")};
        layout.Children.Add(header);Grid.SetRow(_clocks,1);layout.Children.Add(_clocks);Grid.SetRow(footer,2);layout.Children.Add(footer);
        return layout;
    }

    private void AddNavigatorButtonStyles()
    {
        Styles.Add(new Style(s=>s.OfType<Button>().Class("clockPickerAction"))
        {
            Setters={new Setter(TemplatedControl.FontFamilyProperty,ClockPickerPresentation.Body),new Setter(TemplatedControl.FontSizeProperty,12d),
                new Setter(TemplatedControl.FontWeightProperty,FontWeight.SemiBold),new Setter(TemplatedControl.ForegroundProperty,ClockPickerPresentation.Cream),
                new Setter(TemplatedControl.BackgroundProperty,ClockPickerPresentation.Navy),new Setter(TemplatedControl.BorderBrushProperty,ClockPickerPresentation.Gold),
                new Setter(TemplatedControl.BorderThicknessProperty,new Thickness(1.5)),new Setter(Layoutable.MinWidthProperty,0d),new Setter(Layoutable.MinHeightProperty,0d),
                new Setter(TemplatedControl.TemplateProperty,new FuncControlTemplate<Button>((button,scope)=>
                {
                    var presenter=new ContentPresenter{Name="PART_ContentPresenter",HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center,
                        Background=Brushes.Transparent,BorderBrush=Brushes.Transparent,BorderThickness=new(0),Padding=new(0),Opacity=1,
                        RenderTransform=new MatrixTransform(Matrix.Identity)};
                    scope.Register(presenter.Name,presenter);
                    presenter.Bind(ContentPresenter.ContentProperty,new TemplateBinding(ContentControl.ContentProperty),BindingPriority.Template);
                    presenter.Bind(ContentPresenter.ForegroundProperty,button.GetObservable(Button.ForegroundProperty));
                    var border=new Border{CornerRadius=new(4),Child=presenter};
                    border.Bind(Border.BackgroundProperty,button.GetObservable(Button.BackgroundProperty));
                    border.Bind(Border.BorderBrushProperty,button.GetObservable(Button.BorderBrushProperty));
                    border.Bind(Border.BorderThicknessProperty,button.GetObservable(Button.BorderThicknessProperty));
                    border.Bind(Border.PaddingProperty,button.GetObservable(Button.PaddingProperty));return border;
                }))}
        });
        Styles.Add(new Style(s=>s.OfType<Button>().Class("clockPickerAction").Class(":pointerover"))
            {Setters={new Setter(TemplatedControl.BackgroundProperty,Brush.Parse("#214A5C")),new Setter(TemplatedControl.BorderBrushProperty,Brush.Parse("#D7AE5C"))}});
        Styles.Add(new Style(s=>s.OfType<Button>().Class("clockPickerAction").Class(":pressed"))
            {Setters={new Setter(TemplatedControl.BackgroundProperty,Brush.Parse("#071E29"))}});
        Styles.Add(new Style(s=>s.OfType<Button>().Class("clockPickerAction").Class(":focus-visible"))
            {Setters={new Setter(TemplatedControl.BorderBrushProperty,Brush.Parse("#FFF1CE"))}});
        Styles.Add(new Style(s=>s.OfType<Button>().Class("clockPickerAction").Class(":disabled"))
            {Setters={new Setter(Visual.OpacityProperty,.5)}});
        Styles.Add(new Style(s=>s.OfType<Button>().Class("clockPickerClose"))
            {Setters={new Setter(TemplatedControl.BackgroundProperty,Brushes.Transparent),new Setter(TemplatedControl.BorderBrushProperty,Brushes.Transparent),new Setter(TemplatedControl.ForegroundProperty,ClockPickerPresentation.Navy)}});
        Styles.Add(new Style(s=>s.OfType<Button>().Class("clockPickerClose").Class(":pointerover"))
            {Setters={new Setter(TemplatedControl.BackgroundProperty,Brush.Parse("#55FFFFFF")),new Setter(TemplatedControl.BorderBrushProperty,ClockPickerPresentation.Gold)}});
        Styles.Add(new Style(s=>s.OfType<Button>().Class("clockPickerClose").Class(":pressed"))
            {Setters={new Setter(TemplatedControl.BackgroundProperty,Brush.Parse("#40AF7C29"))}});
        Styles.Add(new Style(s=>s.OfType<Button>().Class("clockPickerClose").Class(":focus-visible"))
            {Setters={new Setter(TemplatedControl.BorderBrushProperty,ClockPickerPresentation.Gold)}});
    }
}
