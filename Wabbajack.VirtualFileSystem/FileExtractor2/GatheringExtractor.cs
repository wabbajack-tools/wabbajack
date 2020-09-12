using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Compression.BSA;
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
        private Stream _stream;
        private Definitions.FileType _sig;
        private Exception _killException;
        private uint _itemsCount;

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
                    ulong checkPos = 1024 * 32;
                    _archive._archive.Open(_archive._archiveStream, ref checkPos, null);
                    _itemsCount = _archive._archive.GetNumberOfItems();
                    _archive._archive.Extract(null, 0xFFFFFFFF, 0, this);
                    _archive.Dispose();
                    if (_killException != null)
                    {
                        source.SetException(_killException);
                    }
                    else
                    {
                        source.SetResult(true);
                    }
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
            var entry = _archive.GetEntry(index);
            var path = (RelativePath)entry.FileName;
            if (entry.IsFolder || !_shouldExtract(path))
            {
                outStream = null;
                return 0;
            }

            Utils.Status($"Extracting {path}", Percent.FactoryPutInRange(_results.Count, _itemsCount));
            // Empty files are never extracted via a write call, so we have to fake that now
            if (entry.Size == 0)
            {
                var result = _mapFn(path, new MemoryStreamFactory(new MemoryStream(), path)).Result;
                _results.Add(path, result);
            }
            outStream =  new GatheringExtractorStream<T>(this, entry, path);
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
            private ulong _totalSize;
            private Stream _tmpStream;
            private TempFile _tmpFile;
            private bool _diskCached;
            private RelativePath _path;

            public GatheringExtractorStream(GatheringExtractor<T> extractor, Entry entry, RelativePath path)
            {
                _path = path;
                _extractor = extractor;
                _totalSize = entry.Size;
                _diskCached = _totalSize >= int.MaxValue - 1024;
            }

            private IPath GetPath()
            {
                return _path;
            }

            public int Write(byte[] data, uint size, IntPtr processedSize)
            {
                try
                {
                    if (size == _totalSize)
                        WriteSingleCall(data, size);
                    else if (_diskCached)
                        WriteDiskCached(data, size);
                    else
                        WriteMemoryCached(data, size);

                    if (processedSize != IntPtr.Zero)
                    {
                        Marshal.WriteInt32(processedSize, (int)size);
                    }
                    
                    return 0;
                }
                catch (Exception ex)
                {
                    Utils.Log($"Error during extraction {ex}");
                    _extractor.Kill(ex);

                    return 1;
                }
            }

            private void WriteSingleCall(byte[] data, in uint size)
            {
                var result = _extractor._mapFn(_path, new MemoryBufferFactory(data, (int)size, GetPath())).Result;
                AddResult(result);
                Cleanup();
            }

            private void Cleanup()
            {
                _tmpStream?.Dispose();
                _tmpFile?.DisposeAsync().AsTask().Wait();
            }

            private void AddResult(T result)
            {
                _extractor._results.Add(_path, result);
            }

            private void WriteMemoryCached(byte[] data, in uint size)
            {
                if (_tmpStream == null)
                    _tmpStream = new MemoryStream();
                _tmpStream.Write(data, 0, (int)size);

                if (_tmpStream.Length != (long)_totalSize) return;

                _tmpStream.Flush();
                _tmpStream.Position = 0;
                var result = _extractor._mapFn(_path, new MemoryStreamFactory((MemoryStream)_tmpStream, GetPath())).Result;
                AddResult(result);
                Cleanup();
            }

            private void WriteDiskCached(byte[] data, in uint size)
            {
                if (_tmpFile == null)
                {
                    _tmpFile = new TempFile();
                    _tmpStream = _tmpFile.Path.Create().Result;
                }
                
                _tmpStream.Write(data, 0, (int)size);

                if (_tmpStream.Length != (long)_totalSize) return;

                _tmpStream.Flush();
                _tmpStream.Close();
                
                var result = _extractor._mapFn(_path, new NativeFileStreamFactory(_tmpFile.Path, GetPath())).Result;
                AddResult(result);
                Cleanup();
            }

            public void Seek(long offset, uint seekOrigin, IntPtr newPosition)
            {
                
            }

            public int SetSize(long newSize)
            {
                return 0;
            }
        }

        private void Kill(Exception ex)
        {
            _killException = ex;
        }
    }
}
