using System.Text;
using Moonlight.Crypto;
using Moonlight.Wallet;
using Xunit;

namespace Moonlight.Wallet.Tests;

/// <summary>
/// The settings fingerprint, pinned to actual bytes.
/// </summary>
/// <remarks>
/// It is a hash of the settings as text, so anything that changes how that text is
/// written changes it — and a changed fingerprint tells a wallet its settings were
/// altered by somebody else, which is the one thing this is meant to mean.
///
/// It has been wrong twice. Replacing the serializer with a hand-written writer
/// changed the bytes, and three wallets written the day before started reporting
/// themselves as tampered with. The fix was to keep the indentation the serializer
/// had used — and that turned out to be the second bug rather than a fix, because
/// an indented Utf8JsonWriter breaks lines with Environment.NewLine before .NET 9.
/// The hash was one value on Windows and another everywhere else, so a wallet
/// carried between them accused whoever carried it. CI on Linux and macOS is what
/// found it; every run on Windows had been green.
/// </remarks>
public class SettingsFingerprintTests
{
    private const string Recorded = "F8CD23BE1AF4F5412183D7D383782AFB49E441F8A792CBD087702370614ED84B";

    private static WalletDocument Sample => new()
    {
        Settings = new WalletSettings
        {
            Nodes = ["http://127.0.0.1:28081/"],
            LookaheadAccounts = 1,
            LookaheadAddresses = 3,
        },
    };

    [Fact]
    public void IsTheSameNumberEverywhere()
        => Assert.Equal(Recorded, Convert.ToHexString(Sample.SettingsFingerprint()));

    /// <summary>
    /// The property behind that number, and the one a single machine cannot check by
    /// running the test: the hashed text carries no line break at all.
    /// </summary>
    /// <remarks>
    /// It used to. Before .NET 9 an indented Utf8JsonWriter breaks lines with
    /// Environment.NewLine, so the same settings hashed to one value on Windows and
    /// another everywhere else — and a wallet carried between them reported that its
    /// settings had been changed by somebody, which is the one thing this is meant to
    /// mean. Caught by CI on Linux and macOS while three machines' worth of Windows
    /// runs said everything was fine.
    /// </remarks>
    [Fact]
    public void HashesCompactTextWithNoLineBreakInIt()
    {
        // Spelled out here rather than taken from the code, so the test says what
        // the shape is instead of agreeing with whatever the writer happens to do.
        // There is no newline in it, which is the property that makes the number
        // above the same on every machine.
        const string Compact =
            """{"nodes":["http://127.0.0.1:28081/"],"lookahead_accounts":1,"lookahead_addresses":3}""";

        Assert.Equal(
            Keccak.Hash(Encoding.UTF8.GetBytes(Compact)),
            Sample.SettingsFingerprint());
    }

    /// <summary>Absent, not null: a setting nobody has chosen does not appear at all.</summary>
    [Fact]
    public void UnsetSourcesAreLeftOut()
    {
        WalletDocument document = new() { Settings = new WalletSettings { Nodes = [] } };

        string text = Encoding.UTF8.GetString(document.ToBytes());

        Assert.DoesNotContain("node_source", text, StringComparison.Ordinal);
        Assert.DoesNotContain("rate_source", text, StringComparison.Ordinal);
        Assert.DoesNotContain("null", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ChangingASettingChangesTheFingerprint()
    {
        WalletDocument mine = new() { Settings = new WalletSettings { Nodes = ["http://mine:18081/"] } };
        WalletDocument theirs = mine with { Settings = new WalletSettings { Nodes = ["http://theirs:18081/"] } };

        Assert.NotEqual(mine.SettingsFingerprint(), theirs.SettingsFingerprint());
    }

    /// <summary>The document round-trips through its own reader, unknown fields and all.</summary>
    [Fact]
    public void ReadsBackWhatItWrote()
    {
        WalletDocument document = new()
        {
            Network = "Stagenet",
            Settings = new WalletSettings
            {
                Nodes = ["http://one:18081/", "http://two:18081/"],
                NodeSource = "https://nodes.example/list",
                RateSource = "https://prices.example/xmr",
                LookaheadAccounts = 7,
                LookaheadAddresses = 70,
            },
            Secret = "c2VjcmV0",
        };

        WalletDocument again = WalletDocument.Parse(document.ToBytes());

        Assert.Equal(document.Format, again.Format);
        Assert.Equal("Stagenet", again.Network);
        Assert.Equal(document.Settings.Nodes, again.Settings.Nodes);
        Assert.Equal("https://nodes.example/list", again.Settings.NodeSource);
        Assert.Equal("https://prices.example/xmr", again.Settings.RateSource);
        Assert.Equal(7u, again.Settings.LookaheadAccounts);
        Assert.Equal(70u, again.Settings.LookaheadAddresses);
        Assert.Equal("c2VjcmV0", again.Secret);
        Assert.Equal(document.SettingsFingerprint(), again.SettingsFingerprint());
    }

    [Fact]
    public void RefusesSomethingThatIsNotADocument()
    {
        Assert.Throws<FormatException>(() => WalletDocument.Parse("not json"u8));
        Assert.Throws<FormatException>(() => WalletDocument.Parse("{}"u8));
        Assert.Throws<FormatException>(() => WalletDocument.Parse("[1,2,3]"u8));
    }
}
