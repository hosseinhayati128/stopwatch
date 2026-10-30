using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.LogicalTree;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class NoteEntryWindow
{
    private void ApplyTodoEditorAppearance()
    {
        if(AppThemeCatalog.Normalize(_settings.ThemeMode)!=AppThemeCatalog.Nika || Classes.Contains("nikaTodoDialog"))return;
        _settings.TodoAppearance ??= new();_settings.TodoAppearance.Normalize();
        var p=_settings.TodoAppearance.Current;bool pixel=_settings.TodoAppearance.IsPixel;
        Classes.Add("nikaTodoDialog");Classes.Set("pixelTodoDialog",pixel);
        TodoVisuals.Palette(this,p);FontFamily=TodoVisuals.BodyFont;
        Resources["AppFontFamily"]=TodoVisuals.BodyFont;
        var oldFrame=(Border)Content!;var grid=(Grid)oldFrame.Child!;oldFrame.Child=null;Content=null;
        grid.RowDefinitions=new RowDefinitions("64,*,Auto");
        var map=new TodoArtwork {Kind="Background",Choice=p.Background,Opacity=.65};Grid.SetRow(map,1);Grid.SetRowSpan(map,2);grid.Children.Insert(0,map);
        var header=(Grid)HeadingText.Parent!.Parent!;
        header.Margin=new Thickness(0);header.Background=TodoVisuals.Navy;
        if(p.EngravedHeader)header.Children.Insert(0,new TodoArtwork {Kind="Header",Opacity=.65});
        var heading=(StackPanel)HeadingText.Parent!;heading.Margin=new Thickness(12,0,50,0);heading.Spacing=8;
        BadgeIcon.IsVisible=false;heading.Children.Insert(0,new TodoArtwork {Choice=p.HeaderEmblem,Width=44,Height=44});
        HeadingText.FontFamily=TodoVisuals.HeadingFont;HeadingText.FontSize=28;HeadingText.Foreground=TodoVisuals.Ivory;
        CloseButton.Foreground=TodoVisuals.Ivory;CloseButton.Background=Brushes.Transparent;CloseButton.BorderThickness=new Thickness(0);CloseButton.Margin=new Thickness(0,14,12,0);
        var body=grid.Children.OfType<ScrollViewer>().Single();body.Margin=new Thickness(16,14,16,0);
        var footer=grid.Children.OfType<StackPanel>().Single();footer.Margin=new Thickness(16,12,16,12);
        if(NoteInputBox.Parent is Border inputFrame)
        {
            inputFrame.Background=TodoVisuals.Card(p);inputFrame.BorderBrush=TodoVisuals.Brass;
            inputFrame.CornerRadius=new CornerRadius(pixel?0:5);
            if(pixel&&inputFrame.Parent is Panel parent)
            {
                int index=parent.Children.IndexOf(inputFrame);inputFrame.Child=null;parent.Children.RemoveAt(index);
                parent.Children.Insert(index,new TodoFrame {Child=NoteInputBox,Pixel=true,Background=TodoVisuals.Card(p),BorderBrush=TodoVisuals.Brass,BorderThickness=new Thickness(1),Padding=new Thickness(6),MinHeight=80});
            }
        }
        NoteInputBox.FontFamily=TodoVisuals.BodyFont;NoteInputBox.FontSize=16;
        // ScrollViewer content is not in the visual tree until its template attaches.
        foreach(var button in grid.GetLogicalDescendants().OfType<Button>())
            if(button!=CloseButton)TodoVisuals.StyleButton(button,p,primary:button==SaveButton,pixel:p.Toolbar=="pixel-controls");
        TodoVisuals.Checkbox(DueEnabledToggle,p,child:true);
        _subtaskEditor.ApplyTodoAppearance(p,p.Toolbar=="pixel-controls");
        TodoPriorityCombo.Background=TodoVisuals.Card(p);TodoPriorityCombo.Foreground=TodoVisuals.Ink(p);TodoPriorityCombo.BorderBrush=TodoVisuals.Brass;
        TodoPriorityCombo.FontSize=15;TodoPriorityCombo.FontWeight=FontWeight.SemiBold;TodoPriorityCombo.VerticalContentAlignment=VerticalAlignment.Center;
        TodoPriorityRow.ColumnDefinitions=new ColumnDefinitions("150,12,*");
        TodoPlacementRow.ColumnDefinitions=new ColumnDefinitions("150,12,*");
        TodoPriorityCombo.HorizontalAlignment=HorizontalAlignment.Left;TodoPriorityCombo.Width=120;
        if(pixel)TodoVisuals.FramePixelInput(TodoPriorityRow,TodoPriorityCombo,p);
        foreach(var text in grid.GetLogicalDescendants().OfType<TextBlock>())
            if(text!=HeadingText){text.FontFamily=TodoVisuals.BodyFont;if(text.FontSize>=12)text.FontSize=15;}
        foreach(var label in TodoPriorityRow.Children.Concat(TodoPlacementRow.Children).OfType<TextBlock>())
        {label.FontWeight=FontWeight.SemiBold;label.Foreground=TodoVisuals.Ink(p);}
        SaveButton.FontFamily=TodoVisuals.BodyFont;SaveButton.FontSize=15;SaveButton.FontWeight=FontWeight.SemiBold;
        CancelButton.FontSize=15;
        SaveButton.MinHeight=CancelButton.MinHeight=40;
        EditorShortcutHint.FontSize=12;
        TodoVisuals.AddShellTrim(grid,pixel);
        var frame=new TodoFrame {Child=grid,Pixel=pixel,Margin=new Thickness(8),Background=TodoVisuals.Paper(p),BorderBrush=TodoVisuals.Gold,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(9),ClipToBounds=true};
        Content=frame;
        if(_noteType==NoteType.Todo) {Height=DueFields.IsVisible?650:550;MinHeight=440;}
        else {Height=330;MinHeight=270;}
    }

    private void ShowSavedSyncWarning(string message)
    {
        _savedWithWarning=true;
        NoteInputBox.IsEnabled=TodoDetailsPanel.IsEnabled=false;
        SaveButton.Content="Close";
        if(Classes.Contains("nikaTodoDialog"))
        {
            CancelButton.IsVisible=false;Grid.SetColumn(SaveButton,0);Grid.SetColumnSpan(SaveButton,3);
            EditorShortcutHint.Text="Esc to Close";ToolTip.SetTip(SaveButton,"Close (Esc)");
        }
        ShowSaveError(message);
    }
}
