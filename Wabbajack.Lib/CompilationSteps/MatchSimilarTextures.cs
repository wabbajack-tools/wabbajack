using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualBasic.Logging;
using Wabbajack.Common;
using Wabbajack.ImageHashing;
using Wabbajack.VirtualFileSystem;

namespace Wabbajack.Lib.CompilationSteps
{
    public class MatchSimilarTextures : ACompilationStep
    {
        private ILookup<RelativePath, VirtualFile> _byName;
        public MatchSimilarTextures(ACompiler compiler) : base(compiler)
        {
            _byName = _compiler.IndexedFiles.SelectMany(kv => kv.Value)
                .Where(f => f.Name.FileName.Extension == DDS)
                .Where(f => f.ImageState != null)
                .ToLookup(f => f.Name.FileName.FileNameWithoutExtension);
        }

        private const float PerceptualTolerance = 0.80f;

        private static Extension DDS = new(".dds");


        private static string[] PostFixes = new[] {"_n", "_d", "_s"};
        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            if (source.File.Name.FileName.Extension == DDS && source.File.ImageState != null)
            {
                (float Similarity, VirtualFile File) found = _byName[source.Path.FileNameWithoutExtension]
                    .Select(f => (f.ImageState.PerceptualHash.Similarity(source.File.ImageState.PerceptualHash), f))
                    .Select(f =>
                    {
                        Utils.Log($"{f.f.Name.FileName} similar {f.Item1}");
                        return f;
                    })
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
                            let mainFile = source.File.InSameFolder(new RelativePath(mainFileName))
                        where mainFile != null
                        from mainMatch in _byName[mainFile.FullPath.FileName.FileNameWithoutExtension]
                        where mainMatch.ImageState != null
                        where mainFile.ImageState != null
                            let similarity = mainFile.ImageState.PerceptualHash.Similarity(mainMatch.ImageState.PerceptualHash)
                        where similarity >= PerceptualTolerance
                        orderby similarity descending
                        let foundFile = mainMatch.InSameFolder(source.Path.FileName)
                            where foundFile != null
                        select (similarity, postfix, mainFile, mainMatch, foundFile);

                    var foundRec = r.FirstOrDefault();
                    if (foundRec == default)
                    {
                        return null;
                    }

                    found = (foundRec.similarity, foundRec.foundFile);
                }

                var rv = source.EvolveTo<TransformedTexture>();
                rv.ArchiveHashPath = found.File.MakeRelativePaths();
                rv.ImageState = found.File.ImageState;
                
                return rv;
            }

            return null;
        }
    }
}
