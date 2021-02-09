using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IgnoreExtension : ACompilationStep
    {
        private readonly Extension _ext;
        private readonly string _reason;

        public IgnoreExtension(ACompiler compiler, Extension ext) : base(compiler)
        {
            _ext = ext;
            _reason = $"Ignoring {_ext} files";
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            if (source.Path.Extension != _ext)
                return null;

            var result = source.EvolveTo<IgnoredDirectly>();
            result.Reason = _reason;
            return result;
        }
    }
}
