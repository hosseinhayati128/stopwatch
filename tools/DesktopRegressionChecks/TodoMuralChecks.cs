using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class TodoMuralChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    static T Named<T>(Control parent,string name) where T:Control=>parent.GetLogicalDescendants().OfType<T>().Single(c=>c.Name==name);
    static void Click(Button b)=>b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    internal static void CaptureAudit()
    {
        const int width=320,height=360;
        var output=Path.GetFullPath(".superpowers/sdd/2026-10-30-todo-sidebar-artwork/audit");Directory.CreateDirectory(output);
        var gallery=new Avalonia.Controls.Primitives.UniformGrid {Columns=4,Rows=5,Width=width*4,Height=height*5,Background=Brushes.White};
        foreach(var option in TodoAppearanceCatalog.SidebarArtworks.Where(o=>o.Id!="none"))
        {
            var art=new TodoMuralArtwork {Choice=option.Id,Width=width,Height=height-32};
            var panel=new Grid {Background=Brush.Parse("#F5DEAE"),Width=width,Height=height-32};panel.Children.Add(art);
            var label=new TextBlock {Text=option.Label,FontSize=16,Foreground=Brushes.Black,HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Center};
            var cell=new StackPanel {Width=width,Height=height};cell.Children.Add(label);cell.Children.Add(panel);gallery.Children.Add(cell);
            art.Measure(new Size(width,height-32));art.Arrange(new Rect(0,0,width,height-32));
            using var isolated=new RenderTargetBitmap(new PixelSize(width,height-32),new Vector(96,96));isolated.Render(art);isolated.Save(Path.Combine(output,option.Id+".png"));
            panel.Children.Add(new AlignmentGuide());
        }
        gallery.Measure(new Size(width*4,height*5));gallery.Arrange(new Rect(0,0,width*4,height*5));
        using var image=new RenderTargetBitmap(new PixelSize(width*4,height*5),new Vector(96,96));image.Render(gallery);image.Save(Path.Combine(output,"all-17.png"));
        var published=Path.GetFullPath("design/one-piece-v2/todo-overlay/implemented/artwork-audit");Directory.CreateDirectory(published);
        image.Save(Path.Combine(published,"all-17-guides.png"));
        Console.WriteLine("Saved all 17 native artwork renders and gallery: "+output);
    }
    sealed class AlignmentGuide:Control
    {
        public override void Render(DrawingContext context)
        {
            context.DrawLine(new Pen(Brush.Parse("#502C78A0"),1),new Point(Bounds.Width/2,0),new Point(Bounds.Width/2,Bounds.Height));
        }
    }
    internal static void Run(bool dropdownOnly=false)
    {
        typeof(App).Assembly.GetType("StopwatchOverlay.Desktop.ThemeResources")!.GetMethod("ApplyApplication",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{AppThemeCatalog.Nika});
        string dir=Path.GetFullPath(".superpowers/sdd/2026-10-30-todo-sidebar-artwork/fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        string path=SettingsStore.SettingsPath;byte[]? before=File.Exists(path)?File.ReadAllBytes(path):null;
        try
        {
            for(int i=0;i<3;i++)Require(ObsidianNotesSync.AppendEntry(dir,NoteType.Todo,new[]{"Finish illustration","Read 20 pages","Review project notes"}[i],timestamp:new DateTime(2026,10,30,12,i,0)).Success,"Unable to create isolated task.");
            var settings=new AppSettings {ThemeMode=AppThemeCatalog.Nika,ObsidianVaultFolder=dir,TelegramEnabled=false,HabiticaEnabled=false};
            TestDropdown(settings);
            if(!dropdownOnly){TestCentering();TestRendering();TestSettingsAndOverlay(settings);}
            Require((before==null&&!File.Exists(path))||(before!=null&&File.ReadAllBytes(path).SequenceEqual(before)),"Mural tests wrote production settings.");
        }
        finally{Directory.Delete(dir,true);}
    }
    static void TestDropdown(AppSettings settings)
    {
        var window=new SettingsWindow(settings){ShowActivated=false,ShowInTaskbar=false};
        try
        {
            window.Show();var nav=window.FindControl<ListBox>("NavigationList")!;
            nav.SelectedItem=nav.Items.OfType<ListBoxItem>().Single(i=>Equals(i.Tag,"TodoAppearance"));ClockOverlayChecks.Pump(70);
            foreach(string profile in new[]{"classic","pixel"})
            {
                Click(Named<Button>(window,"TodoProfile_"+profile));
                if(profile=="pixel")Click(Named<Button>(window,"TodoBackground_midnight-chart"));
                var choice=Named<ComboBox>(window,"TodoSidebarArtworkChoice");choice.BringIntoView();ClockOverlayChecks.Pump(40);
                choice.IsDropDownOpen=true;ClockOverlayChecks.Pump(80);
                var popup=choice.GetVisualDescendants().OfType<Popup>().Concat(choice.GetLogicalDescendants().OfType<Popup>()).FirstOrDefault();
                Require(popup?.Child!=null&&popup.IsOpen,"Artwork popup was not opened by the native ComboBox.");
                var popupContent=popup!.Child!;
                var scroll=popupContent.GetVisualDescendants().OfType<ScrollViewer>().First();
                Require(scroll.Extent.Height>scroll.Viewport.Height,"Artwork popup did not have enough rows to exercise recycling.");
                for(int cycle=0;cycle<3;cycle++)
                {
                    scroll.Offset=new Vector(0,scroll.Extent.Height);ClockOverlayChecks.Pump(70);
                    Require(popupContent.GetVisualDescendants().OfType<ComboBoxItem>().Any(item=>item.Content is TodoAppearanceOption {Id:"poneglyph"}),"Scrolling did not reach the last artwork.");
                    foreach(var item in popupContent.GetVisualDescendants().OfType<ComboBoxItem>())
                    {
                        if(item.Content is not TodoAppearanceOption option)continue;
                        Require(item.GetVisualDescendants().OfType<TodoMuralArtwork>().Any(a=>a.Choice==option.Id),"Recycled thumbnail shows the wrong artwork: "+option.Id);
                        Require(item.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text==option.Label),"Recycled label shows the wrong artwork: "+option.Id);
                    }
                    scroll.Offset=default;ClockOverlayChecks.Pump(70);
                    Require(popupContent.GetVisualDescendants().OfType<ComboBoxItem>().Any(item=>item.Content is TodoAppearanceOption {Id:"none"}),"Scrolling did not return to the first artwork.");
                }
                scroll.Offset=new Vector(0,scroll.Extent.Height);ClockOverlayChecks.Pump(40);
                choice.SelectedItem=TodoAppearanceCatalog.SidebarArtworks.Single(o=>o.Id=="poneglyph");ClockOverlayChecks.Pump(50);
                Require(settings.TodoAppearance.Current.SidebarArtwork=="poneglyph","Choosing an artwork after scrolling did not apply.");
                var reopened=Named<ComboBox>(window,"TodoSidebarArtworkChoice");reopened.IsDropDownOpen=true;ClockOverlayChecks.Pump(60);reopened.IsDropDownOpen=false;
            }
            Console.WriteLine("PASS native artwork popup scrolling, recycled thumbnails, selection and reopening in Classic and Pixel");
        }
        finally{window.Close();settings.TodoAppearance=new();}
    }
    static void TestCentering()
    {
        // Independently annotated source-image landmarks, not centers returned by
        // the renderer. Decoration bounds locate the transform; FocusX locates the
        // main subject within it (a sword/tail/cloud is not a subject center).
        (string Id,double Left,double Right,double FocusX)[] references =
        [
            ("sun-warrior",9,1202,685), ("dawn-dancer",30,298,155),
            ("sun-pirates",345,601,468), ("kozuki-crane",648,913,780.5),
            ("shandora-bell",955,1226,1086), ("cloud-dragon",32,303,179),
            ("phoenix",335,604,469.5), ("zunesha",641,906,759),
            ("laboon",952,1222,1047), ("sea-king",25,293,146),
            ("straw-hat-crest",341,594,476), ("heart-pirates",652,898,782),
            ("whitebeard",962,1226,1094), ("kuja-serpents",26,292,163),
            ("three-sword-spirit",332,607,469.5), ("aces-flame",655,912,792.5),
            ("poneglyph",950,1222,1079)
        ];
        Require(references.Select(r=>r.Id).ToHashSet().SetEquals(TodoAppearanceCatalog.SidebarArtworks.Where(o=>o.Id!="none").Select(o=>o.Id)),"An artwork lacks an independently reviewed alignment reference.");
        var failures=new List<string>();
        foreach(var scenario in new[]{(new PixelSize(384,480),1d,false),(new PixelSize(384,480),.3,false),(new PixelSize(480,240),1d,true),(new PixelSize(576,720),1d,false)})
        foreach(var reference in references)
        {
            var size=scenario.Item1;
            var art=new TodoMuralArtwork {Choice=reference.Id,Width=size.Width,Height=size.Height,Opacity=scenario.Item2,DarkPaper=scenario.Item3};
            art.Measure(size.ToSize(1));art.Arrange(new Rect(size.ToSize(1)));
            using var image=new RenderTargetBitmap(size,new Vector(96,96));image.Render(art);var bytes=Pixels(image);
            var ink=Enumerable.Range(0,size.Width*size.Height).Where(i=>bytes[4*i+3]>20*scenario.Item2).ToArray();
            Require(ink.Length>0,"Cannot measure missing mural: "+reference.Id);
            double left=ink.Min(i=>i%size.Width),right=ink.Max(i=>i%size.Width)+1;
            double x=left+(reference.FocusX-reference.Left)/(reference.Right-reference.Left)*(right-left);
            double y=(ink.Min(i=>i/size.Width)+ink.Max(i=>i/size.Width)+1)/2.0;
            double tolerance=Math.Min(size.Width,size.Height)*.015;
            if(Math.Abs(x-size.Width/2d)>tolerance||Math.Abs(y-size.Height/2d)>tolerance)
                failures.Add($"{reference.Id} in {size}, opacity {scenario.Item2}: subject offset ({x-size.Width/2d:0.0}, {y-size.Height/2d:0.0})");
            Require(left>=size.Width*.04-1&&right<=size.Width*.96+1,"Artwork lost its safe side margin: "+reference.Id);
        }
        Require(failures.Count==0,"Reviewed subjects are not centered: "+string.Join("; ",failures));
        Console.WriteLine("PASS all 17 reviewed subject anchors, safe margins, portrait/wide/scaled panels, both tints and 30% opacity");
    }
    static void TestRendering()
    {
        foreach(var option in TodoAppearanceCatalog.SidebarArtworks)
        {
            var art=new TodoMuralArtwork {Choice=option.Id,Width=180,Height=240};art.Measure(new Size(180,240));art.Arrange(new Rect(0,0,180,240));
            using var image=new RenderTargetBitmap(new PixelSize(180,240),new Vector(96,96));image.Render(art);
            var bytes=Pixels(image);int visible=Enumerable.Range(0,180*240).Count(i=>bytes[4*i+3]>20);
            if(option.Id=="none")Require(visible==0,"None rendered a motif.");
            else
            {
                if(option.Id=="sun-warrior")image.Save(Path.GetFullPath(".superpowers/sdd/2026-10-30-todo-sidebar-artwork/mural-isolated.png"));
                if(option.Id=="whitebeard")image.Save(Path.GetFullPath(".superpowers/sdd/2026-10-30-todo-sidebar-artwork/mural-isolated-whitebeard.png"));
                Require(visible>250,"Missing or empty motif: "+option.Id);
                Require(visible<180*240*.64,"Navy backing survived as a tile: "+option.Id);
                Require(bytes[3]==0&&bytes[(180*240-1)*4+3]==0,"Artwork overpainted the layer corners.");
                if(option.Id=="sun-warrior")
                {
                    art.Opacity=.5;
                    using var half=new RenderTargetBitmap(new PixelSize(180,240),new Vector(96,96));half.Render(art);
                    var halfBytes=Pixels(half);
                    double fullAlpha=Enumerable.Range(0,180*240).Sum(i=>(double)bytes[4*i+3]);
                    double halfAlpha=Enumerable.Range(0,180*240).Sum(i=>(double)halfBytes[4*i+3]);
                    Console.WriteLine($"Half-opacity coverage ratio={halfAlpha/fullAlpha:0.000}");
                    Require(halfAlpha/fullAlpha is >.45 and <.55,"Artwork opacity is compounded by its masks instead of matching the slider.");
                }
            }
        }
        Console.WriteLine("PASS all 17 sidebar motifs render with transparent backgrounds; None is empty");
    }
    static void TestSettingsAndOverlay(AppSettings settings)
    {
        var overlay=new TodoOverlayWindow(settings){ShowActivated=false,ShowInTaskbar=false,Topmost=false};
        var inspector=new SettingsWindow(settings){ShowActivated=false,ShowInTaskbar=false};
        try
        {
            overlay.Show();overlay.ToggleExpanded();ClockOverlayChecks.Pump(100);
            var mural=overlay.FindControl<TodoMuralArtwork>("TodoMural")!;
            Require(!mural.IsVisible,"Default layer changed the existing appearance.");
            inspector.Show();var nav=inspector.FindControl<ListBox>("NavigationList")!;nav.SelectedItem=nav.Items.OfType<ListBoxItem>().Single(i=>Equals(i.Tag,"TodoAppearance"));ClockOverlayChecks.Pump(70);
            int changes=0;Action<SettingsChangeKind> changed=kind=>{if(kind==SettingsChangeKind.TodoAppearance){changes++;overlay.ApplySettings();}};
            typeof(SettingsWindow).GetEvent("SettingsChanged",Private)!.GetAddMethod(true)!.Invoke(inspector,new object[]{changed});
            void Pick(string id){var c=Named<ComboBox>(inspector,"TodoSidebarArtworkChoice");c.SelectedItem=c.Items.OfType<TodoAppearanceOption>().Single(o=>o.Id==id);ClockOverlayChecks.Pump(30);}
            Require(Named<ComboBox>(inspector,"TodoSidebarArtworkChoice").ItemCount==18,"Sidebar artwork choices are incomplete.");
            Pick("sun-warrior");
            Require(mural.IsVisible&&mural.Choice=="sun-warrior"&&!mural.IsHitTestVisible&&!mural.Focusable,"Live layer missing or intercepting input.");
            var slider=Named<Slider>(inspector,"TodoSidebarArtworkOpacity");slider.Value=32;slider.Value=33;
            Require(ReferenceEquals(slider,Named<Slider>(inspector,"TodoSidebarArtworkOpacity")),"Opacity updates rebuilt the active slider.");
            Require(mural.Opacity==.33&&settings.TodoAppearance.Classic.SidebarArtworkOpacity==.33,"Opacity did not update the live layer.");
            Require(overlay.FindControl<StackPanel>("TodoRows")!.Children.Count==3,"Layer removed native task controls.");
            var preview=(RenderTargetBitmap)inspector.FindControl<ContentControl>("TodoPreviewHost")!.GetVisualDescendants().OfType<Image>().Single().Source!;
            byte[] withArt=Pixels(preview);
            slider.Value=0;
            var emptyPreview=(RenderTargetBitmap)inspector.FindControl<ContentControl>("TodoPreviewHost")!.GetVisualDescendants().OfType<Image>().Single().Source!;
            Require(!Pixels(emptyPreview).SequenceEqual(withArt),"Settings preview ignored the layer or opacity.");
            slider.Value=100;Capture(overlay,"mural-full-opacity");
            slider.Value=18;Capture(overlay,"mural-classic");
            Pick("phoenix");
            Click(Named<Button>(inspector,"TodoProfile_pixel"));Require(!mural.IsVisible,"Classic artwork leaked into Pixel profile.");
            Pick("kozuki-crane");Named<Slider>(inspector,"TodoSidebarArtworkOpacity").Value=25;ClockOverlayChecks.Pump(40);Capture(overlay,"mural-pixel");
            Click(Named<Button>(inspector,"TodoBackground_midnight-chart"));Require(mural.DarkPaper,"Midnight map did not switch the mural to gold.");Capture(overlay,"mural-midnight");
            Click(Named<Button>(inspector,"TodoProfile_classic"));Require(mural.Choice=="phoenix"&&mural.Opacity==.18&&!mural.DarkPaper,"Classic artwork was lost on profile switch.");
            Require(settings.TodoAppearance.Pixel.SidebarArtwork=="kozuki-crane"&&settings.TodoAppearance.Pixel.SidebarArtworkOpacity==.25,"Classic changed the Pixel layer.");
            inspector.Width=840;inspector.Height=600;Named<ComboBox>(inspector,"TodoSidebarArtworkChoice").BringIntoView();ClockOverlayChecks.Pump(70);
            var choice=Named<ComboBox>(inspector,"TodoSidebarArtworkChoice");var pos=choice.TranslatePoint(default,inspector)!.Value;
            Require(pos.X>=0&&pos.X+choice.Bounds.Width<=inspector.ClientSize.Width,"Artwork selector exceeds compact settings width.");Capture(inspector,"mural-settings");
            Pick("straw-hat-crest");Named<Slider>(inspector,"TodoSidebarArtworkOpacity").Value=30;Capture(overlay,"mural-luffy-centered");
            foreach(var option in TodoAppearanceCatalog.SidebarArtworks.Where(o=>o.Id!="none"))
            {
                Pick(option.Id);
                var position=mural.TranslatePoint(default,overlay)!.Value;
                Require(Math.Abs(position.X+mural.Bounds.Width/2-overlay.ClientSize.Width/2)<1,"Native panel offsets the mural layer: "+option.Id);
                Capture(overlay,"artwork-audit/"+option.Id);
            }
            SavePanelGallery();
            Pick("none");Require(!mural.IsVisible&&!Named<Slider>(inspector,"TodoSidebarArtworkOpacity").IsEffectivelyEnabled,"None failed to hide or disable the optional layer.");
            Pick("sun-warrior");settings.ThemeMode=AppThemeCatalog.Daylight;overlay.ApplySettings();Require(!mural.IsVisible,"Artwork leaked into another theme.");
            settings.ThemeMode=AppThemeCatalog.Nika;overlay.ApplySettings();Require(mural.IsVisible,"Restoring Nika did not restore its layer.");
            overlay.ToggleExpanded();Require(!overlay.FindControl<Border>("TodoPanel")!.IsVisible,"Artwork kept the collapsed body visible.");
            Require(overlay.FindControl<TodoArtwork>("TodoEmblem")!.Choice=="luffy","Background artwork changed the launcher.");
            Require(changes>=10,"Layer controls did not notify settings persistence.");
            Console.WriteLine("PASS live selection, continuous opacity, previews, profile isolation, compact settings, theme restoration and collapsed launcher");
        }
        finally{inspector.Close();overlay.Close();}
    }
    static byte[] Pixels(RenderTargetBitmap image)
    {
        var size=image.PixelSize;var bytes=new byte[size.Width*size.Height*4];var ptr=Marshal.AllocHGlobal(bytes.Length);
        try{image.CopyPixels(new PixelRect(size),ptr,bytes.Length,size.Width*4);Marshal.Copy(ptr,bytes,0,bytes.Length);}finally{Marshal.FreeHGlobal(ptr);}return bytes;
    }
    static void SavePanelGallery()
    {
        const int width=240,height=370;
        var directory=Path.GetFullPath("design/one-piece-v2/todo-overlay/implemented/artwork-audit");
        var gallery=new Avalonia.Controls.Primitives.UniformGrid {Columns=4,Rows=5,Width=width*4,Height=height*5,Background=Brush.Parse("#EDF1F4")};
        var bitmaps=new List<Bitmap>();
        try
        {
            foreach(var option in TodoAppearanceCatalog.SidebarArtworks.Where(o=>o.Id!="none"))
            {
                var bitmap=new Bitmap(Path.Combine(directory,option.Id+".png"));bitmaps.Add(bitmap);
                var cell=new StackPanel {Width=width,Height=height};
                cell.Children.Add(new TextBlock {Text=option.Label,FontSize=14,Foreground=Brush.Parse("#03283E"),HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Center,Margin=new Thickness(0,6)});
                cell.Children.Add(new Image {Source=bitmap,Width=width,Height=336,Stretch=Stretch.Uniform});gallery.Children.Add(cell);
            }
            gallery.Measure(new Size(width*4,height*5));gallery.Arrange(new Rect(0,0,width*4,height*5));
            using var image=new RenderTargetBitmap(new PixelSize(width*4,height*5),new Vector(96,96));image.Render(gallery);image.Save(Path.Combine(directory,"all-17-panels.png"));
        }
        finally{foreach(var bitmap in bitmaps)bitmap.Dispose();}
    }
    static void Capture(Window window,string name)
    {
        window.UpdateLayout();foreach(var v in window.GetVisualDescendants())v.InvalidateVisual();window.InvalidateVisual();ClockOverlayChecks.Pump(40);
        var root=(Avalonia.Rendering.IRenderRoot)window;
        typeof(Avalonia.Rendering.IRenderer).GetMethod("Paint",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(root.Renderer,new object[]{new Rect(root.ClientSize)});
        using var image=new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width,(int)window.ClientSize.Height),new Vector(96,96));image.Render(window);
        var path=Path.GetFullPath("design/one-piece-v2/todo-overlay/implemented/"+name+".png");Directory.CreateDirectory(Path.GetDirectoryName(path)!);image.Save(path);
    }
}
