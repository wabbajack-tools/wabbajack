using System.Collections.Generic;
using System.Collections.Concurrent;
using JetBrains.Annotations;

namespace Wabbajack.GameFinder.Paths;

public partial class InMemoryFileSystem
{
    private class InMemoryDirectoryEntry : IDirectoryEntry
    {
        public AbsolutePath Path { get; }

        [PublicAPI]
        public InMemoryDirectoryEntry ParentDirectory { get; }

        public ConcurrentDictionary<RelativePath, InMemoryFileEntry> Files { get; } = new();

        public ConcurrentDictionary<RelativePath, InMemoryDirectoryEntry> Directories { get; } = new();

        public InMemoryDirectoryEntry(AbsolutePath path, InMemoryDirectoryEntry parentDirectory)
        {
            Path = path;
            ParentDirectory = parentDirectory;
        }

        public override string ToString() => Path.ToString();
    }
}
