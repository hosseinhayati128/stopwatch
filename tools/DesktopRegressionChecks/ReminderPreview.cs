using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using StopwatchOverlay.Desktop.Controls;

// Design-only native controls with fixture data; no settings, persistence or reminder service.
internal static class ReminderPreview
{
    const string Output="design/one-piece-v2/notes/reminder-native-preview";
    static readonly IBrush Ink=Brush.Parse("#102B3B"),Muted=Brush.Parse("#655539"),Gold=Brush.Parse("#B78329"),Cream=Brush.Parse("#F8EACA"),Rule=Brush.Parse("#CDB784");
    static readonly FontFamily Body=StopwatchTypography.Resolve("modern-deck").NameFont;
    static TextBlock Text(string text,double size=13,IBrush? color=null)=>new(){Text=text,FontFamily=Body,FontSize=size,Foreground=color??Ink,VerticalAlignment=VerticalAlignment.Center};
    static Button Action(string text,bool primary=false)=>new(){Content=Text(text,13,primary?Brushes.White:Ink),Height=38,MinWidth=88,Padding=new(16,6),
        HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center,Background=primary?Ink:Brushes.Transparent,
        BorderBrush=Gold,BorderThickness=new(1),CornerRadius=new(4)};
    internal static void Run()
    {
        if(Application.Current is not FrameSizePreviewApp)throw new Exception("Isolated preview app required.");
        Application.Current.Styles.Add(new FluentTheme());Directory.CreateDirectory(Output);
        foreach(int percent in new[]{70,90,125})
            foreach(bool editing in new[]{false,true})
                foreach(bool scheduled in new[]{false,true})Render(percent,editing,scheduled);
    }
    static void Render(int percent,bool editing,bool scheduled)
    {
        var title=Text(editing?"Edit Reminder":"Add Reminder",28);title.FontFamily=StopwatchTypography.Resolve("navigator-bold").NameFont;title.FontWeight=FontWeight.Bold;
        var heading=new StackPanel {Spacing=4,Children={title,Text(editing?"Update your reminder and its optional schedule.":"Add a reminder, with an optional date and time.",12,Muted)}};
        var close=Action("");close.MinWidth=32;close.Width=32;close.Height=32;close.Padding=new(0);close.BorderThickness=new(0);close.VerticalAlignment=VerticalAlignment.Top;
        close.Content=new Avalonia.Controls.Shapes.Path {Width=16,Height=16,Stretch=Stretch.Uniform,Data=Geometry.Parse("M1,1 L15,15 M15,1 L1,15"),Stroke=Ink,StrokeThickness=1.7};
        var header=new Grid {ColumnDefinitions=new("*,32"),Children={heading,close}};Grid.SetColumn(close,1);
        var input=new TextBox {Text=editing?"Review the dashboard sketches before the next session.":null,Watermark="What should I remind you about?",
            AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,FontFamily=Body,FontSize=14,Foreground=Ink,Background=Cream,BorderBrush=Rule,
            BorderThickness=new(1),CornerRadius=new(5),Padding=new(14,12),VerticalContentAlignment=VerticalAlignment.Top};
        var toggleLabel=Text("Set date & time");toggleLabel.Margin=new(8,0,0,0);
        var toggle=new CheckBox {Content=toggleLabel,IsChecked=scheduled,MinHeight=28,Padding=new(0),Foreground=Ink};
        toggle.Resources["CheckBoxCheckBackgroundFillChecked"]=Ink;
        toggle.Resources["CheckBoxCheckBorderBrushChecked"]=Gold;
        toggle.Resources["CheckBoxCheckGlyphForegroundChecked"]=Cream;
        var date=new DatePicker {SelectedDate=new DateTimeOffset(2026,11,19,0,0,0,TimeSpan.Zero),DayFormat="dd",MonthFormat="MMM",YearFormat="yyyy",
            HorizontalAlignment=HorizontalAlignment.Stretch,FontFamily=Body,FontSize=13,Background=Cream,Foreground=Ink,BorderBrush=Rule,Height=38};
        var time=new TimePicker {SelectedTime=new TimeSpan(18,30,0),ClockIdentifier="24HourClock",HorizontalAlignment=HorizontalAlignment.Stretch,
            FontFamily=Body,FontSize=13,Background=Cream,Foreground=Ink,BorderBrush=Rule,Height=38};
        var dateField=new StackPanel {Spacing=5,Children={Text("Date",11,Muted),date}};
        var timeField=new StackPanel {Spacing=5,Children={Text("Time · 24-hour",11,Muted),time}};
        var fields=new Grid {ColumnDefinitions=new("*,16,190"),Margin=new(0,10,0,0),Children={dateField,timeField}};Grid.SetColumn(timeField,2);
        var localTime=Text("Local time · uses this computer’s time zone",11,Muted);localTime.Margin=new(0,8,0,0);
        var scheduledFields=new StackPanel {Children={fields,localTime}};
        var noSchedule=Text("No date or time set.",11,Muted);noSchedule.Margin=new(0,4,0,0);
        var scheduling=new Border {Background=Brush.Parse("#33FFF3D8"),BorderBrush=Rule,BorderThickness=new(1),CornerRadius=new(5),Padding=new(12),
            Child=new StackPanel {Children={toggle,noSchedule,scheduledFields}}};
        var cancel=Action("Cancel");var save=Action(editing?"Save changes":"Save reminder",true);
        var actions=new StackPanel {Orientation=Orientation.Horizontal,Spacing=8,Children={cancel,save}};
        var hint=Text("Ctrl+Enter save  ·  Esc cancel",11,Muted);
        var footer=new Grid {ColumnDefinitions=new("*,Auto"),Children={hint,actions}};Grid.SetColumn(actions,1);
        var layout=new Grid {RowDefinitions=new("66,*,16,Auto,16,38"),Children={header,input,scheduling,footer}};
        Grid.SetRow(input,1);Grid.SetRow(scheduling,3);Grid.SetRow(footer,5);
        var frame=new StopwatchFrameSurface {Width=620,ArtworkScale=.16,Padding=new(10),Background=Brush.Parse("#EBD3A5"),Child=layout};
        double scale=percent/100d;
        var window=new Window {Width=620*scale,ShowActivated=false,ShowInTaskbar=false,SystemDecorations=SystemDecorations.None,Background=Brushes.Transparent,
            RequestedThemeVariant=Avalonia.Styling.ThemeVariant.Light,Content=new LayoutTransformControl {LayoutTransform=new ScaleTransform(scale,scale),Child=frame}};
        void UpdateSchedule()
        {
            bool enabled=toggle.IsChecked==true;scheduledFields.IsVisible=enabled;noSchedule.IsVisible=!enabled;
            scheduling.Height=enabled?166:76;
            frame.Height=enabled?510:420;window.Height=frame.Height*scale;
        }
        toggle.IsCheckedChanged+=(_,_)=>UpdateSchedule();UpdateSchedule();
        try
        {
            window.Show();ClockOverlayChecks.Pump(100);
            double inputHeight=input.Bounds.Height;
            toggle.IsChecked=!scheduled;ClockOverlayChecks.Pump(30);
            Require(Math.Abs(input.Bounds.Height-inputHeight)<1.5,"Schedule toggle changed writing space.");
            toggle.IsChecked=scheduled;ClockOverlayChecks.Pump(50);
            Require(scheduledFields.IsVisible==scheduled&&noSchedule.IsVisible!=scheduled,"Schedule visibility differs from toggle.");
            Require(date.SelectedDate?.Day==19&&time.SelectedTime==new TimeSpan(18,30,0),"Toggling lost the draft schedule.");
            Require(Math.Abs(input.Bounds.Height-inputHeight)<1,"Toggling changed the restored writing area.");
            Require(Math.Abs(window.ClientSize.Width-620*scale)<1.1&&Math.Abs(window.ClientSize.Height-frame.Height*scale)<1.1,"Native size mismatch.");
            Require(input.Bounds.Height>=140&&hint.DesiredSize.Width<=hint.Bounds.Width+1,"Writing area or footer clipped.");
            Require(input.TranslatePoint(new Point(0,input.Bounds.Height),frame)!.Value.Y<scheduling.TranslatePoint(default,frame)!.Value.Y,"Editor overlaps scheduling.");
            Require(scheduling.TranslatePoint(new Point(0,scheduling.Bounds.Height),frame)!.Value.Y<footer.TranslatePoint(default,frame)!.Value.Y,"Schedule overlaps actions.");
            if(scheduled)Require(date.Bounds.Width>=250&&time.Bounds.Width>=180&&localTime.DesiredSize.Width<=localTime.Bounds.Width+1,"Schedule fields clipped.");
            foreach(var control in new Control[]{close,cancel,save})
            {
                var edge=control.TranslatePoint(new Point(control.Bounds.Width,control.Bounds.Height),frame)!.Value;
                Require(edge.X<=frame.Bounds.Width-10&&edge.Y<=frame.Bounds.Height-10,"Action outside frame.");
            }
            string name=$"reminder-{(editing?"edit":"add")}-{(scheduled?"scheduled":"unscheduled")}-{percent}";
            using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width),(int)Math.Ceiling(window.ClientSize.Height)),new Vector(96,96));
            if(scale>=1)bitmap.Render(window);
            else{using var dc=bitmap.CreateDrawingContext();dc.DrawRectangle(new VisualBrush(frame){Stretch=Stretch.Fill,SourceRect=new RelativeRect(frame.Bounds,RelativeUnit.Absolute)},null,new Rect(frame.Bounds.Size));}
            bitmap.Save(Path.Combine(Output,name+".png"));
            Console.WriteLine($"PASS {name}: native dimensions, toggle/draft retention, editor space and control bounds");
        }
        finally{window.Close();}
    }
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
