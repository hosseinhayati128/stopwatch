using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.LogicalTree;
using Avalonia.Controls.Templates;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class SettingsWindow
{
    private readonly TodoAppearancePreview _todoPreview=new();
    private TextBlock? _todoSaveStatus;
    private TodoEmblemPickerWindow? _emblemPicker;
    private bool _todoCompactColumns;

    private void LoadTodoAppearance()
    {
        if (TodoAppearancePanel==null) return;
        _settings.TodoAppearance ??= new();_settings.TodoAppearance.Normalize();
        var config=_settings.TodoAppearance;var p=config.Current;
        TodoAppearancePanel.Children.Clear();
        TodoAppearancePanel.Resources["AppFontFamily"] = TodoVisuals.BodyFont;
        void Heading(string text) => TodoAppearancePanel.Children.Add(new TextBlock {Text=text,FontSize=13,FontWeight=FontWeight.SemiBold,Margin=new Thickness(0,12,0,5)});
        TodoAppearancePanel.Children.Add(new TextBlock {Text="Todo appearance",Classes={"heading"}});
        TodoAppearancePanel.Children.Add(new TextBlock {Text="Customize Nika's Chronicle. Each style remembers its own choices.",Classes={"subheading"},TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)});
        if(AppThemeCatalog.Normalize(_settings.ThemeMode)!=AppThemeCatalog.Nika)
        {
            var activate=new Button {Content="Use Nika's Chronicle",Classes={"primary"}};
            activate.Click+=(_,_)=>ThemeCombo.SelectedItem=AppThemeCatalog.Nika;
            TodoAppearancePanel.Children.Add(activate);
        }
        Heading("Theme");
        var profiles=new Grid {ColumnDefinitions=new ColumnDefinitions("*,10,*")};
        for(int i=0;i<TodoAppearanceCatalog.Profiles.Count;i++)
        {
            var option=TodoAppearanceCatalog.Profiles[i];
            var button=new Button {Name="TodoProfile_"+option.Id,Content=option.Label,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Center,Padding=new Thickness(12,10)};
            TodoVisuals.StyleButton(button,p,primary:config.Profile==option.Id,pixel:option.Id=="pixel");
            button.Click+=(_,_)=>{config.Profile=option.Id;CommitTodoAppearance();};Grid.SetColumn(button,i*2);profiles.Children.Add(button);
        }
        TodoAppearancePanel.Children.Add(profiles);
        Heading("Background");
        var backgrounds=new WrapPanel {Orientation=Orientation.Horizontal};
        foreach(var option in TodoAppearanceCatalog.Backgrounds)
        {
            var stack=new StackPanel {Spacing=4};stack.Children.Add(new TodoArtwork {Kind="Background",Choice=option.Id,Width=88,Height=68});
            stack.Children.Add(new TextBlock {Text=option.Label,FontSize=10,TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center,MaxWidth=88,MinHeight=28});
            var button=new Button {Name="TodoBackground_"+option.Id,Content=stack,Padding=new Thickness(4),Margin=new Thickness(0,0,6,5)};
            TodoVisuals.StyleButton(button,p);button.BorderThickness=new Thickness(p.Background==option.Id?2:1);
            AutomationProperties.SetName(button,option.Label+" background");button.Click+=(_,_)=>{p.Background=option.Id;CommitTodoAppearance();};backgrounds.Children.Add(button);
        }
        TodoAppearancePanel.Children.Add(backgrounds);
        AddTodoArtworkLayer(p);
        AddEmblemSelector("Header emblem",p.HeaderEmblem,false);
        var matchesHeader=new CheckBox {Name="TodoLauncherMatchesHeader",Content="Launcher: same as header",IsChecked=p.LauncherMatchesHeader,Margin=new Thickness(0,10,0,0)};
        matchesHeader.IsCheckedChanged+=(_,_)=>{p.LauncherMatchesHeader=matchesHeader.IsChecked==true;CommitTodoAppearance();};TodoAppearancePanel.Children.Add(matchesHeader);
        AddEmblemSelector("Launcher emblem",p.EffectiveLauncherEmblem,true);
        var engraved=new CheckBox {Name="TodoEngravedHeader",Content="Engraved header",IsChecked=p.EngravedHeader,Margin=new Thickness(0,10,0,0)};
        engraved.IsCheckedChanged+=(_,_)=>{p.EngravedHeader=engraved.IsChecked==true;CommitTodoAppearance();};TodoAppearancePanel.Children.Add(engraved);
        AddTodoChoice("Task rows","TodoRowsChoice",TodoAppearanceCatalog.Rows,p.Rows,id=>p.Rows=id);
        AddTodoChoice("Priority markers","TodoPrioritiesChoice",TodoAppearanceCatalog.Priorities,p.Priorities,id=>p.Priorities=id);
        AddTodoChoice("Subtasks and notes","TodoDetailsChoice",TodoAppearanceCatalog.Details,p.Details,id=>p.Details=id);
        AddTodoChoice("Toolbar and actions","TodoToolbarChoice",TodoAppearanceCatalog.Toolbars,p.Toolbar,id=>p.Toolbar=id);
        Heading("Task feedback");
        TodoAppearancePanel.Children.Add(new TextBlock {Text=config.IsPixel?"Pixel Signals":"Quiet Ledger",Classes={"subheading"}});
        _todoSaveStatus=new TextBlock {Name="TodoSaveStatus",Text="Changes apply immediately.",FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,14,0,0)};
        TodoAppearancePanel.Children.Add(_todoSaveStatus);
        foreach(var control in TodoAppearancePanel.GetLogicalDescendants().OfType<Control>())
            if(control is TextBlock label) label.FontFamily=label.Classes.Contains("heading")?TodoVisuals.HeadingFont:TodoVisuals.BodyFont;
            else if(control is ComboBox or CheckBox) control.SetValue(Avalonia.Controls.Documents.TextElement.FontFamilyProperty,TodoVisuals.BodyFont);
        _todoPreview.Apply(config);TodoPreviewHost.Content=_todoPreview;
    }

    private void AddTodoChoice(string label,string name,IReadOnlyList<TodoAppearanceOption> options,string selected,Action<string> set)
    {
        var row=new Grid {ColumnDefinitions=new ColumnDefinitions("*,*"),Margin=new Thickness(0,12,0,0)};
        row.Children.Add(new TextBlock {Text=label,FontSize=12,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,10,0)});
        var combo=new ComboBox {Name=name,ItemsSource=options,SelectedItem=options.First(x=>x.Id==selected),HorizontalAlignment=HorizontalAlignment.Stretch,FontSize=12};
        AutomationProperties.SetName(combo,label);combo.SelectionChanged+=(_,_)=>{if(combo.SelectedItem is TodoAppearanceOption option){set(option.Id);CommitTodoAppearance();}};
        Grid.SetColumn(combo,1);row.Children.Add(combo);TodoAppearancePanel.Children.Add(row);
    }

    private void AddEmblemSelector(string label,string selected,bool launcher)
    {
        var row=new Grid {ColumnDefinitions=new ColumnDefinitions("*,44,Auto,Auto"),Margin=new Thickness(0,10,0,0)};
        row.Children.Add(new TextBlock {Text=label,VerticalAlignment=VerticalAlignment.Center,FontSize=12});
        var art=new TodoArtwork {Choice=selected,Width=38,Height=38};Grid.SetColumn(art,1);row.Children.Add(art);
        var text=new TextBlock {Text=TodoAppearanceCatalog.Emblems.First(o=>o.Id==selected).Label,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(4,0,12,0),FontSize=12};Grid.SetColumn(text,2);row.Children.Add(text);
        var choose=new Button {Name=launcher?"ChooseLauncherEmblem":"ChooseHeaderEmblem",Content="Choose…",Classes={"modern"},Padding=new Thickness(10,5),FontSize=12};
        choose.IsEnabled=!launcher||!_settings.TodoAppearance.Current.LauncherMatchesHeader;
        AutomationProperties.SetName(choose,"Choose "+label.ToLowerInvariant());
        choose.Click+=async(_,_)=>
        {
            if(_emblemPicker!=null){_emblemPicker.Activate();return;}
            var profile=_settings.TodoAppearance.Current;
            _emblemPicker=new TodoEmblemPickerWindow(launcher?profile.LauncherEmblem:profile.HeaderEmblem,launcher,profile);
            try {var result=await _emblemPicker.ShowDialog<string?>(this);if(result!=null&&!_closed){if(launcher)profile.LauncherEmblem=result;else profile.HeaderEmblem=result;CommitTodoAppearance();}}
            finally {_emblemPicker=null;}
        };
        Grid.SetColumn(choose,3);row.Children.Add(choose);TodoAppearancePanel.Children.Add(row);
    }

    private void AddTodoArtworkLayer(TodoAppearanceProfile profile)
    {
        TodoAppearancePanel.Children.Add(new TextBlock {Text="Sidebar artwork layer",FontSize=13,FontWeight=FontWeight.SemiBold,Margin=new Thickness(0,12,0,5)});
        var choice=new ComboBox {Name="TodoSidebarArtworkChoice",ItemsSource=TodoAppearanceCatalog.SidebarArtworks,
            SelectedItem=TodoAppearanceCatalog.SidebarArtworks.First(x=>x.Id==profile.SidebarArtwork),HorizontalAlignment=HorizontalAlignment.Stretch};
        choice.ItemTemplate=new FuncDataTemplate<TodoAppearanceOption>((option,_)=>
        {
            // Popup virtualization clears the data item while recycling a row.
            if(option==null)return null;
            var row=new Grid {ColumnDefinitions=new ColumnDefinitions("48,*"),Margin=new Thickness(0,2)};
            row.Children.Add(new Border {Background=TodoVisuals.Paper(profile),CornerRadius=new CornerRadius(3),Width=42,Height=42,
                Child=new TodoMuralArtwork {Choice=option.Id,DarkPaper=profile.Background=="midnight-chart"}});
            var label=new TextBlock {Text=option.Label,FontSize=12,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};
            Grid.SetColumn(label,1);row.Children.Add(label);return row;
        });
        choice.SelectionBoxItemTemplate=choice.ItemTemplate;
        AutomationProperties.SetName(choice,"Todo sidebar artwork layer");
        choice.SelectionChanged+=(_,_)=>{if(choice.SelectedItem is TodoAppearanceOption option){profile.SidebarArtwork=option.Id;CommitTodoAppearance();}};
        TodoAppearancePanel.Children.Add(choice);
        var opacityRow=new Grid {ColumnDefinitions=new ColumnDefinitions("Auto,*,46"),Margin=new Thickness(0,8,0,0),IsEnabled=profile.SidebarArtwork!="none"};
        opacityRow.Children.Add(new TextBlock {Text="Opacity",FontSize=12,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,10,0)});
        var opacity=new Slider {Name="TodoSidebarArtworkOpacity",Minimum=0,Maximum=100,TickFrequency=1,IsSnapToTickEnabled=true,Value=Math.Round(profile.SidebarArtworkOpacity*100),MinWidth=80};
        var value=new TextBlock {Text=$"{opacity.Value:0}%",FontSize=12,TextAlignment=TextAlignment.Right,VerticalAlignment=VerticalAlignment.Center};
        AutomationProperties.SetName(opacity,"Todo artwork opacity");
        opacity.PropertyChanged+=(_,e)=>
        {
            if(e.Property!=Slider.ValueProperty)return;
            profile.SidebarArtworkOpacity=opacity.Value/100;value.Text=$"{opacity.Value:0}%";
            // Retain this slider while dragging; rebuilding the panel loses pointer capture.
            CommitTodoAppearance(rebuildControls:false);
        };
        Grid.SetColumn(opacity,1);opacityRow.Children.Add(opacity);Grid.SetColumn(value,2);opacityRow.Children.Add(value);TodoAppearancePanel.Children.Add(opacityRow);
    }

    private void CommitTodoAppearance(bool rebuildControls=true)
    {
        _settings.TodoAppearance.Normalize();
        if(rebuildControls)LoadTodoAppearance();else _todoPreview.Apply(_settings.TodoAppearance);
        SettingsChanged?.Invoke(SettingsChangeKind.TodoAppearance);
    }

    internal void ReportPersistenceResult(bool saved)
    {
        if(_todoSaveStatus==null)return;
        _todoSaveStatus.Text=saved?"Changes apply immediately.":"Applied for this session, but settings could not be saved. Check disk space and try again.";
        _todoSaveStatus.Foreground=saved?TodoVisuals.Muted(new TodoAppearanceProfile()):Brush.Parse("#AD3434");
    }

    private void UpdateTodoPreviewVisibility(string category)
    {
        if(TodoPreviewArea==null||ClockPreviewArea==null)return;
        TodoPreviewArea.IsVisible=category=="TodoAppearance";
        ClockPreviewArea.IsVisible=category!="TodoAppearance";
        TodoAppearancePanel.IsVisible=category=="TodoAppearance";
        UpdateTodoResponsiveLayout();
        // The initial sample is built before the window's control templates attach.
        // Refresh when this category becomes visible so its snapshot includes text.
        if(category=="TodoAppearance")_todoPreview.Apply(_settings.TodoAppearance);
    }

    private void UpdateTodoResponsiveLayout()
    {
        if(SettingsColumns==null)return;
        bool compact=CurrentCategory=="TodoAppearance"&&ClientSize.Width<1000;
        if(compact==_todoCompactColumns)return;
        _todoCompactColumns=compact;
        SettingsColumns.ColumnDefinitions=new ColumnDefinitions(compact?"200,160,*":"340,190,*");
    }
}
