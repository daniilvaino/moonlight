using Moonlight.Crypto;
using Moonlight.RingCT;
using MoneroRing.Crypto;
using MoneroSharp.NaCl.Internal.Ed25519Ref10;
using Xunit;

namespace Moonlight.RingCT.Tests;

public class PedersenTests
{
    /// <summary>
    /// The compressed basepoint G. Monero documents H as toPoint(cn_fast_hash(G)),
    /// so the constant can be derived rather than trusted — and deriving it exercises
    /// hash_to_ec against a value the whole protocol depends on.
    /// </summary>
    private static readonly byte[] G =
        Convert.FromHexString("5866666666666666666666666666666666666666666666666666666666666666");

    /// <summary>
    /// H = 8·to_point(keccak(G)), and to_point here means "read these bytes as a
    /// compressed point" — ge_frombytes_vartime, not the ge_fromfe map that
    /// hash_to_ec uses. The two differ, and only one reproduces the constant; the
    /// comment in rctTypes.h says "toPoint" without saying which.
    /// </summary>
    [Fact]
    public void HIsWhatItsDerivationSays()
    {
        byte[] hash = Keccak.Hash(G);

        Assert.Equal(0, RingSig.ge_frombytes_vartime(out GroupElementP3 p3, hash));
        GroupOperations.ge_p3_to_p2(out GroupElementP2 p2, ref p3);
        RingSig.ge_mul8(out GroupElementP1P1 doubled, ref p2);
        GroupOperations.ge_p1p1_to_p3(out GroupElementP3 h, ref doubled);

        byte[] derived = new byte[32];
        GroupOperations.ge_p3_tobytes(derived, 0, ref h);

        Assert.Equal(
            Convert.ToHexString(Pedersen.H).ToLowerInvariant(),
            Convert.ToHexString(derived).ToLowerInvariant());
    }

    [Fact]
    public void HDecompresses() => Assert.True(Point.TryFromBytes(Pedersen.H, out _));

    /// <summary>
    /// The property the whole scheme rests on: commitments add. Inputs and outputs
    /// are checked by adding commitments, never by comparing amounts.
    /// </summary>
    [Fact]
    public void CommitmentsAreHomomorphic()
    {
        for (int i = 0; i < 16; i++)
        {
            ulong a = (ulong)Random.Shared.NextInt64(0, int.MaxValue);
            ulong b = (ulong)Random.Shared.NextInt64(0, int.MaxValue);

            Scalar x = Scalar.Random();
            Scalar y = Scalar.Random();

            Assert.Equal(Pedersen.Commit(a + b, x + y), Pedersen.Commit(a, x) + Pedersen.Commit(b, y));
        }
    }

    [Fact]
    public void CommitmentHidesAndBinds()
    {
        Scalar mask = Scalar.Random();

        // Same amount, different mask: different commitment (hiding).
        Assert.NotEqual(Pedersen.Commit(42, mask), Pedersen.Commit(42, Scalar.Random()));

        // Same mask, different amount: different commitment (binding).
        Assert.NotEqual(Pedersen.Commit(42, mask), Pedersen.Commit(43, mask));

        // Deterministic.
        Assert.Equal(Pedersen.Commit(42, mask), Pedersen.Commit(42, mask));
    }

    [Fact]
    public void PlainCommitmentIsAmountTimesH()
    {
        // A zero mask leaves amount·H, which is what a coinbase output commits to.
        Assert.Equal(Pedersen.Commit(7, Scalar.Zero), Pedersen.CommitPlain(7));
        Assert.Equal(Point.FromBytes(Pedersen.H), Pedersen.CommitPlain(1));
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(1UL)]
    [InlineData(ulong.MaxValue)]
    public void HandlesTheEdgeAmounts(ulong amount)
        => Assert.Equal(32, Pedersen.Commit(amount, Scalar.Random()).ToBytes().Length);
}
