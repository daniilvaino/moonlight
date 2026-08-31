using System.Buffers.Binary;

namespace Moonlight.Serialization.Epee;

/// <summary>A value in an Epee document: a number, a string, a nested section, or an array of those.</summary>
public abstract record EpeeValue
{
    public sealed record Number(ulong Value, bool IsSigned, int Width) : EpeeValue;

    public sealed record Text(byte[] Value) : EpeeValue;

    public sealed record Flag(bool Value) : EpeeValue;

    public sealed record Real(double Value) : EpeeValue;

    public sealed record Section(IReadOnlyDictionary<string, EpeeValue> Fields) : EpeeValue
    {
        public EpeeValue? this[string name] => Fields.TryGetValue(name, out EpeeValue? value) ? value : null;
    }

    public sealed record Array(IReadOnlyList<EpeeValue> Items) : EpeeValue;
}

/// <summary>
/// Epee portable storage, the format monerod's binary endpoints speak. Written to
/// the spec, cross-checked against monero-oxide's reader.
/// </summary>
/// <remarks>
/// Two things surprise people. The length prefix is not the consensus varint: the
/// low two bits say how wide the number is, and the value is what remains after
/// shifting them off. And a field is an array not by its type but by a flag on it,
/// so the same type byte means one value or many.
/// </remarks>
public static class PortableStorage
{
    private const uint SignatureA = 0x0101_1101;
    private const uint SignatureB = 0x0102_0101;
    private const byte FormatVersion = 1;

    private const byte TypeInt64 = 1;
    private const byte TypeInt32 = 2;
    private const byte TypeInt16 = 3;
    private const byte TypeInt8 = 4;
    private const byte TypeUInt64 = 5;
    private const byte TypeUInt32 = 6;
    private const byte TypeUInt16 = 7;
    private const byte TypeUInt8 = 8;
    private const byte TypeDouble = 9;
    private const byte TypeString = 10;
    private const byte TypeBool = 11;
    private const byte TypeObject = 12;
    private const byte TypeArray = 13;

    private const byte ArrayFlag = 0x80;

    public static EpeeValue.Section Parse(ReadOnlySpan<byte> data)
    {
        Reader reader = new(data);

        if (BinaryPrimitives.ReadUInt32LittleEndian(reader.ReadBytes(4)) != SignatureA ||
            BinaryPrimitives.ReadUInt32LittleEndian(reader.ReadBytes(4)) != SignatureB)
        {
            throw new FormatException("not an Epee document");
        }

        byte version = reader.ReadByte();
        if (version != FormatVersion)
        {
            throw new FormatException($"Epee format version {version} is not supported");
        }

        return ReadSection(ref reader);
    }

    /// <summary>
    /// Epee's own length prefix: the low two bits are a width, the rest is the
    /// value. Nothing to do with the varint transactions use.
    /// </summary>
    public static ulong ReadLength(ref Reader reader)
    {
        byte first = reader.ReadByte();

        int extra = (first & 0x03) switch { 0 => 0, 1 => 1, 2 => 3, _ => 7 };

        Span<byte> raw = stackalloc byte[8];
        raw[0] = first;
        reader.ReadBytes(extra).CopyTo(raw[1..]);

        return BinaryPrimitives.ReadUInt64LittleEndian(raw) >> 2;
    }

    private static EpeeValue.Section ReadSection(ref Reader reader)
    {
        ulong count = ReadLength(ref reader);
        Dictionary<string, EpeeValue> fields = new(StringComparer.Ordinal);

        for (ulong i = 0; i < count; i++)
        {
            // A name is at most 255 bytes and carries a plain one-byte length.
            string name = System.Text.Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadByte()));
            EpeeValue value = ReadEntry(ref reader);

            // monero refuses a repeated name rather than letting one silently win.
            if (!fields.TryAdd(name, value))
            {
                throw new FormatException($"duplicate field '{name}'");
            }
        }

        return new EpeeValue.Section(fields);
    }

    private static EpeeValue ReadEntry(ref Reader reader)
    {
        byte type = reader.ReadByte();

        if ((type & ArrayFlag) != 0)
        {
            byte element = (byte)(type & ~ArrayFlag);
            ulong count = ReadLength(ref reader);

            List<EpeeValue> items = new((int)Math.Min(count, 4096));
            for (ulong i = 0; i < count; i++) items.Add(ReadValue(ref reader, element));

            return new EpeeValue.Array(items);
        }

        return ReadValue(ref reader, type);
    }

    private static EpeeValue ReadValue(ref Reader reader, byte type) => type switch
    {
        TypeInt8 => new EpeeValue.Number(reader.ReadByte(), true, 1),
        TypeUInt8 => new EpeeValue.Number(reader.ReadByte(), false, 1),
        TypeInt16 => new EpeeValue.Number(BinaryPrimitives.ReadUInt16LittleEndian(reader.ReadBytes(2)), true, 2),
        TypeUInt16 => new EpeeValue.Number(BinaryPrimitives.ReadUInt16LittleEndian(reader.ReadBytes(2)), false, 2),
        TypeInt32 => new EpeeValue.Number(BinaryPrimitives.ReadUInt32LittleEndian(reader.ReadBytes(4)), true, 4),
        TypeUInt32 => new EpeeValue.Number(BinaryPrimitives.ReadUInt32LittleEndian(reader.ReadBytes(4)), false, 4),
        TypeInt64 => new EpeeValue.Number(BinaryPrimitives.ReadUInt64LittleEndian(reader.ReadBytes(8)), true, 8),
        TypeUInt64 => new EpeeValue.Number(BinaryPrimitives.ReadUInt64LittleEndian(reader.ReadBytes(8)), false, 8),
        TypeDouble => new EpeeValue.Real(BinaryPrimitives.ReadDoubleLittleEndian(reader.ReadBytes(8))),
        TypeBool => new EpeeValue.Flag(reader.ReadByte() != 0),
        TypeString => new EpeeValue.Text(ReadBlob(ref reader)),
        TypeObject => ReadSection(ref reader),
        TypeArray => ReadArrayEntry(ref reader),
        _ => throw new FormatException($"unknown Epee type {type}"),
    };

    /// <summary>The explicit array type, whose element type follows rather than being folded into a flag.</summary>
    private static EpeeValue.Array ReadArrayEntry(ref Reader reader)
    {
        byte element = reader.ReadByte();
        ulong count = ReadLength(ref reader);

        List<EpeeValue> items = new((int)Math.Min(count, 4096));
        for (ulong i = 0; i < count; i++) items.Add(ReadValue(ref reader, element));

        return new EpeeValue.Array(items);
    }

    private static byte[] ReadBlob(ref Reader reader)
    {
        ulong length = ReadLength(ref reader);

        if (length > (ulong)reader.Remaining)
        {
            throw new FormatException($"string of {length} bytes with {reader.Remaining} left");
        }

        return reader.ReadArray((int)length);
    }
}
