using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

// Shared by the native Timer Editor and project chooser; rules are scoped to each window.
internal static class NavigatorFormStyles
{
    private static readonly IBrush FieldPaper=Brush.Parse("#F7E8C7"),EditorRule=Brush.Parse("#509A7B48"),EditorDanger=Brush.Parse("#8F3328");
    internal static void StyleButton(Button button,bool primary=false,bool danger=false)
    {
        button.Classes.Remove("modern");button.Classes.Remove("primary");button.Classes.Remove("danger");
        button.Classes.Add("editorAction");if(primary)button.Classes.Add("editorPrimary");if(danger)button.Classes.Add("editorDanger");
        button.MinWidth=0;button.MinHeight=0;
        button.FontFamily=ClockPickerPresentation.Body;
    }

    internal static void Apply(Window window)
    {
        window.Styles.Add(new Style(s=>s.OfType<Button>().Class("editorAction")){Setters={
            new Setter(TemplatedControl.FontSizeProperty,12d),new Setter(TemplatedControl.FontWeightProperty,FontWeight.SemiBold),
            new Setter(TemplatedControl.ForegroundProperty,ClockPickerPresentation.Navy),new Setter(TemplatedControl.BackgroundProperty,Brushes.Transparent),
            new Setter(TemplatedControl.BorderBrushProperty,ClockPickerPresentation.Gold),new Setter(TemplatedControl.BorderThicknessProperty,new Thickness(1)),
            new Setter(TemplatedControl.TemplateProperty,new FuncControlTemplate<Button>((button,scope)=>
            {
                var presenter=new ContentPresenter{Name="PART_ContentPresenter",HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center,
                    Background=Brushes.Transparent,Padding=new(0),BorderThickness=new(0),RenderTransform=new MatrixTransform(Matrix.Identity)};
                scope.Register(presenter.Name,presenter);presenter.Bind(ContentPresenter.ContentProperty,new TemplateBinding(ContentControl.ContentProperty),BindingPriority.Template);
                presenter.Bind(ContentPresenter.ForegroundProperty,button.GetObservable(Button.ForegroundProperty));
                var border=new Border{CornerRadius=new(4),Child=presenter};
                border.Bind(Border.BackgroundProperty,button.GetObservable(Button.BackgroundProperty));border.Bind(Border.BorderBrushProperty,button.GetObservable(Button.BorderBrushProperty));
                border.Bind(Border.BorderThicknessProperty,button.GetObservable(Button.BorderThicknessProperty));border.Bind(Border.PaddingProperty,button.GetObservable(Button.PaddingProperty));return border;
            }))}});
        foreach(var (kind,normal,hover,pressed,ink) in new[]{
            ("editorAction","#00FFFFFF","#66FFFFFF","#F1D796",ClockPickerPresentation.Navy),
            ("editorPrimary","#102B3B","#214A5C","#071E29",ClockPickerPresentation.Cream),
            ("editorDanger","#00FFFFFF","#EDCEB4","#DCA990",EditorDanger)})
        {
            window.Styles.Add(new Style(s=>s.OfType<Button>().Class(kind)){Setters={new Setter(TemplatedControl.BackgroundProperty,Brush.Parse(normal)),new Setter(TemplatedControl.ForegroundProperty,ink)}});
            window.Styles.Add(new Style(s=>s.OfType<Button>().Class(kind).Class(":pointerover")){Setters={new Setter(TemplatedControl.BackgroundProperty,Brush.Parse(hover))}});
            window.Styles.Add(new Style(s=>s.OfType<Button>().Class(kind).Class(":pressed")){Setters={new Setter(TemplatedControl.BackgroundProperty,Brush.Parse(pressed))}});
        }
        window.Styles.Add(new Style(s=>s.OfType<Button>().Class("editorDanger")){Setters={new Setter(TemplatedControl.BorderBrushProperty,EditorDanger)}});
        window.Styles.Add(new Style(s=>s.OfType<Button>().Class("editorAction").Class(":focus-visible")){Setters={new Setter(TemplatedControl.BorderBrushProperty,ClockPickerPresentation.Navy),new Setter(TemplatedControl.BorderThicknessProperty,new Thickness(2))}});
        window.Styles.Add(new Style(s=>s.OfType<Button>().Class("editorAction").Class(":disabled")){Setters={new Setter(Visual.OpacityProperty,.42)}});
        window.Styles.Add(new Style(s=>s.Is<TemplatedControl>().Class("editorField")){Setters={new Setter(TemplatedControl.BackgroundProperty,FieldPaper),new Setter(TemplatedControl.ForegroundProperty,ClockPickerPresentation.Navy),new Setter(TemplatedControl.BorderBrushProperty,EditorRule)}});
        window.Styles.Add(new Style(s=>s.OfType<TextBox>().Class("editorField").Template().OfType<Border>().Name("PART_BorderElement")){Setters={new Setter(Border.BackgroundProperty,FieldPaper),new Setter(Border.BorderBrushProperty,EditorRule)}});
        window.Styles.Add(new Style(s=>s.OfType<TextBox>().Class("editorField").Class(":focus").Template().OfType<Border>().Name("PART_BorderElement")){Setters={new Setter(Border.BorderBrushProperty,ClockPickerPresentation.Gold)}});
        window.Styles.Add(new Style(s=>s.OfType<TextBox>().Class("invalidTime").Template().OfType<Border>().Name("PART_BorderElement")){Setters={new Setter(Border.BorderBrushProperty,EditorDanger)}});
    }
}
