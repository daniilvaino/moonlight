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
        // A new wallet has no history: it starts at the tip, resolved on the first
        // scan rather than now, so creating one needs no daemon.
        Save(path, account, Storage.Seal(Options.NewPassword(args)), NewDocument(args, account), Empty(RestoreHeight.FromTip));

        Console.WriteLine($"address: {account.Address.Encode()}");
        Console.WriteLine("scanning from the current tip — nothing before now can be yours");
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

        // A restored wallet starts at its restore height if one is given, and
        // otherwise at zero — slow, but never silently missing a payment.
        ulong height = Options.Optional(args, "restore-height") is string given
            ? ulong.Parse(given, System.Globalization.CultureInfo.InvariantCulture)
            : Options.Optional(args, "restore-date") is string date
                ? RestoreHeight.Estimate(DateTimeOffset.Parse(date, System.Globalization.CultureInfo.InvariantCulture))
                : 0;

        Save(path, account, Storage.Seal(Options.NewPassword(args)), NewDocument(args, account), Empty(height));

        Console.WriteLine($"address: {account.Address.Encode()}");
        Console.WriteLine($"scanning from block {height}");

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
        WalletFile wallet = Open(args);
        (Account account, WalletSnapshot snapshot, SubaddressIndex lookahead, _) = wallet;

        WalletState state = new(new Scanner(account, lookahead), snapshot.ScannedHeight);
        state.Restore(snapshot);

        if (snapshot.ScannedHeight == RestoreHeight.FromTip)
        {
            Console.WriteLine("not scanned yet — this wallet starts at the tip; run 'moonlight sync'");
            return 0;
        }

        ulong at = snapshot.ScannedHeight == 0 ? 0 : snapshot.ScannedHeight - 1;
        Balance balance = state.BalanceAt(at);

        Console.WriteLine($"scanned to block {at}");
        Console.WriteLine($"balance:  {Format(balance.Total)} XMR");
        Console.WriteLine($"unlocked: {Format(balance.Unlocked)} XMR");

        foreach (OwnedOutput output in state.Unspent.OrderBy(o => o.Height))
        {
            Console.WriteLine($"  block {output.Height,9}  {Format(output.Amount),20} XMR  " +
                $"({output.Subaddress.Major},{output.Subaddress.Minor})");
        }

        return 0;
    }

    /// <summary>Twelve decimal places, and a wallet that rounds them is lying.</summary>
    public static string Format(ulong atomic)
        => (atomic / 1_000_000_000_000m).ToString("0.############", System.Globalization.CultureInfo.InvariantCulture);

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
        string? daemon = null)
    {
        WalletDocument updated = document with
        {
            Network = account.Network.ToString(),
            Settings = daemon is null ? document.Settings : document.Settings with { Nodes = [daemon] },
        };

        Storage.Save(path, updated, Storage.Encrypt(
            account, seal, snapshot.ScannedHeight, updated.Settings.Lookahead, snapshot, daemon,
            updated.SettingsFingerprint()));
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
