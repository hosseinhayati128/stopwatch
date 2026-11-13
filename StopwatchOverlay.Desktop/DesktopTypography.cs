using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop;

// Coercion leaves local values, resource references and bindings intact underneath the preference.
internal static class DesktopTypography
{
    internal static readonly AttachedProperty<string?> ScopeProperty=AvaloniaProperty.RegisterAttached<Control,string?>("TypographyScope",typeof(DesktopTypography),inherits:true);
    internal static readonly AttachedProperty<bool> PreserveColorProperty=AvaloniaProperty.RegisterAttached<Control,bool>("PreserveTypographyColor",typeof(DesktopTypography),inherits:true);
    private static readonly HashSet<Window> Windows=new();
    private static readonly Dictionary<Control,List<IDisposable>> Anchors=new();
    private static TypographySettings? _settings;
    private static bool _registered;
    private static IDisposable? _loaded,_unloaded,_opened,_inlineAttached,_selection;
    internal static void SetScope(Control control,string scope)=>control.SetValue(ScopeProperty,scope);
    internal static void SetPreserveColor(Control control,bool preserve)=>control.SetValue(PreserveColorProperty,preserve);

    internal static void Initialize(TypographySettings settings)
    {
        if(!_registered)
        {
            foreach(var type in new[]{typeof(TextBlock),typeof(TextBox),typeof(Run),typeof(Span)})
            {
                TextBlock.FontSizeProperty.OverrideMetadata(type,new StyledPropertyMetadata<double>(coerce:CoerceSize));
                TextBlock.ForegroundProperty.OverrideMetadata(type,new StyledPropertyMetadata<IBrush?>(coerce:CoerceColor));
            }
            _registered=true;
        }
        _loaded??=Control.LoadedEvent.AddClassHandler<Control>((control,_)=>RefreshElement(control));
        _unloaded??=Control.UnloadedEvent.AddClassHandler<Control>((control,_)=>Release(control));
        _inlineAttached??=StyledElement.ParentProperty.Changed.AddClassHandler<TextElement>((element,_)=>RefreshInline(element));
        _selection??=SelectingItemsControl.IsSelectedProperty.Changed.AddClassHandler<Control>((control,_)=>Refresh(control));
        _opened??=Window.WindowOpenedEvent.AddClassHandler<Window>((window,_)=>{if(Windows.Add(window))window.Closed+=Closed;Refresh(window);});
        Apply(settings);
    }
    internal static void Apply(TypographySettings settings)
    {
        settings.Normalize();_settings=settings;
        foreach(var window in Windows.ToArray())Refresh(window);
        // Native popups have a separate visual root but retain their owner's typography scope.
        foreach(var control in Anchors.Keys.Where(c=>c.GetVisualRoot() is not Window).ToArray())RefreshElement(control);
    }
    internal static void Shutdown()
    {
        _settings=null;
        foreach(var control in Anchors.Keys.ToArray())Release(control);
        foreach(var window in Windows.ToArray()){Refresh(window);window.Closed-=Closed;}
        Windows.Clear();_loaded?.Dispose();_loaded=null;_unloaded?.Dispose();_unloaded=null;_opened?.Dispose();_opened=null;_inlineAttached?.Dispose();_inlineAttached=null;_selection?.Dispose();_selection=null;
    }
    private static void Closed(object? sender,EventArgs e){if(sender is Window window){Windows.Remove(window);window.Closed-=Closed;}}
    internal static void Refresh(Control root)
    {
        RefreshElement(root);
        foreach(var control in root.GetVisualDescendants().OfType<Control>())RefreshElement(control);
    }
    internal static void ReleasePreview(Control root)
    {
        Release(root);
        foreach(var control in root.GetVisualDescendants().OfType<Control>().ToArray())Release(control);
    }
    private static void RefreshElement(Control control)
    {
        if(control is TextBlock or TextBox)
        {
            if(_settings!=null&&HasContext(control))
            {
                if(!Anchors.TryGetValue(control,out var anchors))Anchors.Add(control,anchors=new List<IDisposable>());
                if(control.GetVisualParent() is {} parent)
                {
                    // Inherited/default values bypass Avalonia coercion; a low-priority live anchor makes them coercible.
                    if(!control.GetBaseValue(TextBlock.FontSizeProperty).HasValue)anchors.Add(control.Bind(TextBlock.FontSizeProperty,parent.GetObservable(TextBlock.FontSizeProperty),BindingPriority.Style));
                    if(!control.GetBaseValue(TextBlock.ForegroundProperty).HasValue)anchors.Add(control.Bind(TextBlock.ForegroundProperty,parent.GetObservable(TextBlock.ForegroundProperty),BindingPriority.Style));
                }
            }
            control.CoerceValue(TextBlock.FontSizeProperty);control.CoerceValue(TextBlock.ForegroundProperty);
            if(control is TextBlock {Inlines:{} inlines})foreach(var inline in inlines)RefreshInline(inline);
        }
        else if(control is ThemedClockFace face)
        {var style=Style(control);face.SetTypography(style?.SizePercent/100??1,Color(control,style));}
        else if(control is PirateTimerText timer)
        {var style=Style(control);timer.SetTypography(style?.SizePercent/100??1,Color(control,style));}
    }
    private static void Release(Control control)
    {
        if(!Anchors.Remove(control,out var anchors))return;
        foreach(var anchor in anchors)anchor.Dispose();
        control.CoerceValue(TextBlock.FontSizeProperty);control.CoerceValue(TextBlock.ForegroundProperty);
    }
    private static Control? InlineOwner(StyledElement element)
    {for(var parent=element.Parent;parent!=null;parent=parent.Parent)if(parent is Control control)return control;return null;}
    private static void RefreshInline(TextElement element)
    {
        element.CoerceValue(TextElement.FontSizeProperty);element.CoerceValue(TextElement.ForegroundProperty);
        if(element is Span span)foreach(var child in span.Inlines)RefreshInline(child);
    }
    private static bool HasContext(Control control)=>control.GetVisualRoot()!=null||control.GetValue(ScopeProperty)!=null;
    private static TypographyStyle? Style(Control control)=>_settings!=null&&HasContext(control)&&!Icon(control)?_settings.Resolve(ResolveScope(control)):null;
    private static bool Icon(Control control)
    {
        string font=control.GetValue(TextBlock.FontFamilyProperty).Name;
        return font.Contains("MDL2",StringComparison.OrdinalIgnoreCase)||font.Contains("Fluent Icons",StringComparison.OrdinalIgnoreCase)||font.Contains("Wingdings",StringComparison.OrdinalIgnoreCase);
    }
    private static double CoerceSize(AvaloniaObject value,double size)
    {
        if(value is TextElement element)return size*(InlineOwner(element) is {} owner?Style(owner)?.SizePercent/100??1:1);
        if(value is not Control control||Style(control) is not {} style)return size;
        // TextBox's presenter inherits its already scaled font. Nested text must not multiply it again.
        for(var parent=control.GetVisualParent();parent!=null;parent=parent.GetVisualParent())
            if(parent is TextBlock or TextBox)return size;
        return size*style.SizePercent/100;
    }
    private static IBrush? CoerceColor(AvaloniaObject value,IBrush? original)
    {
        if(value is TextElement element)return InlineOwner(element) is {} owner?CoerceColor(owner,original):original;
        if(value is not Control control||control.GetValue(PreserveColorProperty))return original;
        for(Visual? node=control;node!=null;node=node.GetVisualParent())
            if(node is Button||node is MenuItem||node is Control c&&(c.GetValue(SelectingItemsControl.IsSelectedProperty)||c.Classes.Contains(":selected")))return original;
        return Color(control,Style(control))??original;
    }
    internal static IBrush? Color(Control control,TypographyStyle? style)
    {
        if(style==null||style.Color==TypographySettings.ThemeDefault)return null;
        string? resource=style.Color switch{TypographySettings.ThemeText=>"PrimaryTextBrush",TypographySettings.ThemeAccent=>"AccentBrush",TypographySettings.ThemeMuted=>"SecondaryTextBrush",_=>null};
        if(resource!=null)return control.TryFindResource(resource,out var found)?found as IBrush:null;
        return Brush.Parse(style.Color);
    }
    internal static string ResolveScope(Control control)
    {
        var visited=new HashSet<Visual>();
        for(Visual? node=control;node!=null&&visited.Add(node);)
        {
            if(node is Control c&&c.GetValue(ScopeProperty) is {} scope)return scope;
            if(node is Window window)return window.GetType().Name switch
            {
                "ControllerWindow" or "ShortcutCommandHintWindow"=>"Controller",
                "ProjectDashboardWindow" or "ProjectRecordEditorWindow" or "ProjectRecordDeleteWindow"=>"Dashboard",
                "SettingsWindow"=>"Settings",
                "NoteEntryWindow" or "NotesViewerWindow" or "NoteCommandHintWindow" or "TodoOverlayWindow"=>"Notes",
                "OverlayWindow" or "ActiveClocksShelfWindow"=>"Overlay",
                _=>"Dialogs"
            };
            node=node switch
            {
                Popup popup when popup.PlacementTarget!=null=>popup.PlacementTarget,
                ContextMenu menu when menu.PlacementTarget!=null=>menu.PlacementTarget,
                IHostedVisualTreeRoot hosted=>hosted.Host,
                _=>node.GetVisualParent()??(node as ILogical)?.LogicalParent as Visual
            };
        }
        return "Dialogs";
    }
}
