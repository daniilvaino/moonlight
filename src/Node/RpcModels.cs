namespace Moonlight.Node;

/// <summary>A block's header, as the daemon reports it.</summary>
/// <remarks>
/// The only shape here that outlives a call. The request and response records this
/// file used to hold existed to be handed to a serializer, and went with it: the
/// client reads the two or three fields it wants straight out of the answer.
/// </remarks>
public sealed record BlockHeaderInfo(
    string Hash,
    ulong Height,
    byte MajorVersion,
    byte MinorVersion,
    ulong Timestamp,
    string PreviousHash,
    uint Nonce,
    ulong TransactionCount,
    ulong Reward);

/// <summary>The daemon answered, and the answer was a refusal.</summary>
public sealed class DaemonException : Exception
{
    public DaemonException(string message)
        : base(message)
    {
    }

    /// <summary>Keeps what actually went wrong, which for a bad answer is a parse error.</summary>
    public DaemonException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
