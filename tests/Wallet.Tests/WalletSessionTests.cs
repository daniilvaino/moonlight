using Moonlight.Core;
using Moonlight.Wallet;
using Xunit;

namespace Moonlight.Wallet.Tests;

/// <summary>
/// Opening a wallet used to be four steps repeated in every application, and they
/// had already drifted. These say what one step now does.
/// </summary>
public sealed class WalletSessionTests : IDisposable
{
    private const int Fast = 1000;

    private readonly string directory = Path.Combine(Path.GetTempPath(), "moonlight-" + Guid.NewGuid().ToString("N"));

    public WalletSessionTests() => Directory.CreateDirectory(directory);

    private string Write(Account account, ulong height = 0, WalletSnapshot? snapshot = null, string? daemon = null)
    {
        string path = Path.Combine(directory, "w.keys");
        WalletDocument document = new()
        {
            Network = account.Network.ToString(),
            Settings = new WalletSettings
            {
                Nodes = daemon is null ? [] : [daemon],
                LookaheadAccounts = 1,
                LookaheadAddresses = 3,
            },
        };

        Storage.Save(path, document, Storage.Encrypt(
            account, Storage.Seal("p", Fast), height, document.Settings.Lookahead, snapshot, daemon,
            document.SettingsFingerprint()));

        return path;
    }

    [Fact]
    public void OpensWithItsScannerAndStateAlreadyArranged()
    {
        Account account = Account.Create();

        WalletSession wallet = WalletSession.Open(Write(account, 4242), "p", "http://node:18081/");

        Assert.Equal(account.Address, wallet.Account.Address);
        Assert.Equal(4242UL, wallet.State.ScannedHeight);
        Assert.Equal(new SubaddressIndex(1, 3), wallet.Lookahead);
    }

    /// <summary>
    /// What was asked for wins over what the wallet remembers, which wins over
    /// localhost. The order was written twice before, in two applications.
    /// </summary>
    [Fact]
    public void TheDaemonAskedForWinsOverTheRememberedOne()
    {
        string path = Write(Account.Create(), daemon: "http://remembered:18081/");

        WalletSession asked = WalletSession.Open(path, "p", "http://asked:18081/");
        Assert.Equal("asked", asked.Daemon.Host);

        WalletSession remembered = WalletSession.Open(path, "p");

        Assert.Equal("remembered", remembered.Daemon.Host);
    }

    [Fact]
    public void SavesTheScanAndTheNodeInUse()
    {
        Account account = Account.Create();
        string path = Write(account, 100);

        WalletSession wallet = WalletSession.Open(path, "p", "http://first:18081/");

        wallet.UseDaemon(new Uri("http://second:18081/"));
        wallet.Save();

        WalletSession again = WalletSession.Open(path, "p");

        Assert.Equal("second", again.Daemon.Host);
        Assert.Equal(100UL, again.State.ScannedHeight);
        Assert.False(again.File.SettingsChangedOutside);
    }

    /// <summary>
    /// Changing node is not an answer to an unresolved restore date, so it survives
    /// the swap. A wallet that dropped it would silently keep its early estimate and
    /// scan weeks of chain for nothing.
    /// </summary>
    [Fact]
    public void ChangingNodeKeepsTheUnresolvedRestoreDate()
    {
        Account account = Account.Create();
        string path = Path.Combine(directory, "d.keys");
        DateTimeOffset date = new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);

        WalletDocument document = new() { Settings = new WalletSettings { LookaheadAccounts = 1, LookaheadAddresses = 3 } };

        Storage.Save(path, document, Storage.Encrypt(
            account, Storage.Seal("p", Fast), 3_146_248, document.Settings.Lookahead, null, null,
            document.SettingsFingerprint(), date));

        WalletSession wallet = WalletSession.Open(path, "p", "http://first:18081/");

        wallet.UseDaemon(new Uri("http://second:18081/"));
        wallet.Save();

        Assert.Equal(date, WalletSession.Open(path, "p").File.PendingRestoreDate);
    }

    /// <summary>
    /// A save writes the current shape, and the fingerprint in it is compared again
    /// on the next open.
    /// </summary>
    /// <remarks>
    /// Carrying the opened document's version forward looks harmless and is not: a
    /// wallet written before the current format keeps a fingerprint that is never
    /// compared, so the check for settings altered by somebody else stays switched
    /// off for the rest of that wallet's life, silently.
    /// </remarks>
    [Fact]
    public void SavingBringsAnOlderWalletUpToTheCurrentFormat()
    {
        Account account = Account.Create();
        string path = Path.Combine(directory, "old.keys");

        // As a previous version wrote it: its own number, and a fingerprint hashed
        // whichever way that version hashed.
        WalletDocument older = new()
        {
            Format = WalletDocument.CurrentFormat - 1,
            Settings = new WalletSettings { LookaheadAccounts = 1, LookaheadAddresses = 3 },
        };

        Storage.Save(path, older, Storage.Encrypt(
            account, Storage.Seal("p", Fast), 10, older.Settings.Lookahead, null, null, [.. new byte[32]]));

        WalletSession opened = WalletSession.Open(path, "p");

        Assert.False(opened.File.SettingsChangedOutside);
        Assert.Equal(WalletDocument.CurrentFormat - 1, opened.File.Document.Format);

        opened.Save();

        WalletSession again = WalletSession.Open(path, "p");

        Assert.Equal(WalletDocument.CurrentFormat, again.File.Document.Format);
        Assert.False(again.File.SettingsChangedOutside);
    }

    [Fact]
    public void RefusesAWalletThatIsNotThere()
        => Assert.Throws<IOException>(
            () => WalletSession.Open(Path.Combine(directory, "absent.keys"), "p"));

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        GC.SuppressFinalize(this);
    }
}
