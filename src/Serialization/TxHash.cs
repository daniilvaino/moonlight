using Moonlight.Crypto;

namespace Moonlight.Serialization;

/// <summary>
/// Transaction ids, per monero's <c>calculate_transaction_hash</c>.
/// </summary>
public static class TxHash
{
    /// <summary>
    /// v1 hashes the whole blob. v2 hashes three parts separately and then hashes
    /// those three hashes — which is what lets a pruned node drop the proofs and
    /// still know the id.
    /// </summary>
    public static byte[] Compute(Transaction tx)
    {
        if (tx.Version == 1)
        {
            return Keccak.Hash(tx.Blob);
        }

        Span<byte> parts = stackalloc byte[3 * Keccak.HashSize];

        Keccak.Hash(tx.Prefix, parts[..32]);
        Keccak.Hash(tx.RctBaseBlob, parts.Slice(32, 32));

        // A null RingCT signature has no proofs, and the third hash is zero rather
        // than the hash of nothing.
        if (tx.Rct is null || tx.Rct.Type == RctBase.Null)
        {
            parts.Slice(64, 32).Clear();
        }
        else
        {
            Keccak.Hash(tx.Prunable, parts.Slice(64, 32));
        }

        return Keccak.Hash(parts);
    }
}
