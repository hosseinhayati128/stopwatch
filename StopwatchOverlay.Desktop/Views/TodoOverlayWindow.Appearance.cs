using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class TodoOverlayWindow
{
    private bool IsNikaTodo => AppThemeCatalog.Normalize(_settings.ThemeMode) == AppThemeCatalog.Nika;
    private TodoAppearanceProfile TodoLook => _settings.TodoAppearance.Current;
    private bool _hadTodoAppearance;

    private void ApplyTodoAppearance()
    {
        _settings.TodoAppearance ??= new();
        _settings.TodoAppearance.Normalize();
        bool active = IsNikaTodo;
        Classes.Set("nikaTodo", active);
        Classes.Set("pixelTodo", active && _settings.TodoAppearance.IsPixel);
        TodoVisuals.Palette(this, active ? TodoLook : null);
        TodoMap.IsVisible = TodoEmblem.IsVisible = active;
        TodoMural.Apply(TodoLook);
        TodoMural.IsVisible = active && TodoLook.SidebarArtwork != "none";
        TodoHeaderArt.IsVisible = active && TodoLook.EngravedHeader;
        TodoHeaderArt.Opacity=.65;
        SkullArtwork.IsVisible = !active && Classes.Contains("pirate");
        if (!active && !_hadTodoAppearance) return;
        _hadTodoAppearance = active;
        TodoPanel.Padding = active ? new Thickness(0) : new Thickness(16,14);
        TodoPanel.Margin = active ? new Thickness(0) : new Thickness(0,8,0,0);
        TodoPanel.BorderThickness = active ? new Thickness(0,1,0,0) : new Thickness(1);
        TodoContentGrid.Margin = active ? new Thickness(14,12,14,10) : new Thickness(0);
        ExpandedHeading.Orientation = active ? Orientation.Horizontal : Orientation.Vertical;
        ExpandedHeading.Margin = active ? new Thickness(12,0,44,0) : new Thickness(12,0,8,0);
        TodoHeading.FontSize = active ? 34 : 25;
        TodoCount.FontSize = active ? 13 : 12;
        TodoCount.Margin = active ? new Thickness(10,7,0,0) : new Thickness(0);
        TodoCount.VerticalAlignment = VerticalAlignment.Center;
        TodoOptionsRow.RowDefinitions = new RowDefinitions(active ? "Auto" : "Auto,Auto");
        Grid.SetRow(ShowCompletedToggle,active ? 0 : 1);
        Grid.SetColumn(ShowCompletedToggle,active ? 1 : 0);
        var sort = TodoOptionsRow.Children.OfType<StackPanel>().First();
        Grid.SetColumnSpan(sort,active ? 1 : 2);
        sort.Margin = active ? new Thickness(0) : new Thickness(0,0,0,10);
        sort.Children.OfType<TextBlock>().First().IsVisible = !active;
        TodoSortCombo.MinWidth = active ? 150 : 136;
        TodoSortCombo.FontSize = active ? 14 : 12;
        TodoSortCombo.VerticalContentAlignment=VerticalAlignment.Center;
        TodoSortCombo.FontWeight=active?FontWeight.SemiBold:FontWeight.Normal;
        TodoSortFrame.Pixel = active && TodoLook.Toolbar == "pixel-controls";
        TodoSortFrame.BorderThickness = active ? new Thickness(1) : new Thickness(0);
        TodoSortFrame.BorderBrush = TodoVisuals.Ink(TodoLook);
        TodoSortFrame.Background = active ? TodoVisuals.Card(TodoLook) : Brushes.Transparent;
        TodoSortCombo.ItemTemplate = active ? new FuncDataTemplate<string>((value,_) => new TextBlock { Text="Sort: "+value, FontFamily=TodoVisuals.BodyFont, FontSize=14, FontWeight=FontWeight.SemiBold }) : null;
        TodoSortCombo.SelectionBoxItemTemplate=TodoSortCombo.ItemTemplate;
        ShowCompletedToggle.FontSize=active?14:12;
        ShortcutHint.IsVisible = !active;
        TodoOptionsRow.Margin = active ? new Thickness(0,0,0,10) : new Thickness(0,0,0,8);
        AddTodoButton.FontSize = active ? 20 : 14;
        AddTodoButton.Height = active ? 42 : double.NaN;
        AddTodoButton.Margin = active ? new Thickness(0,12,0,0) : new Thickness(0,12,0,0);
        if (active)
        {
            UseLayoutRounding=true;
            FontFamily = TodoVisuals.BodyFont;
            Resources["AppFontFamily"] = TodoVisuals.BodyFont;
            TodoPanel.CornerRadius = new CornerRadius(0);
            TodoPanel.BorderBrush = TodoVisuals.Brass;
            TodoPanel.Background = TodoVisuals.Paper(TodoLook);
            TodoMap.Opacity = .65;
            TodoOptionsRow.Background = Brushes.Transparent;
            HeadingSurface.Background = TodoVisuals.Navy;
            HeadingSurface.CornerRadius = new CornerRadius(0);
            TodoHeading.Foreground = TodoCount.Foreground = TodoVisuals.Ivory;
            TodoHeading.FontFamily = TodoVisuals.HeadingFont;
            TodoMap.Choice = TodoLook.Background;
            TodoVisuals.Checkbox(ShowCompletedToggle,TodoLook,child:true);
            TodoVisuals.StyleButton(AddTodoButton,TodoLook,primary:true,pixel:TodoLook.Toolbar == "pixel-controls");
            AddTodoButton.Padding=new Thickness(12,4);
            EmptyMessage.Foreground = TodoVisuals.Muted(TodoLook);
            EmptyMessage.Margin = new Thickness(18,42,18,30);
            EmptyMessage.TextAlignment = TextAlignment.Center;
            EmptyMessage.FontSize = 14;
            TodoSortCombo.Background = Brushes.Transparent;
            TodoSortCombo.BorderThickness = new Thickness(0);
            TodoSortCombo.BorderBrush = TodoVisuals.Brass;
            TodoSortCombo.Foreground = TodoVisuals.Ink(TodoLook);
            ErrorMessage.FontSize = 12;
        }
        else
        {
            ClearValue(FontFamilyProperty);
            Resources.Remove("AppFontFamily");
            Resource(TodoPanel,Border.BackgroundProperty,"DialogSurfaceBrush");
            Resource(TodoPanel,Border.BorderBrushProperty,"BorderBrush");
            Resource(TodoPanel,Border.CornerRadiusProperty,"ThemePanelCornerRadius");
            Resource(HeadingSurface,Border.BackgroundProperty,"SurfaceRaisedBrush");
            Resource(HeadingSurface,Border.CornerRadiusProperty,"ThemePanelCornerRadius");
            Resource(TodoHeading,TextBlock.ForegroundProperty,"PrimaryTextBrush");
            Resource(TodoCount,TextBlock.ForegroundProperty,"SecondaryTextBrush");
            Resource(TodoHeading,TextBlock.FontFamilyProperty,"DisplayHeadingFontFamily");
            TodoVisuals.ReleaseContent(ShowCompletedToggle);
            TodoVisuals.ReleaseContent(AddTodoButton);
            ShowCompletedToggle.ClearValue(CheckBox.TemplateProperty);
            ShowCompletedToggle.ClearValue(CheckBox.MinHeightProperty);
            ShowCompletedToggle.ClearValue(CheckBox.PaddingProperty);
            ShowCompletedToggle.ClearValue(FontFamilyProperty);
            AddTodoButton.ClearValue(Button.PaddingProperty);
            Resource(ShowCompletedToggle,CheckBox.ForegroundProperty,"PrimaryTextBrush");
            foreach (var property in new AvaloniaProperty[] { Button.TemplateProperty,Button.BackgroundProperty,Button.ForegroundProperty,Button.BorderBrushProperty,Button.BorderThicknessProperty,Button.CornerRadiusProperty,FontFamilyProperty,Button.MinHeightProperty }) AddTodoButton.ClearValue(property);
            AddTodoButton.Styles.Clear();
            AddTodoButton.Classes.Remove("todoStyledButton");
            foreach(var property in new AvaloniaProperty[] { ComboBox.BackgroundProperty,ComboBox.BorderBrushProperty,ComboBox.BorderThicknessProperty,ComboBox.ForegroundProperty }) TodoSortCombo.ClearValue(property);
            Resource(EmptyMessage,TextBlock.ForegroundProperty,"SecondaryTextBrush");
            EmptyMessage.Margin = new Thickness(0,8,0,16); EmptyMessage.TextAlignment = TextAlignment.Left; EmptyMessage.ClearValue(TextBlock.FontSizeProperty);
            TodoOptionsRow.Background = Brushes.Transparent;
        }
        if (IsExpanded) Height = active ? 560 : 500;
        UpdateTodoAppearanceVisibility(IsExpanded && TodoPanel.IsVisible);
    }

    private static void Resource(Control control, AvaloniaProperty property, string key)
        => control.Bind(property, control.GetResourceObservable(key));

    private void UpdateTodoAppearanceVisibility(bool expanded)
    {
        TodoCollapse.IsVisible = expanded && IsNikaTodo;
        TodoShellTrim.IsVisible = expanded && IsNikaTodo;
        bool compactHeader = expanded && IsNikaTodo;
        TodoRoot.RowDefinitions = new RowDefinitions(compactHeader ? "64,*" : "72,*");
        OverlayHeader.ColumnDefinitions = new ColumnDefinitions(compactHeader ? "64,*" : "72,*");
        LauncherButton.Width = LauncherButton.Height = LauncherArtworkHost.Width = LauncherArtworkHost.Height = compactHeader ? 64 : 72;
        TodoOutline.BorderThickness = expanded && IsNikaTodo ? new Thickness(1) : new Thickness(0);
        TodoOutline.BorderBrush = TodoVisuals.Gold;
        TodoOutline.Background = expanded && IsNikaTodo ? TodoVisuals.Navy : Brushes.Transparent;
        TodoEmblem.Choice = IsNikaTodo && expanded ? TodoLook.HeaderEmblem : TodoLook.EffectiveLauncherEmblem;
    }

    private void StyleTodoRow(Grid container, HoldToCompleteCheckBox row, TextBlock text, Button badge, NoteEntry entry, TodoDueState due)
    {
        if (!IsNikaTodo) return;
        var p = TodoLook;
        TodoVisuals.RowSurface(container,p);
        TodoVisuals.Checkbox(row,p,p.Rows == "pixel-voyage");
        row.Padding = new Thickness(8,7,5,7);
        text.MaxWidth = double.PositiveInfinity;
        text.FontFamily = TodoVisuals.StrongFont; text.FontSize = 17;
        text.FontWeight = FontWeight.ExtraBold; text.Foreground = TodoVisuals.Ink(p);
        text.Margin = new Thickness(0); text.VerticalAlignment = VerticalAlignment.Center;
        row.Content = null;
        container.Children.Remove(badge);
        TodoVisuals.PriorityBadge(badge,_organization?.GetPriority(entry) ?? 3,p);
        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        content.Children.Add(badge); Grid.SetColumn(text,1); content.Children.Add(text); row.Content = content;
        foreach (var label in container.Children.OfType<TextBlock>())
        {
            label.Foreground = due == TodoDueState.Overdue ? Brush.Parse(p.Background == "midnight-chart" ? "#FFAAA0" : "#9D292D") : TodoVisuals.Muted(p);
            label.Margin = new Thickness(42,0,4,6);
        }
        ApplyTodoFeedback(container,row,p,due);
        var toggle = container.Children.OfType<Button>().FirstOrDefault(b => b.Name == "ToggleTodoDetails");
        if (toggle != null)
        {
            toggle.Foreground = TodoVisuals.Muted(p); toggle.Margin = new Thickness(78,0,0,5); toggle.FontFamily=TodoVisuals.BodyFont; toggle.FontSize=12; toggle.MinHeight=20; toggle.Padding=new Thickness(0);
            toggle.Classes.Add("todoLauncher");
            bool expanded=DetailsExpanded(entry);
            toggle.Content=toggle.Content?.ToString()?[2..];
            var glyph=new Avalonia.Controls.Shapes.Path {Data=Geometry.Parse(expanded?"M 1,2 L 7,8 L 13,2":"M 4,1 L 10,7 L 4,13"),Width=14,Height=14,Stroke=TodoVisuals.Ink(p),StrokeThickness=2,VerticalAlignment=VerticalAlignment.Center};
            var arrow=new Button {Name="TodoDetailsChevron",Content=glyph,Classes={"todoLauncher"},Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(0),Width=20,Height=44,VerticalAlignment=VerticalAlignment.Top};
            Avalonia.Automation.AutomationProperties.SetName(arrow,(expanded?"Collapse":"Expand")+" task details");
            arrow.Click+=(_,e)=>{e.Handled=true;toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};Grid.SetColumn(arrow,3);container.Children.Add(arrow);
        }
        var details = container.Children.OfType<StackPanel>().FirstOrDefault(s => s.Name == "TodoDetailsPanel");
        if (details == null) return;
        foreach (var child in details.GetLogicalDescendants().OfType<HoldToCompleteCheckBox>()) {TodoVisuals.Checkbox(child,p,child:true);child.MinHeight=22;}
        foreach (var label in details.GetLogicalDescendants().OfType<TextBlock>()) { label.FontFamily = TodoVisuals.BodyFont; label.FontSize=14; label.Margin=new Thickness(0); label.MaxWidth=double.PositiveInfinity; label.Foreground=TodoVisuals.Ink(p); }
        foreach (var note in details.Children.OfType<Border>())
        {
            note.BorderBrush=TodoVisuals.Brass;var noteContent=note.Child;note.Child=null;
            var noteBody=new StackPanel {Spacing=5};noteBody.Children.Add(new TextBlock {Text="NOTE",FontFamily=TodoVisuals.BodyFont,FontSize=10,FontWeight=FontWeight.Bold,LetterSpacing=1,Foreground=TodoVisuals.Muted(p)});
            if(noteContent!=null)noteBody.Children.Add(noteContent);note.Child=noteBody;
        }
        container.Children.Remove(details);
        details.Margin = new Thickness(0); details.Spacing=4;
        bool foldout = p.Details == "foldout-card";
        var frame = new TodoFrame { Name = "TodoDetailsFrame", Child = details, Pixel = foldout && _settings.TodoAppearance.IsPixel,
            Margin = new Thickness(foldout ? 10 : 42,3,8,10), Padding = new Thickness(12,4,8,6),
            BorderBrush = TodoVisuals.Brass, BorderThickness = foldout ? new Thickness(1) : new Thickness(1.5,0,0,0),
            Background = foldout ? TodoVisuals.Card(p) : Brushes.Transparent, CornerRadius = new CornerRadius(4) };
        Grid.SetColumn(frame,2); Grid.SetRow(frame,4); Grid.SetColumnSpan(frame,2); container.Children.Add(frame);
    }

    private void StyleTodoMenu(ContextMenu menu)
    {
        if (!IsNikaTodo) return;
        TodoVisuals.Palette(menu,TodoLook);
        menu.Background=TodoVisuals.Paper(TodoLook); menu.Foreground=TodoVisuals.Ink(TodoLook);
        menu.BorderBrush=TodoVisuals.Brass; menu.BorderThickness=new Thickness(1);
        menu.FontFamily=TodoVisuals.BodyFont; menu.FontSize=13;
        menu.CornerRadius=new CornerRadius(_settings.TodoAppearance.IsPixel?0:5);
        menu.Resources["MenuBackgroundBrush"]=TodoVisuals.Paper(TodoLook);
        menu.Resources["MenuForegroundBrush"]=TodoVisuals.Ink(TodoLook);
        foreach(var item in menu.Items.OfType<MenuItem>()) StyleItem(item);
        void StyleItem(MenuItem item)
        {
            string title=item.Header?.ToString()??"";
            if(title=="Delete")item.Foreground=Brush.Parse(TodoLook.Background=="midnight-chart"?"#FFAAA0":"#9D292D");
            else item.Foreground=TodoVisuals.Ink(TodoLook);
            item.FontFamily=TodoVisuals.BodyFont;item.FontSize=14;
            if(item.Icon==null)
            {
                string symbol=title switch {"Edit task" or "Edit note" or "Edit subtask"=>"✎","Add"=>"＋","Note"=>"▤","Subtask"=>"☷","Link" or "Relink"=>"↗","Delete"=>"×",_=>""};
                if(symbol.Length>0)item.Icon=new TextBlock {Text=symbol,FontSize=22,Foreground=item.Foreground,Width=24,TextAlignment=TextAlignment.Center};
            }
            foreach(var child in item.Items.OfType<MenuItem>())StyleItem(child);
        }
    }

    private void UpdateTodoEmptyState(bool configured,int total,int visible)
    {
        TodoEmptyStateHost.IsVisible=IsNikaTodo&&visible==0;
        if(!TodoEmptyStateHost.IsVisible){TodoEmptyStateHost.Content=null;return;}
        bool complete=configured&&total>0;
        var stack=new StackPanel {Spacing=12,MaxWidth=290,Margin=new Thickness(10,16)};
        stack.Children.Add(new TodoStateGlyph {Complete=complete,Pixel=_settings.TodoAppearance.IsPixel,Ink=TodoVisuals.Ink(TodoLook),Width=78,Height=78,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,0,0,8)});
        stack.Children.Add(new TextBlock {Text=!configured?"Your task journal":complete?"All done.":"No todos yet.",FontFamily=TodoVisuals.HeadingFont,FontSize=28,Foreground=TodoVisuals.Ink(TodoLook),TextAlignment=TextAlignment.Center});
        stack.Children.Add(new TextBlock {Text=!configured?"Choose a notes folder in Settings to see your todos here.":complete?"Turn on Show completed\nto see finished tasks.":"Add your first task below.",FontFamily=TodoVisuals.BodyFont,FontSize=15,Foreground=TodoVisuals.Ink(TodoLook),TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center});
        TodoEmptyStateHost.Content=stack;
    }

    private void ApplyTodoFeedback(Grid container,HoldToCompleteCheckBox checkbox,TodoAppearanceProfile p,TodoDueState due)
    {
        bool pixel=_settings.TodoAppearance.IsPixel;
        foreach(var bar in container.Children.OfType<ProgressBar>().ToArray())container.Children.Remove(bar);
        var hold=new Grid {Name="TodoHoldFeedback",ColumnDefinitions=new ColumnDefinitions("Auto,8,*"),Margin=new Thickness(42,0,8,5)};
        hold.Bind(IsVisibleProperty,new Binding(nameof(HoldToCompleteCheckBox.HoldProgress)){Source=checkbox,Converter=new FuncValueConverter<double,bool>(value=>value>0)});
        hold.Children.Add(new TextBlock {Text="Holding…",FontSize=11,Foreground=TodoVisuals.Muted(p)});
        var meter=new TodoHoldMeter {Pixel=pixel,Ink=TodoVisuals.Ink(p),Height=7,VerticalAlignment=VerticalAlignment.Center};
        meter.Bind(TodoHoldMeter.ValueProperty,checkbox.GetObservable(HoldToCompleteCheckBox.HoldProgressProperty));Grid.SetColumn(meter,2);hold.Children.Add(meter);
        Grid.SetColumn(hold,2);Grid.SetRow(hold,1);container.Children.Add(hold);
        var states=new StackPanel {Name="TodoStateLabels",Spacing=4,Margin=new Thickness(42,0,6,6)};
        var progress=container.Children.OfType<Border>().FirstOrDefault(b=>b.Name=="InProgressBadge");
        if(progress!=null)
        {
            container.Children.Remove(progress);progress.Width=progress.Height=double.NaN;progress.Margin=new Thickness(0);progress.HorizontalAlignment=HorizontalAlignment.Left;
            var label=new TextBlock {Text="▷  In progress",FontSize=11,Foreground=Brush.Parse(p.Background=="midnight-chart"?"#8AD2DE":"#185F70")};
            progress.Child=pixel?new TodoFrame {Child=label,Pixel=true,Padding=new Thickness(6,2),BorderBrush=label.Foreground,BorderThickness=new Thickness(1),Background=Brush.Parse(p.Background=="midnight-chart"?"#1B4153":"#C6E2E5")}:label;
            states.Children.Add(progress);
        }
        var date=container.Children.OfType<TextBlock>().FirstOrDefault(t=>t.Name=="TodoDueLabel");
        if(date!=null)
        {
            container.Children.Remove(date);date.Margin=new Thickness(0);date.Text=(due==TodoDueState.Overdue?"△  ":"◷  ")+date.Text;
            date.Foreground=Brush.Parse(due==TodoDueState.Overdue?(p.Background=="midnight-chart"?"#FFAAA0":"#9D292D"):due==TodoDueState.Soon?(p.Background=="midnight-chart"?"#F5CD7E":"#86520C"):(p.Background=="midnight-chart"?"#A7CDCF":"#3D6065"));
            if(pixel)states.Children.Add(new TodoFrame {Child=date,Pixel=true,Padding=new Thickness(6,3),BorderBrush=date.Foreground,BorderThickness=new Thickness(1),Background=TodoVisuals.Card(p),HorizontalAlignment=HorizontalAlignment.Left});
            else states.Children.Add(date);
        }
        if(states.Children.Count>0){Grid.SetColumn(states,2);Grid.SetRow(states,2);container.Children.Add(states);}
    }
}
