using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class ProjectChooserNativeChecks
{
    private const string Output="design/one-piece-v2/project-chooser/native-implementation";
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Click(Button button)=>button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void KeyDown(Control target,Key key)=>target.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=key});
    private sealed class Fixture:IDisposable
    {
        internal readonly TimerNameWindow Window;
        internal T Named<T>(string name) where T:Control=>Window.FindControl<T>(name)!;
        internal Button Save=>Named<Button>("SaveButton");
        internal Fixture(bool creating=true,string theme=AppThemeCatalog.Nika,double score=1)
        {
            typeof(App).Assembly.GetType("StopwatchOverlay.Desktop.ThemeResources")!.GetMethod("ApplyApplication",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[theme]);
            Window=new TimerNameWindow("Illustration",["Illustration","Reading"],creating,categories:["Creative","Learning"],currentCategory:"Creative",
                getProjectCategory:p=>p=="Reading"?"Learning":"Creative",currentScorePerHour:score,getProjectScore:p=>p=="Reading"?3.5:score,theme:theme);
            Window.ShowActivated=false;Window.ShowInTaskbar=false;Window.Show();ClockOverlayChecks.Pump(40);
        }
        public void Dispose()=>Window.Close();
    }
    internal static void Layout()
    {
        using var f=new Fixture();Require(f.Window.Content is StopwatchFrameSurface,"Chooser is missing the shared native frame.");
        Require(f.Window.SystemDecorations==SystemDecorations.None&&((ISolidColorBrush)f.Window.Background!).Color.A==0,"Chooser has native chrome or opaque exterior.");
        Require(Math.Abs(f.Window.Width-560)<1&&Math.Abs(f.Window.Height-490)<1,"Initial panel differs from approved dimensions.");
        Click(f.Named<Button>("AddProjectButton"));ClockOverlayChecks.Pump(25);Require(Math.Abs(f.Window.Height-602)<1,"New project does not expand to approved height.");
        Click(f.Named<Button>("AddCategoryButton"));ClockOverlayChecks.Pump(25);f.Window.Height=420;ClockOverlayChecks.Pump(25);
        var scroll=f.Named<ScrollViewer>("ChooserScroll");var before=f.Save.TranslatePoint(default,f.Window)!.Value;
        scroll.Offset=new Vector(0,scroll.Extent.Height);ClockOverlayChecks.Pump(20);
        Require(scroll.Extent.Height>scroll.Viewport.Height,"Short panel has no scrollable content.");
        Require(Math.Abs(f.Save.TranslatePoint(default,f.Window)!.Value.Y-before.Y)<1&&before.Y+f.Save.Bounds.Height<f.Window.Bounds.Height-15,"Footer scrolls or clips.");
        Click(f.Named<Button>("AddCategoryButton"));Click(f.Named<Button>("AddProjectButton"));ClockOverlayChecks.Pump(20);
        var work=f.Window.Screens.ScreenFromWindow(f.Window)!.WorkingArea;
        f.Window.Position=new PixelPoint(work.X+30,work.Bottom-(int)Math.Ceiling(510*f.Window.RenderScaling));
        Click(f.Named<Button>("AddProjectButton"));Click(f.Named<Button>("AddCategoryButton"));ClockOverlayChecks.Pump(30);
        Require(f.Window.Position.Y>=work.Y&&f.Window.Position.Y+Math.Ceiling(f.Window.Bounds.Height*f.Window.RenderScaling)<=work.Bottom,
            "Expanding near the screen bottom leaves the fixed footer outside the monitor working area.");
    }
    internal static void ResultsAndDefaults()
    {
        foreach(bool creating in new[]{true,false})
        {
            using var f=new Fixture(creating);Require(Equals(f.Save.Content,creating?"Create timer":"Apply project"),"Wrong primary action.");
            f.Named<ComboBox>("ProjectSelector").SelectedIndex=0;Click(f.Save);
            Require(f.Window.WasAccepted&&f.Window.TimerName=="","No project no longer returns an unnamed timer.");
        }
        using(var f=new Fixture())
        {
            Click(f.Named<Button>("AddProjectButton"));f.Named<TextBox>("NewProjectBox").Text="Discarded draft";
            f.Named<ComboBox>("ProjectSelector").SelectedIndex=2;ClockOverlayChecks.Pump(20);
            Require(!f.Named<Border>("NewProjectPanel").IsVisible,"Existing project selection did not leave draft mode.");
            Click(f.Save);Require(f.Window.TimerName=="Reading"&&f.Window.Category=="Learning"&&f.Window.ScorePerHour==3.5,"Selecting a project from draft mode retains stale category/score.");
        }
        using(var f=new Fixture())
        {
            f.Named<ComboBox>("CategorySelector").SelectedIndex=1;
            Click(f.Named<Button>("AddProjectButton"));Click(f.Named<Button>("AddCategoryButton"));
            f.Named<TextBox>("NewCategoryBox").Text="Discarded category";
            f.Named<ComboBox>("ProjectSelector").SelectedIndex=2;ClockOverlayChecks.Pump(20);
            Require(!f.Named<Border>("NewCategoryPanel").IsVisible,"Selecting a project with the same category leaves a stale category draft active.");
            Click(f.Save);Require(f.Window.Category=="Learning","Category draft overrides the selected project's saved category.");
        }
    }
    internal static void DraftsAndValidation()
    {
        using(var f=new Fixture())
        {
            Click(f.Named<Button>("AddProjectButton"));f.Named<TextBox>("NewProjectBox").Text="   ";Click(f.Save);
            Require(f.Window.IsVisible&&!f.Window.WasAccepted&&f.Named<TextBlock>("ValidationText").IsVisible,"Empty project was accepted or has no error.");
            f.Named<TextBox>("NewProjectBox").Text="Pencil studies";
            Click(f.Named<Button>("AddCategoryButton"));f.Named<TextBox>("NewCategoryBox").Text=" ";KeyDown(f.Named<TextBox>("NewCategoryBox"),Avalonia.Input.Key.Enter);
            Require(f.Window.IsVisible&&!f.Window.WasAccepted&&f.Named<TextBlock>("CategoryValidationText").IsVisible,"Empty category bypassed validation.");
            f.Named<TextBox>("NewCategoryBox").Text="Art";f.Named<NumericUpDown>("ScorePerHourBox").Value=2.5m;
            KeyDown(f.Named<TextBox>("NewCategoryBox"),Avalonia.Input.Key.Enter);
            Require(f.Window.WasAccepted&&f.Window.TimerName=="Pencil studies"&&f.Window.Category=="Art"&&f.Window.ScorePerHour==2.5,"Draft fields lost their results.");
        }
        using(var f=new Fixture())
        {
            Click(f.Named<Button>("AddProjectButton"));Click(f.Named<Button>("AddCategoryButton"));ClockOverlayChecks.Pump(20);
            KeyDown(f.Window,Avalonia.Input.Key.Escape);Require(!f.Named<Border>("NewCategoryPanel").IsVisible&&f.Named<Border>("NewProjectPanel").IsVisible,"Escape did not close category first.");
            KeyDown(f.Window,Avalonia.Input.Key.Escape);Require(f.Window.IsVisible&&!f.Named<Border>("NewProjectPanel").IsVisible,"Escape did not return from new project.");
            KeyDown(f.Window,Avalonia.Input.Key.Escape);Require(!f.Window.IsVisible&&!f.Window.WasAccepted,"Escape accepted or failed to close chooser.");
        }
    }
    internal static void ScoresAndThemes()
    {
        foreach(double score in new[]{double.MaxValue,double.NaN,double.PositiveInfinity,-1})
        {using var f=new Fixture(score:score);Click(f.Save);Require(f.Window.WasAccepted&&f.Window.ScorePerHour>=0&&f.Window.ScorePerHour<=1000,"Unbounded score accepted.");}
        using(var f=new Fixture(theme:AppThemeCatalog.Midnight)){Require(f.Window.Content is not StopwatchFrameSurface,"Plain theme acquired Nika frame.");}
        using(var f=new Fixture())
        {
            Click(f.Named<Button>("AddProjectButton"));var input=f.Named<TextBox>("NewProjectBox");input.Text="برنامه نویسی — long project name";ClockOverlayChecks.Pump(25);
            Require(input.FlowDirection==FlowDirection.RightToLeft&&input.IsFocused,"Persian entry lacks direction/focus.");
            input.Text="English";ClockOverlayChecks.Pump(20);Require(input.FlowDirection==FlowDirection.LeftToRight,"Latin direction not restored.");
        }
    }
    private static ulong time=30000;
    private static void Pointer(Window window,Control target,RawPointerEventType type,RawInputModifiers modifiers=RawInputModifiers.None)
    {
        var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        var platform=typeof(TopLevel).GetProperty("PlatformImpl",flags)!.GetValue(window)!;
        var mouse=(IMouseDevice)platform.GetType().GetProperty("MouseDevice",flags)!.GetValue(platform)!;
        var point=target.TranslatePoint(new Point(target.Bounds.Width/2,target.Bounds.Height/2),window)!.Value;
        var ctor=typeof(RawPointerEventArgs).GetConstructors(flags).Single(c=>c.GetParameters().Length==6&&c.GetParameters()[4].ParameterType==typeof(Point));
        ((Action<RawInputEventArgs>)platform.GetType().GetProperty("Input",flags)!.GetValue(platform)!)((RawInputEventArgs)ctor.Invoke([mouse,time+=30,(IInputRoot)window,type,point,modifiers]));
    }
    internal static void NativeInput()
    {
        using var f=new Fixture();var normal=(f.Save.Background as ISolidColorBrush)?.Color;
        Pointer(f.Window,f.Save,RawPointerEventType.Move);var hover=(f.Save.Background as ISolidColorBrush)?.Color;
        Pointer(f.Window,f.Save,RawPointerEventType.LeftButtonDown,RawInputModifiers.LeftMouseButton);
        Require(f.Save.IsPressed&&hover!=normal&&hover!=(f.Save.Background as ISolidColorBrush)?.Color,"Primary hover/pressed feedback missing.");
        var heading=f.Named<TextBlock>("HeadingText");Pointer(f.Window,heading,RawPointerEventType.Move,RawInputModifiers.LeftMouseButton);Pointer(f.Window,heading,RawPointerEventType.LeftButtonUp);
        Require(!f.Window.WasAccepted,"Dragging off Save accepted.");
        foreach(string name in new[]{"ProjectSelector","CategorySelector"})
        {
            var selector=f.Named<ComboBox>(name);selector.Focus();selector.IsDropDownOpen=true;ClockOverlayChecks.Pump(20);
            KeyDown(selector,Avalonia.Input.Key.Down);KeyDown(selector,Avalonia.Input.Key.Enter);ClockOverlayChecks.Pump(20);
            Require(f.Window.IsVisible&&!selector.IsDropDownOpen,"Selecting dropdown item accepted the dialog or kept popup open.");
            selector.IsDropDownOpen=true;ClockOverlayChecks.Pump(20);KeyDown(selector,Avalonia.Input.Key.Escape);
            Require(f.Window.IsVisible,"Escape from popup closed the whole chooser.");selector.IsDropDownOpen=false;
        }
        f.Save.Focus(NavigationMethod.Tab);Require(f.Save.IsFocused,"Keyboard cannot reach Save.");
    }
    internal static void Evidence()
    {
        Directory.CreateDirectory(Output);
        void Save(Fixture f,string name)
        {
            Require(f.Window.Content is StopwatchFrameSurface,"Cannot capture unimplemented native panel.");
            var root=(Control)f.Window.Content!;using var bmp=new RenderTargetBitmap(new PixelSize((int)root.Bounds.Width,(int)root.Bounds.Height),new Vector(96,96));
            bmp.Render(root);bmp.Save(Path.Combine(Output,name+".png"));
        }
        using(var f=new Fixture())
        {
            Save(f,"new-timer-existing");Click(f.Named<Button>("AddProjectButton"));f.Named<TextBox>("NewProjectBox").Text="Pencil studies";ClockOverlayChecks.Pump(30);Save(f,"new-timer-new-project");
            Click(f.Named<Button>("AddCategoryButton"));ClockOverlayChecks.Pump(25);Click(f.Save);ClockOverlayChecks.Pump(20);Save(f,"new-category-validation");
            f.Named<TextBox>("NewProjectBox").Text="برنامه نویسی — long project name";ClockOverlayChecks.Pump(20);Save(f,"rtl-project");
            f.Window.Height=420;ClockOverlayChecks.Pump(25);Save(f,"short-window");
        }
        using(var f=new Fixture(false)){Save(f,"choose-project");}
    }
}
