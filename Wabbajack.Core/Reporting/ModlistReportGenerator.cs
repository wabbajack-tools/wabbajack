using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging;
using Nettle;
using Wabbajack.Common;
using Wabbajack.DTOs.Directives;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Installer;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Reporting;

public sealed class ModlistReportModel
{
    public string Name { get; init; } = "";
    public string TotalInlinedSize { get; init; } = "";
    public object[] InlinedData { get; init; } = Array.Empty<object>();
    public string TotalPatchSize { get; init; } = "";
    public object[] PatchData { get; init; } = Array.Empty<object>();
    public string WabbajackSize { get; init; } = "";
}

public static class ModlistReportGenerator
{
    /// Generates <input>.html next to the .wabbajack and returns its path.
    public static async Task<AbsolutePath> GenerateAsync(
        DTOSerializer dtos,
        AbsolutePath input,
        ILogger logger,
        bool openInBrowser = false,
        CancellationToken token = default)
    {
        logger.LogInformation("Loading modlist {Input}", input);
        var modlist = await StandardInstaller.LoadFromFile(dtos, input);

        // Read patch entry sizes from the WJ archive
        Dictionary<string, long> patchSizes;
        using (var zip = new ZipArchive(input.Open(FileMode.Open, FileAccess.Read, FileShare.Read)))
            patchSizes = zip.Entries.ToDictionary(e => e.Name, e => e.Length);

        var archives = modlist.Archives.ToDictionary(a => a.Hash, a => a.Name);
        var bsas = modlist.Directives.OfType<CreateBSA>().ToDictionary(bsa => bsa.TempID.ToString());

        var inlinedData = modlist.Directives.OfType<InlineFile>()
            .Select(e => new
            {
                To = e.To.ToString(),
                Id = e.SourceDataID.ToString(),
                SizeInt = e.Size,
                Size = e.Size.ToFileSizeString()
            }).ToArray();

        string FixupTo(RelativePath path)
        {
            if (path.GetPart(0) != Consts.BSACreationDir.ToString()) return path.ToString();

            var bsaId = path.GetPart(1);
            if (!bsas.TryGetValue(bsaId, out var bsa)) return path.ToString();

            var relPath = RelativePath.FromParts(path.Parts[2..]);
            return $"<i> {bsa.To} </i> | {relPath}";
        }

        var patchData = modlist.Directives.OfType<PatchedFromArchive>()
            .Select(e => new
            {
                From = $"<i> {archives[e.ArchiveHashPath.Hash]} </i> | {string.Join(" | ", e.ArchiveHashPath.Parts.Select(p => p.ToString()))}",
                To = FixupTo(e.To),
                Id = e.PatchID.ToString(),
                PatchSize = patchSizes[e.PatchID.ToString()].ToFileSizeString(),
                PatchSizeInt = patchSizes[e.PatchID.ToString()],
                FinalSize = e.Size.ToFileSizeString()
            }).ToArray();

        var model = new ModlistReportModel
        {
            Name = modlist.Name,
            TotalInlinedSize = inlinedData.Sum(i => i.SizeInt).ToFileSizeString(),
            InlinedData = inlinedData,
            TotalPatchSize = patchData.Sum(i => i.PatchSizeInt).ToFileSizeString(),
            PatchData = patchData,
            WabbajackSize = input.Size().ToFileSizeString()
        };

        var template = await LoadTemplateAsync();
        var func = NettleEngine.GetCompiler().Compile(template);
        var html = await func(model, token);

        var safeName = string.Join("_", modlist.Name.Split(Path.GetInvalidFileNameChars()));
        var outPath = input.Parent.Combine($"{safeName}-report.html");

        await outPath.WriteAllTextAsync(html, token);
        logger.LogInformation("Exported modlist report to {Path}", outPath);

        if (openInBrowser)
            System.Diagnostics.Process.Start("explorer", outPath.ToString());

        return outPath;
    }

    private static async Task<string> LoadTemplateAsync()
    {
        var asm = typeof(ModlistReportGenerator).Assembly;
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("Wabbajack.Reporting.Resources.ModlistReport.html",
                                             StringComparison.OrdinalIgnoreCase))
                   ?? throw new InvalidOperationException(
                       "Embedded template 'Wabbajack.Reporting.Resources.ModlistReport.html' not found.");

        using var s = asm.GetManifestResourceStream(name)!;
        using var ms = new MemoryStream();
        await s.CopyToAsync(ms);
        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
