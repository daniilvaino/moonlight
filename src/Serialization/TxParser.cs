namespace Moonlight.Serialization;

/// <summary>
/// Consensus transaction blobs. Written against monero's
/// <c>cryptonote_basic/cryptonote_basic.h</c> and <c>ringct/rctTypes.h</c>.
/// </summary>
public static class TxParser
{
    private const byte TxInGen = 0xFF;
    private const byte TxInToScript = 0x00;
    private const byte TxInToScriptHash = 0x01;
    private const byte TxInToKey = 0x02;

    private const byte TxOutToKey = 0x02;
    private const byte TxOutToTaggedKey = 0x03;

    public static Transaction Parse(ReadOnlySpan<byte> blob)
    {
        Reader reader = new(blob);
        Transaction tx = Parse(ref reader, blob, swallowProofs: true);

        if (!reader.AtEnd)
        {
            throw new FormatException($"{reader.Remaining} trailing bytes after the transaction");
        }

        return tx;
    }

    /// <summary>
    /// Parses a transaction that is embedded in something larger, reporting where
    /// it ends. Only sound where the proofs are absent — a miner transaction — since
    /// their length is not otherwise recoverable without parsing them.
    /// </summary>
    public static Transaction ParseEmbedded(ReadOnlySpan<byte> blob, out int consumed)
    {
        Reader reader = new(blob);
        Transaction tx = Parse(ref reader, blob, swallowProofs: false);

        consumed = reader.Position;
        return tx;
    }

    private static Transaction Parse(ref Reader reader, ReadOnlySpan<byte> blob, bool swallowProofs)
    {
        ulong version = reader.ReadVarInt();
        if (version is 0 or > 2)
        {
            throw new FormatException($"unsupported transaction version {version}");
        }

        ulong unlockTime = reader.ReadVarInt();
        TxIn[] inputs = ReadInputs(ref reader);
        TxOut[] outputs = ReadOutputs(ref reader);
        byte[] extra = reader.ReadArray(reader.ReadCount(1));

        int prefixLength = reader.Position;

        if (version == 1)
        {
            byte[][] signatures = ReadRingSignatures(ref reader, inputs);
            return new Transaction(version, unlockTime, inputs, outputs, extra, null, signatures,
                blob[..reader.Position].ToArray(), prefixLength, reader.Position);
        }

        // An empty input list means there is no rctSigBase at all — monero skips it
        // rather than writing a zero type.
        RctBase? rct = inputs.Length == 0 ? null : ReadRctBase(ref reader, inputs.Length, outputs.Length);
        int unprunableLength = reader.Position;

        // The proofs are left as bytes: verifying them belongs to RingCT, and the
        // transaction hash needs exactly this range regardless.
        if (swallowProofs)
        {
            reader.ReadBytes(reader.Remaining);
        }
        else if (rct is not null && rct.Type != RctBase.Null)
        {
            throw new FormatException("an embedded transaction must carry no proofs");
        }

        return new Transaction(version, unlockTime, inputs, outputs, extra, rct, [],
            blob[..reader.Position].ToArray(), prefixLength, unprunableLength);
    }

    private static TxIn[] ReadInputs(ref Reader reader)
    {
        int count = reader.ReadCount(1);
        TxIn[] inputs = new TxIn[count];

        for (int i = 0; i < count; i++)
        {
            byte tag = reader.ReadByte();
            inputs[i] = tag switch
            {
                TxInGen => new TxIn.Gen(reader.ReadVarInt()),
                TxInToKey => ReadToKey(ref reader),
                TxInToScript or TxInToScriptHash => throw new FormatException($"input variant {tag} is not used by consensus"),
                _ => throw new FormatException($"unknown input tag {tag}"),
            };
        }

        return inputs;

        static TxIn.ToKey ReadToKey(ref Reader reader)
        {
            ulong amount = reader.ReadVarInt();

            int offsets = reader.ReadCount(1);
            ulong[] keyOffsets = new ulong[offsets];
            for (int i = 0; i < offsets; i++) keyOffsets[i] = reader.ReadVarInt();

            return new TxIn.ToKey(amount, keyOffsets, reader.ReadKey());
        }
    }

    private static TxOut[] ReadOutputs(ref Reader reader)
    {
        int count = reader.ReadCount(1);
        TxOut[] outputs = new TxOut[count];

        for (int i = 0; i < count; i++)
        {
            ulong amount = reader.ReadVarInt();
            byte tag = reader.ReadByte();

            TxOutTarget target = tag switch
            {
                TxOutToKey => new TxOutTarget.ToKey(reader.ReadKey()),
                TxOutToTaggedKey => new TxOutTarget.ToTaggedKey(reader.ReadKey(), reader.ReadByte()),
                _ => throw new FormatException($"unknown output tag {tag}"),
            };

            outputs[i] = new TxOut(amount, target);
        }

        return outputs;
    }

    /// <summary>v1 rings: one signature per ring member, per input.</summary>
    private static byte[][] ReadRingSignatures(ref Reader reader, TxIn[] inputs)
    {
        byte[][] signatures = new byte[inputs.Length][];

        for (int i = 0; i < inputs.Length; i++)
        {
            int members = inputs[i] switch
            {
                TxIn.ToKey key => key.KeyOffsets.Length,
                _ => 0,
            };

            signatures[i] = reader.ReadArray(members * 64);
        }

        return signatures;
    }

    private static RctBase ReadRctBase(ref Reader reader, int inputs, int outputs)
    {
        byte type = reader.ReadByte();

        if (type == RctBase.Null)
        {
            return new RctBase(type, 0, [], [], []);
        }

        if (type > RctBase.BulletproofPlus)
        {
            throw new FormatException($"unknown RingCT type {type}");
        }

        ulong fee = reader.ReadVarInt();

        // Only the earliest simple type keeps pseudo-outs here; bulletproofs moved
        // them into the prunable part.
        byte[][] pseudoOuts = [];
        if (type == RctBase.Simple)
        {
            pseudoOuts = new byte[inputs][];
            for (int i = 0; i < inputs; i++) pseudoOuts[i] = reader.ReadKey();
        }

        // From Bulletproof2 on, the mask is derived rather than sent, and the amount
        // is truncated to eight bytes.
        bool shortAmounts = type is RctBase.Bulletproof2 or RctBase.Clsag or RctBase.BulletproofPlus;

        EcdhInfo[] ecdh = new EcdhInfo[outputs];
        for (int i = 0; i < outputs; i++)
        {
            ecdh[i] = shortAmounts
                ? new EcdhInfo(null, reader.ReadArray(8))
                : new EcdhInfo(reader.ReadKey(), reader.ReadKey());
        }

        byte[][] outPk = new byte[outputs][];
        for (int i = 0; i < outputs; i++) outPk[i] = reader.ReadKey();

        return new RctBase(type, fee, pseudoOuts, ecdh, outPk);
    }
}
