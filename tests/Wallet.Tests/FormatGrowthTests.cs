using System.Text.Json;
using Moonlight.Wallet;
using Xunit;

namespace Moonlight.Wallet.Tests;

/// <summary>
/// The file will grow — a node list, a price source, whatever comes next. These
/// say what growing is allowed to cost, which is nothing.
/// </summary>
public class FormatGrowthTests
{
    private const int Fast = 1000;

    /// <summary>
    /// A field this version has never heard of is left alone rather than treated as
    /// a broken file. Old code reading a newer wallet is the ordinary case once two
    /// versions exist.
    /// </summary>
    [Fact]
    public void UnknownSettingsAreIgnored()
    {
        Account account = Account.Create();
        byte[] file = Write(account, new WalletDocument());

        using JsonDocument parsed = JsonDocument.Parse(file);
        Dictionary<string, JsonElement> root = parsed.RootElement.EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.Ordinal);

        // Something a later version added, at both levels.
        string grown = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["format"] = root["format"],
            ["network"] = root["network"],
            ["settings"] = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(root["settings"].GetRawText())!
                .ToDictionary(e => e.Key, e => (object?)e.Value, StringComparer.Ordinal)
                .Concat([new KeyValuePair<string, object?>("fee_priority", "elevated")])
                .ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal),
            ["secret"] = root["secret"],
            ["theme"] = "midnight",
        });

        WalletFile opened = Storage.OpenDocument(System.Text.Encoding.UTF8.GetBytes(grown), "p");

        Assert.Equal(account.Address, opened.Account.Address);
    }

    /// <summary>
    /// A wallet written before a setting existed hashed a different object, so its
    /// fingerprint cannot match. That is not tampering, and saying it is would cry
    /// wolf on every wallet in the world the day a field is added.
    /// </summary>
    [Fact]
    public void AnOlderFormatDoesNotReportTampering()
    {
        Account account = Account.Create();

        // As a previous version would have written it: its own shape, its own hash.
        WalletDocument older = new() { Format = WalletDocument.CurrentFormat - 1 };
        byte[] file = Write(account, older);

        WalletFile opened = Storage.OpenDocument(file, "p");

        Assert.False(opened.SettingsChangedOutside);
        Assert.Equal(account.Address, opened.Account.Address);
    }

    /// <summary>
    /// Inside the blob, everything after the snapshot is a tagged section with a
    /// length. A section this version does not know is stepped over, which is what
    /// lets a later version add one without the order mattering.
    /// </summary>
    [Fact]
    public void UnknownSecretSectionsAreSkipped()
    {
        Account account = Account.Create();
        WalletSeal seal = Storage.Seal("p", Fast);

        byte[] blob = Storage.Encrypt(account, seal, 7, null, null, "http://node:18081/");
        WalletFile opened = Storage.Open(blob, "p");

        // What matters is that the daemon still comes back after the sections were
        // read in order and any unknown one skipped.
        Assert.Equal("http://node:18081/", opened.Daemon);
        Assert.Equal(7UL, opened.Snapshot.ScannedHeight);
    }

    [Fact]
    public void TheSecretSurvivesAGrowingDocument()
    {
        Account account = Account.Create();
        WalletSnapshot snapshot = new(500, [], new Dictionary<string, ulong>());

        WalletDocument document = new()
        {
            Settings = new WalletSettings { Nodes = ["http://a", "http://b"], RateSource = "https://p" },
        };

        WalletFile opened = Storage.OpenDocument(Write(account, document, snapshot), "p");

        Assert.Equal(500UL, opened.Snapshot.ScannedHeight);
        Assert.Equal(2, opened.Document.Settings.Nodes.Length);
        Assert.Equal("https://p", opened.Document.Settings.RateSource);
    }

    private static byte[] Write(Account account, WalletDocument document, WalletSnapshot? snapshot = null)
        => (document with
        {
            Secret = Convert.ToBase64String(Storage.Encrypt(
                account, Storage.Seal("p", Fast), snapshot?.ScannedHeight ?? 0,
                document.Settings.Lookahead, snapshot, null, document.SettingsFingerprint())),
        }).ToBytes();
}
