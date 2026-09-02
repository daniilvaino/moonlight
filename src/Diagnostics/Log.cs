using System.Globalization;

namespace Moonlight.Diagnostics;

public enum Level
{
    Debug,
    Info,
    Warning,
    Error,
}

/// <summary>One line of the log.</summary>
public readonly record struct Entry(DateTimeOffset At, Level Level, string Source, string Message)
{
    public override string ToString()
        => string.Create(CultureInfo.InvariantCulture,
            $"{At:HH:mm:ss} {Name(Level),-5} {Source,-9} {Message}");

    private static string Name(Level level) => level switch
    {
        Level.Debug => "debug",
        Level.Info => "info",
        Level.Warning => "warn",
        _ => "error",
    };
}

/// <summary>
/// The log. Deliberately ours and deliberately small: a wallet that reaches for a
/// logging framework inherits its packages, its reflection and its configuration
/// files, and the sterile tree allows none of the three.
/// </summary>
/// <remarks>
/// Everything is kept in memory, newest last, up to <see cref="Capacity"/> lines —
/// enough for a screen to show what just happened without the process growing all
/// day. A file is optional and appended to as lines arrive; if writing fails the
/// log carries on in memory rather than taking the wallet down with it.
/// </remarks>
public static class Log
{
    /// <summary>Lines kept in memory. Beyond this the oldest go.</summary>
    public const int Capacity = 500;

    // Not System.Threading.Lock: that is .NET 9, and this targets net8.0.
    private static readonly object Gate = new();
    private static readonly Queue<Entry> Lines = new(Capacity);

    /// <summary>Below this nothing is recorded. Debug is off unless asked for.</summary>
    public static Level Minimum { get; set; } = Level.Info;

    /// <summary>Where lines are also appended, or null to keep them in memory only.</summary>
    public static string? File { get; set; }

    /// <summary>Raised for each line, so a screen can follow along.</summary>
    public static event Action<Entry>? Written;

    public static void Debug(string source, string message) => Write(Level.Debug, source, message);

    public static void Info(string source, string message) => Write(Level.Info, source, message);

    public static void Warn(string source, string message) => Write(Level.Warning, source, message);

    public static void Error(string source, string message) => Write(Level.Error, source, message);

    /// <summary>
    /// An exception, named by its type. The type is half the information — an
    /// HttpRequestException and a FormatException from the same call mean very
    /// different things — and a message alone loses it.
    /// </summary>
    public static void Error(string source, string message, Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);

        Write(Level.Error, source, $"{message}: {error.GetType().Name}: {error.Message}");
    }

    public static Entry[] Recent()
    {
        lock (Gate) return [.. Lines];
    }

    public static void Clear()
    {
        lock (Gate) Lines.Clear();
    }

    private static void Write(Level level, string source, string message)
    {
        if (level < Minimum) return;

        Entry entry = new(DateTimeOffset.Now, level, source, message);

        lock (Gate)
        {
            if (Lines.Count == Capacity) Lines.Dequeue();
            Lines.Enqueue(entry);

            Append(entry);
        }

        // Outside the lock: a handler that logs would deadlock on a lock it already
        // holds, and a slow one would hold up whoever is writing.
        Written?.Invoke(entry);
    }

    private static void Append(Entry entry)
    {
        if (File is not { Length: > 0 } path) return;

        try
        {
            System.IO.File.AppendAllText(path, entry + Environment.NewLine);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // A log that cannot be written is not a reason to stop a wallet. The
            // file is dropped so it is attempted once, not on every line.
            File = null;
        }
    }
}
