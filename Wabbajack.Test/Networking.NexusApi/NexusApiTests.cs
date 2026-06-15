using System.Linq;
using System.Threading.Tasks;

namespace Wabbajack.Networking.NexusApi.Test;

[ClassConstructor<NexusApiClassConstructor>]
public class NexusApiTests
{
    private readonly NexusApi _api;

    public NexusApiTests(NexusApi api)
    {
        _api = api;
    }

    [Test]
    [Category("RequiresOAuth")]
    public async Task CanValidateUser()
    {
        var (info, headers) = await _api.Validate();
        await Assert.That(info.IsPremium).IsTrue();
    }

    [Test]
    [Category("RequiresOAuth")]
    public async Task CanGetModInfo()
    {
        var (_, originalHeaders) = await _api.Validate();

        var (info, headers) = await _api.ModInfo("skyrimspecialedition", 12604);
        await Assert.That(info.Name).IsEqualTo("SkyUI");

        var (files, _) = await _api.ModFiles("skyrimspecialedition", 12604);

        await Assert.That(files.Files.Length > 0).IsTrue();

        var (file, _) = await _api.FileInfo("skyrimspecialedition", 12604,
            files.Files.OrderByDescending(f => f.FileId).First().FileId);

        await Assert.That(file.CategoryName).IsEqualTo("MAIN");

        var (links, _) = await _api.DownloadLink("skyrimspecialedition", 12604, file.FileId);
        await Assert.That(links.Length > 0).IsTrue();
    }
}
