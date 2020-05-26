using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.VirtualFileSystem
{
    public class ExtractedDiskFile : IExtractedFile
    {
        private AbsolutePath _path;

        public ExtractedDiskFile(AbsolutePath path)
        {
            if (path == default)
                throw new InvalidDataException("Path cannot be empty");
            _path = path;
        }

        public async Task<Hash> HashAsync()
        {
            return await _path.FileHashAsync();
        }
        public DateTime LastModifiedUtc => _path.LastModifiedUtc;
        public long Size => _path.Size;
        public async ValueTask<Stream> OpenRead()
        {
            return await _path.OpenRead();
        }

        public async Task<bool> CanExtract()
        {
            return await FileExtractor.CanExtract(_path);
        }

        public Task<ExtractedFiles> ExtractAll(WorkQueue queue, IEnumerable<RelativePath> onlyFiles)
        {
            return FileExtractor.ExtractAll(queue, _path, onlyFiles);
        }

        public async Task MoveTo(AbsolutePath path)
        {
            if (FileExtractor.MightBeArchive(_path.Extension))
            {
                path.Parent.CreateDirectory();
                await _path.CopyToAsync(path);
                return;
            }
            await _path.MoveToAsync(path, true);
            _path = path;
        }
    }
}
