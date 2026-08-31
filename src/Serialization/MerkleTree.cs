using Moonlight.Crypto;

namespace Moonlight.Serialization;

/// <summary>
/// Monero's tree hash. Not a textbook Merkle tree: the first level folds only
/// enough leaves to reach a power of two, so the tree is complete from then on.
/// </summary>
/// <remarks>
/// Ported from <c>src/crypto/tree-hash.c</c>. The odd shape is deliberate — the
/// original CryptoNote code miscomputed the fold width, which is what let block
/// 202612 be built with duplicate transactions and hash two different ways.
/// </remarks>
public static class MerkleTree
{
    public static byte[] Root(IReadOnlyList<byte[]> hashes)
    {
        ArgumentNullException.ThrowIfNull(hashes);

        if (hashes.Count == 0)
        {
            throw new ArgumentException("a block has at least the miner transaction", nameof(hashes));
        }

        if (hashes.Count == 1)
        {
            return hashes[0].AsSpan().ToArray();
        }

        if (hashes.Count == 2)
        {
            return HashPair(hashes[0], hashes[1]);
        }

        // 1 << floor(log2(count)): the width of the first complete level.
        int width = 1;
        while (width < hashes.Count) width <<= 1;
        width >>= 1;

        byte[][] level = new byte[width][];

        // The leaves that pass through untouched, then the pairs that fold.
        int carried = (2 * width) - hashes.Count;
        for (int i = 0; i < carried; i++) level[i] = hashes[i].AsSpan().ToArray();
        for (int i = carried, j = carried; j < width; i += 2, j++) level[j] = HashPair(hashes[i], hashes[i + 1]);

        while (width > 2)
        {
            width >>= 1;
            for (int i = 0, j = 0; j < width; i += 2, j++) level[j] = HashPair(level[i], level[i + 1]);
        }

        return HashPair(level[0], level[1]);
    }

    private static byte[] HashPair(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        Span<byte> pair = stackalloc byte[64];
        left.CopyTo(pair);
        right.CopyTo(pair[32..]);

        return Keccak.Hash(pair);
    }
}
