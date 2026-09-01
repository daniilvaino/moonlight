using Moonlight.Wallet;
using Xunit;

namespace Moonlight.Wallet.Tests;

public class StorageTests
{
    // Deliberately weak so the tests stay fast; the format stores the work factor,
    // so a file written with one number opens with any other.
    private const int Fast = 1000;

    [Fact]
    public void RoundTripsAWallet()
    {
        Account account = Account.Create();

        byte[] file = Storage.Encrypt(account, "correct horse", 2_500_000, Fast);
        (Account restored, ulong height, _) = Storage.Decrypt(file, "correct horse");

        Assert.Equal(account.SpendSecret, restored.SpendSecret);
        Assert.Equal(account.ViewSecret, restored.ViewSecret);
        Assert.Equal(account.Address, restored.Address);
        Assert.Equal(2_500_000UL, height);
    }

    [Theory]
    [InlineData(Network.Mainnet)]
    [InlineData(Network.Testnet)]
    [InlineData(Network.Stagenet)]
    public void RemembersTheNetwork(Network network)
    {
        Account account = Account.Create(network);
        (Account restored, _, _) = Storage.Decrypt(Storage.Encrypt(account, "p", 0, Fast), "p");

        Assert.Equal(network, restored.Network);
        Assert.Equal(account.Address.Encode(), restored.Address.Encode());
    }

    [Fact]
    public void AWrongPasswordIsRefused()
    {
        byte[] file = Storage.Encrypt(Account.Create(), "right", 0, Fast);

        Assert.Throws<FormatException>(() => Storage.Decrypt(file, "wrong"));
    }

    /// <summary>
    /// Every byte is authenticated, header included. Flipping one must fail rather
    /// than decrypt into a wallet that is subtly not the one that was saved.
    /// </summary>
    [Fact]
    public void AnyModificationIsRefused()
    {
        byte[] file = Storage.Encrypt(Account.Create(), "p", 12345, Fast);

        for (int i = 0; i < file.Length; i++)
        {
            byte[] tampered = [.. file];
            tampered[i] ^= 0x01;

            Assert.Throws<FormatException>(() => Storage.Decrypt(tampered, "p"));
        }
    }

    /// <summary>
    /// A wrong password and a modified file fail the same way. Distinguishing them
    /// would tell someone holding the file which of the two they are looking at.
    /// </summary>
    [Fact]
    public void TamperingAndAWrongPasswordAreIndistinguishable()
    {
        byte[] file = Storage.Encrypt(Account.Create(), "p", 0, Fast);
        byte[] tampered = [.. file];
        tampered[^1] ^= 0x01;

        string wrongPassword = Assert.Throws<FormatException>(() => Storage.Decrypt(file, "q")).Message;
        string modified = Assert.Throws<FormatException>(() => Storage.Decrypt(tampered, "p")).Message;

        Assert.Equal(wrongPassword, modified);
    }

    /// <summary>
    /// Two files for the same wallet share no bytes beyond the magic: the salt and
    /// the nonce are fresh each time, so saving twice does not reveal that nothing
    /// changed.
    /// </summary>
    [Fact]
    public void EachSaveIsDifferent()
    {
        Account account = Account.Create();

        byte[] first = Storage.Encrypt(account, "p", 100, Fast);
        byte[] second = Storage.Encrypt(account, "p", 100, Fast);

        Assert.NotEqual(first, second);
        Assert.Equal(first.Length, second.Length);
    }

    [Fact]
    public void TheWorkFactorTravelsWithTheFile()
    {
        Account account = Account.Create();

        // Written with one number, opened without being told which.
        (Account restored, _, _) = Storage.Decrypt(Storage.Encrypt(account, "p", 0, 2000), "p");

        Assert.Equal(account.SpendSecret, restored.SpendSecret);
    }

    [Theory]
    [InlineData("")]
    [InlineData("MOONLIGHT")]
    public void RefusesSomethingThatIsNotAWalletFile(string content)
        => Assert.Throws<FormatException>(() => Storage.Decrypt(System.Text.Encoding.ASCII.GetBytes(content), "p"));

    [Fact]
    public void RefusesAnEmptyPassword()
    {
        Assert.Throws<ArgumentException>(() => Storage.Encrypt(Account.Create(), "", 0, Fast));
        Assert.Throws<ArgumentException>(() => Storage.Decrypt(new byte[100], ""));
    }

    /// <summary>
    /// The lookahead travels with the file. A wallet that forgot it would silently
    /// stop watching the subaddresses it had already handed out.
    /// </summary>
    [Fact]
    public void TheSubaddressLookaheadIsRemembered()
    {
        Account account = Account.Create();
        SubaddressIndex wide = new(5, 1000);

        (_, _, SubaddressIndex restored) = Storage.Decrypt(Storage.Encrypt(account, "p", 0, Fast, wide), "p");
        Assert.Equal(wide, restored);

        (_, _, SubaddressIndex fallback) = Storage.Decrypt(Storage.Encrypt(account, "p", 0, Fast), "p");
        Assert.Equal(Storage.DefaultLookahead, fallback);
    }

    /// <summary>The seed survives, which is what makes a wallet file a wallet and not a key blob.</summary>
    [Fact]
    public void ASeededWalletKeepsItsSeed()
    {
        Account account = Account.FromSeed(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

        (Account restored, _, _) = Storage.Decrypt(Storage.Encrypt(account, "p", 0, Fast), "p");

        Assert.Equal(account.Seed, restored.Seed);
    }
}
