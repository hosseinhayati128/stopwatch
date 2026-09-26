using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace StopwatchOverlay;

/// <summary>Overrides text rendering while retaining each original value, binding and theme resource.</summary>
public static class TypographyManager
{
    public static readonly DependencyProperty ScopeProperty = DependencyProperty.RegisterAttached(
        "Scope", typeof(string), typeof(TypographyManager), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));
    public static void SetScope(DependencyObject element, string value) => element.SetValue(ScopeProperty, value);
    public static string? GetScope(DependencyObject element) => (string?)element.GetValue(ScopeProperty);
    private static TypographySettings _settings = new();
    private static bool _registered;
    private static readonly ConditionalWeakTable<FrameworkElement, AppliedState> States = new();
    private sealed class AppliedState { public double Scale = 1; public string Color = TypographySettings.ThemeDefault; public Brush? Brush; public Color? ResolvedColor; public double Opacity = 1; public Binding? SizeAnchor; public Binding? ColorAnchor; }

    public static void Apply(TypographySettings settings)
    {
        settings.Normalize();
        _settings = settings;
        if (!_registered)
        {
            EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnElementLoaded), true);
            _registered = true;
        }
        if (Application.Current is { } app && app.Dispatcher.CheckAccess())
            foreach (Window window in app.Windows) ApplyWindow(window);
    }

    public static void ApplyWindow(Window window)
    {
        // Content can already be measured for an offscreen preview before the
        // Window's own template has a visual child. Start at the content itself.
        if (window.Content is DependencyObject content) Visit(content, new HashSet<DependencyObject>());
    }

    private static void OnElementLoaded(object sender, RoutedEventArgs args)
    {
        // Loaded is a direct event; dynamically created cards and popup contents arrive here too.
        if (sender is FrameworkElement element) ApplyElement(element);
    }

    private static void Visit(DependencyObject node, HashSet<DependencyObject> visited)
    {
        if (!visited.Add(node)) return;
        if (node is FrameworkElement element) ApplyElement(element);
        // Hidden panels and newly replaced templates may not yet have a visual tree.
        foreach (object child in LogicalTreeHelper.GetChildren(node))
            if (child is DependencyObject dependency) Visit(dependency, visited);
        if (node is Visual or System.Windows.Media.Media3D.Visual3D)
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Visit(VisualTreeHelper.GetChild(node, i), visited);
    }

    internal static string ResolveScope(FrameworkElement element)
    {
        if (GetScope(element) is { Length: > 0 } explicitScope) return explicitScope;
        Window? window = Window.GetWindow(element);
        // Drop-down presenters live in a separate visual root. Resolve their owning
        // control so a panel override also covers its menus, choices and tooltips.
        var visited = new HashSet<DependencyObject>();
        for (DependencyObject? node = element; window == null && node != null && visited.Add(node);)
        {
            if (node != element && GetScope(node) is { Length: > 0 } parentScope) return parentScope;
            DependencyObject? owner = node switch
            {
                Popup popup => popup.PlacementTarget,
                ContextMenu menu => menu.PlacementTarget,
                ToolTip tip => tip.PlacementTarget,
                _ => ItemsControl.ItemsControlFromItemContainer(node)
            };
            node = owner ?? LogicalTreeHelper.GetParent(node) ?? VisualTreeHelper.GetParent(node);
            if (node != null) window = Window.GetWindow(node);
        }
        return window?.GetType().Name switch
        {
            "ControllerWindow" or "ShortcutCommandHintWindow" => "Controller",
            "ProjectDashboardWindow" or "ProjectRecordEditorWindow" or "ProjectRecordDeleteWindow" => "Dashboard",
            "SettingsWindow" => "Settings",
            "NoteEntryWindow" or "NotesViewerWindow" or "NoteCommandHintWindow" => "Notes",
            "OverlayWindow" => "Overlay",
            _ => "Dialogs"
        };
    }

    private static bool IsText(FrameworkElement element)
        => element is TextBlock or AccessText or TextBoxBase or PasswordBox;

    internal static void ApplyElement(FrameworkElement element)
    {
        if (element is Themes.PirateTimerText timer)
        {
            var timerStyle = _settings.Resolve(ResolveScope(timer));
            timer.SetCurrentValue(Themes.PirateTimerText.TextScaleProperty, timerStyle.SizePercent / 100);
            timer.SetCurrentValue(Themes.PirateTimerText.TextBrushProperty, ResolveBrush(timer, timerStyle.Color));
            return;
        }
        if (!IsText(element)) return;
        // Symbol fonts represent icons, not editable application typography.
        var family = (FontFamily)element.GetValue(TextElement.FontFamilyProperty);
        if (family.Source.Contains("MDL2", StringComparison.OrdinalIgnoreCase)
            || family.Source.Contains("Fluent Icons", StringComparison.OrdinalIgnoreCase)
            || family.Source.Contains("Wingdings", StringComparison.OrdinalIgnoreCase)) return;
        var style = _settings.Resolve(ResolveScope(element));
        double scale = style.SizePercent / 100;
        // A text input can contain a text presenter that already inherits its adjusted size.
        for (DependencyObject? parent = VisualTreeHelper.GetParent(element); parent != null; parent = VisualTreeHelper.GetParent(parent))
            if (parent is FrameworkElement ancestor && IsText(ancestor)) { scale = 1; break; }
        var state = States.GetOrCreateValue(element);
        if (state.Scale != scale)
        {
            element.BeginAnimation(TextElement.FontSizeProperty, null);
            if (scale != 1)
            {
                AnchorInheritedValue(element, TextElement.FontSizeProperty, ref state.SizeAnchor);
                HoldAnimation(element, TextElement.FontSizeProperty, new FontScaleAnimation(scale));
            }
            else RestoreInheritance(element, TextElement.FontSizeProperty, ref state.SizeAnchor);
            state.Scale = scale;
        }
        Brush? brush = ResolveBrush(element, style.Color);
        Color? resolvedColor = (brush as SolidColorBrush)?.Color;
        double opacity = brush?.Opacity ?? 1;
        if (state.Color != style.Color || !ReferenceEquals(state.Brush, brush) || state.ResolvedColor != resolvedColor || state.Opacity != opacity)
        {
            if (brush == null)
            {
                element.BeginAnimation(TextElement.ForegroundProperty, null);
                RestoreInheritance(element, TextElement.ForegroundProperty, ref state.ColorAnchor);
            }
            else
            {
                element.BeginAnimation(TextElement.ForegroundProperty, null);
                AnchorInheritedValue(element, TextElement.ForegroundProperty, ref state.ColorAnchor);
                var animation = new ObjectAnimationUsingKeyFrames { Duration = TimeSpan.Zero };
                animation.KeyFrames.Add(new DiscreteObjectKeyFrame(brush, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                HoldAnimation(element, TextElement.ForegroundProperty, animation);
            }
            state.Color = style.Color;
            state.Brush = brush;
            state.ResolvedColor = resolvedColor;
            state.Opacity = opacity;
        }
    }

    private static void HoldAnimation(FrameworkElement element, DependencyProperty property, AnimationTimeline animation)
    {
        var clock = (AnimationClock)animation.CreateClock(true);
        element.ApplyAnimationClock(property, clock);
        clock.Controller!.Begin();
        clock.Controller.SeekAlignedToLastTick(TimeSpan.Zero, TimeSeekOrigin.BeginTime);
    }

    private static void AnchorInheritedValue(FrameworkElement element, DependencyProperty property, ref Binding? anchor)
    {
        if (DependencyPropertyHelper.GetValueSource(element, property).BaseValueSource != BaseValueSource.Inherited)
            return;
        DependencyObject? parent = LogicalTreeHelper.GetParent(element) ?? VisualTreeHelper.GetParent(element);
        if (parent == null) return;
        // WPF can discard an animation on a purely inherited effective value.
        // A live parent binding provides a local animation base without freezing
        // the inherited theme/selection value. Remove only our binding on reset.
        anchor = new Binding { Source = parent, Path = new PropertyPath(property), Mode = BindingMode.OneWay };
        BindingOperations.SetBinding(element, property, anchor);
    }

    private static void RestoreInheritance(FrameworkElement element, DependencyProperty property, ref Binding? anchor)
    {
        if (anchor != null && ReferenceEquals(BindingOperations.GetBindingBase(element, property), anchor))
            element.ClearValue(property);
        anchor = null;
    }

    internal static Brush? ResolveBrush(FrameworkElement element, string choice)
    {
        string? resource = choice switch
        {
            TypographySettings.ThemeText => "PrimaryTextBrush",
            TypographySettings.ThemeAccent => "AccentBrush",
            TypographySettings.ThemeMuted => "SecondaryTextBrush",
            _ => null
        };
        if (resource != null) return element.TryFindResource(resource) as Brush;
        if (!TypographySettings.TryHexColor(choice, out var color)) return null;
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    // A held animation uses the current underlying value, so later binding/theme changes
    // remain effective. Removing it immediately restores the untouched original source.
    private sealed class FontScaleAnimation : DoubleAnimationBase
    {
        private readonly double _factor;
        public FontScaleAnimation(double factor) { _factor = factor; Duration = TimeSpan.Zero; }
        protected override Freezable CreateInstanceCore() => new FontScaleAnimation(_factor);
        protected override double GetCurrentValueCore(double defaultOriginValue, double defaultDestinationValue, AnimationClock clock)
            => defaultOriginValue * _factor;
    }
}



