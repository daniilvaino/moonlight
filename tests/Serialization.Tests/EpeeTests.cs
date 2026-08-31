using Moonlight.Serialization.Epee;
using Xunit;

namespace Moonlight.Serialization.Tests;

public class EpeeTests
{
    /// <summary>
    /// Byte for byte from monero's own epee_serialization.cpp: two boolean fields
    /// named a and b. The nine-byte header is two signatures and a version.
    /// </summary>
    [Fact]
    public void ReadsMoneroTwoKeyDocument()
    {
        byte[] data =
        [
            0x01, 0x11, 0x01, 0x01, 0x01, 0x01, 0x02, 0x01, 0x01,
            0x08,                                   // two fields: 8 >> 2
            0x01, (byte)'a', 0x0B, 0x00,
            0x01, (byte)'b', 0x0B, 0x00,
        ];

        EpeeValue.Section section = PortableStorage.Parse(data);

        Assert.Equal(2, section.Fields.Count);
        Assert.Equal(new EpeeValue.Flag(false), section["a"]);
        Assert.Equal(new EpeeValue.Flag(false), section["b"]);
    }

    /// <summary>
    /// The same document with both fields named a. monero refuses it, and so do we:
    /// letting one silently win makes the meaning depend on parser order.
    /// </summary>
    [Fact]
    public void RefusesADuplicateName()
    {
        byte[] data =
        [
            0x01, 0x11, 0x01, 0x01, 0x01, 0x01, 0x02, 0x01, 0x01,
            0x08,
            0x01, (byte)'a', 0x0B, 0x00,
            0x01, (byte)'a', 0x0B, 0x00,
        ];

        Assert.Throws<FormatException>(() => PortableStorage.Parse(data));
    }

    /// <summary>
    /// Empty arrays of every type, again from monero's tests. The type byte carries
    /// the array flag; an empty one still says what it would have held.
    /// </summary>
    [Theory]
    [InlineData(0x8B)]   // bools
    [InlineData(0x89)]   // doubles
    [InlineData(0x8A)]   // strings
    [InlineData(0x81)]   // int64s
    [InlineData(0x8C)]   // objects
    public void ReadsAnEmptyArrayOfAnyType(byte type)
    {
        byte[] data =
        [
            0x01, 0x11, 0x01, 0x01, 0x01, 0x01, 0x02, 0x01, 0x01,
            0x04,                                   // one field
            0x01, (byte)'x', type, 0x00,
        ];

        EpeeValue.Array array = Assert.IsType<EpeeValue.Array>(PortableStorage.Parse(data)["x"]);
        Assert.Empty(array.Items);
    }

    [Fact]
    public void RefusesSomethingThatIsNotEpee()
    {
        Assert.Throws<FormatException>(() => PortableStorage.Parse([1, 2, 3, 4, 5, 6, 7, 8, 9]));
        Assert.Throws<FormatException>(() => PortableStorage.Parse(
            [0x01, 0x11, 0x01, 0x01, 0x01, 0x01, 0x02, 0x01, 0x09]));   // version 9
    }

    [Fact]
    public void RoundTripsWhatWeWrite()
    {
        byte[] hash = [.. Enumerable.Range(0, 32).Select(i => (byte)i)];

        byte[] document = PortableWriter.Section(w =>
        {
            w.Number("start_height", 2_000_000);
            w.Bool("prune", true);
            w.Byte("flag", 7);
            w.Bytes("hash", hash);
            w.NumberArray("heights", [1, 2, 3]);
            w.BytesArray("block_ids", [hash, hash]);
        });

        EpeeValue.Section section = PortableStorage.Parse(document);

        Assert.Equal(2_000_000UL, Assert.IsType<EpeeValue.Number>(section["start_height"]).Value);
        Assert.True(Assert.IsType<EpeeValue.Flag>(section["prune"]).Value);
        Assert.Equal(7UL, Assert.IsType<EpeeValue.Number>(section["flag"]).Value);
        Assert.Equal(hash, Assert.IsType<EpeeValue.Text>(section["hash"]).Value);

        EpeeValue.Array heights = Assert.IsType<EpeeValue.Array>(section["heights"]);
        Assert.Equal([1UL, 2UL, 3UL], heights.Items.Select(i => ((EpeeValue.Number)i).Value));

        EpeeValue.Array ids = Assert.IsType<EpeeValue.Array>(section["block_ids"]);
        Assert.Equal(2, ids.Items.Count);
        Assert.All(ids.Items, item => Assert.Equal(hash, ((EpeeValue.Text)item).Value));
    }

    /// <summary>
    /// The length prefix is not the consensus varint: its low two bits give the
    /// width and the value is what remains after shifting them off. These lengths
    /// sit either side of the one-, two- and four-byte boundaries.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(63)]        // largest length that fits one byte
    [InlineData(64)]        // first that needs two
    [InlineData(16_383)]
    [InlineData(16_384)]    // first that needs four
    [InlineData(70_000)]
    public void EveryLengthWidthRoundTrips(int length)
    {
        byte[] payload = [.. Enumerable.Range(0, length).Select(i => (byte)i)];

        EpeeValue.Section section = PortableStorage.Parse(PortableWriter.Section(w => w.Bytes("v", payload)));

        Assert.Equal(payload, Assert.IsType<EpeeValue.Text>(section["v"]).Value);
    }
}
