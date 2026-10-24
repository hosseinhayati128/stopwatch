using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Controls.Presenters;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StopwatchOverlay.Desktop.Controls;

internal static class BidiTextChecks
{
    public static void Run()
    {
        foreach (var word in new[] { "Ubuntu", "**Ubuntu**", "[Ubuntu](https://example.com)", "`Ubuntu`", "Ubuntu 24.04" })
        {
            var text = TodoRichText.Create("بررسی سرور " + word + " امروز", false);
            text.FontSize = 18;
            text.Measure(new Size(900, double.PositiveInfinity)); text.Arrange(new Rect(0, 0, 900, text.DesiredSize.Height));
            string visible = string.Concat(text.Inlines!.OfType<Run>().Select(run => run.Text));
            double before = Center(text, visible.IndexOf("بررسی", StringComparison.Ordinal), "بررسی".Length);
            double english = Center(text, visible.IndexOf("Ubuntu", StringComparison.Ordinal), "Ubuntu".Length);
            double after = Center(text, visible.IndexOf("امروز", StringComparison.Ordinal), "امروز".Length);
            if (!(before > english && english > after)) throw new Exception($"Persian sentence fragments reordered around {word}: {before}, {english}, {after}.");
            if (!visible.StartsWith("بررسی سرور ") || !visible.EndsWith(" امروز")) throw new Exception("Rendering modified source text.");
        }
        foreach (string sample in new[] { "بررسی سرور **Ubuntu** امروز", "Review **Ubuntu** server today" })
        {
            var hover = TodoRichText.Create(sample, false);
            foreach (double width in new[] { 280d, 360d, 280d })
            {
                hover.Measure(new Size(width, double.PositiveInfinity));
                hover.Arrange(new Rect(0, 0, width, hover.DesiredSize.Height));
                double original = Center(hover, 0, 5);
                for (int iteration = 0; iteration < 8; iteration++)
                {
                    hover.Foreground = iteration % 2 == 0 ? Brushes.White : Brushes.Black;
                    hover.InvalidateMeasure();
                    // Hover hit testing can request a layout before the pending measure/arrange.
                    double duringHover = Center(hover, 0, 5);
                    if (Math.Abs(duringHover - original) > .1) throw new Exception($"Hover moved task alignment before layout: {original} → {duringHover} at width {width}.");
                    hover.Measure(new Size(width, double.PositiveInfinity));
                    hover.Arrange(new Rect(0, 0, width, hover.DesiredSize.Height));
                    if (Math.Abs(Center(hover, 0, 5) - original) > .1) throw new Exception("Task alignment changed after hover layout.");
                }
            }
        }
        var ltr = TodoRichText.Create("Review Ubuntu server today", false);
        ltr.Measure(new Size(900, double.PositiveInfinity)); ltr.Arrange(new Rect(0, 0, 900, ltr.DesiredSize.Height));
        if (Center(ltr, 0, 6) >= Center(ltr, 21, 5)) throw new Exception("English text changed direction.");
        foreach (var (sample, expected) in new[]
        {
            ("۱۲۳ 24.04 — بررسی Ubuntu امروز", FlowDirection.RightToLeft),
            ("🛠️ بررسی Ubuntu امروز", FlowDirection.RightToLeft),
            ("Review سرور today", FlowDirection.LeftToRight),
            ("\u2066Ubuntu\u2069 بررسی امروز", FlowDirection.RightToLeft),
            ("12345", FlowDirection.LeftToRight),
            ("", FlowDirection.LeftToRight)
        })
            if (AutoTextDirection.Detect(sample) != expected) throw new Exception("First strong direction detection failed: " + sample);
        var input = new TextBox { Text = "بررسی Ubuntu امروز", AcceptsReturn = true };
        AutoTextDirection.Attach(input);
        var window = new Window { Content = input, Width = 500, Height = 160, ShowActivated = false };
        window.Show(); Pump();
        if (input.FlowDirection != FlowDirection.RightToLeft || input.GetVisualDescendants().OfType<TextPresenter>().Single().FlowDirection != FlowDirection.RightToLeft) throw new Exception("Editor did not inherit Persian base direction.");
        input.Text = "Review سرور today"; Pump();
        if (input.FlowDirection != FlowDirection.LeftToRight || input.Text != "Review سرور today") throw new Exception("Editing did not switch direction or modified source text.");
        input.Text = "بررسی **Ubuntu** امروز"; Pump();
        if (input.FlowDirection != FlowDirection.RightToLeft || input.Text != "بررسی **Ubuntu** امروز") throw new Exception("Editing Persian switched the text or left LTR mode active.");
        window.Close();
        Console.WriteLine("PASS mixed Persian-English visual sentence order, Markdown and unchanged source");
    }
    private static void Pump()
    {
        using var cancel = new CancellationTokenSource();
        DispatcherTimer.RunOnce(cancel.Cancel, TimeSpan.FromMilliseconds(180));
        Dispatcher.UIThread.MainLoop(cancel.Token); Dispatcher.UIThread.RunJobs();
    }
    private static double Center(TextBlock text, int start, int length)
    {
        var bounds = text.TextLayout.HitTestTextRange(start, length).ToArray();
        return (bounds.Min(rect => rect.Left) + bounds.Max(rect => rect.Right)) / 2;
    }
}
