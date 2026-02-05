using System;
using System.IO;

namespace Wabbajack.GameFinder.Paths;

public partial class InMemoryFileSystem
{
    private class InMemoryFileEntry : IFileEntry
    {
        private byte[] _contents;

        public InMemoryDirectoryEntry ParentDirectory { get; }

        public IFileSystem FileSystem { get; set; }

        public AbsolutePath Path { get; }

        public Size Size => Size.FromLong(_contents.Length);

        public DateTime LastWriteTime { get; set; }

        public DateTime CreationTime { get; set; }

        public bool IsReadOnly { get; set; }

        public InMemoryFileEntry(IFileSystem fs, AbsolutePath path, InMemoryDirectoryEntry parentDirectory)
        {
            FileSystem = fs;
            Path = path;
            ParentDirectory = parentDirectory;
            _contents = Array.Empty<byte>();
        }

        public InMemoryFileEntry(IFileSystem fs, AbsolutePath path, InMemoryDirectoryEntry parentDirectory, byte[] contents)
            : this(fs, path, parentDirectory)
        {
            _contents = contents;
        }

        public FileVersionInfo GetFileVersionInfo()
        {
            throw new NotImplementedException();
        }

        public MemoryStream CreateReadStream()
        {
            var ms = new MemoryStream(_contents, 0, _contents.Length, false, true);
            return ms;
        }

        public MemoryStream CreateWriteStream()
        {
            var stream = new MemoryStreamWrapper(this);
            return stream;
        }

        public MemoryStream CreateReadWriteStream()
        {
            var stream = new MemoryStreamWrapper(this);
            stream.Write(_contents);
            stream.Position = 0;
            return stream;
        }

        public byte[] GetContents()
        {
            return _contents;
        }

        public void SetContents(byte[] contents)
        {
            _contents = contents;
        }

        public override string ToString() => Path.ToString();

        /// <summary>
        /// Wrapper around <see cref="MemoryStream"/> that updates <see cref="InMemoryFileEntry"/>
        /// on dispose.
        /// </summary>
        private sealed class MemoryStreamWrapper : MemoryStream
        {
            private readonly InMemoryFileEntry _fileEntry;

            public MemoryStreamWrapper(InMemoryFileEntry fileEntry)
            {
                _fileEntry = fileEntry;
            }

            protected override void Dispose(bool disposing)
            {
                if (!disposing) return;
                _fileEntry.SetContents(ToArray());
                base.Dispose(disposing);
            }
        }
    }
}
