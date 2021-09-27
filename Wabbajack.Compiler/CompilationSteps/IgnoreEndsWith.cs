using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.Paths;

namespace Wabbajack.Compiler.CompilationSteps
{
    public class IgnoreFilename : ACompilationStep
    {
        private readonly RelativePath _postfix;
        private readonly string _reason;

        public IgnoreFilename(ACompiler compiler, RelativePath postfix) : base(compiler)
        {
            _postfix = postfix;
            _reason = $"Ignored because path ends with {postfix}";
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            if (source.Path.FileName != _postfix.FileName) return null;
            var result = source.EvolveTo<IgnoredDirectly>();
            result.Reason = _reason;
            return result;
        }
    }
}
