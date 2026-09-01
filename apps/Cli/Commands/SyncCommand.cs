using Moonlight.Wallet;

namespace Moonlight.Cli.Commands;

/// <summary>Scanning the chain, which is the only long-running thing this wallet does.</summary>
internal static class SyncCommand
{
    public static async Task<int> Run(string[] args)
    {
        WalletFile wallet = WalletCommands.Open(args);
        (Account account, WalletSnapshot saved, SubaddressIndex lookahead, _) = wallet;

        WalletState state = new(new Scanner(account, lookahead), saved.ScannedHeight);
        state.Restore(saved);

        using ChainSync sync = new(Options.Daemon(args, wallet.Daemon), state)
        {
            // A restore date this wallet could not resolve offline. The first
            // catch-up asks the daemon and clears it.
            PendingRestoreDate = wallet.PendingRestoreDate,
        };

        DateTimeOffset started = DateTimeOffset.UtcNow;
        ulong from = state.ScannedHeight;

        Console.WriteLine($"scanning on {Options.Daemon(args, wallet.Daemon)}");

        SyncProgress progress = await sync.CatchUpAsync(p => Report(p, from, started)).ConfigureAwait(false);

        Console.WriteLine();
        Console.WriteLine($"caught up at block {progress.Height}");

        Balance balance = state.BalanceAt(progress.Height == 0 ? 0 : progress.Height - 1);

        Console.WriteLine($"balance:  {WalletCommands.Format(balance.Total)} XMR");
        Console.WriteLine($"unlocked: {WalletCommands.Format(balance.Unlocked)} XMR");

        foreach (OwnedOutput output in state.Unspent.OrderBy(o => o.Height))
        {
            Console.WriteLine($"  block {output.Height,9}  {WalletCommands.Format(output.Amount),20} XMR  " +
                $"({output.Subaddress.Major},{output.Subaddress.Minor})");
        }

        // Everything the scan found goes back into the file, so the next run starts
        // where this one stopped rather than reading the chain again.
        WalletCommands.Save(Options.File(args), account, wallet.Seal!, wallet.Document, state.Snapshot(),
            Options.Daemon(args, wallet.Daemon).ToString(), sync.PendingRestoreDate);

        return 0;
    }

    private static void Report(SyncProgress progress, ulong from, DateTimeOffset started)
    {
        double seconds = (DateTimeOffset.UtcNow - started).TotalSeconds;
        double rate = seconds > 0 ? (progress.Height - from) / seconds : 0;

        Console.Write($"\rblock {progress.Height}/{progress.ChainHeight - 1}  {rate:F0} blocks/s  outputs {progress.Outputs}   ");
    }
}
