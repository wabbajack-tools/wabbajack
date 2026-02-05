using System;
using JetBrains.Annotations;

namespace Wabbajack.GameFinder.Paths;

/// <summary>
///     This represents a 'handle' to an underlying MemoryMappedFile.
/// </summary>
[PublicAPI]
public readonly unsafe struct MemoryMappedFileHandle : IDisposable
{
    /// <summary>
    ///     Address of the data represented by this handle.
    /// </summary>
    public byte* Pointer { get; }

    /// <summary>
    ///     Length of the data represented by this handle.
    /// </summary>
    public nuint Length { get; }
    private readonly IDisposable? _owner;

    /// <inheritdoc />
    public MemoryMappedFileHandle() : this((byte*)0, 0, null) { }

    /// <summary>
    ///    Creates a handle for a new MemoryMappedFile.
    /// </summary>
    /// <param name="pointer">Pointer to the mapped data.</param>
    /// <param name="length">Length of the data represented by the handle.</param>
    /// <param name="owner">Whatever holds the raw handle under the hood.</param>
    public MemoryMappedFileHandle(byte* pointer, nuint length, IDisposable? owner)
    {
        Pointer = pointer;
        Length = length;
        _owner = owner;
    }

    /// <summary>
    ///     Returns the data in span form.
    /// </summary>
    public Span<byte> AsSpan() => new(Pointer, (int)Length);

    /// <inheritdoc />
    public void Dispose() => _owner?.Dispose();
}
