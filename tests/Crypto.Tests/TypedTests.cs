using Xunit;

namespace Moonlight.Crypto.Tests;

/// <summary>
/// The same corpus, through the typed API. The vendored functions already pass it;
/// what is under test here is the wrapper — that construction validates, that the
/// operators map to the right primitive, and that nothing is copied by reference.
/// </summary>
public class TypedTests
{
    [Fact]
    public void ScalarConstructionEnforcesCanonical()
        => Run("check_scalar", v => Assert.Equal(v.Flag(1), Scalar.TryFromCanonical(v.Bytes(0), out _)));

    [Fact]
    public void PointConstructionEnforcesValidity()
        => Run("check_key", v => Assert.Equal(v.Flag(1), Point.TryFromBytes(v.Bytes(0), out _)));

    [Fact]
    public void ScalarHashMatchesCorpus()
        => Run("hash_to_scalar", v => Equal(v[1], Scalar.Hash(v.Bytes(0)).ToBytes()));

    [Fact]
    public void PointFromHashMatchesCorpus()
        => Run("hash_to_point", v => Equal(v[1], Point.FromHash(v.Bytes(0)).ToBytes()));

    [Fact]
    public void PointHashToEcMatchesCorpus()
        => Run("biased_hash_to_ec", v => Equal(v[1], Point.HashToEc(v.Bytes(0)).ToBytes()));

    [Fact]
    public void PublicKeyFromSecretMatchesCorpus()
        => Run("secret_key_to_public_key", v =>
        {
            if (!v.Flag(1)) return;

            Scalar secret = Scalar.FromCanonical(v.Bytes(0));
            Equal(v[2], Point.FromSecret(secret).ToBytes());
        });

    [Fact]
    public void KeyImageMatchesCorpus()
        => Run("generate_key_image", v =>
        {
            Point pub = Point.FromBytes(v.Bytes(0));
            Scalar sec = Scalar.FromCanonical(v.Bytes(1));

            Equal(v[2], Point.KeyImage(pub, sec).ToBytes());
        });

    [Fact]
    public void ArithmeticIsConsistent()
    {
        for (int i = 0; i < 32; i++)
        {
            Scalar a = Scalar.Random();
            Scalar b = Scalar.Random();

            Assert.Equal(a, (a + b) - b);
            Assert.Equal(a + b, b + a);
            Assert.Equal(a * b, b * a);
            Assert.Equal(a, a + Scalar.Zero);
            Assert.Equal(Scalar.Zero, a * Scalar.Zero);
            Assert.Equal(Scalar.Zero, a - a);
        }
    }

    [Fact]
    public void InversionUndoesMultiplication()
    {
        for (int i = 0; i < 16; i++)
        {
            Scalar a = Scalar.Random();

            Assert.Equal(Scalar.One, a * Scalar.Invert(a));
            Assert.Equal(a, Scalar.Invert(Scalar.Invert(a)));
        }

        Assert.Equal(Scalar.One, Scalar.Invert(Scalar.One));
        Assert.Throws<DivideByZeroException>(() => Scalar.Invert(Scalar.Zero));
    }

    [Fact]
    public void ToBytesDoesNotExposeInternalState()
    {
        Scalar scalar = Scalar.Random();
        byte[] bytes = scalar.ToBytes();
        bytes[0] ^= 0xFF;

        Assert.NotEqual(bytes, scalar.ToBytes());
    }

    [Fact]
    public void DefaultValuesAreUsable()
    {
        // A default(Scalar) has no backing array; it must still behave as zero
        // rather than throwing somewhere far from here.
        Assert.True(default(Scalar).IsZero);
        Assert.Equal(Scalar.Zero, default);
        Assert.Equal(32, default(Point).ToBytes().Length);
    }

    private static void Run(string op, Action<Vector> check)
    {
        int n = 0;
        foreach (Vector v in TestVectors.Read(op)) { check(v); n++; }
        Assert.NotEqual(0, n);
    }

    private static void Equal(string expectedHex, byte[] actual)
        => Assert.Equal(expectedHex, Convert.ToHexString(actual).ToLowerInvariant());
}
