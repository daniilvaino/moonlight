using Moonlight.Serialization;
using Moonlight.Tests;
using Xunit;

namespace Moonlight.Serialization.Tests;

/// <summary>
/// Monero's own tree-hash corpus, from <c>tests/hash/tests-tree.txt</c>: a root for
/// every count from one hash to sixteen.
///
/// A block of 514 transactions exercises one shape of the tree and hides the rest.
/// Monero's tree hash is not a plain binary fold — for a count that is not a power
/// of two it folds the leading remainder first and leaves a power of two behind — so
/// the counts that break an implementation are the small awkward ones, and this file
/// is nothing but those.
/// </summary>
public class MerkleVectorTests
{
    [Fact]
    public void EveryCountFromOneToSixteen()
    {
        int replayed = 0;

        foreach (string line in File.ReadLines(Corpus.File("hash", "tree.txt")))
        {
            string[] field = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (field.Length != 2) continue;

            byte[] leaves = Convert.FromHexString(field[1]);
            Assert.Equal(0, leaves.Length % 32);

            List<byte[]> hashes = [];
            for (int i = 0; i < leaves.Length; i += 32) hashes.Add(leaves[i..(i + 32)]);

            replayed++;
            Assert.Equal(replayed, hashes.Count);
            Assert.Equal(field[0], Convert.ToHexString(MerkleTree.Root(hashes)).ToLowerInvariant());
        }

        Assert.Equal(16, replayed);
    }
}
