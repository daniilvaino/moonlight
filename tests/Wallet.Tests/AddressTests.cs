using System.Text.Json;
using Moonlight.Crypto;
using Moonlight.Tests;
using Moonlight.Wallet;
using Xunit;

namespace Moonlight.Wallet.Tests;

public class AddressTests
{
    /// <summary>
    /// Addresses monero and monero-oxide print in their own tests: standard,
    /// integrated and subaddress. Each round trip exercises Base58, the prefix
    /// table, the checksum and both key decompressions at once.
    /// </summary>
    [Fact]
    public void RoundTripsRealAddresses()
    {
        int n = 0;

        foreach (JsonElement entry in Corpus.Json("addresses", "monero_addresses.json").RootElement.EnumerateArray())
        {
            string text = entry.GetProperty("address").GetString()!;

            Assert.True(Address.TryParse(text, out Address address), $"failed to parse {text}");
            Assert.Equal(text, address.Encode());

            Assert.Equal(entry.GetProperty("spend").GetString(), Hex(address.SpendKey.ToBytes()));
            Assert.Equal(entry.GetProperty("view").GetString(), Hex(address.ViewKey.ToBytes()));
            Assert.Equal(entry.GetProperty("network").GetString(), address.Network.ToString());
            Assert.Equal(entry.GetProperty("kind").GetString(), address.Kind.ToString());

            if (entry.TryGetProperty("payment_id", out JsonElement paymentId))
            {
                Assert.Equal(paymentId.GetString(), Hex(address.PaymentId!));
            }

            n++;
        }

        Assert.Equal(4, n);
    }

    /// <summary>
    /// A single wrong character must be refused. The checksum is four bytes of
    /// Keccak, which is what stops a mistyped address from being a valid address
    /// belonging to nobody.
    /// </summary>
    [Fact]
    public void RejectsCorruption()
    {
        string text = Corpus.Json("addresses", "monero_addresses.json")
            .RootElement[0].GetProperty("address").GetString()!;

        for (int i = 0; i < text.Length; i++)
        {
            char[] corrupted = text.ToCharArray();
            corrupted[i] = corrupted[i] == '1' ? '2' : '1';

            Assert.False(Address.TryParse(new string(corrupted), out _), $"accepted corruption at {i}");
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]                       // not in the alphabet
    [InlineData("11")]                      // monero's own too-short cases
    [InlineData("111")]
    [InlineData("11111")]
    [InlineData("999999")]
    [InlineData("ZZZZZZ")]
    [InlineData("4AdUndXHHZ6cfufTMvppY6")]  // right alphabet, wrong length
    public void RejectsMalformed(string text) => Assert.False(Address.TryParse(text, out _));

    [Theory]
    [InlineData(Network.Mainnet, AddressKind.Standard)]
    [InlineData(Network.Mainnet, AddressKind.Subaddress)]
    [InlineData(Network.Testnet, AddressKind.Standard)]
    [InlineData(Network.Testnet, AddressKind.Subaddress)]
    [InlineData(Network.Stagenet, AddressKind.Standard)]
    [InlineData(Network.Stagenet, AddressKind.Subaddress)]
    public void EncodesEveryNetworkAndKind(Network network, AddressKind kind)
    {
        Address address = new(network, kind, Point.FromSecret(Scalar.Random()), Point.FromSecret(Scalar.Random()));
        Address parsed = Address.Parse(address.Encode());

        Assert.Equal(address, parsed);
        Assert.Equal(network, parsed.Network);
        Assert.Equal(kind, parsed.Kind);
    }

    [Fact]
    public void IntegratedAddressesRequireAPaymentId()
    {
        Point key = Point.FromSecret(Scalar.Random());

        Assert.Throws<InvalidOperationException>(() =>
            new Address(Network.Mainnet, AddressKind.Integrated, key, key).Encode());

        Assert.Throws<InvalidOperationException>(() =>
            new Address(Network.Mainnet, AddressKind.Standard, key, key, new byte[8]).Encode());
    }

    /// <summary>
    /// The prefixes were chosen so the first character tells a human what they are
    /// looking at. Getting one wrong produces addresses that look plausible and
    /// belong to another network.
    /// </summary>
    [Fact]
    public void TheFirstCharacterIdentifiesNetworkAndKind()
    {
        Point key = Point.FromSecret(Scalar.Random());

        Assert.StartsWith("4", new Address(Network.Mainnet, AddressKind.Standard, key, key).Encode(), StringComparison.Ordinal);
        Assert.StartsWith("8", new Address(Network.Mainnet, AddressKind.Subaddress, key, key).Encode(), StringComparison.Ordinal);
        Assert.StartsWith("5", new Address(Network.Stagenet, AddressKind.Standard, key, key).Encode(), StringComparison.Ordinal);
        Assert.StartsWith("9", new Address(Network.Testnet, AddressKind.Standard, key, key).Encode(), StringComparison.Ordinal);
    }

    /// <summary>
    /// featured_addresses.json is an unofficial extension — kayabaNerve's Featured
    /// Addresses, with a flags byte and its own prefixes. It is not consensus and
    /// this wallet does not parse it; the file stays as a reminder of what it is.
    /// </summary>
    [Fact]
    public void FeaturedAddressesAreNotMoneroAddresses()
    {
        JsonElement featured = Corpus.Json("addresses", "featured_addresses.json").RootElement[0];

        Assert.False(Address.TryParse(featured.GetProperty("address").GetString()!, out _));
    }

    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
}
