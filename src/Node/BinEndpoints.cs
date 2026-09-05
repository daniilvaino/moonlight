using System.Net.Http;
using Moonlight.Serialization;
using Moonlight.Serialization.Epee;

namespace Moonlight.Node;

/// <summary>One block as the binary endpoint returns it: the block itself and its transactions.</summary>
public sealed record BlockBundle(Block Block, IReadOnlyList<Transaction> Transactions);

/// <summary>
/// A batch of blocks and where they sit. The daemon decides where to start from —
/// it answers from the first block in our locator that it recognises — so the
/// height it reports is the authority, not the one we asked for.
/// </summary>
public sealed record BlockBatch(ulong StartHeight, ulong ChainHeight, IReadOnlyList<BlockBundle> Blocks);

/// <summary>
/// monerod's binary endpoints. The JSON ones fetch a block per request; this
/// fetches them in batches, which is the difference between scanning a chain in
/// hours and in days.
/// </summary>
public static class BinEndpoints
{
    /// <summary>
    /// Blocks from a height onward, as many as the daemon cares to send. The
    /// request names the last block ids we know so the daemon can tell us where we
    /// diverge; passing the genesis id alone asks it to start from start_height.
    /// </summary>
    /// <summary>
    /// The request body, built without sending it. Separate because the sync engine
    /// composes requests it does not itself transmit: the socket belongs to whoever
    /// is driving, which for a linked library is not us.
    /// </summary>
    public static byte[] BuildGetBlocksRequest(ulong startHeight, IReadOnlyList<byte[]> knownBlockIds)
    {
        ArgumentNullException.ThrowIfNull(knownBlockIds);

        // block_ids is one blob of concatenated hashes, not an array of them —
        // KV_SERIALIZE_CONTAINER_POD_AS_BLOB in the daemon's own definition. Sending
        // an array is refused with a 400 and no explanation.
        byte[] locator = new byte[knownBlockIds.Count * 32];
        for (int i = 0; i < knownBlockIds.Count; i++) knownBlockIds[i].CopyTo(locator, i * 32);

        return PortableWriter.Section(w =>
        {
            // client is not optional in the daemon's definition, even when RPC
            // payment is off: a request without it fails to parse and comes back
            // as a bare 400.
            w.Bytes("client", []);
            w.Bytes("block_ids", locator);
            w.Number("start_height", startHeight);
            // Pruned: the proofs are four fifths of a block by weight and a scan
            // needs none of them — only the outputs, their keys and the view tag.
            // Measured against a mainnet node, one batch of 490 blocks fell from
            // 48.0 MiB to 10.4 MiB, and from 115 s to 28 s.
            w.Bool("prune", true);
            w.Bool("no_miner_tx", false);
        });
    }

    public static async Task<BlockBatch> GetBlocksAsync(
        HttpClient http,
        ulong startHeight,
        IReadOnlyList<byte[]> knownBlockIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(http);

        byte[] request = BuildGetBlocksRequest(startHeight, knownBlockIds);

        using ByteArrayContent content = new(request);
        using HttpResponseMessage response = await http.PostAsync("getblocks.bin", content, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        return ParseBatch(body);
    }

    /// <summary>The whole response: the blocks and the two heights that frame them.</summary>
    public static BlockBatch ParseBatch(ReadOnlySpan<byte> response)
    {
        EpeeValue.Section root = PortableStorage.Parse(response);

        return new BlockBatch(
            Number(root["start_height"]),
            Number(root["current_height"]),
            ParseBlocks(response));
    }

    private static ulong Number(EpeeValue? value) => value is EpeeValue.Number number ? number.Value : 0;

    /// <summary>
    /// Reads the blocks out of a getblocks.bin response. Public because the shape of
    /// that response is worth testing on its own, without a daemon in the way.
    /// </summary>
    public static IReadOnlyList<BlockBundle> ParseBlocks(ReadOnlySpan<byte> response)
    {
        EpeeValue.Section root = PortableStorage.Parse(response);

        if (root["status"] is EpeeValue.Text status &&
            System.Text.Encoding.ASCII.GetString(status.Value) is string text && text != "OK")
        {
            throw new DaemonException($"getblocks.bin returned {text}");
        }

        if (root["blocks"] is not EpeeValue.Array blocks) return [];

        List<BlockBundle> bundles = new(blocks.Items.Count);

        foreach (EpeeValue item in blocks.Items)
        {
            if (item is not EpeeValue.Section entry || entry["block"] is not EpeeValue.Text blob) continue;

            Block block = BlockParser.Parse(blob.Value);
            List<Transaction> transactions = [];

            // A block with no transactions of its own omits the field entirely
            // rather than sending an empty array.
            if (entry["txs"] is EpeeValue.Array txs)
            {
                foreach (EpeeValue tx in txs.Items)
                {
                    // A full block sends the blob straight; a pruned one wraps it in a
                    // section beside the hash of what was dropped. Anything else is
                    // refused rather than skipped: a transaction quietly left out is a
                    // payment the wallet never sees and never mentions.
                    byte[] txBlob = tx switch
                    {
                        EpeeValue.Text raw => raw.Value,
                        EpeeValue.Section pruned when pruned["blob"] is EpeeValue.Text b => b.Value,
                        _ => throw new DaemonException("a transaction in getblocks.bin was in no form we know"),
                    };

                    transactions.Add(TxParser.Parse(txBlob));
                }
            }

            bundles.Add(new BlockBundle(block, transactions));
        }

        return bundles;
    }
}
