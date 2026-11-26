using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class SettingsWindow
{
    private readonly StopwatchAppearancePreview _stopwatchPreview=new();
    private TextBlock? _stopwatchSaveStatus;
    private CheckBox? _stopwatchArtworkOpaque;
    private Button? _stopwatchActivateButton;
    private Slider? _stopwatchTextSizeSlider;
    private TextBlock? _stopwatchTextSizeValue;
    private bool _loadingStopwatch;
    private StopwatchStyleGallery? _styleGallery;
    private Button? _galleryOpener;
    private bool _usesStyleGalleries;
    private Thickness _previewStatePadding;

    private void LoadStopwatchAppearance()
    {
        if(StopwatchAppearancePanel==null)return;
        _loadingStopwatch=true;
        try
        {
            _settings.StopwatchAppearance.Normalize();
            CloseStyleGallery();
            _usesStyleGalleries = AppThemeCatalog.Normalize(_settings.ThemeMode) == AppThemeCatalog.Nika;
            var p=_settings.StopwatchAppearance;
            StopwatchAppearancePanel.Children.Clear();
            StopwatchAppearancePanel.Children.Add(new TextBlock{Text="Stopwatch appearance",Classes={"heading"}});
            StopwatchAppearancePanel.Children.Add(new TextBlock{Text="One shared style for all your clocks.",Classes={"subheading"},TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)});
            var activate=new Button{Name="UseNikaStopwatchOverlay",Content="Use Nika overlay",Classes={"primary"},Margin=new Thickness(0,0,0,12),IsVisible=OverlayThemeCatalog.Resolve(_settings.OverlayTheme,_settings.ThemeMode)!=OverlayThemeCatalog.Nika};
            _stopwatchActivateButton=activate;
            activate.Click+=(_,_)=>
            {
                _settings.OverlayTheme=OverlayThemeCatalog.Nika;
                bool loading=_loading;_loading=true;try{OverlayThemeCombo.SelectedItem=OverlayThemeCatalog.Nika;}finally{_loading=loading;}
                activate.IsVisible=false;UpdateDependentControlStates();CommitStopwatchAppearance();
            };
            StopwatchAppearancePanel.Children.Add(activate);
            var style=new StackPanel{Spacing=10,Margin=new Thickness(0,12,0,0)};
            var transparency=new StackPanel{Spacing=10,Margin=new Thickness(0,12,0,0)};
            if (_usesStyleGalleries)
            {
                AddGalleryChoice(style, StopwatchGalleryLayer.Background);
                AddGalleryChoice(style, StopwatchGalleryLayer.Frame);
            }
            else
            {
                AddStopwatchChoice(style,"Background",StopwatchAppearanceCatalog.Backgrounds,p.Background,v=>p.Background=v);
                AddStopwatchChoice(style,"Frame",StopwatchAppearanceCatalog.Frames,p.Frame,v=>p.Frame=v);
            }
            AddStopwatchChoice(style,"Typography",StopwatchAppearanceCatalog.Typography,p.Typography,v=>p.Typography=v);
            AddStopwatchChoice(style,"Artwork",StopwatchAppearanceCatalog.Artworks,p.Artwork,v=>p.Artwork=v);
            AddStopwatchChoice(style,"HoverControls",StopwatchAppearanceCatalog.HoverControls,p.HoverControls,v=>p.HoverControls=v,"Hover controls");
            AddStopwatchSlider(style,"StopwatchTextSize","Text size",_settings.TextSize,16,120,value=>
            {
                _settings.TextSize=value;bool loading=_loading;_loading=true;try{TextSizeSlider.Value=value;UpdateValueLabels();}finally{_loading=loading;}
            }," px");
            style.Children.Add(new TextBlock{Name="StopwatchStateSummary",Text="Running / paused marker beside the project name. ACTIVE tab marks the selected clock.",FontSize=12,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)});
            AddStopwatchSlider(transparency,"StopwatchBackgroundOpacity","Background opacity",p.BackgroundOpacity,0,100,v=>p.BackgroundOpacity=v);
            AddStopwatchSlider(transparency,"StopwatchUnselectedOpacity","Unselected clocks",p.UnselectedOpacity,10,100,v=>p.UnselectedOpacity=v);
            transparency.Children.Add(new TextBlock{Text="Selected clocks stay at 100%. The unselected amount applies to the whole clock and its controls.",FontSize=12,TextWrapping=TextWrapping.Wrap});
            transparency.Children.Add(new TextBlock{Text="Keep opaque",FontSize=14,FontWeight=FontWeight.SemiBold,Margin=new Thickness(0,10,0,0)});
            transparency.Children.Add(new TextBlock{Text="Checked parts stay opaque. Unchecked parts follow background opacity, before the unselected amount is applied.",FontSize=12,TextWrapping=TextWrapping.Wrap});
            foreach(var (flag,label) in new[]{(StopwatchOpaqueLayers.Frame,"Frame"),(StopwatchOpaqueLayers.TimeText,"Time text"),(StopwatchOpaqueLayers.ProjectName,"Project name"),(StopwatchOpaqueLayers.StateMarkers,"State markers"),(StopwatchOpaqueLayers.HoverControls,"Hover controls"),(StopwatchOpaqueLayers.Artwork,"Artwork")})
            {
                var check=new CheckBox{Name="StopwatchOpaque"+flag,Content=label,IsChecked=p.OpaqueLayers.HasFlag(flag)};
                AutomationProperties.SetName(check,"Keep "+label.ToLowerInvariant()+" opaque");
                if(flag==StopwatchOpaqueLayers.Artwork){_stopwatchArtworkOpaque=check;check.IsEnabled=p.Artwork!="clean";}
                check.IsCheckedChanged+=(_,_)=>{if(_loadingStopwatch||_loading)return;if(check.IsChecked==true)p.OpaqueLayers|=flag;else p.OpaqueLayers&=~flag;CommitStopwatchAppearance();};
                transparency.Children.Add(check);
            }
            var tabs=new TabControl{Name="StopwatchAppearanceTabs",ItemsSource=new[]{new TabItem{Header="Style",Content=style},new TabItem{Header="Transparency",Content=transparency}}};
            StopwatchAppearancePanel.Children.Add(tabs);
            _stopwatchSaveStatus=new TextBlock{Name="StopwatchSaveStatus",Text="Changes apply immediately.",FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,14,0,0)};
            StopwatchAppearancePanel.Children.Add(_stopwatchSaveStatus);
            StopwatchPreviewHost.Content=_stopwatchPreview;RefreshStopwatchAppearancePreview();
        }
        finally{_loadingStopwatch=false;}
    }

    private void AddGalleryChoice(StackPanel style, StopwatchGalleryLayer layer)
    {
        style.Children.Add(SettingsLabel(layer.Name, 12));
        var button = new Button { Name = $"BrowseStopwatch{layer.Name}s", Height = 40, Padding = new Thickness(6, 2, 12, 2),
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = Brush.Parse("#F8EACA"), BorderBrush = Brush.Parse("#C4A472"), CornerRadius = new CornerRadius(4) };
        UpdateGalleryButton(button, layer);
        button.Click += (_, _) => OpenStyleGallery(button, layer);
        style.Children.Add(button);
    }

    private void UpdateGalleryButton(Button button, StopwatchGalleryLayer layer)
    {
        var option = layer.Options.First(o => o.Id == layer.Selected(_settings.StopwatchAppearance));
        var sample = StopwatchAppearancePreview.Thumbnail(layer.Name, option.Id);
        sample.Width = 94;
        sample.Height = 32;
        var label = SettingsLabel(option.Label,14,SettingsInk);
        label.TextWrapping = TextWrapping.NoWrap;
        label.TextTrimming = TextTrimming.CharacterEllipsis;
        label.FontWeight = FontWeight.SemiBold;
        var browse = SettingsLabel($"Browse {layer.Label}s  ›",13,Brush.Parse("#216B63"));
        var row = new Grid { ColumnDefinitions = new("Auto,8,*,8,Auto"), Children = { sample, label, browse } };
        Grid.SetColumn(label,2);
        Grid.SetColumn(browse,4);
        button.Content = row;
        AutomationProperties.SetName(button, $"Browse {layer.Label}s, current {layer.Label} {option.Label}");
    }

    private void OpenStyleGallery(Button opener, StopwatchGalleryLayer layer)
    {
        if (_styleGallery != null) return;
        _galleryOpener = opener;
        _previewStatePadding = StopwatchPreviewState.Padding;
        _styleGallery = new StopwatchStyleGallery(_settings, layer, id =>
        {
            if (layer.Selected(_settings.StopwatchAppearance) == id) return;
            layer.Select(_settings.StopwatchAppearance, id);
            UpdateGalleryButton(opener, layer);
            CommitStopwatchAppearance();
        }, () => CloseStyleGallery(restoreFocus: true), compact =>
        {
            StopwatchPreviewFooter.IsVisible = !compact;
            StopwatchPreviewState.Padding = compact ? new Thickness(4,2) : _previewStatePadding;
        }) { Name = $"Stopwatch{layer.Name}Gallery", Margin = new Thickness(18,13,12,4) };
        Grid.SetColumn(_styleGallery,Grid.GetColumn(SettingsScrollViewer));
        SettingsScrollViewer.IsVisible = false;
        SettingsColumns.Children.Add(_styleGallery);
        AddHandler(KeyDownEvent, StyleGalleryKeyDown, RoutingStrategies.Tunnel);
        _styleGallery.FocusSelection();
    }

    private void StyleGalleryKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || StopwatchPreviewState.IsDropDownOpen || StopwatchPreviewBackdrop.IsDropDownOpen) return;
        CloseStyleGallery(restoreFocus: true);
        e.Handled = true;
    }

    private void CloseStyleGallery(bool restoreFocus = false)
    {
        if (_styleGallery == null) return;
        RemoveHandler(KeyDownEvent, StyleGalleryKeyDown);
        SettingsColumns.Children.Remove(_styleGallery);
        _styleGallery = null;
        SettingsScrollViewer.IsVisible = StopwatchPreviewFooter.IsVisible = true;
        StopwatchPreviewState.Padding = _previewStatePadding;
        if (restoreFocus) _galleryOpener?.Focus(NavigationMethod.Tab);
        _galleryOpener = null;
    }

    private void AddStopwatchChoice(StackPanel parent,string kind,IReadOnlyList<StopwatchAppearanceOption> options,string selected,Action<string> set,string? label=null)
    {
        parent.Children.Add(new TextBlock{Text=label??kind,FontSize=12,FontWeight=FontWeight.SemiBold});
        var combo=new ComboBox{Name="Stopwatch"+kind+"Choice",ItemsSource=options,SelectedItem=options.First(x=>x.Id==selected),HorizontalAlignment=HorizontalAlignment.Stretch,MinHeight=40};
        combo.ItemTemplate=new FuncDataTemplate<StopwatchAppearanceOption>((option,_)=>
        {
            if(option==null)return null;
            var row=new Grid{ColumnDefinitions=new ColumnDefinitions("92,*"),Margin=new Thickness(0,2)};
            row.Children.Add(StopwatchAppearancePreview.Thumbnail(kind,option.Id));
            var text=new TextBlock{Text=option.Label,FontSize=12,VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap};Grid.SetColumn(text,1);row.Children.Add(text);return row;
        });
        combo.SelectionBoxItemTemplate=combo.ItemTemplate;AutomationProperties.SetName(combo,label??kind);
        combo.SelectionChanged+=(_,_)=>{if(!_loadingStopwatch&&!_loading&&combo.SelectedItem is StopwatchAppearanceOption option){set(option.Id);CommitStopwatchAppearance();}};
        parent.Children.Add(combo);
    }

    private void AddStopwatchSlider(StackPanel parent,string name,string title,double value,double min,double max,Action<double> set,string suffix="%")
    {
        var row=new Grid{ColumnDefinitions=new ColumnDefinitions("*,Auto"),Margin=new Thickness(0,6,0,0)};
        row.Children.Add(new TextBlock{Text=title,FontSize=12,FontWeight=FontWeight.SemiBold});
        var label=new TextBlock{Text=$"{value:0}{suffix}",FontSize=12};Grid.SetColumn(label,1);row.Children.Add(label);parent.Children.Add(row);
        var slider=new Slider{Name=name,Minimum=min,Maximum=max,Value=value,TickFrequency=1,IsSnapToTickEnabled=true};AutomationProperties.SetName(slider,title);
        if(name=="StopwatchTextSize"){_stopwatchTextSizeSlider=slider;_stopwatchTextSizeValue=label;}
        slider.PropertyChanged+=(_,e)=>{if(e.Property==Slider.ValueProperty&&!_loadingStopwatch&&!_loading){set(slider.Value);label.Text=$"{slider.Value:0}{suffix}";CommitStopwatchAppearance();}};
        parent.Children.Add(slider);
    }

    private void CommitStopwatchAppearance(bool rebuildControls=false)
    {
        if(_loading||_loadingStopwatch)return;
        _settings.StopwatchAppearance.Normalize();
        if(rebuildControls)LoadStopwatchAppearance();else RefreshStopwatchAppearancePreview();
        SettingsChanged?.Invoke(SettingsChangeKind.OverlayAppearance);
    }

    private void RefreshStopwatchAppearancePreview()
    {
        SyncStopwatchTextSizeControl();
        _stopwatchPreview.Apply(_settings);
        if(_stopwatchArtworkOpaque!=null)
        {
            bool loading=_loadingStopwatch;_loadingStopwatch=true;
            try{_stopwatchArtworkOpaque.IsEnabled=_settings.StopwatchAppearance.Artwork!="clean";_stopwatchArtworkOpaque.IsChecked=_settings.StopwatchAppearance.OpaqueLayers.HasFlag(StopwatchOpaqueLayers.Artwork);}
            finally{_loadingStopwatch=loading;}
        }
    }

    private void SyncStopwatchTextSizeControl()
    {
        if(_stopwatchTextSizeSlider==null||_stopwatchTextSizeValue==null)return;
        bool loading=_loadingStopwatch;_loadingStopwatch=true;
        try
        {
            _stopwatchTextSizeSlider.Value=_settings.TextSize;
            _stopwatchTextSizeValue.Text=$"{_settings.TextSize:0} px";
        }
        finally{_loadingStopwatch=loading;}
    }

    private void StopwatchPreviewState_SelectionChanged(object? sender,SelectionChangedEventArgs e)
    {
        if(StopwatchPreviewState==null||StopwatchPreviewBackdrop==null)return;
        _stopwatchPreview.SetPreviewState(StopwatchPreviewState.SelectedIndex!=1,StopwatchPreviewBackdrop.SelectedIndex!=1);
    }
}
