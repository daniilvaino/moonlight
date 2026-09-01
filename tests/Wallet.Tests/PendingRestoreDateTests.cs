using Moonlight.Wallet;
using Xunit;

namespace Moonlight.Wallet.Tests;

/// <summary>
/// A wallet restored with no daemon to hand starts from an offline estimate that
/// lands weeks early on purpose. The date is kept so the first daemon it meets can
/// replace that guess — and the rules about when it may not are the whole point.
/// </summary>
public class PendingRestoreDateTests
{
    private const int Fast = 1000;

    private static readonly DateTimeOffset Date = new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TheDateSurvivesTheFile()
    {
        Account account = Account.Create();

        byte[] blob = Storage.Encrypt(account, Storage.Seal("p", Fast), 3_146_248,
            null, null, null, default, Date);

        Assert.Equal(Date, Storage.Open(blob, "p").PendingRestoreDate);
    }

    /// <summary>A wallet whose height is already exact carries no date to resolve.</summary>
    [Fact]
    public void AWalletWithNothingPendingSaysSo()
    {
        byte[] blob = Storage.Encrypt(Account.Create(), Storage.Seal("p", Fast), 100);

        Assert.Null(Storage.Open(blob, "p").PendingRestoreDate);
    }

    /// <summary>
    /// The date is inside the encrypted blob, not the readable settings. When a
    /// wallet was made narrows down whose it might be.
    /// </summary>
    [Fact]
    public void TheDateIsNotReadableWithoutThePassword()
    {
        Account account = Account.Create();
        WalletDocument document = new();

        byte[] file = (document with
        {
            Secret = Convert.ToBase64String(Storage.Encrypt(
                account, Storage.Seal("p", Fast), 3_146_248, null, null, null,
                document.SettingsFingerprint(), Date)),
        }).ToBytes();

        string text = System.Text.Encoding.UTF8.GetString(file);

        Assert.DoesNotContain("2024", text, StringComparison.Ordinal);
        Assert.DoesNotContain(Date.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            text, StringComparison.Ordinal);
        Assert.Equal(Date, Storage.OpenDocument(file, "p").PendingRestoreDate);
    }

    /// <summary>
    /// The guard that matters. Once an output has been found, the start cannot move:
    /// that output was found below the new height, and moving there would leave the
    /// wallet holding money from a range it now claims never to have read.
    /// </summary>
    [Fact]
    public async Task AWalletThatFoundMoneyIsNotMoved()
    {
        WalletState state = Scanned();

        // The daemon address is unroutable on purpose: refusing happens before the
        // question is asked, so a wallet with money is never moved by any answer.
        using Moonlight.Node.DaemonClient unreachable = new(new Uri("http://node.invalid:18081/"));

        Assert.Null(await RestoreHeight.RefineAsync(unreachable, Date, state));
        Assert.Equal(101UL, state.ScannedHeight);
    }

    private static WalletState Scanned()
    {
        Account account = Account.Create();
        WalletState state = new(new Scanner(account, new SubaddressIndex(1, 1)), 100);

        state.Restore(new WalletSnapshot(101, [Output(account)], new Dictionary<string, ulong>()));

        Assert.NotEmpty(state.Outputs);
        return state;
    }

    private static OwnedOutput Output(Account account)
        => new(100, new byte[32], 0, Crypto.Point.FromSecret(Crypto.Scalar.Random()), 1,
            Crypto.Scalar.Random(), new SubaddressIndex(0, 0), null);
}
