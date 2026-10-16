using Wabbajack.App.Avalonia.Util;
using Xunit;

namespace Wabbajack.App.Avalonia.Test.Installers;

public class PortableSystemParametersTests
{
    private const string MemInfo = "MemTotal:       32734188 kB\nMemFree:         1048576 kB\nSwapTotal:       8388604 kB\n";

    [Fact]
    public void ReadsKilobyteFieldsAsBytes()
    {
        Assert.Equal(32734188L * 1024, PortableSystemParameters.MemInfoBytes(MemInfo, "MemTotal"));
        Assert.Equal(8388604L * 1024, PortableSystemParameters.MemInfoBytes(MemInfo, "SwapTotal"));
    }

    [Fact]
    public void AFieldThatIsNotThereIsNull()
    {
        Assert.Null(PortableSystemParameters.MemInfoBytes(MemInfo, "HugePages_Total"));
        Assert.Null(PortableSystemParameters.MemInfoBytes("", "MemTotal"));
    }

    [Fact]
    public void AFieldIsMatchedWhole()
    {
        // MemFree must not answer for a field named Mem.
        Assert.Null(PortableSystemParameters.MemInfoBytes(MemInfo, "Mem"));
    }
}
