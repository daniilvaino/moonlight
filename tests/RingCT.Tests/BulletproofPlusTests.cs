using System.Text.Json;
using Moonlight.Crypto;
using Moonlight.RingCT.BulletproofPlus;
using Moonlight.Serialization;
using Moonlight.Tests;
using Xunit;

namespace Moonlight.RingCT.Tests;

/// <summary>
/// The range proof monero put in a real transaction, verified here. This is the
/// anchor the prover tests lean on: a self-made proof only proves the code agrees
/// with itself, so the verifier is pinned to a proof monero made first.
/// </summary>
public class BulletproofPlusTests
{
    [Fact]
    public void VerifiesARealProof() => Assert.True(Verifier.Verify(RealProof()));

    [Fact]
    public void GeneratorsAreDistinctAndOnTheCurve()
    {
        HashSet<string> seen = [];

        for (int i = 0; i < 8; i++)
        {
            Assert.True(seen.Add(Generators.Gi(i).ToString()));
            Assert.True(seen.Add(Generators.Hi(i).ToString()));
            Assert.False(Generators.Gi(i).IsIdentity);
        }

        // Deterministic: a second call gives the same generator, not a new one.
        Assert.Equal(Generators.Gi(3), Generators.Gi(3));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("A1")]
    [InlineData("B")]
    public void ATamperedPointFailsVerification(string field)
    {
        Proof proof = RealProof();
        Point other = Point.FromSecret(Scalar.Random());

        Proof tampered = field switch
        {
            "A" => proof with { A = other },
            "A1" => proof with { A1 = other },
            _ => proof with { B = other },
        };

        Assert.False(Verifier.Verify(tampered));
    }

    [Theory]
    [InlineData("r1")]
    [InlineData("s1")]
    [InlineData("d1")]
    public void ATamperedScalarFailsVerification(string field)
    {
        Proof proof = RealProof();
        Scalar other = Scalar.Random();

        Proof tampered = field switch
        {
            "r1" => proof with { R1 = other },
            "s1" => proof with { S1 = other },
            _ => proof with { D1 = other },
        };

        Assert.False(Verifier.Verify(tampered));
    }

    /// <summary>
    /// The commitments are what the range is proved about. Swapping one for another
    /// valid commitment must fail, or the proof would say nothing about the amounts
    /// actually in the transaction.
    /// </summary>
    [Fact]
    public void ADifferentCommitmentFailsVerification()
    {
        Proof proof = RealProof();
        Point[] v = [.. proof.V];
        v[0] = Pedersen.Commit(1, Scalar.Random());

        Assert.False(Verifier.Verify(proof with { V = v }));
    }

    [Fact]
    public void AReorderedInnerProductFailsVerification()
    {
        Proof proof = RealProof();

        Point[] l = [.. proof.L];
        (l[0], l[1]) = (l[1], l[0]);

        Assert.False(Verifier.Verify(proof with { L = l }));
    }

    [Fact]
    public void MalformedProofsAreRejectedRatherThanThrowing()
    {
        Proof proof = RealProof();

        Assert.False(Verifier.Verify(proof with { V = [] }));
        Assert.False(Verifier.Verify(proof with { L = [], R = [] }));
        Assert.False(Verifier.Verify(proof with { L = proof.L[..3] }));
    }

    /// <summary>
    /// V is not on the wire: it is rebuilt from the transaction's output
    /// commitments, each divided by the cofactor.
    /// </summary>
    private static Proof RealProof()
    {
        using JsonDocument document = Corpus.Json("clsag", "clsag_tx.json");
        JsonElement root = document.RootElement;

        Transaction tx = TxParser.Parse(Convert.FromHexString(root.GetProperty("hex").GetString()!));
        JsonElement bpp = root.GetProperty("tx").GetProperty("rctsig_prunable").GetProperty("bpp")[0];

        Scalar inverseEight = Scalar.Invert(Scalar.One + Scalar.One + Scalar.One + Scalar.One
                                          + Scalar.One + Scalar.One + Scalar.One + Scalar.One);

        Point[] v = [.. tx.Rct!.OutPk.Select(mask => inverseEight * Point.FromBytes(mask))];

        return new Proof(
            V: v,
            A: Point.FromBytes(Hex(bpp, "A")),
            A1: Point.FromBytes(Hex(bpp, "A1")),
            B: Point.FromBytes(Hex(bpp, "B")),
            R1: Scalar.FromCanonical(Hex(bpp, "r1")),
            S1: Scalar.FromCanonical(Hex(bpp, "s1")),
            D1: Scalar.FromCanonical(Hex(bpp, "d1")),
            L: [.. bpp.GetProperty("L").EnumerateArray().Select(x => Point.FromBytes(Convert.FromHexString(x.GetString()!)))],
            R: [.. bpp.GetProperty("R").EnumerateArray().Select(x => Point.FromBytes(Convert.FromHexString(x.GetString()!)))]);
    }

    private static byte[] Hex(JsonElement element, string property)
        => Convert.FromHexString(element.GetProperty(property).GetString()!);
}
