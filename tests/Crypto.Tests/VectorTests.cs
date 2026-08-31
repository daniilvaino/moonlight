using MoneroRing.Crypto;
using Xunit;

namespace Moonlight.Crypto.Tests;

/// <summary>
/// The vendored crypto layer against monero's own vectors. One test per operation,
/// each running every line the corpus has for it.
/// </summary>
public class VectorTests
{
    [Fact]
    public void CheckScalar()
        => Run("check_scalar", v => Assert.Equal(v.Flag(1), RingSig.sc_check(v.Bytes(0)) == 0));

    [Fact]
    public void CheckKey()
        => Run("check_key", v => Assert.Equal(v.Flag(1), RingSig.check_key(v.Bytes(0))));

    [Fact]
    public void HashToScalar()
        => Run("hash_to_scalar", v => Equal(v[1], RingSig.hash_to_scalar(v.Bytes(0))));

    [Fact]
    public void SecretKeyToPublicKey()
        => Run("secret_key_to_public_key", v =>
        {
            byte[] pub = new byte[32];
            bool ok = RingSig.secret_key_to_public_key(v.Bytes(0), pub);

            Assert.Equal(v.Flag(1), ok);
            if (ok) Equal(v[2], pub);
        });

    [Fact]
    public void GenerateKeyDerivation()
        => Run("generate_key_derivation", v =>
        {
            byte[] derivation = new byte[32];
            bool ok = RingSig.generate_key_derivation(v.Bytes(0), v.Bytes(1), derivation);

            Assert.Equal(v.Flag(2), ok);
            if (ok) Equal(v[3], derivation);
        });

    [Fact]
    public void DerivePublicKey()
        => Run("derive_public_key", v =>
        {
            byte[] derived = new byte[32];
            bool ok = RingSig.derive_public_key(v.Bytes(0), v.OutputIndex(1), v.Bytes(2), derived);

            Assert.Equal(v.Flag(3), ok);
            if (ok) Equal(v[4], derived);
        });

    [Fact]
    public void DeriveSecretKey()
        => Run("derive_secret_key", v =>
        {
            byte[] derived = new byte[32];
            RingSig.derive_secret_key(v.Bytes(0), v.OutputIndex(1), v.Bytes(2), derived);
            Equal(v[3], derived);
        });

    [Fact]
    public void GenerateKeyImage()
        => Run("generate_key_image", v =>
        {
            byte[] image = new byte[32];
            RingSig.generate_key_image(v.Bytes(0), v.Bytes(1), image);
            Equal(v[2], image);
        });

    [Fact]
    public void CheckSignature()
        => Run("check_signature", v =>
            Assert.Equal(v.Flag(3), RingSig.check_signature(v.Bytes(0), v.Bytes(1), v.Bytes(2))));

    [Fact]
    public void CheckRingSignature()
        => Run("check_ring_signature", v =>
        {
            int count = v.Index(2);
            byte[][] keys = new byte[count][];
            for (int i = 0; i < count; i++) keys[i] = v.Bytes(3 + i);

            bool ok = RingSig.check_ring_signature(v.Bytes(0), v.Bytes(1), keys, count, v.Bytes(3 + count));
            Assert.Equal(v.Flag(4 + count), ok);
        });

    private static void Run(string op, Action<Vector> check)
    {
        int n = 0;

        foreach (Vector v in TestVectors.Read(op))
        {
            check(v);
            n++;
        }

        Assert.NotEqual(0, n);
    }

    private static void Equal(string expectedHex, byte[] actual)
        => Assert.Equal(expectedHex, Convert.ToHexString(actual).ToLowerInvariant());
}
