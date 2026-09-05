using System.Runtime.InteropServices;

namespace Moonlight.Core.Abi;

/// <summary>
/// Managed objects, seen from C as opaque pointers.
/// </summary>
/// <remarks>
/// A GCHandle keeps the object alive and turns into a pointer the caller can hold.
/// The check on the way back is the point: a stale or invented pointer must come
/// back as BadArgument rather than dereferencing whatever is at that address, so
/// every handle this hands out is recorded and looked up again on return.
/// </remarks>
internal static class Handles
{
    private static readonly object Gate = new();
    private static readonly HashSet<nint> Live = [];

    public static nint Wrap(object value)
    {
        nint handle = GCHandle.ToIntPtr(GCHandle.Alloc(value));

        lock (Gate) Live.Add(handle);

        return handle;
    }

    /// <summary>The object behind a pointer, or null if it is not one of ours.</summary>
    public static T? Lookup<T>(nint handle)
        where T : class
    {
        if (handle == 0) return null;

        lock (Gate)
        {
            if (!Live.Contains(handle)) return null;
        }

        return GCHandle.FromIntPtr(handle).Target as T;
    }

    /// <summary>
    /// Releases a handle, and says whether it was live. Freeing twice is answered
    /// rather than crashed on, since that is the mistake a caller actually makes.
    /// </summary>
    public static object? Release(nint handle)
    {
        if (handle == 0) return null;

        lock (Gate)
        {
            if (!Live.Remove(handle)) return null;
        }

        GCHandle allocated = GCHandle.FromIntPtr(handle);
        object? target = allocated.Target;
        allocated.Free();

        return target;
    }
}
