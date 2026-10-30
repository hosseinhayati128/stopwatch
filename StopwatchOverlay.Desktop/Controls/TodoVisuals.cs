using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Input;
using Avalonia.Data;
using Avalonia.VisualTree;

namespace StopwatchOverlay.Desktop.Controls;

public static class TodoVisuals
{
    public static readonly IBrush Navy = Brush.Parse("#03283E");
    public static readonly IBrush Ivory = Brush.Parse("#FFF0D3");
    public static readonly IBrush Brass = Brush.Parse("#B18A43");
    public static readonly IBrush Gold = Brush.Parse("#E6BA50");
    public static readonly IBrush FineGold = Brush.Parse("#F6D77A");
    public static readonly FontFamily HeadingFont = new("avares://StopwatchOverlay.Desktop/Assets/Fonts/Nika/#Playfair Display ExtraBold");
    public static readonly FontFamily BodyFont = new("avares://StopwatchOverlay.Desktop/Assets/Fonts/Acanthus/#Inter");
    public static readonly FontFamily StrongFont = new("avares://StopwatchOverlay.Desktop/Assets/Fonts/Nika/#Inter ExtraBold");
    public static IBrush Ink(TodoAppearanceProfile p) => p.Background == "midnight-chart" ? Ivory : Navy;
    public static IBrush Muted(TodoAppearanceProfile p) => Brush.Parse(p.Background == "midnight-chart" ? "#C6D1CF" : "#4E6260");
    public static IBrush Paper(TodoAppearanceProfile p) => Brush.Parse(p.Background == "midnight-chart" ? "#082D43" : "#F4D79A");
    public static IBrush Card(TodoAppearanceProfile p) => Brush.Parse(p.Background == "midnight-chart" ? "#D9082D43" : "#BFFFF0C6");
    public static IBrush Priority(int priority) => Brush.Parse(new[] { "#CC3C36", "#AB4A28", "#9C661D", "#806824", "#52704D", "#226B83", "#345E9B", "#60578E", "#815370", "#435655" }[Math.Clamp(priority,1,10)-1]);

    public static void AddShellTrim(Grid root,bool pixel=false)
    {
        var trim=new TodoFrame {IsHitTestVisible=false,Pixel=pixel,CornerRadius=new CornerRadius(9),
            BorderBrush=Gold,InnerBorderBrush=FineGold,BorderThickness=new Thickness(1),ZIndex=20};
        Grid.SetRowSpan(trim,Math.Max(1,root.RowDefinitions.Count));Grid.SetColumnSpan(trim,Math.Max(1,root.ColumnDefinitions.Count));
        root.Children.Add(trim);
    }

    private static readonly string[] PaletteKeys = { "PrimaryTextBrush", "SecondaryTextBrush", "DialogSurfaceBrush", "SurfaceRaisedBrush", "BorderBrush", "BorderSoftBrush", "AccentBrush", "DangerTextBrush", "OnActionTextBrush" };
    public static void Palette(StyledElement target, TodoAppearanceProfile? p)
    {
        if (p == null) { foreach (string key in PaletteKeys) target.Resources.Remove(key); return; }
        target.Resources["PrimaryTextBrush"] = Ink(p);
        target.Resources["SecondaryTextBrush"] = Muted(p);
        target.Resources["DialogSurfaceBrush"] = Paper(p);
        target.Resources["SurfaceRaisedBrush"] = Paper(p);
        target.Resources["BorderBrush"] = Brass;
        target.Resources["BorderSoftBrush"] = Brass;
        target.Resources["AccentBrush"] = Ink(p);
        target.Resources["DangerTextBrush"] = Brush.Parse(p.Background == "midnight-chart" ? "#FFAAA0" : "#9D292D");
        target.Resources["OnActionTextBrush"] = Ivory;
    }

    public static void StyleButton(Button button, TodoAppearanceProfile p, bool primary = false, bool pixel = false, bool danger = false)
    {
        ReleaseContent(button);
        button.FontFamily = primary ? HeadingFont : BodyFont;
        button.Foreground = primary || danger ? Ivory : Ink(p);
        button.Background = danger ? Brush.Parse("#A93634") : primary ? Navy : Card(p);
        button.BorderBrush = primary ? Gold : Brass;
        button.BorderThickness = new Thickness(1);
        button.CornerRadius = new CornerRadius(5);
        button.MinHeight = 30;
        button.Template = new FuncControlTemplate<Button>((b, scope) =>
        {
            var presenter = new ContentPresenter { Name="PART_ContentPresenter", HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
            scope.Register(presenter.Name,presenter);
            presenter.Bind(ContentPresenter.ContentProperty, new TemplateBinding(ContentControl.ContentProperty), BindingPriority.Template);
            presenter.Bind(ContentPresenter.ContentTemplateProperty, new TemplateBinding(ContentControl.ContentTemplateProperty), BindingPriority.Template);
            // Center the visible lettering, allowing for the font's reserved descender space.
            // Whole-pixel offsets keep native text crisp at the overlay's normal size.
            if(b.Content is string||primary)
                presenter.RenderTransform=new TranslateTransform(0,-Math.Round(b.FontSize*(b.FontFamily.Equals(HeadingFont)?.10:.04)));
            var frame = new TodoFrame { Pixel = pixel, Child = presenter, CornerRadius = new CornerRadius(5), InnerBorderBrush=primary?FineGold:null };
            frame.Bind(Border.BackgroundProperty, b.GetObservable(Button.BackgroundProperty));
            frame.Bind(Border.BorderBrushProperty, b.GetObservable(Button.BorderBrushProperty));
            frame.Bind(Border.BorderThicknessProperty, b.GetObservable(Button.BorderThicknessProperty));
            frame.Bind(Border.PaddingProperty, b.GetObservable(Button.PaddingProperty));
            return frame;
        });
        if(button.Classes.Contains("todoStyledButton"))return;
        button.Classes.Add("todoStyledButton");
        button.Styles.Add(new Style(s => s.OfType<Button>().Class(":pointerover")) { Setters = { new Setter(Visual.OpacityProperty, .86) } });
        button.Styles.Add(new Style(s => s.OfType<Button>().Class(":disabled")) { Setters = { new Setter(Visual.OpacityProperty, .55) } });
        button.Styles.Add(new Style(s => s.OfType<Button>().Class(":focus-visible")) { Setters = { new Setter(Border.BorderBrushProperty, Brush.Parse("#3F9DAF")), new Setter(Border.BorderThicknessProperty, new Thickness(2)) } });
    }

    public static void Checkbox(CheckBox box, TodoAppearanceProfile p, bool pixel = false, bool child = false)
    {
        ReleaseContent(box);
        box.Foreground = Ink(p); box.FontFamily = BodyFont;
        if(child) {box.MinHeight=20;box.Padding=new Thickness(0);}
        box.Template = new FuncControlTemplate<CheckBox>((b, scope) =>
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Background = Brushes.Transparent };
            var glyph = new TodoCheckGlyph(b, Ink(p), pixel) { Width = child ? 19 : 23, Height = child ? 19 : 23, VerticalAlignment = VerticalAlignment.Center };
            var content = new ContentPresenter { Name="PART_ContentPresenter", Margin = new Thickness(9,0,0,0), VerticalAlignment = VerticalAlignment.Center };
            scope.Register(content.Name,content);
            content.Bind(ContentPresenter.ContentProperty, new TemplateBinding(ContentControl.ContentProperty), BindingPriority.Template);
            Grid.SetColumn(content,1); grid.Children.Add(glyph); grid.Children.Add(content);
            var border = new Border { Child = grid };
            border.Bind(Border.PaddingProperty,b.GetObservable(CheckBox.PaddingProperty));
            return border;
        });
    }

    public static void PriorityBadge(Button badge, int priority, TodoAppearanceProfile p)
    {
        bool pixel = p.Priorities == "pixel-ranks", stamp = p.Priorities == "ledger-stamps";
        badge.Classes.Remove("modern");
        badge.Width = badge.Height = badge.MinHeight = 28;
        badge.Margin = new Thickness(0,0,10,0); badge.Padding = new Thickness(0);badge.VerticalAlignment=VerticalAlignment.Center;
        badge.Background = stamp ? Brushes.Transparent : Priority(priority);
        badge.BorderBrush = stamp ? Priority(priority) : Ivory;
        badge.BorderThickness = new Thickness(1);
        badge.Content = new TextBlock { Text = priority.ToString(), FontSize = 14, FontWeight = FontWeight.Bold,
            FontFamily = StrongFont, Foreground = stamp ? Priority(priority) : Ivory, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, RenderTransform=new TranslateTransform(0,-1) };
        badge.Template = new FuncControlTemplate<Button>((b, scope) => {
            var frame = new TodoFrame { Pixel = pixel, CornerRadius = new CornerRadius(stamp ? 3 : 16), Padding = new Thickness(0) };
            frame.Bind(Border.BackgroundProperty,b.GetObservable(Button.BackgroundProperty)); frame.Bind(Border.BorderBrushProperty,b.GetObservable(Button.BorderBrushProperty)); frame.Bind(Border.BorderThicknessProperty,b.GetObservable(Button.BorderThicknessProperty));
            var content = new ContentPresenter { Name="PART_ContentPresenter", HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
            scope.Register(content.Name,content);
            content.Bind(ContentPresenter.ContentProperty,new TemplateBinding(ContentControl.ContentProperty), BindingPriority.Template); frame.Child = content; return frame;
        });
    }

    public static void FramePixelInput(Panel parent, TemplatedControl input, TodoAppearanceProfile p)
    {
        int index=parent.Children.IndexOf(input);
        if(index<0)return;
        var frame=new TodoFrame {Pixel=true,Background=Card(p),BorderBrush=Brass,BorderThickness=new Thickness(1),
            Margin=input.Margin,Width=input.Width,Height=input.Height,MinWidth=input.MinWidth,MinHeight=input.MinHeight,
            HorizontalAlignment=input.HorizontalAlignment,VerticalAlignment=input.VerticalAlignment};
        Grid.SetColumn(frame,Grid.GetColumn(input));Grid.SetRow(frame,Grid.GetRow(input));
        Grid.SetColumnSpan(frame,Grid.GetColumnSpan(input));Grid.SetRowSpan(frame,Grid.GetRowSpan(input));
        parent.Children.RemoveAt(index);
        input.Margin=new Thickness(0);input.Width=input.Height=double.NaN;
        input.HorizontalAlignment=HorizontalAlignment.Stretch;input.VerticalAlignment=VerticalAlignment.Stretch;
        input.Background=Brushes.Transparent;input.BorderThickness=new Thickness(0);
        frame.Child=input;parent.Children.Insert(index,frame);
    }

    // Avalonia keeps a control-valued Content in the old presenter until its next
    // layout. Release it before replacing a template, including theme restoration.
    public static void ReleaseContent(ContentControl control)
    {
        foreach(var presenter in control.GetVisualDescendants().OfType<ContentPresenter>().ToArray())
            if(ReferenceEquals(presenter.Content,control.Content)) {presenter.Content=null;presenter.UpdateChild();}
    }

    public static void RowSurface(Grid row, TodoAppearanceProfile p)
    {
        row.Margin = new Thickness(0,0,0,p.Rows == "ships-ledger" ? 0 : 6);
        var surface = new TodoFrame { IsHitTestVisible = false, Pixel = p.Rows == "pixel-voyage", BorderBrush = p.Rows=="pixel-voyage"?Brush.Parse("#876326"):Brass,
            InnerBorderBrush=p.Rows=="pixel-voyage"?Gold:null,
            Background = p.Rows == "ships-ledger" ? Brushes.Transparent : Card(p),
            BorderThickness = p.Rows == "ships-ledger" ? new Thickness(0,0,0,.65) : p.Rows == "cartographers-notes" ? new Thickness(2,0,0,0) : new Thickness(1),
            CornerRadius = new CornerRadius(4) };
        Grid.SetColumnSpan(surface,4); Grid.SetRowSpan(surface,p.Rows=="pixel-voyage"?4:5); row.Children.Insert(0,surface);
    }

    private sealed class TodoCheckGlyph : Control
    {
        private readonly CheckBox _owner; private readonly IBrush _ink; private readonly bool _pixel;
        public TodoCheckGlyph(CheckBox owner, IBrush ink, bool pixel)
        {
            _owner=owner; _ink=ink; _pixel=pixel; IsHitTestVisible=false;
            AttachedToVisualTree+=(_,_)=>owner.PropertyChanged+=OwnerChanged;
            DetachedFromVisualTree+=(_,_)=>owner.PropertyChanged-=OwnerChanged;
        }
        private void OwnerChanged(object? sender,AvaloniaPropertyChangedEventArgs e)
        { if(e.Property==CheckBox.IsCheckedProperty||e.Property==HoldToCompleteCheckBox.HoldProgressProperty||e.Property==InputElement.IsFocusedProperty)InvalidateVisual(); }
        public override void Render(DrawingContext c)
        {
            var r = new Rect(Bounds.Size).Deflate(1.4);
            IBrush? fill = _owner.IsChecked == true ? Navy : null;
            var pen = new Pen(_owner.IsFocused ? Brush.Parse("#298DA1") : _ink,1.8);
            if (_pixel) c.DrawGeometry(fill,pen,TodoFrame.Outline(r)); else c.DrawRectangle(fill,pen,r,3,3);
            if (_owner.IsChecked == true)
            {
                var path = new StreamGeometry(); using(var p=path.Open()) { p.BeginFigure(new Point(r.Left+r.Width*.22,r.Top+r.Height*.5),false); p.LineTo(new Point(r.Left+r.Width*.43,r.Top+r.Height*.71)); p.LineTo(new Point(r.Left+r.Width*.8,r.Top+r.Height*.26)); p.EndFigure(false); }
                c.DrawGeometry(null,new Pen(Ivory,2.1),path);
            }
            if (_owner is HoldToCompleteCheckBox h && h.HoldProgress > 0)
                c.FillRectangle(Brass,new Rect(r.Left,r.Bottom-2,r.Width*h.HoldProgress,2));
        }
    }
}
