using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.App.Utilities;

public class ModListUtilities
{
    public static async Task<MemoryStream> GetModListImageStream(AbsolutePath modList)
    {
        await using var fs = modList.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        using var ar = new ZipArchive(fs, ZipArchiveMode.Read);
        var entry = ar.GetEntry("modlist-image.png");
        await using var stream = entry!.Open();
        return new MemoryStream(await stream.ReadAllAsync());
    }
}