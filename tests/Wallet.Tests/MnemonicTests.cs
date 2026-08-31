using Moonlight.Crypto;
using Moonlight.Wallet;
using Xunit;

namespace Moonlight.Wallet.Tests;

public class MnemonicTests
{
    /// <summary>
    /// The wallet monero's own functional tests restore, seed and address both
    /// published in that repository. End to end: 25 words to a seed, a seed to two
    /// key pairs, key pairs to an address string.
    /// </summary>
    private const string MoneroTestSeed =
        "velvet lymph giddy number token physics poetry unquoted nibs useful sabotage limits " +
        "benches lifestyle eden nitrogen anvil fewest avoid batch vials washing fences goat unquoted";

    private const string MoneroTestAddress =
        "42ey1afDFnn4886T7196doS9GPMzexD9gXpsZJDwVjeRVdFCSoHnv7KPbBeGpzJBzHRCAs9UxqeoyFQMYbqSWYTfJJQAWDm";

    [Fact]
    public void RestoresTheWalletMoneroTestsWith()
    {
        Account account = Account.FromMnemonic(MoneroTestSeed);

        Assert.Equal(MoneroTestAddress, account.Address.Encode());
        Assert.Equal(MoneroTestSeed, account.Seed);
    }

    [Fact]
    public void RoundTripsRandomSeeds()
    {
        for (int i = 0; i < 64; i++)
        {
            byte[] seed = Scalar.Random().ToBytes();
            string phrase = Mnemonic.Encode(seed);

            Assert.Equal(25, phrase.Split(' ').Length);
            Assert.Equal(seed, Mnemonic.Decode(phrase));
        }
    }

    /// <summary>
    /// Words are matched on their first three characters, so a seed survives being
    /// typed with the rest of a word wrong — and a wrong prefix is refused.
    /// </summary>
    [Fact]
    public void MatchesOnThePrefixOnly()
    {
        string[] words = MoneroTestSeed.Split(' ');
        words[0] = "velvetiness";

        Assert.Equal(Mnemonic.Decode(MoneroTestSeed), Mnemonic.Decode(string.Join(' ', words)));

        words[0] = "walnut";
        Assert.False(Mnemonic.TryDecode(string.Join(' ', words), out _));
    }

    [Fact]
    public void RejectsAWrongChecksumWord()
    {
        string[] words = MoneroTestSeed.Split(' ');
        words[^1] = words[0];   // a real word from the seed, but the wrong one

        Assert.NotEqual(words[^1], MoneroTestSeed.Split(' ')[^1]);
        Assert.False(Mnemonic.TryDecode(string.Join(' ', words), out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("velvet")]
    [InlineData("not a real seed phrase at all")]
    public void RejectsMalformed(string phrase) => Assert.False(Mnemonic.TryDecode(phrase, out _));

    [Fact]
    public void RejectsATwentyFourWordPhrase()
    {
        string[] words = MoneroTestSeed.Split(' ');

        Assert.False(Mnemonic.TryDecode(string.Join(' ', words[..24]), out _));
    }

    [Fact]
    public void TheViewKeyIsDerivedFromTheSpendKey()
    {
        Account account = Account.FromMnemonic(MoneroTestSeed);

        // One phrase restores both pairs: the view secret is the reduced hash of
        // the spend secret, which is why there is only ever one seed.
        Assert.Equal(
            Scalar.Reduce(Keccak.Hash(account.SpendSecret.ToBytes())),
            account.ViewSecret);
    }

    [Fact]
    public void ViewOnlyAccountsSeeTheSameAddress()
    {
        Account account = Account.Create();
        ViewOnlyAccount viewOnly = account.AsViewOnly();

        Assert.Equal(account.Address, viewOnly.Address);
        Assert.Equal(account.ViewSecret, viewOnly.ViewSecret);
    }
}
