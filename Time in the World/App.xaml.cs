using System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Time_in_the_World;

public partial class App : Application
{
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Window window) return;
        var handle = new WindowInteropHelper(window).Handle;
        var darkMode = 1;
        // Windows 10 20H1+; older builds use attribute 19 instead.
        if (DwmSetWindowAttribute(handle, 20, ref darkMode, sizeof(int)) != 0)
            DwmSetWindowAttribute(handle, 19, ref darkMode, sizeof(int));
        // Match the native caption and border to the app on Windows 11.
        var background = 0x001E1410;
        var foreground = 0x00FAF4F1;
        DwmSetWindowAttribute(handle, 34, ref background, sizeof(int));
        DwmSetWindowAttribute(handle, 35, ref background, sizeof(int));
        DwmSetWindowAttribute(handle, 36, ref foreground, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
