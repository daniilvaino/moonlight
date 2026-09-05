using Moonlight.Core;
using Moonlight.Wallet;

namespace Moonlight.Cli.Commands;

/// <summary>Scanning the chain, which is the only long-running thing this wallet does.</summary>
internal static class SyncCommand
{
    public static async Task<int> Run(string[] args)
    {
        using WalletSession wallet = WalletCommands.OpenSession(args);

        DateTimeOffset started = DateTimeOffset.UtcNow;
        ulong from = wallet.State.ScannedHeight;

        Console.WriteLine($"scanning on {wallet.Daemon}");

        SyncProgress progress = await wallet.CatchUpAsync(p => Report(p, from, started)).ConfigureAwait(false);

        Console.WriteLine();
        Console.WriteLine($"caught up at block {progress.Height}");

        Balance balance = wallet.Balance();

        Console.WriteLine($"balance:  {Amounts.Format(balance.Total)} XMR");
        Console.WriteLine($"unlocked: {Amounts.Format(balance.Unlocked)} XMR");

        foreach (OwnedOutput output in wallet.State.Unspent.OrderBy(o => o.Height))
        {
            Console.WriteLine($"  block {output.Height,9}  {Amounts.Format(output.Amount),20} XMR  " +
                $"({output.Subaddress.Major},{output.Subaddress.Minor})");
        }

        // Everything the scan found goes back into the file, so the next run starts
        // where this one stopped rather than reading the chain again.
        wallet.Save();

        return 0;
    }

    private static void Report(SyncProgress progress, ulong from, DateTimeOffset started)
    {
        double seconds = (DateTimeOffset.UtcNow - started).TotalSeconds;
        double rate = seconds > 0 ? (progress.Height - from) / seconds : 0;

        Console.Write($"\rblock {progress.Height}/{progress.ChainHeight - 1}  {rate:F0} blocks/s  outputs {progress.Outputs}   ");
    }
}
