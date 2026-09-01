using System.Net.Http;
using Moonlight.Node;
using Moonlight.Serialization;

namespace Moonlight.Wallet;

/// <summary>Where a sync has got to, for whoever is watching it.</summary>
public readonly record struct SyncProgress(ulong Height, ulong ChainHeight, int Outputs)
{
    public bool CaughtUp => ChainHeight == 0 || Height + 1 >= ChainHeight;
}

/// <summary>
/// Pulls the chain into a wallet, in batches, and keeps it there. This is the
/// shape wallet2 uses and Feather inherits: fetch until caught up, then wake
/// every so often — or on demand — and fetch whatever is new.
/// </summary>
public sealed class ChainSync : IDisposable
{
    /// <summary>
    /// How long to wait before looking again once caught up. Feather's default is
    /// ten seconds, and a Monero block is two minutes, so this is already generous.
    /// </summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(10);

    private readonly HttpClient http;
    private readonly DaemonClient daemon;
    private readonly WalletState state;
    private readonly SemaphoreSlim wake = new(0);
    private readonly bool ownsClients;

    private byte[]? genesis;

    public ChainSync(Uri address, WalletState state)
        : this(new HttpClient { BaseAddress = address, Timeout = TimeSpan.FromMinutes(2) }, new DaemonClient(address), state)
        => ownsClients = true;

    public ChainSync(HttpClient http, DaemonClient daemon, WalletState state)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(daemon);
        ArgumentNullException.ThrowIfNull(state);

        this.http = http;
        this.daemon = daemon;
        this.state = state;
    }

    /// <summary>
    /// A restore date whose exact block is still unknown, from a wallet restored
    /// with no daemon to hand. The first catch-up puts it to the daemon and clears
    /// it; whoever saves the wallet writes back whatever is here, so the question
    /// is asked once rather than on every open.
    /// </summary>
    public DateTimeOffset? PendingRestoreDate { get; set; }

    /// <summary>Ask the loop to look now rather than at the end of its interval.</summary>
    public void RefreshNow()
    {
        if (wake.CurrentCount == 0) wake.Release();
    }

    /// <summary>
    /// Catches up once and returns. The wallet is left exactly as far along as the
    /// daemon could take it.
    /// </summary>
    public async Task<SyncProgress> CatchUpAsync(
        Action<SyncProgress>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        ulong chainHeight = await daemon.GetHeightAsync(cancellationToken).ConfigureAwait(false);

        // Before anything is read: a wallet restored offline started weeks early on
        // purpose, and this is the moment that guess can be replaced by the block
        // the date actually names. RefineAsync refuses once there is money to lose.
        if (PendingRestoreDate is DateTimeOffset pending)
        {
            await RestoreHeight.RefineAsync(daemon, pending, state, cancellationToken).ConfigureAwait(false);
            PendingRestoreDate = null;
        }

        // Before the first batch, so the screen shows where it is going rather than
        // staying blank until a batch comes back.
        onProgress?.Invoke(new SyncProgress(state.ScannedHeight, chainHeight, state.Outputs.Count()));

        while (state.ScannedHeight < chainHeight)
        {
            if (!await FetchOnceAsync(chainHeight, onProgress, cancellationToken).ConfigureAwait(false)) break;

            onProgress?.Invoke(new SyncProgress(state.ScannedHeight, chainHeight, state.Outputs.Count()));
        }

        return new SyncProgress(state.ScannedHeight, chainHeight, state.Outputs.Count());
    }

    /// <summary>
    /// Stays caught up until cancelled: fetch, then wait for the interval or for
    /// RefreshNow, then fetch again. A wallet left open should be current when its
    /// owner looks at it, not when they remember to ask.
    /// </summary>
    public async Task RunAsync(
        Action<SyncProgress>? onProgress = null,
        Action<Exception>? onError = null,
        TimeSpan? interval = null,
        CancellationToken cancellationToken = default)
    {
        TimeSpan wait = interval ?? DefaultInterval;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                SyncProgress progress = await CatchUpAsync(onProgress, cancellationToken).ConfigureAwait(false);
                onProgress?.Invoke(progress);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                // The loop survives — a daemon that goes away is the ordinary case
                // for a wallet left open — but it never swallows the reason. A
                // background loop that fails silently is indistinguishable from one
                // that is not running, which is the worst thing it could be.
                onError?.Invoke(e);
            }

            try
            {
                await wake.WaitAsync(wait, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// How often a batch reports its own progress. Near the tip a batch is megabytes
    /// of full blocks and takes long enough that a counter moving only between
    /// batches looks stopped.
    /// </summary>
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>One batch. Returns false when the daemon has nothing further to give.</summary>
    private async Task<bool> FetchOnceAsync(
        ulong chainHeight, Action<SyncProgress>? onProgress, CancellationToken cancellationToken)
    {
        genesis ??= await GenesisAsync(cancellationToken).ConfigureAwait(false);
        ulong before = state.ScannedHeight;

        BlockBatch batch = await BinEndpoints.GetBlocksAsync(
            http, state.ScannedHeight, Locator(), cancellationToken).ConfigureAwait(false);

        if (batch.Blocks.Count == 0) return false;

        ulong height = batch.StartHeight;
        long next = Environment.TickCount64 + (long)ProgressInterval.TotalMilliseconds;

        foreach (BlockBundle bundle in batch.Blocks)
        {
            // Blocks already behind us come back when the locator overlaps; skipping
            // them is normal, not an error.
            if (height >= state.ScannedHeight)
            {
                List<Transaction> transactions = [bundle.Block.MinerTransaction, .. bundle.Transactions];
                state.Process(height, transactions, BlockParser.ComputeId(bundle.Block),
                    bundle.Block.AllTransactionIds());
            }

            height++;

            if (onProgress is null || Environment.TickCount64 < next) continue;

            next = Environment.TickCount64 + (long)ProgressInterval.TotalMilliseconds;
            onProgress(new SyncProgress(state.ScannedHeight, chainHeight, state.Outputs.Count()));
        }

        // No advance means the daemon is answering with blocks we already have, and
        // asking again would spin. Stopping is right; the next turn of the loop asks
        // afresh.
        return state.ScannedHeight > before;
    }

    /// <summary>
    /// The block locator: recent ids first, then further apart, then the genesis.
    /// The daemon answers from the newest one it recognises, which is how a wallet
    /// on a reorganised chain is told where it actually stands.
    /// </summary>
    private List<byte[]> Locator()
    {
        List<byte[]> ids = [.. state.RecentBlockIds];

        if (genesis is { Length: 32 }) ids.Add(genesis);

        return ids;
    }

    /// <summary>
    /// The genesis id, for the locator. Asked for rather than hardcoded, since it
    /// differs per network — but never fatal: with a start height the daemon
    /// answers from there and ignores the locator entirely, so a wallet must not
    /// fail to sync because one block would not parse.
    /// </summary>
    private async Task<byte[]> GenesisAsync(CancellationToken cancellationToken)
    {
        try
        {
            Block block = await daemon.GetBlockAsync(0, cancellationToken).ConfigureAwait(false);

            return BlockParser.ComputeId(block);
        }
        catch (Exception e) when (e is FormatException or DaemonException)
        {
            return [];
        }
    }

    public void Dispose()
    {
        wake.Dispose();

        if (!ownsClients) return;

        http.Dispose();
        daemon.Dispose();
    }
}
