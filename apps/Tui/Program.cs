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
        WalletFile opened = Load(args);
        (Account account, WalletSnapshot saved, SubaddressIndex lookahead, _) = opened;

        Uri daemon = new(Flag(args, "daemon")
            ?? opened.Document.Settings.Nodes.FirstOrDefault()
            ?? opened.Daemon
            ?? Environment.GetEnvironmentVariable("MOONLIGHT_DAEMON")
            ?? "http://127.0.0.1:18081/");

        SharpOSDriver driver = new();
        Application.Init(driver, new ConsoleMainLoop(driver));

        // Before any screen is built: the schemes are global, and a view created
        // earlier keeps the colours that were in force when it was made.
        Theme.Apply();

        Scanner scanner = new(account, lookahead);

        // The marker is kept rather than flattened to zero: ChainSync resolves it
        // against the chain, and turning it into 0 here is what made a new wallet
        // start reading from the genesis block.
        WalletState state = new(scanner, saved.ScannedHeight);
        if (saved.ScannedHeight != RestoreHeight.FromTip) state.Restore(saved);

        string path = args[0];

        Uri current = daemon;

        void Save()
        {
            WalletDocument document = opened.Document with
            {
                Network = account.Network.ToString(),
                Settings = opened.Document.Settings with { Nodes = [current.ToString()] },
            };

            Storage.Save(path, document, Storage.Encrypt(
                account, opened.Seal!, state.ScannedHeight, lookahead, state.Snapshot(), current.ToString(),
                document.SettingsFingerprint()));
        }

        Dashboard dashboard = new(account) { X = 0, Y = 0, Width = Dim.Fill(), Height = 11 };
        History history = new() { X = 0, Y = 11, Width = Dim.Fill(), Height = Dim.Fill() };
        Receive receive = new(account) { X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill(1) };
        Screens.Node node = new(daemon) { X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill(1) };

        Toplevel top = Application.Top;
        // Below the menu bar. At Y = 0 it drew straight over it, which is why the
        // dashboard had no menu while every other screen did.
        View main = new() { X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill() };
        main.Add(dashboard, history);

        void Show(View screen)
        {
            top.Remove(main);
            top.Remove(receive);
            top.Remove(node);
            top.Add(screen);
            top.SetNeedsDisplay();
        }

        // Kept warm while the wallet is open: the loop catches up, then looks again
        // every ten seconds — the shape wallet2's api uses, which is what Feather is
        // built on. Scan now only nudges it awake.
        using CancellationTokenSource stopping = new();

        ChainSync sync = new(current, state);
        CancellationTokenSource running = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);

        void StartSync()
        {
            _ = sync.RunAsync(
                progress => Application.MainLoop.Invoke(() =>
                {
                    dashboard.Update(state.BalanceAt(progress.Height), progress.Height, progress.ChainHeight, progress.Outputs);
                    history.Update(state.Outputs, state.IsSpent);

                    // Written whenever it settles, so closing the window never costs a scan.
                    if (progress.CaughtUp) Save();
                }),
                cancellationToken: running.Token);
        }

        // A different daemon means a different loop: the old one is stopped, the
        // address is remembered, and scanning continues from where it stands.
        node.Changed += chosen =>
        {
            running.Cancel();
            sync.Dispose();

            current = chosen;
            sync = new ChainSync(current, state);
            running = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);

            Save();
            StartSync();
        };

        top.Add(new MenuBar(
        [
            new MenuBarItem("_Wallet",
            [
                new MenuItem("_Dashboard", "", () => Show(main)),
                new MenuItem("_Receive", "", () => Show(receive)),
                new MenuItem("_Node", "", () => Show(node)),
                null!,
                new MenuItem("_Save", "", Save),
                new MenuItem("_Quit", "", () =>
                {
                    stopping.Cancel();
                    Save();
                    Application.RequestStop();
                }),
            ]),
            new MenuBarItem("_Sync",
            [
                new MenuItem("_Scan now", "", () => sync.RefreshNow()),
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

        StartSync();

        Application.Run();
        stopping.Cancel();
        sync.Dispose();
        Application.Shutdown();
    }

    private static WalletFile Load(string[] args)
    {
        string path = args[0];

        if (!File.Exists(path)) throw new IOException($"no wallet at {path}");

        return Storage.OpenDocument(File.ReadAllBytes(path), Flag(args, "password") ?? Prompt());
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
