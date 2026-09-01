using System.Text.Json;
using System.Text.Json.Serialization;

namespace Moonlight.Wallet;

/// <summary>
/// The settings a wallet keeps in the open: where to get nodes and prices, and how
/// far ahead to watch. None of it says anything about money.
/// </summary>
public sealed record WalletSettings
{
    /// <summary>Nodes to try, in order. The first that answers is used.</summary>
    [JsonPropertyName("nodes")]
    public string[] Nodes { get; init; } = [];

    /// <summary>Where to fetch a longer node list from, when the ones above fail.</summary>
    [JsonPropertyName("node_source")]
    public string? NodeSource { get; init; }

    /// <summary>Where a price comes from, for wallets that want one shown.</summary>
    [JsonPropertyName("rate_source")]
    public string? RateSource { get; init; }

    [JsonPropertyName("lookahead_accounts")]
    public uint LookaheadAccounts { get; init; } = 50;

    [JsonPropertyName("lookahead_addresses")]
    public uint LookaheadAddresses { get; init; } = 200;

    [JsonIgnore]
    public SubaddressIndex Lookahead => new(LookaheadAccounts, LookaheadAddresses);
}

/// <summary>
/// The wallet file: readable settings, and one opaque blob holding everything that
/// would tell a reader about money.
/// </summary>
/// <remarks>
/// The split is deliberate. Keys are the obvious secret, but the outputs a scan
/// found are just as revealing — they are the balance, the history, and the key
/// images that identify this wallet's spends on the chain. Those stay inside.
/// </remarks>
public sealed record WalletDocument
{
    [JsonPropertyName("format")]
    public int Format { get; init; } = 2;

    /// <summary>Shown for a human reading the file; the authority is inside the blob.</summary>
    [JsonPropertyName("network")]
    public string Network { get; init; } = "Mainnet";

    [JsonPropertyName("settings")]
    public WalletSettings Settings { get; init; } = new();

    /// <summary>Keys, scan results and everything else private, under the password.</summary>
    [JsonPropertyName("secret")]
    public string Secret { get; init; } = "";

    public byte[] ToBytes() => JsonSerializer.SerializeToUtf8Bytes(this, WalletJson.Default.WalletDocument);

    public static WalletDocument Parse(ReadOnlySpan<byte> file)
        => JsonSerializer.Deserialize(file, WalletJson.Default.WalletDocument)
           ?? throw new FormatException("not a moonlight wallet file");

    /// <summary>
    /// What the settings hash to. Kept inside the blob so a wallet notices when its
    /// settings were changed by something other than itself — a node quietly
    /// redirected is a privacy attack, not a formatting choice.
    /// </summary>
    public byte[] SettingsFingerprint()
        => Crypto.Keccak.Hash(JsonSerializer.SerializeToUtf8Bytes(Settings, WalletJson.Default.WalletSettings));
}

[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WalletDocument))]
[JsonSerializable(typeof(WalletSettings))]
internal sealed partial class WalletJson : JsonSerializerContext;
