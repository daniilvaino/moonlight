namespace Moonlight.Serialization;

public abstract record TxIn
{
    /// <summary>The miner's input: no key image, just the height it was minted at.</summary>
    public sealed record Gen(ulong Height) : TxIn;

    public sealed record ToKey(ulong Amount, ulong[] KeyOffsets, byte[] KeyImage) : TxIn;

    /// <summary>Consensus-dead variants (tags 0 and 1). Kept as bytes so a blob carrying one still parses.</summary>
    public sealed record Unsupported(byte Tag) : TxIn;
}

public abstract record TxOutTarget
{
    public sealed record ToKey(byte[] Key) : TxOutTarget;

    public sealed record ToTaggedKey(byte[] Key, byte ViewTag) : TxOutTarget;

    public sealed record Unsupported(byte Tag) : TxOutTarget;
}

public sealed record TxOut(ulong Amount, TxOutTarget Target);

public sealed record EcdhInfo(byte[]? Mask, byte[] Amount);

/// <summary>
/// The unprunable half of a RingCT signature: everything needed to state what the
/// transaction claims, without the proofs that back the claim.
/// </summary>
public sealed record RctBase(
    byte Type,
    ulong Fee,
    byte[][] PseudoOuts,
    EcdhInfo[] EcdhInfo,
    byte[][] OutPk)
{
    public const byte Null = 0;
    public const byte Full = 1;
    public const byte Simple = 2;
    public const byte Bulletproof = 3;
    public const byte Bulletproof2 = 4;
    public const byte Clsag = 5;
    public const byte BulletproofPlus = 6;
}

public sealed record Transaction(
    ulong Version,
    ulong UnlockTime,
    TxIn[] Inputs,
    TxOut[] Outputs,
    byte[] Extra,
    RctBase? Rct,
    byte[][] Signatures,
    byte[] Blob,
    int PrefixLength,
    int UnprunableLength)
{
    public bool IsCoinbase => Inputs.Length == 1 && Inputs[0] is TxIn.Gen;

    public ReadOnlySpan<byte> Prefix => Blob.AsSpan(0, PrefixLength);

    /// <summary>The rctSigBase bytes: everything between the prefix and the proofs.</summary>
    public ReadOnlySpan<byte> RctBaseBlob => Blob.AsSpan(PrefixLength, UnprunableLength - PrefixLength);

    public ReadOnlySpan<byte> Prunable => Blob.AsSpan(UnprunableLength);
}
