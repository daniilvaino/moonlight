using System.Buffers.Binary;
using System.Numerics;

namespace Moonlight.Crypto;

/// <summary>
/// BLAKE2b, RFC 7693. Monero reaches for it where Keccak will not do: the unbiased
/// map onto the curve needs 64 bytes of digest from a 32-byte input, and Keccak-256
/// gives 32.
///
/// Monero does not call it unpersonalised. Its <c>blake2b_monero</c> sets the
/// personalisation field of the parameter block to the six bytes of "Monero", which
/// changes the initial state and so every digest that follows — a hash produced
/// without it is a valid BLAKE2b of the same input and useless for comparing with
/// monero. <see cref="Monero"/> is that call; <see cref="Hash"/> is the plain
/// function, and it is the one the RFC's own vectors can be checked against.
/// </summary>
public static class Blake2b
{
    public const int MaxHashSize = 64;
    public const int BlockSize = 128;
    public const int PersonalSize = 16;

    /// <summary>Monero's personalisation, as it writes it: six bytes, then zeroes.</summary>
    private static ReadOnlySpan<byte> MoneroPersonal => "Monero"u8;

    private static ReadOnlySpan<ulong> IV =>
    [
        0x6A09E667F3BCC908, 0xBB67AE8584CAA73B, 0x3C6EF372FE94F82B, 0xA54FF53A5F1D36F1,
        0x510E527FADE682D1, 0x9B05688C2B3E6C1F, 0x1F83D9ABFB41BD6B, 0x5BE0CD19137E2179,
    ];

    /// <summary>The message-word schedule, ten rounds of it and then the first two again.</summary>
    private static ReadOnlySpan<byte> Sigma =>
    [
         0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 15,
        14, 10,  4,  8,  9, 15, 13,  6,  1, 12,  0,  2, 11,  7,  5,  3,
        11,  8, 12,  0,  5,  2, 15, 13, 10, 14,  3,  6,  7,  1,  9,  4,
         7,  9,  3,  1, 13, 12, 11, 14,  2,  6,  5, 10,  4,  0, 15,  8,
         9,  0,  5,  7,  2,  4, 10, 15, 14,  1, 11, 12,  6,  8,  3, 13,
         2, 12,  6, 10,  0, 11,  8,  3,  4, 13,  7,  5, 15, 14,  1,  9,
        12,  5,  1, 15, 14, 13,  4, 10,  0,  7,  6,  3,  9,  2,  8, 11,
        13, 11,  7, 14, 12,  1,  3,  9,  5,  0, 15,  4,  8,  6,  2, 10,
         6, 15, 14,  9, 11,  3,  0,  8, 12,  2, 13,  7,  1,  4, 10,  5,
        10,  2,  8,  4,  7,  6,  1,  5, 15, 11,  9, 14,  3, 12, 13,  0,
         0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 15,
        14, 10,  4,  8,  9, 15, 13,  6,  1, 12,  0,  2, 11,  7,  5,  3,
    ];

    public const int MaxKeySize = 64;

    /// <summary>BLAKE2b with monero's personalisation and a full 64-byte digest.</summary>
    public static byte[] Monero(ReadOnlySpan<byte> data) => Hash(data, personal: MoneroPersonal);

    /// <summary>
    /// BLAKE2b of <paramref name="data"/>, <paramref name="length"/> bytes of digest,
    /// optionally keyed and optionally personalised.
    ///
    /// Nothing in this repository passes a key. It is here because the keyed function
    /// is what the official vector set exercises — 256 of them, inputs from nothing to
    /// 255 bytes — and a branch covered by those is better attested than the one the
    /// wallet actually calls.
    /// </summary>
    public static byte[] Hash(
        ReadOnlySpan<byte> data,
        int length = MaxHashSize,
        ReadOnlySpan<byte> key = default,
        ReadOnlySpan<byte> personal = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, MaxHashSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(key.Length, MaxKeySize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(personal.Length, PersonalSize);

        // The parameter block is 64 bytes read as eight little-endian words and xored
        // into the initial state, so the digest length and the personalisation are
        // part of the state before a byte of the message is.
        Span<byte> parameters = stackalloc byte[64];
        parameters.Clear();
        parameters[0] = (byte)length;
        parameters[1] = (byte)key.Length;
        parameters[2] = 1;              // fanout
        parameters[3] = 1;              // depth
        personal.CopyTo(parameters[48..]);

        Span<ulong> state = stackalloc ulong[8];
        for (int i = 0; i < 8; i++)
        {
            state[i] = IV[i] ^ BinaryPrimitives.ReadUInt64LittleEndian(parameters.Slice(i * 8, 8));
        }

        ulong counted = 0;

        // A key is a whole block of its own in front of the message, zero-padded, and
        // it counts towards the length like any other. With no message it is also the
        // last block — and its counter still reads a full 128 rather than the length
        // of the key.
        if (!key.IsEmpty)
        {
            Span<byte> keyBlock = stackalloc byte[BlockSize];
            keyBlock.Clear();
            key.CopyTo(keyBlock);
            counted = BlockSize;

            if (data.IsEmpty)
            {
                Compress(state, keyBlock, counted, last: true);
                return Digest(state, length);
            }

            Compress(state, keyBlock, counted, last: false);
        }

        // Strictly more than a block, not at least: the final compression must be the
        // one carrying the last bytes, and a message of exactly 128 has them in its
        // only block. Compressing that block early would finalise over nothing.
        int offset = 0;
        while (data.Length - offset > BlockSize)
        {
            counted += BlockSize;
            Compress(state, data.Slice(offset, BlockSize), counted, last: false);
            offset += BlockSize;
        }

        Span<byte> tail = stackalloc byte[BlockSize];
        tail.Clear();
        data[offset..].CopyTo(tail);
        counted += (ulong)(data.Length - offset);
        Compress(state, tail, counted, last: true);

        return Digest(state, length);
    }

    private static byte[] Digest(ReadOnlySpan<ulong> state, int length)
    {
        Span<byte> digest = stackalloc byte[MaxHashSize];
        for (int i = 0; i < 8; i++)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(digest.Slice(i * 8, 8), state[i]);
        }

        return digest[..length].ToArray();
    }

    private static void Compress(Span<ulong> state, ReadOnlySpan<byte> block, ulong counted, bool last)
    {
        Span<ulong> m = stackalloc ulong[16];
        for (int i = 0; i < 16; i++)
        {
            m[i] = BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(i * 8, 8));
        }

        Span<ulong> v = stackalloc ulong[16];
        state.CopyTo(v);
        for (int i = 0; i < 8; i++) v[8 + i] = IV[i];

        // The counter's high word stays zero: it would need a message of 2^64 bytes.
        v[12] ^= counted;
        if (last) v[14] = ~v[14];

        for (int round = 0; round < 12; round++)
        {
            ReadOnlySpan<byte> s = Sigma.Slice(round * 16, 16);

            Mix(v, 0, 4,  8, 12, m[s[0]],  m[s[1]]);
            Mix(v, 1, 5,  9, 13, m[s[2]],  m[s[3]]);
            Mix(v, 2, 6, 10, 14, m[s[4]],  m[s[5]]);
            Mix(v, 3, 7, 11, 15, m[s[6]],  m[s[7]]);
            Mix(v, 0, 5, 10, 15, m[s[8]],  m[s[9]]);
            Mix(v, 1, 6, 11, 12, m[s[10]], m[s[11]]);
            Mix(v, 2, 7,  8, 13, m[s[12]], m[s[13]]);
            Mix(v, 3, 4,  9, 14, m[s[14]], m[s[15]]);
        }

        for (int i = 0; i < 8; i++) state[i] ^= v[i] ^ v[8 + i];
    }

    private static void Mix(Span<ulong> v, int a, int b, int c, int d, ulong x, ulong y)
    {
        v[a] = v[a] + v[b] + x;
        v[d] = BitOperations.RotateRight(v[d] ^ v[a], 32);
        v[c] += v[d];
        v[b] = BitOperations.RotateRight(v[b] ^ v[c], 24);
        v[a] = v[a] + v[b] + y;
        v[d] = BitOperations.RotateRight(v[d] ^ v[a], 16);
        v[c] += v[d];
        v[b] = BitOperations.RotateRight(v[b] ^ v[c], 63);
    }
}
