using System.Net.Http;
using Moonlight.Serialization;
using Moonlight.Serialization.Epee;

namespace Moonlight.Node;

/// <summary>One block as the binary endpoint returns it: the block itself and its transactions.</summary>
public sealed record BlockBundle(Block Block, IReadOnlyList<Transaction> Transactions);

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
    public static async Task<IReadOnlyList<BlockBundle>> GetBlocksAsync(
        HttpClient http,
        ulong startHeight,
        IReadOnlyList<byte[]> knownBlockIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(knownBlockIds);

        byte[] request = PortableWriter.Section(w =>
        {
            w.BytesArray("block_ids", knownBlockIds);
            w.Number("start_height", startHeight);
            w.Bool("prune", false);
            w.Bool("no_miner_tx", false);
        });

        using ByteArrayContent content = new(request);
        using HttpResponseMessage response = await http.PostAsync("getblocks.bin", content, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        return ParseBlocks(body);
    }

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
                    if (tx is EpeeValue.Text raw) transactions.Add(TxParser.Parse(raw.Value));
                }
            }

            bundles.Add(new BlockBundle(block, transactions));
        }

        return bundles;
    }
}
