using Moonlight.Node;
using Moonlight.Serialization;
using Moonlight.Tui.Screens;
using Moonlight.Wallet;
using Terminal.Gui;

namespace Moonlight.Tui;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("moonlight-tui <wallet file> [--password p] [--daemon url] [--screen receive|node]");
            Console.WriteLine();
            Console.WriteLine("This wallet can receive. It cannot spend yet.");
            return;
        }

        try
        {
            Run(args);
        }
        catch (Exception e) when (e is FormatException or IOException or ArgumentException)
        {
            Console.Error.WriteLine($"error: {e.Message}");
        }
    }

    private static void Run(string[] args)
    {
        (Account account, WalletSnapshot saved, SubaddressIndex lookahead) = Load(args);

        Uri daemon = new(Flag(args, "daemon")
            ?? Environment.GetEnvironmentVariable("MOONLIGHT_DAEMON")
            ?? "http://127.0.0.1:18081/");

        SharpOSDriver driver = new();
        Application.Init(driver, new ConsoleMainLoop(driver));

        // Before any screen is built: the schemes are global, and a view created
        // earlier keeps the colours that were in force when it was made.
        Theme.Apply();

        Scanner scanner = new(account, lookahead);
        WalletState state = new(scanner, saved.ScannedHeight);
        state.Restore(saved);

        Dashboard dashboard = new(account) { X = 0, Y = 1, Width = Dim.Fill(), Height = 11 };
        History history = new() { X = 0, Y = 12, Width = Dim.Fill(), Height = Dim.Fill(1) };
        Receive receive = new(account) { X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill(1) };
        Screens.Node node = new(daemon) { X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill(1) };

        Toplevel top = Application.Top;
        View main = new() { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        main.Add(dashboard, history);

        void Show(View screen)
        {
            top.Remove(main);
            top.Remove(receive);
            top.Remove(node);
            top.Add(screen);
            top.SetNeedsDisplay();
        }

        top.Add(new MenuBar(
        [
            new MenuBarItem("_Wallet",
            [
                new MenuItem("_Dashboard", "", () => Show(main)),
                new MenuItem("_Receive", "", () => Show(receive)),
                new MenuItem("_Node", "", () => Show(node)),
                null!,
                new MenuItem("_Quit", "", () => Application.RequestStop()),
            ]),
            new MenuBarItem("_Sync",
            [
                new MenuItem("_Scan now", "", () => _ = Sync(daemon, state, scanner, dashboard, history)),
            ]),
        ]));

        // --screen opens straight onto one of them, which is also what makes the
        // screens checkable without a keyboard.
        Show(Flag(args, "screen") switch
        {
            "receive" => receive,
            "node" => node,
            _ => main,
        });

        dashboard.Update(state.Balance(), state.ScannedHeight, 0, state.Outputs.Count());
        history.Update(state.Outputs, state.IsSpent);

        Application.Run();
        Application.Shutdown();
    }

    /// <summary>
    /// Scanning, one block at a time, yielding to the interface between blocks so
    /// the window stays alive. A wallet that freezes while it works looks broken.
    /// </summary>
    private static async Task Sync(Uri daemon, WalletState state, Scanner scanner, Dashboard dashboard, History history)
    {
        using DaemonClient client = new(daemon);

        try
        {
            ulong height = await client.GetHeightAsync().ConfigureAwait(true);

            for (ulong at = state.ScannedHeight; at < height; at++)
            {
                Block block = await client.GetBlockAsync(at).ConfigureAwait(true);
                List<Transaction> transactions = [block.MinerTransaction];

                if (block.TransactionIds.Length > 0)
                {
                    string[] ids = [.. block.TransactionIds.Select(id => Convert.ToHexString(id).ToLowerInvariant())];
                    transactions.AddRange((await client.GetTransactionsAsync(ids).ConfigureAwait(true))
                        .Select(blob => TxParser.Parse(blob)));
                }

                state.Process(at, transactions);

                if (at % 20 == 0 || at == height - 1)
                {
                    dashboard.Update(state.BalanceAt(at), at, height, state.Outputs.Count());
                    history.Update(state.Outputs, state.IsSpent);
                    Application.Refresh();
                }
            }
        }
        catch (Exception e) when (e is HttpRequestException or DaemonException or FormatException)
        {
            MessageBox.ErrorQuery("Sync", e.Message, "Ok");
        }
    }

    private static (Account Account, WalletSnapshot Snapshot, SubaddressIndex Lookahead) Load(string[] args)
    {
        string path = args[0];

        if (!File.Exists(path)) throw new IOException($"no wallet at {path}");

        string password = Flag(args, "password") ?? Prompt();

        return Storage.Open(File.ReadAllBytes(path), password);
    }

    private static string Prompt()
    {
        Console.Write("password: ");
        return Console.ReadLine() ?? "";
    }

    private static string? Flag(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == $"--{name}") return args[i + 1];
        }

        return null;
    }
}
