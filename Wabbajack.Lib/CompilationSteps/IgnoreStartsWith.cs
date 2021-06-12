using System.Threading.Tasks;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IgnoreStartsWith : ACompilationStep
    {
        private readonly string _prefix;
        private readonly string _reason;

        public IgnoreStartsWith(ACompiler compiler, string prefix) : base(compiler)
        {
            _prefix = prefix;
            _reason = string.Format("Ignored because path starts with {0}", _prefix);
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            if (!((string)source.Path).StartsWith(_prefix))
            {
                return null;
            }

            var result = source.EvolveTo<IgnoredDirectly>();
            result.Reason = _reason;
            return result;

        }
    }
}
