using System.Text.Json;
using Moonlight.Crypto;
using Moonlight.Wallet;
using Xunit;

namespace Moonlight.Wallet.Tests;

/// <summary>
/// The file's two halves: settings anyone can read and edit, and one blob nobody
/// can read without the password.
/// </summary>
public class WalletDocumentTests
{
    private const int Fast = 1000;

    private static byte[] Write(Account account, WalletDocument document, WalletSnapshot? snapshot = null)
        => (document with
        {
            Secret = Convert.ToBase64String(Storage.Encrypt(
                account, Storage.Seal("p", Fast), snapshot?.ScannedHeight ?? 0,
                document.Settings.Lookahead, snapshot, null, document.SettingsFingerprint())),
        }).ToBytes();

    [Fact]
    public void SettingsAreReadableWithoutThePassword()
    {
        WalletDocument document = new()
        {
            Settings = new WalletSettings
            {
                Nodes = ["http://one:18081/", "http://two:18081/"],
                RateSource = "https://prices.example/xmr",
                LookaheadAccounts = 3,
                LookaheadAddresses = 20,
            },
        };

        byte[] file = Write(Account.Create(), document);

        using JsonDocument plain = JsonDocument.Parse(file);
        JsonElement settings = plain.RootElement.GetProperty("settings");

        Assert.Equal(2, plain.RootElement.GetProperty("format").GetInt32());
        Assert.Equal(2, settings.GetProperty("nodes").GetArrayLength());
        Assert.Equal("https://prices.example/xmr", settings.GetProperty("rate_source").GetString());
        Assert.Equal(3, settings.GetProperty("lookahead_accounts").GetInt32());
    }

    /// <summary>
    /// The scan results are not settings. An output says what this wallet holds and
    /// its key image identifies its spends on the chain, so none of it appears
    /// outside the blob.
    /// </summary>
    [Fact]
    public void NothingAboutMoneyIsReadable()
    {
        Account account = Account.Create();
        Point image = Point.FromSecret(Scalar.Random());

        OwnedOutput output = new(
            100, new byte[32], 0, Point.FromSecret(Scalar.Random()), 123_456_789,
            Scalar.Random(), new SubaddressIndex(0, 0), image);

        WalletSnapshot snapshot = new(101, [output], new Dictionary<string, ulong>());
        byte[] file = Write(account, new WalletDocument(), snapshot);

        string text = System.Text.Encoding.UTF8.GetString(file);

        Assert.DoesNotContain("123456789", text, StringComparison.Ordinal);
        Assert.DoesNotContain(account.Address.Encode(), text, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToHexString(image.ToBytes()), text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(account.Seed.Split(' ')[0] + " " + account.Seed.Split(' ')[1], text, StringComparison.Ordinal);
    }

    [Fact]
    public void RoundTripsThroughTheDocument()
    {
        Account account = Account.Create();
        WalletDocument document = new() { Settings = new WalletSettings { Nodes = ["http://node:18081/"] } };

        WalletFile opened = Storage.OpenDocument(Write(account, document), "p");

        Assert.Equal(account.Address, opened.Account.Address);
        Assert.Equal(["http://node:18081/"], opened.Document.Settings.Nodes);
        Assert.False(opened.SettingsChangedOutside);
    }

    /// <summary>
    /// Editing settings by hand is allowed — that is the point of keeping them
    /// readable — and the wallet notices, because a node changed by somebody else
    /// is a privacy attack rather than a formatting choice.
    /// </summary>
    [Fact]
    public void SettingsChangedByHandAreNoticed()
    {
        Account account = Account.Create();
        byte[] file = Write(account, new WalletDocument { Settings = new WalletSettings { Nodes = ["http://mine:18081/"] } });

        WalletDocument tampered = WalletDocument.Parse(file) with
        {
            Settings = new WalletSettings { Nodes = ["http://theirs:18081/"] },
        };

        WalletFile opened = Storage.OpenDocument(tampered.ToBytes(), "p");

        Assert.True(opened.SettingsChangedOutside);

        // Still opens: the keys are untouched, and refusing would strand the owner.
        Assert.Equal(account.Address, opened.Account.Address);
        Assert.Equal(["http://theirs:18081/"], opened.Document.Settings.Nodes);
    }

    [Fact]
    public void RefusesSomethingThatIsNotAWalletDocument()
    {
        Assert.ThrowsAny<Exception>(() => Storage.OpenDocument("not json"u8, "p"));
        Assert.ThrowsAny<Exception>(() => Storage.OpenDocument("{}"u8, "p"));
    }
}
