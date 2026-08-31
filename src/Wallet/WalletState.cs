using Moonlight.Crypto;
using Moonlight.Serialization;

namespace Moonlight.Wallet;

/// <summary>What a wallet holds, and how much of it can be spent right now.</summary>
public readonly record struct Balance(ulong Total, ulong Unlocked)
{
    public ulong Locked => Total - Unlocked;
}

/// <summary>
/// The wallet's own view of the chain: which outputs are ours, which of them are
/// already spent, and how far we have scanned.
/// </summary>
/// <remarks>
/// Spends are recognised by key image. An input naming an image we produced is
/// our output being spent — by us, or by anyone who has our keys. That is the
/// only signal there is; the chain says nothing else about whose money moved.
/// </remarks>
public sealed class WalletState
{
    /// <summary>A coinbase output is locked for sixty blocks.</summary>
    public const ulong CoinbaseLock = 60;

    /// <summary>Everything else needs ten confirmations.</summary>
    public const ulong SpendableAge = 10;

    /// <summary>Above this, an unlock time is a timestamp rather than a height.</summary>
    private const ulong MaxBlockNumber = 500_000_000;

    private readonly Scanner scanner;
    private readonly Dictionary<string, OwnedOutput> outputs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ulong> spentAt = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (bool IsCoinbase, ulong UnlockTime)> conditions = new(StringComparer.Ordinal);

    public WalletState(Scanner scanner, ulong restoreHeight = 0)
    {
        ArgumentNullException.ThrowIfNull(scanner);

        this.scanner = scanner;
        RestoreHeight = restoreHeight;
        ScannedHeight = restoreHeight;
    }

    public ulong RestoreHeight { get; }

    /// <summary>The height after the last block processed.</summary>
    public ulong ScannedHeight { get; private set; }

    public IEnumerable<OwnedOutput> Outputs => outputs.Values;

    public IEnumerable<OwnedOutput> Unspent => outputs.Values.Where(o => !IsSpent(o));

    /// <summary>
    /// Processes one block's transactions, the miner's first. Blocks must arrive in
    /// order: a gap is silently missing money, so it is refused.
    /// </summary>
    public void Process(ulong height, IReadOnlyList<Transaction> transactions)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        if (height < ScannedHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(height), $"block {height} arrived after {ScannedHeight}");
        }

        foreach (Transaction transaction in transactions)
        {
            RecordSpends(transaction, height);

            foreach (OwnedOutput output in scanner.Scan(transaction, height))
            {
                string key = output.Key.ToString();

                outputs[key] = output;
                conditions[key] = (transaction.IsCoinbase, transaction.UnlockTime);
            }
        }

        ScannedHeight = height + 1;
    }

    public bool IsSpent(OwnedOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        return output.KeyImage is { } image && spentAt.ContainsKey(image.ToString());
    }

    /// <summary>
    /// The balance as of a height. Unlocked money is what could be spent in the
    /// next block; a view-only wallet cannot see spends at all, so its total is an
    /// upper bound rather than a balance.
    /// </summary>
    public Balance BalanceAt(ulong height, DateTimeOffset? now = null)
    {
        ulong total = 0;
        ulong unlocked = 0;

        foreach (OwnedOutput output in Unspent)
        {
            total += output.Amount;

            if (IsUnlocked(output, height, now ?? DateTimeOffset.UtcNow))
            {
                unlocked += output.Amount;
            }
        }

        return new Balance(total, unlocked);
    }

    public Balance Balance() => BalanceAt(ScannedHeight == 0 ? 0 : ScannedHeight - 1);

    private bool IsUnlocked(OwnedOutput output, ulong height, DateTimeOffset now)
    {
        (bool isCoinbase, ulong unlockTime) = conditions.TryGetValue(output.Key.ToString(), out var found)
            ? found
            : (false, 0);

        ulong age = isCoinbase ? CoinbaseLock : SpendableAge;
        if (height + 1 < output.Height + age) return false;

        if (unlockTime == 0) return true;

        // The same field means two things, split at a height no chain will reach.
        return unlockTime < MaxBlockNumber
            ? height + 1 >= unlockTime
            : (ulong)now.ToUnixTimeSeconds() >= unlockTime;
    }

    private void RecordSpends(Transaction transaction, ulong height)
    {
        foreach (TxIn input in transaction.Inputs)
        {
            if (input is not TxIn.ToKey key) continue;

            string image = Convert.ToHexString(key.KeyImage).ToLowerInvariant();
            spentAt.TryAdd(image, height);
        }
    }
}
