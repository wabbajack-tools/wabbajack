using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IgnoreEndsWith : ACompilationStep
    {
        private readonly string _reason;
        private readonly string _postfix;

        public IgnoreEndsWith(Compiler compiler, string postfix) : base(compiler)
        {
            _postfix = postfix;
            _reason = $"Ignored because path ends with {postfix}";
        }

        public override Directive Run(RawSourceFile source)
        {
            if (!source.Path.EndsWith(_postfix)) return null;
            var result = source.EvolveTo<IgnoredDirectly>();
            result.Reason = _reason;
            return result;

        }
    }
}
