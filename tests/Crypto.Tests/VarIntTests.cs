using Xunit;

namespace Moonlight.Crypto.Tests;

public class VarIntTests
{
    [Theory]
    [InlineData(0UL, "00")]
    [InlineData(1UL, "01")]
    [InlineData(127UL, "7f")]
    [InlineData(128UL, "8001")]
    [InlineData(300UL, "ac02")]
    [InlineData(ulong.MaxValue, "ffffffffffffffffff01")]
    public void EncodesAsMoneroDoes(ulong value, string expected)
    {
        Span<byte> buffer = stackalloc byte[VarInt.MaxLength];
        int written = VarInt.Write(buffer, value);

        Assert.Equal(expected, Convert.ToHexString(buffer[..written]).ToLowerInvariant());
        Assert.Equal(written, VarInt.Length(value));
    }

    [Fact]
    public void RoundTripsAcrossBoundaries()
    {
        List<ulong> values = [0, 1, 127, 128, 16383, 16384, ulong.MaxValue];
        for (int shift = 0; shift < 64; shift++) values.Add(1UL << shift);

        Span<byte> buffer = stackalloc byte[VarInt.MaxLength];

        foreach (ulong value in values)
        {
            int written = VarInt.Write(buffer, value);

            Assert.True(VarInt.TryRead(buffer[..written], out ulong read, out int consumed));
            Assert.Equal(value, read);
            Assert.Equal(written, consumed);
        }
    }

    [Theory]
    [InlineData("80")]                          // truncated: continuation with nothing after
    [InlineData("ffffffffffffffffff02")]        // 10th byte carries more than one bit
    [InlineData("ffffffffffffffffff81")]        // continues past 64 bits
    public void RejectsMalformed(string hex)
        => Assert.False(VarInt.TryRead(Convert.FromHexString(hex), out _, out _));
}
