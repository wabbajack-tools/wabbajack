using System;
using System.IO;
using System.Text;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Compiler.Test;

public class GetFileVersionTests
{
    [Fact]
    public void GetFileVersion_ReturnsVersionFromBinaryWhenFileVersionInfoReturnsEmpty()
    {
        var tmpPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe");
        try
        {
            File.WriteAllBytes(tmpPath, Encoding.Latin1.GetBytes("not a real PE 1.10.163.0 end"));
            var result = ((AbsolutePath)tmpPath).GetFileVersion();
            Assert.Equal("1.10.163.0", result);
        }
        finally
        {
            File.Delete(tmpPath);
        }
    }

    [Fact]
    public void GetFileVersion_ReturnsNullWhenNoVersionPresent()
    {
        var tmpPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe");
        try
        {
            File.WriteAllBytes(tmpPath, Encoding.Latin1.GetBytes("no version info here just text"));
            var result = ((AbsolutePath)tmpPath).GetFileVersion();
            Assert.Null(result);
        }
        finally
        {
            File.Delete(tmpPath);
        }
    }
}
