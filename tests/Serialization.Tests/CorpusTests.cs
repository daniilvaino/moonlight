using System.Text.Json;
using Moonlight.Tests;
using Xunit;

namespace Moonlight.Serialization.Tests;

/// <summary>
/// Real transactions the parser will have to eat byte for byte. Until TxParser
/// exists these assert the corpus itself is intact — a silently truncated vector
/// file is the one failure that makes every later test pass for free.
/// </summary>
public class CorpusTests
{
    [Fact]
    public void TransactionCorpusDecodes()
    {
        using JsonDocument doc = Corpus.Json("blocks", "transactions.json");
        int count = 0;

        foreach (JsonElement tx in doc.RootElement.EnumerateArray())
        {
            byte[] id = Convert.FromHexString(tx.GetProperty("id").GetString()!);
            byte[] blob = Convert.FromHexString(tx.GetProperty("hex").GetString()!);

            Assert.Equal(32, id.Length);
            Assert.NotEmpty(blob);
            count++;
        }

        Assert.Equal(5, count);
    }

    [Fact]
    public void Block202612TransactionsDecode()
    {
        using JsonDocument doc = Corpus.Json("blocks", "block_202612_transactions.txt");
        int count = 0;

        foreach (JsonElement hex in doc.RootElement.EnumerateArray())
        {
            Assert.NotEmpty(Convert.FromHexString(hex.GetString()!));
            count++;
        }

        Assert.Equal(514, count);
    }
}
