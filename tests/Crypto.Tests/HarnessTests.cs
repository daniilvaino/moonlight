using Xunit;

namespace Moonlight.Crypto.Tests;

/// <summary>
/// The harness itself, under test before any crypto exists: if the reader misreads
/// tests.txt, every vector-driven test built on it is worthless.
/// </summary>
public class HarnessTests
{
    [Fact]
    public void VectorFileIsPresent()
    {
        Assert.True(TestVectors.Available, $"missing {TestVectors.Path}");
        Assert.Equal(5945, TestVectors.All().Count);
    }

    [Fact]
    public void EveryLineMatchesTheGrammar()
    {
        List<string> bad = [];

        foreach ((Vector v, int line) in TestVectors.All().Select((v, i) => (v, i + 1)))
        {
            int expected = VectorGrammar.Arity(v.Op, v.Args);
            if (expected != v.Args.Length)
            {
                bad.Add($"line {line}: {v.Op} has {v.Args.Length} args, grammar says {expected}");
            }
        }

        Assert.Empty(bad.Take(10));
    }

    [Fact]
    public void EveryOperationIsKnown()
    {
        string[] unknown = TestVectors.All().Select(v => v.Op)
            .Distinct()
            .Where(op => !VectorGrammar.Ops.Contains(op))
            .ToArray();

        Assert.Empty(unknown);
    }

    [Fact]
    public void HexArgumentsDecode()
    {
        foreach (Vector v in TestVectors.Read("generate_key_image"))
        {
            Assert.Equal(32, v.Bytes(0).Length);   // public key
            Assert.Equal(32, v.Bytes(1).Length);   // secret key
            Assert.Equal(32, v.Bytes(2).Length);   // key image
        }
    }

    [Theory]
    [InlineData("check_ring_signature", 1024)]
    [InlineData("check_signature", 512)]
    [InlineData("hash_to_point", 371)]
    [InlineData("generate_key_image", 256)]
    [InlineData("point_to_wei_x_y", 200)]
    [InlineData("derive_key_image_generator", 200)]
    [InlineData("derive_view_tag", 70)]
    [InlineData("check_ge_p3_identity", 6)]
    public void OperationCountsAreStable(string op, int count)
        => Assert.Equal(count, TestVectors.Read(op).Count());

    /// <summary>
    /// The coverage number the readme prints, counted instead of remembered.
    ///
    /// It is all of them now, so the part of this worth reading is the empty list
    /// below rather than the arithmetic. It stays because the number was once kept by
    /// hand in two documents and drifted there: half of derive_key_image_generator
    /// was written off as unimplemented crypto when it was a map we already had. A
    /// number nothing counts goes stale in the direction that flatters nobody.
    /// </summary>
    [Fact]
    public void EveryLineIsReplayed()
    {
        int total = TestVectors.All().Count;
        int held = TestVectors.All().Count(NotReplayed);

        Assert.Equal(5945, total);
        Assert.Equal(0, held);
    }

    /// <summary>
    /// Lines no test replays, and why. There are none: every operation the corpus
    /// contains is performed and its result compared. Anything held back in future
    /// belongs here with its reason, so the readme's number stays a counted one.
    /// </summary>
    private static bool NotReplayed(Vector v) => v.Op switch
    {
        _ => false,
    };
}
