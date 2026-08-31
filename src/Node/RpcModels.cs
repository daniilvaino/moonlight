using System.Text.Json.Serialization;

namespace Moonlight.Node;

internal sealed record JsonRpcRequest<T>(
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("params")] T Params)
{
    // Init-only rather than expression-bodied: the serializer ignores statics, and
    // an analyzer will otherwise insist these become static.
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("id")]
    public string Id { get; init; } = "0";
}

internal sealed record JsonRpcError(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message);

internal sealed record JsonRpcResponse<T>(
    [property: JsonPropertyName("result")] T? Result,
    [property: JsonPropertyName("error")] JsonRpcError? Error);

public sealed record BlockHeaderInfo(
    [property: JsonPropertyName("hash")] string Hash,
    [property: JsonPropertyName("height")] ulong Height,
    [property: JsonPropertyName("major_version")] byte MajorVersion,
    [property: JsonPropertyName("minor_version")] byte MinorVersion,
    [property: JsonPropertyName("timestamp")] ulong Timestamp,
    [property: JsonPropertyName("prev_hash")] string PreviousHash,
    [property: JsonPropertyName("nonce")] uint Nonce,
    [property: JsonPropertyName("num_txes")] ulong TransactionCount,
    [property: JsonPropertyName("reward")] ulong Reward);

internal sealed record GetBlockParams(
    [property: JsonPropertyName("height")] ulong Height);

internal sealed record GetBlockResult(
    [property: JsonPropertyName("blob")] string Blob,
    [property: JsonPropertyName("block_header")] BlockHeaderInfo Header,
    [property: JsonPropertyName("miner_tx_hash")] string MinerTxHash,
    [property: JsonPropertyName("tx_hashes")] string[]? TransactionHashes);

internal sealed record GetHeightResult(
    [property: JsonPropertyName("height")] ulong Height,
    [property: JsonPropertyName("hash")] string? Hash);

internal sealed record GetTransactionsRequest(
    [property: JsonPropertyName("txs_hashes")] string[] Hashes)
{
    [JsonPropertyName("decode_as_json")]
    public bool DecodeAsJson { get; init; }
}

internal sealed record TransactionEntry(
    [property: JsonPropertyName("tx_hash")] string Hash,
    [property: JsonPropertyName("as_hex")] string? AsHex,
    [property: JsonPropertyName("pruned_as_hex")] string? PrunedAsHex,
    [property: JsonPropertyName("prunable_as_hex")] string? PrunableAsHex);

internal sealed record GetTransactionsResult(
    [property: JsonPropertyName("txs")] TransactionEntry[]? Transactions,
    [property: JsonPropertyName("missed_tx")] string[]? Missed,
    [property: JsonPropertyName("status")] string? Status);

/// <summary>The daemon answered, and the answer was a refusal.</summary>
public sealed class DaemonException(string message) : Exception(message);
