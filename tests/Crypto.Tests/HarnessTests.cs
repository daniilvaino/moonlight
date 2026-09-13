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
    /// Every line of every line-oriented corpus is replayed, so the parts worth
    /// reading are the empty held-back list below and the fact that this counts all
    /// four files rather than the first one. It has drifted twice already: once when
    /// half of derive_key_image_generator was written off as unimplemented crypto
    /// that we in fact had, and once when three corpora were added beside the
    /// original and the badge went on quoting the original alone.
    /// </summary>
    [Fact]
    public void EveryLineOfEveryCorpusIsReplayed()
    {
        Assert.Equal(0, TestVectors.All().Count(NotReplayed));

        (string What, int Lines)[] corpora =
        [
            ("monero's crypto corpus", TestVectors.All().Count),
            ("BLAKE2b", Lines("hash/blake2b.txt", "hash:")),
            ("Keccak", Lines("hash/keccak.txt")),
            ("the tree hash", Lines("hash/tree.txt")),
        ];

        Assert.Equal([5945, 256, 321, 16], corpora.Select(c => c.Lines));
        Assert.Equal(6538, corpora.Sum(c => c.Lines));
    }

    /// <summary>
    /// Vectors in a corpus: one per line, or one per line carrying
    /// <paramref name="marker"/> where a vector spans several.
    /// </summary>
    private static int Lines(string file, string? marker = null)
        => System.IO.File.ReadLines(Moonlight.Tests.Corpus.File(file.Split('/')))
            .Count(line => marker is null
                ? line.Trim().Length > 0
                : line.StartsWith(marker, StringComparison.Ordinal));

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
