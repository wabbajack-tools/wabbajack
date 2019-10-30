using System.Linq;
using Alphaleonis.Win32.Filesystem;

namespace Wabbajack.Lib.CompilationSteps
{
    class DirectMatch : ACompilationStep
    {
        public DirectMatch(Compiler compiler) : base(compiler)
        {
        }

        public override Directive Run(RawSourceFile source)
        {
            if (!_compiler.IndexedFiles.TryGetValue(source.Hash, out var found)) return null;
            var result = source.EvolveTo<FromArchive>();

            var match = found.Where(f =>
                                Path.GetFileName(f.Paths[f.Paths.Length - 1]) == Path.GetFileName(source.Path))
                            .OrderBy(f => f.Paths.Length)
                            .FirstOrDefault() 
                        ?? found.OrderBy(f => f.Paths.Length).FirstOrDefault();

            result.ArchiveHashPath = match.MakeRelativePaths();

            return result;

        }
    }
}
