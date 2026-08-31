using Moonlight.Crypto;

namespace Moonlight.RingCT.BulletproofPlus;

/// <summary>
/// The Fiat–Shamir transcript. Every challenge is the hash of the previous state
/// and whatever the prover has just committed to, so a prover cannot pick a
/// challenge and work backwards.
/// </summary>
/// <remarks>
/// Ported from <c>transcript_update</c> in <c>bulletproofs_plus.cc</c>. The state
/// starts as a point and becomes a scalar after the first update; both are 32
/// bytes and the reference hashes them the same way.
/// </remarks>
public struct Transcript(ReadOnlySpan<byte> initial)
{
    private byte[] state = initial.ToArray();

    public readonly Scalar Current => Scalar.FromCanonical(state);

    public Scalar Update(ReadOnlySpan<byte> first)
    {
        byte[] buffer = new byte[64];
        state.CopyTo(buffer, 0);
        first.CopyTo(buffer.AsSpan(32));

        state = Scalar.Hash(buffer).ToBytes();
        return Scalar.FromCanonical(state);
    }

    public Scalar Update(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        byte[] buffer = new byte[96];
        state.CopyTo(buffer, 0);
        first.CopyTo(buffer.AsSpan(32));
        second.CopyTo(buffer.AsSpan(64));

        state = Scalar.Hash(buffer).ToBytes();
        return Scalar.FromCanonical(state);
    }

    /// <summary>Replaces the state outright, as the reference does when it sets z.</summary>
    public void Set(Scalar value) => state = value.ToBytes();
}
