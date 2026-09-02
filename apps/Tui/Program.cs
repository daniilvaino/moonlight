using Moonlight.Diagnostics;
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

        // Beside the wallet, so a log is where its wallet is. --log names another
        // path, and --debug asks for the per-batch lines as well.
        Log.File = Flag(args, "log") ?? args[0] + ".log";
        if (args.Contains("--debug")) Log.Minimum = Level.Debug;

        Log.Info("wallet", $"opened {System.IO.Path.GetFileName(args[0])} at block {saved.ScannedHeight}");
        Log.Info("node", $"daemon {daemon}");

        SharpOSDriver driver = new();
        Application.Init(driver, new ConsoleMainLoop(driver));

        // Before any screen is built: the schemes are global, and a view created
        // earlier keeps the colours that were in force when it was made.
        Theme.Apply();

        Scanner scanner = new(account, lookahead);

        WalletState state = new(scanner, saved.ScannedHeight);
        state.Restore(saved);

        string path = args[0];

        Uri current = daemon;

        // Kept warm while the wallet is open: the loop catches up, then looks again
        // every ten seconds — the shape wallet2's api uses, which is what Feather is
        // built on. Scan now only nudges it awake.
        using CancellationTokenSource stopping = new();

        ChainSync sync = new(current, state) { PendingRestoreDate = opened.PendingRestoreDate };
        CancellationTokenSource running = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);

        void Save()
        {
            WalletDocument document = opened.Document with
            {
                Network = account.Network.ToString(),
                Settings = opened.Document.Settings with { Nodes = [current.ToString()] },
            };

            Storage.Save(path, document, Storage.Encrypt(
                account, opened.Seal!, state.ScannedHeight, lookahead, state.Snapshot(), current.ToString(),
                document.SettingsFingerprint(), sync.PendingRestoreDate));
        }

        Home home = new(account);
        History history = new();
        Send send = new();
        Receive receive = new(account);
        Coins coins = new();
        Screens.Node node = new(daemon);
        Journal journal = new();

        // TabView adds a tab's view as it is and sizes nothing, so a page that does
        // not say how big it is renders as an empty tab.
        foreach (View page in new View[] { home, history, send, receive, coins, node, journal })
        {
            page.X = 0;
            page.Y = 0;
            page.Width = Dim.Fill();
            page.Height = Dim.Fill();
        }

        Toplevel top = Application.Top;

        // Menu on the first line, tabs under it, status on the last — the shape
        // Feather uses. What the wallet is doing and what it holds then stay on
        // screen whichever tab is open, rather than living on one of them.
        Status status = new() { X = 0, Y = Pos.AnchorEnd(1), Width = Dim.Fill(), Height = 1 };

        TabView tabs = new() { X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill(1) };

        TabView.Tab[] pages =
        [
            new TabView.Tab("Home", home),
            new TabView.Tab("History", history),
            new TabView.Tab("Send", send),
            new TabView.Tab("Receive", receive),
            new TabView.Tab("Coins", coins),
            new TabView.Tab("Node", node),
            new TabView.Tab("Log", journal),
        ];

        foreach (TabView.Tab page in pages) tabs.AddTab(page, andSelect: false);

        tabs.SelectedTab = pages[0];

        // Selecting a tab before the view has been laid out scrolls the strip: with
        // a zero-width Bounds nothing counts as visible, so it scrolls to the
        // selection and never comes back. Rewinding on layout lets it scroll again
        // only when the tabs genuinely do not fit.
        tabs.LayoutComplete += _ =>
        {
            tabs.TabScrollOffset = 0;
            tabs.EnsureSelectedTabIsVisible();
        };

        void Show(string name)
            => tabs.SelectedTab = Array.Find(
                pages, p => string.Equals(p.Text, name, StringComparison.OrdinalIgnoreCase)) ?? pages[0];

        top.Add(tabs, status);

        // One place that repaints everything, so no tab can drift out of step with
        // the status line or with another tab.
        void Refresh(ulong scanned, ulong chainHeight)
        {
            ulong at = scanned == 0 ? 0 : scanned - 1;
            Balance held = state.BalanceAt(at);

            home.Update(held, scanned, chainHeight, state.Outputs.Count());
            history.Update(state.Outputs, state.SpentAt);
            coins.Update(state.Outputs, state.IsSpent, at);

            status.Report(
                Amounts.Activity(scanned, chainHeight),
                $"{Amounts.Format(held.Total)} XMR",
                current.Host);

            journal.Reload();
        }

        // Shown for a moment and then taken back, so the status line returns to
        // saying what the wallet is doing.
        void Flash(string text)
        {
            status.Note(text);

            Application.MainLoop.AddTimeout(TimeSpan.FromSeconds(1), _ =>
            {
                status.Note(null);
                return false;
            });
        }

        void StartSync()
        {
            _ = sync.RunAsync(
                progress => Application.MainLoop.Invoke(() =>
                {
                    Refresh(progress.Height, progress.ChainHeight);

                    // Written whenever it settles, so closing the window never costs a scan.
                    if (progress.CaughtUp) Save();
                }),
                error => Application.MainLoop.Invoke(() => node.Report(error)),
                cancellationToken: running.Token);
        }

        // A different daemon means a different loop: the old one is stopped, the
        // address is remembered, and scanning continues from where it stands.
        node.Changed += chosen =>
        {
            running.Cancel();
            sync.Dispose();

            current = chosen;
            // The unresolved restore date travels to the new loop: changing node
            // is not an answer to it.
            sync = new ChainSync(current, state) { PendingRestoreDate = sync.PendingRestoreDate };
            running = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);

            Save();
            StartSync();
        };

        View? keys = null;

        // Saving on purpose, rather than because a scan settled: it says so, since
        // a save that changes nothing on screen looks like a key that did nothing.
        void SaveNow()
        {
            Save();
            Log.Info("wallet", $"saved at block {state.ScannedHeight}");
            Flash("saved — ready");
        }

        void Quit()
        {
            stopping.Cancel();
            Save();
            Application.RequestStop();
        }

        // The shortcuts come from Keys, which is also what the help window reads,
        // so a listed key is a bound key.
        top.Add(new MenuBar(
        [
            new MenuBarItem("_Wallet",
            [
                new MenuItem("_Home", "", () => Show("Home")),
                new MenuItem("_Receive", "", () => Show("Receive")),
                new MenuItem("_Coins", "", () => Show("Coins")),
                new MenuItem("_Node", "", () => Show("Node")),
                null!,
                new MenuItem("_Save", "", SaveNow, null, null, Screens.Keys.Save.Binding),
                new MenuItem("_Quit", "", Quit, null, null, Screens.Keys.Quit.Binding),
            ]),
            new MenuBarItem("_Sync",
            [
                new MenuItem("_Scan now", "", () => sync.RefreshNow(), null, null, Screens.Keys.Scan.Binding),
            ]),
            new MenuBarItem("_Help",
            [
                new MenuItem("_Keys", "", ShowKeys, null, null, Screens.Keys.Help.Binding),
            ]),
        ]));

        void SetKeys(bool visible)
        {
            if (visible == (keys is not null)) return;

            if (visible)
            {
                keys = Screens.Keys.Build();
                top.Add(keys);
            }
            else
            {
                top.Remove(keys);
                keys = null;
            }

            top.SetNeedsDisplay();
        }

        void ShowKeys() => SetKeys(keys is null);

        // Before anything else sees the key, which is why typing has to be checked
        // here: a backtick meant for the "pay to" field is a backtick, not a
        // shortcut.
        Application.RootKeyEvent = key =>
        {
            bool typing = top.MostFocused is TextField or TextView;

            if (Screens.Keys.Panel.Matches(key.Key) && !typing)
            {
                // Where releases are real the tilde holds the panel open, so the
                // press only ever opens it and the release closes it.
                SetKeys(Screens.Keys.HoldToShow || keys is null);
                return true;
            }

            if (key.Key == Key.Esc && keys is not null)
            {
                SetKeys(false);
                return true;
            }

            if ((key.Key & Key.AltMask) != 0)
            {
                int digit = (int)(key.Key & ~Key.AltMask) - '1';

                if (digit >= 0 && digit < pages.Length)
                {
                    tabs.SelectedTab = pages[digit];
                    return true;
                }
            }

            return false;
        };


        if (Screens.Keys.HoldToShow)
        {
            top.KeyUp += e =>
            {
                if (Screens.Keys.Panel.Matches(e.KeyEvent.Key)) SetKeys(false);
            };
        }

        // --screen opens straight onto one of them, which is also what makes the
        // screens checkable without a keyboard.
        Show(Flag(args, "screen") ?? "home");

        Refresh(state.ScannedHeight, 0);

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
