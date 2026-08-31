using System.Text.Json;
using Moonlight.Tests;
using Xunit;

namespace Moonlight.RingCT.Tests;

/// <summary>CLSAG reference data from monero-oxide: one signed transaction and its ring.</summary>
public class CorpusTests
{
    [Fact]
    public void ClsagTransactionDecodes()
    {
        using JsonDocument doc = Corpus.Json("clsag", "clsag_tx.json");
        Assert.NotEmpty(Convert.FromHexString(doc.RootElement.GetProperty("hex").GetString()!));
    }

    [Fact]
    public void RingDataDecodes()
    {
        using JsonDocument doc = Corpus.Json("clsag", "ring_data.json");
        int rings = 0;

        foreach (JsonElement ring in doc.RootElement.EnumerateArray())
        {
            int members = 0;

            foreach (JsonElement member in ring.EnumerateArray())
            {
                Assert.Equal(32, Convert.FromHexString(member.GetProperty("key").GetString()!).Length);
                Assert.Equal(32, Convert.FromHexString(member.GetProperty("mask").GetString()!).Length);
                members++;
            }

            Assert.Equal(16, members);
            rings++;
        }

        Assert.Equal(2, rings);
    }
}
