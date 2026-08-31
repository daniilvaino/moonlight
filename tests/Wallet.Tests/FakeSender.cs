using Moonlight.Crypto;
using Moonlight.RingCT;
using Moonlight.Serialization;
using Moonlight.Wallet;
using MoneroRing.Crypto;

namespace Moonlight.Wallet.Tests;

/// <summary>
/// Builds transactions the way a sender does, so the scanner can be tested against
/// something other than the one real transaction in the corpus.
/// </summary>
/// <remarks>
/// This is not a transaction builder: there are no ring signatures, no range
/// proofs and no balance. What it gets right is the part the scanner reads — the
/// transaction key, the one-time output keys, the view tags and the encrypted
/// amounts — which is exactly what these tests are about.
/// </remarks>
internal static class FakeSender
{
    /// <summary>A transaction paying the given amounts to an address, plus any key images it spends.</summary>
    public static Transaction Pay(
        Address destination,
        ulong[] amounts,
        ulong unlockTime = 0,
        bool coinbase = false,
        ulong height = 0,
        IReadOnlyList<Point>? spending = null)
    {
        ArgumentNullException.ThrowIfNull(destination);

        Scalar txSecret = Scalar.Random();

        // Paying a subaddress publishes r·D rather than r·G: the recipient's own
        // spend key stands in for the basepoint, which is what makes a·R come out
        // to the same derivation the sender computed as r·C.
        byte[] txPublic = destination.Kind == AddressKind.Subaddress
            ? (txSecret * destination.SpendKey).ToBytes()
            : Point.FromSecret(txSecret).ToBytes();

        List<byte> blob = [];
        Write(blob, 2);                 // version
        Write(blob, unlockTime);

        // Inputs: either the miner's, or one per key image being spent.
        if (coinbase)
        {
            Write(blob, 1);
            blob.Add(0xFF);
            Write(blob, height);
        }
        else
        {
            IReadOnlyList<Point> images = spending ?? [];
            Write(blob, (ulong)Math.Max(images.Count, 1));

            foreach (Point image in images.Count > 0 ? images : [Point.FromSecret(Scalar.Random())])
            {
                blob.Add(0x02);
                Write(blob, 0);         // amount, zero in RingCT
                Write(blob, 1);         // one ring member is enough to parse
                Write(blob, 0);
                blob.AddRange(image.ToBytes());
            }
        }

        // Outputs, each with the one-time key and view tag the recipient will look for.
        byte[] derivation = new byte[32];
        RingSig.generate_key_derivation(destination.ViewKey.ToBytes(), txSecret.ToBytes(), derivation);

        Write(blob, (ulong)amounts.Length);
        List<Scalar> shared = [];

        for (int i = 0; i < amounts.Length; i++)
        {
            byte[] oneTime = new byte[32];
            RingSig.derive_public_key(derivation, (uint)i, destination.SpendKey.ToBytes(), oneTime);

            Write(blob, coinbase ? amounts[i] : 0);
            blob.Add(0x03);                                   // tagged key
            blob.AddRange(oneTime);
            blob.Add(ViewTag.Derive(derivation, (ulong)i));

            shared.Add(Scalar.FromCanonical(RingSig.derivation_to_scalar(derivation, (uint)i)));
        }

        // extra: just the transaction key.
        Write(blob, 33);
        blob.Add(0x01);
        blob.AddRange(txPublic);

        if (coinbase)
        {
            blob.Add(0x00);                                   // RingCT type: null
        }
        else
        {
            blob.Add(RctBase.BulletproofPlus);
            Write(blob, 0);                                   // fee

            foreach ((ulong amount, Scalar secret) in amounts.Zip(shared))
            {
                blob.AddRange(Ecdh.EncodeAmount(amount, secret.ToBytes()));
            }

            foreach ((ulong amount, Scalar secret) in amounts.Zip(shared))
            {
                blob.AddRange(Pedersen.Commit(amount, Ecdh.CommitmentMask(secret.ToBytes())).ToBytes());
            }
        }

        return TxParser.Parse([.. blob]);
    }

    private static void Write(List<byte> blob, ulong value)
    {
        byte[] buffer = new byte[VarInt.MaxLength];
        blob.AddRange(buffer.AsSpan(0, VarInt.Write(buffer, value)).ToArray());
    }
}
