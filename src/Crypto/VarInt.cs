namespace Moonlight.Crypto;

/// <summary>
/// Monero's variable-length integer: seven bits per byte, little-endian, high
/// bit set on every byte but the last.
/// </summary>
/// <remarks>
/// Lives here rather than in Serialization because view tags need it and the
/// layers only depend downward.
/// </remarks>
public static class VarInt
{
    /// <summary>Longest encoding of a 64-bit value: ceil(64 / 7).</summary>
    public const int MaxLength = 10;

    public static int Write(Span<byte> destination, ulong value)
    {
        int i = 0;

        while (value >= 0x80)
        {
            destination[i++] = (byte)(value | 0x80);
            value >>= 7;
        }

        destination[i++] = (byte)value;
        return i;
    }

    public static int Length(ulong value)
    {
        int n = 1;
        while (value >= 0x80)
        {
            value >>= 7;
            n++;
        }

        return n;
    }

    /// <summary>Reads one value, returning false on a truncated or over-long encoding.</summary>
    public static bool TryRead(ReadOnlySpan<byte> source, out ulong value, out int bytesRead)
    {
        value = 0;
        bytesRead = 0;

        for (int shift = 0; bytesRead < source.Length; shift += 7)
        {
            byte b = source[bytesRead++];

            // The 10th byte carries a single bit; anything more does not fit in 64.
            if (shift > 63 || (shift == 63 && b > 1))
            {
                return false;
            }

            value |= (ulong)(b & 0x7F) << shift;

            if ((b & 0x80) == 0)
            {
                return true;
            }
        }

        return false;
    }
}
