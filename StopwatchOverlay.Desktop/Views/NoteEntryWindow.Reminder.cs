using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class NoteEntryWindow
{
    private Border? _reminderSchedule;
    private TextBlock? _reminderNoSchedule;

    private Border CreateReminderSchedule()
    {
        var ink=ClockPickerPresentation.Navy;var cream=Brush.Parse("#F8EACA");var rule=Brush.Parse("#CDB784");
        TextBlock Label(string text)=>new(){Text=text,FontSize=11,Foreground=ClockPickerPresentation.Muted,FontFamily=ClockPickerPresentation.Body,TextWrapping=TextWrapping.Wrap};
        ((Panel)TodoDueDate.Parent!).Children.Remove(TodoDueDate);
        ((Panel)TodoDueTime.Parent!).Children.Remove(TodoDueTime);
        ((Panel)TodoDetailsPanel.Parent!).Children.Remove(TodoDetailsPanel);
        TodoDetailsPanel.Children.Clear();TodoDetailsPanel.Margin=new(0);TodoDetailsPanel.Spacing=0;
        // Picker template buttons use AppFontFamily rather than inheriting the picker font.
        TodoDetailsPanel.Resources["AppFontFamily"]=ClockPickerPresentation.Body;
        DueFields.Children.Clear();DueFields.Spacing=0;
        DueEnabledToggle.Content=new TextBlock {Text="Set date & time",FontSize=13,Foreground=ink,Margin=new(8,0,0,0)};
        DueEnabledToggle.MinHeight=28;DueEnabledToggle.Padding=new(0);
        DueEnabledToggle.Resources["CheckBoxCheckBackgroundFillChecked"]=ink;
        DueEnabledToggle.Resources["CheckBoxCheckBorderBrushChecked"]=Brush.Parse("#B78329");
        DueEnabledToggle.Resources["CheckBoxCheckGlyphForegroundChecked"]=cream;
        TodoDueDate.Background=TodoDueTime.Background=cream;TodoDueDate.Foreground=TodoDueTime.Foreground=ink;
        TodoDueDate.BorderBrush=TodoDueTime.BorderBrush=rule;TodoDueDate.FontSize=TodoDueTime.FontSize=13;
        TodoDueDate.FontFamily=TodoDueTime.FontFamily=ClockPickerPresentation.Body;
        TodoDueDate.MinHeight=TodoDueTime.MinHeight=38;TodoDueTime.HorizontalAlignment=HorizontalAlignment.Stretch;
        AutomationProperties.SetName(TodoDueDate,"Reminder date");AutomationProperties.SetName(TodoDueTime,"Reminder time (24-hour, local)");
        var dateField=new StackPanel {Spacing=5,Children={Label("Date"),TodoDueDate}};
        var timeField=new StackPanel {Spacing=5,Children={Label("Time · 24-hour"),TodoDueTime}};
        var fields=new Grid {ColumnDefinitions=new("*,16,190"),Margin=new(0,10,0,0),Children={dateField,timeField}};Grid.SetColumn(timeField,2);
        var localTime=Label("Local time · uses this computer’s time zone");localTime.Margin=new(0,8,0,0);
        DueFields.Children.Add(fields);DueFields.Children.Add(localTime);
        _reminderNoSchedule=Label("No date or time set.");_reminderNoSchedule.Margin=new(0,4,0,0);_reminderNoSchedule.IsVisible=!DueFields.IsVisible;
        TodoDetailsPanel.Children.Add(DueEnabledToggle);TodoDetailsPanel.Children.Add(_reminderNoSchedule);TodoDetailsPanel.Children.Add(DueFields);
        _reminderSchedule=new Border {MinHeight=DueFields.IsVisible?166:76,Margin=new(0,16,0,0),Padding=new(12),Background=Brush.Parse("#33FFF3D8"),
            BorderBrush=rule,BorderThickness=new(1),CornerRadius=new(5),Child=TodoDetailsPanel};
        return _reminderSchedule;
    }

    private void UpdateReminderSchedule()
    {
        if(_reminderSchedule==null)return;
        bool enabled=DueEnabledToggle.IsChecked==true;
        double scale=IsVisible?DesktopPageAppearance.Scale(this):AppUiScale.Normalize(_settings.UiScalePercent)/100;
        double nextMinimum=enabled?166:76;
        double nextHeight=Height+(nextMinimum-_reminderSchedule.MinHeight)*scale;
        _reminderSchedule.MinHeight=nextMinimum;_reminderNoSchedule!.IsVisible=!enabled;
        MinHeight=(enabled?490:400)*scale;Height=System.Math.Max(MinHeight,nextHeight);
    }
}
