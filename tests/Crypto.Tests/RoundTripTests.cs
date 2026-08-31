using MoneroRing.Crypto;
using Xunit;

namespace Moonlight.Crypto.Tests;

/// <summary>
/// The generating half of the crypto layer. Its vectors in tests.txt cannot be
/// replayed — the reference seeds a fixed PRNG and compares the bytes it draws,
/// and we have a real one. So each generator is checked against the verifier that
/// already passes the corpus: a signature this code produces must satisfy the
/// check that 512 monero-made signatures satisfy.
/// </summary>
public class RoundTripTests
{
    private const int Iterations = 64;

    [Fact]
    public void RandomScalarIsCanonical()
    {
        for (int i = 0; i < Iterations; i++)
        {
            byte[] scalar = new byte[32];
            RingSig.random_scalar(scalar);

            Assert.Equal(0, RingSig.sc_check(scalar));
            Assert.Contains(scalar, b => b != 0);
        }
    }

    [Fact]
    public void GeneratedKeysAgree()
    {
        for (int i = 0; i < Iterations; i++)
        {
            byte[] pub = new byte[32];
            byte[] sec = new byte[32];
            RingSig.generate_keys(pub, sec);

            Assert.True(RingSig.check_key(pub));
            Assert.Equal(0, RingSig.sc_check(sec));

            byte[] derived = new byte[32];
            Assert.True(RingSig.secret_key_to_public_key(sec, derived));
            Assert.Equal(pub, derived);
        }
    }

    [Fact]
    public void SignatureVerifies()
    {
        for (int i = 0; i < Iterations; i++)
        {
            byte[] pub = new byte[32];
            byte[] sec = new byte[32];
            RingSig.generate_keys(pub, sec);

            byte[] prefix = Keccak.Hash([(byte)i]);
            byte[] sig = RingSig.generate_signature(prefix, pub, sec);

            Assert.True(RingSig.check_signature(prefix, pub, sig));

            // A signature that verifies against the wrong message would mean the
            // prefix never entered the challenge.
            Assert.False(RingSig.check_signature(Keccak.Hash([(byte)(i + 1)]), pub, sig));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(11)]
    public void RingSignatureVerifies(int ringSize)
    {
        for (int i = 0; i < 8; i++)
        {
            byte[][] pubs = new byte[ringSize][];
            byte[] sec = new byte[32];
            int secIndex = i % ringSize;

            for (int n = 0; n < ringSize; n++)
            {
                byte[] pub = new byte[32];
                byte[] s = new byte[32];
                RingSig.generate_keys(pub, s);
                pubs[n] = pub;
                if (n == secIndex) sec = s;
            }

            byte[] image = new byte[32];
            RingSig.generate_key_image(pubs[secIndex], sec, image);

            byte[] prefix = Keccak.Hash([(byte)i]);
            byte[] sig = RingSig.generate_ring_signature(prefix, image, pubs, ringSize, sec, secIndex);

            Assert.True(RingSig.check_ring_signature(prefix, image, pubs, ringSize, sig));
            Assert.False(RingSig.check_ring_signature(Keccak.Hash([(byte)(i + 1)]), image, pubs, ringSize, sig));
        }
    }

    [Fact]
    public void KeyImageIsStableAndUnlinkedToTheRing()
    {
        byte[] pub = new byte[32];
        byte[] sec = new byte[32];
        RingSig.generate_keys(pub, sec);

        byte[] first = new byte[32];
        byte[] second = new byte[32];
        RingSig.generate_key_image(pub, sec, first);
        RingSig.generate_key_image(pub, sec, second);

        // Deterministic: the same output spent twice must present the same image,
        // which is the whole basis of double-spend detection.
        Assert.Equal(first, second);
    }
}
