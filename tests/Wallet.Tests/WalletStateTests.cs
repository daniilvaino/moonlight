using Moonlight.Crypto;
using Moonlight.Serialization;
using Moonlight.Wallet;
using Xunit;

namespace Moonlight.Wallet.Tests;

public class WalletStateTests
{
    private const ulong Xmr = 1_000_000_000_000;

    private static (Account Account, WalletState State) NewWallet(ulong restoreHeight = 0)
    {
        Account account = Account.Create();
        return (account, new WalletState(new Scanner(account), restoreHeight));
    }

    [Fact]
    public void CreditsAPaymentAndLocksItForTenBlocks()
    {
        (Account account, WalletState state) = NewWallet();

        state.Process(100, [FakeSender.Pay(account.Address, [2 * Xmr])]);

        Assert.Equal(2 * Xmr, state.BalanceAt(100).Total);
        Assert.Equal(0UL, state.BalanceAt(100).Unlocked);
        Assert.Equal(0UL, state.BalanceAt(108).Unlocked);
        Assert.Equal(2 * Xmr, state.BalanceAt(109).Unlocked);
    }

    /// <summary>
    /// A coinbase is locked for sixty blocks rather than ten. Treating it as an
    /// ordinary output would offer the owner money the network will not let them
    /// spend for another fifty blocks.
    /// </summary>
    [Fact]
    public void ACoinbaseIsLockedForSixtyBlocks()
    {
        (Account account, WalletState state) = NewWallet();

        state.Process(100, [FakeSender.Pay(account.Address, [5 * Xmr], coinbase: true, height: 100)]);

        Assert.Equal(5 * Xmr, state.BalanceAt(158).Total);
        Assert.Equal(0UL, state.BalanceAt(158).Unlocked);
        Assert.Equal(5 * Xmr, state.BalanceAt(159).Unlocked);
    }

    [Fact]
    public void AnExplicitUnlockHeightIsHonoured()
    {
        (Account account, WalletState state) = NewWallet();

        state.Process(100, [FakeSender.Pay(account.Address, [Xmr], unlockTime: 500)]);

        Assert.Equal(0UL, state.BalanceAt(400).Unlocked);
        Assert.Equal(Xmr, state.BalanceAt(499).Unlocked);
    }

    /// <summary>
    /// The same field is a height below 500 000 000 and a timestamp above it. The
    /// split is what lets one number mean two things.
    /// </summary>
    [Fact]
    public void AnExplicitUnlockTimestampIsHonoured()
    {
        (Account account, WalletState state) = NewWallet();
        DateTimeOffset unlockAt = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

        state.Process(100, [FakeSender.Pay(account.Address, [Xmr], unlockTime: (ulong)unlockAt.ToUnixTimeSeconds())]);

        Assert.Equal(0UL, state.BalanceAt(1000, unlockAt.AddDays(-1)).Unlocked);
        Assert.Equal(Xmr, state.BalanceAt(1000, unlockAt).Unlocked);
    }

    /// <summary>
    /// A spend is recognised only by its key image. Nothing else on the chain says
    /// whose output moved.
    /// </summary>
    [Fact]
    public void AKeyImageMarksAnOutputSpent()
    {
        (Account account, WalletState state) = NewWallet();

        state.Process(100, [FakeSender.Pay(account.Address, [3 * Xmr])]);
        OwnedOutput output = state.Outputs.Single();

        Assert.False(state.IsSpent(output));
        Assert.Equal(3 * Xmr, state.BalanceAt(200).Total);

        state.Process(150, [FakeSender.Pay(Account.Create().Address, [Xmr], spending: [output.KeyImage!.Value])]);

        Assert.True(state.IsSpent(output));
        Assert.Equal(0UL, state.BalanceAt(200).Total);
        Assert.Empty(state.Unspent);
    }

    /// <summary>
    /// Somebody else's key image must not touch our balance, or a wallet would
    /// zero itself every time an unrelated transaction went by.
    /// </summary>
    [Fact]
    public void AStrangersKeyImageChangesNothing()
    {
        (Account account, WalletState state) = NewWallet();

        state.Process(100, [FakeSender.Pay(account.Address, [Xmr])]);
        state.Process(101, [FakeSender.Pay(Account.Create().Address, [Xmr])]);

        Assert.Equal(Xmr, state.BalanceAt(200).Total);
    }

    /// <summary>
    /// A view-only wallet cannot compute key images, so it cannot see its own
    /// spends. Its total is an upper bound, and saying so is better than quietly
    /// reporting a balance that is too high.
    /// </summary>
    [Fact]
    public void AViewOnlyWalletCannotSeeSpends()
    {
        Account account = Account.Create();
        WalletState full = new(new Scanner(account));
        WalletState viewOnly = new(new Scanner(account.AsViewOnly()));

        Transaction payment = FakeSender.Pay(account.Address, [Xmr]);
        full.Process(100, [payment]);
        viewOnly.Process(100, [payment]);

        Point image = full.Outputs.Single().KeyImage!.Value;
        Transaction spend = FakeSender.Pay(Account.Create().Address, [Xmr], spending: [image]);

        full.Process(150, [spend]);
        viewOnly.Process(150, [spend]);

        Assert.Equal(0UL, full.BalanceAt(200).Total);
        Assert.Equal(Xmr, viewOnly.BalanceAt(200).Total);
    }

    [Fact]
    public void SeveralOutputsInOneTransactionAllCount()
    {
        (Account account, WalletState state) = NewWallet();

        state.Process(100, [FakeSender.Pay(account.Address, [Xmr, 2 * Xmr, 3 * Xmr])]);

        Assert.Equal(3, state.Outputs.Count());
        Assert.Equal(6 * Xmr, state.BalanceAt(200).Total);
    }

    /// <summary>
    /// Blocks out of order mean a gap, and a gap is money the owner never hears
    /// about. Refusing is the only safe answer.
    /// </summary>
    [Fact]
    public void BlocksMustArriveInOrder()
    {
        (Account _, WalletState state) = NewWallet();

        state.Process(100, []);

        Assert.Equal(101UL, state.ScannedHeight);
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Process(99, []));
    }

    [Fact]
    public void ScanningStartsAtTheRestoreHeight()
    {
        (Account _, WalletState state) = NewWallet(restoreHeight: 2_000_000);

        Assert.Equal(2_000_000UL, state.ScannedHeight);
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Process(1_999_999, []));
    }

    [Fact]
    public void SubaddressPaymentsAreCredited()
    {
        Account account = Account.Create();
        WalletState state = new(new Scanner(account));

        Address subaddress = Subaddress.Address(account, new SubaddressIndex(0, 7));
        state.Process(100, [FakeSender.Pay(subaddress, [4 * Xmr])]);

        OwnedOutput output = state.Outputs.Single();

        Assert.Equal(new SubaddressIndex(0, 7), output.Subaddress);
        Assert.Equal(4 * Xmr, output.Amount);
    }
}
