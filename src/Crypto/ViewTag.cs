namespace Moonlight.Crypto;

/// <summary>
/// The one-byte hint that lets a wallet skip most outputs without a full
/// derivation. Written to the spec in monero's crypto.cpp: the first byte of
/// H("view_tag" || derivation || varint(output_index)).
/// </summary>
public static class ViewTag
{
    private static ReadOnlySpan<byte> Salt => "view_tag"u8;   // no null terminator

    public static byte Derive(ReadOnlySpan<byte> derivation, ulong outputIndex)
    {
        if (derivation.Length != 32)
        {
            throw new ArgumentException("derivation must be 32 bytes", nameof(derivation));
        }

        Span<byte> buffer = stackalloc byte[Salt.Length + 32 + VarInt.MaxLength];
        Salt.CopyTo(buffer);
        derivation.CopyTo(buffer[Salt.Length..]);

        int written = VarInt.Write(buffer[(Salt.Length + 32)..], outputIndex);

        Span<byte> hash = stackalloc byte[Keccak.HashSize];
        Keccak.Hash(buffer[..(Salt.Length + 32 + written)], hash);

        return hash[0];
    }
}
