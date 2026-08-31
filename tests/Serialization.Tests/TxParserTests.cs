using System.Text.Json;
using Moonlight.Serialization;
using Moonlight.Tests;
using Xunit;

namespace Moonlight.Serialization.Tests;

public class TxParserTests
{
    /// <summary>
    /// The id is the end-to-end check: it is computed from byte ranges the parser
    /// decided, so a boundary off by one anywhere gives a different hash. The corpus
    /// covers both hashing rules — four v2 and one v1.
    /// </summary>
    [Fact]
    public void ComputesTheIdsOfRealTransactions()
    {
        int v1 = 0, v2 = 0;

        foreach ((string id, byte[] blob) in Transactions())
        {
            Transaction tx = TxParser.Parse(blob);

            Assert.Equal(id, Hex(TxHash.Compute(tx)));
            if (tx.Version == 1) v1++; else v2++;
        }

        Assert.Equal(1, v1);
        Assert.Equal(4, v2);
    }

    [Fact]
    public void CoinbaseIsRecognised()
    {
        Transaction coinbase = TxParser.Parse(Transactions().First().Blob);

        Assert.True(coinbase.IsCoinbase);
        Assert.IsType<TxIn.Gen>(coinbase.Inputs[0]);
    }

    /// <summary>
    /// v1 signatures are sized by the ring, so the parser knows where the blob ends
    /// and can refuse anything after it. A v2 blob cannot be checked this way until
    /// the proofs are parsed: trailing bytes are indistinguishable from proof bytes,
    /// which is why the id — not the parse — is what proves a v2 boundary.
    /// </summary>
    [Fact]
    public void RejectsTrailingBytesAfterAV1Transaction()
    {
        byte[] blob = Transactions().First(t => t.Blob[0] == 1).Blob;

        Assert.Equal(1UL, TxParser.Parse(blob).Version);
        Assert.Throws<FormatException>(() => TxParser.Parse([.. blob, 0x00]));
    }

    /// <summary>
    /// Cut inside the prefix, where the parser knows the shape it is looking for.
    /// Cutting later only shortens what a v2 blob calls its proofs — see above.
    /// </summary>
    [Fact]
    public void RejectsTruncationInsideThePrefix()
    {
        foreach ((_, byte[] blob) in Transactions())
        {
            int prefix = TxParser.Parse(blob).PrefixLength;

            for (int cut = 1; cut < prefix; cut += Math.Max(prefix / 8, 1))
            {
                byte[] truncated = blob[..cut];
                Assert.ThrowsAny<Exception>(() => TxParser.Parse(truncated));
            }
        }
    }

    [Fact]
    public void RingCtBaseHasTheShapeItsTypeImplies()
    {
        using JsonDocument doc = Corpus.Json("clsag", "clsag_tx.json");
        Transaction tx = TxParser.Parse(Convert.FromHexString(doc.RootElement.GetProperty("hex").GetString()!));

        Assert.Equal(2UL, tx.Version);
        Assert.NotNull(tx.Rct);

        // Named clsag_tx upstream, but the type byte says Bulletproof+ — CLSAG is
        // the ring signature it carries, not the RingCT type.
        Assert.Equal(RctBase.BulletproofPlus, tx.Rct!.Type);

        // From Bulletproof2 on the mask is derived, not sent, and the amount is
        // truncated to eight bytes.
        Assert.All(tx.Rct.EcdhInfo, e =>
        {
            Assert.Null(e.Mask);
            Assert.Equal(8, e.Amount.Length);
        });

        Assert.Equal(tx.Outputs.Length, tx.Rct.OutPk.Length);
        Assert.Empty(tx.Rct.PseudoOuts);
        Assert.NotEqual(0UL, tx.Rct.Fee);
    }

    [Fact]
    public void OutputsCarryKeysAndInputsCarryImages()
    {
        foreach ((_, byte[] blob) in Transactions())
        {
            Transaction tx = TxParser.Parse(blob);

            Assert.All(tx.Outputs, o => Assert.Equal(32, o.Target switch
            {
                TxOutTarget.ToKey k => k.Key.Length,
                TxOutTarget.ToTaggedKey t => t.Key.Length,
                _ => 0,
            }));

            foreach (TxIn input in tx.Inputs.OfType<TxIn.ToKey>())
            {
                TxIn.ToKey key = (TxIn.ToKey)input;
                Assert.Equal(32, key.KeyImage.Length);
                Assert.NotEmpty(key.KeyOffsets);
            }
        }
    }

    private static List<(string Id, byte[] Blob)> Transactions()
    {
        using JsonDocument doc = Corpus.Json("blocks", "transactions.json");

        return [.. doc.RootElement.EnumerateArray().Select(e =>
            (e.GetProperty("id").GetString()!, Convert.FromHexString(e.GetProperty("hex").GetString()!)))];
    }

    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
}
