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
