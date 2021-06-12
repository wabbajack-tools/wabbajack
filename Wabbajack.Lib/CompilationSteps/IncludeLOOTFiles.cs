using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeLootFiles : ACompilationStep
    {
        private readonly string _prefix;

        public IncludeLootFiles(ACompiler compiler) : base(compiler)
        {
            _prefix = Consts.LOOTFolderFilesDir + "\\";
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            if (!source.Path.StartsWith(_prefix)) return null;
            var result = source.EvolveTo<InlineFile>();
            result.SourceDataID = await _compiler.IncludeFile(await source.AbsolutePath.ReadAllBytesAsync());
            return result;
        }
    }
}
