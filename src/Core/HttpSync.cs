using System.Net.Http;
using System.Net.Http.Headers;
using Moonlight.Diagnostics;
using Moonlight.Node;
using Moonlight.Wallet;

namespace Moonlight.Core;

/// <summary>
/// The engine driven over HTTP: the synchronous brother of the state machine, for
/// the applications that are happy to let this library own the socket.
/// </summary>
/// <remarks>
/// All this does is carry bytes. Everything about following a chain — the locator,
/// the batching, the order blocks arrive in, when to stop — is in
/// <see cref="SyncEngine"/>, and a host that would rather do its own networking
/// gets exactly the same behaviour by driving that instead.
/// </remarks>
public sealed class HttpSync : IDisposable
{
    /// <summary>
    /// How long to wait before looking again once caught up. Feather's default is
    /// ten seconds, and a Monero block is two minutes, so this is already generous.
    /// </summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(10);

    private static readonly MediaTypeHeaderValue Json = new("application/json");
    private static readonly MediaTypeHeaderValue Binary = new("application/octet-stream");

    private readonly HttpClient http;
    private readonly DaemonClient daemon;
    private readonly WalletState state;
    private readonly SyncEngine engine;
    private readonly SemaphoreSlim wake = new(0);
    private readonly bool ownsClients;

    public HttpSync(Uri address, WalletState state)
        : this(new HttpClient { BaseAddress = address, Timeout = TimeSpan.FromMinutes(2) }, new DaemonClient(address), state)
        => ownsClients = true;

    public HttpSync(HttpClient http, DaemonClient daemon, WalletState state)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(daemon);
        ArgumentNullException.ThrowIfNull(state);

        this.http = http;
        this.daemon = daemon;
        this.state = state;

        engine = new SyncEngine(state);
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
        engine.Restart();
        engine.Progressed = onProgress;

        bool settled = false;

        try
        {
            while (engine.Next() is SyncRequest request)
            {
                cancellationToken.ThrowIfCancellationRequested();

                byte[] response;

                try
                {
                    response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e) when (e is not OperationCanceledException && engine.Failed(e))
                {
                    // The engine can do without this one — only the genesis, and only
                    // because a start height makes the locator unnecessary.
                    continue;
                }

                engine.Supply(response);

                if (!settled)
                {
                    // After the chain height is known and before a single block is
                    // read: a wallet restored offline started weeks early on purpose,
                    // and this is the moment that guess can be replaced.
                    settled = true;
                    await SettleRestoreDateAsync(cancellationToken).ConfigureAwait(false);
                }

                onProgress?.Invoke(engine.Progress);
            }
        }
        finally
        {
            engine.Progressed = null;
        }

        return engine.Progress;
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
                Log.Error("sync", "catch-up failed", e);
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
    /// Replaces an offline estimate with the block the date actually names. Still
    /// driven from here rather than from the engine: it is a binary search over
    /// block headers, and moving it inside is its own piece of work.
    /// </summary>
    private async Task SettleRestoreDateAsync(CancellationToken cancellationToken)
    {
        if (PendingRestoreDate is not DateTimeOffset pending) return;

        ulong? exact = await RestoreHeight.RefineAsync(daemon, pending, state, cancellationToken).ConfigureAwait(false);
        PendingRestoreDate = null;

        Log.Info("restore", exact is ulong at
            ? $"{pending:yyyy-MM-dd} resolved to block {at}, from an estimate"
            : $"{pending:yyyy-MM-dd} left as estimated — the wallet has already scanned");
    }

    private async Task<byte[]> SendAsync(SyncRequest request, CancellationToken cancellationToken)
    {
        using ByteArrayContent content = new(request.Body);
        content.Headers.ContentType = request.Path.EndsWith(".bin", StringComparison.Ordinal) ? Binary : Json;

        using HttpResponseMessage response = await http.PostAsync(request.Path, content, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        wake.Dispose();

        if (!ownsClients) return;

        http.Dispose();
        daemon.Dispose();
    }
}
