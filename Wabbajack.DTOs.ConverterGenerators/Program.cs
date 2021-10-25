using Wabbajack.DTOs.BSA.ArchiveStates;
using Wabbajack.DTOs.BSA.FileStates;
using Wabbajack.DTOs.DownloadStates;

namespace Wabbajack.DTOs.ConverterGenerators;

internal class Program
{
    private static void Main(string[] args)
    {
        var cfile = new CFile();
        new PolymorphicGenerator<IDownloadState>().GenerateAll(cfile);
        new PolymorphicGenerator<IArchive>().GenerateAll(cfile);
        new PolymorphicGenerator<Directive>().GenerateAll(cfile);
        new PolymorphicGenerator<AFile>().GenerateAll(cfile);


        cfile.Write(@"..\Wabbajack.DTOs\JsonConverters\Generated.cs");
    }
}