using System.Buffers.Binary;
using Moonlight.Crypto;

namespace Moonlight.Wallet;

/// <summary>
/// Where a subaddress sits: an account, and a position within it. (0, 0) is the
/// wallet's own address, not a subaddress.
/// </summary>
public readonly record struct SubaddressIndex(uint Major, uint Minor)
{
    public bool IsZero => Major == 0 && Minor == 0;
}

/// <summary>
/// Subaddresses: unlimited receiving addresses from one key pair, unlinkable to
/// each other and to the wallet's main address without the view key.
/// </summary>
/// <remarks>
/// Ported from <c>device_default.cpp</c>. The domain separator is "SubAddr" plus
/// its terminating zero — the reference takes <c>sizeof</c> of the literal, so the
/// NUL is part of the hashed data.
/// </remarks>
public static class Subaddress
{
    private static ReadOnlySpan<byte> Salt => "SubAddr\0"u8;

    /// <summary>m = Hs("SubAddr\0" || viewSecret || major || minor), the offset that moves the spend key.</summary>
    public static Scalar SecretKey(Scalar viewSecret, SubaddressIndex index)
    {
        byte[] buffer = new byte[Salt.Length + 32 + 8];
        Salt.CopyTo(buffer);
        viewSecret.ToBytes().CopyTo(buffer, Salt.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(Salt.Length + 32), index.Major);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(Salt.Length + 36), index.Minor);

        return Scalar.Hash(buffer);
    }

    /// <summary>
    /// The pair a subaddress publishes: D = B + m·G, and C = a·D. The view key is
    /// no longer a·G, which is why subaddresses cannot be linked without it.
    /// </summary>
    public static (Point Spend, Point View) Keys(Account account, SubaddressIndex index)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (index.IsZero)
        {
            return (account.SpendPublic, account.ViewPublic);
        }

        Point spend = account.SpendPublic + Point.BaseMultiply(SecretKey(account.ViewSecret, index));
        return (spend, account.ViewSecret * spend);
    }

    public static Address Address(Account account, SubaddressIndex index)
    {
        ArgumentNullException.ThrowIfNull(account);

        (Point spend, Point view) = Keys(account, index);

        return new Address(
            account.Network,
            index.IsZero ? AddressKind.Standard : AddressKind.Subaddress,
            spend,
            view);
    }

    /// <summary>The private key for a subaddress: the account's spend secret plus the offset.</summary>
    public static Scalar SpendSecret(Account account, SubaddressIndex index)
    {
        ArgumentNullException.ThrowIfNull(account);

        return index.IsZero
            ? account.SpendSecret
            : account.SpendSecret + SecretKey(account.ViewSecret, index);
    }
}
