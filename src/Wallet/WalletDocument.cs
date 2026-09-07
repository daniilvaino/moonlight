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
    /// <summary>
    /// The shape of this file. Adding a setting bumps it, and a fingerprint is only
    /// compared against a file of the same shape — otherwise every wallet written
    /// before the new field would report itself as tampered with.
    /// </summary>
    /// <remarks>
    /// 3 changes no field. It marks the point where the fingerprint stopped being
    /// hashed over indented text, which carried Environment.NewLine into it and so
    /// differed between Windows and everywhere else. Wallets written as 2 keep their
    /// old hash and are simply not compared, which is what this number is for; the
    /// next save writes them as 3.
    /// </remarks>
    public const int CurrentFormat = 3;

    [JsonPropertyName("format")]
    public int Format { get; init; } = CurrentFormat;

    /// <summary>Shown for a human reading the file; the authority is inside the blob.</summary>
    [JsonPropertyName("network")]
    public string Network { get; init; } = "Mainnet";

    [JsonPropertyName("settings")]
    public WalletSettings Settings { get; init; } = new();

    /// <summary>Keys, scan results and everything else private, under the password.</summary>
    [JsonPropertyName("secret")]
    public string Secret { get; init; } = "";

    /// <summary>
    /// Written by hand rather than serialized.
    /// </summary>
    /// <remarks>
    /// JsonSerializer needs reflection at run time even with a source-generated
    /// context — measured: opening a wallet through the library built with
    /// reflection disabled failed here and nowhere else. The reader and the writer
    /// need none, and a wallet has exactly two documents to read, both with a shape
    /// this file already states.
    /// </remarks>
    public byte[] ToBytes()
    {
        using MemoryStream stream = new();

        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("format", Format);
            writer.WriteString("network", Network);

            writer.WritePropertyName("settings");
            WriteSettings(writer, Settings);

            writer.WriteString("secret", Secret);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    public static WalletDocument Parse(ReadOnlySpan<byte> file)
    {
        JsonDocument parsed;

        try
        {
            parsed = JsonDocument.Parse(file.ToArray());
        }
        catch (JsonException e)
        {
            throw new FormatException("not a moonlight wallet file", e);
        }

        using (parsed)
        {
            JsonElement root = parsed.RootElement;

            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("secret", out JsonElement secret))
            {
                throw new FormatException("not a moonlight wallet file");
            }

            return new WalletDocument
            {
                Format = root.TryGetProperty("format", out JsonElement format) ? format.GetInt32() : 0,
                Network = root.TryGetProperty("network", out JsonElement network) ? network.GetString() ?? "Mainnet" : "Mainnet",
                Settings = root.TryGetProperty("settings", out JsonElement settings) ? ReadSettings(settings) : new WalletSettings(),
                Secret = secret.GetString() ?? "",
            };
        }
    }

    /// <summary>
    /// What the settings hash to. Kept inside the blob so a wallet notices when its
    /// settings were changed by something other than itself — a node quietly
    /// redirected is a privacy attack, not a formatting choice.
    /// </summary>
    public byte[] SettingsFingerprint()
    {
        using MemoryStream stream = new();

        // Compact, and that is the whole point: an indented writer breaks lines with
        // Environment.NewLine before .NET 9, so the same settings hashed to one value
        // on Windows and another everywhere else. A wallet written on one and opened
        // on the other reported that its settings had been changed by somebody —
        // which is the one thing this is meant to mean.
        using (Utf8JsonWriter writer = new(stream)) WriteSettings(writer, Settings);

        return Crypto.Keccak.Hash(stream.ToArray());
    }

    private static void WriteSettings(Utf8JsonWriter writer, WalletSettings settings)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("nodes");
        writer.WriteStartArray();
        foreach (string node in settings.Nodes) writer.WriteStringValue(node);
        writer.WriteEndArray();

        // Absent rather than null, as the serializer wrote them: a setting nobody
        // has chosen should not appear at all.
        if (settings.NodeSource is not null) writer.WriteString("node_source", settings.NodeSource);
        if (settings.RateSource is not null) writer.WriteString("rate_source", settings.RateSource);

        writer.WriteNumber("lookahead_accounts", settings.LookaheadAccounts);
        writer.WriteNumber("lookahead_addresses", settings.LookaheadAddresses);

        writer.WriteEndObject();
    }

    /// <summary>
    /// Anything unknown is left alone. Old code reading a newer wallet is the
    /// ordinary case once two versions exist.
    /// </summary>
    private static WalletSettings ReadSettings(JsonElement settings)
    {
        if (settings.ValueKind != JsonValueKind.Object) return new WalletSettings();

        WalletSettings defaults = new();

        return new WalletSettings
        {
            Nodes = settings.TryGetProperty("nodes", out JsonElement nodes) && nodes.ValueKind == JsonValueKind.Array
                ? [.. nodes.EnumerateArray().Select(n => n.GetString()).OfType<string>()]
                : [],
            NodeSource = Text(settings, "node_source"),
            RateSource = Text(settings, "rate_source"),
            LookaheadAccounts = Number(settings, "lookahead_accounts", defaults.LookaheadAccounts),
            LookaheadAddresses = Number(settings, "lookahead_addresses", defaults.LookaheadAddresses),
        };
    }

    private static string? Text(JsonElement parent, string name)
        => parent.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static uint Number(JsonElement parent, string name, uint fallback)
        => parent.TryGetProperty(name, out JsonElement value) && value.TryGetUInt32(out uint number)
            ? number
            : fallback;
}
