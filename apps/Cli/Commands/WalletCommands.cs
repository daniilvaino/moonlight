using Moonlight.Core;
using Moonlight.Core.Http;
using Moonlight.Node;
using Moonlight.Wallet;

namespace Moonlight.Cli.Commands;

/// <summary>Everything that needs only the wallet file.</summary>
internal static class WalletCommands
{
    public static int Create(string[] args)
    {
        string path = Options.File(args);
        Refuse(path);

        Account account = Account.Create(Options.Network(args));

        // Fixed now, not at the first scan: a wallet paid in the minutes after it
        // was made would otherwise never see that payment.
        ulong from = StartHeight(args);

        Save(path, account, Storage.Seal(Options.NewPassword(args)), NewDocument(args, account), Empty(from));

        Console.WriteLine($"address: {account.Address.Encode()}");
        Console.WriteLine($"scanning from block {from}");
        Console.WriteLine();
        Console.WriteLine("seed — write it down, it is the only way back:");
        Console.WriteLine($"  {account.Seed}");

        return 0;
    }

    public static int Restore(string[] args)
    {
        string[] positional = Options.Positional(args);

        if (positional.Length < 2)
        {
            throw new ArgumentException("restore <file> \"<25 words>\"");
        }

        string path = positional[0];
        Refuse(path);

        Account account = Account.FromMnemonic(string.Join(' ', positional[1..]), Options.Network(args));

        (ulong height, DateTimeOffset? pending) = RestoreStart(args);

        Save(path, account, Storage.Seal(Options.NewPassword(args)), NewDocument(args, account),
            Empty(height), pendingRestoreDate: pending);

        Console.WriteLine($"address: {account.Address.Encode()}");
        Console.WriteLine($"scanning from block {height}");

        if (pending is not null)
        {
            Console.WriteLine("no daemon answered, so that is an estimate — it will be replaced");
            Console.WriteLine("with the exact block on the first scan that reaches one");
        }

        return 0;
    }

    public static int Address(string[] args)
    {
        Account account = Open(args).Account;
        string[] positional = Options.Positional(args);

        uint major = positional.Length > 1 ? uint.Parse(positional[1], System.Globalization.CultureInfo.InvariantCulture) : 0;
        uint minor = positional.Length > 2 ? uint.Parse(positional[2], System.Globalization.CultureInfo.InvariantCulture) : 0;

        Console.WriteLine(Subaddress.Address(account, new SubaddressIndex(major, minor)).Encode());
        return 0;
    }

    public static int Seed(string[] args)
    {
        Account account = Open(args).Account;

        Console.WriteLine(account.Seed);
        return 0;
    }

    public static int ShowBalance(string[] args)
    {
        using WalletConnection wallet = OpenConnection(args);

        ulong at = Amounts.LastScanned(wallet.State.ScannedHeight);
        Balance balance = wallet.Balance();

        Console.WriteLine($"scanned to block {at}");
        Console.WriteLine($"balance:  {Amounts.Format(balance.Total)} XMR");
        Console.WriteLine($"unlocked: {Amounts.Format(balance.Unlocked)} XMR");

        foreach (OwnedOutput output in wallet.State.Unspent.OrderBy(o => o.Height))
        {
            Console.WriteLine($"  block {output.Height,9}  {Amounts.Format(output.Amount),20} XMR  " +
                $"({output.Subaddress.Major},{output.Subaddress.Minor})");
        }

        return 0;
    }

    /// <summary>An open wallet, with its scanner, its state and its node already arranged.</summary>
    public static WalletConnection OpenConnection(string[] args)
        => WalletConnection.Open(Options.File(args), Options.Password(args), Options.Optional(args, "daemon"));

    /// <summary>The file alone, for the commands that only want the keys in it.</summary>
    public static WalletFile Open(string[] args)
    {
        string path = Options.File(args);

        if (!System.IO.File.Exists(path))
        {
            throw new IOException($"no wallet at {path}");
        }

        WalletFile wallet = Storage.OpenDocument(System.IO.File.ReadAllBytes(path), Options.Password(args));

        // Editing the file by hand is allowed. Being redirected to another node
        // without noticing is not, so every command says so, not just some.
        if (wallet.SettingsChangedOutside)
        {
            Console.Error.WriteLine("note: the settings in this file were changed outside the wallet");
        }

        return wallet;
    }

    public static void Save(
        string path,
        Account account,
        WalletSeal seal,
        WalletDocument document,
        WalletSnapshot snapshot,
        string? daemon = null,
        DateTimeOffset? pendingRestoreDate = null)
    {
        WalletDocument updated = document with
        {
            // Written in this shape, so it says so: a document that keeps an older
            // number carries a fingerprint nothing will ever compare.
            Format = WalletDocument.CurrentFormat,
            Network = account.Network.ToString(),
            Settings = daemon is null ? document.Settings : document.Settings with { Nodes = [daemon] },
        };

        Storage.Save(path, updated, Storage.Encrypt(
            account, seal, snapshot.ScannedHeight, updated.Settings.Lookahead, snapshot, daemon,
            updated.SettingsFingerprint(), pendingRestoreDate));
    }

    /// <summary>
    /// Where a restore starts. An explicit height is taken as given. A date is put
    /// to the daemon, which answers with the exact block; with no daemon to ask,
    /// the offline estimate stands in and the date is kept so that the first daemon
    /// this wallet meets can replace the guess before any scanning is done.
    /// </summary>
    private static (ulong Height, DateTimeOffset? Pending) RestoreStart(string[] args)
    {
        if (Options.Optional(args, "restore-height") is string given)
        {
            return (ulong.Parse(given, System.Globalization.CultureInfo.InvariantCulture), null);
        }

        // No height and no date: from the beginning. Slow, and never wrong.
        if (Options.Optional(args, "restore-date") is not string text) return (0, null);

        DateTimeOffset date = DateTimeOffset.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

        using DaemonClient daemon = new(Options.Daemon(args));

        try
        {
            return (RestoreHeight.ForDateAsync(daemon, date).GetAwaiter().GetResult(), null);
        }
        catch (Exception e) when (e is System.Net.Http.HttpRequestException or DaemonException or TaskCanceledException)
        {
            return (RestoreHeight.Estimate(date), date);
        }
    }

    /// <summary>The daemon's height if one answers, and a deliberately early guess if not.</summary>
    private static ulong StartHeight(string[] args)
    {
        using DaemonClient daemon = new(Options.Daemon(args));

        return RestoreHeight.ForNewWalletAsync(daemon).GetAwaiter().GetResult();
    }

    private static WalletDocument NewDocument(string[] args, Account account)
    {
        SubaddressIndex lookahead = Lookahead(args);

        return new WalletDocument
        {
            Network = account.Network.ToString(),
            Settings = new WalletSettings
            {
                Nodes = Options.Optional(args, "daemon") is string node ? [node] : [],
                LookaheadAccounts = lookahead.Major,
                LookaheadAddresses = lookahead.Minor,
            },
        };
    }

    /// <summary>--lookahead 50,200 — how many accounts and addresses a scan watches.</summary>
    public static SubaddressIndex Lookahead(string[] args)
    {
        if (Options.Optional(args, "lookahead") is not string given) return Storage.DefaultLookahead;

        string[] parts = given.Split(',');

        return parts.Length == 2
            ? new SubaddressIndex(uint.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
                                  uint.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture))
            : throw new ArgumentException("--lookahead wants accounts,addresses — for example 50,200");
    }

    public static WalletSnapshot Empty(ulong scannedHeight)
        => new(scannedHeight, [], new Dictionary<string, ulong>());

    private static void Refuse(string path)
    {
        if (System.IO.File.Exists(path))
        {
            throw new IOException($"{path} already exists — refusing to overwrite a wallet");
        }
    }
}
