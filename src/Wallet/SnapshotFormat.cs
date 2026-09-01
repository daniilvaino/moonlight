using System.Buffers.Binary;
using Moonlight.Crypto;
using Moonlight.Serialization;

namespace Moonlight.Wallet;

/// <summary>
/// How a scan's results are written into the wallet file. Its own format, not
/// consensus, so it uses the same VarInt and nothing else clever.
/// </summary>
internal static class SnapshotFormat
{
    public static void Write(List<byte> body, WalletSnapshot snapshot)
    {
        WriteVarInt(body, (ulong)snapshot.Outputs.Count);

        foreach (OwnedOutput output in snapshot.Outputs)
        {
            WriteVarInt(body, output.Height);
            body.AddRange(output.TransactionId);
            WriteVarInt(body, (ulong)output.OutputIndex);
            body.AddRange(output.Key.ToBytes());
            WriteFixed(body, output.Amount);
            body.AddRange(output.Mask.ToBytes());
            WriteVarInt(body, output.Subaddress.Major);
            WriteVarInt(body, output.Subaddress.Minor);
            WriteVarInt(body, output.UnlockTime);
            body.Add(output.IsCoinbase ? (byte)1 : (byte)0);

            // A view-only wallet has none, and that difference has to survive.
            body.Add(output.KeyImage is null ? (byte)0 : (byte)1);
            if (output.KeyImage is { } image) body.AddRange(image.ToBytes());
        }

        WriteVarInt(body, (ulong)snapshot.Spent.Count);

        foreach ((string image, ulong height) in snapshot.Spent)
        {
            body.AddRange(Convert.FromHexString(image));
            WriteVarInt(body, height);
        }
    }

    public static WalletSnapshot Read(ref Reader reader, ulong scannedHeight)
    {
        int count = reader.ReadCount(100);
        List<OwnedOutput> outputs = new(count);

        for (int i = 0; i < count; i++)
        {
            ulong height = reader.ReadVarInt();
            byte[] transactionId = reader.ReadKey();
            int index = (int)reader.ReadVarInt();
            Point key = Point.FromBytes(reader.ReadBytes(32));
            ulong amount = BinaryPrimitives.ReadUInt64LittleEndian(reader.ReadBytes(8));
            Scalar mask = Scalar.FromCanonical(reader.ReadBytes(32));
            SubaddressIndex subaddress = new((uint)reader.ReadVarInt(), (uint)reader.ReadVarInt());
            ulong unlockTime = reader.ReadVarInt();
            bool coinbase = reader.ReadByte() != 0;

            Point? image = reader.ReadByte() != 0 ? Point.FromBytes(reader.ReadBytes(32)) : null;

            outputs.Add(new OwnedOutput(
                height, transactionId, index, key, amount, mask, subaddress, image, coinbase, unlockTime));
        }

        int spentCount = reader.ReadCount(33);
        Dictionary<string, ulong> spent = new(spentCount, StringComparer.Ordinal);

        for (int i = 0; i < spentCount; i++)
        {
            string image = Convert.ToHexString(reader.ReadBytes(32)).ToLowerInvariant();
            spent[image] = reader.ReadVarInt();
        }

        return new WalletSnapshot(scannedHeight, outputs, spent);
    }

    private static void WriteVarInt(List<byte> body, ulong value)
    {
        byte[] buffer = new byte[VarInt.MaxLength];
        body.AddRange(buffer.AsSpan(0, VarInt.Write(buffer, value)).ToArray());
    }

    private static void WriteFixed(List<byte> body, ulong value)
    {
        byte[] buffer = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
        body.AddRange(buffer);
    }
}
