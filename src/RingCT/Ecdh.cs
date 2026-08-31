using Moonlight.Crypto;

namespace Moonlight.RingCT;

/// <summary>
/// How the amount and its mask travel to the recipient, hidden under the shared
/// secret. Two schemes: the original sent both as scalars, and from Bulletproof2
/// on the mask is derived rather than sent and the amount is eight bytes XORed
/// with a keystream.
/// </summary>
public static class Ecdh
{
    private static ReadOnlySpan<byte> AmountSalt => "amount"u8;

    private static ReadOnlySpan<byte> CommitmentMaskSalt => "commitment_mask"u8;

    /// <summary>H("commitment_mask" || sharedSecret), reduced.</summary>
    public static Scalar CommitmentMask(ReadOnlySpan<byte> sharedSecret)
        => Scalar.Hash(Concat(CommitmentMaskSalt, sharedSecret));

    /// <summary>H("amount" || sharedSecret) — not reduced; only its first eight bytes are used.</summary>
    public static byte[] AmountEncodingFactor(ReadOnlySpan<byte> sharedSecret)
        => Keccak.Hash(Concat(AmountSalt, sharedSecret));

    /// <summary>The short form: mask derived, amount XORed into eight bytes.</summary>
    public static byte[] EncodeAmount(ulong amount, ReadOnlySpan<byte> sharedSecret)
    {
        byte[] encoded = new byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(encoded, amount);

        Xor8(encoded, AmountEncodingFactor(sharedSecret));
        return encoded;
    }

    public static ulong DecodeAmount(ReadOnlySpan<byte> encoded, ReadOnlySpan<byte> sharedSecret)
    {
        if (encoded.Length != 8)
        {
            throw new ArgumentException("an encoded amount is 8 bytes", nameof(encoded));
        }

        Span<byte> plain = stackalloc byte[8];
        encoded.CopyTo(plain);
        Xor8(plain, AmountEncodingFactor(sharedSecret));

        return System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(plain);
    }

    /// <summary>
    /// The original form, before Bulletproof2: both fields are full scalars, each
    /// blinded by a further hash of the shared secret.
    /// </summary>
    public static (Scalar Mask, Scalar Amount) EncodeLegacy(Scalar mask, Scalar amount, ReadOnlySpan<byte> sharedSecret)
    {
        (Scalar first, Scalar second) = LegacyFactors(sharedSecret);
        return (mask + first, amount + second);
    }

    public static (Scalar Mask, Scalar Amount) DecodeLegacy(Scalar mask, Scalar amount, ReadOnlySpan<byte> sharedSecret)
    {
        (Scalar first, Scalar second) = LegacyFactors(sharedSecret);
        return (mask - first, amount - second);
    }

    private static (Scalar, Scalar) LegacyFactors(ReadOnlySpan<byte> sharedSecret)
    {
        Scalar first = Scalar.Hash(sharedSecret);
        return (first, Scalar.Hash(first.ToBytes()));
    }

    private static void Xor8(Span<byte> value, ReadOnlySpan<byte> keystream)
    {
        for (int i = 0; i < 8; i++) value[i] ^= keystream[i];
    }

    private static byte[] Concat(ReadOnlySpan<byte> salt, ReadOnlySpan<byte> tail)
    {
        byte[] buffer = new byte[salt.Length + tail.Length];
        salt.CopyTo(buffer);
        tail.CopyTo(buffer.AsSpan(salt.Length));
        return buffer;
    }
}
