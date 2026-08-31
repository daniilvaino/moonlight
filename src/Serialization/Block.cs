using System.Buffers.Binary;
using Moonlight.Crypto;

namespace Moonlight.Serialization;

public sealed record BlockHeader(
    ulong MajorVersion,
    ulong MinorVersion,
    ulong Timestamp,
    byte[] PreviousId,
    uint Nonce)
{
    // A record's generated equality compares byte[] by reference, so two headers
    // read from identical bytes would come out unequal. Headers get compared —
    // that is how a chain is followed — so equality is spelled out here.
    public bool Equals(BlockHeader? other)
        => other is not null
        && MajorVersion == other.MajorVersion
        && MinorVersion == other.MinorVersion
        && Timestamp == other.Timestamp
        && Nonce == other.Nonce
        && PreviousId.AsSpan().SequenceEqual(other.PreviousId);

    public override int GetHashCode()
        => HashCode.Combine(MajorVersion, MinorVersion, Timestamp, Nonce,
            PreviousId.Length >= 4 ? BitConverter.ToInt32(PreviousId, 0) : 0);

    /// <summary>Nonce is a fixed 32-bit little-endian field, not a varint — the miner rewrites it in place.</summary>
    public byte[] Serialize()
    {
        Span<byte> varints = stackalloc byte[3 * VarInt.MaxLength];
        int n = VarInt.Write(varints, MajorVersion);
        n += VarInt.Write(varints[n..], MinorVersion);
        n += VarInt.Write(varints[n..], Timestamp);

        byte[] blob = new byte[n + 32 + 4];
        varints[..n].CopyTo(blob);
        PreviousId.CopyTo(blob.AsSpan(n));
        BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(n + 32), Nonce);

        return blob;
    }
}

public sealed record Block(BlockHeader Header, Transaction MinerTransaction, byte[][] TransactionIds)
{
    /// <summary>The miner transaction leads the tree; the ids follow in order.</summary>
    public byte[][] AllTransactionIds() => [TxHash.Compute(MinerTransaction), .. TransactionIds];
}
