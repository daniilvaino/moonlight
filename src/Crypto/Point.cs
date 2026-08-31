using MoneroRing.Crypto;
using MoneroSharp.NaCl.Internal.Ed25519Ref10;

namespace Moonlight.Crypto;

/// <summary>
/// A point on ed25519, stored compressed and known to decompress. As with
/// <see cref="Scalar"/>, the validity check happens once, at construction.
/// </summary>
public readonly struct Point : IEquatable<Point>
{
    public const int Size = 32;

    private readonly byte[]? value;

    private Point(byte[] valid) => value = valid;

    private byte[] Bytes => value ?? new byte[Size];

    /// <summary>Decompresses to check the bytes — monero's check_key.</summary>
    public static bool TryFromBytes(ReadOnlySpan<byte> bytes, out Point point)
    {
        point = default;

        if (bytes.Length != Size)
        {
            return false;
        }

        byte[] copy = bytes.ToArray();
        if (!RingSig.check_key(copy))
        {
            return false;
        }

        point = new Point(copy);
        return true;
    }

    public static Point FromBytes(ReadOnlySpan<byte> bytes)
        => TryFromBytes(bytes, out Point p) ? p : throw new ArgumentException("not a valid point", nameof(bytes));

    /// <summary>scalar · G — monero's secret_key_to_public_key.</summary>
    public static Point FromSecret(Scalar secret)
    {
        byte[] pub = new byte[Size];
        return RingSig.secret_key_to_public_key(secret.ToBytes(), pub)
            ? new Point(pub)
            : throw new ArgumentException("scalar does not yield a public key", nameof(secret));
    }

    /// <summary>
    /// ge_fromfe_frombytes_vartime on a hash, and nothing more — monero's
    /// hash_to_point. Not the same as <see cref="HashToEc"/>, which hashes first
    /// and multiplies by 8; confusing the two gives a valid point and a wrong one.
    /// </summary>
    public static Point FromHash(ReadOnlySpan<byte> hash)
    {
        RingSig.ge_fromfe_frombytes_vartime(out GroupElementP2 p2, hash.ToArray());

        byte[] bytes = new byte[Size];
        GroupOperations.ge_tobytes(bytes, 0, ref p2);
        return new Point(bytes);
    }

    /// <summary>H(data) mapped to the curve and multiplied by 8 — monero's biased_hash_to_ec.</summary>
    public static Point HashToEc(ReadOnlySpan<byte> data)
    {
        byte[] bytes = new byte[Size];
        RingSig.hash_to_ec(data.ToArray(), bytes);
        return new Point(bytes);
    }

    /// <summary>The key image of an output: secret · H(pub).</summary>
    public static Point KeyImage(Point publicKey, Scalar secret)
    {
        byte[] image = new byte[Size];
        RingSig.generate_key_image(publicKey.ToBytes(), secret.ToBytes(), image);
        return new Point(image);
    }

    public static Point operator +(Point a, Point b)
    {
        if (RingSig.ge_frombytes_vartime(out GroupElementP3 pa, a.Bytes) != 0 ||
            RingSig.ge_frombytes_vartime(out GroupElementP3 pb, b.Bytes) != 0)
        {
            throw new InvalidOperationException("a Point that does not decompress");
        }

        GroupOperations.ge_p3_to_cached(out GroupElementCached cached, ref pb);
        GroupOperations.ge_add(out GroupElementP1P1 sum, ref pa, ref cached);
        GroupOperations.ge_p1p1_to_p2(out GroupElementP2 result, ref sum);

        byte[] bytes = new byte[Size];
        GroupOperations.ge_tobytes(bytes, 0, ref result);
        return new Point(bytes);
    }

    public static Point Add(Point a, Point b) => a + b;

    public static Point operator -(Point a, Point b)
    {
        if (RingSig.ge_frombytes_vartime(out GroupElementP3 pa, a.Bytes) != 0 ||
            RingSig.ge_frombytes_vartime(out GroupElementP3 pb, b.Bytes) != 0)
        {
            throw new InvalidOperationException("a Point that does not decompress");
        }

        GroupOperations.ge_p3_to_cached(out GroupElementCached cached, ref pb);
        GroupOperations.ge_sub(out GroupElementP1P1 difference, ref pa, ref cached);
        GroupOperations.ge_p1p1_to_p2(out GroupElementP2 result, ref difference);

        byte[] bytes = new byte[Size];
        GroupOperations.ge_tobytes(bytes, 0, ref result);
        return new Point(bytes);
    }

    public static Point Subtract(Point a, Point b) => a - b;

    /// <summary>scalar · point.</summary>
    public static Point operator *(Scalar scalar, Point point)
    {
        if (RingSig.ge_frombytes_vartime(out GroupElementP3 p, point.Bytes) != 0)
        {
            throw new InvalidOperationException("a Point that does not decompress");
        }

        RingSig.ge_scalarmult(out GroupElementP2 result, scalar.ToBytes(), ref p);

        byte[] bytes = new byte[Size];
        GroupOperations.ge_tobytes(bytes, 0, ref result);
        return new Point(bytes);
    }

    public static Point Multiply(Scalar scalar, Point point) => scalar * point;

    /// <summary>scalar · G, where the scalar need not be a valid secret key.</summary>
    public static Point BaseMultiply(Scalar scalar)
    {
        GroupOperations.ge_scalarmult_base(out GroupElementP3 result, scalar.ToBytes(), 0);

        byte[] bytes = new byte[Size];
        GroupOperations.ge_p3_tobytes(bytes, 0, ref result);
        return new Point(bytes);
    }

    /// <summary>The neutral element, as monero encodes it.</summary>
    public static Point Identity { get; } = FromBytes(
        [1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]);

    public bool IsIdentity => Equals(Identity);

    public byte[] ToBytes() => Bytes.AsSpan().ToArray();

    public bool Equals(Point other) => Bytes.AsSpan().SequenceEqual(other.Bytes);

    public override bool Equals(object? obj) => obj is Point other && Equals(other);

    public override int GetHashCode() => BitConverter.ToInt32(Bytes, 0);

    public static bool operator ==(Point a, Point b) => a.Equals(b);

    public static bool operator !=(Point a, Point b) => !a.Equals(b);

    public override string ToString() => Convert.ToHexString(Bytes).ToLowerInvariant();
}
