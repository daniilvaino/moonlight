using MoneroRing.Crypto;

namespace Moonlight.Crypto;

/// <summary>
/// A scalar mod l, always canonical. Constructing one is the only place a
/// 32-byte blob is checked, so anything downstream that holds a Scalar holds a
/// value ref10 will accept.
/// </summary>
public readonly struct Scalar : IEquatable<Scalar>
{
    public const int Size = 32;

    private readonly byte[]? value;

    private Scalar(byte[] canonical) => value = canonical;

    public static Scalar Zero => new(new byte[Size]);

    public bool IsZero => Bytes.All(b => b == 0);

    private byte[] Bytes => value ?? new byte[Size];

    /// <summary>Accepts only a canonical encoding — the check monero calls sc_check.</summary>
    public static bool TryFromCanonical(ReadOnlySpan<byte> bytes, out Scalar scalar)
    {
        scalar = Zero;

        if (bytes.Length != Size)
        {
            return false;
        }

        byte[] copy = bytes.ToArray();
        if (RingSig.sc_check(copy) != 0)
        {
            return false;
        }

        scalar = new Scalar(copy);
        return true;
    }

    public static Scalar FromCanonical(ReadOnlySpan<byte> bytes)
        => TryFromCanonical(bytes, out Scalar s) ? s : throw new ArgumentException("not a canonical scalar", nameof(bytes));

    /// <summary>Reduces any 32 bytes mod l. Use where monero uses sc_reduce32.</summary>
    public static Scalar Reduce(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Size)
        {
            throw new ArgumentException($"expected {Size} bytes", nameof(bytes));
        }

        byte[] copy = bytes.ToArray();
        RingSig.sc_reduce32(copy);
        return new Scalar(copy);
    }

    public static Scalar Random()
    {
        byte[] bytes = new byte[Size];
        RingSig.random_scalar(bytes);
        return new Scalar(bytes);
    }

    /// <summary>H(data) reduced mod l — monero's hash_to_scalar.</summary>
    public static Scalar Hash(ReadOnlySpan<byte> data)
        => new(RingSig.hash_to_scalar(data.ToArray()));

    public byte[] ToBytes() => Bytes.AsSpan().ToArray();

    public static Scalar operator +(Scalar a, Scalar b)
    {
        byte[] result = new byte[Size];
        RingSig.sc_add(result, a.Bytes, b.Bytes);
        return new Scalar(result);
    }

    public static Scalar operator -(Scalar a, Scalar b)
    {
        byte[] result = new byte[Size];
        RingSig.sc_sub(result, a.Bytes, b.Bytes);
        return new Scalar(result);
    }

    public static Scalar operator *(Scalar a, Scalar b)
    {
        byte[] result = new byte[Size];
        MoneroSharp.NaCl.Internal.Ed25519Ref10.ScalarOperations.sc_muladd(result, a.Bytes, b.Bytes, Zero.Bytes);
        return new Scalar(result);
    }

    public static Scalar One { get; } = FromCanonical([1, .. new byte[31]]);

    /// <summary>
    /// Multiplicative inverse by Fermat: a^(l-2) mod l. ref10 has no inversion, and
    /// the exponent is fixed, so square-and-multiply over its 253 bits is both the
    /// simplest way and a constant number of operations.
    /// </summary>
    public static Scalar Invert(Scalar value)
    {
        if (value.IsZero)
        {
            throw new DivideByZeroException("zero has no inverse");
        }

        // l - 2, little-endian. l = 2^252 + 27742317777372353535851937790883648493.
        ReadOnlySpan<byte> exponent =
        [
            0xeb, 0xd3, 0xf5, 0x5c, 0x1a, 0x63, 0x12, 0x58, 0xd6, 0x9c, 0xf7, 0xa2, 0xde, 0xf9, 0xde, 0x14,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10,
        ];

        Scalar result = One;
        Scalar square = value;

        for (int byteIndex = 0; byteIndex < 32; byteIndex++)
        {
            for (int bit = 0; bit < 8; bit++)
            {
                if ((exponent[byteIndex] >> bit & 1) != 0)
                {
                    result *= square;
                }

                square *= square;
            }
        }

        return result;
    }

    public static Scalar Add(Scalar a, Scalar b) => a + b;

    public static Scalar Subtract(Scalar a, Scalar b) => a - b;

    public static Scalar Multiply(Scalar a, Scalar b) => a * b;

    public bool Equals(Scalar other) => Bytes.AsSpan().SequenceEqual(other.Bytes);

    public override bool Equals(object? obj) => obj is Scalar other && Equals(other);

    public override int GetHashCode() => BitConverter.ToInt32(Bytes, 0);

    public static bool operator ==(Scalar a, Scalar b) => a.Equals(b);

    public static bool operator !=(Scalar a, Scalar b) => !a.Equals(b);

    public override string ToString() => Convert.ToHexString(Bytes).ToLowerInvariant();
}
