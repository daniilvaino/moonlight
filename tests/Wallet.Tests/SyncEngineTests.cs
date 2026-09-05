using System.Text.Json;
using Moonlight.Core;
using Moonlight.Crypto;
using Moonlight.Node;
using Moonlight.Serialization;
using Moonlight.Serialization.Epee;
using Moonlight.Tests;
using Moonlight.Wallet;
using Xunit;

namespace Moonlight.Wallet.Tests;

/// <summary>
/// The sync engine, driven by hand. There is no daemon here and no HttpClient —
/// that is the point of the engine not owning the socket: what used to need a stub
/// web server is now a byte array.
/// </summary>
public class SyncEngineTests
{
    private static WalletState Fresh(ulong from = 0)
        => new(new Scanner(Account.Create(), new SubaddressIndex(1, 1)), from);

    private static byte[] Height(ulong height)
        => System.Text.Encoding.UTF8.GetBytes($"{{\"height\":{height},\"status\":\"OK\"}}");

    /// <summary>An empty batch, which is how a daemon says it has nothing more.</summary>
    private static byte[] NoBlocks()
        => PortableWriter.Section(root =>
        {
            root.Bytes("status", "OK"u8);
            root.Number("start_height", 0);
            root.Number("current_height", 0);
        });

    [Fact]
    public void AsksTheChainHeightFirst()
    {
        SyncEngine engine = new(Fresh());

        SyncRequest request = Assert.NotNull(engine.Next());

        Assert.Equal("get_height", request.Path);
        Assert.Equal("{}", System.Text.Encoding.UTF8.GetString(request.Body));
    }

    /// <summary>
    /// Asking twice without answering returns the same request. The engine moves
    /// only when it is told something, which is what makes a lost response a retry
    /// rather than a skipped block.
    /// </summary>
    [Fact]
    public void RepeatsItselfUntilAnswered()
    {
        SyncEngine engine = new(Fresh());

        Assert.Equal(engine.Next()!.Value.Path, engine.Next()!.Value.Path);
    }

    [Fact]
    public void AWalletAtTheTipIsDoneAtOnce()
    {
        SyncEngine engine = new(Fresh(from: 100));

        engine.Supply(Height(100));

        Assert.True(engine.CaughtUp);
        Assert.Null(engine.Next());
        Assert.Equal(100UL, engine.ChainHeight);
    }

    [Fact]
    public void BehindTheTipItGoesForTheGenesisThenBlocks()
    {
        SyncEngine engine = new(Fresh());

        engine.Supply(Height(500));
        Assert.Equal("json_rpc", engine.Next()!.Value.Path);

        // No genesis to be had. That is not fatal: with a start height the daemon
        // answers from there and ignores the locator entirely.
        Assert.True(engine.Failed(new DaemonException("no")));
        Assert.Equal("getblocks.bin", engine.Next()!.Value.Path);
    }

    /// <summary>Only the genesis is optional. A failure anywhere else is the caller's to handle.</summary>
    [Fact]
    public void OtherFailuresAreNotSwallowed()
    {
        SyncEngine engine = new(Fresh());

        Assert.False(engine.Failed(new DaemonException("no")));
    }

    [Fact]
    public void TheBlocksRequestCarriesTheStartHeight()
    {
        SyncEngine engine = new(Fresh(from: 4242));

        engine.Supply(Height(9000));
        engine.Failed(new DaemonException("no"));

        EpeeValue.Section request = PortableStorage.Parse(engine.Next()!.Value.Body);

        Assert.Equal(4242UL, Assert.IsType<EpeeValue.Number>(request["start_height"]).Value);
        Assert.True(Assert.IsType<EpeeValue.Flag>(request["prune"]).Value);
    }

    /// <summary>
    /// A daemon with nothing to give ends the sweep. Asking again would spin on the
    /// same answer forever.
    /// </summary>
    [Fact]
    public void AnEmptyBatchEndsTheSweep()
    {
        SyncEngine engine = new(Fresh());

        engine.Supply(Height(500));
        engine.Failed(new DaemonException("no"));
        engine.Supply(NoBlocks());

        Assert.True(engine.CaughtUp);
        Assert.Null(engine.Next());
    }

    /// <summary>A warm wallet asks again every interval, from the top.</summary>
    [Fact]
    public void RestartingAsksTheHeightAgain()
    {
        SyncEngine engine = new(Fresh(from: 100));

        engine.Supply(Height(100));
        Assert.True(engine.CaughtUp);

        engine.Restart();

        Assert.False(engine.CaughtUp);
        Assert.Equal("get_height", engine.Next()!.Value.Path);
    }

    [Fact]
    public void RefusesAnAnswerToAQuestionItDidNotAsk()
    {
        SyncEngine engine = new(Fresh(from: 100));

        engine.Supply(Height(100));

        Assert.Throws<InvalidOperationException>(() => engine.Supply(Height(100)));
    }

    [Fact]
    public void RefusesAHeightItCannotRead()
    {
        SyncEngine engine = new(Fresh());

        Assert.Throws<DaemonException>(() => engine.Supply("{\"status\":\"OK\"}"u8));
    }

    /// <summary>
    /// The engine reads JSON by hand because the source-generated serializer needs
    /// reflection at run time, and a linked library cannot afford it. This is the
    /// shape monerod actually answers get_height with.
    /// </summary>
    [Fact]
    public void ReadsTheHeightOutOfARealAnswer()
    {
        SyncEngine engine = new(Fresh());

        engine.Supply(
            """{"hash":"abc","height":3753117,"status":"OK","untrusted":false}"""u8);

        Assert.Equal(3753117UL, engine.ChainHeight);
    }

    /// <summary>The genesis request is a JSON-RPC call, and monerod is particular about its shape.</summary>
    [Fact]
    public void TheGenesisRequestIsWellFormedJsonRpc()
    {
        SyncEngine engine = new(Fresh());

        engine.Supply(Height(500));

        using JsonDocument request = JsonDocument.Parse(engine.Next()!.Value.Body);

        Assert.Equal("2.0", request.RootElement.GetProperty("jsonrpc").GetString());
        Assert.Equal("get_block", request.RootElement.GetProperty("method").GetString());
        Assert.Equal(0UL, request.RootElement.GetProperty("params").GetProperty("height").GetUInt64());
    }
}
