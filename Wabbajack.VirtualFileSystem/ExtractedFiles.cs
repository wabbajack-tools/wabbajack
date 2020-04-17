using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.VirtualFileSystem
{
    public class ExtractedFiles : IAsyncDisposable, IEnumerable<KeyValuePair<RelativePath, IExtractedFile>>
    {
        private Dictionary<RelativePath, IExtractedFile> _files;
        private IAsyncDisposable _disposable;
        private AbsolutePath _tempFolder;

        public ExtractedFiles(Dictionary<RelativePath, IExtractedFile> files, IAsyncDisposable disposeOther)
        {
            _files = files;
            _disposable = disposeOther;
        }

        public ExtractedFiles(TempFolder tempPath)
        {
            _files = tempPath.Dir.EnumerateFiles().ToDictionary(f => f.RelativeTo(tempPath.Dir),
                f => (IExtractedFile)new ExtractedDiskFile(f));
            _disposable = tempPath;
        }
        
        public async ValueTask DisposeAsync()
        {
            if (_disposable != null)
            {
                await _disposable.DisposeAsync();
                _disposable = null;
            }
        }

        public bool ContainsKey(RelativePath key)
        {
            return _files.ContainsKey(key);
        }

        public int Count => _files.Count;

        public IExtractedFile this[RelativePath key] => _files[key];
        public IEnumerator<KeyValuePair<RelativePath, IExtractedFile>> GetEnumerator()
        {
            return _files.GetEnumerator();
        }

        public async Task MoveAllTo(AbsolutePath folder)
        {
            foreach (var (key, value) in this)
            {
                await value.MoveTo(key.RelativeTo(folder));
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
