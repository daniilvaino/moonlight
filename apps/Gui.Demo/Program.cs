using Avalonia;

namespace Moonlight.Gui.Demo;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        UiStateStore.Enabled = true;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<App>()
        .UsePlatformDetect()
        .LogToTrace();
}
