using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class NoteEntryWindow
{
    private void ApplyNoteEditorAppearance()
    {
        if(AppThemeCatalog.Normalize(_settings.ThemeMode)!=AppThemeCatalog.Nika)return;
        bool reminder=_noteType==NoteType.Reminder;
        Classes.Add(reminder?"nikaReminder":"nikaQuickNote");
        FontFamily=ClockPickerPresentation.Body;
        var ink=ClockPickerPresentation.Navy;
        var gold=Brush.Parse("#B78329");
        // Reuse the named controls so input, save, cancel and accessibility keep their handlers.
        var oldHeader=(Grid)HeadingText.Parent!.Parent!;
        ((Panel)HeadingText.Parent!).Children.Remove(HeadingText);
        oldHeader.Children.Remove(CloseButton);
        ((Border)NoteInputBox.Parent!).Child=null;
        ((Panel)CancelButton.Parent!).Children.Remove(CancelButton);
        ((Panel)SaveButton.Parent!).Children.Remove(SaveButton);
        ((Panel)SaveError.Parent!).Children.Remove(SaveError);
        ((Panel)EditorShortcutHint.Parent!).Children.Remove(EditorShortcutHint);
        Content=null;

        HeadingText.FontFamily=StopwatchTypography.Resolve("navigator-bold").NameFont;
        HeadingText.FontSize=28;HeadingText.FontWeight=FontWeight.Bold;HeadingText.Foreground=ink;
        var heading=new StackPanel {Spacing=4,Children={HeadingText,new TextBlock {
            Text=reminder?(_isEditing?"Update your reminder and its optional schedule.":"Add a reminder, with an optional date and time."):"Capture a thought. Keep your flow.",
            FontSize=12,Foreground=ClockPickerPresentation.Muted}}};
        CloseButton.Width=32;CloseButton.Height=32;CloseButton.MinHeight=0;CloseButton.Padding=new(0);
        CloseButton.Background=Brushes.Transparent;CloseButton.BorderThickness=new(0);
        CloseButton.Content=new Avalonia.Controls.Shapes.Path {Width=16,Height=16,Stretch=Stretch.Uniform,
            Data=Geometry.Parse("M1,1 L15,15 M15,1 L1,15"),Stroke=ink,StrokeThickness=1.7};
        AutomationProperties.SetName(CloseButton,reminder?"Close Reminder":"Close Quick Note");
        var header=new Grid {ColumnDefinitions=new("*,32"),Margin=new(0,0,0,16),Background=Brushes.Transparent,Children={heading,CloseButton}};
        Grid.SetColumn(CloseButton,1);header.PointerPressed+=OnHeaderPointerPressed;

        NoteInputBox.Foreground=NoteInputBox.CaretBrush=ink;NoteInputBox.FontFamily=ClockPickerPresentation.Body;
        NoteInputBox.FontSize=14;NoteInputBox.Padding=new(14,12);NoteInputBox.VerticalContentAlignment=VerticalAlignment.Top;
        NoteInputBox.Watermark=reminder?"What should I remind you about?":"Write a quick note…";
        AutomationProperties.SetName(NoteInputBox,reminder?"Reminder text":"Note text");
        var editor=new Border {Background=Brush.Parse("#F8EACA"),BorderBrush=Brush.Parse("#CDB784"),BorderThickness=new(1),CornerRadius=new(5),Child=NoteInputBox};
        foreach(var button in new[]{CancelButton,SaveButton})
        {
            button.MinHeight=38;button.MinWidth=88;button.Padding=new(16,6);button.FontSize=13;
            button.FontFamily=ClockPickerPresentation.Body;button.FontWeight=FontWeight.Normal;
            button.BorderBrush=gold;button.BorderThickness=new(1);button.CornerRadius=new(4);
            button.Foreground=button==SaveButton?Brushes.White:ink;
            button.Background=button==SaveButton?ink:Brushes.Transparent;
        }
        DesktopTypography.SetPreserveColor(SaveButton,true);
        SaveButton.Content=reminder?(_isEditing?"Save changes":"Save reminder"):"Save note";
        var actions=new StackPanel {Orientation=Orientation.Horizontal,Spacing=8,Children={CancelButton,SaveButton}};
        Grid.SetColumn(CancelButton,0);Grid.SetColumn(SaveButton,0);
        EditorShortcutHint.Text="Ctrl+Enter save  ·  Esc cancel";EditorShortcutHint.FontSize=11;
        EditorShortcutHint.Foreground=ClockPickerPresentation.Muted;EditorShortcutHint.HorizontalAlignment=HorizontalAlignment.Left;
        EditorShortcutHint.VerticalAlignment=VerticalAlignment.Center;EditorShortcutHint.TextWrapping=TextWrapping.Wrap;EditorShortcutHint.Margin=new(0,0,12,0);
        var footer=new Grid {ColumnDefinitions=new("*,Auto"),Children={EditorShortcutHint,actions}};Grid.SetColumn(actions,1);
        SaveError.Foreground=Brush.Parse("#9C3026");SaveError.Margin=new(0,0,0,8);
        var bottom=new StackPanel {Margin=new(0,16,0,0),Children={SaveError,footer}};
        var layout=new Grid {RowDefinitions=new("Auto,*,Auto"),Children={header,editor,bottom}};
        Grid.SetRow(editor,1);Grid.SetRow(bottom,2);
        if(reminder)
        {
            layout.RowDefinitions=new("Auto,*,Auto,Auto");
            var schedule=CreateReminderSchedule();Grid.SetRow(schedule,2);layout.Children.Add(schedule);Grid.SetRow(bottom,3);
        }
        Content=new StopwatchFrameSurface {ArtworkScale=.16,Padding=new(10),Background=Brush.Parse("#EBD3A5"),Child=layout};
        double scale=AppUiScale.Normalize(_settings.UiScalePercent)/100;
        MinWidth=520*scale;MinHeight=330*scale;Width=620*scale;Height=360*scale;
        if(reminder){MinWidth=600*scale;MinHeight=(DueFields.IsVisible?490:400)*scale;Height=(DueFields.IsVisible?510:420)*scale;}
        Opened+=(_,_)=>FitNoteEditorContent();
        SizeChanged+=(_,_)=>FitNoteEditorContent();
    }

    internal void FitNoteEditorContent(bool scaleChanged = false)
    {
        if (_detailEditorRoot != null) { FitTodoDetailEditor(scaleChanged); return; }
        if(DesktopPageAppearance.Content(this) is not StopwatchFrameSurface frame)return;
        double scale=DesktopPageAppearance.Scale(this);
        // Keep the transformed editor inside the native client area after shrinking or zooming.
        frame.Width=(ClientSize.Width>0?ClientSize.Width:Width)/scale;
        frame.Height=(ClientSize.Height>0?ClientSize.Height:Height)/scale;
        if(Content is LayoutTransformControl transform)transform.InvalidateArrange();
    }
}
