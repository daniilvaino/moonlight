using Moonlight.Crypto;

namespace Moonlight.RingCT.BulletproofPlus;

/// <summary>
/// A Bulletproof+ range proof: every committed amount lies in [0, 2^64), proved
/// in log space rather than one proof per bit.
/// </summary>
/// <remarks>
/// <paramref name="V"/> is not serialized in a transaction — it is rebuilt from
/// <c>outPk</c>, each mask multiplied by 1/8. The points travel divided by the
/// cofactor and are multiplied back by eight on the way in.
/// </remarks>
public sealed record Proof(
    Point[] V,
    Point A,
    Point A1,
    Point B,
    Scalar R1,
    Scalar S1,
    Scalar D1,
    Point[] L,
    Point[] R);
