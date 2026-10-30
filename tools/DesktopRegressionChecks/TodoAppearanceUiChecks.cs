using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class TodoAppearanceUiChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static void Click(Button button)=>button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    static T Named<T>(Control parent,string name) where T:Control => parent.GetLogicalDescendants().OfType<T>().First(c=>c.Name==name);
    internal static void Run()
    {
        typeof(App).Assembly.GetType("StopwatchOverlay.Desktop.ThemeResources")!.GetMethod("ApplyApplication",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{AppThemeCatalog.Nika});
        string directory=Path.GetFullPath(Path.Combine(".superpowers","sdd","2026-10-29-todo-appearance","fixtures",Guid.NewGuid().ToString("N")));Directory.CreateDirectory(directory);
        string settingsPath=SettingsStore.SettingsPath;
        byte[]? before=File.Exists(settingsPath)?File.ReadAllBytes(settingsPath):null;
        try
        {
            var timestamp=new DateTime(2026,10,29,12,0,0);
            string body=TodoDetails.Append(TodoDetails.Append("Finish illustration",TodoDetailKind.Subtask,"Sketch layout"),TodoDetailKind.Subtask,"Add colors");
            body=TodoDetails.Append(body,TodoDetailKind.Subtask,"Export PNG");
            body=TodoDetails.Append(body,TodoDetailKind.Note,"Warm palette; keep map subtle.");
            body=TodoDetails.SetCompleted(body,TodoDetails.Parse(body).Items.First(),true);
            foreach(var (text,index) in new[]{body,"Read 20 pages","Review project notes","Plan tomorrow"}.Select((t,i)=>(t,i)))
            {
                var result=ObsidianNotesSync.AppendEntry(directory,NoteType.Todo,text,timestamp:timestamp.AddMinutes(index));
                Require(result.Success,"Cannot create isolated sample task: "+result.Message);
            }
            var entries=ObsidianNotesSync.LoadAllNotes(directory).Where(e=>e.Type==NoteType.Todo).OrderBy(e=>e.Timestamp).ToArray();
            for(int i=0;i<4;i++)TodoOrganizationStore.SetPriority(directory,entries[i],new[]{1,3,6,10}[i]);
            ObsidianNotesSync.SetTodoCompleted(directory,entries[3],true);
            var settings=new AppSettings {ThemeMode=AppThemeCatalog.Nika,ObsidianVaultFolder=directory,TelegramEnabled=false,HabiticaEnabled=false};
            CheckReviewFindings(settings);
            TestNativeOverlay(settings,entries[0]);
            TestInspector(settings);
            TestDialogs(settings);
            TestEmptyStates(directory);
            TestAlpha();
            Require((before==null&&!File.Exists(settingsPath))||(before!=null&&File.ReadAllBytes(settingsPath).SequenceEqual(before)),"Appearance checks wrote real user settings.");
        }
        finally {Directory.Delete(directory,true);}
    }

    static void TestNativeOverlay(AppSettings settings,NoteEntry first)
    {
        var window=new TodoOverlayWindow(settings){ShowActivated=false,ShowInTaskbar=false,Topmost=false};
        try
        {
            window.Show();ClockOverlayChecks.Pump(100);window.ToggleExpanded();ClockOverlayChecks.Pump(180);
            Require(window.FindControl<Border>("TodoPanel")!.IsVisible,"Expanded Nika panel stayed hidden after native resize.");
            Require(window.FindControl<StackPanel>("TodoRows")!.Children.Count==4,"Appearance lost tasks.");
            var expanded=(HashSet<string>)typeof(TodoOverlayWindow).GetField("_expandedDetails",Private)!.GetValue(window)!;
            expanded.Add(TodoOrganizationStore.GetKey(first));window.ApplySettings();ClockOverlayChecks.Pump(80);
            Require(window.GetVisualDescendants().OfType<TodoFrame>().Any(f=>f.Name=="TodoDetailsFrame"),"Margin notes did not expand.");
            Capture(window,"classic-overlay");
            CheckVisualFidelity(window);
            var heading=window.FindControl<TextBlock>("TodoHeading")!;
            Require(new Typeface(heading.FontFamily).GlyphTypeface.FamilyName.Contains("Playfair"),"Todo heading fell back instead of using its embedded font.");
            foreach(var option in TodoAppearanceCatalog.Backgrounds)
            {
                settings.TodoAppearance.Classic.Background=option.Id;window.ApplySettings();ClockOverlayChecks.Pump(40);
                Require(window.FindControl<TodoArtwork>("TodoMap")!.Choice==option.Id,"Background did not apply.");
                Require(window.GetVisualDescendants().OfType<HoldToCompleteCheckBox>().Count()>=6,"Background removed task controls.");
            }
            settings.TodoAppearance.Classic.Background="navigator-parchment";
            foreach(var option in TodoAppearanceCatalog.Rows){settings.TodoAppearance.Classic.Rows=option.Id;window.ApplySettings();ClockOverlayChecks.Pump(30);Require(window.FindControl<StackPanel>("TodoRows")!.Children.Count==4,"Row style lost tasks.");}
            settings.TodoAppearance.Classic.Rows="ships-ledger";
            settings.TodoAppearance.Profile="pixel";window.ApplySettings();ClockOverlayChecks.Pump(70);
            Require(window.GetVisualDescendants().OfType<TodoFrame>().Any(f=>f.Pixel),"Pixel has no native stepped frames.");Capture(window,"pixel-overlay");
            var lastRow=window.FindControl<StackPanel>("TodoRows")!.Children.Last();
            var viewport=window.FindControl<ScrollViewer>("TodoScrollViewer")!;
            Require(lastRow.TranslatePoint(new Point(0,lastRow.Bounds.Height),viewport)!.Value.Y<=viewport.Bounds.Height+1,"Approved four-task Pixel composition clips its last row.");
            settings.TodoAppearance.Pixel.HeaderEmblem="zoro";settings.TodoAppearance.Pixel.LauncherEmblem="law";window.ApplySettings();
            Require(window.FindControl<TodoArtwork>("TodoEmblem")!.Choice=="zoro","Expanded header used launcher emblem.");
            window.ToggleExpanded();ClockOverlayChecks.Pump(100);
            Require(window.FindControl<TodoArtwork>("TodoEmblem")!.Choice=="law","Collapsed launcher used header emblem.");
            settings.TodoAppearance.Pixel.LauncherMatchesHeader=true;window.ApplySettings();
            Require(window.FindControl<TodoArtwork>("TodoEmblem")!.Choice=="zoro","Linked launcher did not use the header emblem.");
            settings.TodoAppearance.Pixel.HeaderEmblem="nami";window.ApplySettings();
            Require(window.FindControl<TodoArtwork>("TodoEmblem")!.Choice=="nami","Linked launcher did not follow the changed header.");
            settings.TodoAppearance.Pixel.LauncherMatchesHeader=false;window.ApplySettings();
            Require(window.FindControl<TodoArtwork>("TodoEmblem")!.Choice=="law","Unlinking did not restore the separate launcher artwork.");
            Require(window.FindControl<Border>("TodoOutline")!.BorderThickness==new Thickness(0),"Collapsed launcher has a frame.");Capture(window,"transparent-launcher");
            window.ToggleExpanded();ClockOverlayChecks.Pump(100);
            TodoOrganizationStore.SetInProgress(settings.ObsidianVaultFolder,first,true);
            TodoOrganizationStore.SaveTodo(settings.ObsidianVaultFolder,first,first.Text,1,DateTime.Now.AddHours(-2));
            window.ApplySettings();ClockOverlayChecks.Pump(70);Capture(window,"pixel-feedback");
            var check=window.GetVisualDescendants().OfType<HoldToCompleteCheckBox>().First();
            check.HoldProgress=.6;ClockOverlayChecks.Pump(30);Capture(window,"pixel-hold");check.CancelHold();
            settings.TodoAppearance.Profile="classic";window.ApplySettings();ClockOverlayChecks.Pump(50);Capture(window,"classic-feedback");
            var classicChild=window.GetVisualDescendants().OfType<HoldToCompleteCheckBox>().First(c=>c.Name=="SubtaskCheckBox");
            Require(((StackPanel)classicChild.Parent!).Children.OfType<ProgressBar>().Any(),"Pixel subtask meter leaked into Classic.");
            CheckTaskInput(window);
            settings.ThemeMode=AppThemeCatalog.Pirate;window.ApplySettings();ClockOverlayChecks.Pump(50);
            Require(!window.Classes.Contains("nikaTodo")&&!window.FindControl<TodoArtwork>("TodoMap")!.IsVisible,"Nika art leaked into One Piece.");
            Require(window.FindControl<Button>("AddTodoButton")!.GetValue(Button.TemplateProperty)!=null,"Original Add button template did not restore.");
            settings.ThemeMode=AppThemeCatalog.Nika;
        }
        finally {window.Close();}
    }

    static void CheckReviewFindings(AppSettings settings)
    {
        var failures=new List<string>();
        void Verify(string name,Action action){try{action();}catch(Exception ex){failures.Add(name+": "+ex.GetBaseException().Message);}}
        Verify("original Luffy artwork",()=>
        {
            byte[] Render(Control art,int size)
            {
                art.Measure(new Size(size,size));art.Arrange(new Rect(0,0,size,size));
                using var target=new RenderTargetBitmap(new PixelSize(size,size),new Vector(96,96));target.Render(art);
                var pixels=new byte[size*size*4];var buffer=Marshal.AllocHGlobal(pixels.Length);
                try{target.CopyPixels(new PixelRect(0,0,size,size),buffer,pixels.Length,size*4);Marshal.Copy(buffer,pixels,0,pixels.Length);}finally{Marshal.FreeHGlobal(buffer);}
                return pixels;
            }
            foreach(int size in new[]{38,64,72,160})
                Require(Render(new TodoArtwork{Choice="luffy"},size).SequenceEqual(Render(new PirateArtwork{Part="Skull"},size)),"Luffy does not match the existing original skull artwork at "+size+" pixels.");
        });
        Verify("pixel subtask hold feedback",()=>
        {
            settings.TodoAppearance.Profile="pixel";
            var window=new TodoOverlayWindow(settings){ShowActivated=false,ShowInTaskbar=false,Topmost=false};
            try
            {
                window.Show();window.ToggleExpanded();
                var first=ObsidianNotesSync.LoadAllNotes(settings.ObsidianVaultFolder).Where(e=>e.Type==NoteType.Todo).OrderBy(e=>e.Timestamp).First();
                ((HashSet<string>)typeof(TodoOverlayWindow).GetField("_expandedDetails",Private)!.GetValue(window)!).Add(TodoOrganizationStore.GetKey(first));
                window.ApplySettings();ClockOverlayChecks.Pump(80);
                var checkbox=window.GetVisualDescendants().OfType<HoldToCompleteCheckBox>().First(c=>c.Name=="SubtaskCheckBox"&&c.IsChecked!=true);
                var meter=((StackPanel)checkbox.Parent!).Children.OfType<TodoHoldMeter>().SingleOrDefault();
                Require(meter is {Pixel:true},"Pixel subtasks still use a continuous loading bar.");
                checkbox.HoldProgress=.6;ClockOverlayChecks.Pump(25);Require(meter!.Value==.6,"Subtask hold progress is not bound to its pixel meter.");
                Capture(window,"pixel-subtask-hold");checkbox.CancelHold();Require(meter.Value==0&&!meter.IsVisible,"Cancel left stale subtask hold progress.");
            }
            finally{window.Close();settings.TodoAppearance.Profile="classic";}
        });
        Verify("compact inspector",()=>
        {
            var window=new SettingsWindow(settings){Width=840,Height=600,ShowActivated=false,ShowInTaskbar=false};
            try
            {
                window.Show();var nav=window.FindControl<ListBox>("NavigationList")!;nav.SelectedItem=nav.Items.OfType<ListBoxItem>().Single(i=>Equals(i.Tag,"TodoAppearance"));ClockOverlayChecks.Pump(80);
                window.Width=1060;ClockOverlayChecks.Pump(40);window.Width=840;ClockOverlayChecks.Pump(100);
                Require(Named<CheckBox>(window,"TodoLauncherMatchesHeader")!=null,"Same-as-header option is missing.");
                Require(window.ClientSize.Width<=841,"Compact viewport was not exercised.");
                foreach(string name in new[]{"TodoProfile_pixel","ChooseHeaderEmblem","ChooseLauncherEmblem","TodoRowsChoice"})
                {var c=Named<Control>(window,name);var pos=c.TranslatePoint(default,window)!.Value;Require(c.IsVisible&&c.Bounds.Width>0&&pos.X>=0&&pos.X+c.Bounds.Width<=window.ClientSize.Width,"Control outside viewport: "+name);}
            }
            finally{window.Close();}
        });
        Verify("verified emblem regions",()=>Require(typeof(TodoArtwork).GetMethod("EmblemSource")!=null,"No verified per-emblem source regions."));
        Verify("pixel editor fields",()=>
        {
            settings.TodoAppearance.Profile="pixel";
            var editor=new NoteEntryWindow(NoteType.Todo,settings){ShowActivated=false,Topmost=false,ShowInTaskbar=false};
            try
            {
                Require(editor.FindControl<TextBox>("NoteInputBox")!.Parent is TodoFrame{Pixel:true},"Main text field lacks stepped frame.");
                Require(editor.FindControl<ComboBox>("TodoPriorityCombo")!.Parent is TodoFrame{Pixel:true},"Priority input lacks stepped frame.");
                var checklist=(HabiticaSubtaskEditor)typeof(NoteEntryWindow).GetField("_subtaskEditor",Private)!.GetValue(editor)!;
                checklist.SetSubtasks(new[]{new StopwatchOverlay.Habitica.HabiticaSubtask(Guid.NewGuid(),"Sample",false)});
                var input=checklist.GetLogicalDescendants().OfType<TextBox>().Single();Require(input.Parent is TodoFrame{Pixel:true},"Checklist field lacks stepped frame.");
                input.Text="Edited";Require(checklist.GetSubtasks().Single().Text=="Edited","Stepped frame lost native input value.");
            }
            finally{editor.Close();settings.TodoAppearance.Profile="classic";}
        });
        if(failures.Count>0)throw new Exception(string.Join("; ",failures));
    }

    static void TestInspector(AppSettings settings)
    {
        var inspector=new SettingsWindow(settings){ShowActivated=false,ShowInTaskbar=false};
        try
        {
            inspector.Show();ClockOverlayChecks.Pump(80);
            var nav=inspector.FindControl<ListBox>("NavigationList")!;nav.SelectedItem=nav.Items.OfType<ListBoxItem>().Single(i=>Equals(i.Tag,"TodoAppearance"));ClockOverlayChecks.Pump(100);
            Capture(inspector,"settings-classic");
            var firstPreview=(RenderTargetBitmap)inspector.FindControl<ContentControl>("TodoPreviewHost")!.GetVisualDescendants().OfType<Image>().Single().Source!;
            var footerPixels=new byte[220*32*4];var footerBuffer=Marshal.AllocHGlobal(footerPixels.Length);
            try{firstPreview.CopyPixels(new PixelRect(80,493,220,32),footerBuffer,footerPixels.Length,220*4);Marshal.Copy(footerBuffer,footerPixels,0,footerPixels.Length);}finally{Marshal.FreeHGlobal(footerBuffer);}
            Require(Enumerable.Range(0,220*32).Count(i=>footerPixels[i*4]>180&&footerPixels[i*4+1]>180&&footerPixels[i*4+2]>180&&footerPixels[i*4+3]>200)>30,"Initial preview snapshot omitted native button text.");
            void Pick(string target,string id)
            {
                Click(Named<Button>(inspector,"Choose"+target+"Emblem"));ClockOverlayChecks.Pump(30);
                var picker=(TodoEmblemPickerWindow)typeof(SettingsWindow).GetField("_emblemPicker",Private)!.GetValue(inspector)!;
                picker.Select(id);Click(Named<Button>(picker,"UseEmblem"));ClockOverlayChecks.Pump(30);
            }
            int changes=0;
            Action<SettingsChangeKind> changed=kind=>{if(kind==SettingsChangeKind.TodoAppearance)changes++;};
            typeof(SettingsWindow).GetEvent("SettingsChanged",Private)!.GetAddMethod(true)!.Invoke(inspector,new object[]{changed});
            Pick("Launcher","law");
            Named<CheckBox>(inspector,"TodoLauncherMatchesHeader").IsChecked=true;
            Require(settings.TodoAppearance.Classic.LauncherMatchesHeader&&!Named<Button>(inspector,"ChooseLauncherEmblem").IsEnabled,"Linked launcher remained independently editable.");
            Pick("Header","zoro");
            Require(settings.TodoAppearance.Classic.EffectiveLauncherEmblem=="zoro"&&settings.TodoAppearance.Classic.LauncherEmblem=="law","Changing linked header erased or ignored the separate launcher choice.");
            Capture(inspector,"settings-linked-emblems");
            Click(Named<Button>(inspector,"TodoProfile_pixel"));
            Require(Named<CheckBox>(inspector,"TodoLauncherMatchesHeader").IsChecked==false&&Named<Button>(inspector,"ChooseLauncherEmblem").IsEnabled,"Link control leaked between profiles.");
            Click(Named<Button>(inspector,"TodoProfile_classic"));
            Require(Named<CheckBox>(inspector,"TodoLauncherMatchesHeader").IsChecked==true,"Switching profiles lost the link preference.");
            Named<CheckBox>(inspector,"TodoLauncherMatchesHeader").IsChecked=false;
            Require(settings.TodoAppearance.Classic.EffectiveLauncherEmblem=="law"&&Named<Button>(inspector,"ChooseLauncherEmblem").IsEnabled,"Independent launcher selection did not restore.");
            Require(changes>=6,"Emblem preference changes did not notify settings persistence.");
            Pick("Header","luffy");Pick("Launcher","luffy");
            Click(Named<Button>(inspector,"TodoBackground_midnight-chart"));
            var choice=Named<ComboBox>(inspector,"TodoRowsChoice");choice.SelectedItem=TodoAppearanceCatalog.Rows[1];
            Click(Named<Button>(inspector,"TodoProfile_pixel"));
            Require(settings.TodoAppearance.Current.Rows=="pixel-voyage","Pixel inherited Classic rows.");
            Click(Named<Button>(inspector,"TodoBackground_quiet-atlas"));
            Click(Named<Button>(inspector,"TodoProfile_classic"));
            Require(settings.TodoAppearance.Current.Rows=="parchment-cards"&&settings.TodoAppearance.Current.Background=="midnight-chart","Switching profile lost Classic selections.");
            Require(settings.TodoAppearance.Pixel.Background=="quiet-atlas","Switching profile lost Pixel selections.");
            foreach(var (name,count) in new[]{("TodoRowsChoice",4),("TodoPrioritiesChoice",3),("TodoDetailsChoice",2),("TodoToolbarChoice",2)})Require(Named<ComboBox>(inspector,name).ItemCount==count,"Retained selector options changed: "+name);
            inspector.Width=840;inspector.Height=600;ClockOverlayChecks.Pump(100);Capture(inspector,"settings-compact");
            var previewHost=inspector.FindControl<ContentControl>("TodoPreviewHost")!;
            var previewImage=previewHost.GetVisualDescendants().OfType<Image>().Single();
            Require(previewImage.Bounds.Width<=previewHost.Bounds.Width+1,"Scaled sample exceeds the preview viewport: "+previewImage.Bounds.Width+" > "+previewHost.Bounds.Width);
            var picker=new TodoEmblemPickerWindow("luffy",true,settings.TodoAppearance.Current){ShowActivated=false,ShowInTaskbar=false};
            try{picker.Select("brook");Require(picker.SelectedEmblem=="brook"&&picker.Result==null,"Staged selection applied immediately.");picker.Show();ClockOverlayChecks.Pump(80);Capture(picker,"emblem-picker");Click(Named<Button>(picker,"CancelEmblem"));Require(picker.Result==null,"Cancel applied the emblem.");}finally{picker.Close();}
            var use=new TodoEmblemPickerWindow("luffy",false,settings.TodoAppearance.Current);
            try{use.Select("nami");Click(Named<Button>(use,"UseEmblem"));Require(use.Result=="nami","Use emblem lost staged choice.");}finally{use.Close();}
            settings.TodoAppearance.Classic=new();settings.TodoAppearance.Pixel=TodoAppearanceProfile.PixelDefaults();settings.TodoAppearance.Profile="classic";
        }
        finally{inspector.Close();}
    }

    static void TestDialogs(AppSettings settings)
    {
        foreach(string profile in new[]{"classic","pixel"})
        {
            settings.TodoAppearance.Profile=profile;
            var editor=new NoteEntryWindow(NoteType.Todo,settings){ShowActivated=false,ShowInTaskbar=false,Topmost=false};
            try
            {
                editor.FindControl<TextBox>("NoteInputBox")!.Text="Finish illustration\nWarm palette; keep map subtle.";
                var checklist=(HabiticaSubtaskEditor)typeof(NoteEntryWindow).GetField("_subtaskEditor",Private)!.GetValue(editor)!;
                checklist.SetSubtasks(new[]{new StopwatchOverlay.Habitica.HabiticaSubtask(Guid.NewGuid(),"Sketch layout",false),new StopwatchOverlay.Habitica.HabiticaSubtask(Guid.NewGuid(),"Add colors",false)});
                editor.Show();ClockOverlayChecks.Pump(90);Capture(editor,profile+"-editor");
                if(profile=="pixel")Require(editor.FindControl<Button>("TodoPlacementButton")!.GetVisualDescendants().OfType<TodoFrame>().Any(f=>f.Pixel),"Pixel placement action inside ScrollViewer missed its appearance template.");
                typeof(NoteEntryWindow).GetMethod("ShowSavedSyncWarning",Private)!.Invoke(editor,new object[]{"Saved locally. The connected service could not be reached."});
                Require(!editor.FindControl<Button>("CancelButton")!.IsVisible&&editor.FindControl<Button>("SaveButton")!.Content?.ToString()=="Close","Saved warning has duplicate/save actions.");
                Require(editor.FindControl<TextBlock>("EditorShortcutHint")!.Text=="Esc to Close","Saved warning has stale shortcut hint.");
                ClockOverlayChecks.Pump(30);Capture(editor,profile+"-saved-warning");
            }
            finally{editor.Close();}
            var detail=NoteEntryWindow.ForTodoDetail(settings,"Add note","Warm palette; keep map subtle.",_=>new(true,""));detail.ShowActivated=false;detail.Topmost=false;
            try{detail.Show();ClockOverlayChecks.Pump(60);Capture(detail,profile+"-detail");}finally{detail.Close();}
            var confirmation=new ConfirmationDialogWindow("Delete todo","Delete this todo?","Finish illustration\n\nThis removes the task from your Obsidian todo file and deletes its linked Telegram message.\n\nThis cannot be undone.","Delete",true){ShowActivated=false};
            try{confirmation.ApplyTodoAppearance(settings);confirmation.Show();ClockOverlayChecks.Pump(60);Capture(confirmation,profile+"-delete");}finally{confirmation.Close();}
        }
        foreach(var type in new[]{NoteType.Note,NoteType.Reminder}){var other=new NoteEntryWindow(type,settings);try{Require(!other.Classes.Contains("nikaTodoDialog"),"Todo styling leaked into "+type);}finally{other.Close();}}
    }

    static void TestAlpha()
    {
        using var bitmap=new Bitmap(AssetLoader.Open(new Uri("avares://StopwatchOverlay.Desktop/Assets/Todo/emblems.png")));
        var memory=Marshal.AllocHGlobal(4);
        try{bitmap.CopyPixels(new PixelRect(0,0,1,1),memory,4,4);Require(Marshal.ReadByte(memory,3)==0,"Crew atlas background is opaque.");}finally{Marshal.FreeHGlobal(memory);}
        foreach(var option in TodoAppearanceCatalog.Emblems)
        {
            const int size=160;
            var art=new TodoArtwork{Choice=option.Id,Width=size,Height=size};art.Measure(new Size(size,size));art.Arrange(new Rect(0,0,size,size));using var target=new RenderTargetBitmap(new PixelSize(size,size),new Vector(96,96));target.Render(art);
            string output=Path.GetFullPath("design/one-piece-v2/todo-overlay/implemented/emblems/"+option.Id+".png");Directory.CreateDirectory(Path.GetDirectoryName(output)!);target.Save(output);
            var pixels=new byte[size*size*4];var buffer=Marshal.AllocHGlobal(pixels.Length);
            try{target.CopyPixels(new PixelRect(0,0,size,size),buffer,pixels.Length,size*4);Marshal.Copy(buffer,pixels,0,pixels.Length);}finally{Marshal.FreeHGlobal(buffer);}
            var opaque=Enumerable.Range(0,size*size).Select(i=>pixels[i*4+3]>30).ToArray();
            int components=0;var queue=new Queue<int>();
            for(int start=0;start<opaque.Length;start++)
            {
                if(!opaque[start])continue;opaque[start]=false;queue.Enqueue(start);int count=0;
                while(queue.Count>0){int k=queue.Dequeue(),x=k%size,y=k/size;count++;foreach(int next in new[]{x>0?k-1:-1,x<size-1?k+1:-1,y>0?k-size:-1,y<size-1?k+size:-1})if(next>=0&&opaque[next]){opaque[next]=false;queue.Enqueue(next);}}
                if(count>12)components++;
            }
            Require(components==1,"Emblem has a neighboring fragment or empty silhouette: "+option.Id+" ("+components+" components).");
        }
    }

    static void TestEmptyStates(string directory)
    {
        string vault=Path.Combine(directory,"empty-state");Directory.CreateDirectory(vault);
        var settings=new AppSettings {ThemeMode=AppThemeCatalog.Nika,ObsidianVaultFolder=vault,TodoOverlayShowCompleted=false};
        foreach(string state in new[]{"empty","all-done"})
        {
            if(state=="all-done") {ObsidianNotesSync.AppendEntry(vault,NoteType.Todo,"Finished sample");var entry=ObsidianNotesSync.LoadAllNotes(vault).Single();ObsidianNotesSync.SetTodoCompleted(vault,entry,true);}
            foreach(string profile in new[]{"classic","pixel"})
            {
                settings.TodoAppearance.Profile=profile;
                var window=new TodoOverlayWindow(settings){ShowActivated=false,Topmost=false,ShowInTaskbar=false};
                try
                {
                    window.Show();ClockOverlayChecks.Pump(40);window.ToggleExpanded();ClockOverlayChecks.Pump(70);
                    var host=window.FindControl<ContentControl>("TodoEmptyStateHost");
                    Require(host?.IsVisible==true,"Approved illustrated empty state is missing.");
                    Require(host!.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text==(state=="empty"?"No todos yet.":"All done.")),"Empty state title does not reflect the task state.");
                    Capture(window,profile+"-"+state);
                }
                finally{window.Close();}
            }
        }
    }

    static void CheckTaskInput(TodoOverlayWindow window)
    {
        var pointer=new Avalonia.Input.Pointer(791,Avalonia.Input.PointerType.Mouse,true);
        var row=window.FindControl<StackPanel>("TodoRows")!.Children.OfType<Grid>().First().Children.OfType<HoldToCompleteCheckBox>().Single();
        void Press(int clicks)=>typeof(HoldToCompleteCheckBox).GetMethod("OnPointerPressed",Private)!.Invoke(row,new object[]{new Avalonia.Input.PointerPressedEventArgs(row,pointer,row,new Point(10,10),0,new Avalonia.Input.PointerPointProperties(Avalonia.Input.RawInputModifiers.LeftMouseButton,Avalonia.Input.PointerUpdateKind.LeftButtonPressed),Avalonia.Input.KeyModifiers.None,clicks)});
        void Release()=>typeof(HoldToCompleteCheckBox).GetMethod("OnPointerReleased",Private)!.Invoke(row,new object[]{new Avalonia.Input.PointerReleasedEventArgs(row,pointer,row,new Point(10,10),0,new Avalonia.Input.PointerPointProperties(Avalonia.Input.RawInputModifiers.None,Avalonia.Input.PointerUpdateKind.LeftButtonReleased),Avalonia.Input.KeyModifiers.None,Avalonia.Input.MouseButton.Left)});
        Press(1);Release();Require(row.IsChecked!=true,"Short click completed a task.");
        Press(1);ClockOverlayChecks.Pump(250);row.CancelHold();Release();Require(row.IsChecked!=true,"Cancelled hold completed a task.");
        Press(1);ClockOverlayChecks.Pump(1100);Release();Require(row.IsChecked==true,"Native styled checkbox lost hold completion.");
        ClockOverlayChecks.Pump(60);
    }

    static void CheckVisualFidelity(TodoOverlayWindow window)
    {
        var failures=new List<string>();
        var add=window.FindControl<Button>("AddTodoButton")!;
        int width=(int)Math.Ceiling(add.Bounds.Width),height=(int)Math.Ceiling(add.Bounds.Height);
        var position=add.TranslatePoint(default,window)!.Value;
        using var target=new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width,(int)window.ClientSize.Height),new Vector(96,96));target.Render(window);
        var pixels=new byte[width*height*4];var memory=Marshal.AllocHGlobal(pixels.Length);
        try{target.CopyPixels(new PixelRect((int)position.X,(int)position.Y,width,height),memory,pixels.Length,width*4);Marshal.Copy(memory,pixels,0,pixels.Length);}finally{Marshal.FreeHGlobal(memory);}
        var ink=Enumerable.Range(0,width*height).Where(i=>pixels[i*4]>180&&pixels[i*4+1]>210&&pixels[i*4+2]>220&&pixels[i*4+3]>220).ToArray();
        Require(ink.Length>20,"Add button lettering was not rendered.");
        double x=(ink.Min(i=>i%width)+ink.Max(i=>i%width)+1)/2.0,y=(ink.Min(i=>i/width)+ink.Max(i=>i/width)+1)/2.0;
        Console.WriteLine($"Todo Add lettering center: ({x:0.##},{y:0.##}), button center: ({width/2.0:0.##},{height/2.0:0.##})");
        if(Math.Abs(y-height/2.0)>1.25||Math.Abs(x-width/2.0)>1.25)failures.Add("Add button lettering is not optically centered.");
        var row=window.FindControl<StackPanel>("TodoRows")!.Children.OfType<Grid>().First();
        var badge=row.GetVisualDescendants().OfType<Button>().Single(b=>b.Classes.Contains("priorityBadge"));
        var parentCheck=row.Children.OfType<HoldToCompleteCheckBox>().Single();
        var label=((Grid)parentCheck.Content!).Children.OfType<TextBlock>().Single();
        double badgeY=badge.TranslatePoint(new Point(0,badge.Bounds.Height/2),row)!.Value.Y;
        double textY=label.TranslatePoint(new Point(0,label.Bounds.Height/2),row)!.Value.Y;
        Console.WriteLine($"Todo row centers: badge={badgeY:0.##}, text={textY:0.##}");
        if(Math.Abs(badgeY-textY)>1)failures.Add("Priority badge and task text do not share a center line.");
        Require(failures.Count==0,string.Join(" ",failures));
    }

    static void Capture(Window window,string name)
    {
        window.UpdateLayout();
        // SetupWithoutStarting has no app render loop. Repaint after native resize,
        // including scaled preview descendants, before exporting their draw lists.
        foreach(var visual in window.GetVisualDescendants())visual.InvalidateVisual();
        window.InvalidateVisual();ClockOverlayChecks.Pump(35);
        var renderRoot=(Avalonia.Rendering.IRenderRoot)window;
        typeof(Avalonia.Rendering.IRenderer).GetMethod("Paint",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!
            .Invoke(renderRoot.Renderer,new object[]{new Rect(renderRoot.ClientSize)});
        string output=Path.GetFullPath("design/one-piece-v2/todo-overlay/implemented/"+name+".png");Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width),(int)Math.Ceiling(window.ClientSize.Height)),new Vector(96,96));
        bitmap.Render(window);bitmap.Save(output);Console.WriteLine("Captured "+name+" "+window.ClientSize);
    }
}
