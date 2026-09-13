using MoneroRing.Crypto;
using MoneroSharp.NaCl.Internal.Ed25519Ref10;
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

    /// <summary>
    /// The raw map: ge_fromfe_frombytes_vartime on the hash, straight to bytes.
    /// No Keccak and no ge_mul8 — those belong to biased_hash_to_ec, and confusing
    /// the two gives a point that is on the curve and simply wrong.
    /// </summary>
    [Fact]
    public void HashToPoint()
        => Run("hash_to_point", v =>
        {
            RingSig.ge_fromfe_frombytes_vartime(out GroupElementP2 point, v.Bytes(0));

            byte[] res = new byte[32];
            GroupOperations.ge_tobytes(res, 0, ref point);
            Equal(v[1], res);
        });

    [Fact]
    public void BiasedHashToEc()
        => Run("biased_hash_to_ec", v =>
        {
            byte[] res = new byte[32];
            RingSig.hash_to_ec(v.Bytes(0), res);
            Equal(v[1], res);
        });

    /// <summary>
    /// The flag is not a result here, it is which map to use: every input appears
    /// twice in the corpus, once under each, and the two give different points. The
    /// true one is the biased map — the same <c>hash_to_ec</c> that
    /// <see cref="BiasedHashToEc"/> replays 256 lines of — so it is replayed here
    /// against the generator the corpus expects.
    ///
    /// The false one is a second map we do not have; it is FCMP++ groundwork, and
    /// those hundred lines wait for it. Counted rather than skipped quietly, because
    /// a filter that silently matches nothing is a test that passes for free.
    /// </summary>
    [Fact]
    public void DeriveKeyImageGenerator()
    {
        int replayed = 0;

        foreach (Vector v in TestVectors.Read("derive_key_image_generator"))
        {
            byte[] generator;

            if (v.Flag(1))
            {
                generator = new byte[32];
                RingSig.hash_to_ec(v.Bytes(0), generator);
            }
            else
            {
                generator = Point.UnbiasedHashToEc(v.Bytes(0)).ToBytes();
            }

            Equal(v[2], generator);
            replayed++;
        }

        Assert.Equal(200, replayed);
    }

    /// <summary>
    /// Ed25519 written in the Weierstrass form FCMP++ proves over. The corpus gives
    /// the point and both coordinates, so this checks the whole map and not only
    /// that it produced something of the right length.
    /// </summary>
    [Fact]
    public void PointToWeierstrass()
        => Run("point_to_wei_x_y", v =>
        {
            Assert.True(Point.TryFromBytes(v.Bytes(0), out Point point));
            Assert.True(point.TryToWeierstrass(out byte[] x, out byte[] y));

            Equal(v[1], x);
            Equal(v[2], y);
        });

    [Fact]
    public void DeriveViewTag()
        => Run("derive_view_tag", v =>
            Equal(v[2], [ViewTag.Derive(v.Bytes(0), v.OutputIndex(1))]));

    /// <summary>
    /// The one operation in the corpus that is about representation rather than
    /// arithmetic, and the only reason it exists is to show a check that gets it
    /// wrong.
    ///
    /// Monero builds ((K+K)-K)-K, which is the identity for every K, and asks two
    /// questions of the result. The naive check reads the ten limbs of each
    /// coordinate straight out of the structure: X and T all zero, Y equal to Z. It
    /// answers false for three of these six points, because the limbs of a genuine
    /// identity need not be the reduced ones. The correct check reduces first, and
    /// says true every time.
    ///
    /// Replaying it pins something no other vector does: that our group arithmetic
    /// produces the same intermediate representations as monero's, limb for limb,
    /// and not merely the same points.
    /// </summary>
    [Fact]
    public void CheckGeP3Identity()
        => Run("check_ge_p3_identity", v =>
        {
            GroupElementP3 identity = IdentityByFourOperations(v.Bytes(0));

            Assert.Equal(v.Flag(1), LimbsSayInfinity(identity));
            Assert.Equal(v.Flag(2), ReducedSaysInfinity(identity));
        });

    /// <summary>(K + K) - K - K, in the p3 representation the operations leave behind.</summary>
    private static GroupElementP3 IdentityByFourOperations(byte[] key)
    {
        RingSig.ge_frombytes_vartime(out GroupElementP3 p3, key);
        GroupOperations.ge_p3_to_cached(out GroupElementCached cached, ref p3);

        GroupOperations.ge_add(out GroupElementP1P1 step, ref p3, ref cached);
        GroupOperations.ge_p1p1_to_p3(out p3, ref step);
        GroupOperations.ge_sub(out step, ref p3, ref cached);
        GroupOperations.ge_p1p1_to_p3(out p3, ref step);
        GroupOperations.ge_sub(out step, ref p3, ref cached);
        GroupOperations.ge_p1p1_to_p3(out p3, ref step);

        return p3;
    }

    /// <summary>
    /// Monero's broken check, reproduced as broken: the limbs are compared as they
    /// sit, without being reduced mod q first.
    /// </summary>
    private static bool LimbsSayInfinity(GroupElementP3 p)
    {
        int[] x = Limbs(p.X), y = Limbs(p.Y), z = Limbs(p.Z), t = Limbs(p.T);

        for (int i = 0; i < 10; i++)
        {
            if ((x[i] | t[i]) != 0 || y[i] != z[i]) return false;
        }

        return true;
    }

    /// <summary>
    /// The answer that is right. Encoding reduces, so a point that compresses to the
    /// identity is the identity however its limbs happen to be arranged.
    /// </summary>
    private static bool ReducedSaysInfinity(GroupElementP3 p)
    {
        byte[] bytes = new byte[32];
        GroupOperations.ge_p3_tobytes(bytes, 0, ref p);

        return Point.FromBytes(bytes).IsIdentity;
    }

    private static int[] Limbs(FieldElement f)
        => [f.x0, f.x1, f.x2, f.x3, f.x4, f.x5, f.x6, f.x7, f.x8, f.x9];

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
