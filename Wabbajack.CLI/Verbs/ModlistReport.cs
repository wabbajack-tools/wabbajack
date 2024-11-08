using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nettle;
using Wabbajack.CLI.Builder;
using Wabbajack.Common;
using Wabbajack.DTOs.Directives;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Installer;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.CLI.Verbs;

public class ModlistReport
{
    private readonly ILogger<ModlistReport> _logger;
    private readonly DTOSerializer _dtos;

    public ModlistReport(ILogger<ModlistReport> logger, DTOSerializer dtos)
    {
        _logger = logger;
        _dtos = dtos;
    }

    public static VerbDefinition Definition = new("modlist-report",
        "Generates a usage report for a Modlist file", new[]
        {
            new OptionDefinition(typeof(AbsolutePath), "i", "input", "Wabbajack file from which to generate a report"),
            new OptionDefinition(typeof(bool), "b", "browser", "Open report in browser after generating it (default true)")
        });
    
    private static async Task<string> ReportTemplate(object o)
    {
        var data = await (typeof(ModlistReport).Assembly.GetManifestResourceStream("Wabbajack.CLI.Resources.ModlistReport.html")!).ReadAllAsync();
        var func = NettleEngine.GetCompiler().Compile(Encoding.UTF8.GetString(data));
        return await func(o, CancellationToken.None);
    }

    public async Task<int> Run(AbsolutePath input, bool browser = true)
    {

        _logger.LogInformation("Loading modlist...");
        var modlist = await StandardInstaller.LoadFromFile(_dtos, input);

        Dictionary<string, long> patchSizes;
        using (var zip = new ZipArchive(input.Open(FileMode.Open, FileAccess.Read, FileShare.Read)))
        {
            patchSizes = zip.Entries.ToDictionary(e => e.Name, e => e.Length);
        }

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
            
            if (!bsas.TryGetValue(bsaId, out var bsa))
            {
                return path.ToString();
            }

            var relPath = RelativePath.FromParts(path.Parts[2..]);
            return $"<i> {bsa.To} </i> | {relPath}";
        }
        
        var patchData = modlist.Directives.OfType<PatchedFromArchive>()
            .Select(e => new
            {
                From = $"<i> {archives[e.ArchiveHashPath.Hash]} </i> | {string.Join(" | ", e.ArchiveHashPath.Parts.Select(e => e.ToString()))}",
                To = FixupTo(e.To),
                Id = e.PatchID.ToString(),
                PatchSize = patchSizes[e.PatchID.ToString()].ToFileSizeString(),
                PatchSizeInt = patchSizes[e.PatchID.ToString()],
                FinalSize = e.Size.ToFileSizeString(),
            }).ToArray();

        var data = await ReportTemplate(new
        {
            Name = modlist.Name,
            TotalInlinedSize = inlinedData.Sum(i => i.SizeInt).ToFileSizeString(),
            InlinedData = inlinedData,
            TotalPatchSize = patchData.Sum(i => i.PatchSizeInt).ToFileSizeString(),
            PatchData = patchData,
            WabbajackSize = input.Size().ToFileSizeString()
        });

        var path = input.WithExtension(Ext.Html);
        await path.WriteAllTextAsync(data);
        _logger.LogInformation($"Exported modlist report to {path}");

        if(browser) System.Diagnostics.Process.Start("explorer", path.ToString());
        return 0;
    }
}