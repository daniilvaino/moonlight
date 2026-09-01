using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Moonlight.Gui.Demo;

namespace Moonlight.Gui.Tests;

public sealed class VisualCaptureTests
{
    private static readonly string OutputDirectory = Path.Combine(Path.GetTempPath(), "moonlight-gui-eval");

    [AvaloniaFact]
    public static void CaptureEveryPrimaryViewAtReferenceSize()
    {
        Directory.CreateDirectory(OutputDirectory);

        var window = new MainWindow
        {
            Width = 835,
            Height = 469
        };
        try
        {
            window.Show();
            Capture(window, "home");

            foreach (string view in new[] { "History", "Send", "Receive", "Coins", "Notes", "Calc" })
            {
                Button tab = window.FindControl<Button>($"{view}Tab")
                    ?? throw new InvalidOperationException($"Missing {view} tab.");
                tab.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Capture(window, view.ToLowerInvariant());
            }
        }
        finally
        {
            window.Close();
        }
    }

    private static void Capture(Window window, string name)
    {
        using var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException($"The {name} view rendered no frame.");
        if (frame.PixelSize != new PixelSize(835, 469))
            throw new InvalidOperationException($"The {name} frame was {frame.PixelSize}, expected 835×469.");
        frame.Save(Path.Combine(OutputDirectory, $"{name}.png"), PngBitmapEncoderOptions.Default);
    }
}
