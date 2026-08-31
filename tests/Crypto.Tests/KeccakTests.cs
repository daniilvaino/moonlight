using Moonlight.Crypto;
using Xunit;

namespace Moonlight.Crypto.Tests;

public class KeccakTests
{
    // Original Keccak-256, not SHA3-256. Every vector here differs from the SHA-3
    // answer for the same input, which is the point of having them.
    [Theory]
    [InlineData("", "c5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a470")]
    [InlineData("abc", "4e03657aea45a94fc7d47ba826c8d667c0d1e6e33a64a036ec44f58fa12d6c45")]
    [InlineData("The quick brown fox jumps over the lazy dog",
                "4d741b6f1eb29cb2a9b9911c82f56fa8d73b04959d3d9d222895df6c0b28aa15")]
    public void MatchesKnownAnswers(string text, string expected)
        => Assert.Equal(expected, Hex(Keccak.Hash(System.Text.Encoding.UTF8.GetBytes(text))));

    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

    [Theory]
    [InlineData(135)]   // one short of the rate
    [InlineData(136)]   // exactly the rate: padding takes a whole extra block
    [InlineData(137)]
    [InlineData(272)]
    public void HandlesRateBoundaries(int length)
    {
        byte[] data = new byte[length];
        for (int i = 0; i < length; i++) data[i] = (byte)i;

        Assert.Equal(32, Keccak.Hash(data).Length);
    }

    [Fact]
    public void SpanOverloadMatchesAllocating()
    {
        byte[] data = [1, 2, 3, 4, 5];
        byte[] destination = new byte[32];

        Keccak.Hash(data, destination);

        Assert.Equal(Keccak.Hash(data), destination);
    }
}
