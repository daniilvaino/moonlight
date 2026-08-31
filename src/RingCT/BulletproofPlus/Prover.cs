using Moonlight.Crypto;

namespace Moonlight.RingCT.BulletproofPlus;

/// <summary>
/// Bulletproof+ proving. Ported from <c>bulletproof_plus_PROVE</c> in
/// <c>bulletproofs_plus.cc</c>.
/// </summary>
/// <remarks>
/// The shape is an inner-product argument: the vectors halve each round, so a
/// proof over 64·M bits costs log2(64·M) rounds and carries one L and one R per
/// round. Everything the prover publishes is divided by the cofactor first, which
/// is why the verifier multiplies by eight on the way in.
/// </remarks>
public static class Prover
{
    private static readonly Scalar One = Scalar.One;
    private static readonly Scalar Two = Scalar.One + Scalar.One;
    private static readonly Scalar MinusOne = Scalar.Zero - Scalar.One;
    private static readonly Scalar InverseEight = Scalar.Invert(Scalar.FromCanonical([8, .. new byte[31]]));
    private static readonly Scalar MinusInverseEight = Scalar.Zero - InverseEight;

    private static readonly Point H = Point.FromBytes(Pedersen.H);

    /// <param name="amounts">The amounts being committed to, one per output.</param>
    /// <param name="masks">Their blinding factors, in the same order.</param>
    public static Proof Prove(IReadOnlyList<ulong> amounts, IReadOnlyList<Scalar> masks)
    {
        ArgumentNullException.ThrowIfNull(amounts);
        ArgumentNullException.ThrowIfNull(masks);

        if (amounts.Count != masks.Count || amounts.Count == 0)
        {
            throw new ArgumentException("one mask per amount, and at least one amount", nameof(masks));
        }

        int logM = 0;
        while ((1 << logM) <= Generators.MaxM && (1 << logM) < amounts.Count) logM++;

        int m = 1 << logM;
        int mn = m * Generators.N;
        int rounds = logM + Generators.LogN;

        // The commitments, divided by the cofactor as they travel.
        Point[] v = new Point[amounts.Count];
        for (int i = 0; i < amounts.Count; i++)
        {
            v[i] = InverseEight * Pedersen.Commit(amounts[i], masks[i]);
        }

        // The bit vectors: aL is the amount in binary, aR is aL - 1 componentwise.
        Scalar[] aL = new Scalar[mn];
        Scalar[] aR = new Scalar[mn];
        Scalar[] aL8 = new Scalar[mn];
        Scalar[] aR8 = new Scalar[mn];

        for (int j = 0; j < m; j++)
        {
            for (int i = 0; i < Generators.N; i++)
            {
                bool bit = j < amounts.Count && ((amounts[j] >> i) & 1) != 0;

                aL[(j * Generators.N) + i] = bit ? One : Scalar.Zero;
                aL8[(j * Generators.N) + i] = bit ? InverseEight : Scalar.Zero;
                aR[(j * Generators.N) + i] = bit ? Scalar.Zero : MinusOne;
                aR8[(j * Generators.N) + i] = bit ? Scalar.Zero : MinusInverseEight;
            }
        }

        while (true)
        {
            Proof? proof = Attempt(amounts, masks, v, aL, aR, aL8, aR8, mn, rounds);
            if (proof is not null) return proof;
        }
    }

    /// <summary>
    /// One attempt. A challenge of zero would break the argument, so the reference
    /// starts over with fresh nonces rather than continuing; it has never happened.
    /// </summary>
    private static Proof? Attempt(
        IReadOnlyList<ulong> amounts,
        IReadOnlyList<Scalar> masks,
        Point[] v,
        Scalar[] aL,
        Scalar[] aR,
        Scalar[] aL8,
        Scalar[] aR8,
        int mn,
        int rounds)
    {
        Transcript transcript = new(Generators.InitialTranscript.ToBytes());
        transcript.Update(Scalar.Hash(Concat(v)).ToBytes());

        Scalar alpha = Scalar.Random();
        Point a = VectorExponent(aL8, aR8) + Point.BaseMultiply(alpha * InverseEight);

        Scalar y = transcript.Update(a.ToBytes());
        if (y.IsZero) return null;

        Scalar z = Scalar.Hash(y.ToBytes());
        if (z.IsZero) return null;
        transcript.Set(z);

        Scalar zSquared = z * z;

        Scalar[] d = new Scalar[mn];
        d[0] = zSquared;
        for (int i = 1; i < Generators.N; i++) d[i] = d[i - 1] * Two;
        for (int j = 1; j < mn / Generators.N; j++)
        {
            for (int i = 0; i < Generators.N; i++)
            {
                d[(j * Generators.N) + i] = d[((j - 1) * Generators.N) + i] * zSquared;
            }
        }

        Scalar[] yPowers = ScalarPowers(y, mn + 2);

        Scalar[] aPrime = new Scalar[mn];
        Scalar[] bPrime = new Scalar[mn];
        for (int i = 0; i < mn; i++)
        {
            aPrime[i] = aL[i] - z;
            bPrime[i] = aR[i] + z + (d[i] * yPowers[mn - i]);
        }

        Scalar alpha1 = alpha;
        Scalar running = One;
        for (int j = 0; j < amounts.Count; j++)
        {
            running *= zSquared;
            alpha1 += yPowers[mn + 1] * running * masks[j];
        }

        Point[] gPrime = new Point[mn];
        Point[] hPrime = new Point[mn];
        Scalar[] yInversePowers = new Scalar[mn];
        Scalar yInverse = Scalar.Invert(y);

        yInversePowers[0] = One;
        for (int i = 0; i < mn; i++)
        {
            gPrime[i] = Generators.Gi(i);
            hPrime[i] = Generators.Hi(i);
            if (i > 0) yInversePowers[i] = yInversePowers[i - 1] * yInverse;
        }

        Point[] l = new Point[rounds];
        Point[] r = new Point[rounds];
        int size = mn;

        for (int round = 0; round < rounds; round++)
        {
            size /= 2;

            Scalar cL = WeightedInnerProduct(aPrime[..size], bPrime[size..], y);
            Scalar cR = WeightedInnerProduct(Scale(aPrime[size..], yPowers[size]), bPrime[..size], y);

            Scalar dL = Scalar.Random();
            Scalar dR = Scalar.Random();

            l[round] = ComputeLR(size, yInversePowers[size], gPrime, size, hPrime, 0, aPrime, 0, bPrime, size, cL, dL);
            r[round] = ComputeLR(size, yPowers[size], gPrime, 0, hPrime, size, aPrime, size, bPrime, 0, cR, dR);

            Scalar challenge = transcript.Update(l[round].ToBytes(), r[round].ToBytes());
            if (challenge.IsZero) return null;

            Scalar challengeInverse = Scalar.Invert(challenge);

            gPrime = Fold(gPrime, challengeInverse, yInversePowers[size] * challenge);
            hPrime = Fold(hPrime, challenge, challengeInverse);

            Scalar weight = challengeInverse * yPowers[size];
            aPrime = Add(Scale(aPrime[..size], challenge), Scale(aPrime[size..], weight));
            bPrime = Add(Scale(bPrime[..size], challengeInverse), Scale(bPrime[size..], challenge));

            alpha1 += (dL * challenge * challenge) + (dR * challengeInverse * challengeInverse);
        }

        // The final round: two scalars and their blinding, committed and then opened.
        Scalar rNonce = Scalar.Random();
        Scalar sNonce = Scalar.Random();
        Scalar dNonce = Scalar.Random();
        Scalar eta = Scalar.Random();

        Point a1 = (rNonce * InverseEight * gPrime[0])
                 + (sNonce * InverseEight * hPrime[0])
                 + Point.BaseMultiply(dNonce * InverseEight)
                 + (((rNonce * y * bPrime[0]) + (sNonce * y * aPrime[0])) * InverseEight * H);

        Point b = Point.BaseMultiply(eta * InverseEight) + (rNonce * y * sNonce * InverseEight * H);

        Scalar e = transcript.Update(a1.ToBytes(), b.ToBytes());
        if (e.IsZero) return null;

        return new Proof(
            V: v,
            A: a,
            A1: a1,
            B: b,
            R1: rNonce + (aPrime[0] * e),
            S1: sNonce + (bPrime[0] * e),
            D1: eta + (dNonce * e) + (alpha1 * e * e),
            L: l,
            R: r);
    }

    /// <summary>sum a[i]·Gi[i] + b[i]·Hi[i].</summary>
    private static Point VectorExponent(Scalar[] a, Scalar[] b)
    {
        Point sum = Point.Identity;

        for (int i = 0; i < a.Length; i++)
        {
            if (!a[i].IsZero) sum += a[i] * Generators.Gi(i);
            if (!b[i].IsZero) sum += b[i] * Generators.Hi(i);
        }

        return sum;
    }

    private static Point ComputeLR(
        int size,
        Scalar y,
        Point[] g,
        int gOffset,
        Point[] h,
        int hOffset,
        Scalar[] a,
        int aOffset,
        Scalar[] b,
        int bOffset,
        Scalar c,
        Scalar d)
    {
        Point sum = Point.Identity;

        for (int i = 0; i < size; i++)
        {
            sum += a[aOffset + i] * y * InverseEight * g[gOffset + i];
            sum += b[bOffset + i] * InverseEight * h[hOffset + i];
        }

        return sum + (c * InverseEight * H) + Point.BaseMultiply(d * InverseEight);
    }

    /// <summary>Halves a generator vector: v[n] becomes a·v[n] + b·v[n + half].</summary>
    private static Point[] Fold(Point[] v, Scalar a, Scalar b)
    {
        int half = v.Length / 2;
        Point[] folded = new Point[half];

        for (int i = 0; i < half; i++)
        {
            folded[i] = (a * v[i]) + (b * v[half + i]);
        }

        return folded;
    }

    private static Scalar WeightedInnerProduct(Scalar[] a, Scalar[] b, Scalar y)
    {
        Scalar result = Scalar.Zero;
        Scalar power = One;

        for (int i = 0; i < a.Length; i++)
        {
            power *= y;
            result += a[i] * b[i] * power;
        }

        return result;
    }

    private static Scalar[] ScalarPowers(Scalar x, int count)
    {
        Scalar[] powers = new Scalar[count];
        powers[0] = One;

        for (int i = 1; i < count; i++) powers[i] = powers[i - 1] * x;

        return powers;
    }

    private static Scalar[] Scale(Scalar[] values, Scalar factor)
        => [.. values.Select(value => value * factor)];

    private static Scalar[] Add(Scalar[] a, Scalar[] b)
        => [.. a.Zip(b, (x, y) => x + y)];

    private static byte[] Concat(Point[] points)
    {
        byte[] buffer = new byte[points.Length * 32];
        for (int i = 0; i < points.Length; i++) points[i].ToBytes().CopyTo(buffer, i * 32);
        return buffer;
    }
}
