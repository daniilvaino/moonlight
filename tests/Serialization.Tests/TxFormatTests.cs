using Moonlight.Serialization;
using Moonlight.Tests;
using Xunit;

namespace Moonlight.Serialization.Tests;

/// <summary>
/// One real transaction of every format monero has had, from its own
/// <c>tests/data/txs</c>. The five transactions the parser was checked against
/// before these are all recent shapes; these reach back to the first version and
/// through every change on the way.
///
/// Each file is named for the transaction it holds, so the expectation travels with
/// the bytes: parse the blob, hash it, and the identifier must begin with the digits
/// in the name. A parser that reads the wrong number of bytes anywhere produces a
/// different identifier, so this one assertion covers the whole shape.
/// </summary>
public class TxFormatTests
{
    /// <summary>
    /// The eight formats, oldest first. A coinbase is its own shape in both eras —
    /// its input is a height rather than a ring, and it has no signatures to read.
    /// </summary>
    [Theory]
    [InlineData("v1_coinbase_tx_bf4c0300.bin", 1, true)]
    [InlineData("v1_tx_hf3_effcceb9.bin", 1, false)]
    [InlineData("v2_coinbase_tx_7f88a52a.bin", 2, true)]
    [InlineData("rct_full_tx_14056427.bin", 2, false)]
    [InlineData("rct_simple_tx_c69861bf.bin", 2, false)]
    [InlineData("rct_bp_tx_a685d68e.bin", 2, false)]
    [InlineData("rct_bp_compact_tx_10312fd4.bin", 2, false)]
    [InlineData("rct_clsag_tx_200c3215.bin", 2, false)]
    [InlineData("bpp_tx_e89415.bin", 2, false)]
    public void EachFormatParsesToItsOwnIdentifier(string file, ulong version, bool coinbase)
    {
        byte[] blob = File.ReadAllBytes(Corpus.File("txs", file));

        Transaction tx = TxParser.Parse(blob);

        Assert.Equal(version, tx.Version);
        Assert.Equal(coinbase, tx.IsCoinbase);

        string id = Convert.ToHexString(TxHash.Compute(tx)).ToLowerInvariant();
        Assert.StartsWith(IdentifierIn(file), id, StringComparison.Ordinal);
    }

    /// <summary>
    /// The hex between the last underscore and the extension. A file added without
    /// one would otherwise be checked against an empty prefix, which every
    /// identifier starts with.
    /// </summary>
    private static string IdentifierIn(string file)
    {
        string name = Path.GetFileNameWithoutExtension(file);
        string prefix = name[(name.LastIndexOf('_') + 1)..];

        Assert.Matches("^[0-9a-f]{6,}$", prefix);
        return prefix;
    }
}
