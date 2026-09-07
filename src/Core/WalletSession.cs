using Moonlight.Diagnostics;
using Moonlight.Wallet;

namespace Moonlight.Core;

/// <summary>
/// An open wallet: its keys, what a scan has found, the node it talks to, and the
/// file it came from. One of these is what an application drives; everything below
/// it — the scanner, the chain sync, the storage format — is arranged here rather
/// than in each application.
/// </summary>
/// <remarks>
/// Before this, opening a wallet meant repeating four steps in every application:
/// read the file, build a scanner from the account and its lookahead, restore the
/// snapshot into a state, and start a sync carrying whatever restore date was still
/// unresolved. The command line and the terminal interface each had their own copy,
/// and they had already drifted — one saved the pending date on exit and the other
/// did not.
/// </remarks>
public sealed class WalletSession
{
    private readonly string path;

    private WalletSession(string path, WalletFile file, Uri daemon)
    {
        this.path = path;
        File = file;
        Daemon = daemon;

        Account = file.Account;
        Lookahead = file.Lookahead;

        State = new WalletState(new Scanner(file.Account, file.Lookahead), file.Snapshot.ScannedHeight);
        State.Restore(file.Snapshot);

        // One engine per wallet, owned here. Whoever drives it — a connection over
        // HTTP, or a host through the C interface — moves the same one, so what it
        // settles is what a save writes down.
        Engine = new SyncEngine(State) { PendingRestoreDate = file.PendingRestoreDate };
    }

    public Account Account { get; }

    public SubaddressIndex Lookahead { get; }

    public WalletState State { get; }

    /// <summary>Following the chain, without the socket. Drive it yourself, or let RunAsync do it.</summary>
    public SyncEngine Engine { get; }

    /// <summary>The file as it was opened. Its settings are the ones a save carries forward.</summary>
    public WalletFile File { get; }

    public Uri Daemon { get; private set; }

    /// <summary>Where the file lives, so a save needs nothing passed to it.</summary>
    public string Path => path;

    /// <summary>
    /// Opens a wallet file. The daemon is resolved from what was asked for, then
    /// what the wallet remembers, then the environment, then localhost.
    /// </summary>
    public static WalletSession Open(string path, string password, string? daemon = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (!System.IO.File.Exists(path)) throw new IOException($"no wallet at {path}");

        WalletFile file = Storage.OpenDocument(System.IO.File.ReadAllBytes(path), password);

        Uri address = DaemonAddress.Resolve(
            daemon,
            file.Document.Settings.Nodes.FirstOrDefault() ?? file.Daemon);

        Log.Info("wallet", $"opened {System.IO.Path.GetFileName(path)} at block {file.Snapshot.ScannedHeight}");
        Log.Info("node", $"daemon {address}");

        if (file.SettingsChangedOutside)
        {
            // Not refused: editing the file by hand is allowed, and being redirected
            // to somebody else's node without noticing is what this is watching for.
            Log.Warn("wallet", "the settings in this file were changed outside the wallet");
        }

        return new WalletSession(path, file, address);
    }

    /// <summary>
    /// Records which node this wallet is using, so a save writes it down. Nothing
    /// here opens a connection: how bytes reach that address is the business of
    /// whoever is driving the engine.
    /// </summary>
    public void UseDaemon(Uri daemon)
    {
        ArgumentNullException.ThrowIfNull(daemon);

        Daemon = daemon;
        Log.Info("node", $"daemon {daemon}");
    }

    public Balance Balance() => State.BalanceAt(Amounts.LastScanned(State.ScannedHeight));

    /// <summary>
    /// Writes the wallet: the scan so far, the node in use, and whatever settings
    /// the file already carried.
    /// </summary>
    public void Save()
    {
        WalletDocument document = File.Document with
        {
            // Whatever shape it was opened in, it is being written in this one. Left
            // to carry the old number, a wallet would keep a fingerprint nothing ever
            // compares — the tamper check quietly switched off for the rest of its
            // life, with nothing to show for it.
            Format = WalletDocument.CurrentFormat,
            Network = Account.Network.ToString(),
            Settings = File.Document.Settings with { Nodes = [Daemon.ToString()] },
        };

        Storage.Save(path, document, Storage.Encrypt(
            Account,
            File.Seal!,
            State.ScannedHeight,
            Lookahead,
            State.Snapshot(),
            Daemon.ToString(),
            document.SettingsFingerprint(),
            Engine.PendingRestoreDate));
    }
}
