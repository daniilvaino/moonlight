using System.Globalization;

namespace Moonlight.Tui.Screens;

/// <summary>How numbers reach the screen, in one place so every screen agrees.</summary>
internal static class Amounts
{
    /// <summary>Twelve decimal places, and a wallet that rounds them is lying.</summary>
    public static string Format(ulong atomic)
        => (atomic / 1_000_000_000_000m).ToString("0.############", CultureInfo.InvariantCulture);

    /// <summary>
    /// How far a scan has got. ScannedHeight is the next block to read, so the last
    /// one read is one below it — printing it raw gave "block 120 of 119, 100.8%".
    /// </summary>
    public static string Progress(ulong scanned, ulong height)
    {
        ulong last = scanned == 0 ? 0 : scanned - 1;
        ulong tip = height == 0 ? 0 : height - 1;

        return tip == 0
            ? $"block {last}"
            : last >= tip
                ? $"block {last} — up to date"
                : $"block {last} of {tip}   {100.0 * last / tip:F1}%";
    }

    /// <summary>The same thing in the words the status line uses.</summary>
    public static string Activity(ulong scanned, ulong height)
    {
        if (height == 0) return "connecting";

        ulong last = scanned == 0 ? 0 : scanned - 1;
        ulong tip = height - 1;

        return last >= tip
            ? "synchronized"
            : $"scanning {last} of {tip}   {100.0 * last / tip:F1}%";
    }
}
