using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Paths.IO;
using Wabbajack.Test.TestingInfra;

namespace Wabbajack.Compression.BSA.Test;

public sealed class BsaClassConstructor : DiClassConstructorBase
{
    protected override void ConfigureServices(IServiceCollection service)
    {
        service.AddSingleton<TemporaryFileManager, TemporaryFileManager>();
        service.AddSingleton(new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        });
        service.AddSingleton(new JsonSerializerOptions());
    }
}
