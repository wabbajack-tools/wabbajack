using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.Hashing.PHash;
using Wabbajack.Paths;
using Wabbajack.VFS;

namespace Wabbajack.Compiler.CompilationSteps;

public class MatchSimilarTextures : ACompilationStep
{
    private const float PerceptualTolerance = 0.80f;

    private static readonly Extension DDS = new(".dds");


    private static readonly string[] PostFixes = {"_n", "_d", "_s"};
    private readonly ILookup<RelativePath, VirtualFile> _byName;

    public MatchSimilarTextures(ACompiler compiler) : base(compiler)
    {
        _byName = _compiler.IndexedFiles.SelectMany(kv => kv.Value)
            .Where(f => f.Name.FileName.Extension == DDS)
            .Where(f => f.ImageState != default)
            .ToLookup(f => f.Name.FileName.FileNameWithoutExtension);
    }

    public override async ValueTask<Directive?> Run(RawSourceFile source)
    {
        if (source.File.Name.FileName.Extension == DDS && source.File.ImageState != null)
        {
            _compiler._logger.LogInformation("Looking for texture match for {source}", source.File.FullPath);
            (float Similarity, VirtualFile File) found = _byName[source.Path.FileNameWithoutExtension]
                .Select(f => (
                    IImageLoader.ComputeDifference(f.ImageState!.PerceptualHash, source.File.ImageState.PerceptualHash),
                    f))
                .Select(f => { return f; })
                .OrderByDescending(f => f.Item1)
                .FirstOrDefault();

            if (found == default || found.Similarity <= PerceptualTolerance)
            {
                // This looks bad, but it's fairly simple: normal and displacement textures don't match very well
                // via perceptual hashing. So instead we'll try to find a diffuse map with the same name, and look
                // for normal maps in the same folders. Example: roof_n.dds didn't match, so find a match betweeen
                // roof.dds and a perceptual match in the downloads. Then try to find a roof_n.dds in the same folder
                // as the match we found for roof.dds. 
                found = default;
                var r = from postfix in PostFixes
                    where source.File.Name.FileName.FileNameWithoutExtension.EndsWith(postfix)
                    let mainFileName =
                        source.File.Name.FileName.FileNameWithoutExtension.ToString()[..^postfix.Length] +
                        ".dds"
                    let mainFile = source.File.InSameFolder(mainFileName.ToRelativePath())
                    where mainFile != null
                    from mainMatch in _byName[mainFile.FullPath.FileName.FileNameWithoutExtension]
                    where mainMatch.ImageState != null
                    where mainFile.ImageState != null
                    let similarity = IImageLoader.ComputeDifference(mainFile.ImageState!.PerceptualHash,
                        mainMatch.ImageState!.PerceptualHash)
                    where similarity >= PerceptualTolerance
                    orderby similarity descending
                    let foundFile = mainMatch.InSameFolder(source.Path.FileName)
                    where foundFile != null
                    select (similarity, postfix, mainFile, mainMatch, foundFile);

                foreach (var record in r)
                    _compiler._logger.LogInformation("Found Match for {source} {data}", source.File.Name,
                        record.foundFile.ImageState);

                var foundRec = r.FirstOrDefault();
                if (foundRec == default) return null;

                found = (foundRec.similarity, foundRec.foundFile);
            }

            _compiler._logger.LogInformation("Found Match for {source} {sourceData} {destData}", source.File.Name,
                source.File.ImageState, found.File.ImageState);

            var rv = source.EvolveTo<TransformedTexture>();
            rv.ArchiveHashPath = found.File.MakeRelativePaths();
            rv.ImageState = source.File.ImageState!;

            return rv;
        }

        return null;
    }
}