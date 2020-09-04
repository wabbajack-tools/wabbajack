using System;
using System.Collections.Generic;
using System.IO;
using Wabbajack.Common;

namespace Wabbajack.VirtualFileSystem.SevenZipExtractor
{
    internal class TypedExtractor<T> : IArchiveExtractCallback
    {
        private Dictionary<RelativePath, T> _mappings;
        private Action<RelativePath, T, Func<Stream>> _callback;
        private Dictionary<uint, RelativePath> _indexToFile;

        public TypedExtractor(Dictionary<RelativePath, T> mappings, Action<RelativePath, T, Func<Stream>> callback)
        {
            _mappings = mappings;
            _callback = callback;
        }

        public void Extract(ArchiveFile file)
        {
            _indexToFile = new Dictionary<uint, RelativePath>();

            uint idx = 0;
            foreach (var entry in file.Entries)
            {
                var rel = (RelativePath)entry.FileName;
                if (_mappings.ContainsKey(rel)) ;
                {
                    _indexToFile.Add(idx, rel);
                }
                idx += 1;
            }

            file._archive.Extract(null, 0xFFFFFFFF, 0, this);
        }
        
        public void SetTotal(ulong total)
        {
            throw new System.NotImplementedException();
        }

        public void SetCompleted(ref ulong completeValue)
        {
            throw new System.NotImplementedException();
        }

        public int GetStream(uint index, out ISequentialOutStream outStream, AskMode askExtractMode)
        {
            outStream = null;
            throw new System.NotImplementedException();
        }

        public void PrepareOperation(AskMode askExtractMode)
        {
            throw new System.NotImplementedException();
        }

        public void SetOperationResult(OperationResult resultEOperationResult)
        {
            throw new System.NotImplementedException();
        }
    }
}
