using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace StopwatchOverlay.Desktop.Views;

public partial class SettingsWindow
{
    private ScrollViewer? _habiticaPage;
    private Panel? _habiticaParent;
    private int _habiticaIndex;

    private void BuildHabiticaPage(Grid host)
    {
        _habiticaParent = (Panel)HabiticaPanel.Parent!; _habiticaIndex = _habiticaParent.Children.IndexOf(HabiticaPanel);
        _habiticaParent.Children.Remove(HabiticaPanel);
        HabiticaPanel.SetNavigatorLayout(true);
        _habiticaPage = new ScrollViewer { Name = "HabiticaScroll", Content = HabiticaPanel, Padding = new(0, 0, 5, 0),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        host.Children.Add(_habiticaPage);
    }

    private void RestoreHabiticaPage()
    {
        if (_habiticaPage == null) return;
        _habiticaPage.Content = null; HabiticaPanel.SetNavigatorLayout(false);
        _habiticaParent!.Children.Insert(_habiticaIndex, HabiticaPanel);
        ((Panel)_habiticaPage.Parent!).Children.Remove(_habiticaPage); _habiticaPage = null; _habiticaParent = null;
    }
}
