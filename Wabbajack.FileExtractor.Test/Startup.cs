using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Xunit.DependencyInjection;
using Xunit.DependencyInjection.Logging;

namespace Wabbajack.FileExtractor.Test;

public class Startup
{
    public void ConfigureServices(IServiceCollection service)
    {
        service.AddSingleton<TemporaryFileManager, TemporaryFileManager>();
        service.AddSingleton(new ParallelOptions {MaxDegreeOfParallelism = Environment.ProcessorCount});
        service.AddAllSingleton<IResource, IResource<FileExtractor>, Resource<FileExtractor>>(s =>
            new Resource<FileExtractor>("File Extractor", 2));
        service.AddSingleton<FileExtractor>();
        service.AddSingleton(new JsonSerializerOptions());
    }

    public void Configure(ILoggerFactory loggerFactory, ITestOutputHelperAccessor accessor)
    {
        loggerFactory.AddProvider(new XunitTestOutputLoggerProvider(accessor, delegate { return true; }));
    }
}