using System.Runtime.InteropServices;

namespace Moonlight.Core.Abi;

/// <summary>
/// The exported names, and nothing else. Each one hands straight to <see cref="Api"/>.
/// </summary>
/// <remarks>
/// This is the only file in the sterile tree allowed to carry UnmanagedCallersOnly,
/// and the purity gate enforces that. The rule is about direction: DllImport reaches
/// for native code we would then depend on, while an export only lets somebody else
/// call us and adds no dependency at all.
///
/// The entry point names are the interface. They are declared once here and
/// described in moonlight.h, and renaming one breaks every caller — so they do not
/// get renamed.
/// </remarks>
internal static unsafe class Exports
{
    [UnmanagedCallersOnly(EntryPoint = "moonlight_abi_version")]
    public static int AbiVersion() => Api.AbiVersion();

    [UnmanagedCallersOnly(EntryPoint = "moonlight_wallet_open")]
    public static Status WalletOpen(byte* path, byte* password, nint* handle)
        => Api.WalletOpen(path, password, handle);

    [UnmanagedCallersOnly(EntryPoint = "moonlight_wallet_close")]
    public static Status WalletClose(nint handle) => Api.WalletClose(handle);

    [UnmanagedCallersOnly(EntryPoint = "moonlight_wallet_address")]
    public static Status WalletAddress(nint handle, byte* buffer, int capacity, int* needed)
        => Api.WalletAddress(handle, buffer, capacity, needed);

    [UnmanagedCallersOnly(EntryPoint = "moonlight_wallet_balance")]
    public static Status WalletBalance(nint handle, ulong* total, ulong* unlocked)
        => Api.WalletBalance(handle, total, unlocked);

    [UnmanagedCallersOnly(EntryPoint = "moonlight_wallet_scanned_height")]
    public static Status WalletScannedHeight(nint handle, ulong* height)
        => Api.WalletScannedHeight(handle, height);

    [UnmanagedCallersOnly(EntryPoint = "moonlight_wallet_save")]
    public static Status WalletSave(nint handle) => Api.WalletSave(handle);

    [UnmanagedCallersOnly(EntryPoint = "moonlight_sync_next")]
    public static Status SyncNext(
        nint handle,
        byte* path, int pathCapacity, int* pathNeeded,
        byte* body, int bodyCapacity, int* bodyNeeded)
        => Api.SyncNext(handle, path, pathCapacity, pathNeeded, body, bodyCapacity, bodyNeeded);

    [UnmanagedCallersOnly(EntryPoint = "moonlight_sync_supply")]
    public static Status SyncSupply(nint handle, byte* response, int length)
        => Api.SyncSupply(handle, response, length);

    [UnmanagedCallersOnly(EntryPoint = "moonlight_sync_failed")]
    public static Status SyncFailed(nint handle) => Api.SyncFailed(handle);

    [UnmanagedCallersOnly(EntryPoint = "moonlight_sync_restart")]
    public static Status SyncRestart(nint handle) => Api.SyncRestart(handle);

    [UnmanagedCallersOnly(EntryPoint = "moonlight_sync_progress")]
    public static Status SyncProgress(nint handle, ulong* scanned, ulong* chainHeight, int* outputs)
        => Api.SyncProgress(handle, scanned, chainHeight, outputs);
}
