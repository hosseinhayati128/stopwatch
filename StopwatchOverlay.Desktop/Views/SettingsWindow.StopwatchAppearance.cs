using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Documents;
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
    private StopwatchArtworkPicker? _artworkPicker;
    private Button? _stopwatchActivateButton;
    private StopwatchTextSizeEditor? _stopwatchTextSize;
    private bool _loadingStopwatch;
    private StopwatchStyleGallery? _styleGallery;
    private Button? _galleryOpener;
    private bool _usesStyleGalleries;
    private bool? _activeOverlayVisible;

    internal void SetActiveOverlayVisibility(bool? visible)
    {
        if (_activeOverlayVisible == visible) return;
        _activeOverlayVisible = visible;
        DesktopOverlayAction.IsEnabled = visible.HasValue;
        DesktopOverlayAction.Content = visible == true ? "Hide active overlay" : "Show active overlay";
        DesktopOverlayStatus.Text = visible switch { true => "Visible", false => "Hidden", null => "No active timer" };
        DesktopOverlayStatus.Classes.Set("shown", visible == true);
    }

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
            _artworkPicker = null;
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
                AddGalleryChoice(style, StopwatchGalleryLayer.Typography);
                style.Children.Add(new TextBlock { Text = "Artwork", FontSize = 12, FontWeight = FontWeight.SemiBold });
                _artworkPicker = new StopwatchArtworkPicker(_settings, id =>
                {
                    if (_loading || _loadingStopwatch || p.Artwork == id) return;
                    p.Artwork = id;
                    CommitStopwatchAppearance();
                });
                style.Children.Add(_artworkPicker);
                AddGalleryChoice(style, StopwatchGalleryLayer.Toolbar);
            }
            else
            {
                AddStopwatchChoice(style,"Background",StopwatchAppearanceCatalog.Backgrounds,p.Background,v=>p.Background=v);
                AddStopwatchChoice(style,"Frame",StopwatchAppearanceCatalog.Frames,p.Frame,v=>p.Frame=v);
                AddStopwatchChoice(style,"Typography",StopwatchAppearanceCatalog.Typography,p.Typography,v=>p.Typography=v);
                AddStopwatchChoice(style,"Artwork",StopwatchAppearanceCatalog.Artworks,p.Artwork,v=>p.Artwork=v);
                AddStopwatchChoice(style,"HoverControls",StopwatchAppearanceCatalog.HoverControls,p.HoverControls,v=>p.HoverControls=v,"Hover controls");
            }
            _stopwatchTextSize = new StopwatchTextSizeEditor(_settings.TextSize, _usesStyleGalleries);
            _stopwatchTextSize.ValueChanged += StopwatchTextSizeChanged;
            style.Children.Add(_stopwatchTextSize);
            style.Children.Add(new TextBlock{Name="StopwatchStateSummary",Text="Running / paused marker beside the project name. ACTIVE tab marks the selected clock.",FontSize=12,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)});
            BuildStopwatchTransparency(transparency, p);
            var tabs=new TabControl{Name="StopwatchAppearanceTabs",ItemsSource=new[]{new TabItem{Header="Style",Content=style},new TabItem{Header="Transparency",Content=transparency}}};
            StopwatchAppearancePanel.Children.Add(tabs);
            _stopwatchSaveStatus=new TextBlock{Name="StopwatchSaveStatus",Text="Changes apply immediately.",FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,14,0,0)};
            StopwatchAppearancePanel.Children.Add(_stopwatchSaveStatus);
            StopwatchPreviewHost.Content=_stopwatchPreview;RefreshStopwatchAppearancePreview();
        }
        finally{_loadingStopwatch=false;}
    }

    private void BuildStopwatchTransparency(StackPanel tab, StopwatchAppearanceSettings profile)
    {
        var background = new StackPanel { Spacing = 3 };
        AddOpacitySlider(background, "StopwatchBackgroundOpacity", "Background opacity", profile.BackgroundOpacity,
            0, 100, value => profile.BackgroundOpacity = value);
        background.Children.Add(new Border { Height = 1, Background = SettingsRule, Margin = new(0, 2, 0, 4) });
        background.Children.Add(new TextBlock { Text = "Keep opaque", FontSize = 14, FontWeight = FontWeight.SemiBold });
        background.Children.Add(new TextBlock { Text = "Checked parts ignore background opacity.", FontSize = 12, TextWrapping = TextWrapping.Wrap });

        var layers = new UniformGrid { Columns = 3 };
        layers.SizeChanged += (_, e) => layers.Columns = Math.Clamp((int)(e.NewSize.Width / 116), 1, 3);
        foreach (var (flag, label) in new[] {
            (StopwatchOpaqueLayers.Frame, "Frame"), (StopwatchOpaqueLayers.TimeText, "Time text"),
            (StopwatchOpaqueLayers.ProjectName, "Project name"), (StopwatchOpaqueLayers.StateMarkers, "State markers"),
            (StopwatchOpaqueLayers.HoverControls, "Hover controls"), (StopwatchOpaqueLayers.Artwork, "Artwork") })
        {
            var check = new CheckBox { Name = "StopwatchOpaque" + flag, Content = label,
                IsChecked = profile.OpaqueLayers.HasFlag(flag), FontSize = 13, MinHeight = 29 };
            AutomationProperties.SetName(check, "Keep " + label.ToLowerInvariant() + " opaque");
            if (flag == StopwatchOpaqueLayers.Artwork)
            {
                _stopwatchArtworkOpaque = check;
                check.IsEnabled = profile.Artwork != "clean";
            }
            check.IsCheckedChanged += (_, _) =>
            {
                if (_loadingStopwatch || _loading) return;
                if (check.IsChecked == true) profile.OpaqueLayers |= flag;
                else profile.OpaqueLayers &= ~flag;
                CommitStopwatchAppearance();
            };
            layers.Children.Add(check);
        }
        background.Children.Add(layers);

        var inactive = new StackPanel { Spacing = 3 };
        AddOpacitySlider(inactive, "StopwatchUnselectedOpacity", "Inactive clock opacity", profile.UnselectedOpacity,
            10, 100, value => profile.UnselectedOpacity = value);
        inactive.Children.Add(new TextBlock { Text = "Dims the entire inactive clock, including its controls.",
            FontSize = 12, TextWrapping = TextWrapping.Wrap });
        foreach (var content in new[] { background, inactive })
        {
            var card = SettingsCard(content);
            card.Padding = new(15, 10);
            if (_usesStyleGalleries)
            {
                card.Background = Brush.Parse("#F7E8C6");
                card.BorderBrush = Brush.Parse("#CBB084");
                TextElement.SetFontFamily(content, ClockPickerPresentation.Body);
                TextElement.SetForeground(content, SettingsInk);
            }
            tab.Children.Add(card);
        }
    }

    private void AddGalleryChoice(StackPanel style, StopwatchGalleryLayer layer)
    {
        style.Children.Add(SettingsLabel(layer.DisplayName, 12));
        var button = new Button { Name = layer.ButtonName, Height = 40, Padding = new Thickness(6, 2, 12, 2),
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = Brush.Parse("#F8EACA"), BorderBrush = Brush.Parse("#C4A472"), CornerRadius = new CornerRadius(4) };
        UpdateGalleryButton(button, layer);
        button.Click += (_, _) => OpenStyleGallery(button, layer);
        style.Children.Add(button);
    }

    private void UpdateGalleryButton(Button button, StopwatchGalleryLayer layer)
    {
        var option = layer.Options.First(o => o.Id == layer.Selected(_settings.StopwatchAppearance));
        var sample = layer == StopwatchGalleryLayer.Toolbar
            ? StopwatchAppearancePreview.CreateToolbarThumbnail(_settings.StopwatchAppearance)
            : StopwatchAppearancePreview.Thumbnail(layer.Name, option.Id);
        sample.Width = 94;
        sample.Height = 32;
        var label = SettingsLabel(option.Label,14,SettingsInk);
        label.TextWrapping = TextWrapping.NoWrap;
        label.TextTrimming = TextTrimming.CharacterEllipsis;
        label.FontWeight = FontWeight.SemiBold;
        var browse = SettingsLabel($"{layer.BrowseLabel}  ›",13,Brush.Parse("#216B63"));
        var row = new Grid { ColumnDefinitions = new("Auto,8,*,8,Auto"), Children = { sample, label, browse } };
        Grid.SetColumn(label,2);
        Grid.SetColumn(browse,4);
        button.Content = row;
        AutomationProperties.SetName(button, $"{layer.BrowseLabel}, current {layer.Label} {option.Label}");
    }

    private void OpenStyleGallery(Button opener, StopwatchGalleryLayer layer)
    {
        if (_styleGallery != null) return;
        _galleryOpener = opener;
        _stopwatchPreview.SetTypographySample(layer == StopwatchGalleryLayer.Typography);
        _styleGallery = new StopwatchStyleGallery(_settings, layer, id =>
        {
            if (layer.Selected(_settings.StopwatchAppearance) == id) return;
            layer.Select(_settings.StopwatchAppearance, id);
            UpdateGalleryButton(opener, layer);
            CommitStopwatchAppearance();
        }, () => CloseStyleGallery(restoreFocus: true), compact => StopwatchPreviewFooter.IsVisible = !compact) { Name = $"Stopwatch{layer.Name}Gallery", Margin = new Thickness(18,13,12,4) };
        Grid.SetColumn(_styleGallery,Grid.GetColumn(SettingsScrollViewer));
        SettingsScrollViewer.IsVisible = false;
        SettingsColumns.Children.Add(_styleGallery);
        AddHandler(KeyDownEvent, StyleGalleryKeyDown, RoutingStrategies.Tunnel);
        _styleGallery.FocusSelection();
    }

    private void StyleGalleryKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        CloseStyleGallery(restoreFocus: true);
        e.Handled = true;
    }

    private void CloseStyleGallery(bool restoreFocus = false)
    {
        if (_styleGallery == null) return;
        RemoveHandler(KeyDownEvent, StyleGalleryKeyDown);
        SettingsColumns.Children.Remove(_styleGallery);
        _styleGallery = null;
        _stopwatchPreview.SetTypographySample(false);
        SettingsScrollViewer.IsVisible = StopwatchPreviewFooter.IsVisible = true;
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

    private void AddOpacitySlider(StackPanel parent,string name,string title,double value,double min,double max,Action<double> set)
    {
        var row=new Grid{ColumnDefinitions=new ColumnDefinitions("*,Auto")};
        row.Children.Add(new TextBlock{Text=title,FontSize=16,FontWeight=FontWeight.SemiBold,VerticalAlignment=VerticalAlignment.Center});
        var label=new TextBlock{Text=$"{value:0}%",FontSize=15,FontWeight=FontWeight.SemiBold,Foreground=Brush.Parse("#F8EACA")};
        var valueDisplay = new Border { Child = label, Background = Brush.Parse("#173B49"), CornerRadius = new(4), Padding = new(8,2) };
        Grid.SetColumn(valueDisplay,1);row.Children.Add(valueDisplay);parent.Children.Add(row);
        var slider=new Slider{Name=name,Minimum=min,Maximum=max,Value=value,TickFrequency=1,IsSnapToTickEnabled=true};AutomationProperties.SetName(slider,title);
        slider.PropertyChanged+=(_,e)=>{if(e.Property==Slider.ValueProperty&&!_loadingStopwatch&&!_loading){set(slider.Value);label.Text=$"{slider.Value:0}%";CommitStopwatchAppearance();}};
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
        _artworkPicker?.Apply(_settings);
        if(_stopwatchArtworkOpaque!=null)
        {
            bool loading=_loadingStopwatch;_loadingStopwatch=true;
            try{_stopwatchArtworkOpaque.IsEnabled=_settings.StopwatchAppearance.Artwork!="clean";_stopwatchArtworkOpaque.IsChecked=_settings.StopwatchAppearance.OpaqueLayers.HasFlag(StopwatchOpaqueLayers.Artwork);}
            finally{_loadingStopwatch=loading;}
        }
    }

    private void SyncStopwatchTextSizeControl()
    {
        if (_stopwatchTextSize != null)
            _stopwatchTextSize.Value = _settings.TextSize;
    }

    private void StopwatchTextSizeChanged(double value)
    {
        if (_loadingStopwatch || _loading || _settings.TextSize == value) return;
        _settings.TextSize = value;
        _loading = true;
        try
        {
            TextSizeSlider.Value = value;
            UpdateValueLabels();
        }
        finally { _loading = false; }
        CommitStopwatchAppearance();
    }

    private void PreviewChoiceChanged(object? sender, RoutedEventArgs e)
    {
        _stopwatchPreview.SetPreviewState(PreviewPaused?.IsChecked != true, PreviewLight?.IsChecked != true);
    }
}
