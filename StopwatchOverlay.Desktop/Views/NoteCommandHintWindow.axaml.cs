using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class NoteCommandHintWindow : Window
{
    private readonly double _scale;
    private readonly Border _frame;
    private readonly Grid _body, _layout;
    private readonly LayoutTransformControl _scaled;

    public NoteCommandHintWindow() : this(null) { }
    public NoteCommandHintWindow(IReadOnlyDictionary<NoteCommandAction, uint>? keys) : this(keys, null) { }

    internal NoteCommandHintWindow(IReadOnlyDictionary<NoteCommandAction, uint>? keys, AppSettings? settings)
    {
        InitializeComponent();
        keys ??= AppSettings.DefaultNoteCommandModeKeys();
        _scale = AppUiScale.Normalize(settings?.UiScalePercent ?? AppUiScale.DefaultPercent) / 100;
        bool nika = AppThemeCatalog.Normalize(settings?.ThemeMode) == AppThemeCatalog.Nika;
        IBrush Color(string resource, string color, string fallback) => !nika && this.TryFindResource(resource, out var value) && value is IBrush brush
            ? brush : Brush.Parse(nika ? color : fallback);
        var ink = Color("PrimaryTextBrush", "#102B3B", "#FFFFFF");
        var muted = Color("SecondaryTextBrush", "#655539", "#CCCCCC");
        var gold = Color("BorderBrush", "#B78329", "#666666");
        var paper = Color("SurfaceRaisedBrush", "#EBD3A5", "#202020");
        var badgeFill = Color("SurfaceBrush", "#F8EACA", "#303030");
        if (nika) FontFamily = ClockPickerPresentation.Body;
        TextBlock Text(string value, double size=13, IBrush? brush=null) => new() {
            [DesktopTypography.ScopeProperty] = "Notes", Text=value, FontSize=size, Foreground=brush??ink,
            TextWrapping=TextWrapping.Wrap, VerticalAlignment=VerticalAlignment.Center };
        Border Badge(string value, bool dark=false)
        {
            var label=Text(value,11,dark && nika?Brushes.White:ink);
            label.TextWrapping=TextWrapping.NoWrap; label.FontWeight=FontWeight.SemiBold;
            label.HorizontalAlignment=HorizontalAlignment.Center;
            DesktopTypography.SetPreserveColor(label,true);
            return new Border { Background=dark && nika?ink:badgeFill, BorderBrush=gold, BorderThickness=new(1),
                CornerRadius=new(3),Padding=new(4,1),Child=label };
        }
        var title=Text("Note Commands",18); title.FontWeight=FontWeight.Bold;
        if(nika)title.FontFamily=StopwatchTypography.Resolve("navigator-bold").NameFont;
        var leader=Badge((settings?.NoteLeaderShortcut??AppSettings.DefaultNoteLeaderShortcut()).Format().Replace("+"," + "),true);
        leader.VerticalAlignment=VerticalAlignment.Top;
        var header=new Grid {ColumnDefinitions=new("*,Auto"),Margin=new(0,0,0,7),Children={title,leader}};
        Grid.SetColumn(leader,1);
        _body=new Grid();
        void Add(string id,uint key,string label)
        {
            var badge=Badge(Shortcut.FormatKeyName(key));badge.MinWidth=44;
            var text=Text(label);
            var row=new Grid {Name="NoteCommandRow"+id,ColumnDefinitions=new("Auto,8,*"),MinHeight=21,Children={badge,text}};
            Grid.SetColumn(text,2);_body.Children.Add(row);
        }
        foreach(var (action,label) in new[]{(NoteCommandAction.AddTodo,"Add Todo"),(NoteCommandAction.AddNote,"Quick note"),
            (NoteCommandAction.AddReminder,"Add reminder"),(NoteCommandAction.ViewNotes,"View notes"),(NoteCommandAction.ToggleTodoOverlay,"Show / hide todos")})
            if(keys.TryGetValue(action,out uint key) && key!=0)Add(action.ToString(),key,label);
        bool spaceFallback=!keys.Values.Contains(ShortcutCommandMap.VK_SPACE);
        if(spaceFallback)Add("Space",ShortcutCommandMap.VK_SPACE,"Expand / collapse");
        var help=Text(spaceFallback?"Space expands / collapses Todos":"Press a key to select",11,muted);
        var escape=Text("Esc cancels",11,muted);
        var footer=new Grid {ColumnDefinitions=new("*,Auto"),Margin=new(0,7,0,0),Children={help,escape}};Grid.SetColumn(escape,1);
        var scroll=new ScrollViewer {Content=_body,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled};
        _layout=new Grid {RowDefinitions=new("Auto,*,Auto"),Children={header,scroll,footer}};Grid.SetRow(scroll,1);Grid.SetRow(footer,2);
        _frame=new Border {Name="NoteCommandFrame",Padding=new(12,10),CornerRadius=new(6),BorderThickness=new(1),BorderBrush=gold,Background=paper,Child=_layout};
        _scaled=new LayoutTransformControl {LayoutTransform=new ScaleTransform(_scale,_scale),Child=_frame};
        Content=_scaled;
        FitToScreen();Opened+=(_,_)=>FitToScreen();
    }

    private void FitToScreen()
    {
        var screen=Screens.ScreenFromVisual(this)??Screens.Primary;
        double availableWidth=screen==null?1920:screen.WorkingArea.Width/screen.Scaling-48;
        double availableHeight=screen==null?1080:screen.WorkingArea.Height/screen.Scaling-48;
        double width=Math.Min(620,availableWidth/_scale);
        int columns=Math.Clamp((int)(width/190),1,3);
        _body.ColumnDefinitions=new(string.Join(",12,",Enumerable.Repeat("*",columns)));
        _body.RowDefinitions=new(string.Join(",4,",Enumerable.Repeat("Auto",Math.Max(1,(_body.Children.Count+columns-1)/columns))));
        for(int i=0;i<_body.Children.Count;i++){Grid.SetColumn(_body.Children[i],i%columns*2);Grid.SetRow(_body.Children[i],i/columns*2);}
        var measure=new Size(Math.Max(1,width-26),double.PositiveInfinity);
        _body.Measure(measure);_layout.Children[0].Measure(measure);_layout.Children[2].Measure(measure);
        double height=Math.Min(Math.Max(132,_body.DesiredSize.Height+_layout.Children[0].DesiredSize.Height+_layout.Children[2].DesiredSize.Height+28),availableHeight/_scale);
        _frame.Width=width;_frame.Height=height;Width=width*_scale;Height=height*_scale;
        _scaled.InvalidateArrange();
        if(screen!=null)Position=new PixelPoint(screen.WorkingArea.X+(screen.WorkingArea.Width-(int)Math.Round(Width*screen.Scaling))/2,
            screen.WorkingArea.Y+(int)Math.Round(24*screen.Scaling));
    }
}
