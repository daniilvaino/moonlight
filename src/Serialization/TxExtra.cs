namespace Moonlight.Serialization;

/// <summary>
/// What a transaction carries outside consensus: the public key a recipient needs
/// to find their outputs, and optionally a payment id.
/// </summary>
/// <remarks>
/// The field is a loose sequence of tagged records, and miners have historically
/// put arbitrary bytes there. Parsing stops at the first thing it cannot read
/// rather than failing: an unreadable tail must not cost a wallet the public key
/// that came before it.
/// </remarks>
public sealed record TxExtra(
    byte[]? PublicKey,
    byte[][] AdditionalPublicKeys,
    byte[]? PaymentId,
    byte[]? EncryptedPaymentId)
{
    private const byte TagPadding = 0x00;
    private const byte TagPublicKey = 0x01;
    private const byte TagNonce = 0x02;
    private const byte TagMergeMining = 0x03;
    private const byte TagAdditionalPublicKeys = 0x04;

    private const byte NoncePaymentId = 0x00;
    private const byte NonceEncryptedPaymentId = 0x01;

    public static TxExtra Parse(ReadOnlySpan<byte> extra)
    {
        byte[]? publicKey = null;
        byte[][] additional = [];
        byte[]? paymentId = null;
        byte[]? encryptedPaymentId = null;

        Reader reader = new(extra);

        try
        {
            while (!reader.AtEnd)
            {
                switch (reader.ReadByte())
                {
                    case TagPadding:
                        // Padding runs to the end; anything after it is unreachable.
                        reader.ReadBytes(reader.Remaining);
                        break;

                    case TagPublicKey:
                        // Only the first counts. A second is not a second key, it is
                        // a malformed field, and monero reads the first as well.
                        byte[] key = reader.ReadKey();
                        publicKey ??= key;
                        break;

                    case TagAdditionalPublicKeys:
                        int count = reader.ReadCount(32);
                        byte[][] keys = new byte[count][];
                        for (int i = 0; i < count; i++) keys[i] = reader.ReadKey();
                        if (additional.Length == 0) additional = keys;
                        break;

                    case TagNonce:
                        ReadNonce(reader.ReadBytes(reader.ReadByte()), ref paymentId, ref encryptedPaymentId);
                        break;

                    case TagMergeMining:
                        reader.ReadVarInt();
                        reader.ReadKey();
                        break;

                    default:
                        // An unknown tag has no length, so the rest cannot be walked.
                        reader.ReadBytes(reader.Remaining);
                        break;
                }
            }
        }
        catch (FormatException)
        {
            // Truncated or malformed: keep whatever was already read.
        }

        return new TxExtra(publicKey, additional, paymentId, encryptedPaymentId);
    }

    private static void ReadNonce(ReadOnlySpan<byte> nonce, ref byte[]? paymentId, ref byte[]? encrypted)
    {
        if (nonce.Length == 33 && nonce[0] == NoncePaymentId)
        {
            paymentId ??= nonce[1..].ToArray();
        }
        else if (nonce.Length == 9 && nonce[0] == NonceEncryptedPaymentId)
        {
            encrypted ??= nonce[1..].ToArray();
        }
    }
}
