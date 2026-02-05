using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.GameFinder.Paths.FileProviders;
using Wabbajack.GameFinder.Paths.Utilities;

namespace Wabbajack.GameFinder.Paths;

/// <summary>
/// A file system that reads through to an upstream IFileSystem and falls back to
/// one or more read-only sources mounted at given mount points. On the first write to a path that
/// only exists in a read-only source, the file is copied into the upstream file system (copy-on-write)
/// and subsequent operations use the upstream file. Deleting a file marks it deleted so read-only
/// sources are hidden until the file is recreated.
/// </summary>
public sealed class ReadOnlySourcesFileSystem : BaseFileSystem, IReadOnlyFileSystem
{
    private readonly IFileSystem _upstream;
    private readonly IReadOnlyList<IReadOnlyFileSource> _sources;
    // Thread-safe sets/maps for concurrent access in tests and callers
    private readonly ConcurrentDictionary<AbsolutePath, byte> _deleted = new();
    private readonly ConcurrentDictionary<AbsolutePath, (IReadOnlyFileSource source, RelativePath rel)> _sourceMap = new();

    // Filesystem creation timestamp (used as initial timestamp for RO entries)
    private readonly DateTime _fsCreationTimeUtc = DateTime.UtcNow;
    // Per-file last write timestamps for in-memory/virtual entries
    private readonly ConcurrentDictionary<AbsolutePath, long> _lastWriteTicksUtc = new();

    public ReadOnlySourcesFileSystem(IFileSystem upstream, IEnumerable<IReadOnlyFileSource> sources,
        Dictionary<AbsolutePath, AbsolutePath> pathMappings,
        Dictionary<KnownPath, AbsolutePath> knownPathMappings) : 
        base(OSInformation.Shared, pathMappings, knownPathMappings, false)
    {
        _upstream = upstream;
        _sources = sources.ToArray();
    }
    
    public ReadOnlySourcesFileSystem(IFileSystem upstream, IEnumerable<IReadOnlyFileSource> sources)
    {
        _upstream = upstream;
        _sources = sources.ToArray();
    }

    public override IFileSystem CreateOverlayFileSystem(
        Dictionary<AbsolutePath, AbsolutePath> pathMappings,
        Dictionary<KnownPath, AbsolutePath> knownPathMappings,
        bool convertCrossPlatformPaths = false)
    {
        return new ReadOnlySourcesFileSystem(_upstream, _sources, pathMappings, knownPathMappings);
    }

    private static AbsolutePath Normalize(AbsolutePath p) => p;
    private bool IsDeleted(AbsolutePath p) => _deleted.ContainsKey(Normalize(p));
    private void MarkDeleted(AbsolutePath p) { var n = Normalize(p); _deleted[n] = 1; _sourceMap.TryRemove(n, out _); }
    private void ClearDeleted(AbsolutePath p) { _deleted.TryRemove(Normalize(p), out _); }
    private void ClearDeletionIfUpstreamPresent(AbsolutePath p) { if (_upstream.FileExists(p)) _deleted.TryRemove(Normalize(p), out _); }

    private void TouchFile(AbsolutePath path)
    {
        var ticks = DateTime.UtcNow.Ticks;
        _lastWriteTicksUtc[Normalize(path)] = ticks;
    }

    public override int ReadBytesRandomAccess(AbsolutePath path, Span<byte> bytes, long offset)
    {
        ClearDeletionIfUpstreamPresent(path);
        if (IsDeleted(path))
            throw new FileNotFoundException($"File not found: {path}");

        if (_upstream.FileExists(path))
            return _upstream.ReadBytesRandomAccess(path, bytes, offset);

        if (TryResolveSource(path, out var src, out var rel))
        if (TryResolveSource(path, out var source, out var relPath))
        {
            using var s = source.OpenRead(relPath);
            s.Seek(offset, SeekOrigin.Begin);
            return s.Read(bytes);
        }
        throw new FileNotFoundException($"File not found: {path}");
    }
    public override async Task<int> ReadBytesRandomAccessAsync(AbsolutePath path, Memory<byte> bytes, long offset, CancellationToken cancellationToken = default)
    {
        ClearDeletionIfUpstreamPresent(path);
        if (IsDeleted(path))
            throw new FileNotFoundException($"File not found: {path}");

        if (_upstream.FileExists(path))
            return await _upstream.ReadBytesRandomAccessAsync(path, bytes, offset, cancellationToken);

        if (TryResolveSource(path, out var src, out var rel))
        {
            await using var s = src.OpenRead(rel);
            if (offset > 0) s.Seek(offset, SeekOrigin.Begin);
            return await s.ReadAtLeastAsync(bytes, bytes.Length, false, cancellationToken);
        }

        throw new FileNotFoundException($"File not found: {path}");
    }

    protected override IFileEntry InternalGetFileEntry(AbsolutePath path)
    {
        ClearDeletionIfUpstreamPresent(path);
        if (IsDeleted(path))
            return _upstream.GetFileEntry(path);

        if (_upstream.FileExists(path))
            return _upstream.GetFileEntry(path);

        if (TryResolveSource(path, out var src, out var rel))
        {
            using var s = src.OpenRead(rel);
            var len = (ulong)s.Length;
            return new VirtualReadOnlyFileEntry(this, path, len);
        }

        return _upstream.GetFileEntry(path);
    }

    protected override IDirectoryEntry InternalGetDirectoryEntry(AbsolutePath path)
    {
        if (_upstream.DirectoryExists(path))
            return _upstream.GetDirectoryEntry(path);

        if (SourceHasAnyUnder(path))
            return new VirtualDirectoryEntry();

        return _upstream.GetDirectoryEntry(path);
    }

    protected override IEnumerable<AbsolutePath> InternalEnumerateFiles(AbsolutePath directory, string pattern, bool recursive)
    {
        var set = new HashSet<AbsolutePath>();
        if (_upstream.DirectoryExists(directory))
        foreach (var f in _upstream.EnumerateFiles(directory, pattern, recursive))
        {
            if (IsDeleted(f)) continue;
            if (set.Add(f)) yield return f;
        }

        foreach (var p in EnumerateSourceFilesUnder(directory, recursive))
        {
            if (IsDeleted(p)) continue;
            if (!EnumeratorHelpers.MatchesPattern(pattern, p.GetFullPath(), MatchType.Win32))
                continue;
            if (set.Add(p)) yield return p;
        }
    }

    protected override IEnumerable<AbsolutePath> InternalEnumerateDirectories(AbsolutePath directory, string pattern, bool recursive)
    {
        var set = new HashSet<AbsolutePath>();
        if (_upstream.DirectoryExists(directory))
        foreach (var d in _upstream.EnumerateDirectories(directory, pattern, recursive))
        {
            if (set.Add(d)) yield return d;
        }

        foreach (var d in EnumerateSourceDirectoriesUnder(directory, recursive))
        {
            if (!EnumeratorHelpers.MatchesPattern(pattern, d.GetFullPath(), MatchType.Win32))
                continue;
            if (set.Add(d)) yield return d;
        }
    }

    protected override IEnumerable<IFileEntry> InternalEnumerateFileEntries(AbsolutePath directory, string pattern, bool recursive)
    {
        var yielded = new HashSet<AbsolutePath>();
        if (_upstream.DirectoryExists(directory))
        foreach (var entry in _upstream.EnumerateFileEntries(directory, pattern, recursive))
        {
            if (IsDeleted(entry.Path)) continue;
            if (yielded.Add(entry.Path)) yield return entry;
        }

        foreach (var p in EnumerateSourceFilesUnder(directory, recursive))
        {
            if (IsDeleted(p)) continue;
            if (!EnumeratorHelpers.MatchesPattern(pattern, p.GetFullPath(), MatchType.Win32))
                continue;
            if (yielded.Contains(p)) continue;
            if (TryResolveSource(p, out var src, out var rel))
            {
                using var s = src.OpenRead(rel);
                yield return new VirtualReadOnlyFileEntry(this, p, (ulong)s.Length);
            }
        }
    }

    protected override Stream InternalOpenFile(AbsolutePath path, FileMode mode, FileAccess access, FileShare share)
    {
        ClearDeletionIfUpstreamPresent(path);

        // If marked deleted, hide sources until recreated
        if (IsDeleted(path))
        {
            if (access == FileAccess.Read)
                throw new FileNotFoundException($"File not found: {path}");

            switch (mode)
            {
                case FileMode.Create:
                case FileMode.OpenOrCreate:
                    _upstream.CreateDirectory(path.Parent);
                    ClearDeleted(path);
                    TouchFile(path);
                    return _upstream.OpenFile(path, FileMode.Create, access, share);
                case FileMode.CreateNew:
                    _upstream.CreateDirectory(path.Parent);
                    ClearDeleted(path);
                    TouchFile(path);
                    return _upstream.OpenFile(path, FileMode.CreateNew, access, share);
                case FileMode.Truncate:
                case FileMode.Open:
                default:
                    throw new FileNotFoundException($"File not found: {path}");
            }
        }

        // Upstream has precedence
        if (_upstream.FileExists(path) || !TryResolveSource(path, out var src, out var rel))
        {
            // If opening for write, bump the FS timestamp
            if (access != FileAccess.Read) TouchFile(path);
            return _upstream.OpenFile(path, mode, access, share);
        }

        // Maps to read-only source
        if (access == FileAccess.Read)
        {
            if (mode == FileMode.Open || mode == FileMode.OpenOrCreate)
                return src.OpenRead(rel);
            if (mode == FileMode.CreateNew)
                throw new IOException($"File already exists: {path}");
            if (mode == FileMode.Create || mode == FileMode.Truncate)
                throw new ArgumentException($"Invalid mode/access combination: {mode}/{access}");
        }
        else // write or readwrite
        {
            switch (mode)
            {
                case FileMode.Open:
                    EnsureCopiedToUpstream(path, src, rel);
                    TouchFile(path);
                    return _upstream.OpenFile(path, FileMode.Open, access, share);
                case FileMode.OpenOrCreate:
                    if (TryResolveSource(path, out _, out _))
                    {
                        EnsureCopiedToUpstream(path, src, rel);
                        TouchFile(path);
                        return _upstream.OpenFile(path, FileMode.Open, access, share);
                    }
                    else
                    {
                        _upstream.CreateDirectory(path.Parent);
                        TouchFile(path);
                        return _upstream.OpenFile(path, FileMode.Create, access, share);
                    }
                case FileMode.Create:
                    _upstream.CreateDirectory(path.Parent);
                    TouchFile(path);
                    return _upstream.OpenFile(path, FileMode.Create, access, share);
                case FileMode.CreateNew:
                    if (TryResolveSource(path, out _, out _))
                        throw new IOException($"File already exists: {path}");
                    _upstream.CreateDirectory(path.Parent);
                    TouchFile(path);
                    return _upstream.OpenFile(path, FileMode.CreateNew, access, share);
                case FileMode.Truncate:
                    EnsureCopiedToUpstream(path, src, rel);
                    TouchFile(path);
                    return _upstream.OpenFile(path, FileMode.Truncate, access, share);
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        return _upstream.OpenFile(path, mode, access, share);
    }

    protected override void InternalCreateDirectory(AbsolutePath path)
    {
        _upstream.CreateDirectory(path);
    }

    protected override bool InternalDirectoryExists(AbsolutePath path)
    {
        if (_upstream.DirectoryExists(path)) return true;
        return SourceHasAnyUnder(path);
    }

    protected override void InternalDeleteDirectory(AbsolutePath path, bool recursive)
    {
        if (_upstream.DirectoryExists(path))
        {
            _upstream.DeleteDirectory(path, recursive);
            return;
        }
        throw new IOException($"Cannot delete read-only directory: {path}");
    }

    protected override bool InternalFileExists(AbsolutePath path)
    {
        ClearDeletionIfUpstreamPresent(path);
        if (IsDeleted(path)) return false;
        if (_upstream.FileExists(path)) return true;
        return TryResolveSource(path, out _, out _);
    }

    protected override void InternalDeleteFile(AbsolutePath path)
    {
        if (_upstream.FileExists(path))
        {
            _upstream.DeleteFile(path); _sourceMap.TryRemove(path, out _);
        }
        // Mark as deleted to hide any read-only source
        MarkDeleted(path);
        // Do not update global timestamps; per-file semantics only.
    }

    protected override void InternalMoveFile(AbsolutePath source, AbsolutePath dest, bool overwrite)
    {
        if (_upstream.FileExists(source))
        {
            _upstream.MoveFile(source, dest, overwrite); _sourceMap.TryRemove(source, out _); _sourceMap.TryRemove(dest, out _);
            TouchFile(dest);
            return;
        }

        if (TryResolveSource(source, out var src, out var rel))
        {
            EnsureCopiedToUpstream(source, src, rel);
            _upstream.MoveFile(source, dest, overwrite); _sourceMap.TryRemove(source, out _); _sourceMap.TryRemove(dest, out _);
            TouchFile(dest);
            return;
        }

        throw new FileNotFoundException($"File not found: {source}");
    }

    protected override unsafe MemoryMappedFileHandle InternalCreateMemoryMappedFile(AbsolutePath absPath, FileMode mode, MemoryMappedFileAccess access, ulong fileSize)
    {
        ClearDeletionIfUpstreamPresent(absPath);
        if (IsDeleted(absPath))
        {
            if (access == MemoryMappedFileAccess.Read)
                throw new FileNotFoundException($"File not found: {absPath}");
            _upstream.CreateDirectory(absPath.Parent);
            ClearDeleted(absPath);
            TouchFile(absPath);
            return _upstream.CreateMemoryMappedFile(absPath, FileMode.Create, access, fileSize);
        }

        if (_upstream.FileExists(absPath) || access != MemoryMappedFileAccess.Read)
        {
            if (!_upstream.FileExists(absPath) && TryResolveSource(absPath, out var s, out var r) && access != MemoryMappedFileAccess.Read)
            {
                EnsureCopiedToUpstream(absPath, s, r);
            }
            if (access != MemoryMappedFileAccess.Read) TouchFile(absPath);
            return _upstream.CreateMemoryMappedFile(absPath, mode, access, fileSize);
        }

        if (!TryResolveSource(absPath, out var src, out var rel))
            throw new FileNotFoundException($"File not found: {absPath}");

        using var stream = src.OpenRead(rel);
        var len = (int)stream.Length;
        var buffer = GC.AllocateUninitializedArray<byte>(len);
        stream.ReadExactly(buffer, 0, len);
        var pin = new Pin<byte>(buffer);
        return new MemoryMappedFileHandle(pin.Pointer, (nuint)buffer.Length, pin);
    }

    private IEnumerable<AbsolutePath> EnumerateSourceFilesUnder(AbsolutePath directory, bool recursive)
    {
        foreach (var src in _sources)
        {
            foreach (var rel in src.EnumerateFiles())
            {
                var abs = src.MountPoint / rel;
                if (recursive)
                {
                    if (abs.InFolder(directory) || abs.Parent == directory)
                        yield return abs.WithFileSystem(this);
                }
                else
                {
                    if (abs.Parent == directory)
                        yield return abs.WithFileSystem(this);
                }
            }
        }
    }

    private IEnumerable<AbsolutePath> EnumerateSourceDirectoriesUnder(AbsolutePath directory, bool recursive)
    {
        var set = new HashSet<AbsolutePath>();
        foreach (var src in _sources)
        {
            foreach (var rel in src.EnumerateFiles())
            {
                var abs = src.MountPoint / rel;
                foreach (var parent in abs.GetAllParents())
                {
                    if (!recursive && parent != abs.Parent) continue;
                    if (directory == parent || abs.InFolder(directory))
                    {
                        if (parent == abs) continue; // skip the file itself
                        if (parent == abs.GetRootDirectory()) continue;
                        var p = parent.WithFileSystem(this);
                        if (p.InFolder(directory) || p == directory)
                        {
                            if (set.Add(p)) yield return p;
                        }
                    }
                }
            }
        }
    }

    private bool TryResolveSource(AbsolutePath path, out IReadOnlyFileSource source, out RelativePath relative)
    {
        if (IsDeleted(path)) { source = default!; relative = default; return false; }
        if (_sourceMap.TryGetValue(path, out var entry)) { source = entry.source; relative = entry.rel; return true; }
        if (FindSourceMapping(path, out source, out relative)) { _sourceMap[path] = (source, relative); return true; }
        return false;
    }

    private bool FindSourceMapping(AbsolutePath path, out IReadOnlyFileSource source, out RelativePath relative)
    {
        foreach (var s in _sources)
        {
            if (path.InFolder(s.MountPoint) || path == s.MountPoint || path.Parent == s.MountPoint)
            {
                var rel = path.RelativeTo(s.MountPoint);
                if (rel == RelativePath.Empty && path == s.MountPoint)
                    continue;
                if (s.EnumerateFiles().Any(r => r.Equals(rel)))
                {
                    source = s;
                    relative = rel;
                    return true;
                }
            }
        }
        source = default!;
        relative = default;
        return false;
    }
    private bool SourceHasAnyUnder(AbsolutePath directory)
    {
        foreach (var s in _sources)
        {
            foreach (var rel in s.EnumerateFiles())
            {
                var abs = s.MountPoint / rel;
                if (abs.InFolder(directory) || abs.Parent == directory)
                    return true;
            }
        }
        return false;
    }
    private void EnsureCopiedToUpstream(AbsolutePath abs, IReadOnlyFileSource source, RelativePath rel)
    {
        if (_upstream.FileExists(abs)) { _sourceMap.TryRemove(abs, out _); return; }
        _upstream.CreateDirectory(abs.Parent);
        using var read = source.OpenRead(rel);
        using var write = _upstream.CreateFile(abs);
        read.CopyTo(write);
        _sourceMap.TryRemove(abs, out _);
        TouchFile(abs);
    }

    private sealed class VirtualReadOnlyFileEntry : IFileEntry
    {
        public IFileSystem FileSystem { get; set; }
        public AbsolutePath Path { get; }
        public Size Size { get; }
        public DateTime LastWriteTime
        {
            get
            {
                var roFs = FileSystem as ReadOnlySourcesFileSystem;
                if (roFs == null) return DateTime.UnixEpoch;
                if (roFs._lastWriteTicksUtc.TryGetValue(Path, out var ticks))
                    return new DateTime(ticks, DateTimeKind.Utc);
                return roFs._fsCreationTimeUtc;
            }
            set { /* ignored for read-only entries */ }
        }
        public DateTime CreationTime
        {
            get
            {
                var roFs = FileSystem as ReadOnlySourcesFileSystem;
                return roFs?._fsCreationTimeUtc ?? DateTime.UnixEpoch;
            }
            set { /* ignored for read-only entries */ }
        }
        public bool IsReadOnly { get; set; }

        public VirtualReadOnlyFileEntry(IFileSystem fs, AbsolutePath path, ulong size)
        {
            FileSystem = fs;
            Path = path;
            Size = Size.From(size);
            IsReadOnly = true;
        }

        public FileVersionInfo GetFileVersionInfo() => new(new Version(0,0,0,0), new Version(0,0,0,0), null, null);
    }

    private sealed class VirtualDirectoryEntry : IDirectoryEntry { }
}











