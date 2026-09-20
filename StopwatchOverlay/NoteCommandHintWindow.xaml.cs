using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace StopwatchOverlay;

public partial class NoteCommandHintWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    public NoteCommandHintWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var helper = new WindowInteropHelper(this);
        if (helper.Handle != IntPtr.Zero)
        {
            int exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Rect workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - ActualWidth) / 2;
        Top = workArea.Top + 24;
    }
}
