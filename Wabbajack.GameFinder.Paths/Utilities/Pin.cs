using System;
using System.Runtime.InteropServices;
namespace Wabbajack.GameFinder.Paths.Utilities;

/// <summary>
///     Pins an existing array to memory.
/// </summary>
/// <typeparam name="T">The type of element being pinned in native memory.</typeparam>
internal unsafe class Pin<T> : IDisposable where T : unmanaged
{
    /// <summary>
    ///     Pointer to the first value of the pinned array.
    /// </summary>
    public T* Pointer { get; private set; }

    // Handle keeping the object pinned.
    private GCHandle _handle;
    private bool _disposed;

    /// <summary>
    ///     Pins an array of values to the heap.
    /// </summary>
    /// <param name="value">
    ///     The values to be pinned on the heap.
    ///     Depending on runtime used, these may be copied; so please use <see cref="Pointer" /> property.
    ///     Do not use original array once passed to this function.
    /// </param>
    public Pin(T[] value)
    {
        _handle = GCHandle.Alloc(value, GCHandleType.Pinned);
        Pointer = (T*)_handle.AddrOfPinnedObject();
    }

    ~Pin() => Dispose();

    /// <inheritdoc />
    public void Dispose()
    {
        // Add disposed check
        if (_disposed)
            return;

        _disposed = true;
        if (_handle.IsAllocated)
            _handle.Free();

        Pointer = (T*)0;
        GC.SuppressFinalize(this);
    }
}
