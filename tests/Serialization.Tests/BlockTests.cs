using System.Text.Json;
using Moonlight.Serialization;
using Moonlight.Tests;
using Xunit;

namespace Moonlight.Serialization.Tests;

public class BlockTests
{
    // Block 202612 of mainnet: header fields as they were mined.
    private static readonly BlockHeader Header202612 = new(
        MajorVersion: 1,
        MinorVersion: 0,
        Timestamp: 1_409_804_570,
        PreviousId: Convert.FromHexString("5da0a3d004c352a90cc86b00fab676695d76a4d1de16036c41ba4dd188c4d76f"),
        Nonce: 1_073_744_198);

    /// <summary>
    /// The block whose transactions were duplicated to forge a valid tree hash.
    /// The chain records one hash; hashing its contents today yields another. A
    /// consumer that returns the second disagrees with the network about one block.
    /// </summary>
    [Fact]
    public void Block202612HashesToWhatTheChainRecorded()
    {
        byte[][] ids = TransactionIds202612();

        Assert.Equal(514, ids.Length);
        Assert.Equal(
            "bbd604d2ba11ba27935e006ed39c9bfdd99b76bf4a50654bc1e1e61217962698",
            Hex(BlockParser.ComputeId(Header202612, ids)));
    }

    [Fact]
    public void MerkleRootIsStableAndOrderSensitive()
    {
        byte[][] ids = TransactionIds202612();
        byte[] root = MerkleTree.Root(ids);

        Assert.Equal(32, root.Length);
        Assert.Equal(root, MerkleTree.Root(ids));

        (ids[0], ids[1]) = (ids[1], ids[0]);
        Assert.NotEqual(root, MerkleTree.Root(ids));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(513)]
    [InlineData(514)]
    public void MerkleRootHandlesEveryTreeShape(int count)
    {
        byte[][] ids = [.. TransactionIds202612().Take(count)];

        Assert.Equal(32, MerkleTree.Root(ids).Length);

        // One leaf is itself; two leaves are one hash of the pair. Everything else
        // folds down to a power of two first, which is where the original code was
        // wrong.
        if (count == 1) Assert.Equal(ids[0], MerkleTree.Root(ids));
    }

    [Fact]
    public void RoundTripsABlockBlob()
    {
        Transaction miner = MinerTransaction();
        byte[][] ids = [.. TransactionIds202612().Take(3)];

        byte[] blob = BuildBlockBlob(Header202612, miner.Blob, ids);
        Block block = BlockParser.Parse(blob);

        Assert.Equal(Header202612, block.Header);
        Assert.True(block.MinerTransaction.IsCoinbase);
        Assert.Equal(3, block.TransactionIds.Length);
        Assert.Equal(4, block.AllTransactionIds().Length);

        Assert.Throws<FormatException>(() => BlockParser.Parse([.. blob, 0x00]));
    }

    [Fact]
    public void RefusesABlockWhoseFirstTransactionIsNotCoinbase()
    {
        byte[] spend = Transactions().First(t => TxParser.Parse(t).IsCoinbase == false);

        Assert.Throws<FormatException>(() =>
            BlockParser.Parse(BuildBlockBlob(Header202612, spend, [])));
    }

    private static byte[] BuildBlockBlob(BlockHeader header, byte[] minerBlob, byte[][] ids)
    {
        List<byte> blob = [.. header.Serialize(), .. minerBlob];

        byte[] count = new byte[Moonlight.Crypto.VarInt.MaxLength];
        int n = Moonlight.Crypto.VarInt.Write(count, (ulong)ids.Length);
        blob.AddRange(count[..n]);

        foreach (byte[] id in ids) blob.AddRange(id);
        return [.. blob];
    }

    private static Transaction MinerTransaction()
        => Transactions().Select(b => TxParser.Parse(b)).First(t => t.IsCoinbase);

    private static List<byte[]> Transactions()
    {
        using JsonDocument doc = Corpus.Json("blocks", "transactions.json");
        return [.. doc.RootElement.EnumerateArray().Select(e => Convert.FromHexString(e.GetProperty("hex").GetString()!))];
    }

    private static byte[][] TransactionIds202612()
    {
        using JsonDocument doc = Corpus.Json("blocks", "block_202612_transactions.txt");
        return [.. doc.RootElement.EnumerateArray().Select(e => Convert.FromHexString(e.GetString()!))];
    }

    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
}
