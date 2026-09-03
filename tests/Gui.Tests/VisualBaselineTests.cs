using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Moonlight.Gui.Demo;
using Xunit;

namespace Moonlight.Gui.Tests;

public sealed class VisualBaselineTests
{
    private static readonly PixelSize ProductionSize = new(977, 499);
    private static readonly string[] SecondaryViews =
        ["History", "Send", "Receive", "Coins", "Contacts", "Notes", "Calc"];

    [AvaloniaFact]
    public static void PrimaryViewsMatchApprovedMacOsBaselines()
    {
        if (!OperatingSystem.IsMacOS())
            Assert.Skip("The committed visual baselines currently cover the macOS Skia renderer only.");

        string repositoryRoot = RepositoryLayout.Root;
        string baselineDirectory = Path.Combine(
            repositoryRoot,
            "tests",
            "Gui.Tests",
            "Baselines",
            "macos",
            $"{ProductionSize.Width}x{ProductionSize.Height}");
        string artifactDirectory = Path.Combine(
            repositoryRoot,
            "artifacts",
            "gui-eval",
            "macos",
            $"{ProductionSize.Width}x{ProductionSize.Height}");
        bool updateBaselines = IsBaselineUpdateRequested();

        Directory.CreateDirectory(baselineDirectory);
        RecreateDirectory(artifactDirectory);

        var failures = new List<string>();
        var window = new MainWindow();
        try
        {
            window.Show();
            EvaluateView(window, "home", baselineDirectory, artifactDirectory, updateBaselines, failures);

            foreach (string view in SecondaryViews)
            {
                Button tab = window.FindControl<Button>($"{view}Tab")
                    ?? throw new InvalidOperationException($"Missing {view} tab.");
                tab.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                EvaluateView(window, view.ToLowerInvariant(), baselineDirectory, artifactDirectory, updateBaselines, failures);
            }
        }
        finally
        {
            window.Close();
        }

        Assert.True(
            failures.Count == 0,
            "Visual regressions detected:\n"
            + string.Join("\n", failures)
            + $"\nInspect actual, diff, and JSON reports in '{artifactDirectory}'.");
    }

    private static void EvaluateView(
        Window window,
        string name,
        string baselineDirectory,
        string artifactDirectory,
        bool updateBaselines,
        List<string> failures)
    {
        Dispatcher.UIThread.RunJobs();
        using Bitmap frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException($"The {name} view rendered no frame.");
        if (frame.PixelSize != ProductionSize)
        {
            failures.Add($"{name}: rendered {frame.PixelSize}, expected {ProductionSize}");
            return;
        }

        string actualPath = Path.Combine(artifactDirectory, $"{name}-actual.png");
        string baselinePath = Path.Combine(baselineDirectory, $"{name}.png");
        string diffPath = Path.Combine(artifactDirectory, $"{name}-diff.png");
        string reportPath = Path.Combine(artifactDirectory, $"{name}-report.json");
        frame.Save(actualPath, PngBitmapEncoderOptions.Default);

        if (updateBaselines)
        {
            File.Copy(actualPath, baselinePath, true);
            return;
        }

        if (!File.Exists(baselinePath))
        {
            failures.Add(
                $"{name}: baseline is missing at '{baselinePath}'. "
                + "Review the actual image, then explicitly approve it with "
                + "MOONLIGHT_UPDATE_VISUAL_BASELINES=1 dotnet test tests/Gui.Tests/Gui.Tests.csproj.");
            return;
        }

        VisualDiffReport report = VisualRegression.Compare(
            name,
            baselinePath,
            actualPath,
            diffPath,
            reportPath);
        if (!report.Passed)
        {
            failures.Add(
                $"{name}: {report.DifferentPixels}/{report.ComparedPixels} pixels differ "
                + $"({report.DifferentPixelRatio:P3}), MAE {report.MeanAbsoluteError:P4}");
        }
    }

    private static bool IsBaselineUpdateRequested()
    {
        bool requested = string.Equals(
            Environment.GetEnvironmentVariable("MOONLIGHT_UPDATE_VISUAL_BASELINES"),
            "1",
            StringComparison.Ordinal);
        bool continuousIntegration = string.Equals(
            Environment.GetEnvironmentVariable("CI"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        if (requested && continuousIntegration)
            throw new InvalidOperationException("Visual baselines cannot be updated in CI.");
        return requested;
    }

    private static void RecreateDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);
        Directory.CreateDirectory(path);
    }
}
