using Moonlight.Crypto;
using Moonlight.Serialization;

namespace Moonlight.Wallet;

/// <summary>
/// What a scan found, in a form that survives being written to a file. Without
/// this a wallet reads the chain from its restore height on every run.
/// </summary>
public sealed record WalletSnapshot(
    ulong ScannedHeight,
    IReadOnlyList<OwnedOutput> Outputs,
    IReadOnlyDictionary<string, ulong> Spent);

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

    /// <summary>
    /// How many recent block ids to keep. They are the locator a daemon needs to
    /// tell us where we stand after a reorganisation; ten is what wallet2 sends
    /// before it starts spacing them out.
    /// </summary>
    private const int RecentBlocks = 10;

    private readonly Scanner scanner;
    private readonly List<byte[]> recent = [];
    private readonly Dictionary<string, OwnedOutput> outputs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ulong> spentAt = new(StringComparer.Ordinal);

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
    /// <summary>The ids of the last blocks processed, newest first.</summary>
    public IReadOnlyList<byte[]> RecentBlockIds => recent;

    /// <summary>
    /// Moves the starting point without reading anything, for a wallet that begins
    /// at the tip. Only legal before any block has been processed.
    /// </summary>
    public void SkipTo(ulong height)
    {
        if (outputs.Count > 0)
        {
            throw new InvalidOperationException("a wallet that has already scanned cannot skip");
        }

        ScannedHeight = height;
    }

    public void Process(ulong height, IReadOnlyList<Transaction> transactions, byte[]? blockId = null)
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
                outputs[output.Key.ToString()] = output;
            }
        }

        if (blockId is not null)
        {
            recent.Insert(0, blockId);
            if (recent.Count > RecentBlocks) recent.RemoveAt(recent.Count - 1);
        }

        ScannedHeight = height + 1;
    }

    /// <summary>Everything worth keeping between runs: the outputs, and which are spent.</summary>
    public WalletSnapshot Snapshot() => new(ScannedHeight, [.. outputs.Values], spentAt.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal));

    /// <summary>Reloads a snapshot, so a wallet resumes where it left off rather than rescanning the chain.</summary>
    public void Restore(WalletSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        outputs.Clear();
        spentAt.Clear();

        foreach (OwnedOutput output in snapshot.Outputs) outputs[output.Key.ToString()] = output;
        foreach ((string image, ulong height) in snapshot.Spent) spentAt[image] = height;

        ScannedHeight = snapshot.ScannedHeight;
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

    private static bool IsUnlocked(OwnedOutput output, ulong height, DateTimeOffset now)
    {
        ulong age = output.IsCoinbase ? CoinbaseLock : SpendableAge;
        if (height + 1 < output.Height + age) return false;

        if (output.UnlockTime == 0) return true;

        // The same field means two things, split at a height no chain will reach.
        return output.UnlockTime < MaxBlockNumber
            ? height + 1 >= output.UnlockTime
            : (ulong)now.ToUnixTimeSeconds() >= output.UnlockTime;
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
