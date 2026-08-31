using MoneroRing.Crypto;
using Xunit;

namespace Moonlight.Crypto.Tests;

/// <summary>
/// The generating half of tests.txt, replayed. These vectors record the bytes
/// monero's reference drew from a deterministic generator, so replaying them means
/// substituting that generator and producing the same bytes — not merely producing
/// something that verifies.
/// </summary>
/// <remarks>
/// The generator is shared across the whole file and advances with every draw, so
/// the four generating operations have to be replayed together, in file order.
/// Skipping one, or drawing a different number of bytes than the reference did,
/// desynchronises everything after it — which makes this a test of the draw
/// pattern as much as of the arithmetic.
/// </remarks>
public class GeneratorVectorTests
{
    [Fact]
    public void ReplaysEveryGeneratedVector()
    {
        MoneroTestRandom random = new();
        RingSig.RandomSource = random.Fill;

        try
        {
            int scalars = 0, keys = 0, signatures = 0, rings = 0;

            foreach (Vector v in TestVectors.All())
            {
                switch (v.Op)
                {
                    case "random_scalar":
                    {
                        byte[] actual = new byte[32];
                        RingSig.random_scalar(actual);

                        Assert.Equal(v[0], Hex(actual));
                        scalars++;
                        break;
                    }

                    case "generate_keys":
                    {
                        byte[] pub = new byte[32];
                        byte[] sec = new byte[32];
                        RingSig.generate_keys(pub, sec);

                        Assert.Equal(v[0], Hex(pub));
                        Assert.Equal(v[1], Hex(sec));
                        keys++;
                        break;
                    }

                    case "generate_signature":
                    {
                        byte[] signature = RingSig.generate_signature(v.Bytes(0), v.Bytes(1), v.Bytes(2));

                        Assert.Equal(v[3], Hex(signature));
                        signatures++;
                        break;
                    }

                    case "generate_ring_signature":
                    {
                        int count = v.Index(2);
                        byte[][] pubs = new byte[count][];
                        for (int i = 0; i < count; i++) pubs[i] = v.Bytes(3 + i);

                        byte[] signature = RingSig.generate_ring_signature(
                            v.Bytes(0), v.Bytes(1), pubs, count, v.Bytes(3 + count), v.Index(4 + count));

                        Assert.Equal(v[5 + count], Hex(signature));
                        rings++;
                        break;
                    }
                }
            }

            Assert.Equal(245, scalars);
            Assert.Equal(256, keys);
            Assert.Equal(256, signatures);
            Assert.Equal(256, rings);
        }
        finally
        {
            RingSig.RandomSource = null!;
        }
    }

    /// <summary>
    /// The generator itself, before anything uses it: the same state produces the
    /// same stream, and a fresh one starts over.
    /// </summary>
    [Fact]
    public void TheGeneratorIsDeterministic()
    {
        byte[] first = new byte[64];
        byte[] second = new byte[64];

        new MoneroTestRandom().Fill(first);
        new MoneroTestRandom().Fill(second);

        Assert.Equal(first, second);
        Assert.Contains(first, b => b != 0);
    }

    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
}
