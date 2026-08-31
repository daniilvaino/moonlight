using Moonlight.Node;
using Moonlight.Serialization;
using Xunit;

namespace Moonlight.Integration.Tests;

/// <summary>
/// Against a real daemon, when one is offered. Set MOONLIGHT_DAEMON to its
/// address (for example http://127.0.0.1:18081/) to run these; without it they
/// are skipped rather than failing, because a missing node is not a bug.
/// </summary>
public class ChainTests
{
    private static readonly string? Address = Environment.GetEnvironmentVariable("MOONLIGHT_DAEMON");

    private static DaemonClient? Connect() => Address is null ? null : new DaemonClient(new Uri(Address));

    /// <summary>
    /// A date maps to the first block at or after it. The offline estimate must
    /// land at or before that block — early is recoverable, late is not.
    /// </summary>
    [Fact]
    public async Task RestoreHeightAgreesWithTheChain()
    {
        using DaemonClient? client = Connect();
        if (client is null) return;

        foreach (DateTimeOffset date in new[]
        {
            new DateTimeOffset(2016, 3, 20, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
        })
        {
            ulong exact = await Wallet.RestoreHeight.ForDateAsync(client, date);
            ulong estimate = Wallet.RestoreHeight.Estimate(date);

            Assert.True(estimate <= exact, $"estimate {estimate} overshot {exact} for {date:yyyy-MM-dd}");
            Assert.True(exact - estimate < 100_000, $"estimate {estimate} is {exact - estimate} blocks early");
        }
    }

    [Fact]
    public async Task ReportsAHeight()
    {
        using DaemonClient? client = Connect();
        if (client is null) return;

        Assert.True(await client.GetHeightAsync() > 0);
    }

    /// <summary>
    /// The verify-chain idea in miniature: every block we pull must parse to the
    /// last byte, and every transaction in it must hash to the id the daemon
    /// listed. Downloaded blocks are the only corpus that grows on its own.
    /// </summary>
    [Fact]
    public async Task BlocksParseAndTheirTransactionsHashCorrectly()
    {
        using DaemonClient? client = Connect();
        if (client is null) return;

        ulong height = await client.GetHeightAsync();
        int checkedTransactions = 0;

        // A spread rather than a run: consecutive blocks are the same era, and the
        // formats we care about changed with the hard forks.
        foreach (ulong h in new ulong[] { 1, 100_000, 202_612, 1_000_000, 1_400_000, 2_000_000, height - 2 })
        {
            if (h >= height) continue;

            Block block = await client.GetBlockAsync(h);
            Assert.True(block.MinerTransaction.IsCoinbase);

            if (block.TransactionIds.Length == 0) continue;

            string[] ids = [.. block.TransactionIds.Select(id => Convert.ToHexString(id).ToLowerInvariant())];
            byte[][] blobs = await client.GetTransactionsAsync(ids);

            for (int i = 0; i < blobs.Length; i++)
            {
                Transaction tx = TxParser.Parse(blobs[i]);
                Assert.Equal(ids[i], Convert.ToHexString(TxHash.Compute(tx)).ToLowerInvariant());
                checkedTransactions++;
            }
        }

        Assert.True(checkedTransactions > 0, "no transactions were checked");
    }
}
