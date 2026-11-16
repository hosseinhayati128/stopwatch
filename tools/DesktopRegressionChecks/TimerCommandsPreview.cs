using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;

// Isolated proposal: synthetic bindings/context, no hooks or persisted settings.
internal static class TimerCommandsPreview
{
    const string Output="design/one-piece-v2/shortcuts/timer-commands-two-stage-preview";
    const double Width=800,Height=280;
    const double QuickWidth=620,QuickHeight=128;
    static readonly IBrush Ink=Brush.Parse("#102B3B"),Muted=Brush.Parse("#655539");
    static TextBlock Text(string text,double size=13,IBrush? ink=null)=>new(){Text=text,FontSize=size,Foreground=ink??Ink,
        FontFamily=StopwatchTypography.Resolve("modern-deck").NameFont,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};
    static Border Badge(string text,bool dark=false)=>new(){Background=Brush.Parse(dark?"#102B3B":"#F8EACA"),
        BorderBrush=Brush.Parse("#B78329"),BorderThickness=new(1),CornerRadius=new(3),Padding=new(4,1),
        Child=new TextBlock{Text=text,Foreground=dark?Brushes.White:Ink,FontSize=11,FontWeight=FontWeight.SemiBold,
            FontFamily=StopwatchTypography.Resolve("modern-deck").NameFont,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
    internal static void Run()
    {
        if(Application.Current is not FrameSizePreviewApp)throw new Exception("Private preview app required.");
        Application.Current.Styles.Add(new FluentTheme());Directory.CreateDirectory(Output);
        foreach(int scale in new[]{70,90,125})Render(scale,false);
        Render(90,true);
    }
    static void Render(int percent,bool custom)
    {
        var keys=AppSettings.DefaultCommandModeKeys();var leader=AppSettings.DefaultLeaderShortcut();
        if(custom){keys[ShortcutAction.StartStop]=0x78;keys[ShortcutAction.Reset]=0;leader=new Shortcut(Shortcut.MOD_CONTROL,0x77);}
        var rows=new List<Grid>();
        Border Group(string title,params (ShortcutAction Action,string Label)[] actions)
        {
            var heading=Text(title,14);heading.Margin=new(0,0,0,5);heading.FontWeight=FontWeight.SemiBold;
            var list=new StackPanel{Spacing=1,Children={heading}};
            foreach(var (action,label) in actions)
            {
                if(!keys.TryGetValue(action,out uint key)||key==0)continue;
                var badge=Badge(Shortcut.FormatKeyName(key));var name=Text(label);
                var row=new Grid{ColumnDefinitions=new("44,8,*"),MinHeight=21,Children={badge,name}};Grid.SetColumn(name,2);
                list.Children.Add(row);rows.Add(row);
            }
            return new Border{BorderBrush=Brush.Parse("#409A7B48"),Child=list,VerticalAlignment=VerticalAlignment.Stretch};
        }
        var timers=Group("Timer controls",(ShortcutAction.StartStop,"Start / stop"),(ShortcutAction.Reset,"Reset timer"),
            (ShortcutAction.Lap,"Record lap"),(ShortcutAction.ToggleClock,"Clock mode"),(ShortcutAction.NewTimer,"New timer"),
            (ShortcutAction.CloseTimer,"Close timer"),(ShortcutAction.EditTimer,"Edit timer"),(ShortcutAction.UndoTimerEdit,"Undo timer edit"));
        var projects=Group("Projects & review",(ShortcutAction.RenameTimer,"Choose project"),(ShortcutAction.OpenDashboard,"Project dashboard"),
            (ShortcutAction.AddRecord,"Add time record"),(ShortcutAction.PeriodicReview,"Activity & Time Review"),(ShortcutAction.SyncActivityWatch,"Sync ActivityWatch"));
        var overlays=Group("Overlay & navigation",(ShortcutAction.ToggleOverlay,"Show / hide overlay"),(ShortcutAction.NextTimer,"Next timer"),
            (ShortcutAction.OpenController,"Open controller"),(ShortcutAction.ShowAllClocks,"Choose a clock"),(ShortcutAction.ToggleActiveClocks,"Running clocks"),
            (ShortcutAction.SeparateOverlay,"Separate overlay"),(ShortcutAction.MergeOverlay,"Merge overlay"),(ShortcutAction.NextSeparatedOverlay,"Next overlay"));
        var groups=new Grid{ColumnDefinitions=new("*,12,*,12,*"),Children={timers,projects,overlays}};Grid.SetColumn(projects,2);Grid.SetColumn(overlays,4);
        projects.BorderThickness=overlays.BorderThickness=new(1,0,0,0);projects.Padding=overlays.Padding=new(12,0,0,0);
        var quick=new Grid{ColumnDefinitions=new("*,12,*,12,*"),RowDefinitions=new("Auto,4,Auto")};
        var quickRows=new List<Grid>();
        foreach(var (action,label) in new[]{(ShortcutAction.StartStop,"Start / stop"),(ShortcutAction.Reset,"Reset timer"),
            (ShortcutAction.EditTimer,"Edit timer"),(ShortcutAction.ToggleOverlay,"Show / hide overlay"),
            (ShortcutAction.NextTimer,"Next timer"),(ShortcutAction.ShowAllClocks,"Choose a clock")})
        {
            if(!keys.TryGetValue(action,out uint key)||key==0)continue;
            var name=Text(label);var row=new Grid{ColumnDefinitions=new("44,8,*"),MinHeight=21,
                Children={Badge(Shortcut.FormatKeyName(key)),name}};Grid.SetColumn(name,2);
            int index=quickRows.Count;Grid.SetColumn(row,(index%3)*2);Grid.SetRow(row,(index/3)*2);
            quick.Children.Add(row);quickRows.Add(row);
        }
        var title=Text("Timer Commands",18);title.FontFamily=StopwatchTypography.Resolve("navigator-bold").NameFont;title.FontWeight=FontWeight.Bold;
        var leaderBadge=Badge(leader.Format().Replace("+"," + "),true);leaderBadge.VerticalAlignment=VerticalAlignment.Top;
        var header=new Grid{ColumnDefinitions=new("*,Auto"),Margin=new(0,0,0,7),Children={title,leaderBadge}};Grid.SetColumn(leaderBadge,1);
        var context=Text("Programming · Esc cancels",11,Muted);
        var expandHint=new StackPanel{Orientation=Orientation.Horizontal,Spacing=6,Children={Badge("F2",true),Text("All commands",11)}};
        var hintHost=new Border{Child=expandHint};
        var footer=new Grid{ColumnDefinitions=new("*,Auto"),Margin=new(0,7,0,0),Children={context,hintHost}};Grid.SetColumn(hintHost,1);
        var body=new Border{Child=quick};
        var layout=new Grid{RowDefinitions=new("Auto,*,Auto"),Children={header,body,footer}};Grid.SetRow(body,1);Grid.SetRow(footer,2);
        double scale=percent/100d;
        var frame=new Border{Width=QuickWidth,Height=QuickHeight,Padding=new(12,10),CornerRadius=new(6),BorderThickness=new(1),BorderBrush=Brush.Parse("#B78329"),Background=Brush.Parse("#EBD3A5"),Child=layout};
        var w=new Window{Width=QuickWidth*scale,Height=QuickHeight*scale,ShowActivated=false,ShowInTaskbar=false,SystemDecorations=SystemDecorations.None,
            Background=Brushes.Transparent,Content=new LayoutTransformControl{LayoutTransform=new ScaleTransform(scale,scale),Child=frame}};
        bool expanded=false;
        w.KeyDown+=(_,e)=>
        {
            if(e.Key!=Avalonia.Input.Key.F2||expanded)return;
            expanded=true;e.Handled=true;
            body.Child=groups;frame.Width=Width;frame.Height=Height;
            w.Position=new PixelPoint(w.Position.X-(int)Math.Round((Width-QuickWidth)*scale*w.RenderScaling/2),w.Position.Y);
            w.Width=Width*scale;w.Height=Height*scale;
            context.Text="Programming · active timer";hintHost.Child=Text("Press a key · Esc cancels",11,Muted);
            ((LayoutTransformControl)w.Content!).InvalidateArrange();
        };
        try
        {
            w.Show();ClockOverlayChecks.Pump(130);
            if(quickRows.Count!=(custom?5:6))throw new Exception("Quick action coverage incorrect.");
            CheckRows(quickRows);Capture(custom?"custom-quick-90":$"quick-{percent}");
            var position=w.Position;
            w.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Avalonia.Input.Key.F2});ClockOverlayChecks.Pump(130);
            if(!expanded||body.Child!=groups||w.Position.Y!=position.Y)throw new Exception("Second F2 did not expand at the same top edge.");
            if(rows.Count!=(custom?20:21))throw new Exception("Command coverage incorrect.");
            CheckRows(rows);
            if(w.ClientSize.Width<Width*scale-2||w.ClientSize.Height<Height*scale-2)throw new Exception("Preview exceeds private desktop.");
            Capture(custom?"custom-expanded-90":$"expanded-{percent}");
            Console.WriteLine($"PASS {percent}% quick hints → F2 → {(custom?"custom mappings":"all 21 commands")} · native layout, no hooks.");
            void CheckRows(IEnumerable<Grid> visibleRows)
            {
                foreach(var row in visibleRows)
                {
                    var edge=row.TranslatePoint(new Point(row.Bounds.Width,row.Bounds.Height),frame);
                    if(edge==null||edge.Value.X>frame.Bounds.Width||edge.Value.Y>footer.TranslatePoint(default,frame)!.Value.Y||row.Bounds.Width<170)
                        throw new Exception("Command row clipped.");
                }
            }
            void Capture(string name)
            {
                using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(w.ClientSize.Width),(int)Math.Ceiling(w.ClientSize.Height)),new Vector(96,96));
                if(scale>=1)bitmap.Render(w);
                else{using var dc=bitmap.CreateDrawingContext();dc.DrawRectangle(new VisualBrush(frame){Stretch=Stretch.Fill,SourceRect=new RelativeRect(frame.Bounds,RelativeUnit.Absolute)},null,new Rect(frame.Bounds.Size));}
                bitmap.Save(Path.Combine(Output,name+".png"));
            }
        }
        finally{w.Close();}
    }
}
