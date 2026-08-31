using System.Text.Json;
using Moonlight.Crypto;
using Moonlight.RingCT;
using Moonlight.Serialization;
using Moonlight.Tests;
using Xunit;

namespace Moonlight.RingCT.Tests;

/// <summary>
/// A real transaction, signed by monero, verified by us. Sign-then-verify proves
/// only that the code agrees with itself; a transcript that is wrong in the same
/// way twice passes that test and fails the network. This one cannot.
/// </summary>
public class ClsagVectorTests
{
    [Fact]
    public void VerifiesSignaturesMoneroProduced()
    {
        using JsonDocument tx = Corpus.Json("clsag", "clsag_tx.json");
        using JsonDocument rings = Corpus.Json("clsag", "ring_data.json");

        JsonElement root = tx.RootElement;
        Transaction parsed = TxParser.Parse(Convert.FromHexString(root.GetProperty("hex").GetString()!));
        JsonElement prunable = root.GetProperty("tx").GetProperty("rctsig_prunable");

        byte[] message = RingCtMessage.Compute(parsed, BulletproofPlusElements(prunable));

        JsonElement clsags = prunable.GetProperty("CLSAGs");
        JsonElement pseudoOuts = prunable.GetProperty("pseudoOuts");
        JsonElement inputs = root.GetProperty("tx").GetProperty("vin");

        Assert.Equal(2, clsags.GetArrayLength());

        for (int i = 0; i < clsags.GetArrayLength(); i++)
        {
            (Point[] ring, Point[] commitments) = Ring(rings.RootElement[i]);

            ClsagSignature signature = new(
                S: [.. clsags[i].GetProperty("s").EnumerateArray().Select(s => Scalar.FromCanonical(Hex(s)))],
                C1: Scalar.FromCanonical(Hex(clsags[i].GetProperty("c1"))),
                D: Point.FromBytes(Hex(clsags[i].GetProperty("D"))),
                I: Point.FromBytes(Hex(inputs[i].GetProperty("key").GetProperty("k_image"))));

            Assert.Equal(16, ring.Length);
            Assert.True(
                Clsag.Verify(message, ring, commitments, Point.FromBytes(Hex(pseudoOuts[i])), signature),
                $"input {i} failed to verify");
        }
    }

    [Fact]
    public void RejectsARealSignatureUnderATamperedMessage()
    {
        using JsonDocument tx = Corpus.Json("clsag", "clsag_tx.json");
        using JsonDocument rings = Corpus.Json("clsag", "ring_data.json");

        JsonElement root = tx.RootElement;
        JsonElement prunable = root.GetProperty("tx").GetProperty("rctsig_prunable");
        (Point[] ring, Point[] commitments) = Ring(rings.RootElement[0]);

        ClsagSignature signature = new(
            S: [.. prunable.GetProperty("CLSAGs")[0].GetProperty("s").EnumerateArray().Select(s => Scalar.FromCanonical(Hex(s)))],
            C1: Scalar.FromCanonical(Hex(prunable.GetProperty("CLSAGs")[0].GetProperty("c1"))),
            D: Point.FromBytes(Hex(prunable.GetProperty("CLSAGs")[0].GetProperty("D"))),
            I: Point.FromBytes(Hex(root.GetProperty("tx").GetProperty("vin")[0].GetProperty("key").GetProperty("k_image"))));

        Assert.False(Clsag.Verify(
            Keccak.Hash("not the message"u8),
            ring,
            commitments,
            Point.FromBytes(Hex(prunable.GetProperty("pseudoOuts")[0])),
            signature));
    }

    /// <summary>A, A1, B, r1, s1, d1, then every L and then every R.</summary>
    private static byte[][] BulletproofPlusElements(JsonElement prunable)
    {
        List<byte[]> elements = [];

        foreach (JsonElement proof in prunable.GetProperty("bpp").EnumerateArray())
        {
            foreach (string field in new[] { "A", "A1", "B", "r1", "s1", "d1" })
            {
                elements.Add(Hex(proof.GetProperty(field)));
            }

            foreach (JsonElement l in proof.GetProperty("L").EnumerateArray()) elements.Add(Hex(l));
            foreach (JsonElement r in proof.GetProperty("R").EnumerateArray()) elements.Add(Hex(r));
        }

        return [.. elements];
    }

    private static (Point[] Keys, Point[] Masks) Ring(JsonElement ring)
    {
        List<Point> keys = [];
        List<Point> masks = [];

        foreach (JsonElement member in ring.EnumerateArray())
        {
            keys.Add(Point.FromBytes(Hex(member.GetProperty("key"))));
            masks.Add(Point.FromBytes(Hex(member.GetProperty("mask"))));
        }

        return ([.. keys], [.. masks]);
    }

    private static byte[] Hex(JsonElement element) => Convert.FromHexString(element.GetString()!);
}
