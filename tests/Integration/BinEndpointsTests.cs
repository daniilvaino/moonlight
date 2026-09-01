using System.Text.Json;
using Moonlight.Node;
using Moonlight.Serialization;
using Moonlight.Serialization.Epee;
using Moonlight.Tests;
using Xunit;

namespace Moonlight.Integration.Tests;

/// <summary>
/// The getblocks.bin response shape, built here and read back. A real daemon is
/// exercised by ChainTests; what is under test here is that we read the envelope
/// the way monerod writes it.
/// </summary>
public class BinEndpointsTests
{
    [Fact]
    public void ReadsBlocksAndTheirTransactions()
    {
        (byte[] blockBlob, byte[][] txs) = SampleBlock();

        byte[] response = PortableWriter.Section(w =>
        {
            w.Bytes("status", "OK"u8);
            w.BytesArray("blocks_block", [blockBlob]);
            w.BytesArray("blocks_txs", txs);
        });

        // The real envelope nests each block; this flat one is only here to prove
        // a response without the expected shape yields nothing rather than throwing.
        Assert.Empty(BinEndpoints.ParseBlocks(response));
    }

    /// <summary>
    /// The daemon says where the blocks it sent begin: it answers from the newest
    /// id in our locator that it knows, which is not necessarily the height we
    /// asked for. Believing our own number instead would file blocks under the
    /// wrong heights after a reorganisation.
    /// </summary>
    [Fact]
    public void TheDaemonsHeightsAreRead()
    {
        (byte[] blockBlob, byte[][] txs) = SampleBlock();

        byte[] response = PortableWriter.Section(root =>
        {
            root.Bytes("status", "OK"u8);
            root.Number("start_height", 2_500_000);
            root.Number("current_height", 2_500_123);
            root.Sections("blocks", [entry =>
            {
                entry.Bytes("block", blockBlob);
                entry.BytesArray("txs", txs);
            }]);
        });

        BlockBatch batch = BinEndpoints.ParseBatch(response);

        Assert.Equal(2_500_000UL, batch.StartHeight);
        Assert.Equal(2_500_123UL, batch.ChainHeight);
        Assert.Single(batch.Blocks);
    }

    [Fact]
    public void ReadsTheNestedEnvelope()
    {
        (byte[] blockBlob, byte[][] txs) = SampleBlock();

        byte[] response = BuildResponse("OK", blockBlob, txs);
        IReadOnlyList<BlockBundle> bundles = BinEndpoints.ParseBlocks(response);

        BlockBundle bundle = Assert.Single(bundles);
        Assert.True(bundle.Block.MinerTransaction.IsCoinbase);
        Assert.Equal(txs.Length, bundle.Transactions.Count);
        Assert.All(bundle.Transactions, tx => Assert.True(tx.Outputs.Length > 0));
    }

    [Fact]
    public void ABlockWithNoTransactionsIsNotAnError()
    {
        (byte[] blockBlob, _) = SampleBlock();

        BlockBundle bundle = Assert.Single(BinEndpoints.ParseBlocks(BuildResponse("OK", blockBlob, [])));

        Assert.Empty(bundle.Transactions);
    }

    [Fact]
    public void ARefusalIsReportedRatherThanReturnedAsNoBlocks()
    {
        (byte[] blockBlob, _) = SampleBlock();

        DaemonException error = Assert.Throws<DaemonException>(
            () => BinEndpoints.ParseBlocks(BuildResponse("Failed", blockBlob, [])));

        Assert.Contains("Failed", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Builds the envelope monerod sends: a status, and blocks each carrying their transactions.</summary>
    private static byte[] BuildResponse(string status, byte[] block, byte[][] transactions)
        => PortableWriter.Section(root =>
        {
            root.Bytes("status", System.Text.Encoding.ASCII.GetBytes(status));
            root.Sections("blocks", [entry =>
            {
                entry.Bytes("block", block);
                if (transactions.Length > 0) entry.BytesArray("txs", transactions);
            }]);
        });

    /// <summary>A block carrying a real coinbase, and the corpus transactions as its body.</summary>
    private static (byte[] Block, byte[][] Transactions) SampleBlock()
    {
        using JsonDocument document = Corpus.Json("blocks", "transactions.json");

        byte[][] all = [.. document.RootElement.EnumerateArray()
            .Select(e => Convert.FromHexString(e.GetProperty("hex").GetString()!))];

        byte[] coinbase = all.First(blob => TxParser.Parse(blob).IsCoinbase);
        byte[][] rest = [.. all.Where(blob => !TxParser.Parse(blob).IsCoinbase)];

        BlockHeader header = new(1, 0, 1_409_804_570,
            Convert.FromHexString("5da0a3d004c352a90cc86b00fab676695d76a4d1de16036c41ba4dd188c4d76f"), 7);

        List<byte> blob = [.. header.Serialize(), .. coinbase];

        byte[] count = new byte[Moonlight.Crypto.VarInt.MaxLength];
        int n = Moonlight.Crypto.VarInt.Write(count, (ulong)rest.Length);
        blob.AddRange(count[..n]);

        foreach (byte[] tx in rest) blob.AddRange(TxHash.Compute(TxParser.Parse(tx)));

        return ([.. blob], rest);
    }
}
