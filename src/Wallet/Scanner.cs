using Moonlight.Crypto;
using Moonlight.RingCT;
using Moonlight.Serialization;
using MoneroRing.Crypto;

namespace Moonlight.Wallet;

/// <summary>An output this wallet can spend, and everything needed to spend it.</summary>
public sealed record OwnedOutput(
    ulong Height,
    byte[] TransactionId,
    int OutputIndex,
    Point Key,
    ulong Amount,
    Scalar Mask,
    SubaddressIndex Subaddress,
    Point? KeyImage);

/// <summary>
/// Finds a wallet's outputs in transactions it is shown. The whole wallet rests
/// on this: an output missed here is money the owner never learns about, and
/// nothing anywhere reports an error.
/// </summary>
/// <remarks>
/// Ported from <c>is_out_to_acc</c> and the amount handling around it. Two things
/// make it fast enough to be usable: view tags reject fifteen outputs in sixteen
/// before any derivation, and the subaddress table turns a search into a lookup.
/// </remarks>
public sealed class Scanner
{
    private readonly Scalar viewSecret;
    private readonly Point spendPublic;
    private readonly Scalar? spendSecret;
    private readonly Dictionary<string, SubaddressIndex> subaddresses;

    /// <summary>A view-only scanner: it finds outputs and reads amounts, and cannot produce key images.</summary>
    public Scanner(ViewOnlyAccount account, int accounts = 1, int addressesPerAccount = 200)
    {
        ArgumentNullException.ThrowIfNull(account);

        viewSecret = account.ViewSecret;
        spendPublic = account.SpendPublic;
        spendSecret = null;
        subaddresses = BuildTable(viewSecret, spendPublic, accounts, addressesPerAccount);
    }

    public Scanner(Account account, int accounts = 1, int addressesPerAccount = 200)
    {
        ArgumentNullException.ThrowIfNull(account);

        viewSecret = account.ViewSecret;
        spendPublic = account.SpendPublic;
        spendSecret = account.SpendSecret;
        subaddresses = BuildTable(viewSecret, spendPublic, accounts, addressesPerAccount);
    }

    /// <summary>How many outputs were examined and how many the view tag let through.</summary>
    public long Examined { get; private set; }

    public long PassedViewTag { get; private set; }

    public IReadOnlyList<OwnedOutput> Scan(Transaction transaction, ulong height, byte[]? transactionId = null)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        TxExtra extra = TxExtra.Parse(transaction.Extra);
        if (extra.PublicKey is null) return [];

        byte[] id = transactionId ?? TxHash.Compute(transaction);
        List<OwnedOutput> found = [];

        for (int i = 0; i < transaction.Outputs.Length; i++)
        {
            Examined++;

            (byte[] key, byte? viewTag) = Target(transaction.Outputs[i]);
            if (key is null) continue;

            // The transaction key first, then the per-output key if the sender used
            // one — which they must when paying a subaddress.
            byte[]?[] candidates = extra.AdditionalPublicKeys.Length > i
                ? [extra.PublicKey, extra.AdditionalPublicKeys[i]]
                : [extra.PublicKey];

            foreach (byte[]? candidate in candidates)
            {
                if (candidate is null) continue;
                if (!TryOwn(transaction, candidate, i, key, viewTag, height, id, out OwnedOutput? output)) continue;

                found.Add(output!);
                break;
            }
        }

        return found;
    }

    private bool TryOwn(
        Transaction transaction,
        byte[] transactionKey,
        int index,
        byte[] outputKey,
        byte? viewTag,
        ulong height,
        byte[] id,
        out OwnedOutput? output)
    {
        output = null;

        byte[] derivation = new byte[32];
        if (!RingSig.generate_key_derivation(transactionKey, viewSecret.ToBytes(), derivation)) return false;

        // The cheap rejection, before any curve operation.
        if (viewTag is not null && ViewTag.Derive(derivation, (ulong)index) != viewTag) return false;
        PassedViewTag++;

        Scalar shared = Scalar.FromCanonical(RingSig.derivation_to_scalar(derivation, (uint)index));
        Point offset = Point.BaseMultiply(shared);

        // P - Hs(D||i)·G is the spend key this output was sent to. For the main
        // address that is our own; for a subaddress it is one from the table.
        Point spend = Point.FromBytes(outputKey) - offset;
        if (!subaddresses.TryGetValue(spend.ToString(), out SubaddressIndex subaddress)) return false;

        (ulong amount, Scalar mask) = Amount(transaction, index, derivation, shared);

        Point? keyImage = spendSecret is null ? null : KeyImage(outputKey, shared, subaddress);

        output = new OwnedOutput(height, id, index, Point.FromBytes(outputKey), amount, mask, subaddress, keyImage);
        return true;
    }

    /// <summary>
    /// The amount, and the mask that commits to it. A coinbase says its amount in
    /// the clear; a RingCT output hides it behind the shared secret.
    /// </summary>
    private static (ulong Amount, Scalar Mask) Amount(Transaction transaction, int index, byte[] derivation, Scalar shared)
    {
        if (transaction.Rct is null || transaction.Rct.Type == RctBase.Null || transaction.Rct.EcdhInfo.Length <= index)
        {
            return (transaction.Outputs[index].Amount, Scalar.Zero);
        }

        EcdhInfo ecdh = transaction.Rct.EcdhInfo[index];
        byte[] secret = shared.ToBytes();

        if (ecdh.Mask is null)
        {
            return (Ecdh.DecodeAmount(ecdh.Amount, secret), Ecdh.CommitmentMask(secret));
        }

        (Scalar mask, Scalar amount) = Ecdh.DecodeLegacy(
            Scalar.FromCanonical(ecdh.Mask), Scalar.FromCanonical(ecdh.Amount), secret);

        return (System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(amount.ToBytes()), mask);
    }

    /// <summary>
    /// The image that marks this output spent: x·H(P), where x is the output's
    /// private key. Only a wallet holding the spend secret can compute it, which is
    /// why a view-only wallet cannot tell a spent output from an unspent one.
    /// </summary>
    private Point KeyImage(byte[] outputKey, Scalar shared, SubaddressIndex subaddress)
    {
        Scalar secret = shared + spendSecret!.Value;

        if (!subaddress.IsZero)
        {
            secret += Subaddress.SecretKey(viewSecret, subaddress);
        }

        return Point.KeyImage(Point.FromBytes(outputKey), secret);
    }

    private static (byte[] Key, byte? ViewTag) Target(TxOut output) => output.Target switch
    {
        TxOutTarget.ToKey k => (k.Key, (byte?)null),
        TxOutTarget.ToTaggedKey t => (t.Key, t.ViewTag),
        _ => (null!, null),
    };

    /// <summary>
    /// Every subaddress spend key the wallet expects to be paid at, by its
    /// compressed form. Scanning is then a dictionary lookup per output rather than
    /// a derivation per address.
    /// </summary>
    private static Dictionary<string, SubaddressIndex> BuildTable(
        Scalar viewSecret,
        Point spendPublic,
        int accounts,
        int addressesPerAccount)
    {
        Dictionary<string, SubaddressIndex> table = new(StringComparer.Ordinal)
        {
            [spendPublic.ToString()] = new SubaddressIndex(0, 0),
        };

        for (uint major = 0; major < accounts; major++)
        {
            for (uint minor = 0; minor < addressesPerAccount; minor++)
            {
                if (major == 0 && minor == 0) continue;

                SubaddressIndex index = new(major, minor);
                Point spend = spendPublic + Point.BaseMultiply(Subaddress.SecretKey(viewSecret, index));

                table.TryAdd(spend.ToString(), index);
            }
        }

        return table;
    }
}
