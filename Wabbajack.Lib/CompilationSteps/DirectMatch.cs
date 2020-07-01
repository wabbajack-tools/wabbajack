using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;
using Wabbajack.Lib.Downloaders;
using Wabbajack.VirtualFileSystem;

namespace Wabbajack.Lib.CompilationSteps
{
    public class DirectMatch : ACompilationStep
    {
        public DirectMatch(ACompiler compiler) : base(compiler)
        {
        }

        public static int GetFilePriority(MO2Compiler compiler, VirtualFile file)
        {
            var archive = file.TopParent;
            var adata = compiler.ArchivesByFullPath[archive.AbsoluteName];
            if (adata.State is GameFileSourceDownloader.State gs)
            {
                return gs.Game == compiler.CompilingGame.Game ? 2 : 3;
            }
            return 1;
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            var mo2Compiler = (MO2Compiler)_compiler;
            if (!_compiler.IndexedFiles.TryGetValue(source.Hash, out var found)) return null;
            var result = source.EvolveTo<FromArchive>();

            var match = found.Where(f => f.Name.FileName == source.Path.FileName)
                            .OrderBy(f => GetFilePriority(mo2Compiler, f))
                            .ThenBy(f => f.NestingFactor)
                            .FirstOrDefault()
                        ?? found.OrderBy(f => f.NestingFactor).FirstOrDefault();

            result.ArchiveHashPath = match.MakeRelativePaths();

            return result;
        }

        public override IState GetState()
        {
            return new State();
        }

        [JsonObject("DirectMatch")]
        public class State : IState
        {
            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new DirectMatch(compiler);
            }
        }
    }
}
