using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

// Design fixture only: real controls, synthetic defaults, no live settings or hotkey registration.
internal static class ShortcutsMappingsPreview
{
    const string Output = "design/one-piece-v2/shortcuts/mappings-native-preview";
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly IBrush Ink=Brush.Parse("#102B3B"), Muted=Brush.Parse("#655539");
    static TextBlock Text(string value,double size=13,IBrush? color=null)=>new(){Text=value,FontSize=size,Foreground=color??Ink,
        FontFamily=StopwatchTypography.Resolve("modern-deck").NameFont,TextWrapping=TextWrapping.Wrap};
    static Border Card(Control content)=>new(){Padding=new(18),CornerRadius=new(7),BorderThickness=new(1),
        BorderBrush=Brush.Parse("#509A7B48"),Background=Brush.Parse("#40FFF6E4"),Child=content,VerticalAlignment=VerticalAlignment.Top};
    static Button Action(string label)=>new(){Content=label,Classes={"editorAction"},Padding=new(14,9),MinHeight=38};
    static T Field<T>(ShortcutsWindow w,string name)=>(T)typeof(ShortcutsWindow).GetField(name,Hidden)!.GetValue(w)!;
    static T Find<T>(Control w,string name) where T:Control=>w.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    static void Desktop(string method,params object[] args)=>typeof(ShortcutsWindow).Assembly.GetType("StopwatchOverlay.Desktop.DesktopPageAppearance")!
        .GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static void Pump()=>ClockOverlayChecks.Pump(80);
    static void Click(Button button)=>button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    internal static void Run()
    {
        Require(Application.Current is FrameSizePreviewApp,"Isolated preview app required.");
        Application.Current!.Styles.Add(new FluentTheme());Directory.CreateDirectory(Output);
        foreach(int scale in new[]{70,90,125})Render(scale);
    }

    static void Render(int percent)
    {
        Desktop("Initialize",new AppSettings{ThemeMode=AppThemeCatalog.Nika,UiScalePercent=percent});
        var w=new ShortcutsWindow{ShowActivated=false,ShowInTaskbar=false};
        try
        {
            (string Description,TextBox Field,Button Clear) ReadRow(Grid row)=>(row.Children.OfType<TextBlock>().Single().Text!,row.Children.OfType<TextBox>().Single(),row.Children.OfType<Button>().Single());
            var timerRows=Field<StackPanel>(w,"WinF2CommandsPanel").Children.Cast<Grid>().ToDictionary(r=>(ShortcutAction)r.Children.OfType<TextBox>().Single().Tag!,ReadRow);
            var noteRows=Field<StackPanel>(w,"WinF3CommandsPanel").Children.Cast<Grid>().ToDictionary(r=>(NoteCommandAction)r.Children.OfType<TextBox>().Single().Tag!,ReadRow);
            typeof(ShortcutsWindow).GetMethod("ApplyNavigatorLayout",Hidden)!.Invoke(w,[AppThemeCatalog.Nika]);
            typeof(ShortcutsWindow).GetMethod("SelectShortcutPage",Hidden)!.Invoke(w,[false]);
            var mappings=Field<StackPanel>(w,"_commandMappingPage");var host=(StackPanel)mappings.Parent!;host.Children.Remove(mappings);
            Require(timerRows.Count==21&&noteRows.Count==5,"Command catalog changed; review grouping.");
            var fields=new List<TextBox>();
            Control Row((string Description,TextBox Field,Button Clear) old,string title)
            {
                var (description,field,clear)=old;
                ((Panel)field.Parent!).Children.Remove(field);((Panel)clear.Parent!).Children.Remove(clear);
                field.Classes.Clear();field.Classes.Add("editorField");field.Width=94;field.MinHeight=38;field.Margin=default;
                field.Padding=new(8,8);field.FontSize=13;field.VerticalAlignment=VerticalAlignment.Center;field.VerticalContentAlignment=VerticalAlignment.Center;
                field.FontFamily=Text("").FontFamily;AutomationProperties.SetName(field,title+" command key");fields.Add(field);
                clear.Classes.Clear();clear.Classes.Add("editorAction");clear.Padding=new(10,8);clear.MinHeight=38;clear.VerticalAlignment=VerticalAlignment.Center;
                AutomationProperties.SetName(clear,"Clear "+title);ToolTip.SetTip(field,"Select to record one command key. Escape is reserved for leaving command mode.");
                var copy=Text(title,14);copy.VerticalAlignment=VerticalAlignment.Center;ToolTip.SetTip(copy,description);
                var row=new Grid{ColumnDefinitions=new("*,10,94,8,Auto"),MinHeight=40,Children={copy,field,clear}};
                Grid.SetColumn(field,2);Grid.SetColumn(clear,4);
                return new Border{BorderBrush=Brush.Parse("#259A7B48"),BorderThickness=new(0,0,0,1),Padding=new(0,2),Child=row};
            }
            Border Group(string title,string hint,params (ShortcutAction Action,string Title)[] items)
            {
                var heading=Text(title,19);heading.Margin=new(0,0,0,7);ToolTip.SetTip(heading,hint);
                var list=new StackPanel{Spacing=2,Children={heading}};
                foreach(var item in items)list.Children.Add(Row(timerRows[item.Action],item.Title));
                return Card(list);
            }
            var timer=Group("Timer controls","Start, adjust and manage your timers",
                (ShortcutAction.StartStop,"Start / stop"),(ShortcutAction.Reset,"Reset timer"),(ShortcutAction.Lap,"Record lap"),
                (ShortcutAction.ToggleClock,"Clock mode"),(ShortcutAction.NewTimer,"New timer"),(ShortcutAction.CloseTimer,"Close timer"),
                (ShortcutAction.EditTimer,"Edit timer"),(ShortcutAction.UndoTimerEdit,"Undo timer edit"));
            var projects=Group("Projects & review","Organize records and review your time",
                (ShortcutAction.RenameTimer,"Choose project"),(ShortcutAction.OpenDashboard,"Project dashboard"),(ShortcutAction.AddRecord,"Add time record"),
                (ShortcutAction.PeriodicReview,"Activity & Time Review"),(ShortcutAction.SyncActivityWatch,"Sync ActivityWatch"));
            var overlays=Group("Overlay & navigation","Find a clock or change what is visible",
                (ShortcutAction.ToggleOverlay,"Show / hide overlay"),(ShortcutAction.NextTimer,"Next timer"),(ShortcutAction.OpenController,"Open controller"),
                (ShortcutAction.ShowAllClocks,"Choose a clock"),(ShortcutAction.ToggleActiveClocks,"Running clocks"),
                (ShortcutAction.SeparateOverlay,"Separate overlay"),(ShortcutAction.MergeOverlay,"Merge overlay"),(ShortcutAction.NextSeparatedOverlay,"Next overlay"));
            var timerGrid=new Grid{Children={timer,projects,overlays}};
            var noteList=new StackPanel{Spacing=2,Children={new TextBlock{Text="Notes & Todos",FontSize=19,Foreground=Ink,Margin=new(0,0,0,7)}}};
            foreach(var item in new[]{(NoteCommandAction.AddTodo,"Add Todo"),(NoteCommandAction.AddNote,"Quick note"),
                (NoteCommandAction.AddReminder,"Add reminder"),(NoteCommandAction.ViewNotes,"View notes"),(NoteCommandAction.ToggleTodoOverlay,"Todo overlay")})
                noteList.Children.Add(Row(noteRows[item.Item1],item.Item2));
            var noteCard=Card(noteList);
            var help=Card(new StackPanel{Spacing=12,Children={Text("One key after the leader",21),
                Text("Win + F3  →  N",23),Text("Press the note leader, release it, then press N to add a quick note.",13,Muted),
                new Border{Height=1,Background=Brush.Parse("#359A7B48")},Text("Keys stay within their group",16),
                Text("N can create a timer after Win + F2 and a note after Win + F3. Within one group, each key must be unique.",13,Muted),
                Text("Escape always leaves command mode.",13),Text("Change either leader on Main shortcuts.",12,Muted)}});
            var noteGrid=new Grid{Children={noteCard,help}};
            var timerTab=Action("Timer commands · 21");var noteTab=Action("Notes & Todos · 5");
            var sequence=Text("",13,Muted);
            var tabs=new WrapPanel{Children={timerTab,noteTab}};timerTab.Margin=new(0,0,10,0);
            var banner=new Border{Padding=new(12,8),CornerRadius=new(4),Background=Brush.Parse("#25FFFFFF"),Child=sequence};
            ToolTip.SetTip(banner,"Select a key to record a new command. Clear removes that mapping. Change leaders on Main shortcuts.");
            var warnings=new[]{Field<Border>(w,"WinF2ValidationWarningBorder"),Field<Border>(w,"WinF3ValidationWarningBorder")};
            foreach(var warning in warnings)
            {
                ((Panel)warning.Parent!).Children.Remove(warning);warning.Margin=default;warning.Padding=new(12,9);
                warning.Background=Brush.Parse("#F6DFC3");warning.BorderBrush=Brush.Parse("#A24B32");warning.BorderThickness=new(1);warning.CornerRadius=new(4);
                foreach(var text in warning.GetVisualDescendants().OfType<TextBlock>())text.Foreground=Brush.Parse("#813620");
            }
            var content=new StackPanel{Spacing=14,Children={tabs,banner,warnings[0],warnings[1],timerGrid,noteGrid}};host.Children.Add(content);
            bool notes=false;
            void Select(bool value)
            {
                notes=value;timerGrid.IsVisible=!notes;noteGrid.IsVisible=notes;
                timerTab.Classes.Set("editorPrimary",!notes);noteTab.Classes.Set("editorPrimary",notes);
                var leader=notes?w.ResultNoteLeader:w.ResultLeader;
                sequence.Text=leader.Format().Replace("+"," + ")+"  →  command key     ·     Esc exits     ·     Select a key to edit";
                if(w.IsVisible)Find<ScrollViewer>(w,"MainShortcutsScroll").ScrollToHome();
            }
            timerTab.Click+=(_,_)=>Select(false);noteTab.Click+=(_,_)=>Select(true);Select(false);
            w.Show();Pump();
            var scroll=Find<ScrollViewer>(w,"MainShortcutsScroll");var save=Find<Button>(w,"ShortcutsSave");
            void Resize()
            {
                int columns=scroll.Bounds.Width>=1350?3:scroll.Bounds.Width>=1020?2:1;
                timerGrid.ColumnDefinitions=new(columns==3?"*,18,*,18,*":columns==2?"*,18,*":"*");
                timerGrid.RowDefinitions=new(columns==3?"Auto":columns==2?"Auto,18,Auto":"Auto,18,Auto,18,Auto");
                for(int i=0;i<3;i++){Grid.SetColumn(timerGrid.Children[i],(i%columns)*2);Grid.SetRow(timerGrid.Children[i],(i/columns)*2);}
                Grid.SetColumnSpan(overlays,columns==2?3:1);
                bool stack=scroll.Bounds.Width<1020;noteGrid.ColumnDefinitions=new(stack?"*":"1.3*,18,1*");noteGrid.RowDefinitions=new(stack?"Auto,18,Auto":"Auto");
                Grid.SetColumn(help,stack?0:2);Grid.SetRow(help,stack?2:0);
            }
            scroll.SizeChanged+=(_,_)=>Resize();Resize();Pump();save.Focus();Pump();
            var footer=save.TranslatePoint(default,w);
            void Check()
            {
                Require(scroll.Viewport.Height>100&&scroll.Extent.Width<=scroll.Viewport.Width+1,"Invalid viewport or horizontal overflow.");
                Require(save.TranslatePoint(default,w)==footer,"Footer moved while scrolling.");
                foreach(var field in fields.Where(f=>f.IsEffectivelyVisible))Require(field.Bounds.Width>=90,"Key field clipped.");
            }
            void Capture(string name)
            {
                Check();using var bitmap=new RenderTargetBitmap(new PixelSize((int)w.ClientSize.Width,(int)w.ClientSize.Height),new Vector(96,96));
                var frame=w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single();
                if(percent>=100)bitmap.Render(w);
                else {using var context=bitmap.CreateDrawingContext();context.DrawRectangle(new VisualBrush(frame){Stretch=Stretch.Fill,
                    SourceRect=new RelativeRect(frame.Bounds,RelativeUnit.Absolute)},null,new Rect(frame.Bounds.Size));}
                bitmap.Save(Path.Combine(Output,name+".png"));
            }
            Capture("timers-"+percent);scroll.ScrollToEnd();Pump();Capture("overlays-"+percent);
            Click(noteTab);Pump();Capture("notes-"+percent);
            if(percent==90)
            {
                var first=fields[21];Require(first.Focus(),"Mapping field cannot receive focus.");Pump();Capture("recording");
                first.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Avalonia.Input.Key.N});Pump();
                Require(w.ResultNoteCommandModeKeys[NoteCommandAction.AddTodo]==0x54&&warnings[1].IsVisible,"Duplicate replaced the original mapping.");
                Capture("duplicate");
                Click((Button)((Grid)first.Parent!).Children[2]);save.Focus();Pump();
                Require(w.ResultNoteCommandModeKeys[NoteCommandAction.AddTodo]==0,"Sample Clear failed.");Capture("unassigned");
            }
            Require(w.DialogResult!=true,"Preview must not save settings.");
            Console.WriteLine($"PASS {percent}% native proposal: 21 timer + 5 note commands, responsive groups, scroll, fixed footer, key widths; no hotkey registration.");
        }
        finally{w.Close();Desktop("Shutdown");}
    }
}
