using Moonlight.Crypto;

namespace Moonlight.Serialization;

/// <summary>
/// Consensus block blobs and block ids. Written against monero's
/// <c>cryptonote_basic.h</c> and <c>cryptonote_format_utils.cpp</c>.
/// </summary>
public static class BlockParser
{
    /// <summary>The one block whose stored hash is not the hash of its contents.</summary>
    private static ReadOnlySpan<byte> CorrectHash202612 =>
        [0x42, 0x6d, 0x16, 0xcf, 0xf0, 0x4c, 0x71, 0xf8, 0xb1, 0x63, 0x40, 0xb7, 0x22, 0xdc, 0x40, 0x10,
         0xa2, 0xdd, 0x38, 0x31, 0xc2, 0x20, 0x41, 0x43, 0x1f, 0x77, 0x25, 0x47, 0xba, 0x6e, 0x33, 0x1a];

    private static ReadOnlySpan<byte> ExistingHash202612 =>
        [0xbb, 0xd6, 0x04, 0xd2, 0xba, 0x11, 0xba, 0x27, 0x93, 0x5e, 0x00, 0x6e, 0xd3, 0x9c, 0x9b, 0xfd,
         0xd9, 0x9b, 0x76, 0xbf, 0x4a, 0x50, 0x65, 0x4b, 0xc1, 0xe1, 0xe6, 0x12, 0x17, 0x96, 0x26, 0x98];

    public static Block Parse(ReadOnlySpan<byte> blob)
    {
        Reader reader = new(blob);

        BlockHeader header = ReadHeader(ref reader);

        // The miner transaction is embedded whole; parsing it is what tells us
        // where it ends.
        int start = reader.Position;
        Transaction miner = ParseMinerTransaction(blob[start..], out int consumed);
        reader.ReadBytes(consumed);

        int count = reader.ReadCount(32);
        byte[][] ids = new byte[count][];
        for (int i = 0; i < count; i++) ids[i] = reader.ReadKey();

        if (!reader.AtEnd)
        {
            throw new FormatException($"{reader.Remaining} trailing bytes after the block");
        }

        return new Block(header, miner, ids);
    }

    public static BlockHeader ReadHeader(ref Reader reader)
    {
        ulong major = reader.ReadVarInt();
        ulong minor = reader.ReadVarInt();
        ulong timestamp = reader.ReadVarInt();
        byte[] previous = reader.ReadKey();

        ReadOnlySpan<byte> nonce = reader.ReadBytes(4);
        return new BlockHeader(major, minor, timestamp, previous,
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(nonce));
    }

    /// <summary>
    /// Header, Merkle root, and the transaction count including the miner's. This
    /// is what proof of work runs over.
    /// </summary>
    public static byte[] HashingBlob(BlockHeader header, IReadOnlyList<byte[]> allTransactionIds)
    {
        byte[] headerBlob = header.Serialize();
        byte[] root = MerkleTree.Root(allTransactionIds);

        Span<byte> count = stackalloc byte[VarInt.MaxLength];
        int countLength = VarInt.Write(count, (ulong)allTransactionIds.Count);

        byte[] blob = new byte[headerBlob.Length + 32 + countLength];
        headerBlob.CopyTo(blob, 0);
        root.CopyTo(blob, headerBlob.Length);
        count[..countLength].CopyTo(blob.AsSpan(headerBlob.Length + 32));

        return blob;
    }

    /// <summary>
    /// The block id: Keccak over the hashing blob, prefixed with its own length.
    /// The length prefix is only there for the id, not for proof of work.
    /// </summary>
    public static byte[] ComputeId(BlockHeader header, IReadOnlyList<byte[]> allTransactionIds)
    {
        byte[] blob = HashingBlob(header, allTransactionIds);

        byte[] buffer = new byte[VarInt.MaxLength + blob.Length];
        int n = VarInt.Write(buffer, (ulong)blob.Length);
        blob.CopyTo(buffer, n);

        byte[] hash = Keccak.Hash(buffer.AsSpan(0, n + blob.Length));

        // Block 202612 was built with duplicate transactions, which the tree hash
        // of the day accepted. The chain stores the hash it got then, so we hand
        // back that one — otherwise every consumer disagrees with the network
        // about one block from 2014.
        return hash.AsSpan().SequenceEqual(CorrectHash202612) ? ExistingHash202612.ToArray() : hash;
    }

    public static byte[] ComputeId(Block block) => ComputeId(block.Header, block.AllTransactionIds());

    private static Transaction ParseMinerTransaction(ReadOnlySpan<byte> blob, out int consumed)
    {
        // A miner transaction has no proofs to swallow, so the parser stops exactly
        // where it ends and the block's own fields resume.
        Transaction tx = TxParser.ParseEmbedded(blob, out consumed);

        if (!tx.IsCoinbase)
        {
            throw new FormatException("the first transaction in a block must be the miner transaction");
        }

        return tx;
    }
}
