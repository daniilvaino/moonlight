using System.Text;
using Moonlight.Core;
using Moonlight.Node;
using Moonlight.Wallet;
using Xunit;

namespace Moonlight.Wallet.Tests;

/// <summary>
/// Settling an offline restore estimate, driven by hand. This used to live in the
/// driver, which meant the engine could not do it and anything driving the engine
/// directly — the C interface included — silently went without.
/// </summary>
public class RestoreDateEngineTests
{
    private static readonly DateTimeOffset Date = new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private static WalletState Fresh(ulong from = 0)
        => new(new Scanner(Account.Create(), new SubaddressIndex(1, 1)), from);

    private static byte[] Height(ulong height)
        => Encoding.UTF8.GetBytes($"{{\"height\":{height},\"status\":\"OK\"}}");

    private static byte[] Header(ulong timestamp)
        => Encoding.UTF8.GetBytes($"{{\"result\":{{\"block_header\":{{\"timestamp\":{timestamp}}}}}}}");

    /// <summary>
    /// A chain where block N was mined at second N, so the block at or after a
    /// timestamp is the block of that number — which makes the answer checkable.
    /// </summary>
    private static ulong Settle(SyncEngine engine, ulong chainHeight)
    {
        engine.Supply(Height(chainHeight));

        int steps = 0;

        while (engine.Next() is SyncRequest request && request.Path == "json_rpc")
        {
            ulong asked = Asked(request.Body);

            // The genesis request is the next stage, not another search step.
            if (asked == 0 && steps > 0) break;

            engine.Supply(Header(asked));

            if (++steps > 64) throw new InvalidOperationException("the search is not narrowing");
        }

        return (ulong)steps;
    }

    private static ulong Asked(byte[] body)
    {
        using System.Text.Json.JsonDocument request = System.Text.Json.JsonDocument.Parse(body);

        return request.RootElement.GetProperty("params").GetProperty("height").GetUInt64();
    }

    [Fact]
    public void FindsTheFirstBlockAtOrAfterTheDate()
    {
        WalletState state = Fresh();
        SyncEngine engine = new(state) { PendingRestoreDate = DateTimeOffset.FromUnixTimeSeconds(700) };

        Settle(engine, 1000);

        Assert.Equal(700UL, state.ScannedHeight);
        Assert.Null(engine.PendingRestoreDate);
    }

    /// <summary>Ten requests for a thousand blocks, not a thousand.</summary>
    [Fact]
    public void NarrowsByHalving()
    {
        SyncEngine engine = new(Fresh()) { PendingRestoreDate = DateTimeOffset.FromUnixTimeSeconds(700) };

        Assert.InRange(Settle(engine, 1000), 1UL, 11UL);
    }

    /// <summary>
    /// The guard that matters. An output was found below the new height, and the
    /// scan that found it would not happen again.
    /// </summary>
    [Fact]
    public void AWalletThatFoundMoneyIsNotMoved()
    {
        Account account = Account.Create();
        WalletState state = new(new Scanner(account, new SubaddressIndex(1, 1)), 100);

        state.Restore(new WalletSnapshot(101, [Output(account)], new Dictionary<string, ulong>()));

        SyncEngine engine = new(state) { PendingRestoreDate = Date };

        engine.Supply(Height(1000));

        // Straight past the search, and the date is not asked again on a later open.
        Assert.Equal("json_rpc", engine.Next()!.Value.Path);
        Assert.Equal(0UL, Asked(engine.Next()!.Value.Body));
        Assert.Null(engine.PendingRestoreDate);
        Assert.Equal(101UL, state.ScannedHeight);
    }

    /// <summary>
    /// Only ever forward. The offline estimate lands early by design, so an answer
    /// pointing backwards means something is wrong, and rescanning is not what it
    /// would cost.
    /// </summary>
    [Fact]
    public void ADateBehindTheScanDoesNotMoveIt()
    {
        WalletState state = Fresh(from: 900);
        SyncEngine engine = new(state) { PendingRestoreDate = DateTimeOffset.FromUnixTimeSeconds(100) };

        Settle(engine, 1000);

        Assert.Equal(900UL, state.ScannedHeight);
        Assert.Null(engine.PendingRestoreDate);
    }

    [Fact]
    public void AWalletWithNoDateGoesStraightToTheGenesis()
    {
        SyncEngine engine = new(Fresh());

        engine.Supply(Height(1000));

        Assert.Equal("json_rpc", engine.Next()!.Value.Path);
        Assert.Equal(0UL, Asked(engine.Next()!.Value.Body));
    }

    [Fact]
    public void RefusesAHeaderItCannotRead()
    {
        SyncEngine engine = new(Fresh()) { PendingRestoreDate = Date };

        engine.Supply(Height(1000));

        Assert.Throws<DaemonException>(() => engine.Supply("{\"result\":{}}"u8));
    }

    private static OwnedOutput Output(Account account)
        => new(100, new byte[32], 0, Crypto.Point.FromSecret(Crypto.Scalar.Random()), 1,
            Crypto.Scalar.Random(), new SubaddressIndex(0, 0), null);
}
