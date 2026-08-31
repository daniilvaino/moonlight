using Moonlight.Crypto;
using Moonlight.Wallet;
using Xunit;

namespace Moonlight.Wallet.Tests;

/// <summary>
/// Subaddresses of the wallet monero's functional tests restore, with the values
/// that repository asserts. Same seed as MnemonicTests, so a mistake in the
/// derivation shows up here rather than as a wallet that quietly cannot be paid.
/// </summary>
public class SubaddressTests
{
    private const string Seed =
        "velvet lymph giddy number token physics poetry unquoted nibs useful sabotage limits " +
        "benches lifestyle eden nitrogen anvil fewest avoid batch vials washing fences goat unquoted";

    private static Account Wallet => Account.FromMnemonic(Seed);

    [Theory]
    [InlineData(0, 0, "42ey1afDFnn4886T7196doS9GPMzexD9gXpsZJDwVjeRVdFCSoHnv7KPbBeGpzJBzHRCAs9UxqeoyFQMYbqSWYTfJJQAWDm")]
    [InlineData(0, 1, "84QRUYawRNrU3NN1VpFRndSukeyEb3Xpv8qZjjsoJZnTYpDYceuUTpog13D7qPxpviS7J29bSgSkR11hFFoXWk2yNdsR9WF")]
    [InlineData(1, 0, "82pP87g1Vkd3LUMssBCumk3MfyEsFqLAaGDf6oxddu61EgSFzt8gCwUD4tr3kp9TUfdPs2CnpD7xLZzyC1Ei9UsW3oyCWDf")]
    [InlineData(1, 1, "87qyoPVaEcWikVBmG1TaP1KumZ3hB3Q5f4wZRjuppNdwYjWzs2RgbLYQgtpdu2YdoTT3EZhiUGaPJQt2FsykeFZbCtaGXU4")]
    [InlineData(1, 2, "87KfgTZ8ER5D3Frefqnrqif11TjVsTPaTcp37kqqKMrdDRUhpJRczeR7KiBmSHF32UJLP3HHhKUDmEQyJrv2mV8yFDCq8eB")]
    [InlineData(2, 0, "8Bdb75y2MhvbkvaBnG7vYP6DCNneLWcXqNmfPmyyDkavAUUgrHQEAhTNK3jEq69kGPDrd3i5inPivCwTvvA12eQ4SJk9iyy")]
    public void MatchesMoneroSubaddresses(uint major, uint minor, string expected)
        => Assert.Equal(expected, Subaddress.Address(Wallet, new SubaddressIndex(major, minor)).Encode());

    /// <summary>The keys monero's wallet reports for the same seed.</summary>
    [Fact]
    public void MatchesTheReportedSecretKeys()
    {
        Account account = Wallet;

        Assert.Equal("148d78d2aba7dbca5cd8f6abcfb0b3c009ffbdbea1ff373d50ed94d78286640e", Hex(account.SpendSecret));
        Assert.Equal("49774391fa5e8d249fc2c5b45dadef13534bf2483dede880dac88f061e809100", Hex(account.ViewSecret));
    }

    [Fact]
    public void IndexZeroIsTheMainAddressAndNotASubaddress()
    {
        Account account = Wallet;
        Address address = Subaddress.Address(account, new SubaddressIndex(0, 0));

        Assert.Equal(AddressKind.Standard, address.Kind);
        Assert.Equal(account.Address, address);
        Assert.Equal(account.SpendSecret, Subaddress.SpendSecret(account, new SubaddressIndex(0, 0)));
    }

    [Fact]
    public void SubaddressesAreDistinct()
    {
        Account account = Wallet;

        HashSet<string> seen = [];
        for (uint major = 0; major < 3; major++)
        {
            for (uint minor = 0; minor < 8; minor++)
            {
                Assert.True(seen.Add(Subaddress.Address(account, new SubaddressIndex(major, minor)).Encode()));
            }
        }
    }

    /// <summary>
    /// The published spend key of a subaddress must be the one its private key
    /// yields — otherwise funds sent there could be seen and never spent.
    /// </summary>
    [Fact]
    public void ThePrivateKeyMatchesThePublishedOne()
    {
        Account account = Wallet;

        foreach (SubaddressIndex index in new SubaddressIndex[] { new(0, 0), new(0, 1), new(1, 0), new(3, 7) })
        {
            (Point spend, _) = Subaddress.Keys(account, index);

            Assert.Equal(spend, Point.FromSecret(Subaddress.SpendSecret(account, index)));
        }
    }

    /// <summary>
    /// A subaddress's view key is a·D, not a·G, so two subaddresses of the same
    /// wallet share nothing an observer can link.
    /// </summary>
    [Fact]
    public void SubaddressViewKeysAreNotTheAccountViewKey()
    {
        Account account = Wallet;
        (Point _, Point view) = Subaddress.Keys(account, new SubaddressIndex(1, 0));

        Assert.NotEqual(account.ViewPublic, view);
    }

    private static string Hex(Scalar scalar) => Convert.ToHexString(scalar.ToBytes()).ToLowerInvariant();
}
