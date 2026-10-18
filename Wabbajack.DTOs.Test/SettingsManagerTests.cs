using System;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;
using Xunit;

namespace Wabbajack.DTOs.Test;

/// <summary>
///     Saving settings while something else saves the same ones: the app and the CLI at once, or two test
///     processes on one machine. Every save used one temporary file per key, so one save could rename another's
///     temporary file away and the second rename then found nothing.
/// </summary>
public class SettingsManagerTests : IDisposable
{
    private readonly AbsolutePath _folder =
        KnownFolders.EntryPoint.Combine("settings-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (_folder.DirectoryExists()) _folder.DeleteDirectory();
    }

    private SettingsManager Manager() => new(NullLogger<SettingsManager>.Instance,
        new Services.OSIntegrated.Configuration {SavedSettingsLocation = _folder}, new DTOSerializer(Array.Empty<JsonConverter>()));

    [Fact]
    public async Task ConcurrentSavesOfOneSettingAllSucceed()
    {
        var first = Manager();
        var second = Manager();

        await Task.WhenAll(Enumerable.Range(0, 400)
            .Select(i => Task.Run(() => (i % 2 == 0 ? first : second).Save("resource_settings", new System.Collections.Generic.List<int> {i}))));

        Assert.Single(await first.Load<System.Collections.Generic.List<int>>("resource_settings"));
        Assert.Empty(Directory.GetFiles(_folder.ToString(), "*.temp"));
    }
}
