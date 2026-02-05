using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Wabbajack.GameFinder.Paths.Utilities;

namespace Wabbajack.GameFinder.Paths;

/// <summary>
/// Implementation of <see cref="IFileSystem"/> for use with tests.
/// </summary>
[PublicAPI]
public partial class InMemoryFileSystem : BaseFileSystem
{
    private readonly InMemoryDirectoryEntry _rootDirectory;

    private readonly ConcurrentDictionary<AbsolutePath, InMemoryFileEntry> _files = new();
    private readonly ConcurrentDictionary<AbsolutePath, InMemoryDirectoryEntry> _directories = new();

    /// <summary>
    /// Constructor.
    /// </summary>
    public InMemoryFileSystem(IOSInformation? os = null)
        : this(
            new Dictionary<AbsolutePath, AbsolutePath>(),
            new Dictionary<KnownPath, AbsolutePath>(),
            false, os) { }

    private InMemoryFileSystem(
        Dictionary<AbsolutePath, AbsolutePath> pathMappings,
        Dictionary<KnownPath, AbsolutePath> knownPathMappings,
        bool convertCrossPlatformPaths,
        IOSInformation? os = null)
        : base(os ?? OSInformation.Shared, pathMappings, knownPathMappings, convertCrossPlatformPaths)
    {
        var root = OS.IsWindows ? "C:/" : "/";

        _rootDirectory = new InMemoryDirectoryEntry(
            AbsolutePath.FromSanitizedFullPath(root, this),
            null!
        );

        _directories[_rootDirectory.Path] = _rootDirectory;
    }

    #region Helper Functions

    /// <summary>
    /// Helper function to add a new file to the in-memory file system.
    /// </summary>
    /// <param name="path">Path to the file.</param>
    /// <param name="contents"></param>
    public void AddFile(AbsolutePath path, byte[] contents)
        => InternalAddFile(path, contents);

    /// <summary>
    /// Helper function to add a new file to the in-memory file system.
    /// </summary>
    /// <param name="path">Path to the file.</param>
    /// <param name="contents"></param>
    public void AddFile(AbsolutePath path, string contents)
        => InternalAddFile(path, Encoding.UTF8.GetBytes(contents));

    /// <summary>
    /// Adds an empty file to the in-memory file system.
    /// </summary>
    /// <param name="path">Path to the file.</param>
    public void AddEmptyFile(AbsolutePath path)
        => AddFile(path, Array.Empty<byte>());

    private InMemoryFileEntry InternalAddFile(AbsolutePath path, byte[] contents)
    {
        if (!path.InFolder(_rootDirectory.Path))
            throw new ArgumentException($"Path {path} is not in root directory {_rootDirectory.Path}");

        var directory = GetOrAddDirectory(path.Parent);
        var inMemoryFile = new InMemoryFileEntry(this, path, directory, contents);

        if (!_files.TryAdd(path, inMemoryFile))
            throw new ArgumentException($"An item with the same key has already been added. Key: {path}");

        if (!directory.Files.TryAdd(inMemoryFile.Path.RelativeTo(directory.Path), inMemoryFile))
            throw new ArgumentException($"An item with the same key has already been added. Key: {inMemoryFile.Path.RelativeTo(directory.Path)}");

        inMemoryFile.CreationTime = DateTime.UtcNow;
        inMemoryFile.LastWriteTime = DateTime.UtcNow;
        return inMemoryFile;
    }

    /// <summary>
    /// Adds a new directory to the in-memory file system.
    /// </summary>
    /// <param name="path">Path to the directory.</param>
    public void AddDirectory(AbsolutePath path)
        => GetOrAddDirectory(path);

    /// <summary>
    /// Adds multiple directories to the in-memory file system.
    /// </summary>
    /// <param name="paths">Paths to the directories</param>
    public void AddDirectories([InstantHandle] IEnumerable<AbsolutePath> paths)
    {
        foreach (var path in paths)
        {
            GetOrAddDirectory(path);
        }
    }

    private InMemoryDirectoryEntry GetOrAddDirectory(AbsolutePath path)
    {
        if (!path.InFolder(_rootDirectory.Path))
            throw new ArgumentException($"Path {path} is not in root directory {_rootDirectory.Path}");

        // directory already exists
        if (_directories.TryGetValue(path, out var existingDir))
            return existingDir;

        // directory doesn't exist, we have to create this directory and all
        // parent directories, using a scuffed top-to-bottom implementation for now
        var directoriesToCreate = new Stack<AbsolutePath>();

        var current = path;
        do
        {
            directoriesToCreate.Push(current);
            if (current == current.Parent)
                throw new UnreachableException("Infinite loop should not happen if our code is correct.");

            current = current.Parent;
        } while (current != _rootDirectory.Path);

        var currentParentDirectory = _rootDirectory;
        while (directoriesToCreate.TryPop(out var directoryPath))
        {
            if (!_directories.TryGetValue(directoryPath, out var directory))
            {
                directory = _directories.GetOrAdd(directoryPath, p => new InMemoryDirectoryEntry(p, _rootDirectory));

                currentParentDirectory.Directories.TryAdd(directory.Path.RelativeTo(currentParentDirectory.Path), directory);
            }

            currentParentDirectory = directory;
        }

        return _directories[path];
    }

    #endregion

    #region Implementation

    /// <inheritdoc/>
    public override IFileSystem CreateOverlayFileSystem(
        Dictionary<AbsolutePath, AbsolutePath> pathMappings,
        Dictionary<KnownPath, AbsolutePath> knownPathMappings,
        bool convertCrossPlatformPaths = false)
        => new InMemoryFileSystem(pathMappings, knownPathMappings, convertCrossPlatformPaths, OS);

    /// <inheritdoc />
    public override int ReadBytesRandomAccess(AbsolutePath path, Span<byte> bytes, long offset)
    {
        using var s = ReadFile(path);
        s.Seek(offset, SeekOrigin.Begin);
        return s.ReadAtLeast(bytes, bytes.Length, false);
    }

    /// <inheritdoc />
    public override async Task<int> ReadBytesRandomAccessAsync(AbsolutePath path, Memory<byte> bytes, long offset, CancellationToken cancellationToken = default)
    {
        await using var s = ReadFile(path);
        s.Seek(offset, SeekOrigin.Begin);
        return await s.ReadAtLeastAsync(bytes, bytes.Length, false, cancellationToken);
    }

    /// <inheritdoc/>
    protected override IFileEntry InternalGetFileEntry(AbsolutePath path)
    {
        if (_files.TryGetValue(path, out var file)) return file;

        var parentDirectory = GetOrAddDirectory(path);
        var inMemoryFile = new InMemoryFileEntry(this, path, parentDirectory, Array.Empty<byte>());
        return inMemoryFile;
    }

    /// <inheritdoc/>
    protected override IDirectoryEntry InternalGetDirectoryEntry(AbsolutePath path)
    {
        if (_directories.TryGetValue(path, out var directory)) return directory;

        var parentDirectory = InternalGetDirectoryEntry(path.Parent);
        var inMemoryDirectory = new InMemoryDirectoryEntry(path, (InMemoryDirectoryEntry)parentDirectory);
        return inMemoryDirectory;
    }

    /// <inheritdoc/>
    protected override IEnumerable<AbsolutePath> InternalEnumerateFiles(AbsolutePath directory, string pattern, bool recursive)
    {
        return InternalEnumerateFileEntries(directory, pattern, recursive).Select(x => x.Path);
    }

    /// <inheritdoc/>
    protected override IEnumerable<AbsolutePath> InternalEnumerateDirectories(AbsolutePath directory, string pattern, bool recursive)
    {
        if (!_directories.TryGetValue(directory, out var directoryEntry))
            yield break;

        foreach (var subDirectoryEntry in directoryEntry.Directories.Values)
        {
            if (!EnumeratorHelpers.MatchesPattern(pattern, subDirectoryEntry.Path.GetFullPath(), MatchType.Win32))
                continue;

            yield return subDirectoryEntry.Path;
            if (!recursive) continue;

            foreach (var subDirectoryPath in InternalEnumerateDirectories(subDirectoryEntry.Path, pattern, recursive))
            {
                yield return subDirectoryPath;
            }
        }
    }

    /// <inheritdoc/>
    protected override IEnumerable<IFileEntry> InternalEnumerateFileEntries(AbsolutePath directory, string pattern, bool recursive)
    {
        if (!_directories.TryGetValue(directory, out var directoryEntry))
            yield break;

        foreach (var fileEntry in directoryEntry.Files.Values)
        {
            if (!EnumeratorHelpers.MatchesPattern(pattern, fileEntry.Path.GetFullPath(), MatchType.Win32))
                continue;
            yield return fileEntry;
        }

        if (!recursive) yield break;
        foreach (var subDirectoryEntry in directoryEntry.Directories.Values)
        {
            foreach (var subDirectoryFileEntry in InternalEnumerateFileEntries(subDirectoryEntry.Path, pattern, recursive))
            {
                yield return subDirectoryFileEntry;
            }
        }
    }

    /// <inheritdoc/>
    protected override Stream InternalOpenFile(AbsolutePath path, FileMode mode, FileAccess access, FileShare share)
    {
        var inMemoryFileEntry = InternalCreateFile(path, mode, access, 0);
        return access switch
        {
            FileAccess.Read => inMemoryFileEntry.CreateReadStream(),
            FileAccess.Write => inMemoryFileEntry.CreateWriteStream(),
            FileAccess.ReadWrite => inMemoryFileEntry.CreateReadWriteStream(),
        };
    }

    private InMemoryFileEntry InternalCreateFile(AbsolutePath path, FileMode mode, FileAccess access, ulong fileSize)
    {
        if (access == FileAccess.Read && mode != FileMode.Open && mode != FileMode.OpenOrCreate)
        {
            throw new ArgumentException($"Access can't be Read with mode {mode}", nameof(access));
        }

        InMemoryFileEntry? inMemoryFileEntry;
        switch (mode)
        {
            case FileMode.Open:
            {
                _files.TryGetValue(path, out inMemoryFileEntry);
                break;
            }
            case FileMode.Create:
            {
                if (!_files.TryGetValue(path, out inMemoryFileEntry))
                    inMemoryFileEntry = InternalAddFile(path, new byte[fileSize]);
                else
                    inMemoryFileEntry.SetContents(Array.Empty<byte>());
                break;
            }
            case FileMode.CreateNew:
            {
                if (_files.ContainsKey(path))
                    throw new IOException($"{FileMode.CreateNew} can't be used if the file already exists!");
                inMemoryFileEntry = InternalAddFile(path, new byte[fileSize]);
                break;
            }
            case FileMode.OpenOrCreate:
            {
                if (!_files.TryGetValue(path, out inMemoryFileEntry))
                    inMemoryFileEntry = InternalAddFile(path, new byte[fileSize]);
                break;
            }
            case FileMode.Truncate:
            {
                if (_files.TryGetValue(path, out inMemoryFileEntry))
                    inMemoryFileEntry.SetContents(Array.Empty<byte>());
                break;
            }
            case FileMode.Append:
            default: throw new NotImplementedException();
        }

        if (inMemoryFileEntry is null)
            throw new FileNotFoundException($"File \"{path.GetFullPath()}\" does not exist");

        inMemoryFileEntry.LastWriteTime = DateTime.UtcNow;
        return inMemoryFileEntry;
    }

    /// <inheritdoc/>
    protected override void InternalCreateDirectory(AbsolutePath path)
        => AddDirectory(path);

    /// <inheritdoc/>
    protected override bool InternalDirectoryExists(AbsolutePath path)
        => _directories.ContainsKey(path);

    /// <inheritdoc/>
    protected override bool InternalFileExists(AbsolutePath path)
        => _files.ContainsKey(path);

    /// <inheritdoc/>
    protected override void InternalDeleteFile(AbsolutePath path)
    {
        if (!_files.TryGetValue(path, out var file))
            throw new FileNotFoundException($"File at {path} does not exist!");

        var parentDirectory = file.ParentDirectory;
        parentDirectory.Files.TryRemove(path.RelativeTo(parentDirectory.Path), out _);
        _files.TryRemove(path, out _);
    }

    /// <inheritdoc/>
    protected override void InternalDeleteDirectory(AbsolutePath path, bool recursive)
    {
        if (!_directories.TryGetValue(path, out var directory))
            throw new DirectoryNotFoundException($"Directory at {path} does not exist!");

        if (recursive)
        {
            foreach (var kv in directory.Files)
            {
                var (_, file) = kv;
                _files.TryRemove(file.Path, out _);
            }

            foreach (var kv in directory.Directories)
            {
                var (_, subDirectory) = kv;
                InternalDeleteDirectoryRecursive(subDirectory.Path);
            }
        }
        else
        {
            if (directory.Files.Any() || directory.Directories.Any())
                throw new IOException($"The directory at {path} is not empty!");

        }

        var parentDirectory = directory.ParentDirectory;
        parentDirectory.Directories.TryRemove(path.RelativeTo(parentDirectory.Path), out _);
        _directories.TryRemove(path, out _);
    }

    private void InternalDeleteDirectoryRecursive(AbsolutePath path)
    {
        // Don't throw if the directory doesn't exist
        if (!_directories.TryGetValue(path, out var directory))
            return;

        foreach (var kv in directory.Files)
        {
            var (_, file) = kv;
            _files.TryRemove(file.Path, out _);
        }

        foreach (var kv in directory.Directories)
        {
            var (_, subDirectory) = kv;
            InternalDeleteDirectoryRecursive(subDirectory.Path);
        }

        // Don't remove this from parent.Directories since parent is iterating over it

        _directories.TryRemove(path, out _);
    }


    /// <inheritdoc/>
    protected override void InternalMoveFile(AbsolutePath source, AbsolutePath dest, bool overwrite)
    {
        if (!_files.TryGetValue(source, out var sourceFile))
            throw new FileNotFoundException($"File does not exist at path {source}");

        if (_files.TryGetValue(dest, out var destFile))
        {
            if (!overwrite)
                throw new IOException($"Destination file at {dest} already exist!");

            destFile.SetContents(sourceFile.GetContents());
        }
        else
        {
            destFile = InternalAddFile(dest, sourceFile.GetContents());
        }

        destFile.CreationTime = sourceFile.CreationTime;
        destFile.LastWriteTime = sourceFile.LastWriteTime;
        destFile.IsReadOnly = sourceFile.IsReadOnly;

        DeleteFile(source);
    }

    /// <inheritdoc/>
    protected override unsafe MemoryMappedFileHandle InternalCreateMemoryMappedFile(AbsolutePath absPath, FileMode mode, MemoryMappedFileAccess access, ulong fileSize)
    {
        var fileAccess = access switch
        {
            MemoryMappedFileAccess.Read => FileAccess.Read,
            MemoryMappedFileAccess.Write => FileAccess.Write,
            MemoryMappedFileAccess.ReadWrite => FileAccess.ReadWrite,
            _ => throw new ArgumentOutOfRangeException(nameof(access), access, null)
        };

        var file = InternalCreateFile(absPath, mode, fileAccess, fileSize);
        var buffer = file.GetContents();
        var pin = new Pin<byte>(buffer);
        return new MemoryMappedFileHandle(pin.Pointer, (nuint)file.Size.Value, pin);
    }

    #endregion
}
