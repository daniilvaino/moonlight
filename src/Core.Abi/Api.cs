using System.Runtime.InteropServices;
using System.Text;
using Moonlight.Wallet;

namespace Moonlight.Core.Abi;

/// <summary>
/// What the C interface does. <see cref="Exports"/> is the door; this is the room.
/// </summary>
/// <remarks>
/// Separate because a method carrying UnmanagedCallersOnly cannot be called from
/// managed code at all — the language forbids it — so putting the behaviour behind
/// the attribute would make the whole interface testable only through a native
/// build. Here it is ordinary code with ordinary tests, and the exports are one
/// line each.
///
/// Every method takes and returns only blittable values, and none of them throws:
/// a managed exception unwinding through a C frame is undefined behaviour rather
/// than an error anyone can handle.
///
/// Nothing here allocates memory the caller has to free. Where a result is bytes or
/// text, the caller offers a buffer and is told the size needed if it was too
/// small — one rule, no ownership to get wrong, and it works the same from Swift,
/// Rust, Python or C.
///
/// The session drives <see cref="SyncEngine"/>, so a host does its own networking:
/// ask for the next request, send it however it likes, hand the answer back.
/// </remarks>
public static unsafe class Api
{
    /// <summary>The interface version, so a host can refuse a library it does not know.</summary>
    private const int Version = 1;

    
    public static int AbiVersion() => Version;

    /// <summary>
    /// Opens a wallet file. The handle it writes back is what every other call
    /// takes, and must be closed with moonlight_wallet_close.
    /// </summary>
    
    public static Status WalletOpen(byte* path, byte* password, nint* handle)
    {
        if (path is null || password is null || handle is null) return Status.BadArgument;

        *handle = 0;

        try
        {
            WalletSession session = WalletSession.Open(Text(path), Text(password));

            *handle = Handles.Wrap(new Session(session));
            return Status.Ok;
        }
        catch (Exception e)
        {
            return e is FormatException or IOException ? Status.CannotOpen : Status.Failed;
        }
    }

    
    public static Status WalletClose(nint handle)
    {
        if (Handles.Release(handle) is not Session session) return Status.BadArgument;

        session.Wallet.Dispose();
        return Status.Ok;
    }

    /// <summary>The wallet's main address, written as UTF-8 with a terminating zero.</summary>
    
    public static Status WalletAddress(nint handle, byte* buffer, int capacity, int* needed)
        => Handles.Lookup<Session>(handle) is not Session session
            ? Status.BadArgument
            : Write(session.Wallet.Account.Address.Encode(), buffer, capacity, needed);

    /// <summary>Total and unlocked, in atomic units.</summary>
    
    public static Status WalletBalance(nint handle, ulong* total, ulong* unlocked)
    {
        if (Handles.Lookup<Session>(handle) is not Session session) return Status.BadArgument;
        if (total is null || unlocked is null) return Status.BadArgument;

        try
        {
            Balance balance = session.Wallet.Balance();

            *total = balance.Total;
            *unlocked = balance.Unlocked;

            return Status.Ok;
        }
        catch (Exception)
        {
            return Status.Failed;
        }
    }

    
    public static Status WalletScannedHeight(nint handle, ulong* height)
    {
        if (Handles.Lookup<Session>(handle) is not Session session) return Status.BadArgument;
        if (height is null) return Status.BadArgument;

        *height = session.Wallet.State.ScannedHeight;
        return Status.Ok;
    }

    
    public static Status WalletSave(nint handle)
    {
        if (Handles.Lookup<Session>(handle) is not Session session) return Status.BadArgument;

        try
        {
            session.Wallet.Save();
            return Status.Ok;
        }
        catch (Exception)
        {
            return Status.Failed;
        }
    }

    /// <summary>
    /// What to send next: the path into the buffer, the body into another. Returns
    /// Done when the sweep is finished, at which point moonlight_sync_restart begins
    /// another.
    /// </summary>
    
    public static Status SyncNext(
        nint handle,
        byte* path, int pathCapacity, int* pathNeeded,
        byte* body, int bodyCapacity, int* bodyNeeded)
    {
        if (Handles.Lookup<Session>(handle) is not Session session) return Status.BadArgument;

        try
        {
            if (session.Engine.Next() is not SyncRequest request) return Status.Done;

            Status wrote = Write(request.Path, path, pathCapacity, pathNeeded);
            Status carried = Write(request.Body, body, bodyCapacity, bodyNeeded);

            // Both sizes are reported even when one does not fit, so a caller that
            // has to grow a buffer only makes the call twice rather than four times.
            return wrote != Status.Ok ? wrote : carried;
        }
        catch (Exception)
        {
            return Status.Failed;
        }
    }

    /// <summary>The answer to the last request.</summary>
    
    public static Status SyncSupply(nint handle, byte* response, int length)
    {
        if (Handles.Lookup<Session>(handle) is not Session session) return Status.BadArgument;
        if (length < 0 || (response is null && length != 0)) return Status.BadArgument;

        try
        {
            session.Engine.Supply(new ReadOnlySpan<byte>(response, length));
            return Status.Ok;
        }
        catch (InvalidOperationException)
        {
            return Status.WrongState;
        }
        catch (Exception)
        {
            return Status.BadResponse;
        }
    }

    /// <summary>
    /// The last request could not be sent. Answers Ok when the engine can carry on
    /// without it and Failed when the caller has to deal with it.
    /// </summary>
    
    public static Status SyncFailed(nint handle)
        => Handles.Lookup<Session>(handle) is not Session session
            ? Status.BadArgument
            : session.Engine.Failed(new IOException("the host could not send the request")) ? Status.Ok : Status.Failed;

    /// <summary>Begins another sweep, which is what a warm wallet does every interval.</summary>
    
    public static Status SyncRestart(nint handle)
    {
        if (Handles.Lookup<Session>(handle) is not Session session) return Status.BadArgument;

        session.Engine.Restart();
        return Status.Ok;
    }

    
    public static Status SyncProgress(nint handle, ulong* scanned, ulong* chainHeight, int* outputs)
    {
        if (Handles.Lookup<Session>(handle) is not Session session) return Status.BadArgument;
        if (scanned is null || chainHeight is null || outputs is null) return Status.BadArgument;

        SyncProgress progress = session.Engine.Progress;

        *scanned = progress.Height;
        *chainHeight = progress.ChainHeight;
        *outputs = progress.Outputs;

        return Status.Ok;
    }

    /// <summary>A wallet and the engine reading the chain into it.</summary>
    private sealed class Session(WalletSession wallet)
    {
        public WalletSession Wallet { get; } = wallet;

        public SyncEngine Engine { get; } = new(wallet.State);
    }

    private static string Text(byte* value) => Marshal.PtrToStringUTF8((nint)value) ?? "";

    private static Status Write(string value, byte* buffer, int capacity, int* needed)
        => Write(Encoding.UTF8.GetBytes(value), buffer, capacity, needed, terminate: true);

    private static Status Write(ReadOnlySpan<byte> value, byte* buffer, int capacity, int* needed)
        => Write(value, buffer, capacity, needed, terminate: false);

    /// <summary>
    /// Into the caller's buffer, or the size it should have been. Text is terminated
    /// so it can be used as a C string; bytes are not, since a length is the only
    /// thing that describes them.
    /// </summary>
    private static Status Write(ReadOnlySpan<byte> value, byte* buffer, int capacity, int* needed, bool terminate)
    {
        int size = value.Length + (terminate ? 1 : 0);

        if (needed is not null) *needed = size;
        if (capacity < 0) return Status.BadArgument;
        if (buffer is null || capacity < size) return Status.BufferTooSmall;

        value.CopyTo(new Span<byte>(buffer, capacity));
        if (terminate) buffer[value.Length] = 0;

        return Status.Ok;
    }
}
