using System.Text.Json;
using Moonlight.Diagnostics;
using Moonlight.Node;
using Moonlight.Serialization;
using Moonlight.Wallet;

namespace Moonlight.Core;

/// <summary>Where a sync has got to, for whoever is watching it.</summary>
public readonly record struct SyncProgress(ulong Height, ulong ChainHeight, int Outputs)
{
    public bool CaughtUp => ChainHeight == 0 || Height + 1 >= ChainHeight;
}

/// <summary>What the engine wants sent, and where.</summary>
/// <remarks>
/// A path relative to the daemon's address and a body, because that is all monerod
/// needs and all a host has to be able to do. Every request here is a POST.
/// </remarks>
public readonly record struct SyncRequest(string Path, byte[] Body);

/// <summary>
/// Following the chain, with the socket left to whoever is driving. Ask it for the
/// next request, send that however you like, hand back the answer, repeat.
/// </summary>
/// <remarks>
/// Written this way because the transport belongs to the host. A wallet linked into
/// a Swift or Rust application should use that application's networking — its trust
/// store, its proxy settings, its idea of a timeout — and once the socket is the
/// host's, the loop may as well be too: a thread of ours whose only job is to call
/// back into the host would be a thread for nothing.
///
/// What stays here is everything that is actually hard and worth not writing twice:
/// the block locator, the batching, the order blocks must arrive in, and the rule
/// that a batch which does not advance means stop rather than ask again.
///
/// It is deliberately not async. There is nothing to await when the caller does the
/// waiting, and an interface with no Task in it crosses a C ABI unchanged.
/// </remarks>
public sealed class SyncEngine
{
    private enum Stage
    {
        /// <summary>How far the chain goes, which frames everything after it.</summary>
        ChainHeight,

        /// <summary>The genesis id for the locator. Asked once, and never fatal.</summary>
        Genesis,

        Blocks,
        CaughtUp,
    }

    /// <summary>Milliseconds between progress reports while a batch is being read.</summary>
    private const long ProgressInterval = 250;

    private readonly WalletState state;
    private Stage stage = Stage.ChainHeight;
    private byte[] genesis = [];

    public SyncEngine(WalletState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        this.state = state;
    }

    /// <summary>
    /// Called while a batch is being read, at most every quarter second. Near the
    /// tip a batch is megabytes of blocks and takes long enough that a counter
    /// moving only between batches looks stopped.
    /// </summary>
    public Action<SyncProgress>? Progressed { get; set; }

    /// <summary>The tip as the daemon last reported it.</summary>
    public ulong ChainHeight { get; private set; }

    public ulong ScannedHeight => state.ScannedHeight;

    public bool CaughtUp => stage == Stage.CaughtUp;

    public SyncProgress Progress => new(state.ScannedHeight, ChainHeight, state.Outputs.Count());

    /// <summary>
    /// What to send next, or null when there is nothing more to ask. Calling it
    /// twice without supplying an answer returns the same request: the engine moves
    /// only when it is told something.
    /// </summary>
    public SyncRequest? Next() => stage switch
    {
        Stage.ChainHeight => new SyncRequest("get_height", "{}"u8.ToArray()),
        Stage.Genesis => new SyncRequest("json_rpc", GetBlockRequest(0)),
        Stage.Blocks => new SyncRequest("getblocks.bin", BinEndpoints.BuildGetBlocksRequest(state.ScannedHeight, Locator())),
        _ => null,
    };

    /// <summary>
    /// The answer to the last request. Throws if the daemon said something that
    /// cannot be read — a wallet that carried on from an unreadable answer would be
    /// guessing at where it stands on the chain.
    /// </summary>
    public void Supply(ReadOnlySpan<byte> response)
    {
        switch (stage)
        {
            case Stage.ChainHeight:
                ChainHeight = ReadHeight(response);
                stage = state.ScannedHeight < ChainHeight ? Stage.Genesis : Stage.CaughtUp;
                break;

            case Stage.Genesis:
                genesis = ReadGenesisId(response);
                stage = Stage.Blocks;
                break;

            case Stage.Blocks:
                if (!ReadBlocks(response)) stage = Stage.CaughtUp;
                break;

            default:
                throw new InvalidOperationException("nothing was asked for");
        }
    }

    /// <summary>
    /// A request that failed. The genesis is the one the engine can do without: with
    /// a start height the daemon answers from there and ignores the locator, so a
    /// block that will not parse must not stop a wallet from syncing.
    /// </summary>
    public bool Failed(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (stage != Stage.Genesis) return false;

        Log.Debug("sync", $"genesis unavailable, syncing without it: {error.Message}");
        genesis = [];
        stage = Stage.Blocks;

        return true;
    }

    /// <summary>Starts again from the top, which is what a warm wallet does every interval.</summary>
    public void Restart() => stage = Stage.ChainHeight;

    /// <summary>
    /// The block locator: recent ids first, then the genesis. The daemon answers
    /// from the newest one it recognises, which is how a wallet on a reorganised
    /// chain is told where it actually stands.
    /// </summary>
    private List<byte[]> Locator()
    {
        List<byte[]> ids = [.. state.RecentBlockIds];

        if (genesis is { Length: 32 }) ids.Add(genesis);

        return ids;
    }

    private bool ReadBlocks(ReadOnlySpan<byte> response)
    {
        BlockBatch batch = BinEndpoints.ParseBatch(response);

        if (batch.Blocks.Count == 0)
        {
            Log.Debug("sync", $"daemon sent nothing from block {state.ScannedHeight}");
            return false;
        }

        Log.Debug("sync", $"{batch.Blocks.Count} blocks from {batch.StartHeight}");

        ulong before = state.ScannedHeight;
        ulong height = batch.StartHeight;
        long next = Environment.TickCount64 + ProgressInterval;

        foreach (BlockBundle bundle in batch.Blocks)
        {
            // Blocks already behind us come back when the locator overlaps; skipping
            // them is normal, not an error.
            if (height >= state.ScannedHeight)
            {
                List<Transaction> transactions = [bundle.Block.MinerTransaction, .. bundle.Transactions];
                state.Process(height, transactions, BlockParser.ComputeId(bundle.Block), bundle.Block.AllTransactionIds());
            }

            height++;

            if (Progressed is null || Environment.TickCount64 < next) continue;

            next = Environment.TickCount64 + ProgressInterval;
            Progressed(Progress);
        }

        if (batch.ChainHeight > ChainHeight) ChainHeight = batch.ChainHeight;

        // No advance means the daemon is answering with blocks we already have, and
        // asking again would spin.
        return state.ScannedHeight > before && state.ScannedHeight < ChainHeight;
    }

    /// <summary>
    /// Read by hand rather than deserialized. The source-generated serializer needs
    /// reflection at run time — measured: it throws under a build with reflection
    /// disabled, while the reader does not — and reflection is what a small linked
    /// library cannot afford.
    /// </summary>
    private static ulong ReadHeight(ReadOnlySpan<byte> response)
    {
        Utf8JsonReader reader = new(response);

        while (reader.Read())
        {
            if (reader.TokenType != JsonTokenType.PropertyName) continue;
            if (!reader.ValueTextEquals("height"u8)) continue;

            reader.Read();
            return reader.GetUInt64();
        }

        throw new DaemonException("get_height said nothing about a height");
    }

    private static byte[] GetBlockRequest(ulong height)
    {
        using System.IO.MemoryStream stream = new();

        using (Utf8JsonWriter writer = new(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("jsonrpc", "2.0");
            writer.WriteString("id", "0");
            writer.WriteString("method", "get_block");
            writer.WriteStartObject("params");
            writer.WriteNumber("height", height);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static byte[] ReadGenesisId(ReadOnlySpan<byte> response)
    {
        using JsonDocument document = JsonDocument.Parse(response.ToArray());

        if (document.RootElement.TryGetProperty("error", out JsonElement error))
        {
            throw new DaemonException($"get_block failed: {error}");
        }

        if (!document.RootElement.TryGetProperty("result", out JsonElement result) ||
            !result.TryGetProperty("blob", out JsonElement blob) ||
            blob.GetString() is not string hex)
        {
            throw new DaemonException("get_block returned no block");
        }

        return BlockParser.ComputeId(BlockParser.Parse(Convert.FromHexString(hex)));
    }
}
