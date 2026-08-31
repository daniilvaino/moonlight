using Moonlight.Crypto;
using Moonlight.Serialization;

namespace Moonlight.RingCT;

/// <summary>
/// What a RingCT signature actually signs. Not the transaction hash: three
/// hashes, of the prefix, of the unprunable RingCT base, and of the range proof
/// fields, hashed together.
/// </summary>
/// <remarks>
/// Ported from <c>get_pre_mlsag_hash</c> in <c>rctSigs.cpp</c>. The range proof's
/// V values are deliberately left out — they are recomputed from
/// <c>outPk</c>, which the base blob already covers.
/// </remarks>
public static class RingCtMessage
{
    /// <param name="proofElements">
    /// The range proof fields in order. For Bulletproofs+ that is A, A1, B, r1,
    /// s1, d1, then every L and then every R.
    /// </param>
    public static byte[] Compute(Transaction transaction, IReadOnlyList<byte[]> proofElements)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(proofElements);

        Span<byte> parts = stackalloc byte[3 * Keccak.HashSize];

        Keccak.Hash(transaction.Prefix, parts[..32]);
        Keccak.Hash(transaction.RctBaseBlob, parts.Slice(32, 32));

        byte[] proofs = new byte[proofElements.Count * 32];
        for (int i = 0; i < proofElements.Count; i++)
        {
            if (proofElements[i].Length != 32)
            {
                throw new ArgumentException("every proof element is 32 bytes", nameof(proofElements));
            }

            proofElements[i].CopyTo(proofs, i * 32);
        }

        Keccak.Hash(proofs, parts.Slice(64, 32));

        return Keccak.Hash(parts);
    }
}
