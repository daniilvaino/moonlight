using Moonlight.Wallet;
using Xunit;

namespace Moonlight.Wallet.Tests;

public class RestoreHeightTests
{
    /// <summary>The one pair we can check offline: block 202612 was mined on 2014-09-04.</summary>
    [Fact]
    public void LandsNearTheAnchor()
    {
        ulong height = RestoreHeight.Estimate(DateTimeOffset.FromUnixTimeSeconds(1_409_804_570));

        Assert.InRange(height, 180_000UL, 202_612UL);
    }

    /// <summary>
    /// The v2 fork was block 1009827, "on or around the 20th of March, 2016" per
    /// monero's own hardfork table. The estimate should agree to within a few weeks
    /// of blocks — this is what tells us the one-minute era is being counted at one
    /// minute.
    /// </summary>
    [Fact]
    public void AgreesWithTheV2ForkDate()
    {
        ulong height = RestoreHeight.Estimate(new DateTimeOffset(2016, 3, 20, 0, 0, 0, TimeSpan.Zero));

        Assert.InRange(height, 970_000UL, 1_009_827UL);
    }

    /// <summary>
    /// Always early, never late. Starting a scan early costs time; starting late
    /// means the wallet never sees its own outputs and reports a wrong balance
    /// with no error anywhere.
    /// </summary>
    [Theory]
    [InlineData(2014, 9, 4, 202_612)]
    [InlineData(2016, 3, 20, 1_009_827)]
    public void NeverOvershootsAKnownHeight(int year, int month, int day, ulong known)
        => Assert.True(RestoreHeight.Estimate(new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero)) <= known);

    [Fact]
    public void IsMonotonic()
    {
        ulong previous = 0;

        for (int year = 2014; year <= 2026; year++)
        {
            ulong height = RestoreHeight.Estimate(new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero));

            Assert.True(height >= previous, $"{year} went backwards");
            previous = height;
        }
    }

    /// <summary>
    /// A wallet made just now cannot own anything older than now, so it starts at
    /// the tip. The height is only known once a daemon answers, which is why the
    /// file carries a marker rather than a number.
    /// </summary>
    [Fact]
    public void ANewWalletStartsAtTheTip()
    {
        Assert.Equal(2_999_999UL, RestoreHeight.Resolve(RestoreHeight.FromTip, 3_000_000));
        Assert.Equal(0UL, RestoreHeight.Resolve(RestoreHeight.FromTip, 0));

        // A real height is left exactly as it is.
        Assert.Equal(1_234UL, RestoreHeight.Resolve(1_234, 3_000_000));
        Assert.Equal(0UL, RestoreHeight.Resolve(0, 3_000_000));
    }

    [Fact]
    public void ClampsBeforeGenesis()
    {
        Assert.Equal(0UL, RestoreHeight.Estimate(DateTimeOffset.FromUnixTimeSeconds(0)));
        Assert.Equal(0UL, RestoreHeight.Estimate(new DateTimeOffset(2013, 1, 1, 0, 0, 0, TimeSpan.Zero)));
    }

    /// <summary>
    /// Roughly a block every two minutes since the fork — a sanity bound rather
    /// than an assertion about the real chain, which drifts.
    /// </summary>
    [Fact]
    public void GrowsAtAboutTheBlockRate()
    {
        ulong start = RestoreHeight.Estimate(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));
        ulong end = RestoreHeight.Estimate(new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero));

        // 366 days at two minutes is 263 520 blocks.
        Assert.InRange(end - start, 260_000UL, 265_000UL);
    }
}
