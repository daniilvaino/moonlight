using Moonlight.Wallet;
using Xunit;

namespace Moonlight.Wallet.Tests;

/// <summary>
/// How the file reaches the disk, which matters as much as what is in it: a
/// wallet half-written is a wallet gone.
/// </summary>
public class StorageWriteTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "moonlight-" + Guid.NewGuid().ToString("N"));

    public StorageWriteTests() => Directory.CreateDirectory(directory);

    [Fact]
    public void WritesTheFile()
    {
        string path = Path.Combine(directory, "w.keys");
        Account account = Account.Create();
        WalletSeal seal = Storage.Seal("p", 1000);

        Storage.Save(path, Storage.Encrypt(account, seal, 42));

        Assert.True(File.Exists(path));
        Assert.Equal(account.Address, Storage.Open(File.ReadAllBytes(path), "p").Account.Address);
    }

    /// <summary>
    /// The previous file is kept. A save that goes wrong should cost the last scan,
    /// never the keys.
    /// </summary>
    [Fact]
    public void KeepsThePreviousFileAsABackup()
    {
        string path = Path.Combine(directory, "w.keys");
        Account first = Account.Create();
        Account second = Account.Create();
        WalletSeal seal = Storage.Seal("p", 1000);

        Storage.Save(path, Storage.Encrypt(first, seal));
        Storage.Save(path, Storage.Encrypt(second, seal));

        Assert.Equal(second.Address, Storage.Open(File.ReadAllBytes(path), "p").Account.Address);
        Assert.Equal(first.Address, Storage.Open(File.ReadAllBytes(path + ".bak"), "p").Account.Address);
    }

    /// <summary>Nothing is left behind for the next save to trip over.</summary>
    [Fact]
    public void LeavesNoTemporaryFile()
    {
        string path = Path.Combine(directory, "w.keys");
        WalletSeal seal = Storage.Seal("p", 1000);

        Storage.Save(path, Storage.Encrypt(Account.Create(), seal));
        Storage.Save(path, Storage.Encrypt(Account.Create(), seal));

        Assert.False(File.Exists(path + ".new"));
    }

    /// <summary>
    /// The key is derived once and reused, so a wallet saving as it scans does not
    /// pay for PBKDF2 on every block batch. Each write still gets a fresh nonce.
    /// </summary>
    [Fact]
    public void ASealedWalletSavesWithoutRederiving()
    {
        Account account = Account.Create();
        WalletSeal seal = Storage.Seal("p", 1000);

        byte[] first = Storage.Encrypt(account, seal, 1);
        byte[] second = Storage.Encrypt(account, seal, 2);

        // Same salt, different nonce: the salt sits at a fixed offset in the header.
        Assert.Equal(first.AsSpan(14, 16).ToArray(), second.AsSpan(14, 16).ToArray());
        Assert.NotEqual(first.AsSpan(30, 12).ToArray(), second.AsSpan(30, 12).ToArray());

        Assert.Equal(1UL, Storage.Open(first, "p").Snapshot.ScannedHeight);
        Assert.Equal(2UL, Storage.Open(second, "p").Snapshot.ScannedHeight);
    }

    /// <summary>An opened wallet carries its key, which is what makes later saves cheap.</summary>
    [Fact]
    public void AnOpenedWalletCanBeSavedAgain()
    {
        Account account = Account.Create();
        WalletFile opened = Storage.Open(Storage.Encrypt(account, "p", 5, 1000), "p");

        Assert.NotNull(opened.Seal);

        byte[] again = Storage.Encrypt(account, opened.Seal!, 6);

        Assert.Equal(6UL, Storage.Open(again, "p").Snapshot.ScannedHeight);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        GC.SuppressFinalize(this);
    }
}
