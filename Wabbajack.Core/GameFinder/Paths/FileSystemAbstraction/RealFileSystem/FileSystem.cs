using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Wabbajack.GameFinder.Paths.Utilities;

namespace Wabbajack.GameFinder.Paths;

/// <summary>
/// Default implementation of <see cref="IFileSystem"/>.
/// </summary>
[PublicAPI]
public partial class FileSystem : BaseFileSystem
{
    /// <summary>
    /// Shared instance of the default implementation.
    /// </summary>
    public static readonly IFileSystem Shared = new FileSystem();

    private static readonly TimeSpan DefaultTimeout;

    static FileSystem()
    {
        // Windows CI has horrible IO performance
        if (OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable("CI") == "true")
        {
            DefaultTimeout = TimeSpan.FromSeconds(3);
        }
        else
        {
            DefaultTimeout = TimeSpan.FromMilliseconds(1000);
        }
    }

    private static EnumerationOptions GetSearchOptions(bool recursive) => new()
    {
        AttributesToSkip = 0,
        RecurseSubdirectories = recursive,
        MatchType = MatchType.Win32
    };

    internal FileSystem() { }

    #region Implementation

    internal FileSystem(
        Dictionary<AbsolutePath, AbsolutePath> pathMappings,
        Dictionary<KnownPath, AbsolutePath> knownPathMappings,
        bool convertCrossPlatformPaths)
        : base(OSInformation.Shared, pathMappings, knownPathMappings, convertCrossPlatformPaths) { }

    /// <inheritdoc/>
    public override IFileSystem CreateOverlayFileSystem(
        Dictionary<AbsolutePath, AbsolutePath> pathMappings,
        Dictionary<KnownPath, AbsolutePath> knownPathMappings,
        bool convertCrossPlatformPaths = false)
        => new FileSystem(pathMappings, knownPathMappings, convertCrossPlatformPaths);

    /// <inheritdoc />
    public override int ReadBytesRandomAccess(AbsolutePath path, Span<byte> bytes, long offset)
    {
        using var handle = File.OpenHandle(path.GetFullPath(), options: FileOptions.RandomAccess);
        var readTotal = 0;
        while (readTotal < bytes.Length)
        {
            var read = RandomAccess.Read(handle, bytes.Slice(readTotal), offset + readTotal);
            if (read <= 0) break;
            readTotal += read;
        }
        return readTotal;
    }

    /// <inheritdoc />
    public override async Task<int> ReadBytesRandomAccessAsync(AbsolutePath absolutePath, Memory<byte> bytes, long offset, CancellationToken cancellationToken = default)
    {
        using var handle = File.OpenHandle(absolutePath.GetFullPath(), options: FileOptions.RandomAccess);
        var readTotal = 0;
        while (readTotal < bytes.Length)
        {
            var read = await RandomAccess.ReadAsync(handle, bytes.Slice(readTotal), offset + readTotal, cancellationToken);
            if (read <= 0) break;
            readTotal += read;
        }
        return readTotal;
    }

    /// <inheritdoc/>
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    public override void SetUnixFileMode(AbsolutePath absolutePath, UnixFileMode flags)
    {
        if (!OS.IsUnix())
            return;

        File.SetUnixFileMode(absolutePath.GetFullPath(), flags);
    }

    /// <inheritdoc/>
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    public override UnixFileMode GetUnixFileMode(AbsolutePath absolutePath)
    {
        return OS.IsUnix() ? File.GetUnixFileMode(absolutePath.GetFullPath()) : default;
    }

    /// <inheritdoc/>
    protected override IFileEntry InternalGetFileEntry(AbsolutePath path)
        => new FileEntry(this, path);

    /// <inheritdoc/>
    protected override IDirectoryEntry InternalGetDirectoryEntry(AbsolutePath path)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    protected override IEnumerable<AbsolutePath> InternalEnumerateFiles(AbsolutePath directory, string pattern, bool recursive)
    {
        var options = GetSearchOptions(recursive);
        using var enumerator = new FilesEnumerator(directory.GetFullPath(), pattern, options);
        while (enumerator.MoveNext())
        {
            var item = enumerator.Current;
            if (item.IsDirectory) continue;
            yield return AbsolutePath.FromSanitizedFullPath(PathHelpers.JoinParts(enumerator.CurrentDirectory, item.FileName), this);
        }
    }

    /// <inheritdoc/>
    protected override IEnumerable<AbsolutePath> InternalEnumerateDirectories(AbsolutePath directory, string pattern, bool recursive)
    {
        var options = GetSearchOptions(recursive);
        var enumerator = new DirectoriesEnumerator(directory.GetFullPath(), "*", options);
        while (enumerator.MoveNext())
        {
            var item = enumerator.Current;
            yield return AbsolutePath.FromSanitizedFullPath(PathHelpers.JoinParts(enumerator.CurrentDirectory, item), this);
        }
    }

    /// <inheritdoc/>
    protected override IEnumerable<IFileEntry> InternalEnumerateFileEntries(AbsolutePath directory, string pattern, bool recursive)
    {
        var options = GetSearchOptions(recursive);
        var enumerator = new FilesEnumeratorEx(directory.GetFullPath(), pattern, options);

        while (enumerator.MoveNext())
        {
            var item = enumerator.Current;
            if (item.IsDirectory) continue;
            yield return new FileEntry(this, AbsolutePath.FromSanitizedFullPath(PathHelpers.JoinParts(enumerator.CurrentDirectory, item.FileName), this));
        }
    }

    /// <inheritdoc/>
    protected override Stream InternalOpenFile(AbsolutePath path, FileMode mode, FileAccess access, FileShare share)
        => File.Open(path.GetFullPath(), mode, access, share);

    /// <inheritdoc/>
    protected override void InternalCreateDirectory(AbsolutePath path)
        => Directory.CreateDirectory(path.GetFullPath());

    /// <inheritdoc/>
    protected override bool InternalDirectoryExists(AbsolutePath path)
        => Directory.Exists(path.GetFullPath());

    /// <inheritdoc/>
    protected override void InternalDeleteDirectory(AbsolutePath path, bool recursive)
    {
        var fullPath = path.GetFullPath();
        if (!Directory.Exists(fullPath)) return;

        if (!recursive)
        {
            var hasChildren = EnumerateFiles(path, recursive: false).Any() ||
                          EnumerateDirectories(path, recursive: false).Any();
            if (hasChildren)
            {
                throw new IOException($"The directory {fullPath} is not empty and {nameof(recursive)} is set to {recursive}");
            }
        }

        foreach (var subDirectories in Directory.GetDirectories(fullPath))
        {
            InternalDeleteDirectory(FromUnsanitizedFullPath(subDirectories), recursive);
        }

        try
        {
            var di = new DirectoryInfo(fullPath);
            if (di.Attributes.HasFlag(FileAttributes.ReadOnly))
                di.Attributes &= ~FileAttributes.ReadOnly;

            var attempts = 0;
        TopParent:

            try
            {
                Directory.Delete(fullPath, true);
            }
            catch (IOException ioException)
            {
                if (attempts > 10)
                    throw new Exception($"Unable to delete directory {fullPath} after {attempts} attempts\n{ioException}", ioException);

                Thread.Sleep(DefaultTimeout);
                attempts++;
                goto TopParent;
            }
        }
        catch (UnauthorizedAccessException)
        {
            Directory.Delete(fullPath, true);
        }
    }

    /// <inheritdoc/>
    protected override bool InternalFileExists(AbsolutePath path)
        => File.Exists(path.GetFullPath());

    /// <inheritdoc/>
    protected override void InternalDeleteFile(AbsolutePath path)
    {
        var fullPath = path.GetFullPath();
        if (File.Exists(fullPath))
        {
            try
            {
                File.Delete(fullPath);
            }
            catch (UnauthorizedAccessException)
            {
                var fi = new FileInfo(fullPath);

                if (fi.IsReadOnly)
                {
                    fi.IsReadOnly = false;
                    File.Delete(fullPath);
                }
                else
                {
                    throw;
                }
            }
        }

        if (Directory.Exists(fullPath))
            DeleteDirectory(path, true);
    }

    /// <inheritdoc/>
    protected override void InternalMoveFile(AbsolutePath source, AbsolutePath dest, bool overwrite)
        => File.Move(source.GetFullPath(), dest.GetFullPath(), overwrite);

    /// <inheritdoc/>
    protected override unsafe MemoryMappedFileHandle InternalCreateMemoryMappedFile(AbsolutePath absPath, FileMode mode, MemoryMappedFileAccess access, ulong fileSize)
    {
        var fs = new FileStream(absPath.GetFullPath(), new FileStreamOptions
        {
            Mode = mode,
            Access = GetFileAccess(access),
            Share = FileShare.Read,
            BufferSize = 0,
            PreallocationSize = (long)fileSize
        });
        MemoryMappedFile? mmf = null;
        MemoryMappedViewAccessor? view = null;
        try
        {
            // Note(sewer):
            // If the file is empty, we can't create a memory mapped file.
            // So instead return a null pointer.
            // This pointer is null because this helps us detect invalid read/write via Access Violation (0xC0000005).
            if (fileSize == 0)
                fileSize = (ulong)fs.Length;

            if (fileSize == 0)
            {
                fs.Dispose(); // Dispose early.
                return new MemoryMappedFileHandle((byte*)0, 0, null);
            }

            mmf = MemoryMappedFile.CreateFromFile(fs, null, (long)fileSize, access, HandleInheritability.None, false);
            view = mmf.CreateViewAccessor(0, 0, access);
            var ptrData = (byte*)view.SafeMemoryMappedViewHandle.DangerousGetHandle();
            return new MemoryMappedFileHandle(ptrData, (nuint)fileSize, new FilesystemMemoryMappedHandle(view, mmf));
        }
        catch
        {
            fs.Dispose();
            mmf?.Dispose();
            view?.Dispose();
            throw;
        }
    }

    #endregion

    // Note(Sewer): This is taken straight from Runtime.
    // https://github.com/dotnet/runtime/blob/3a78480ae21a909ecd3ac9edfd2aa3e63dd890fb/src/libraries/System.IO.MemoryMappedFiles/src/System/IO/MemoryMappedFiles/MemoryMappedFile.cs#L486
    private static FileAccess GetFileAccess(MemoryMappedFileAccess access)
    {
        switch (access)
        {
            case MemoryMappedFileAccess.Read:
            case MemoryMappedFileAccess.ReadExecute:
                return FileAccess.Read;

            case MemoryMappedFileAccess.ReadWrite:
            case MemoryMappedFileAccess.CopyOnWrite:
            case MemoryMappedFileAccess.ReadWriteExecute:
                return FileAccess.ReadWrite;

            case MemoryMappedFileAccess.Write:
            default:
                Debug.Assert(access == MemoryMappedFileAccess.Write);
                return FileAccess.Write;
        }
    }
}
