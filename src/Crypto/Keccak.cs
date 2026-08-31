namespace Moonlight.Crypto;

/// <summary>
/// Keccak-256 as Monero uses it: the original submission's 0x01 padding, not
/// SHA-3's 0x06. The two differ in one byte and in nothing else, which is why
/// mixing them up produces a hash that is wrong everywhere and looks right.
/// </summary>
public static class Keccak
{
    public const int HashSize = 32;

    private const int Rate = 200 - (2 * HashSize);   // 136 bytes
    private const int Rounds = 24;

    private static ReadOnlySpan<byte> RhoOffsets =>
    [
         0,  1, 62, 28, 27,
        36, 44,  6, 55, 20,
         3, 10, 43, 25, 39,
        41, 45, 15, 21,  8,
        18,  2, 61, 56, 14,
    ];

    private static ReadOnlySpan<ulong> RoundConstants =>
    [
        0x0000000000000001, 0x0000000000008082, 0x800000000000808A, 0x8000000080008000,
        0x000000000000808B, 0x0000000080000001, 0x8000000080008081, 0x8000000000008009,
        0x000000000000008A, 0x0000000000000088, 0x0000000080008009, 0x000000008000000A,
        0x000000008000808B, 0x800000000000008B, 0x8000000000008089, 0x8000000000008003,
        0x8000000000008002, 0x8000000000000080, 0x000000000000800A, 0x800000008000000A,
        0x8000000080008081, 0x8000000000008080, 0x0000000080000001, 0x8000000080008008,
    ];

    public static byte[] Hash(ReadOnlySpan<byte> data)
    {
        byte[] result = new byte[HashSize];
        Hash(data, result);
        return result;
    }

    public static void Hash(ReadOnlySpan<byte> data, Span<byte> destination)
    {
        if (destination.Length < HashSize)
        {
            throw new ArgumentException($"destination must be at least {HashSize} bytes", nameof(destination));
        }

        Span<ulong> state = stackalloc ulong[25];
        state.Clear();

        while (data.Length >= Rate)
        {
            Absorb(state, data[..Rate]);
            Permute(state);
            data = data[Rate..];
        }

        Span<byte> block = stackalloc byte[Rate];
        block.Clear();
        data.CopyTo(block);
        block[data.Length] = 0x01;      // original Keccak padding
        block[Rate - 1] |= 0x80;

        Absorb(state, block);
        Permute(state);

        for (int i = 0; i < HashSize / 8; i++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(i * 8, 8), state[i]);
        }
    }

    private static void Absorb(Span<ulong> state, ReadOnlySpan<byte> block)
    {
        for (int i = 0; i < Rate / 8; i++)
        {
            state[i] ^= System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(i * 8, 8));
        }
    }

    private static void Permute(Span<ulong> a)
    {
        Span<ulong> c = stackalloc ulong[5];
        Span<ulong> b = stackalloc ulong[25];

        for (int round = 0; round < Rounds; round++)
        {
            // Theta
            for (int x = 0; x < 5; x++)
            {
                c[x] = a[x] ^ a[x + 5] ^ a[x + 10] ^ a[x + 15] ^ a[x + 20];
            }

            for (int x = 0; x < 5; x++)
            {
                ulong d = c[(x + 4) % 5] ^ ulong.RotateLeft(c[(x + 1) % 5], 1);
                for (int y = 0; y < 25; y += 5)
                {
                    a[y + x] ^= d;
                }
            }

            // Rho and pi
            for (int x = 0; x < 5; x++)
            {
                for (int y = 0; y < 5; y++)
                {
                    b[y + (5 * (((2 * x) + (3 * y)) % 5))] = ulong.RotateLeft(a[x + (5 * y)], RhoOffsets[x + (5 * y)]);
                }
            }

            // Chi
            for (int y = 0; y < 25; y += 5)
            {
                for (int x = 0; x < 5; x++)
                {
                    a[y + x] = b[y + x] ^ (~b[y + ((x + 1) % 5)] & b[y + ((x + 2) % 5)]);
                }
            }

            // Iota
            a[0] ^= RoundConstants[round];
        }
    }
}
