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

        if (state.ScannedHeight == RestoreHeight.FromTip)
        {
            state.SkipTo(RestoreHeight.Resolve(RestoreHeight.FromTip, chainHeight));
        }

        while (state.ScannedHeight < chainHeight)
        {
            if (!await FetchOnceAsync(cancellationToken).ConfigureAwait(false)) break;

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
            catch (Exception e) when (e is HttpRequestException or DaemonException or FormatException or TaskCanceledException)
            {
                // A daemon that goes away is the ordinary case for a wallet left
                // open. Keep the loop; the next turn will find it or not.
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

    /// <summary>One batch. Returns false when the daemon has nothing further to give.</summary>
    private async Task<bool> FetchOnceAsync(CancellationToken cancellationToken)
    {
        genesis ??= await GenesisAsync(cancellationToken).ConfigureAwait(false);

        BlockBatch batch = await BinEndpoints.GetBlocksAsync(
            http, state.ScannedHeight, Locator(), cancellationToken).ConfigureAwait(false);

        if (batch.Blocks.Count == 0) return false;

        ulong height = batch.StartHeight;

        foreach (BlockBundle bundle in batch.Blocks)
        {
            // Blocks already behind us come back when the locator overlaps; skipping
            // them is normal, not an error.
            if (height >= state.ScannedHeight)
            {
                List<Transaction> transactions = [bundle.Block.MinerTransaction, .. bundle.Transactions];
                state.Process(height, transactions, BlockParser.ComputeId(bundle.Block));
            }

            height++;
        }

        return height > state.ScannedHeight || batch.Blocks.Count > 0;
    }

    /// <summary>
    /// The block locator: recent ids first, then further apart, then the genesis.
    /// The daemon answers from the newest one it recognises, which is how a wallet
    /// on a reorganised chain is told where it actually stands.
    /// </summary>
    private List<byte[]> Locator()
    {
        List<byte[]> ids = [.. state.RecentBlockIds];

        if (genesis is not null) ids.Add(genesis);

        return ids;
    }

    private async Task<byte[]> GenesisAsync(CancellationToken cancellationToken)
    {
        // Asked for rather than hardcoded: it differs per network, and the daemon
        // is right there.
        Block block = await daemon.GetBlockAsync(0, cancellationToken).ConfigureAwait(false);

        return BlockParser.ComputeId(block);
    }

    public void Dispose()
    {
        wake.Dispose();

        if (!ownsClients) return;

        http.Dispose();
        daemon.Dispose();
    }
}
