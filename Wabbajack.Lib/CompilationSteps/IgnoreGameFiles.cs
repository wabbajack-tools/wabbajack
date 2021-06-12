using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IgnoreGameFiles : ACompilationStep
    {
        private readonly string _startDir;

        public IgnoreGameFiles(ACompiler compiler) : base(compiler)
        {
            _startDir = Consts.GameFolderFilesDir + "\\";
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            if (!((string)source.Path).StartsWith(_startDir, System.StringComparison.OrdinalIgnoreCase)) return null;
            var i = source.EvolveTo<IgnoredDirectly>();
            i.Reason = "Default game file";
            return i;
        }
    }
}
