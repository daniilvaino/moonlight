using Terminal.Gui;

namespace Moonlight.Tui;

internal static class Program
{
    private static void Main()
    {
        SharpOSDriver driver = new();
        Application.Init(driver, new ConsoleMainLoop(driver));

        Toplevel top = Application.Top;
        Window win = new("Moonlight")
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

        Button quit = new("Quit") { X = 2, Y = 3 };
        quit.Clicked += () => Application.RequestStop();

        win.Add(new Label("Wallet skeleton. Screens come next.") { X = 2, Y = 1 });
        win.Add(quit);

        top.Add(new MenuBar([new MenuBarItem("_File", [new MenuItem("_Quit", "", () => Application.RequestStop())])]));
        top.Add(win);

        Application.Run();
        Application.Shutdown();
    }
}
