using System.Globalization;
using Moonlight.Node;
using Moonlight.Serialization;
using Moonlight.Wallet;

namespace Moonlight.Cli.Commands;

/// <summary>Scanning the chain, which is the only long-running thing this wallet does.</summary>
internal static class SyncCommand
{
    public static async Task<int> Run(string[] args)
    {
        (Account account, ulong from) = WalletCommands.Open(args);

        using DaemonClient daemon = new(Options.Daemon(args));
        ulong height = await daemon.GetHeightAsync().ConfigureAwait(false);

        Scanner scanner = new(account);
        WalletState state = new(scanner, from);

        Console.WriteLine($"scanning {from} to {height - 1} on {Options.Daemon(args)}");

        DateTimeOffset started = DateTimeOffset.UtcNow;
        ulong scanned = 0;

        for (ulong at = from; at < height; at++)
        {
            Block block = await daemon.GetBlockAsync(at).ConfigureAwait(false);

            List<Transaction> transactions = [block.MinerTransaction];

            if (block.TransactionIds.Length > 0)
            {
                string[] ids = [.. block.TransactionIds.Select(id => Convert.ToHexString(id).ToLowerInvariant())];
                transactions.AddRange((await daemon.GetTransactionsAsync(ids).ConfigureAwait(false)).Select(blob => TxParser.Parse(blob)));
            }

            state.Process(at, transactions);
            scanned++;

            if (scanned % 100 == 0) Report(state, at, height, started, scanner);
        }

        Report(state, height - 1, height, started, scanner);

        Balance balance = state.BalanceAt(height - 1);
        Console.WriteLine();
        Console.WriteLine($"balance:  {Format(balance.Total)} XMR");
        Console.WriteLine($"unlocked: {Format(balance.Unlocked)} XMR");

        foreach (OwnedOutput output in state.Unspent.OrderBy(o => o.Height))
        {
            Console.WriteLine($"  block {output.Height,9}  {Format(output.Amount),20} XMR  " +
                $"({output.Subaddress.Major},{output.Subaddress.Minor})");
        }

        // The wallet file remembers where scanning got to; the outputs themselves
        // are not persisted yet, so a rescan starts from the restore height.
        WalletCommands.Save(Options.File(args), account, Options.Password(args), from);

        return 0;
    }

    private static void Report(WalletState state, ulong at, ulong height, DateTimeOffset started, Scanner scanner)
    {
        double seconds = (DateTimeOffset.UtcNow - started).TotalSeconds;
        double rate = seconds > 0 ? (at - state.RestoreHeight + 1) / seconds : 0;

        Console.Write($"\rblock {at}/{height - 1}  {rate:F0} blocks/s  " +
            $"outputs seen {scanner.Examined}, ours {state.Outputs.Count()}   ");
    }

    /// <summary>Monero has twelve decimal places, and a wallet that rounds them is lying.</summary>
    private static string Format(ulong atomic)
        => (atomic / 1_000_000_000_000m).ToString("0.############", CultureInfo.InvariantCulture);
}
