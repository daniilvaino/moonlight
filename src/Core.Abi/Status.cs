namespace Moonlight.Core.Abi;

/// <summary>
/// What an entry point returns. Every one of them returns one of these, and none of
/// them throws: a managed exception unwinding through a C frame is undefined
/// behaviour rather than an error anyone can handle.
/// </summary>
/// <remarks>
/// Negative for failures so a caller can test for one with a sign, and stable
/// numbers because they are part of the interface — a value here is never reused
/// for something else once it has been published.
/// </remarks>
public enum Status
{
    Ok = 0,

    /// <summary>Nothing more to do. Not a failure: the sweep is finished.</summary>
    Done = 1,

    /// <summary>A null pointer, a negative length, or a handle that is not one.</summary>
    BadArgument = -1,

    /// <summary>The buffer offered is too small. The needed size is reported back.</summary>
    BufferTooSmall = -2,

    /// <summary>The wallet file is not one, or the password is wrong. Deliberately the same answer.</summary>
    CannotOpen = -3,

    /// <summary>The daemon's answer could not be read.</summary>
    BadResponse = -4,

    /// <summary>The call does not make sense in the state the session is in.</summary>
    WrongState = -5,

    /// <summary>Anything else. The message is available separately.</summary>
    Failed = -100,
}
