using System;
using System.IO;

namespace Wabbajack.GameFinder.Paths;

public partial class FileSystem
{
    private class FileEntry : IFileEntry
    {
        private readonly AbsolutePath _path;

        private FileInfo? _info;
        private FileInfo GetFileInfo() => _info ??= new FileInfo(_path.GetFullPath());

        /// <inheritdoc/>
        public IFileSystem FileSystem { get; set; }

        /// <inheritdoc/>
        public AbsolutePath Path => _path;

        /// <inheritdoc/>
        public Size Size => Size.FromLong(GetFileInfo().Length);

        /// <inheritdoc/>
        public DateTime LastWriteTime
        {
            get => GetFileInfo().LastWriteTime;
            set => GetFileInfo().LastWriteTime = value;
        }

        /// <inheritdoc/>
        public DateTime CreationTime
        {
            get => GetFileInfo().CreationTime;
            set => GetFileInfo().CreationTime = value;
        }

        /// <inheritdoc/>
        public bool IsReadOnly
        {
            get => GetFileInfo().IsReadOnly;
            set => GetFileInfo().IsReadOnly = value;
        }

        public FileEntry(IFileSystem fileSystem, AbsolutePath path)
        {
            FileSystem = fileSystem;
            _path = path;
        }

        /// <inheritdoc/>
        public FileVersionInfo GetFileVersionInfo()
        {
            var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(_path.GetFullPath());

            var sProductVersion = fvi.ProductVersion;
            if (!Version.TryParse(sProductVersion, out var productVersion))
            {
                productVersion = new Version(fvi.ProductMajorPart, fvi.ProductMinorPart, fvi.ProductBuildPart, fvi.ProductPrivatePart);
            }

            var sFileVersion = fvi.FileVersion;
            if (!Version.TryParse(sFileVersion, out var fileVersion))
            {
                fileVersion = new Version(fvi.FileMajorPart, fvi.FileMinorPart, fvi.FileBuildPart, fvi.FilePrivatePart);
            }

            return new FileVersionInfo(
                ProductVersion: productVersion,
                FileVersion: fileVersion,
                ProductVersionString: sProductVersion,
                FileVersionString: sFileVersion
            );
        }
    }
}
