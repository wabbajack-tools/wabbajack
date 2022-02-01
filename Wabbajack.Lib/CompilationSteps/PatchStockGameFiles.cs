using System.Linq;
using System.Threading.Tasks;
using F23.StringSimilarity;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack.Lib.CompilationSteps
{
    public class PatchStockGameFiles : ACompilationStep
    {
        private readonly Task<ILookup<Hash, Archive>> _files;
        private readonly Levenshtein _distFn;

        public PatchStockGameFiles(ACompiler compiler) : base(compiler)
        {
            _distFn = new Levenshtein();
            _files = Task.Run(async () => (await ClientAPI.GetGameFilesFromGithub(Game.SkyrimSpecialEdition, "1.5.97.0"))
                .ToLookup(f => f.Hash));
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            if (_compiler.CompilingGame.Game != Game.SkyrimSpecialEdition) return null;

            var found = (await _files)[source.Hash];
            if (!found.Any()) return null;

            var srcFile = _compiler.IndexedArchives
                .Where(f => f.State is GameFileSourceDownloader.State)
                .OrderBy(l => _distFn.Distance(l.File.Name.FileName.ToString(), source.Path.FileName.ToString()))
                .FirstOrDefault();

            if (srcFile == null) return null;

            var resolved = source.EvolveTo<PatchedFromArchive>();
            resolved.Choices = new[] {srcFile.File};
            resolved.FromHash = srcFile.File.Hash;
            return resolved;
        }
    }
}
