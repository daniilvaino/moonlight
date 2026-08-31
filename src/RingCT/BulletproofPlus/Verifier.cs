using Moonlight.Crypto;

namespace Moonlight.RingCT.BulletproofPlus;

/// <summary>
/// Bulletproof+ verification. Ported from <c>bulletproof_plus_VERIFY</c> in
/// <c>bulletproofs_plus.cc</c>, one proof at a time — the reference batches
/// several behind random weights, which changes the cost, not the answer.
/// </summary>
public static class Verifier
{
    private static readonly Scalar Two = Scalar.One + Scalar.One;

    /// <summary>2^64 - 1, the top of the range being proved.</summary>
    private static readonly Scalar TwoSixtyFourMinusOne = BuildTwoSixtyFourMinusOne();

    public static bool Verify(Proof proof)
    {
        ArgumentNullException.ThrowIfNull(proof);

        if (proof.V.Length < 1 || proof.L.Length != proof.R.Length || proof.L.Length == 0)
        {
            return false;
        }

        // Every point on the wire is divided by the cofactor; multiplying back is
        // also what forces it into the prime-order subgroup.
        Point[] v8 = [.. proof.V.Select(MultiplyByEight)];
        Point[] l8 = [.. proof.L.Select(MultiplyByEight)];
        Point[] r8 = [.. proof.R.Select(MultiplyByEight)];
        Point a8 = MultiplyByEight(proof.A);
        Point a18 = MultiplyByEight(proof.A1);
        Point b8 = MultiplyByEight(proof.B);

        int logM = 0;
        while ((1 << logM) <= Generators.MaxM && (1 << logM) < proof.V.Length) logM++;

        if (proof.L.Length != 6 + logM) return false;

        int m = 1 << logM;
        int mn = m * Generators.N;
        int rounds = logM + Generators.LogN;

        // Challenges, in the order the prover produced them.
        Transcript transcript = new(Generators.InitialTranscript.ToBytes());
        transcript.Update(Scalar.Hash(Concat(proof.V)).ToBytes());

        Scalar y = transcript.Update(proof.A.ToBytes());
        if (y.IsZero) return false;

        Scalar z = Scalar.Hash(y.ToBytes());
        if (z.IsZero) return false;
        transcript.Set(z);

        Scalar[] challenges = new Scalar[rounds];
        for (int j = 0; j < rounds; j++)
        {
            challenges[j] = transcript.Update(proof.L[j].ToBytes(), proof.R[j].ToBytes());
            if (challenges[j].IsZero) return false;
        }

        Scalar e = transcript.Update(proof.A1.ToBytes(), proof.B.ToBytes());
        if (e.IsZero) return false;

        Scalar[] challengesInverse = [.. challenges.Select(Scalar.Invert)];
        Scalar yInverse = Scalar.Invert(y);

        // y^(MN), and y^(MN+1).
        Scalar yMn = y;
        for (int remaining = mn; remaining > 1; remaining /= 2) yMn *= yMn;
        Scalar yMn1 = yMn * y;

        Scalar eSquared = e * e;
        Scalar zSquared = z * z;
        Scalar minusOne = Scalar.Zero - Scalar.One;

        List<(Scalar Scalar, Point Point)> terms = [];

        // The commitments, each weighted by a further power of z^2.
        Scalar temp = (Scalar.Zero - eSquared) * yMn1;
        foreach (Point commitment in v8)
        {
            temp *= zSquared;
            terms.Add((temp, commitment));
        }

        temp = minusOne;
        terms.Add((temp, b8));
        temp *= e;
        terms.Add((temp, a18));

        Scalar minusESquared = temp * e;
        terms.Add((minusESquared, a8));

        Scalar gScalar = proof.D1;

        // d: z^2 scaled by the bit positions, repeated per aggregated output.
        Scalar[] d = new Scalar[mn];
        d[0] = zSquared;
        for (int i = 1; i < Generators.N; i++) d[i] = d[i - 1] + d[i - 1];
        for (int j = 1; j < m; j++)
        {
            for (int i = 0; i < Generators.N; i++) d[(j * Generators.N) + i] = d[((j - 1) * Generators.N) + i] * zSquared;
        }

        Scalar sumD = TwoSixtyFourMinusOne * SumOfEvenPowers(z, 2 * m);
        Scalar sumY = SumOfScalarPowers(y, mn);

        Scalar hScalar = ((((zSquared - z) * sumY) + (yMn1 * z * sumD)) * eSquared) + (proof.R1 * y * proof.S1);

        // The folded inner-product challenges, expanded back into one factor per slot.
        Scalar[] cache = new Scalar[1 << rounds];
        cache[0] = challengesInverse[0];
        cache[1] = challenges[0];

        for (int j = 1; j < rounds; j++)
        {
            for (int s = 1 << (j + 1); s-- > 0; --s)
            {
                cache[s] = cache[s / 2] * challenges[j];
                cache[s - 1] = cache[s / 2] * challengesInverse[j];
            }
        }

        Scalar er1y = e * proof.R1;
        Scalar es1 = e * proof.S1;
        Scalar eSquaredZ = eSquared * z;
        Scalar minusESquaredZ = Scalar.Zero - eSquaredZ;
        Scalar minusESquaredY = (Scalar.Zero - eSquared) * yMn;

        for (int i = 0; i < mn; i++)
        {
            Scalar gi = (er1y * cache[i]) + eSquaredZ;
            Scalar hi = (es1 * cache[~i & (mn - 1)]) + minusESquaredZ + (minusESquaredY * d[i]);

            terms.Add((gi, Generators.Gi(i)));
            terms.Add((hi, Generators.Hi(i)));

            er1y *= yInverse;
            minusESquaredY *= yInverse;
        }

        for (int j = 0; j < rounds; j++)
        {
            terms.Add((challenges[j] * challenges[j] * minusESquared, l8[j]));
            terms.Add((challengesInverse[j] * challengesInverse[j] * minusESquared, r8[j]));
        }

        terms.Add((gScalar, Point.FromBytes(BasePoint)));
        terms.Add((hScalar, Point.FromBytes(Pedersen.H)));

        return MultiExp(terms).IsIdentity;
    }

    /// <summary>The compressed ed25519 basepoint.</summary>
    private static ReadOnlySpan<byte> BasePoint =>
        [0x58, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66,
         0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66];

    /// <summary>
    /// Naive: one scalar multiplication per term. Pippenger belongs here later —
    /// this is the honest version that can be checked against the reference first.
    /// </summary>
    private static Point MultiExp(List<(Scalar Scalar, Point Point)> terms)
    {
        Point sum = Point.Identity;

        foreach ((Scalar scalar, Point point) in terms)
        {
            if (scalar.IsZero) continue;
            sum += scalar * point;
        }

        return sum;
    }

    private static Point MultiplyByEight(Point point) => Scalar.FromCanonical([8, .. new byte[31]]) * point;

    /// <summary>x^2 + x^4 + … + x^n, for n a power of two.</summary>
    private static Scalar SumOfEvenPowers(Scalar x, int n)
    {
        Scalar squared = x * x;
        Scalar result = squared;

        while (n > 2)
        {
            result += squared * result;
            squared *= squared;
            n /= 2;
        }

        return result;
    }

    /// <summary>x + x^2 + … + x^n.</summary>
    private static Scalar SumOfScalarPowers(Scalar x, int n)
    {
        if (n == 1) return x;

        Scalar result = Scalar.One;
        Scalar power = x;
        int count = n + 1;

        if ((count & (count - 1)) == 0)
        {
            result += power;
            while (count > 2)
            {
                power *= power;
                result += power * result;
                count /= 2;
            }
        }
        else
        {
            Scalar previous = power;
            for (int i = 1; i < count; i++)
            {
                if (i > 1) previous *= power;
                result += previous;
            }
        }

        return result - Scalar.One;
    }

    private static Scalar BuildTwoSixtyFourMinusOne()
    {
        Scalar value = Two;
        for (int i = 0; i < 6; i++) value *= value;   // 2^64

        return value - Scalar.One;
    }

    private static byte[] Concat(Point[] points)
    {
        byte[] buffer = new byte[points.Length * 32];
        for (int i = 0; i < points.Length; i++) points[i].ToBytes().CopyTo(buffer, i * 32);
        return buffer;
    }
}
