using Moonlight.Wallet;

namespace Moonlight.Core.Http;

/// <summary>
/// An open wallet with a connection to a node: the session, and a loop that keeps
/// it current. This is what an application wants when it is happy to let the
/// library own the socket.
/// </summary>
/// <remarks>
/// The split is between a wallet and a socket, not between logic and I/O. Every
/// rule about following a chain lives in <see cref="SyncEngine"/> inside the
/// session, and a host that would rather do its own networking drives that engine
/// and gets the same behaviour — including the ones that are easy to get wrong,
/// like settling a restore date only while there is no money to lose.
/// </remarks>
public sealed class WalletConnection : IDisposable
{
    private HttpSync sync;
    private bool disposed;

    private WalletConnection(WalletSession wallet)
    {
        Wallet = wallet;
        sync = new HttpSync(wallet.Daemon, wallet.Engine);
    }

    public WalletSession Wallet { get; }

    public Account Account => Wallet.Account;

    public WalletState State => Wallet.State;

    public Uri Daemon => Wallet.Daemon;

    public WalletFile File => Wallet.File;

    public static WalletConnection Open(string path, string password, string? daemon = null)
        => new(WalletSession.Open(path, password, daemon));

    /// <summary>Catches up once and returns where it got to.</summary>
    public Task<SyncProgress> CatchUpAsync(
        Action<SyncProgress>? onProgress = null,
        CancellationToken cancellationToken = default)
        => sync.CatchUpAsync(onProgress, cancellationToken);

    /// <summary>
    /// Stays caught up until cancelled. This is what makes a wallet warm: it is
    /// current when its owner looks at it, not when they remember to ask.
    /// </summary>
    public Task RunAsync(
        Action<SyncProgress>? onProgress = null,
        Action<Exception>? onError = null,
        TimeSpan? interval = null,
        CancellationToken cancellationToken = default)
        => sync.RunAsync(onProgress, onError, interval, cancellationToken);

    /// <summary>Asks the loop to look now rather than at the end of its interval.</summary>
    public void RefreshNow() => sync.RefreshNow();

    /// <summary>
    /// Points the wallet at another node. Only the connection is replaced: the
    /// engine carries on where it stands, so a different node is not a reason to
    /// read the chain again, and an unsettled restore date is not answered by one.
    /// </summary>
    public void UseDaemon(Uri daemon)
    {
        Wallet.UseDaemon(daemon);

        sync.Dispose();
        sync = new HttpSync(daemon, Wallet.Engine);
    }

    public Balance Balance() => Wallet.Balance();

    public void Save() => Wallet.Save();

    public void Dispose()
    {
        if (disposed) return;

        disposed = true;
        sync.Dispose();
    }
}
