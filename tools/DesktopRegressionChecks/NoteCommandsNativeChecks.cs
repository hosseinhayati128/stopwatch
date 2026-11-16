using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Views;

internal static class NoteCommandsNativeChecks
{
    const string Output="design/one-piece-v2/shortcuts/note-commands-implemented";
    internal static void Run()
    {
        Directory.CreateDirectory(Output);
        foreach(int scale in new[]{70,90,125})Render(scale,false,AppThemeCatalog.Nika);
        Render(90,true,AppThemeCatalog.Nika);
        Render(90,false,AppThemeCatalog.Midnight);Render(90,false,AppThemeCatalog.Daylight);
    }
    static void Render(int percent,bool custom,string theme)
    {
        var settings=new AppSettings {ThemeMode=theme,UiScalePercent=percent};
        if(custom)
        {
            settings.NoteLeaderShortcut=new Shortcut(Shortcut.MOD_CONTROL,0x77);
            settings.NoteCommandModeKeys[NoteCommandAction.AddNote]=0x20;
            settings.NoteCommandModeKeys[NoteCommandAction.AddTodo]=0x22;
            settings.NoteCommandModeKeys[NoteCommandAction.AddReminder]=0;
            settings.Typography.Global.SizePercent=125;
        }
        Desktop("ThemeResources","ApplyApplication",theme);
        Desktop("DesktopPageAppearance","Initialize",settings);
        Desktop("DesktopTypography","Initialize",settings.Typography);
        var w=(Window)Activator.CreateInstance(typeof(NoteCommandHintWindow),BindingFlags.Instance|BindingFlags.NonPublic,null,
            [settings.NoteCommandModeKeys,settings],null)!;
        w.ShowActivated=false;
        try
        {
            w.Show();ClockOverlayChecks.Pump(120);
            var frame=w.GetVisualDescendants().OfType<Border>().Single(c=>c.Name=="NoteCommandFrame");
            var rows=w.GetVisualDescendants().OfType<Grid>().Where(c=>c.Name?.StartsWith("NoteCommandRow")==true).ToArray();
            Require(rows.Length==(custom?4:6),"Assigned command count or Space fallback wrong.");
            Require(frame.TranslatePoint(default,w) is {} origin && Math.Abs(origin.X)<1 && Math.Abs(origin.Y)<1,"Frame offset inside window.");
            Require(Math.Abs(w.ClientSize.Width-frame.Bounds.Width*percent/100d)<=1 && Math.Abs(w.ClientSize.Height-frame.Bounds.Height*percent/100d)<=1,"Window/panel sizing differs.");
            var scroll=w.GetVisualDescendants().OfType<ScrollViewer>().Single();
            Require(scroll.Extent.Height<=scroll.Viewport.Height+1,"Commands unnecessarily clipped.");
            foreach(var row in rows)
            {
                var edge=row.TranslatePoint(new Point(row.Bounds.Width,row.Bounds.Height),frame)!.Value;
                Require(row.Bounds.Width>150 && row.Bounds.Height>=row.DesiredSize.Height-1 && edge.X<=frame.Bounds.Width-12 && edge.Y<=frame.Bounds.Height-24,"Command clipped or overlaps footer.");
                var badge=(Border)row.Children[0];var key=(TextBlock)badge.Child!;
                Require(key.Bounds.Width>=key.DesiredSize.Width-1,"Custom key badge clipped.");
            }
            var text=w.GetVisualDescendants().OfType<TextBlock>().Select(t=>t.Text).ToArray();
            Require(text.Contains(custom?"Ctrl + F8":"Win + F3"),"Configured leader missing.");
            Require(text.Contains("Space expands / collapses Todos")==!custom,"Space footer contradicts mapping.");
            var screen=w.Screens.ScreenFromVisual(w)!;var area=screen.WorkingArea;
            Require(w.Position.X>=area.X && w.Position.Y>=area.Y && w.Position.X+w.ClientSize.Width*screen.Scaling<=area.Right+1 && w.Position.Y+w.ClientSize.Height*screen.Scaling<=area.Bottom+1,"Popup outside work area.");
            string suffix=custom?"custom":theme==AppThemeCatalog.Nika?percent.ToString():theme.ToLowerInvariant();
            using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(w.ClientSize.Width),(int)Math.Ceiling(w.ClientSize.Height)),new Vector(96,96));
            if(percent>=100)bitmap.Render(w);
            else {using var dc=bitmap.CreateDrawingContext();dc.DrawRectangle(new VisualBrush(frame){Stretch=Stretch.Fill,SourceRect=new RelativeRect(frame.Bounds,RelativeUnit.Absolute)},null,new Rect(frame.Bounds.Size));}
            bitmap.Save(Path.Combine(Output,$"note-commands-{suffix}.png"));
            Console.WriteLine($"PASS actual Note Commands {suffix}: mappings, Space, layout, bounds and capture");
        }
        finally {w.Close();Desktop("DesktopTypography","Shutdown");Desktop("DesktopPageAppearance","Shutdown");}
    }
    static void Desktop(string type,string method,params object[] args)=>typeof(ControllerWindow).Assembly.GetType("StopwatchOverlay.Desktop."+type)!
        .GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
