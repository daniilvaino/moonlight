namespace Moonlight.Core;

/// <summary>Which node to talk to, and in what order the answers are preferred.</summary>
public static class DaemonAddress
{
    /// <summary>Where a wallet looks when nothing else says otherwise.</summary>
    public const string Local = "http://127.0.0.1:18081/";

    /// <summary>The environment variable a shell can set for a session.</summary>
    public const string Variable = "MOONLIGHT_DAEMON";

    /// <summary>
    /// What was asked for wins, then what the wallet last used, then the
    /// environment, then localhost.
    /// </summary>
    /// <remarks>
    /// The order matters and was duplicated in two applications before this: a flag
    /// is a deliberate choice for one run, a remembered node is the owner's standing
    /// choice, and the variable is the shell's. Localhost last, because a wallet
    /// that silently talks to a stranger's node is a privacy failure and one that
    /// fails to reach its own is merely an error message.
    /// </remarks>
    public static Uri Resolve(string? asked = null, string? remembered = null)
        => new(asked
            ?? remembered
            ?? Environment.GetEnvironmentVariable(Variable)
            ?? Local);
}
