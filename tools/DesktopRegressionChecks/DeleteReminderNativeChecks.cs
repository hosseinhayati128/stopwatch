using System.Reflection;
using System.Text.Json;
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
using StopwatchOverlay.Habitica;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class DeleteReminderNativeChecks
{
    const string Output = "design/one-piece-v2/delete-reminder/implemented";
    const string Short = "Review the dashboard sketches.";
    const string Long = "Review the dashboard sketches and the full accessibility checklist before tomorrow’s planning session. Bring the notes about keyboard navigation, smaller screens, timer switching, and the remaining reminder improvements.";
    static readonly DateTime Due = new(2026,11,20,18,30,0);
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static T Named<T>(Window w,string name) where T:Control => w.FindControl<T>(name) ?? w.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    static void Click(Window w,string name){Named<Button>(w,name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));ClockOverlayChecks.Pump(25);}
    static void Key(Control c,Avalonia.Input.Key key){c.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=key});c.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyUpEvent,Key=key});ClockOverlayChecks.Pump(25);}
    static object? Call(object instance,string method,params object?[] args)=>instance.GetType().GetMethod(method,Private)!.Invoke(instance,args);
    static void Desktop(string type,string method,params object[] args)=>typeof(ConfirmationDialogWindow).Assembly.GetType("StopwatchOverlay.Desktop."+type)!.GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
    sealed class Fixture:IDisposable
    {
        internal readonly AppSettings Settings;
        internal Fixture(int scale=90,string theme=AppThemeCatalog.Nika)
        {
            Settings=new(){ThemeMode=theme,UiScalePercent=scale,TelegramEnabled=false,HabiticaEnabled=false};
            Desktop("ThemeResources","ApplyApplication",theme);Desktop("DesktopPageAppearance","Initialize",Settings);Desktop("DesktopTypography","Initialize",Settings.Typography);
        }
        internal ConfirmationDialogWindow Create(string text=Short,DateTime? due=null)
        {
            var w=new ConfirmationDialogWindow("Delete reminder","Delete this reminder?",text,"Delete",true){ShowActivated=false,ShowInTaskbar=false};
            var configure=typeof(ConfirmationDialogWindow).GetMethod("ConfigureDeleteReminder",Private);
            Require(configure!=null,"Delete Reminder has no approved presentation yet.");
            configure!.Invoke(w,[text,due,Settings]);return w;
        }
        internal ConfirmationDialogWindow Open(string text=Short,DateTime? due=null){var w=Create(text,due);w.Show();ClockOverlayChecks.Pump(70);return w;}
        public void Dispose(){Desktop("DesktopTypography","Shutdown");Desktop("DesktopPageAppearance","Shutdown");}
    }
    internal static void Layout()
    {
        Directory.CreateDirectory(Output);
        foreach(int scale in new[]{70,90,125})
        {
            using var f=new Fixture(scale);
            foreach(bool longText in new[]{false,true})foreach(bool scheduled in new[]{false,true})
            {
                string state=(longText?"long-":"")+(scheduled?"scheduled":"unscheduled"),text=longText?Long:Short;
                var w=f.Open(text,scheduled?Due:null);
                try
                {
                    Require(w.SystemDecorations==SystemDecorations.None&&w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(),"Shared borderless frame missing.");
                    Require(Math.Abs(w.ClientSize.Width-540*scale/100d)<1.1&&Math.Abs(w.ClientSize.Height-(longText?520:450)*scale/100d)<1.1,"Native size differs from approved preview.");
                    Require(Named<TextBlock>(w,"DeleteReminderText").Text==text,"Reminder text was changed or truncated.");
                    Require(Named<TextBlock>(w,"DeleteReminderSchedule").Text==(scheduled?"20 Nov 2026 · 18:30":"No date or time set"),"Incorrect schedule.");
                    Require(Named<Button>(w,"CancelButton").IsFocused,"Cancel lacks initial focus.");Bounds(w);
                    var body=Named<ScrollViewer>(w,"DeleteReminderBody");Require(body.Extent.Height<=body.Viewport.Height+1,"Approved sample needs scrolling.");
                    Capture(w,scale,state);
                    var safe=Named<Button>(w,"CancelButton");var presenter=safe.GetVisualDescendants().OfType<ContentPresenter>().Single(p=>p.Name=="PART_ContentPresenter");
                    foreach(string pseudo in new[]{":pointerover",":pressed"})
                    {
                        ((IPseudoClasses)safe.Classes).Set(pseudo,true);ClockOverlayChecks.Pump(15);
                        Require(presenter.Background is ISolidColorBrush b&&b.Color.R<70,"Cancel loses hover/pressed contrast.");((IPseudoClasses)safe.Classes).Set(pseudo,false);
                    }
                }
                finally{w.Close();}
            }
        }
    }
    static void Bounds(Window w)
    {
        var frame=w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single();
        var edge=frame.TranslatePoint(new Point(frame.Bounds.Width,frame.Bounds.Height),w)!.Value;
        Require(Math.Abs(edge.X-w.ClientSize.Width)<1.5&&Math.Abs(edge.Y-w.ClientSize.Height)<1.5,"Frame does not fit native window.");
        foreach(var t in frame.GetVisualDescendants().OfType<TextBlock>())Require(t.TextLayout.Height<=t.Bounds.Height+1,"Clipped text: "+t.Text);
        var body=Named<ScrollViewer>(w,"DeleteReminderBody");Require(body.Viewport.Height>55,$"Body viewport unusable: {body.Viewport}; window {w.ClientSize}; frame {frame.Bounds}; heading {Named<TextBlock>(w,"HeadingText").Bounds}; actions {Named<Button>(w,"CancelButton").Bounds} / {Named<Button>(w,"ConfirmButton").Bounds}.");
        Require(body.TranslatePoint(new Point(0,body.Bounds.Height),frame)!.Value.Y<Named<Button>(w,"CancelButton").TranslatePoint(default,frame)!.Value.Y,"Body overlaps actions.");
        foreach(string name in new[]{"DeleteReminderCancel","CancelButton","ConfirmButton"})
        {
            var button=Named<Button>(w,name);var end=button.TranslatePoint(new Point(button.Bounds.Width,button.Bounds.Height),frame)!.Value;
            Require(end.X<=frame.Bounds.Width-10&&end.Y<=frame.Bounds.Height-15,"Action escapes frame.");
            if(name=="DeleteReminderCancel")continue;
            var text=button.GetVisualDescendants().OfType<TextBlock>().Single();var center=text.TranslatePoint(new Point(text.Bounds.Width/2,text.Bounds.Height/2),button)!.Value;
            Require(Math.Abs(center.X-button.Bounds.Width/2)<1&&Math.Abs(center.Y-button.Bounds.Height/2)<1,"Action label off center.");
        }
    }
    internal static void Accessibility()
    {
        using var f=new Fixture(125);f.Settings.Typography.Global.SizePercent=150;Desktop("DesktopTypography","Apply",f.Settings.Typography);
        string text=string.Join(" ",Enumerable.Repeat("یادداشت‌های جلسه و برنامه‌ریزی برای پروژه فردا را مرور کنم.",30));var w=f.Open(text,Due);
        try
        {
            Require(Named<TextBlock>(w,"DeleteReminderText").FlowDirection==FlowDirection.RightToLeft,"RTL direction missing.");Bounds(w);
            var reading=Named<ScrollViewer>(w,"DeleteReminderTextScroll");Require(reading.Extent.Height>reading.Viewport.Height,"Long reminder is not bounded.");
            reading.Offset=new(0,100000);ClockOverlayChecks.Pump(25);Require(reading.Offset.Y>0,"Long reminder cannot scroll.");
            // Windows clamps a native window off a nonexistent negative-origin monitor back to the real display.
            Call(w,"FitDeleteReminderToScreen",new Screen(2,new PixelRect(0,0,720,1280),new PixelRect(0,30,720,1200),true));ClockOverlayChecks.Pump(50);Capture(w,125,"narrow-large-rtl");Bounds(w);
            Require(w.Position.X>=0&&w.Position.Y>=30&&w.Position.X+w.ClientSize.Width*2<=721&&w.Position.Y+w.ClientSize.Height*2<=1231,$"Dialog exceeds monitor work area: position {w.Position}, client {w.ClientSize}.");
            var cancel=Named<Button>(w,"CancelButton");var delete=Named<Button>(w,"ConfirmButton");
            Require(delete.TranslatePoint(default,w)!.Value.Y>=cancel.TranslatePoint(new Point(0,cancel.Bounds.Height),w)!.Value.Y,"Narrow actions do not stack.");
            var body=Named<ScrollViewer>(w,"DeleteReminderBody");body.Offset=new(0,100000);ClockOverlayChecks.Pump(25);
            var warning=Named<TextBlock>(w,"DeleteReminderWarning");Require(warning.TranslatePoint(new Point(0,warning.Bounds.Height),body)!.Value.Y<=body.Bounds.Height+1,"Deletion scope cannot be reached.");
            Capture(w,125,"narrow-large-rtl");cancel.Focus();Key(cancel,Avalonia.Input.Key.Tab);Require(delete.IsFocused,"Cannot reach Delete with Tab.");Key(delete,Avalonia.Input.Key.Escape);Require(!w.IsVisible&&!w.Result,"Escape deleted reminder.");
        }
        finally{w.Close();}
        f.Settings.Typography.Global.SizePercent=100;Desktop("DesktopTypography","Apply",f.Settings.Typography);f.Settings.UiScalePercent=70;Desktop("DesktopPageAppearance","Apply",f.Settings,true);
        w=f.Open();try{f.Settings.UiScalePercent=90;Desktop("DesktopPageAppearance","Apply",f.Settings,true);ClockOverlayChecks.Pump(40);Require(Math.Abs(w.ClientSize.Width-486)<1.1,"Live page scale not applied.");Bounds(w);}finally{w.Close();}
    }
    internal static void Actions()
    {
        using var f=new Fixture();
        void Check(Action<ConfirmationDialogWindow> action,bool expected)
        {
            var w=f.Open();int closed=0;w.Closed+=(_,_)=>closed++;
            try{action(w);Require(!w.IsVisible&&w.Result==expected&&closed==1,"Wrong completion result.");Click(w,"ConfirmButton");Click(w,"CancelButton");Require(w.Result==expected&&closed==1,"Stale callback changed result.");}finally{w.Close();}
        }
        Check(w=>Click(w,"CancelButton"),false);Check(w=>Click(w,"ConfirmButton"),true);Check(w=>Click(w,"DeleteReminderCancel"),false);Check(w=>w.Close(),false);
        foreach(var key in new[]{Avalonia.Input.Key.Enter,Avalonia.Input.Key.Space})
        {
            Check(w=>Key(Named<Button>(w,"CancelButton"),key),false);
            Check(w=>{var b=Named<Button>(w,"ConfirmButton");b.Focus();Key(b,key);},true);
        }
        Check(w=>Key(Named<Button>(w,"ConfirmButton"),Avalonia.Input.Key.Escape),false);
        var initial=f.Open();try{Key(initial,Avalonia.Input.Key.Enter);Require(initial.IsVisible&&!initial.Result,"Unfocused Enter deleted reminder.");}finally{initial.Close();}
        foreach(string theme in new[]{AppThemeCatalog.Midnight,AppThemeCatalog.Daylight,AppThemeCatalog.Pirate})
        {
            f.Settings.ThemeMode=theme;Desktop("ThemeResources","ApplyApplication",theme);var w=f.Open(Long,Due);
            try{Require(!w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(),"Nika frame leaked to legacy theme.");Require(Named<TextBlock>(w,"MessageText").Text!.Contains("20 Nov 2026"),"Legacy reminder lost schedule.");Click(w,"CancelButton");Require(!w.Result,"Legacy cancel accepted deletion.");}finally{w.Close();}
        }
    }
    internal static void Viewer()
    {
        using var f=new Fixture();
        foreach(string scenario in new[]{"cancel","delete","owner-close","settings-change","missing","before-vault-change","before-account-change"})
        {
            string root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"delete-reminder-check-"+Guid.NewGuid().ToString("N")));Directory.CreateDirectory(root);
            f.Settings.ObsidianVaultFolder=root;f.Settings.NotesSubfolder="Capture";
            const string account="11111111-1111-4111-8111-111111111111";
            f.Settings.HabiticaUserId=account;f.Settings.HabiticaEnabled=true;
            var target=new NoteEntry(NoteType.Reminder,DateTime.Today.AddHours(9),Short);var neighbor=new NoteEntry(NoteType.Reminder,DateTime.Today.AddHours(10),"Keep this reminder");
            foreach(var entry in new[]{target,neighbor})Require(HabiticaIntegration.Current.Apply(f.Settings,null,entry,Due,null,false,()=>ObsidianNotesSync.AppendEntry(root,entry.Type,entry.Text,"Capture",entry.Timestamp)).LocalResult.Success,"Fixture write failed.");
            string metadataPath=Path.Combine(root,"Capture",".stopwatch-habitica.json");
            HabiticaIntegration.Vault ReadMetadata()=>JsonSerializer.Deserialize<HabiticaIntegration.Vault>(File.ReadAllText(metadataPath))!;
            var seeded=ReadMetadata();foreach(var item in seeded.Items)item.Targets[account].RemoteId=Guid.NewGuid().ToString();
            File.WriteAllText(metadataPath,JsonSerializer.Serialize(seeded));
            f.Settings.HabiticaEnabled=false;
            var w=new NotesViewerWindow(f.Settings){ShowActivated=false,Topmost=false};var prompts=new List<ConfirmationDialogWindow>();
            using var listener=Window.WindowOpenedEvent.AddClassHandler<Window>((window,_)=>{if(window is ConfirmationDialogWindow dialog)prompts.Add(dialog);});
            try
            {
                w.Show();ClockOverlayChecks.Pump(50);
                if(scenario.StartsWith("before-"))
                {
                    string other=Path.Combine(root,"OtherVault");Directory.CreateDirectory(Path.Combine(other,"Capture"));
                    foreach(string file in Directory.GetFiles(Path.Combine(root,"Capture")))File.Copy(file,Path.Combine(other,"Capture",Path.GetFileName(file)));
                    if(scenario=="before-vault-change")f.Settings.ObsidianVaultFolder=other;
                    else f.Settings.HabiticaUserId="22222222-2222-4222-8222-222222222222";
                    var stale=(Task)Call(w,"DeleteReminderAsync",target)!;ClockOverlayChecks.Pump(35);
                    Require(stale.IsCompletedSuccessfully&&prompts.Count==0,"Stale viewer row can target a changed vault or Habitica account.");
                    Require(ObsidianNotesSync.LoadAllNotes(root,"Capture").Count==2&&ObsidianNotesSync.LoadAllNotes(other,"Capture").Count==2,"Stale row changed a vault.");
                    continue;
                }
                var pending=(Task)Call(w,"DeleteReminderAsync",target)!;ClockOverlayChecks.Pump(35);
                Require(prompts.Count==1&&prompts[0].GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(),"Viewer did not use approved reminder dialog.");
                var duplicate=(Task)Call(w,"DeleteReminderAsync",neighbor)!;ClockOverlayChecks.Pump(25);Require(prompts.Count==1&&duplicate.IsCompleted,"Repeated request opened a second dialog.");
                var dialog=prompts[0];Require(Named<TextBlock>(dialog,"DeleteReminderText").Text==target.Text&&Named<TextBlock>(dialog,"DeleteReminderSchedule").Text=="20 Nov 2026 · 18:30","Viewer passed wrong reminder or due date.");
                if(scenario=="owner-close")w.Close();
                else if(scenario=="cancel")Click(dialog,"CancelButton");
                else
                {
                    if(scenario=="settings-change")f.Settings.NotesSubfolder="Elsewhere";
                    if(scenario=="missing")Require(ObsidianNotesSync.DeleteEntry(root,target,"Capture").Success,"Fixture delete failed.");
                    Click(dialog,"ConfirmButton");Click(dialog,"ConfirmButton");
                }
                ClockOverlayChecks.Pump(40);Require(pending.IsCompletedSuccessfully&&!dialog.IsVisible,"Deletion flow failed or left dialog open.");
                var entries=ObsidianNotesSync.LoadAllNotes(root,"Capture");Require(entries.Any(e=>e.Text==neighbor.Text),"Deleted a neighboring reminder.");
                Require(entries.Any(e=>e.Text==target.Text)==(scenario is "cancel" or "owner-close" or "settings-change"),"Incorrect reminder persistence.");
                var metadata=ReadMetadata();var deleted=metadata.Items.Single(i=>i.Entry!.Text==target.Text).Targets[account];
                var kept=metadata.Items.Single(i=>i.Entry!.Text==neighbor.Text).Targets[account];
                Require(deleted.Deleted==(scenario=="delete")&&deleted.Revision==(scenario=="delete"?2:1),"Offline Habitica deletion intent does not match confirmed local deletion.");
                Require(!kept.Deleted&&kept.Revision==1&&kept.Due?.ToLocalTime()==Due&&deleted.Due?.ToLocalTime()==Due,"Neighbor sync state or reminder schedule changed.");
                Require(metadata.Intents.Count==0&&!f.Settings.HabiticaEnabled,"Deletion left an incomplete intent or enabled sync.");
                if(scenario=="owner-close"){Click(dialog,"ConfirmButton");Require(ObsidianNotesSync.LoadAllNotes(root,"Capture").Count==2,"Closed prompt accepted stale deletion.");}
            }
            finally
            {
                w.Close();foreach(var dialog in prompts)dialog.Close();f.Settings.NotesSubfolder="Capture";
                Require(root.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(root).StartsWith("delete-reminder-check-"),"Fixture cleanup escaped temporary root.");Directory.Delete(root,true);
            }
        }
    }
    static void Capture(Window w,int scale,string state)
    {
        var frame=w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single();
        using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(w.ClientSize.Width),(int)Math.Ceiling(w.ClientSize.Height)),new Vector(96,96));
        if(scale>=100)bitmap.Render(w);else{using var dc=bitmap.CreateDrawingContext();dc.DrawRectangle(new VisualBrush(frame){Stretch=Stretch.Fill,SourceRect=new RelativeRect(frame.Bounds,RelativeUnit.Absolute)},null,new Rect(frame.Bounds.Size));}
        bitmap.Save(Path.Combine(Output,$"delete-reminder-{state}-{scale}.png"));
    }
}
