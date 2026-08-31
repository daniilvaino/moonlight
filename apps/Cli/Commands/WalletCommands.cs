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
        Save(path, account, Options.NewPassword(args), scannedHeight: 0);

        Console.WriteLine($"address: {account.Address.Encode()}");
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

        Save(path, account, Options.NewPassword(args), height);

        Console.WriteLine($"address: {account.Address.Encode()}");
        Console.WriteLine($"scanning from block {height}");

        return 0;
    }

    public static int Address(string[] args)
    {
        (Account account, _) = Open(args);
        string[] positional = Options.Positional(args);

        uint major = positional.Length > 1 ? uint.Parse(positional[1], System.Globalization.CultureInfo.InvariantCulture) : 0;
        uint minor = positional.Length > 2 ? uint.Parse(positional[2], System.Globalization.CultureInfo.InvariantCulture) : 0;

        Console.WriteLine(Subaddress.Address(account, new SubaddressIndex(major, minor)).Encode());
        return 0;
    }

    public static int Seed(string[] args)
    {
        (Account account, _) = Open(args);

        Console.WriteLine(account.Seed);
        return 0;
    }

    public static int ShowBalance(string[] args)
    {
        (Account _, ulong height) = Open(args);

        // The file keeps the height but not the outputs yet, so this reports what
        // is known rather than pretending to a balance it cannot compute.
        Console.WriteLine($"scanned to block {height}");
        Console.WriteLine("balance: run 'moonlight sync' — this build does not yet keep outputs between runs");

        return 0;
    }

    public static (Account Account, ulong ScannedHeight) Open(string[] args)
    {
        string path = Options.File(args);

        if (!System.IO.File.Exists(path))
        {
            throw new IOException($"no wallet at {path}");
        }

        return Storage.Decrypt(System.IO.File.ReadAllBytes(path), Options.Password(args));
    }

    public static void Save(string path, Account account, string password, ulong scannedHeight)
        => System.IO.File.WriteAllBytes(path, Storage.Encrypt(account, password, scannedHeight));

    private static void Refuse(string path)
    {
        if (System.IO.File.Exists(path))
        {
            throw new IOException($"{path} already exists — refusing to overwrite a wallet");
        }
    }
}
