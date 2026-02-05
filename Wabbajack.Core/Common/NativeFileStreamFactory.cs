using System;
using System.IO;
using System.Threading.Tasks;
using Wabbajack.DTOs.Streams;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Common;

public class NativeFileStreamFactory : IStreamFactory
{
    protected AbsolutePath _file;

    private DateTime? _lastModifiedCache;

    public NativeFileStreamFactory(AbsolutePath file, IPath path)
    {
        _file = file;
        Name = path;
    }

    public NativeFileStreamFactory(AbsolutePath file)
    {
        _file = file;
        Name = file;
    }

    public ValueTask<Stream> GetStream()
    {
        return new ValueTask<Stream>(_file.Open(FileMode.Open, FileAccess.Read, FileShare.Read));
    }

    public DateTime LastModifiedUtc
    {
        get
        {
            _lastModifiedCache ??= _file.LastModifiedUtc();
            return _lastModifiedCache.Value;
        }
    }

    public IPath Name { get; }

    public AbsolutePath FullPath => (AbsolutePath) Name;
}