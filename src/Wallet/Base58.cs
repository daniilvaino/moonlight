namespace Moonlight.Wallet;

/// <summary>
/// Monero's Base58, which is not Bitcoin's. Data is encoded in eight-byte blocks,
/// each becoming exactly eleven characters, so the output length is fixed by the
/// input length — no leading-zero convention, no variable expansion.
/// </summary>
public static class Base58
{
    private const string Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    private const int FullBlock = 8;
    private const int FullEncodedBlock = 11;

    /// <summary>How many characters a block of 1..8 bytes turns into.</summary>
    private static ReadOnlySpan<int> EncodedBlockSizes => [0, 2, 3, 5, 6, 7, 9, 10, 11];

    public static string Encode(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty) return string.Empty;

        int fullBlocks = data.Length / FullBlock;
        int remainder = data.Length % FullBlock;

        char[] result = new char[(fullBlocks * FullEncodedBlock) + EncodedBlockSizes[remainder]];
        result.AsSpan().Fill(Alphabet[0]);

        for (int i = 0; i < fullBlocks; i++)
        {
            EncodeBlock(data.Slice(i * FullBlock, FullBlock), result.AsSpan(i * FullEncodedBlock, FullEncodedBlock));
        }

        if (remainder > 0)
        {
            EncodeBlock(
                data.Slice(fullBlocks * FullBlock, remainder),
                result.AsSpan(fullBlocks * FullEncodedBlock, EncodedBlockSizes[remainder]));
        }

        return new string(result);
    }

    public static bool TryDecode(string text, out byte[] data)
    {
        data = [];
        if (string.IsNullOrEmpty(text)) return false;

        int fullBlocks = text.Length / FullEncodedBlock;
        int remainder = text.Length % FullEncodedBlock;

        int remainderBytes = EncodedBlockSizes.IndexOf(remainder);
        if (remainderBytes < 0) return false;

        byte[] result = new byte[(fullBlocks * FullBlock) + remainderBytes];

        for (int i = 0; i < fullBlocks; i++)
        {
            if (!TryDecodeBlock(text.AsSpan(i * FullEncodedBlock, FullEncodedBlock), result.AsSpan(i * FullBlock, FullBlock)))
            {
                return false;
            }
        }

        if (remainder > 0 && !TryDecodeBlock(
                text.AsSpan(fullBlocks * FullEncodedBlock, remainder),
                result.AsSpan(fullBlocks * FullBlock, remainderBytes)))
        {
            return false;
        }

        data = result;
        return true;
    }

    public static byte[] Decode(string text)
        => TryDecode(text, out byte[] data) ? data : throw new FormatException("not valid Monero Base58");

    /// <summary>A block is one big-endian integer, written in base 58 right to left.</summary>
    private static void EncodeBlock(ReadOnlySpan<byte> block, Span<char> destination)
    {
        ulong value = 0;
        foreach (byte b in block) value = (value << 8) | b;

        for (int i = destination.Length - 1; i >= 0 && value > 0; i--)
        {
            destination[i] = Alphabet[(int)(value % 58)];
            value /= 58;
        }
    }

    private static bool TryDecodeBlock(ReadOnlySpan<char> block, Span<byte> destination)
    {
        ulong value = 0;

        foreach (char c in block)
        {
            int digit = Alphabet.IndexOf(c);
            if (digit < 0) return false;

            // Overflow means the block does not encode a number this small; that is
            // a malformed block, not a large one.
            ulong next = (value * 58) + (ulong)digit;
            if (next < value) return false;

            value = next;
        }

        // The value must fit the byte width this block claims.
        if (destination.Length < FullBlock && value > (1UL << (8 * destination.Length)) - 1) return false;

        for (int i = destination.Length - 1; i >= 0; i--)
        {
            destination[i] = (byte)(value & 0xFF);
            value >>= 8;
        }

        return true;
    }
}
