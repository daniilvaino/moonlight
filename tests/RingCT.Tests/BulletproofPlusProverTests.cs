using Moonlight.Crypto;
using Moonlight.RingCT.BulletproofPlus;
using Xunit;

namespace Moonlight.RingCT.Tests;

/// <summary>
/// The prover, checked against the verifier — which is itself checked against a
/// range proof monero made. That chain is the strongest claim available without
/// building monerod: our proofs satisfy the same equation monero's do.
/// </summary>
public class BulletproofPlusProverTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void ProofsVerify(int outputs)
    {
        ulong[] amounts = [.. Enumerable.Range(1, outputs).Select(i => (ulong)i * 1_000_000_000)];
        Scalar[] masks = [.. amounts.Select(_ => Scalar.Random())];

        Assert.True(Verifier.Verify(Prover.Prove(amounts, masks)));
    }

    /// <summary>
    /// The extremes of the range being proved. Zero and 2^64-1 are exactly the
    /// values a wrong bit decomposition gets wrong.
    /// </summary>
    [Theory]
    [InlineData(0UL)]
    [InlineData(1UL)]
    [InlineData(ulong.MaxValue)]
    [InlineData(1UL << 63)]
    public void TheEdgesOfTheRangeVerify(ulong amount)
        => Assert.True(Verifier.Verify(Prover.Prove([amount], [Scalar.Random()])));

    /// <summary>
    /// The proof commits to the amounts. V is not on the wire in a transaction, so
    /// the wallet rebuilds it from outPk — and a proof whose commitments were
    /// replaced must fail, or it would say nothing about the money.
    /// </summary>
    [Fact]
    public void TheProofIsBoundToItsCommitments()
    {
        Proof proof = Prover.Prove([5_000_000_000_000], [Scalar.Random()]);

        Point[] other = [.. proof.V];
        other[0] = Pedersen.Commit(1, Scalar.Random());

        Assert.False(Verifier.Verify(proof with { V = other }));
    }

    /// <summary>The proof is log-sized: six rounds for one output, seven for two.</summary>
    [Theory]
    [InlineData(1, 6)]
    [InlineData(2, 7)]
    [InlineData(3, 8)]
    [InlineData(4, 8)]
    public void TheProofIsTheSizeItsAggregationImplies(int outputs, int expectedRounds)
    {
        ulong[] amounts = [.. Enumerable.Repeat(1_000UL, outputs)];
        Proof proof = Prover.Prove(amounts, [.. amounts.Select(_ => Scalar.Random())]);

        Assert.Equal(expectedRounds, proof.L.Length);
        Assert.Equal(expectedRounds, proof.R.Length);
        Assert.Equal(outputs, proof.V.Length);
    }

    [Fact]
    public void ProofsAreNotDeterministic()
    {
        ulong[] amounts = [42_000_000];
        Scalar[] masks = [Scalar.Random()];

        Proof first = Prover.Prove(amounts, masks);
        Proof second = Prover.Prove(amounts, masks);

        // Same commitments, different nonces, both valid.
        Assert.Equal(first.V[0], second.V[0]);
        Assert.NotEqual(first.A, second.A);
        Assert.True(Verifier.Verify(first));
        Assert.True(Verifier.Verify(second));
    }

    /// <summary>
    /// The commitments a proof carries are divided by the cofactor, matching what a
    /// transaction stores: outPk multiplied by 1/8.
    /// </summary>
    [Fact]
    public void CommitmentsTravelDividedByEight()
    {
        Scalar mask = Scalar.Random();
        Proof proof = Prover.Prove([7_000_000], [mask]);

        Scalar eight = Scalar.FromCanonical([8, .. new byte[31]]);

        Assert.Equal(Pedersen.Commit(7_000_000, mask), eight * proof.V[0]);
    }

    [Fact]
    public void RefusesMismatchedInput()
    {
        Assert.Throws<ArgumentException>(() => Prover.Prove([1, 2], [Scalar.Random()]));
        Assert.Throws<ArgumentException>(() => Prover.Prove([], []));
    }
}
