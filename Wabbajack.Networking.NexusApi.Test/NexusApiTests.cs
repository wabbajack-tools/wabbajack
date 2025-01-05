using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Wabbajack.Networking.NexusApi.Test;

public class NexusApiTests
{
    private readonly NexusApi _api;

    public NexusApiTests(NexusApi api)
    {
        _api = api;
    }

    [Fact]
    [Trait("Category", "RequiresOAuth")]
    public async Task CanValidateUser()
    {
        var (info, headers) = await _api.Validate();
        Assert.True(info.IsPremium);
    }

    [Fact]
    [Trait("Category", "RequiresOAuth")]
    public async Task CanGetModInfo()
    {
        var (_, originalHeaders) = await _api.Validate();

        var (info, headers) = await _api.ModInfo("skyrimspecialedition", 12604);
        Assert.Equal("SkyUI", info.Name);

        var (files, _) = await _api.ModFiles("skyrimspecialedition", 12604);

        Assert.True(files.Files.Length > 0);

        var (file, _) = await _api.FileInfo("skyrimspecialedition", 12604,
            files.Files.OrderByDescending(f => f.FileId).First().FileId);

        Assert.Equal("MAIN", file.CategoryName);

        var (links, _) = await _api.DownloadLink("skyrimspecialedition", 12604, file.FileId);
        Assert.True(links.Length > 0);
    }
}
