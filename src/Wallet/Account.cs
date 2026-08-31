using Moonlight.Crypto;

namespace Moonlight.Wallet;

/// <summary>
/// A wallet's root keys. Two pairs: spend proves ownership, view finds outputs.
/// Both come from one seed, which is why a single phrase restores everything.
/// </summary>
/// <remarks>
/// Ported from <c>account_base::generate</c>. The view key is not independent —
/// it is derived from the spend key, so one seed suffices.
/// </remarks>
public sealed class Account
{
    private Account(Scalar spendSecret, Scalar viewSecret, Network network)
    {
        SpendSecret = spendSecret;
        ViewSecret = viewSecret;
        SpendPublic = Point.FromSecret(spendSecret);
        ViewPublic = Point.FromSecret(viewSecret);
        Network = network;
    }

    public Scalar SpendSecret { get; }

    public Scalar ViewSecret { get; }

    public Point SpendPublic { get; }

    public Point ViewPublic { get; }

    public Network Network { get; }

    public Address Address => new(Network, AddressKind.Standard, SpendPublic, ViewPublic);

    /// <summary>The 25-word phrase that restores this account.</summary>
    public string Seed => Mnemonic.Encode(SpendSecret.ToBytes());

    public static Account FromSeed(ReadOnlySpan<byte> seed, Network network = Network.Mainnet)
    {
        // The seed is reduced, not required to be canonical: any 32 bytes are a
        // valid seed, and reduction is what makes them a key.
        Scalar spend = Scalar.Reduce(seed);

        return new Account(spend, Scalar.Reduce(Keccak.Hash(spend.ToBytes())), network);
    }

    /// <summary>
    /// From the two secret keys directly. A wallet restored this way has no seed
    /// phrase, because the view key is not derivable from a spend key that was
    /// never generated from one.
    /// </summary>
    public static Account FromKeys(Scalar spendSecret, Scalar viewSecret, Network network = Network.Mainnet)
        => new(spendSecret, viewSecret, network);

    public static Account FromMnemonic(string phrase, Network network = Network.Mainnet)
        => FromSeed(Mnemonic.Decode(phrase), network);

    public static Account Create(Network network = Network.Mainnet)
    {
        byte[] seed = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(seed);

        return FromSeed(seed, network);
    }

    /// <summary>
    /// The view-only half. Enough to find incoming outputs and read amounts, and
    /// deliberately not enough to spend them.
    /// </summary>
    public ViewOnlyAccount AsViewOnly() => new(ViewSecret, SpendPublic, Network);
}

/// <summary>A wallet that can see but not spend — a separate type, so the distinction cannot be lost by accident.</summary>
public sealed record ViewOnlyAccount(Scalar ViewSecret, Point SpendPublic, Network Network)
{
    public Point ViewPublic => Point.FromSecret(ViewSecret);

    public Address Address => new(Network, AddressKind.Standard, SpendPublic, ViewPublic);
}
