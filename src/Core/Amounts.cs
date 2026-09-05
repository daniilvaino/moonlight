using System.Globalization;

namespace Moonlight.Core;

/// <summary>
/// How amounts and progress reach a screen. Here rather than in each application,
/// because three of them showing the same number differently is three chances to
/// show it wrongly.
/// </summary>
public static class Amounts
{
    /// <summary>Atomic units per XMR. Twelve decimal places, and a wallet that rounds them is lying.</summary>
    public const ulong PerXmr = 1_000_000_000_000;

    public static string Format(ulong atomic)
        => (atomic / (decimal)PerXmr).ToString("0.############", CultureInfo.InvariantCulture);

    /// <summary>
    /// How far a scan has got. ScannedHeight is the next block to read, so the last
    /// one read is one below it — printing it raw gave "block 120 of 119, 100.8%".
    /// </summary>
    public static string Progress(ulong scanned, ulong chainHeight)
    {
        ulong last = LastScanned(scanned);
        ulong tip = chainHeight == 0 ? 0 : chainHeight - 1;

        return tip == 0
            ? $"block {last}"
            : last >= tip
                ? $"block {last} — up to date"
                : $"block {last} of {tip}   {100.0 * last / tip:F1}%";
    }

    /// <summary>The same thing in the words a status line uses.</summary>
    public static string Activity(ulong scanned, ulong chainHeight)
    {
        if (chainHeight == 0) return "connecting";

        ulong last = LastScanned(scanned);
        ulong tip = chainHeight - 1;

        return last >= tip
            ? "synchronized"
            : $"scanning {last} of {tip}   {100.0 * last / tip:F1}%";
    }

    /// <summary>The last block actually read, given the height of the next one.</summary>
    public static ulong LastScanned(ulong scanned) => scanned == 0 ? 0 : scanned - 1;
}
