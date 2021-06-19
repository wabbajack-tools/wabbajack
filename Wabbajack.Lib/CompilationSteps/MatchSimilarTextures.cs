using System.Linq;
using System.Threading.Tasks;
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
                .ToLookup(f => f.Name.FileName.FileNameWithoutExtension);
        }

        private static Extension DDS = new(".dds");


        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            if (source.Path.Extension == DDS)
            {
                var found = _byName[source.Path.FileNameWithoutExtension]
                    .Select(f => (f.ImageState.PerceptualHash.Similarity(source.File.ImageState.PerceptualHash), f))
                    .Where(f => f.Item1 >= 0.90f)
                    .OrderByDescending(f => f.Item1)
                    .FirstOrDefault();

                if (found == default) return null;

                var rv = source.EvolveTo<TransformedTexture>();
                rv.ArchiveHashPath = found.f.MakeRelativePaths();
                rv.ImageState = found.f.ImageState;
                
                return rv;
            }

            return null;
        }
    }
}
