using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Moonlight.Serialization;

namespace Moonlight.Node;

/// <summary>
/// monerod's JSON-RPC. Enough of it to follow the chain and pull real blocks;
/// the binary endpoints are a separate matter.
/// </summary>
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
        GetHeightResult result = await PostAsync("get_height", DaemonJson.Default.GetHeightResult, cancellationToken)
            .ConfigureAwait(false);

        return result.Height;
    }

    /// <summary>The block at a height, already parsed from the blob the daemon sends.</summary>
    public async Task<Block> GetBlockAsync(ulong height, CancellationToken cancellationToken = default)
    {
        GetBlockResult result = await JsonRpcAsync(
            "get_block",
            new GetBlockParams(height),
            DaemonJson.Default.JsonRpcRequestGetBlockParams,
            DaemonJson.Default.JsonRpcResponseGetBlockResult,
            cancellationToken).ConfigureAwait(false);

        return BlockParser.Parse(Convert.FromHexString(result.Blob));
    }

    public async Task<BlockHeaderInfo> GetBlockHeaderAsync(ulong height, CancellationToken cancellationToken = default)
    {
        GetBlockResult result = await JsonRpcAsync(
            "get_block",
            new GetBlockParams(height),
            DaemonJson.Default.JsonRpcRequestGetBlockParams,
            DaemonJson.Default.JsonRpcResponseGetBlockResult,
            cancellationToken).ConfigureAwait(false);

        return result.Header;
    }

    /// <summary>
    /// Full transaction blobs by id, in the order asked for. A daemon that does not
    /// have one says so, and that is an error here rather than a silent gap.
    /// </summary>
    public async Task<byte[][]> GetTransactionsAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return [];
        }

        using HttpResponseMessage response = await http.PostAsJsonAsync(
            "get_transactions",
            new GetTransactionsRequest([.. ids]),
            DaemonJson.Default.GetTransactionsRequest,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        GetTransactionsResult result = await response.Content
            .ReadFromJsonAsync(DaemonJson.Default.GetTransactionsResult, cancellationToken)
            .ConfigureAwait(false) ?? throw new DaemonException("get_transactions returned no body");

        if (result.Missed is { Length: > 0 })
        {
            throw new DaemonException($"daemon does not have {result.Missed.Length} of the requested transactions");
        }

        TransactionEntry[] entries = result.Transactions ?? [];
        Dictionary<string, TransactionEntry> byHash = entries.ToDictionary(e => e.Hash, StringComparer.OrdinalIgnoreCase);

        byte[][] blobs = new byte[ids.Count][];
        for (int i = 0; i < ids.Count; i++)
        {
            if (!byHash.TryGetValue(ids[i], out TransactionEntry? entry) || entry.AsHex is null)
            {
                throw new DaemonException($"no blob for transaction {ids[i]}");
            }

            blobs[i] = Convert.FromHexString(entry.AsHex);
        }

        return blobs;
    }

    private async Task<TResult> JsonRpcAsync<TParams, TResult>(
        string method,
        TParams parameters,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<JsonRpcRequest<TParams>> requestType,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<JsonRpcResponse<TResult>> responseType,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await http.PostAsJsonAsync(
            "json_rpc",
            new JsonRpcRequest<TParams>(method, parameters),
            requestType,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        JsonRpcResponse<TResult> body = await response.Content
            .ReadFromJsonAsync(responseType, cancellationToken)
            .ConfigureAwait(false) ?? throw new DaemonException($"{method} returned no body");

        if (body.Error is not null)
        {
            throw new DaemonException($"{method} failed: {body.Error.Message} ({body.Error.Code})");
        }

        return body.Result ?? throw new DaemonException($"{method} returned no result");
    }

    /// <summary>The non-RPC endpoints, which take no parameters and answer directly.</summary>
    private async Task<TResult> PostAsync<TResult>(
        string path,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TResult> resultType,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await http
            .PostAsync(path, new StringContent("{}", System.Text.Encoding.UTF8, "application/json"), cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(resultType, cancellationToken).ConfigureAwait(false)
            ?? throw new DaemonException($"{path} returned no body");
    }

    public void Dispose()
    {
        if (ownsClient) http.Dispose();
    }
}
