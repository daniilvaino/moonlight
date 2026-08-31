using System.Text.Json;
using Moonlight.Tests;
using Xunit;

namespace Moonlight.Wallet.Tests;

/// <summary>
/// monero-oxide's Featured Address vectors — an unofficial extension, not
/// consensus. Kept for their key material and as a record of what the file is;
/// the standard address vectors live in monero_addresses.json.
/// </summary>
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
