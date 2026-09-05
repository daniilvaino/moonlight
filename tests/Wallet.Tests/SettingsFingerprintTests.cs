using System.Text;
using Moonlight.Wallet;
using Xunit;

namespace Moonlight.Wallet.Tests;

/// <summary>
/// The settings fingerprint, pinned to actual bytes.
/// </summary>
/// <remarks>
/// It is a hash of the settings as text, so any change to how that text is written
/// changes it — and a changed fingerprint tells every wallet already on disk that
/// its settings were altered by somebody else, which is the one thing this is meant
/// to detect. Replacing the serializer with a hand-written writer did exactly that,
/// and it went unnoticed until three wallets written the day before were opened:
/// the serializer had been configured to indent, and the indentation was in the
/// hash.
/// </remarks>
public class SettingsFingerprintTests
{
    /// <summary>
    /// What these settings hashed to when the source-generated serializer wrote
    /// them. Taken from a wallet file on disk rather than from a guess about
    /// whitespace — the first attempt at this test guessed, and guessed wrong while
    /// the code was right.
    /// </summary>
    private const string Recorded = "03649608B34FC495547AC2D52A9BE57E2BE7D1BD651F388A9BFA4829B9E62E28";

    [Fact]
    public void IsWhatWalletsOnDiskWereWrittenWith()
    {
        WalletDocument document = new()
        {
            Settings = new WalletSettings
            {
                Nodes = ["http://127.0.0.1:28081/"],
                LookaheadAccounts = 1,
                LookaheadAddresses = 3,
            },
        };

        Assert.Equal(Recorded, Convert.ToHexString(document.SettingsFingerprint()));
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
