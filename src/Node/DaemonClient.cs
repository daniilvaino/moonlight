using System.Net.Http;
using System.Text;
using System.Text.Json;
using Moonlight.Serialization;

namespace Moonlight.Node;

/// <summary>
/// monerod's JSON-RPC. Enough of it to follow the chain and pull real blocks;
/// the binary endpoints are a separate matter.
/// </summary>
/// <remarks>
/// The JSON is read and written by hand. A source-generated serializer would be
/// the obvious choice and is not usable here for two reasons, both measured: it
/// still needs reflection at run time, which a small linked library cannot afford,
/// and the generator does not run under bflat at all, so the file it produces
/// would have to be built first and carried alongside the sources.
/// </remarks>
public sealed class DaemonClient : IDisposable
{
    private readonly HttpClient http;
    private readonly bool ownsClient;

    public DaemonClient(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        this.http = http;
        ownsClient = false;
    }

    public DaemonClient(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        http = new HttpClient { BaseAddress = address, Timeout = TimeSpan.FromSeconds(30) };
        ownsClient = true;
    }

    public async Task<ulong> GetHeightAsync(CancellationToken cancellationToken = default)
    {
        using JsonDocument answer = await PostAsync("get_height", "{}"u8.ToArray(), cancellationToken)
            .ConfigureAwait(false);

        return Number(answer.RootElement, "height")
            ?? throw new DaemonException("get_height said nothing about a height");
    }

    /// <summary>The block at a height, already parsed from the blob the daemon sends.</summary>
    public async Task<Block> GetBlockAsync(ulong height, CancellationToken cancellationToken = default)
    {
        using JsonDocument answer = await GetBlockRpcAsync(height, cancellationToken).ConfigureAwait(false);

        JsonElement result = Result(answer, "get_block");

        return result.TryGetProperty("blob", out JsonElement blob) && blob.GetString() is string hex
            ? BlockParser.Parse(Convert.FromHexString(hex))
            : throw new DaemonException("get_block returned no block");
    }

    public async Task<BlockHeaderInfo> GetBlockHeaderAsync(ulong height, CancellationToken cancellationToken = default)
    {
        using JsonDocument answer = await GetBlockRpcAsync(height, cancellationToken).ConfigureAwait(false);

        if (!Result(answer, "get_block").TryGetProperty("block_header", out JsonElement header))
        {
            throw new DaemonException("get_block returned no block header");
        }

        return new BlockHeaderInfo(
            Text(header, "hash") ?? "",
            Number(header, "height") ?? 0,
            (byte)(Number(header, "major_version") ?? 0),
            (byte)(Number(header, "minor_version") ?? 0),
            Number(header, "timestamp") ?? 0,
            Text(header, "prev_hash") ?? "",
            (uint)(Number(header, "nonce") ?? 0),
            Number(header, "num_txes") ?? 0,
            Number(header, "reward") ?? 0);
    }

    /// <summary>
    /// Full transaction blobs by id, in the order asked for. A daemon that does not
    /// have one says so, and that is an error here rather than a silent gap.
    /// </summary>
    public async Task<byte[][]> GetTransactionsAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0) return [];

        using MemoryStream body = new();

        using (Utf8JsonWriter writer = new(body))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("txs_hashes");
            foreach (string id in ids) writer.WriteStringValue(id);
            writer.WriteEndArray();
            writer.WriteBoolean("decode_as_json", false);
            writer.WriteEndObject();
        }

        using JsonDocument answer = await PostAsync("get_transactions", body.ToArray(), cancellationToken)
            .ConfigureAwait(false);

        if (answer.RootElement.TryGetProperty("missed_tx", out JsonElement missed) &&
            missed.ValueKind == JsonValueKind.Array &&
            missed.GetArrayLength() > 0)
        {
            throw new DaemonException($"daemon does not have {missed.GetArrayLength()} of the requested transactions");
        }

        Dictionary<string, string> byHash = new(StringComparer.OrdinalIgnoreCase);

        if (answer.RootElement.TryGetProperty("txs", out JsonElement transactions) &&
            transactions.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement entry in transactions.EnumerateArray())
            {
                if (Text(entry, "tx_hash") is string hash && Text(entry, "as_hex") is string hex)
                {
                    byHash[hash] = hex;
                }
            }
        }

        byte[][] blobs = new byte[ids.Count][];

        for (int i = 0; i < ids.Count; i++)
        {
            blobs[i] = byHash.TryGetValue(ids[i], out string? hex)
                ? Convert.FromHexString(hex)
                : throw new DaemonException($"no blob for transaction {ids[i]}");
        }

        return blobs;
    }

    private Task<JsonDocument> GetBlockRpcAsync(ulong height, CancellationToken cancellationToken)
    {
        using MemoryStream body = new();

        using (Utf8JsonWriter writer = new(body))
        {
            writer.WriteStartObject();
            writer.WriteString("jsonrpc", "2.0");
            writer.WriteString("id", "0");
            writer.WriteString("method", "get_block");
            writer.WriteStartObject("params");
            writer.WriteNumber("height", height);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return PostAsync("json_rpc", body.ToArray(), cancellationToken);
    }

    /// <summary>The result of a JSON-RPC call, or the refusal it carried instead.</summary>
    private static JsonElement Result(JsonDocument answer, string method)
    {
        if (answer.RootElement.TryGetProperty("error", out JsonElement error))
        {
            throw new DaemonException(
                $"{method} failed: {Text(error, "message") ?? "?"} ({Number(error, "code") ?? 0})");
        }

        return answer.RootElement.TryGetProperty("result", out JsonElement result)
            ? result
            : throw new DaemonException($"{method} returned no result");
    }

    private async Task<JsonDocument> PostAsync(string path, byte[] body, CancellationToken cancellationToken)
    {
        using ByteArrayContent content = new(body);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")
        {
            CharSet = Encoding.UTF8.WebName,
        };

        using HttpResponseMessage response = await http.PostAsync(path, content, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        byte[] answer = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return JsonDocument.Parse(answer);
        }
        catch (JsonException e)
        {
            throw new DaemonException($"{path} answered with something that is not JSON", e);
        }
    }

    private static string? Text(JsonElement parent, string name)
        => parent.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static ulong? Number(JsonElement parent, string name)
        => parent.TryGetProperty(name, out JsonElement value) && value.TryGetUInt64(out ulong number)
            ? number
            : null;

    public void Dispose()
    {
        if (ownsClient) http.Dispose();
    }
}
