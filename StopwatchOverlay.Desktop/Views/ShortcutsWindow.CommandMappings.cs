using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class ShortcutsWindow
{
    private StackPanel? _commandMappingPage, _timerCommandPage, _noteCommandPage;
    private Button? _timerCommandsTab, _noteCommandsTab;
    private TextBlock? _commandMappingSequence, _noteCommandExample, _noteCommandDescription;

    private Control CreateCommandMappingsLayout()
    {
        TextBlock Text(string value,double size=13,IBrush? color=null)=>new(){Text=value,FontSize=size,
            Foreground=color??ClockPickerPresentation.Navy,FontFamily=ClockPickerPresentation.Body,TextWrapping=TextWrapping.Wrap};
        Border Card(Control child)=>new(){Padding=new(18),CornerRadius=new(7),BorderThickness=new(1),
            BorderBrush=Brush.Parse("#509A7B48"),Background=Brush.Parse("#40FFF6E4"),Child=child,VerticalAlignment=VerticalAlignment.Top};
        Button Tab(string label,string name)
        {
            var button=new Button{Content=label,Name=name,Padding=new(14,9)};
            NavigatorFormStyles.StyleButton(button);button.MinHeight=38;return button;
        }
        var timerRows=WinF2CommandsPanel.Children.Cast<Grid>().ToDictionary(row=>(ShortcutAction)row.Children.OfType<TextBox>().Single().Tag!);
        var noteRows=WinF3CommandsPanel.Children.Cast<Grid>().ToDictionary(row=>(NoteCommandAction)row.Children.OfType<TextBox>().Single().Tag!);
        Control Row(Grid old,string title,string clearName)
        {
            var description=old.Children.OfType<TextBlock>().Single().Text;
            var field=old.Children.OfType<TextBox>().Single();var clear=old.Children.OfType<Button>().Single();old.Children.Clear();
            field.Classes.Add("editorField");field.Width=94;field.MinHeight=38;field.Margin=default;field.Padding=new(8);
            field.FontSize=13;field.FontFamily=ClockPickerPresentation.Body;field.VerticalAlignment=VerticalAlignment.Center;
            field.VerticalContentAlignment=VerticalAlignment.Center;
            AutomationProperties.SetName(field,title+" command key");
            ToolTip.SetTip(field,"Press one command key. Tab moves to the next control; Escape is reserved for leaving command mode.");
            clear.Name=clearName;NavigatorFormStyles.StyleButton(clear);clear.Padding=new(10,8);clear.MinHeight=38;
            clear.VerticalAlignment=VerticalAlignment.Center;AutomationProperties.SetName(clear,"Clear "+title);
            var label=Text(title,14);label.VerticalAlignment=VerticalAlignment.Center;ToolTip.SetTip(label,description);
            var row=new Grid{ColumnDefinitions=new("*,10,94,8,Auto"),MinHeight=40,Children={label,field,clear}};
            Grid.SetColumn(field,2);Grid.SetColumn(clear,4);
            return new Border{BorderBrush=Brush.Parse("#259A7B48"),BorderThickness=new(0,0,0,1),Padding=new(0,2),Child=row};
        }
        Border Group(string title,string hint,params (ShortcutAction Action,string Title)[] items)
        {
            var heading=Text(title,19);heading.Margin=new(0,0,0,7);ToolTip.SetTip(heading,hint);
            var list=new StackPanel{Spacing=2,Children={heading}};
            foreach(var item in items)list.Children.Add(Row(timerRows[item.Action],item.Title,"ClearTimerCommand"+item.Action));
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
        var noteHeading=Text("Notes & Todos",19);noteHeading.Margin=new(0,0,0,7);
        var noteList=new StackPanel{Spacing=2,Children={noteHeading}};
        foreach(var item in new[]{(NoteCommandAction.AddTodo,"Add Todo"),(NoteCommandAction.AddNote,"Quick note"),
            (NoteCommandAction.AddReminder,"Add reminder"),(NoteCommandAction.ViewNotes,"View notes"),(NoteCommandAction.ToggleTodoOverlay,"Todo overlay")})
            noteList.Children.Add(Row(noteRows[item.Item1],item.Item2,"ClearNoteCommand"+item.Item1));
        _noteCommandExample=Text("",23);_noteCommandExample.Name="NoteCommandExample";
        _noteCommandDescription=Text("",13,ClockPickerPresentation.Muted);
        var help=Card(new StackPanel{Spacing=12,Children={Text("One key after the leader",21),_noteCommandExample,_noteCommandDescription,
            new Border{Height=1,Background=Brush.Parse("#359A7B48")},Text("Keys stay within their group",16),
            Text("A key may be used once in each group. Its action depends on which leader you press first.",13,ClockPickerPresentation.Muted),
            Text("Escape always leaves command mode.",13),Text("Change either leader on Main shortcuts.",12,ClockPickerPresentation.Muted)}});
        var noteGrid=new Grid{Children={Card(noteList),help}};
        foreach(var warning in new[]{WinF2ValidationWarningBorder,WinF3ValidationWarningBorder})
        {
            ((Panel)warning.Parent!).Children.Remove(warning);warning.Margin=default;warning.Padding=new(12,9);
            warning.Background=Brush.Parse("#F6DFC3");warning.BorderBrush=Brush.Parse("#A24B32");warning.BorderThickness=new(1);warning.CornerRadius=new(4);
            ((TextBlock)warning.Child!).Foreground=Brush.Parse("#813620");
        }
        _timerCommandsTab=Tab("Timer commands · 21","TimerCommandsTab");_timerCommandsTab.Margin=new(0,0,10,0);
        _noteCommandsTab=Tab("Notes & Todos · 5","NoteCommandsTab");
        _timerCommandsTab.Click+=(_,_)=>SelectCommandFamily(false);_noteCommandsTab.Click+=(_,_)=>SelectCommandFamily(true);
        _commandMappingSequence=Text("",13,ClockPickerPresentation.Muted);_commandMappingSequence.Name="CommandMappingSequence";
        var banner=new Border{Padding=new(12,8),CornerRadius=new(4),Background=Brush.Parse("#25FFFFFF"),Child=_commandMappingSequence};
        ToolTip.SetTip(banner,"Select a key to record a new command. Clear removes that mapping. Change leaders on Main shortcuts.");
        _timerCommandPage=new StackPanel{Name="TimerCommandPage",Spacing=14,Children={WinF2ValidationWarningBorder,timerGrid}};
        _noteCommandPage=new StackPanel{Name="NoteCommandPage",Spacing=14,Children={WinF3ValidationWarningBorder,noteGrid}};
        _commandMappingPage=new StackPanel{Name="CommandMappingsPage",Spacing=14,Children={
            new WrapPanel{Children={_timerCommandsTab,_noteCommandsTab}},banner,_timerCommandPage,_noteCommandPage}};
        _commandMappingPage.SizeChanged+=(_,_)=>
        {
            double width=_commandMappingPage.Bounds.Width;
            int columns=width>=1350?3:width>=1020?2:1;
            if(timerGrid.ColumnDefinitions.Count==columns*2-1)return;
            timerGrid.ColumnDefinitions=new(columns==3?"*,18,*,18,*":columns==2?"*,18,*":"*");
            timerGrid.RowDefinitions=new(columns==3?"Auto":columns==2?"Auto,18,Auto":"Auto,18,Auto,18,Auto");
            for(int i=0;i<3;i++){Grid.SetColumn(timerGrid.Children[i],(i%columns)*2);Grid.SetRow(timerGrid.Children[i],(i/columns)*2);}
            Grid.SetColumnSpan(overlays,columns==2?3:1);
            bool stack=width<1020;noteGrid.ColumnDefinitions=new(stack?"*":"1.3*,18,1*");noteGrid.RowDefinitions=new(stack?"Auto,18,Auto":"Auto");
            Grid.SetColumn(help,stack?0:2);Grid.SetRow(help,stack?2:0);
        };
        SelectCommandFamily(false);return _commandMappingPage;
    }

    private void SelectCommandFamily(bool notes)
    {
        CommandsTabControl.SelectedIndex=notes?1:0;
        if(_commandMappingPage==null)return;
        _timerCommandPage!.IsVisible=!notes;_noteCommandPage!.IsVisible=notes;
        _timerCommandsTab!.Classes.Set("editorPrimary",!notes);_noteCommandsTab!.Classes.Set("editorPrimary",notes);
        UpdateCommandMappingText();_shortcutScroll?.ScrollToHome();
    }

    private void UpdateCommandMappingText()
    {
        if(_commandMappingSequence==null)return;
        bool notes=_noteCommandPage!.IsVisible;var leader=notes?_pendingNoteLeader:_pendingLeader;
        _commandMappingSequence.Text=leader.VirtualKey==0
            ? $"Assign a {(notes?"note":"timer")} leader on Main shortcuts to use these commands."
            : leader.Format().Replace("+"," + ")+"  →  command key     ·     Esc exits     ·     Select a key to edit";
        _pendingNoteCommandModeKeys.TryGetValue(NoteCommandAction.AddNote,out var noteKey);
        if(_pendingNoteLeader.VirtualKey==0||noteKey==0)
        {
            _noteCommandExample!.Text=_pendingNoteLeader.VirtualKey==0?"Note leader not assigned":"Quick note not assigned";
            _noteCommandDescription!.Text=_pendingNoteLeader.VirtualKey==0
                ? "Assign a note leader on Main shortcuts to open note commands."
                : "Assign a key to Quick note to use this command.";
        }
        else
        {
            string combo=_pendingNoteLeader.Format().Replace("+"," + "),key=Shortcut.FormatKeyName(noteKey);
            _noteCommandExample!.Text=$"{combo}  →  {key}";
            _noteCommandDescription!.Text=$"Press {combo}, release it, then press {key} to add a quick note.";
        }
    }
}
