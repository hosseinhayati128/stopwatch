using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class DeleteProjectNativeChecks
{
    const string Output = "design/one-piece-v2/delete-project/implemented";
    const string LongName = "Programming · Desktop interface improvements and accessibility review";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static T Named<T>(Window w, string name) where T : Control => w.FindControl<T>(name) ?? w.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    static void Click(Window w, string name) { Named<Button>(w, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); ClockOverlayChecks.Pump(30); }
    static void Key(Control c, Avalonia.Input.Key key)
    {
        c.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
        c.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = key }); ClockOverlayChecks.Pump(25);
    }
    static void Desktop(string type, string method, params object[] args) => typeof(ConfirmationDialogWindow).Assembly.GetType("StopwatchOverlay.Desktop." + type)!.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args);
    sealed class Fixture : IDisposable
    {
        internal readonly AppSettings Settings;
        internal Fixture(int scale = 90, string theme = AppThemeCatalog.Nika)
        {
            Settings = new() { ThemeMode = theme, UiScalePercent = scale, TelegramEnabled = false, HabiticaEnabled = false };
            Desktop("ThemeResources", "ApplyApplication", theme); Desktop("DesktopPageAppearance", "Initialize", Settings); Desktop("DesktopTypography", "Initialize", Settings.Typography);
        }
        internal ConfirmationDialogWindow Create(string project = "Programming", bool blocked = false)
        {
            var w = new ConfirmationDialogWindow("Delete Project", "Delete Project", project, "Delete project", true) { ShowActivated = false, ShowInTaskbar = false };
            typeof(ConfirmationDialogWindow).GetMethod("ConfigureDeleteProject", Private)!.Invoke(w, [project, blocked, Settings]);
            return w;
        }
        internal ConfirmationDialogWindow Open(string project = "Programming", bool blocked = false) { var w = Create(project, blocked); w.Show(); ClockOverlayChecks.Pump(65); return w; }
        public void Dispose() { Desktop("DesktopTypography", "Shutdown"); Desktop("DesktopPageAppearance", "Shutdown"); }
    }
    internal static void Layout()
    {
        Directory.CreateDirectory(Output);
        foreach (int scale in new[] { 70, 90, 125 })
        {
            using var f = new Fixture(scale);
            foreach (string state in new[] { "standard", "long-name", "active-timer" })
            {
                bool blocked = state == "active-timer"; string name = state == "standard" ? "Programming" : LongName;
                var w = f.Open(name, blocked);
                try
                {
                    Require(w.SystemDecorations == SystemDecorations.None && w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(), "Delete Project is missing the approved shared frame.");
                    Require(Math.Abs(w.ClientSize.Width - 540 * scale / 100d) < 1.1 && Math.Abs(w.ClientSize.Height - 420 * scale / 100d) < 1.1, "Dialog native size does not follow page scale.");
                    Require(Named<TextBlock>(w,"DeleteProjectName").Text == name, "Project identity was shortened or changed.");
                    Require(Named<Button>(w,"CancelButton").IsFocused, "Safe action lacks initial focus.");
                    Require(Named<Button>(w,"ConfirmButton").IsVisible != blocked, "Blocked warning exposes deletion.");
                    Bounds(w); var body = Named<ScrollViewer>(w,"DeleteProjectBody"); Require(body.Extent.Height <= body.Viewport.Height + 1, "Approved samples need scrolling.");
                    Capture(w, scale, state);
                    var safe = Named<Button>(w,"CancelButton"); var presenter = safe.GetVisualDescendants().OfType<ContentPresenter>().Single(p=>p.Name=="PART_ContentPresenter");
                    foreach (string pseudo in new[] { ":pointerover", ":pressed" })
                    {
                        ((IPseudoClasses)safe.Classes).Set(pseudo,true); ClockOverlayChecks.Pump(15);
                        Require(presenter.Background is ISolidColorBrush b && b.Color.R < 70, "Safe-action hover/pressed background loses text contrast.");
                        ((IPseudoClasses)safe.Classes).Set(pseudo,false);
                    }
                }
                finally { w.Close(); }
            }
        }
        using (var f = new Fixture())
        {
            f.Settings.Typography.Global.SizePercent = 150; Desktop("DesktopTypography","Apply",f.Settings.Typography);
            var w = f.Open("برنامه‌نویسی و پژوهش درباره طراحی رابط کاربری و یادداشت‌های یک پروژه بلندمدت");
            try
            {
                Require(Named<TextBlock>(w,"DeleteProjectName").FlowDirection == FlowDirection.RightToLeft,"RTL project lost its direction.");
                Bounds(w); Reach(w); Capture(w,90,"large-text");
                f.Settings.UiScalePercent=125; Desktop("DesktopPageAppearance","Apply",f.Settings,true); ClockOverlayChecks.Pump(40);
                Require(Math.Abs(w.ClientSize.Width-675)<1.1,"Live zoom did not resize the project dialog.");
            }
            finally { w.Close(); }
            w=f.Open(LongName,true);
            try
            {
                var screen=new Screen(1.25,new PixelRect(-800,0,800,600),new PixelRect(-800,32,800,548),true);
                typeof(ConfirmationDialogWindow).GetMethod("FitDeleteProjectToScreen",Private)!.Invoke(w,[screen]); ClockOverlayChecks.Pump(35);
                Require(w.Position.X>=-800&&w.Position.Y>=32&&w.Position.X+w.ClientSize.Width*1.25<=1&&w.Position.Y+w.ClientSize.Height*1.25<=581,"Dialog exceeds work area.");
                Bounds(w); Reach(w); Capture(w,125,"small-screen");
            }
            finally { w.Close(); }
        }
    }
    static void Bounds(Window w)
    {
        var frame=w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single();
        var edge=frame.TranslatePoint(new Point(frame.Bounds.Width,frame.Bounds.Height),w)!.Value;
        Require(Math.Abs(edge.X-w.ClientSize.Width)<1.5&&Math.Abs(edge.Y-w.ClientSize.Height)<1.5,"Frame does not fit native window.");
        foreach(string name in new[]{"HeadingText","DeleteProjectCancel","CancelButton","ConfirmButton"})
        {
            var c=Named<Control>(w,name); if(!c.IsVisible)continue;
            var corner=c.TranslatePoint(new Point(c.Bounds.Width,c.Bounds.Height),frame)!.Value;
            Require(corner.X<=frame.Bounds.Width-10&&corner.Y<=frame.Bounds.Height-15,name+" escapes frame.");
        }
        foreach(var t in frame.GetVisualDescendants().OfType<TextBlock>())Require(t.TextLayout.Height<=t.Bounds.Height+1,"Text clipped: "+t.Text);
        var close=Named<Button>(w,"DeleteProjectCancel");var glyph=(Avalonia.Controls.Shapes.Path)close.Content!;
        var title=Named<TextBlock>(w,"HeadingText");
        Require(glyph.IsEffectivelyVisible&&glyph.Bounds.Width>0&&close.TranslatePoint(default,frame)!.Value.X>=title.TranslatePoint(new Point(title.Bounds.Width,0),frame)!.Value.X-1,"Heading hides close glyph.");
        var body=Named<ScrollViewer>(w,"DeleteProjectBody");Require(body.Viewport.Height>60,"Summary viewport is unusable.");
        Require(body.TranslatePoint(new Point(0,body.Bounds.Height),frame)!.Value.Y<Named<Button>(w,"CancelButton").TranslatePoint(default,frame)!.Value.Y,"Summary overlaps actions.");
        foreach(var b in new[]{Named<Button>(w,"CancelButton"),Named<Button>(w,"ConfirmButton")}.Where(b=>b.IsVisible))
        {
            var t=b.GetVisualDescendants().OfType<TextBlock>().Single();var c=t.TranslatePoint(new Point(t.Bounds.Width/2,t.Bounds.Height/2),b)!.Value;
            Require(Math.Abs(c.X-b.Bounds.Width/2)<1&&Math.Abs(c.Y-b.Bounds.Height/2)<1,"Action text is off center.");
        }
    }
    static void Reach(Window w)
    {
        var body=Named<ScrollViewer>(w,"DeleteProjectBody");body.Offset=new(0,10000);ClockOverlayChecks.Pump(25);
        var warning=Named<TextBlock>(w,"DeleteProjectWarning");var bottom=warning.TranslatePoint(new Point(0,warning.Bounds.Height),body)!.Value;
        Require(bottom.Y<=body.Bounds.Height+1&&bottom.Y>=warning.Bounds.Height,"Warning cannot be reached by scrolling.");
        body.Offset=default;ClockOverlayChecks.Pump(25);var top=Named<TextBlock>(w,"DeleteProjectName").TranslatePoint(default,body)!.Value;
        Require(top.Y>=0&&top.Y<body.Bounds.Height,"Project identity cannot be reached.");
    }
    static void Capture(Window w,int scale,string state)
    {
        var frame=w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single();
        using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(w.ClientSize.Width),(int)Math.Ceiling(w.ClientSize.Height)),new Vector(96,96));
        if(scale>=100)bitmap.Render(w);else{using var dc=bitmap.CreateDrawingContext();dc.DrawRectangle(new VisualBrush(frame){Stretch=Stretch.Fill,SourceRect=new RelativeRect(frame.Bounds,RelativeUnit.Absolute)},null,new Rect(frame.Bounds.Size));}
        bitmap.Save(Path.Combine(Output,$"delete-project-{state}-{scale}.png"));
    }
    internal static void NarrowScreen()
    {
        using var f = new Fixture(125);
        var w = f.Open(LongName);
        try
        {
            var screen = new Screen(2, new PixelRect(-720,0,720,1280), new PixelRect(-720,30,720,1200), true);
            typeof(ConfirmationDialogWindow).GetMethod("FitDeleteProjectToScreen",Private)!.Invoke(w,[screen]); ClockOverlayChecks.Pump(45);
            Bounds(w); Reach(w);
            var cancel=Named<Button>(w,"CancelButton");var delete=Named<Button>(w,"ConfirmButton");
            Require(delete.TranslatePoint(default,w)!.Value.Y>=cancel.TranslatePoint(new Point(0,cancel.Bounds.Height),w)!.Value.Y,"Narrow confirmation actions do not stack.");
            Capture(w,125,"narrow-screen");
            cancel.Focus();Key(cancel,Avalonia.Input.Key.Tab);Require(delete.IsFocused,"Narrow confirmation cannot reach Delete by keyboard.");
            Key(delete,Avalonia.Input.Key.Escape);Require(!w.IsVisible&&!w.Result,"Narrow confirmation Escape accepted deletion.");
        }
        finally { w.Close(); }
    }
    internal static void StatusContrast()
    {
        using var f=new Fixture(70);var w=f.Open(LongName,true);
        try
        {
            var ink=((ISolidColorBrush)Named<TextBlock>(w,"DeleteProjectWarning").Foreground!).Color;
            var paper=((ISolidColorBrush)w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single().Background!).Color;
            double Channel(byte c){double v=c/255d;return v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4);}
            double Luminance(Color c)=>.2126*Channel(c.R)+.7152*Channel(c.G)+.0722*Channel(c.B);
            double first=Luminance(ink),second=Luminance(paper);
            Require((Math.Max(first,second)+.05)/(Math.Min(first,second)+.05)>=4.5,"Small blocked-state notice lacks 4.5:1 contrast.");
        }
        finally { w.Close(); }
    }
    internal static void Actions()
    {
        using var f=new Fixture();
        foreach(bool blocked in new[]{false,true})
        {
            var initial=f.Open(blocked:blocked);try{Require(Named<Button>(initial,"CancelButton").IsFocused,"Safe default focus is missing.");Key(initial,Avalonia.Input.Key.Enter);Require(initial.IsVisible&&!initial.Result,"Unfocused Enter accepted a deletion.");}finally{initial.Close();}
            void Check(Action<ConfirmationDialogWindow> action,bool result)
            {
                var w=f.Open(blocked:blocked);int closed=0;w.Closed+=(_,_)=>closed++;
                try{action(w);ClockOverlayChecks.Pump(20);Require(!w.IsVisible&&w.Result==result&&closed==1,"Incorrect dialog completion.");Click(w,"ConfirmButton");Click(w,"CancelButton");Require(w.Result==result&&closed==1,"Repeated event changed completed result.");}finally{w.Close();}
            }
            Check(w=>Click(w,"CancelButton"),false);Check(w=>Click(w,"ConfirmButton"),!blocked);
            foreach(var key in new[]{Avalonia.Input.Key.Enter,Avalonia.Input.Key.Space})
            {
                Check(w=>Key(Named<Button>(w,"CancelButton"),key),false);
                if(!blocked)Check(w=>{var b=Named<Button>(w,"ConfirmButton");b.Focus();Key(b,key);},true);
            }
            Check(w=>Key(Named<Button>(w,"CancelButton"),Avalonia.Input.Key.Escape),false);Check(w=>Click(w,"DeleteProjectCancel"),false);Check(w=>w.Close(),false);
            var owner=new Window{Width=400,Height=300,ShowActivated=false,ShowInTaskbar=false};owner.Show();
            var owned=f.Create(blocked:blocked);var task=owned.ShowDialog<bool>(owner);ClockOverlayChecks.Pump(35);owner.Close();ClockOverlayChecks.Pump(35);
            Require(task.IsCompletedSuccessfully&&!task.Result&&!owned.IsVisible,"Owner shutdown accepted deletion or left a prompt.");
        }
        foreach(string theme in new[]{AppThemeCatalog.Midnight,AppThemeCatalog.Daylight,AppThemeCatalog.Pirate})
        {
            f.Settings.ThemeMode=theme;Desktop("ThemeResources","ApplyApplication",theme);
            foreach(bool blocked in new[]{false,true})
            {
                var w=f.Open(LongName,blocked);try{Require(!w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(),"Nika frame leaked into legacy theme.");Require(Named<Button>(w,"ConfirmButton").IsVisible!=blocked,"Legacy blocked warning offers deletion.");Click(w,blocked?"CancelButton":"ConfirmButton");Require(w.Result==!blocked,"Legacy result changed.");}finally{w.Close();}
            }
        }
    }
    internal static void Dashboard()
    {
        using var f=new Fixture();
        foreach(string scenario in new[]{"cancel","success","active","late-active","missing","owner-close"})
        {
            var history=new ProjectTimeHistory();var now=DateTime.UtcNow;var first=history.AddManualInterval("Programming",now.AddHours(-3),now.AddHours(-2));
            history.AddManualInterval("Programming",now.AddDays(-2),now.AddDays(-2).AddHours(1));var other=history.AddManualInterval("Art",now.AddHours(-2),now.AddHours(-1));
            var timer=Guid.NewGuid();if(scenario=="active")history.StartTracking(timer,"Programming",now.AddMinutes(-10));
            int deletes=0;string? target=null;
            var w=new ProjectDashboardWindow(()=>history.CreateView(DateTime.UtcNow),(_,_,_)=>new(ProjectRecordMutationStatus.NotFound),(_,_,_,_)=>new(ProjectRecordMutationStatus.NotFound),_=>new(ProjectRecordMutationStatus.NotFound),()=>true,()=>null,f.Settings,key=>{deletes++;target=key;return history.DeleteProject(key);}){ShowActivated=false,ShowInTaskbar=false};
            var prompts=new List<ConfirmationDialogWindow>();using var listener=Window.WindowOpenedEvent.AddClassHandler<Window>((window,_)=>{if(window is ConfirmationDialogWindow c)prompts.Add(c);});
            void Select(string key){var combo=Named<ComboBox>(w,"ProjectFilterSelector");combo.SelectedItem=combo.Items.Cast<object>().Single(i=>(string?)i.GetType().GetProperty("Key")!.GetValue(i)==key);ClockOverlayChecks.Pump(20);}
            try
            {
                w.Show();ClockOverlayChecks.Pump(40);Select(first.ProjectKey);Click(w,"DeleteProjectButton");Click(w,"DeleteProjectButton");
                Require(prompts.Count==1,"Repeated request opened duplicate project confirmations.");var dialog=prompts.Single();
                Require(dialog.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any()&&Named<TextBlock>(dialog,"DeleteProjectName").Text=="Programming","Dashboard did not supply current project/settings.");
                if(scenario=="owner-close"){w.Close();ClockOverlayChecks.Pump(30);Require(deletes==0&&!dialog.IsVisible,"Owner close deleted a project.");continue;}
                if(scenario=="cancel"){Click(dialog,"CancelButton");Require(deletes==0&&history.CreateView(now).Intervals.Count==3,"Cancel mutated history.");continue;}
                Select(other.ProjectKey);
                if(scenario=="late-active")history.StartTracking(timer,"Programming",now.AddMinutes(-5));
                if(scenario=="missing")history.DeleteProject("Programming");
                Click(dialog,"ConfirmButton");
                Require(deletes==1&&target==first.ProjectKey,"Deletion used a changed selection or repeated callback.");
                var view=history.CreateView(DateTime.UtcNow);Require(view.Intervals.Any(i=>i.Id==other.Id),"Other project's history was changed.");
                if(scenario is "active" or "late-active")
                {
                    Require(view.Intervals.Count==4&&history.GetOpenInterval(timer)!=null,"Active project lost history or timer state.");
                    Require(prompts.Count==2,"Active timer warning did not open.");var warning=prompts.Last();
                    Require(warning.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any()&&Named<TextBlock>(warning,"DeleteProjectName").Text=="Programming"&&!Named<Button>(warning,"ConfirmButton").IsVisible,"Blocked warning targets the wrong project or offers deletion.");
                    Click(warning,"CancelButton");Require(deletes==1,"Acknowledging warning retried deletion.");
                    history.StopTracking(timer,DateTime.UtcNow);Select(first.ProjectKey);Click(w,"DeleteProjectButton");Click(prompts.Last(),"ConfirmButton");
                    Require(deletes==2&&history.CreateView(DateTime.UtcNow).Intervals.All(i=>i.ProjectKey==other.ProjectKey),"Retry after pausing did not delete the original project's full history.");
                }
                else if(scenario=="missing")
                {
                    Require(prompts.Count==2&&Named<TextBlock>(prompts.Last(),"MessageText").Text!.Contains("not found"),"Missing-project outcome was replaced with an active warning.");Click(prompts.Last(),"ConfirmButton");
                    Require(!Named<ComboBox>(w,"ProjectFilterSelector").Items.Cast<object>().Any(i=>(string?)i.GetType().GetProperty("Key")!.GetValue(i)==first.ProjectKey),"Missing-project list did not refresh.");
                }
                else Require(view.Intervals.Count==1&&view.Projects.All(p=>p.Key!=first.ProjectKey),"Confirmed deletion did not remove all target sessions/project.");
            }
            finally{foreach(var p in prompts.Where(p=>p.IsVisible).ToArray())p.Close();w.Close();}
        }
    }
}
