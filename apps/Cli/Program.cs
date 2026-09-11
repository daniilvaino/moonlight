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
        Console.WriteLine($"moonlight {BuildVersion.Value} ({Mode})");
        return 0;
    }

    /// <summary>
    /// Which of the three builds this is. All three are answered at compile time,
    /// because that is when the answer is decided: bflat defines its own symbol, and
    /// NATIVEAOT follows the PublishAot property, which the managed publish turns off.
    /// </summary>
    /// <remarks>
    /// Two run-time answers were tried first and both were wrong.
    /// RuntimeFeature.IsDynamicCodeCompiled looks like the question and is not:
    /// PublishAot writes IsDynamicCodeSupported=false into the managed runtimeconfig
    /// too, so a managed build called itself NativeAOT. Assembly.Location does
    /// separate them — it is empty in a native image — but the single-file analyzer
    /// refuses it, and silencing an AOT analyzer to ask an AOT question is the wrong
    /// trade in a project built around them.
    ///
    /// One inaccuracy is left on purpose: PublishAot states intent, so a plain
    /// `dotnet build` carries NATIVEAOT while producing a managed assembly. Every
    /// artifact a release ships answers correctly, which is what the string is for.
    /// </remarks>
    private static string Mode =>
#if BFLAT
        "bflat";
#elif NATIVEAOT
        "nativeaot";
#else
        "managed";
#endif
}
