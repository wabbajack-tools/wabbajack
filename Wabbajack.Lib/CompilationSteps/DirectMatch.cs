using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;

namespace Wabbajack.Lib.CompilationSteps
{
    public class DirectMatch : ACompilationStep
    {
        public DirectMatch(ACompiler compiler) : base(compiler)
        {
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            if (!_compiler.IndexedFiles.TryGetValue(source.Hash, out var found)) return null;
            var result = source.EvolveTo<FromArchive>();

            var match = found.Where(f => f.Name.FileName == source.Path.FileName)
                            .OrderBy(f => f.NestingFactor)
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
