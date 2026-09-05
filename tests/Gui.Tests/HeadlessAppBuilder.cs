using Avalonia;
using Avalonia.Headless;
using Moonlight.Gui.Demo;

[assembly: AvaloniaTestApplication(typeof(Moonlight.Gui.Tests.HeadlessAppBuilder))]

namespace Moonlight.Gui.Tests;

public static class HeadlessAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions
        {
            UseHeadlessDrawing = false
        });
}
