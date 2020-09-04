using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Common.FileSignatures;
using Wabbajack.VirtualFileSystem.SevenZipExtractor;

namespace Wabbajack.VirtualFileSystem
{
    public class GatheringExtractor<T> : IArchiveExtractCallback
    {
        private ArchiveFile _archive;
        private Predicate<RelativePath> _shouldExtract;
        private Func<RelativePath, IStreamFactory, ValueTask<T>> _mapFn;
        private Dictionary<RelativePath, T> _results;
        private Dictionary<uint, RelativePath> _indexes;
        private Stream _stream;
        private Definitions.FileType _sig;

        public GatheringExtractor(Stream stream, Definitions.FileType sig, Predicate<RelativePath> shouldExtract, Func<RelativePath,IStreamFactory, ValueTask<T>> mapfn)
        {
            
            _shouldExtract = shouldExtract;
            _mapFn = mapfn;
            _results = new Dictionary<RelativePath, T>();
            _stream = stream;
            _sig = sig;
        }

        public async Task<Dictionary<RelativePath, T>> Extract()
        {
            var source = new TaskCompletionSource<bool>();

            var th = new Thread(() =>
            {
                try
                {
                    _archive = ArchiveFile.Open(_stream, _sig).Result;
                    _indexes = _archive.Entries
                        .Where(f => !f.IsFolder)
                        .Select((entry, idx) => ((RelativePath)entry.FileName, (uint)idx))
                        .Where(t => _shouldExtract(t.Item1))
                        .ToDictionary(t => t.Item2, t => t.Item1);


                    _archive._archive.Extract(null, 0xFFFFFFFF, 0, this);
                    _archive.Dispose();
                    source.SetResult(true);
                }
                catch (Exception ex)
                {
                    source.SetException(ex);
                }
                
            }) {Priority = ThreadPriority.BelowNormal, Name = "7Zip Extraction Worker Thread"};
            th.Start();
            
            

            await source.Task;
            return _results;
        }

        public void SetTotal(ulong total)
        {
            
        }

        public void SetCompleted(ref ulong completeValue)
        {
            
        }

        public int GetStream(uint index, out ISequentialOutStream outStream, AskMode askExtractMode)
        {
            if (_indexes.ContainsKey(index))
            {
                outStream =  new GatheringExtractorStream<T>(this, index);
                return 0;
            }

            outStream = null;
            return 0;
        }

        public void PrepareOperation(AskMode askExtractMode)
        {
            
        }

        public void SetOperationResult(OperationResult resultEOperationResult)
        {
            
        }

        private class GatheringExtractorStream<T> : ISequentialOutStream, IOutStream
        {
            private GatheringExtractor<T> _extractor;
            private uint _index;

            public GatheringExtractorStream(GatheringExtractor<T> extractor, uint index)
            {
                _extractor = extractor;
                _index = index;
            }

            public int Write(IntPtr data, uint size, IntPtr processedSize)
            {
                unsafe
                {
                    var result = _extractor._mapFn(_extractor._indexes[_index], new UnmanagedStreamFactory((byte*)data, size)).AsTask().Result;

                    _extractor._results[_extractor._indexes[_index]] = result;
                
                    if (processedSize != IntPtr.Zero)
                    {
                        Marshal.WriteInt32(processedSize, (int) size);
                    }

                    return 0;
                }
            }

            public void Seek(long offset, uint seekOrigin, IntPtr newPosition)
            {
                
            }

            public int SetSize(long newSize)
            {
                return 0;
            }
        }
    }
}
