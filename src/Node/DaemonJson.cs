using System.Text.Json;
using System.Text.Json.Serialization;

namespace Moonlight.Node;

/// <summary>
/// Source-generated serializers. Reflection-based JSON does not survive trimming
/// or NativeAOT, and the whole point of this tree is that it does.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(JsonRpcRequest<GetBlockParams>))]
[JsonSerializable(typeof(JsonRpcRequest<object?>))]
[JsonSerializable(typeof(JsonRpcResponse<GetBlockResult>))]
[JsonSerializable(typeof(JsonRpcResponse<BlockHeaderInfo>))]
[JsonSerializable(typeof(GetHeightResult))]
[JsonSerializable(typeof(GetTransactionsRequest))]
[JsonSerializable(typeof(GetTransactionsResult))]
internal sealed partial class DaemonJson : JsonSerializerContext;
