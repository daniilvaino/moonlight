using Xunit;

namespace Moonlight.Crypto.Tests;

/// <summary>
/// BLAKE2b against the RFC's own vectors before it is trusted with anything of
/// monero's. The corpus cannot do this job: it only ever calls the personalised
/// form, so a fault in the compression function and a fault in the parameter block
/// would look the same there. These separate the two.
/// </summary>
public class Blake2bTests
{
    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

    /// <summary>RFC 7693, Appendix A: BLAKE2b-512 of "abc".</summary>
    [Fact]
    public void TheRfcVector()
        => Assert.Equal(
            "ba80a53f981c4d0d6a2797b69f12f6e94c212f14685ac4b74b12bb6fdbffa2d1" +
            "7d87c5392aab792dc252d5de4533cc9518d38aa8dbf1925ab92386edd4009923",
            Hex(Blake2b.Hash("abc"u8)));

    /// <summary>
    /// The empty message. It still compresses once — a block of zeroes with the
    /// counter at zero and the finalisation flag set — and a version that skipped
    /// that would return the initial state instead.
    /// </summary>
    [Fact]
    public void TheEmptyMessage()
        => Assert.Equal(
            "786a02f742015903c6c6fd852552d272912f4740e15847618a86e217f71f5419" +
            "d25e1031afee585313896444934eb04b903a685b1448b755d56f701afe9be2ce",
            Hex(Blake2b.Hash([])));

    /// <summary>
    /// Exactly one block. The loop must leave these bytes to the final compression
    /// rather than compressing them and then finalising over nothing, and the two
    /// readings of "full" differ only here.
    /// </summary>
    [Fact]
    public void ExactlyOneBlock()
    {
        byte[] message = new byte[Blake2b.BlockSize];
        for (int i = 0; i < message.Length; i++) message[i] = (byte)i;

        Assert.Equal(
            "2319e3789c47e2daa5fe807f61bec2a1a6537fa03f19ff32e87eecbfd64b7e0e" +
            "8ccff439ac333b040f19b0c4ddd11a61e24ac1fe0f10a039806c5dcc0da3d115",
            Hex(Blake2b.Hash(message)));
    }

    /// <summary>Two blocks and a bit, so the counter has to advance correctly.</summary>
    [Fact]
    public void PastTwoBlocks()
    {
        byte[] message = new byte[300];
        for (int i = 0; i < message.Length; i++) message[i] = (byte)(i * 7);

        Assert.Equal(64, Blake2b.Hash(message).Length);
        Assert.NotEqual(Hex(Blake2b.Hash(message)), Hex(Blake2b.Hash(message.AsSpan(0, 299))));
    }

    /// <summary>
    /// The personalisation is not decoration: it is xored into the state before the
    /// message, so monero's digest of an input and the plain digest of the same input
    /// have nothing to do with each other.
    /// </summary>
    /// <summary>
    /// The personalisation is not decoration: it is xored into the state before the
    /// message, so monero's digest of an input and the plain digest of the same input
    /// have nothing to do with each other. Both are pinned, because a parameter block
    /// assembled wrongly still produces a perfectly stable wrong answer.
    /// </summary>
    [Fact]
    public void PersonalisationChangesEverything()
    {
        byte[] input = new byte[32];

        Assert.Equal(
            "9ab7a73a97a1a3031406b6c169634a9c06cfb81dec3323bb4de5ce6f4b7ca107" +
            "de534442a7eaeafbaf366ccfdde1cb97d7c884e4344cd0a23039de71a56d630a",
            Hex(Blake2b.Hash(input)));

        Assert.Equal(
            "72fbe52b516218044873bfabb3db86c6b7d5b8cad0b84cf83e8eb1502243131f" +
            "b532adf71c0a2980a4d62d91415104b6dfb29f51fdebab11a9859a200793bdc3",
            Hex(Blake2b.Monero(input)));
    }

    /// <summary>
    /// A shorter digest is a different function, not a prefix of the longer one: the
    /// length goes into the parameter block and so into the initial state.
    /// </summary>
    [Fact]
    public void ShorterDigestsAreTheirOwnFunction()
    {
        Assert.Equal("bddd813c634239723171ef3fee98579b94964e3bb1cb3e427262c8c068d52319",
            Hex(Blake2b.Hash("abc"u8, 32)));

        Assert.NotEqual(Hex(Blake2b.Hash("abc"u8))[..64], Hex(Blake2b.Hash("abc"u8, 32)));
    }

    [Fact]
    public void LengthsOutsideTheRangeAreRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Blake2b.Hash("abc"u8, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Blake2b.Hash("abc"u8, 65));
    }
}
