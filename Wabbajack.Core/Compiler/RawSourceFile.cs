using System.IO;
using Wabbajack.DTOs;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.VFS;

namespace Wabbajack.Compiler;

/// <summary>
///     Contains everything we know about a given file from the source folder
/// </summary>
public class RawSourceFile
{
    public readonly RelativePath Path;

    public RawSourceFile(VirtualFile file, RelativePath path)
    {
        File = file;
        Path = path;
    }

    public AbsolutePath AbsolutePath
    {
        get
        {
            if (!File.IsNative)
                throw new InvalidDataException("Can't get the absolute path of a non-native file");
            return File.FullPath.Base;
        }
    }

    public VirtualFile File { get; }

    public Hash Hash => File.Hash;

    public T EvolveTo<T>() where T : Directive, new()
    {
        var v = new T {To = Path, Hash = File.Hash, Size = File.Size};
        return v;
    }
}