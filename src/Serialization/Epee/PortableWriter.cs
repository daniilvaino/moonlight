using System.Buffers.Binary;

namespace Moonlight.Serialization.Epee;

/// <summary>
/// Writes Epee documents — enough of the format to ask monerod's binary endpoints
/// a question. Requests carry heights, hashes and flags; nothing here needs the
/// float or signed types.
/// </summary>
public sealed class PortableWriter
{
    private const uint SignatureA = 0x0101_1101;
    private const uint SignatureB = 0x0102_0101;

    private readonly List<byte> buffer = [];

    public static byte[] Section(Action<PortableWriter> write)
    {
        ArgumentNullException.ThrowIfNull(write);

        PortableWriter writer = new();

        Span<byte> header = stackalloc byte[9];
        BinaryPrimitives.WriteUInt32LittleEndian(header, SignatureA);
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..], SignatureB);
        header[8] = 1;
        writer.buffer.AddRange(header.ToArray());

        writer.WriteBody(write);
        return [.. writer.buffer];
    }

    private List<(string Name, Action<PortableWriter> Value)>? fields;

    public void Number(string name, ulong value) => Add(name, w =>
    {
        w.buffer.Add(5);
        w.WriteFixed(value, 8);
    });

    public void Byte(string name, byte value) => Add(name, w =>
    {
        w.buffer.Add(8);
        w.buffer.Add(value);
    });

    public void Bool(string name, bool value) => Add(name, w =>
    {
        w.buffer.Add(11);
        w.buffer.Add(value ? (byte)1 : (byte)0);
    });

    public void Bytes(string name, ReadOnlySpan<byte> value)
    {
        byte[] copy = value.ToArray();

        Add(name, w =>
        {
            w.buffer.Add(10);
            w.WriteLength((ulong)copy.Length);
            w.buffer.AddRange(copy);
        });
    }

    public void NumberArray(string name, IReadOnlyList<ulong> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        ulong[] copy = [.. values];

        Add(name, w =>
        {
            w.buffer.Add(5 | 0x80);
            w.WriteLength((ulong)copy.Length);
            foreach (ulong value in copy) w.WriteFixed(value, 8);
        });
    }

    /// <summary>An array of byte strings — how hashes travel in a request.</summary>
    public void BytesArray(string name, IReadOnlyList<byte[]> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        byte[][] copy = [.. values];

        Add(name, w =>
        {
            w.buffer.Add(10 | 0x80);
            w.WriteLength((ulong)copy.Length);

            foreach (byte[] value in copy)
            {
                w.WriteLength((ulong)value.Length);
                w.buffer.AddRange(value);
            }
        });
    }

    /// <summary>A nested section: the object type, then the same body as a document without its header.</summary>
    public void Section(string name, Action<PortableWriter> write)
    {
        ArgumentNullException.ThrowIfNull(write);

        Add(name, w =>
        {
            w.buffer.Add(12);
            w.WriteBody(write);
        });
    }

    /// <summary>An array of sections — how monerod returns a list of blocks.</summary>
    public void Sections(string name, IReadOnlyList<Action<PortableWriter>> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        Action<PortableWriter>[] copy = [.. items];

        Add(name, w =>
        {
            w.buffer.Add(12 | 0x80);
            w.WriteLength((ulong)copy.Length);

            foreach (Action<PortableWriter> item in copy) w.WriteBody(item);
        });
    }

    private void WriteBody(Action<PortableWriter> write)
    {
        List<(string Name, Action<PortableWriter> Value)> nested = [];
        PortableWriter collector = new() { fields = nested };
        write(collector);

        WriteLength((ulong)nested.Count);
        foreach ((string name, Action<PortableWriter> value) in nested)
        {
            WriteName(name);
            value(this);
        }
    }

    private void Add(string name, Action<PortableWriter> value)
        => (fields ?? throw new InvalidOperationException("write fields through Section")).Add((name, value));

    private void WriteName(string name)
    {
        byte[] encoded = System.Text.Encoding.UTF8.GetBytes(name);

        if (encoded.Length > 255)
        {
            throw new ArgumentException("an Epee field name is at most 255 bytes", nameof(name));
        }

        buffer.Add((byte)encoded.Length);
        buffer.AddRange(encoded);
    }

    /// <summary>The low two bits are the width; the value occupies the rest.</summary>
    private void WriteLength(ulong value)
    {
        Span<byte> raw = stackalloc byte[8];

        if (value <= 0x3F)
        {
            buffer.Add((byte)(value << 2));
        }
        else if (value <= 0x3FFF)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(raw, (ushort)((value << 2) | 1));
            buffer.AddRange(raw[..2].ToArray());
        }
        else if (value <= 0x3FFF_FFFF)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(raw, (uint)((value << 2) | 2));
            buffer.AddRange(raw[..4].ToArray());
        }
        else
        {
            BinaryPrimitives.WriteUInt64LittleEndian(raw, (value << 2) | 3);
            buffer.AddRange(raw.ToArray());
        }
    }

    private void WriteFixed(ulong value, int width)
    {
        Span<byte> raw = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(raw, value);
        buffer.AddRange(raw[..width].ToArray());
    }
}
