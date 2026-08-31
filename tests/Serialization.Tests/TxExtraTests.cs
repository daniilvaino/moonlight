using System.Text.Json;
using Moonlight.Serialization;
using Moonlight.Tests;
using Xunit;

namespace Moonlight.Serialization.Tests;

public class TxExtraTests
{
    /// <summary>
    /// Every real transaction in the corpus carries its public key first. Without
    /// it a recipient cannot compute the shared secret, so this is the one field a
    /// wallet cannot do without.
    /// </summary>
    [Fact]
    public void FindsThePublicKeyOfEveryRealTransaction()
    {
        int withEncryptedPaymentId = 0;

        foreach ((byte[] blob, _) in Transactions())
        {
            Transaction tx = TxParser.Parse(blob);
            TxExtra extra = TxExtra.Parse(tx.Extra);

            Assert.NotNull(extra.PublicKey);
            Assert.Equal(32, extra.PublicKey!.Length);
            Assert.Equal(tx.Extra.AsSpan(1, 32).ToArray(), extra.PublicKey);

            if (extra.EncryptedPaymentId is not null)
            {
                Assert.Equal(8, extra.EncryptedPaymentId.Length);
                withEncryptedPaymentId++;
            }
        }

        Assert.Equal(2, withEncryptedPaymentId);
    }

    /// <summary>
    /// The coinbase carries a 17-byte nonce whose first byte is zero. That is a
    /// miner's arbitrary nonce, not a payment id — a payment id is 33 bytes. Reading
    /// it as one would attach a stranger's nonce to a wallet's own transfer.
    /// </summary>
    [Fact]
    public void AMinerNonceIsNotAPaymentId()
    {
        (byte[] blob, _) = Transactions().First(t => TxParser.Parse(t.Blob).IsCoinbase);
        TxExtra extra = TxExtra.Parse(TxParser.Parse(blob).Extra);

        Assert.NotNull(extra.PublicKey);
        Assert.Null(extra.PaymentId);
        Assert.Null(extra.EncryptedPaymentId);
    }

    [Fact]
    public void ReadsAPaymentIdAndAdditionalKeys()
    {
        byte[] key = [.. Enumerable.Repeat((byte)0x11, 32)];
        byte[] second = [.. Enumerable.Repeat((byte)0x22, 32)];
        byte[] paymentId = [.. Enumerable.Repeat((byte)0x33, 32)];

        byte[] extra =
        [
            0x01, .. key,
            0x04, 0x02, .. second, .. second,
            0x02, 33, 0x00, .. paymentId,
        ];

        TxExtra parsed = TxExtra.Parse(extra);

        Assert.Equal(key, parsed.PublicKey);
        Assert.Equal(2, parsed.AdditionalPublicKeys.Length);
        Assert.Equal(second, parsed.AdditionalPublicKeys[0]);
        Assert.Equal(paymentId, parsed.PaymentId);
    }

    /// <summary>
    /// Miners have put arbitrary bytes in this field for years. A tail we cannot
    /// read must not cost us the key that came before it — losing that key means
    /// silently missing an incoming payment.
    /// </summary>
    [Fact]
    public void KeepsWhatItReadBeforeGarbage()
    {
        byte[] key = [.. Enumerable.Repeat((byte)0x44, 32)];

        Assert.Equal(key, TxExtra.Parse([0x01, .. key, 0x7F, 0xDE, 0xAD]).PublicKey);
        Assert.Equal(key, TxExtra.Parse([0x01, .. key, 0x01, 0x02]).PublicKey);          // truncated second key
        Assert.Equal(key, TxExtra.Parse([0x01, .. key, 0x00, 0x00, 0x00]).PublicKey);    // padding
    }

    [Fact]
    public void AnEmptyFieldIsNotAnError()
    {
        TxExtra extra = TxExtra.Parse([]);

        Assert.Null(extra.PublicKey);
        Assert.Empty(extra.AdditionalPublicKeys);
    }

    private static List<(byte[] Blob, string Id)> Transactions()
    {
        using JsonDocument doc = Corpus.Json("blocks", "transactions.json");

        return [.. doc.RootElement.EnumerateArray().Select(e =>
            (Convert.FromHexString(e.GetProperty("hex").GetString()!), e.GetProperty("id").GetString()!))];
    }
}
