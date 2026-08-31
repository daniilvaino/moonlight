using Moonlight.Crypto;

namespace Moonlight.Wallet;

public enum Network
{
    Mainnet,
    Testnet,
    Stagenet,
}

public enum AddressKind
{
    Standard,
    Integrated,
    Subaddress,
}

/// <summary>
/// A Monero address: two public keys, what network they belong to, and — for an
/// integrated address — the payment id folded in.
/// </summary>
public sealed record Address(
    Network Network,
    AddressKind Kind,
    Point SpendKey,
    Point ViewKey,
    byte[]? PaymentId = null)
{
    private const int ChecksumLength = 4;

    public override string ToString() => Encode();

    public string Encode()
    {
        byte[] paymentId = PaymentId ?? [];

        if (Kind == AddressKind.Integrated && paymentId.Length != 8)
        {
            throw new InvalidOperationException("an integrated address carries an eight-byte payment id");
        }

        if (Kind != AddressKind.Integrated && paymentId.Length != 0)
        {
            throw new InvalidOperationException("only an integrated address carries a payment id");
        }

        Span<byte> prefix = stackalloc byte[VarInt.MaxLength];
        int prefixLength = VarInt.Write(prefix, Prefix(Network, Kind));

        byte[] body = new byte[prefixLength + 64 + paymentId.Length];
        prefix[..prefixLength].CopyTo(body);
        SpendKey.ToBytes().CopyTo(body, prefixLength);
        ViewKey.ToBytes().CopyTo(body, prefixLength + 32);
        paymentId.CopyTo(body, prefixLength + 64);

        byte[] full = new byte[body.Length + ChecksumLength];
        body.CopyTo(full, 0);
        Keccak.Hash(body).AsSpan(0, ChecksumLength).CopyTo(full.AsSpan(body.Length));

        return Base58.Encode(full);
    }

    public static Address Parse(string text)
        => TryParse(text, out Address? address) ? address : throw new FormatException("not a Monero address");

    public static bool TryParse(string text, out Address address)
    {
        address = null!;

        if (!Base58.TryDecode(text, out byte[] full) || full.Length <= ChecksumLength) return false;

        // Checksum first: everything after this is only worth reading if the bytes
        // survived being typed or copied.
        ReadOnlySpan<byte> body = full.AsSpan(0, full.Length - ChecksumLength);
        if (!Keccak.Hash(body).AsSpan(0, ChecksumLength).SequenceEqual(full.AsSpan(full.Length - ChecksumLength)))
        {
            return false;
        }

        if (!VarInt.TryRead(body, out ulong prefix, out int prefixLength)) return false;
        if (!TryNetworkAndKind(prefix, out Network network, out AddressKind kind)) return false;

        int expected = prefixLength + 64 + (kind == AddressKind.Integrated ? 8 : 0);
        if (body.Length != expected) return false;

        if (!Point.TryFromBytes(body.Slice(prefixLength, 32), out Point spend)) return false;
        if (!Point.TryFromBytes(body.Slice(prefixLength + 32, 32), out Point view)) return false;

        byte[]? paymentId = kind == AddressKind.Integrated ? body[(prefixLength + 64)..].ToArray() : null;

        address = new Address(network, kind, spend, view, paymentId);
        return true;
    }

    private static ulong Prefix(Network network, AddressKind kind) => (network, kind) switch
    {
        (Network.Mainnet, AddressKind.Standard) => 18,
        (Network.Mainnet, AddressKind.Integrated) => 19,
        (Network.Mainnet, AddressKind.Subaddress) => 42,
        (Network.Testnet, AddressKind.Standard) => 53,
        (Network.Testnet, AddressKind.Integrated) => 54,
        (Network.Testnet, AddressKind.Subaddress) => 63,
        (Network.Stagenet, AddressKind.Standard) => 24,
        (Network.Stagenet, AddressKind.Integrated) => 25,
        (Network.Stagenet, AddressKind.Subaddress) => 36,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static bool TryNetworkAndKind(ulong prefix, out Network network, out AddressKind kind)
    {
        (network, kind) = prefix switch
        {
            18 => (Network.Mainnet, AddressKind.Standard),
            19 => (Network.Mainnet, AddressKind.Integrated),
            42 => (Network.Mainnet, AddressKind.Subaddress),
            53 => (Network.Testnet, AddressKind.Standard),
            54 => (Network.Testnet, AddressKind.Integrated),
            63 => (Network.Testnet, AddressKind.Subaddress),
            24 => (Network.Stagenet, AddressKind.Standard),
            25 => (Network.Stagenet, AddressKind.Integrated),
            36 => (Network.Stagenet, AddressKind.Subaddress),
            _ => ((Network)(-1), (AddressKind)(-1)),
        };

        return (int)network >= 0;
    }

    public bool Equals(Address? other)
        => other is not null
        && Network == other.Network
        && Kind == other.Kind
        && SpendKey == other.SpendKey
        && ViewKey == other.ViewKey
        && (PaymentId ?? []).AsSpan().SequenceEqual(other.PaymentId ?? []);

    public override int GetHashCode() => HashCode.Combine(Network, Kind, SpendKey, ViewKey);
}
