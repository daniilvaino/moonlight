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

    /// <summary>
    /// These are transaction *ids*, not blobs — 514 of them, the contents of block
    /// 202612. They are here for the Merkle root, which is where that block is
    /// famous for having two valid hashes.
    /// </summary>
    [Fact]
    public void Block202612IdsDecode()
    {
        using JsonDocument doc = Corpus.Json("blocks", "block_202612_transactions.txt");
        int count = 0;

        foreach (JsonElement hex in doc.RootElement.EnumerateArray())
        {
            Assert.Equal(32, Convert.FromHexString(hex.GetString()!).Length);
            count++;
        }

        Assert.Equal(514, count);
    }
}
