using Moonlight.Cli.Commands;

namespace Moonlight.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            return args.Length == 0 ? Usage() : await Run(args).ConfigureAwait(false);
        }
        catch (Exception e) when (e is FormatException or InvalidOperationException or ArgumentException or IOException)
        {
            // Expected failures — a wrong password, a bad address, no daemon — are
            // a message, not a stack trace.
            Console.Error.WriteLine($"error: {e.Message}");
            return 1;
        }
    }

    private static Task<int> Run(string[] args) => args[0] switch
    {
        "new" => Task.FromResult(WalletCommands.Create(args[1..])),
        "restore" => Task.FromResult(WalletCommands.Restore(args[1..])),
        "address" => Task.FromResult(WalletCommands.Address(args[1..])),
        "seed" => Task.FromResult(WalletCommands.Seed(args[1..])),
        "balance" => Task.FromResult(WalletCommands.ShowBalance(args[1..])),
        "sync" => SyncCommand.Run(args[1..]),
        "version" => Task.FromResult(Version()),
        _ => Task.FromResult(Unknown(args[0])),
    };

    private static int Usage()
    {
        Console.WriteLine("""
            moonlight — a Monero wallet in pure managed C#

              new       <file>                 create a wallet
              restore   <file> "<25 words>"    restore one from its seed
              address   <file> [major] [minor] show an address
              seed      <file>                 show the seed phrase
              balance   <file>                 show the balance as last scanned
              sync      <file> [--daemon URL]  scan the chain
              version

            Options
              --password <p>   read the password from the command line instead of a prompt
              --network <n>    mainnet (default), stagenet or testnet
              --daemon <url>   default http://127.0.0.1:18081/, or MOONLIGHT_DAEMON
              --lookahead A,B  how many accounts and addresses a scan watches (default 50,200)

            Restoring
              --restore-height <n>     start scanning at a block
              --restore-date <date>    start at the first block on or after a date
                                       neither: from the genesis block, which is slow but misses nothing

            This wallet can receive. It cannot spend yet.
            """);

        return 1;
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"error: unknown command '{command}'");
        return 1;
    }

    private static int Version()
    {
        Console.WriteLine("moonlight 0.0.1");
        return 0;
    }
}
