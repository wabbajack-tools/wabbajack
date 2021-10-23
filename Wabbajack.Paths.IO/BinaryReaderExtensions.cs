using System.IO;

namespace Wabbajack.Paths.IO;

public static class BinaryReaderExtensions
{
    public static IPath ReadIPath(this BinaryReader rdr)
    {
        if (rdr.ReadBoolean()) return rdr.ReadAbsolutePath();

        return rdr.ReadRelativePath();
    }

    public static AbsolutePath ReadAbsolutePath(this BinaryReader rdr)
    {
        return rdr.ReadString().ToAbsolutePath();
    }

    public static RelativePath ReadRelativePath(this BinaryReader rdr)
    {
        return rdr.ReadString().ToRelativePath();
    }
}