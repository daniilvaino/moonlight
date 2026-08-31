using System.Text.Json;
using Moonlight.Tests;
using Xunit;

namespace Moonlight.Wallet.Tests;

/// <summary>Address vectors from monero-oxide: mainnet/stagenet, subaddress, integrated, featured.</summary>
public class CorpusTests
{
    [Fact]
    public void AddressCorpusIsWellFormed()
    {
        using JsonDocument doc = Corpus.Json("addresses", "featured_addresses.json");
        int count = 0;

        foreach (JsonElement entry in doc.RootElement.EnumerateArray())
        {
            Assert.NotEmpty(entry.GetProperty("address").GetString()!);
            Assert.Equal(32, Convert.FromHexString(entry.GetProperty("spend").GetString()!).Length);
            Assert.Equal(32, Convert.FromHexString(entry.GetProperty("view").GetString()!).Length);
            count++;
        }

        Assert.Equal(24, count);
    }

    [Fact]
    public void NetworksAreTheThreeWeSupport()
    {
        using JsonDocument doc = Corpus.Json("addresses", "featured_addresses.json");

        string[] networks = doc.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("network").GetString()!)
            .Distinct()
            .Order()
            .ToArray();

        Assert.Equal(["Mainnet", "Stagenet", "Testnet"], networks);
    }
}
