using Moonlight.Crypto;

namespace Moonlight.RingCT;

/// <summary>A CLSAG signature: one scalar per ring member, the closing challenge, and the auxiliary key image.</summary>
/// <remarks><paramref name="D"/> is stored multiplied by 1/8, as it travels on the wire.</remarks>
public sealed record ClsagSignature(Scalar[] S, Scalar C1, Point D, Point I);

/// <summary>
/// Concise Linkable Spontaneous Anonymous Group signatures — what Monero has
/// signed with since v13. One ring proves two things at once: that the signer
/// owns one of the output keys, and that the same signer knows the blinding of
/// the matching commitment.
/// </summary>
/// <remarks>
/// Ported line by line from <c>src/ringct/rctSigs.cpp</c> (<c>CLSAG_Gen</c> and
/// <c>verRctCLSAGSimple</c>) with <c>device_default.cpp</c> for the three steps
/// the reference delegates to a hardware device.
/// </remarks>
public static class Clsag
{
    private static ReadOnlySpan<byte> RoundSalt => "CLSAG_round"u8;

    private static ReadOnlySpan<byte> Aggregate0Salt => "CLSAG_agg_0"u8;

    private static ReadOnlySpan<byte> Aggregate1Salt => "CLSAG_agg_1"u8;

    /// <summary>1/8 mod l. Auxiliary key images travel divided by the cofactor.</summary>
    private static ReadOnlySpan<byte> InverseEight =>
        [0x79, 0x2f, 0xdc, 0xe2, 0x29, 0xe5, 0x06, 0x61, 0xd0, 0xda, 0x1c, 0x7d, 0xb3, 0x9d, 0xd3, 0x07,
         0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x06];

    /// <param name="message">What is being signed — for a transaction, its message hash.</param>
    /// <param name="ring">The output keys, decoys and the real one alike.</param>
    /// <param name="commitments">The ring's commitments, as they appear on chain.</param>
    /// <param name="secret">The private key for <c>ring[index]</c>.</param>
    /// <param name="maskDifference">The blinding of <c>commitments[index]</c> minus that of the pseudo-output.</param>
    /// <param name="commitmentOffset">The pseudo-output commitment this input is balanced against.</param>
    /// <param name="index">Where the real output sits in the ring.</param>
    public static ClsagSignature Sign(
        ReadOnlySpan<byte> message,
        IReadOnlyList<Point> ring,
        IReadOnlyList<Point> commitments,
        Scalar secret,
        Scalar maskDifference,
        Point commitmentOffset,
        int index)
    {
        ArgumentNullException.ThrowIfNull(ring);
        ArgumentNullException.ThrowIfNull(commitments);

        int n = ring.Count;
        if (n != commitments.Count) throw new ArgumentException("ring and commitments must be the same length", nameof(commitments));
        if ((uint)index >= (uint)n) throw new ArgumentOutOfRangeException(nameof(index));

        // The offset commitments the ring is actually signed over.
        Point[] offset = new Point[n];
        for (int i = 0; i < n; i++) offset[i] = commitments[i] - commitmentOffset;

        Point h = Point.HashToEc(ring[index].ToBytes());
        Point image = secret * h;
        Point auxiliary = maskDifference * h;
        Point auxiliaryOnWire = Scalar.FromCanonical(InverseEight) * auxiliary;

        (Scalar muP, Scalar muC) = Aggregates(ring, commitments, image, auxiliaryOnWire, commitmentOffset);

        Scalar alpha = Scalar.Random();

        byte[] roundPrefix = RoundPrefix(ring, commitments, commitmentOffset, message);
        Scalar c = RoundHash(roundPrefix, Point.BaseMultiply(alpha), alpha * h);

        Scalar[] s = new Scalar[n];
        Scalar c1 = Scalar.Zero;

        int j = (index + 1) % n;
        if (j == 0) c1 = c;

        while (j != index)
        {
            s[j] = Scalar.Random();

            Scalar cp = muP * c;
            Scalar cc = muC * c;

            Point l = Point.BaseMultiply(s[j]) + (cp * ring[j]) + (cc * offset[j]);
            Point r = (s[j] * Point.HashToEc(ring[j].ToBytes())) + (cp * image) + (cc * auxiliary);

            c = RoundHash(roundPrefix, l, r);

            j = (j + 1) % n;
            if (j == 0) c1 = c;
        }

        // s[index] = alpha - c·(mu_P·p + mu_C·z): the one scalar that closes the ring.
        s[index] = alpha - (c * ((muP * secret) + (muC * maskDifference)));

        return new ClsagSignature(s, c1, auxiliaryOnWire, image);
    }

    public static bool Verify(
        ReadOnlySpan<byte> message,
        IReadOnlyList<Point> ring,
        IReadOnlyList<Point> commitments,
        Point commitmentOffset,
        ClsagSignature signature)
    {
        ArgumentNullException.ThrowIfNull(ring);
        ArgumentNullException.ThrowIfNull(commitments);
        ArgumentNullException.ThrowIfNull(signature);

        int n = ring.Count;
        if (n == 0 || n != commitments.Count || n != signature.S.Length) return false;
        if (signature.I.IsIdentity) return false;

        // The auxiliary image is sent divided by eight; multiplying it back is also
        // what forces it into the prime-order subgroup.
        Point auxiliary = Scalar.FromCanonical([8, .. new byte[31]]) * signature.D;
        if (auxiliary.IsIdentity) return false;

        (Scalar muP, Scalar muC) = Aggregates(ring, commitments, signature.I, signature.D, commitmentOffset);

        byte[] roundPrefix = RoundPrefix(ring, commitments, commitmentOffset, message);
        Scalar c = signature.C1;

        for (int i = 0; i < n; i++)
        {
            Scalar cp = muP * c;
            Scalar cc = muC * c;

            Point l = Point.BaseMultiply(signature.S[i]) + (cp * ring[i]) + (cc * (commitments[i] - commitmentOffset));
            Point r = (signature.S[i] * Point.HashToEc(ring[i].ToBytes())) + (cp * signature.I) + (cc * auxiliary);

            c = RoundHash(roundPrefix, l, r);
            if (c.IsZero) return false;
        }

        // The ring closes only if the chain of challenges comes back to where it started.
        return c == signature.C1;
    }

    private static (Scalar P, Scalar C) Aggregates(
        IReadOnlyList<Point> ring,
        IReadOnlyList<Point> commitments,
        Point image,
        Point auxiliaryOnWire,
        Point commitmentOffset)
    {
        List<byte[]> tail = [];
        foreach (Point p in ring) tail.Add(p.ToBytes());
        foreach (Point c in commitments) tail.Add(c.ToBytes());
        tail.Add(image.ToBytes());
        tail.Add(auxiliaryOnWire.ToBytes());
        tail.Add(commitmentOffset.ToBytes());

        return (Scalar.Hash(Concat(Domain(Aggregate0Salt), tail)),
                Scalar.Hash(Concat(Domain(Aggregate1Salt), tail)));
    }

    /// <summary>Everything the round hash covers except L and R, which change each turn.</summary>
    private static byte[] RoundPrefix(
        IReadOnlyList<Point> ring,
        IReadOnlyList<Point> commitments,
        Point commitmentOffset,
        ReadOnlySpan<byte> message)
    {
        List<byte[]> parts = [];
        foreach (Point p in ring) parts.Add(p.ToBytes());
        foreach (Point c in commitments) parts.Add(c.ToBytes());
        parts.Add(commitmentOffset.ToBytes());
        parts.Add(message.ToArray());

        return Concat(Domain(RoundSalt), parts);
    }

    private static Scalar RoundHash(byte[] prefix, Point l, Point r)
    {
        byte[] buffer = new byte[prefix.Length + 64];
        prefix.CopyTo(buffer, 0);
        l.ToBytes().CopyTo(buffer, prefix.Length);
        r.ToBytes().CopyTo(buffer, prefix.Length + 32);

        return Scalar.Hash(buffer);
    }

    /// <summary>
    /// A domain separator occupies a whole 32-byte slot, zero-padded on the right —
    /// the reference writes the string over a zeroed key rather than hashing the
    /// bare string.
    /// </summary>
    private static byte[] Domain(ReadOnlySpan<byte> salt)
    {
        byte[] slot = new byte[32];
        salt.CopyTo(slot);
        return slot;
    }

    private static byte[] Concat(byte[] head, List<byte[]> tail)
    {
        byte[] buffer = new byte[head.Length + tail.Sum(t => t.Length)];
        head.CopyTo(buffer, 0);

        int offset = head.Length;
        foreach (byte[] part in tail)
        {
            part.CopyTo(buffer, offset);
            offset += part.Length;
        }

        return buffer;
    }
}
