using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths.IO;

namespace Wabbajack.DTOs.Test;

[ClassConstructor<DtosClassConstructor>]
public class ArchiveTests
{
    private readonly DTOSerializer _serializer;

    public ArchiveTests(DTOSerializer serializer)
    {
        _serializer = serializer;
    }


    [Test]
    public async Task CanLoadPolymorphicStates()
    {
        var jsonPath = KnownFolders.EntryPoint.Combine(@"Resources\HttpArchiveSample.json");
        var data = _serializer.Deserialize<Archive>(jsonPath.ReadAllText());
    }
}