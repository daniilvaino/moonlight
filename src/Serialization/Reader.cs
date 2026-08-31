using Moonlight.Crypto;

namespace Moonlight.Serialization;

/// <summary>
/// Sequential reader over a consensus blob. Every method throws on a short read,
/// so a parser can be written as straight-line code: a truncated blob fails at the
/// point of truncation instead of producing a plausible half-parsed object.
/// </summary>
public ref struct Reader(ReadOnlySpan<byte> data)
{
    private readonly ReadOnlySpan<byte> data = data;

    public int Position { get; private set; }

    public readonly int Remaining => data.Length - Position;

    public readonly bool AtEnd => Remaining == 0;

    public byte ReadByte()
    {
        if (Remaining < 1) throw new FormatException($"truncated at {Position}: expected 1 byte");
        return data[Position++];
    }

    public ReadOnlySpan<byte> ReadBytes(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (Remaining < count) throw new FormatException($"truncated at {Position}: expected {count} bytes, {Remaining} left");

        ReadOnlySpan<byte> slice = data.Slice(Position, count);
        Position += count;
        return slice;
    }

    public byte[] ReadArray(int count) => ReadBytes(count).ToArray();

    /// <summary>A 32-byte key, hash, image or commitment.</summary>
    public byte[] ReadKey() => ReadArray(32);

    public ulong ReadVarInt()
    {
        if (!VarInt.TryRead(data[Position..], out ulong value, out int consumed))
        {
            throw new FormatException($"malformed varint at {Position}");
        }

        Position += consumed;
        return value;
    }

    /// <summary>A length-prefixed count, rejected if it cannot possibly fit in what is left.</summary>
    public int ReadCount(int minimumBytesEach)
    {
        ulong count = ReadVarInt();

        if (count > (ulong)(Remaining / Math.Max(minimumBytesEach, 1)))
        {
            throw new FormatException($"count {count} at {Position} exceeds the {Remaining} bytes remaining");
        }

        return (int)count;
    }
}
