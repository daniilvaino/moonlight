using Moonlight.Crypto;

namespace Moonlight.RingCT.BulletproofPlus;

/// <summary>
/// The public generator vectors, derived from H by hashing. Nobody knows their
/// discrete logs, which is what makes the proof binding, and everybody derives
/// the same ones, which is what makes it verifiable.
/// </summary>
/// <remarks>Ported from <c>get_exponent</c> and <c>init_exponents</c> in <c>bulletproofs_plus.cc</c>.</remarks>
public static class Generators
{
    /// <summary>Bits per range proof — amounts are 64-bit.</summary>
    public const int N = 64;

    public const int LogN = 6;

    /// <summary>The most outputs a single aggregated proof may cover.</summary>
    public const int MaxM = 16;

    public const int MaxMN = MaxM * N;

    private static ReadOnlySpan<byte> ExponentSalt => "bulletproof_plus"u8;

    private static ReadOnlySpan<byte> TranscriptSalt => "bulletproof_plus_transcript"u8;

    private static readonly object Gate = new();
    private static readonly Point[] GiCache = new Point[MaxMN];
    private static readonly Point[] HiCache = new Point[MaxMN];
    private static int generated;

    /// <summary>Where every proof's transcript starts.</summary>
    public static Point InitialTranscript { get; } = Point.HashToEc(Keccak.Hash(TranscriptSalt));

    public static Point Gi(int index)
    {
        Ensure(index + 1);
        return GiCache[index];
    }

    public static Point Hi(int index)
    {
        Ensure(index + 1);
        return HiCache[index];
    }

    /// <summary>
    /// H, then the domain separator, then the index as a varint — hashed, then
    /// mapped to the curve. The pair for slot i uses indices 2i and 2i+1, so Hi
    /// comes first.
    /// </summary>
    private static Point Exponent(int index)
    {
        Span<byte> varint = stackalloc byte[VarInt.MaxLength];
        int varintLength = VarInt.Write(varint, (ulong)index);

        byte[] buffer = new byte[32 + ExponentSalt.Length + varintLength];
        Pedersen.H.CopyTo(buffer);
        ExponentSalt.CopyTo(buffer.AsSpan(32));
        varint[..varintLength].CopyTo(buffer.AsSpan(32 + ExponentSalt.Length));

        Point generator = Point.HashToEc(Keccak.Hash(buffer));

        return generator.IsIdentity
            ? throw new InvalidOperationException($"generator {index} is the point at infinity")
            : generator;
    }

    private static void Ensure(int count)
    {
        if (count <= generated) return;

        lock (Gate)
        {
            for (int i = generated; i < count; i++)
            {
                HiCache[i] = Exponent(i * 2);
                GiCache[i] = Exponent((i * 2) + 1);
            }

            generated = Math.Max(generated, count);
        }
    }
}
