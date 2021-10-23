using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.DTOs.Test;

public class ArchiveTests
{
    private readonly DTOSerializer _serializer;

    public ArchiveTests(DTOSerializer serializer)
    {
        _serializer = serializer;
    }


    [Fact]
    public void CanLoadPolymorphicStates()
    {
        var jsonPath = KnownFolders.EntryPoint.Combine(@"Resources\HttpArchiveSample.json");
        var data = _serializer.Deserialize<Archive>(jsonPath.ReadAllText());
    }
}