using Moonlight.Crypto;
using Moonlight.RingCT;
using Xunit;

namespace Moonlight.RingCT.Tests;

public class ClsagTests
{
    /// <summary>One input as a wallet would hold it, plus the ring it hides in.</summary>
    private sealed record Setup(
        Point[] Ring,
        Point[] Commitments,
        Scalar Secret,
        Scalar MaskDifference,
        Point Offset,
        int Index,
        byte[] Message);

    private static Setup Build(int ringSize, int index, ulong amount = 1_000_000_000_000)
    {
        Point[] ring = new Point[ringSize];
        Point[] commitments = new Point[ringSize];

        for (int i = 0; i < ringSize; i++)
        {
            ring[i] = Point.FromSecret(Scalar.Random());
            commitments[i] = Pedersen.Commit((ulong)Random.Shared.NextInt64(1, 1 << 30), Scalar.Random());
        }

        // The real output: a key we own and a commitment we know the mask of.
        Scalar secret = Scalar.Random();
        Scalar mask = Scalar.Random();
        ring[index] = Point.FromSecret(secret);
        commitments[index] = Pedersen.Commit(amount, mask);

        // The pseudo-output commits to the same amount under a different mask, so
        // the difference of the masks is what the signature proves knowledge of.
        Scalar pseudoMask = Scalar.Random();
        Point offset = Pedersen.Commit(amount, pseudoMask);

        return new Setup(ring, commitments, secret, mask - pseudoMask, offset, index, Keccak.Hash("message"u8));
    }

    private static ClsagSignature Sign(Setup s)
        => Clsag.Sign(s.Message, s.Ring, s.Commitments, s.Secret, s.MaskDifference, s.Offset, s.Index);

    private static bool Verify(Setup s, ClsagSignature sig)
        => Clsag.Verify(s.Message, s.Ring, s.Commitments, s.Offset, sig);

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    [InlineData(11, 0)]
    [InlineData(11, 5)]
    [InlineData(11, 10)]
    [InlineData(16, 9)]
    public void SignedRingsVerify(int ringSize, int index)
    {
        Setup s = Build(ringSize, index);

        Assert.True(Verify(s, Sign(s)));
    }

    [Fact]
    public void TheSignatureIsTheShapeItsRingImplies()
    {
        Setup s = Build(11, 4);
        ClsagSignature sig = Sign(s);

        Assert.Equal(11, sig.S.Length);
        Assert.False(sig.I.IsIdentity);
        Assert.All(sig.S, scalar => Assert.True(Scalar.TryFromCanonical(scalar.ToBytes(), out _)));
    }

    /// <summary>
    /// The key image depends only on the output being spent, never on the ring it
    /// was hidden in — otherwise re-spending with different decoys would go
    /// undetected, and the double-spend check would be worthless.
    /// </summary>
    [Fact]
    public void TheKeyImageDoesNotDependOnTheRing()
    {
        Setup first = Build(11, 3);
        Setup second = Build(11, 7) with
        {
            Secret = first.Secret,
            Ring = [.. Enumerable.Range(0, 11).Select(_ => Point.FromSecret(Scalar.Random()))],
        };
        second.Ring[7] = Point.FromSecret(first.Secret);

        Assert.Equal(Sign(first).I, Sign(second).I);
    }

    [Fact]
    public void ADifferentMessageDoesNotVerify()
    {
        Setup s = Build(11, 2);
        ClsagSignature sig = Sign(s);

        Assert.False(Clsag.Verify(Keccak.Hash("other"u8), s.Ring, s.Commitments, s.Offset, sig));
    }

    [Fact]
    public void ATamperedRingDoesNotVerify()
    {
        Setup s = Build(11, 2);
        ClsagSignature sig = Sign(s);

        Point[] swapped = [.. s.Ring];
        swapped[0] = Point.FromSecret(Scalar.Random());

        Assert.False(Clsag.Verify(s.Message, swapped, s.Commitments, s.Offset, sig));
    }

    [Fact]
    public void ATamperedScalarDoesNotVerify()
    {
        Setup s = Build(11, 2);
        ClsagSignature sig = Sign(s);

        Scalar[] scalars = [.. sig.S];
        scalars[6] = Scalar.Random();

        Assert.False(Verify(s, sig with { S = scalars }));
    }

    [Fact]
    public void AWrongCommitmentOffsetDoesNotVerify()
    {
        Setup s = Build(11, 2);
        ClsagSignature sig = Sign(s);

        // Same amount, different mask: the balance still works out, but this input
        // was not signed against this pseudo-output.
        Assert.False(Clsag.Verify(s.Message, s.Ring, s.Commitments, Pedersen.Commit(1_000_000_000_000, Scalar.Random()), sig));
    }

    /// <summary>
    /// The commitment half is not decorative: signing with a mask difference that
    /// does not match the offset must fail even though the key half is honest.
    /// </summary>
    [Fact]
    public void AWrongMaskDifferenceDoesNotVerify()
    {
        Setup s = Build(11, 2) with { MaskDifference = Scalar.Random() };

        Assert.False(Verify(s, Sign(s)));
    }

    [Fact]
    public void SignaturesAreNotDeterministic()
    {
        Setup s = Build(11, 2);

        // Different nonces each time; both must verify.
        ClsagSignature first = Sign(s);
        ClsagSignature second = Sign(s);

        Assert.NotEqual(first.C1, second.C1);
        Assert.True(Verify(s, first));
        Assert.True(Verify(s, second));
        Assert.Equal(first.I, second.I);
    }

    [Fact]
    public void MalformedInputIsRejectedRatherThanThrowing()
    {
        Setup s = Build(11, 2);
        ClsagSignature sig = Sign(s);

        Assert.False(Clsag.Verify(s.Message, s.Ring[..5], s.Commitments, s.Offset, sig));
        Assert.False(Clsag.Verify(s.Message, [], [], s.Offset, sig));
        Assert.False(Verify(s, sig with { I = Point.Identity }));
    }
}
