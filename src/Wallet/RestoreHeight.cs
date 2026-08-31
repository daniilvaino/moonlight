using Moonlight.Node;

namespace Moonlight.Wallet;

/// <summary>
/// Where a scan should start. A wallet that scans from block zero reads years of
/// chain to find nothing; one that starts too late misses its own money. Both
/// answers here err early, which is the recoverable direction.
/// </summary>
public static class RestoreHeight
{
    /// <summary>
    /// Block 202612 and the second it was mined at — the one height/timestamp pair
    /// this repository can check without a daemon, since the corpus carries that
    /// block's contents.
    /// </summary>
    private const ulong AnchorHeight = 202_612;

    private const long AnchorTimestamp = 1_409_804_570;   // 2014-09-04

    /// <summary>Blocks targeted one minute until the v2 fork, two minutes after it.</summary>
    private const ulong ForkV2Height = 1_009_827;

    private const int SecondsBeforeFork = 60;

    private const int SecondsAfterFork = 120;

    private static readonly long ForkV2Timestamp =
        AnchorTimestamp + ((long)(ForkV2Height - AnchorHeight) * SecondsBeforeFork);

    /// <summary>A month of blocks, held back against drift. Starting early costs time; starting late loses money.</summary>
    private const ulong Margin = 30 * 24 * 60 * 60 / SecondsAfterFork;

    /// <summary>
    /// An offline guess from a date. Always available and free, but a guess: blocks
    /// are not exactly on target, so it deliberately lands early.
    /// </summary>
    public static ulong Estimate(DateTimeOffset date)
    {
        long timestamp = date.ToUnixTimeSeconds();

        ulong height = timestamp < ForkV2Timestamp
            ? Before(timestamp)
            : ForkV2Height + (ulong)((timestamp - ForkV2Timestamp) / SecondsAfterFork);

        return height > Margin ? height - Margin : 0;
    }

    private static ulong Before(long timestamp)
    {
        long blocks = (timestamp - AnchorTimestamp) / SecondsBeforeFork;

        if (blocks >= 0) return AnchorHeight + (ulong)blocks;

        ulong back = (ulong)Math.Min(-blocks, (long)AnchorHeight);
        return AnchorHeight - back;
    }

    /// <summary>
    /// The exact answer, from the chain itself: the first block whose timestamp is
    /// at or after the date. Binary search over block headers, the same way wallet2
    /// does it.
    /// </summary>
    public static async Task<ulong> ForDateAsync(
        DaemonClient daemon,
        DateTimeOffset date,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(daemon);

        ulong target = (ulong)Math.Max(date.ToUnixTimeSeconds(), 0);
        ulong low = 0;
        ulong high = await daemon.GetHeightAsync(cancellationToken).ConfigureAwait(false) - 1;

        while (low < high)
        {
            ulong middle = low + ((high - low) / 2);
            BlockHeaderInfo header = await daemon.GetBlockHeaderAsync(middle, cancellationToken).ConfigureAwait(false);

            if (header.Timestamp < target)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }
}
