using Moonlight.Crypto;
using Moonlight.RingCT;
using Xunit;

namespace Moonlight.RingCT.Tests;

public class EcdhTests
{
    [Fact]
    public void AmountRoundTrips()
    {
        byte[] shared = Scalar.Random().ToBytes();

        foreach (ulong amount in new ulong[] { 0, 1, 12345, 1_000_000_000_000, ulong.MaxValue })
        {
            byte[] encoded = Ecdh.EncodeAmount(amount, shared);

            Assert.Equal(8, encoded.Length);
            Assert.Equal(amount, Ecdh.DecodeAmount(encoded, shared));
        }
    }

    [Fact]
    public void TheWrongSecretGivesTheWrongAmount()
    {
        byte[] shared = Scalar.Random().ToBytes();
        byte[] encoded = Ecdh.EncodeAmount(1_000_000_000_000, shared);

        Assert.NotEqual(1_000_000_000_000UL, Ecdh.DecodeAmount(encoded, Scalar.Random().ToBytes()));
    }

    /// <summary>
    /// Decoding never fails — every eight bytes are a number. A wallet learns the
    /// output was not its own by rebuilding the commitment and finding it does not
    /// match, not by an error here.
    /// </summary>
    [Fact]
    public void TheCommitmentIsWhatDetectsAWrongSecret()
    {
        byte[] shared = Scalar.Random().ToBytes();
        const ulong amount = 3_000_000_000;

        Scalar mask = Ecdh.CommitmentMask(shared);
        Point commitment = Pedersen.Commit(amount, mask);
        byte[] encoded = Ecdh.EncodeAmount(amount, shared);

        // The recipient's side: derive the mask, decode the amount, rebuild.
        Assert.Equal(commitment, Pedersen.Commit(Ecdh.DecodeAmount(encoded, shared), Ecdh.CommitmentMask(shared)));

        byte[] other = Scalar.Random().ToBytes();
        Assert.NotEqual(commitment, Pedersen.Commit(Ecdh.DecodeAmount(encoded, other), Ecdh.CommitmentMask(other)));
    }

    [Fact]
    public void MaskIsDeterministicAndSecretDependent()
    {
        byte[] shared = Scalar.Random().ToBytes();

        Assert.Equal(Ecdh.CommitmentMask(shared), Ecdh.CommitmentMask(shared));
        Assert.NotEqual(Ecdh.CommitmentMask(shared), Ecdh.CommitmentMask(Scalar.Random().ToBytes()));
    }

    [Fact]
    public void LegacyFormRoundTrips()
    {
        byte[] shared = Scalar.Random().ToBytes();
        Scalar mask = Scalar.Random();
        Scalar amount = Scalar.Random();

        (Scalar encodedMask, Scalar encodedAmount) = Ecdh.EncodeLegacy(mask, amount, shared);
        (Scalar decodedMask, Scalar decodedAmount) = Ecdh.DecodeLegacy(encodedMask, encodedAmount, shared);

        Assert.Equal(mask, decodedMask);
        Assert.Equal(amount, decodedAmount);
        Assert.NotEqual(mask, encodedMask);
    }

    /// <summary>The two salts must not collide: same secret, different derivations.</summary>
    [Fact]
    public void TheTwoDerivationsAreSeparated()
    {
        byte[] shared = Scalar.Random().ToBytes();

        Assert.NotEqual(
            Convert.ToHexString(Ecdh.CommitmentMask(shared).ToBytes()),
            Convert.ToHexString(Ecdh.AmountEncodingFactor(shared)));
    }
}
