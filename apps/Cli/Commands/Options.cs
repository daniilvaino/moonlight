using Moonlight.Wallet;

namespace Moonlight.Cli.Commands;

/// <summary>Flags shared by every command, and the small amount of prompting the CLI does.</summary>
internal static class Options
{
    public static string Value(string[] args, string name, string? fallback = null)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == $"--{name}") return args[i + 1];
        }

        return fallback ?? throw new ArgumentException($"missing --{name}");
    }

    public static string? Optional(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == $"--{name}") return args[i + 1];
        }

        return null;
    }

    public static string[] Positional(string[] args)
    {
        List<string> positional = [];

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--", StringComparison.Ordinal))
            {
                i++;   // skip its value
                continue;
            }

            positional.Add(args[i]);
        }

        return [.. positional];
    }

    public static string File(string[] args)
    {
        string[] positional = Positional(args);

        return positional.Length > 0
            ? positional[0]
            : throw new ArgumentException("which wallet file?");
    }

    public static Network Network(string[] args) => (Optional(args, "network") ?? "mainnet").ToLowerInvariant() switch
    {
        "mainnet" => Wallet.Network.Mainnet,
        "stagenet" => Wallet.Network.Stagenet,
        "testnet" => Wallet.Network.Testnet,
        var other => throw new ArgumentException($"unknown network '{other}'"),
    };

    /// <summary>The flag wins, then the wallet's own last daemon, then the environment, then localhost.</summary>
    public static Uri Daemon(string[] args, string? remembered = null)
        => Core.DaemonAddress.Resolve(Optional(args, "daemon"), remembered);

    /// <summary>
    /// A password from the command line if given, otherwise typed without echo. The
    /// flag exists for scripts; on a shared machine it puts the password in the
    /// history, which is why it is not the default.
    /// </summary>
    public static string Password(string[] args, string prompt = "password: ")
    {
        if (Optional(args, "password") is string given) return given;

        Console.Write(prompt);
        string password = ReadHidden();
        Console.WriteLine();

        return password.Length > 0 ? password : throw new ArgumentException("a wallet needs a password");
    }

    public static string NewPassword(string[] args)
    {
        if (Optional(args, "password") is string given) return given;

        string first = Password(args, "new password: ");

        Console.Write("again: ");
        string second = ReadHidden();
        Console.WriteLine();

        return first == second ? first : throw new ArgumentException("the two passwords differ");
    }

    private static string ReadHidden()
    {
        // No echo where there is a console; where there is not — a pipe, a test —
        // fall back to a plain line rather than failing.
        if (Console.IsInputRedirected) return Console.ReadLine() ?? "";

        System.Text.StringBuilder typed = new();

        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter) return typed.ToString();

            if (key.Key == ConsoleKey.Backspace)
            {
                if (typed.Length > 0) typed.Length--;
                continue;
            }

            if (!char.IsControl(key.KeyChar)) typed.Append(key.KeyChar);
        }
    }
}
