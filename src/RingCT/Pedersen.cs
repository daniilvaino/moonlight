using Moonlight.Crypto;
using MoneroRing.Crypto;
using MoneroSharp.NaCl.Internal.Ed25519Ref10;

namespace Moonlight.RingCT;

/// <summary>
/// Amount commitments: <c>C = mask·G + amount·H</c>. The mask hides the amount;
/// the homomorphism is what lets a verifier check that inputs equal outputs
/// without learning either.
/// </summary>
public static class Pedersen
{
    /// <summary>
    /// The second generator, a fixed constant in monero rather than something
    /// derived at runtime. Nobody knows its discrete log with respect to G, which
    /// is the whole reason a commitment binds.
    /// </summary>
    public static ReadOnlySpan<byte> H =>
        [0x8b, 0x65, 0x59, 0x70, 0x15, 0x37, 0x99, 0xaf, 0x2a, 0xea, 0xdc, 0x9f, 0xf1, 0xad, 0xd0, 0xea,
         0x6c, 0x72, 0x51, 0xd5, 0x41, 0x54, 0xcf, 0xa9, 0x2c, 0x17, 0x3a, 0x0d, 0xd3, 0x9c, 0x1f, 0x94];

    public static Point Commit(ulong amount, Scalar mask)
    {
        if (RingSig.ge_frombytes_vartime(out GroupElementP3 h, H.ToArray()) != 0)
        {
            throw new InvalidOperationException("H failed to decompress");
        }

        RingSig.ge_double_scalarmult_base_vartime(out GroupElementP2 c, ToScalarBytes(amount), ref h, mask.ToBytes());

        byte[] bytes = new byte[32];
        GroupOperations.ge_tobytes(bytes, 0, ref c);
        return Point.FromBytes(bytes);
    }

    /// <summary>A commitment with a zero mask: what a plaintext amount looks like.</summary>
    public static Point CommitPlain(ulong amount) => Commit(amount, Scalar.Zero);

    /// <summary>An amount as a scalar: eight little-endian bytes, zero-padded.</summary>
    private static byte[] ToScalarBytes(ulong amount)
    {
        byte[] scalar = new byte[32];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(scalar, amount);
        return scalar;
    }
}
