using System.Text.Json;
using SkiaSharp;

namespace Moonlight.Gui.Tests;

internal sealed record VisualDiffOptions(
    byte ChannelTolerance = 8,
    double MaximumDifferentPixelRatio = 0.0025,
    double MaximumMeanAbsoluteError = 0.0005);

internal sealed record VisualDiffReport(
    string Name,
    string ExpectedImage,
    string ActualImage,
    string DiffImage,
    string ExpectedSize,
    string ActualSize,
    byte ChannelTolerance,
    long DifferentPixels,
    long ComparedPixels,
    double DifferentPixelRatio,
    double MeanAbsoluteError,
    double MaximumDifferentPixelRatio,
    double MaximumMeanAbsoluteError,
    bool Passed);

internal static class VisualRegression
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static VisualDiffReport Compare(
        string name,
        string expectedPath,
        string actualPath,
        string diffPath,
        string reportPath,
        VisualDiffOptions? options = null)
    {
        options ??= new VisualDiffOptions();

        using SKBitmap expected = Decode(expectedPath, "expected");
        using SKBitmap actual = Decode(actualPath, "actual");

        int width = Math.Max(expected.Width, actual.Width);
        int height = Math.Max(expected.Height, actual.Height);
        long comparedPixels = (long)width * height;
        long differentPixels = 0;
        long absoluteError = 0;

        using var diff = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool expectedContainsPixel = x < expected.Width && y < expected.Height;
                bool actualContainsPixel = x < actual.Width && y < actual.Height;
                if (!expectedContainsPixel || !actualContainsPixel)
                {
                    differentPixels++;
                    absoluteError += 255 * 4;
                    diff.SetPixel(x, y, SKColors.Red);
                    continue;
                }

                SKColor expectedPixel = expected.GetPixel(x, y);
                SKColor actualPixel = actual.GetPixel(x, y);
                int redDelta = Math.Abs(expectedPixel.Red - actualPixel.Red);
                int greenDelta = Math.Abs(expectedPixel.Green - actualPixel.Green);
                int blueDelta = Math.Abs(expectedPixel.Blue - actualPixel.Blue);
                int alphaDelta = Math.Abs(expectedPixel.Alpha - actualPixel.Alpha);
                int maximumDelta = Math.Max(Math.Max(redDelta, greenDelta), Math.Max(blueDelta, alphaDelta));
                absoluteError += redDelta + greenDelta + blueDelta + alphaDelta;

                if (maximumDelta > options.ChannelTolerance)
                {
                    differentPixels++;
                    byte intensity = (byte)Math.Max(96, maximumDelta);
                    diff.SetPixel(x, y, new SKColor(intensity, 0, intensity, 255));
                }
                else
                {
                    byte luminance = (byte)((actualPixel.Red + actualPixel.Green + actualPixel.Blue) / 9);
                    diff.SetPixel(x, y, new SKColor(luminance, luminance, luminance, 255));
                }
            }
        }

        double differentPixelRatio = comparedPixels == 0 ? 1 : differentPixels / (double)comparedPixels;
        double meanAbsoluteError = comparedPixels == 0 ? 1 : absoluteError / (double)(comparedPixels * 4 * 255);
        bool dimensionsMatch = expected.Width == actual.Width && expected.Height == actual.Height;
        bool passed = dimensionsMatch
            && differentPixelRatio <= options.MaximumDifferentPixelRatio
            && meanAbsoluteError <= options.MaximumMeanAbsoluteError;

        Directory.CreateDirectory(Path.GetDirectoryName(diffPath)!);
        SavePng(diff, diffPath);

        var report = new VisualDiffReport(
            name,
            expectedPath,
            actualPath,
            diffPath,
            $"{expected.Width}x{expected.Height}",
            $"{actual.Width}x{actual.Height}",
            options.ChannelTolerance,
            differentPixels,
            comparedPixels,
            differentPixelRatio,
            meanAbsoluteError,
            options.MaximumDifferentPixelRatio,
            options.MaximumMeanAbsoluteError,
            passed);

        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static SKBitmap Decode(string path, string role)
        => SKBitmap.Decode(path)
            ?? throw new InvalidOperationException($"Could not decode the {role} image at '{path}'.");

    private static void SavePng(SKBitmap bitmap, string path)
    {
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream output = File.Create(path);
        data.SaveTo(output);
    }
}
