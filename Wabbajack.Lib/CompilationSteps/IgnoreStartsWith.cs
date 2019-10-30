using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IgnoreStartsWith : ACompilationStep
    {
        private readonly string _reason;
        private readonly string _prefix;

        public IgnoreStartsWith(Compiler compiler, string prefix) : base(compiler)
        {
            _prefix = prefix;
            _reason = string.Format("Ignored because path starts with {0}", _prefix);
        }

        public override Directive Run(RawSourceFile source)
        {
            if (source.Path.StartsWith(_prefix))
            {
                var result = source.EvolveTo<IgnoredDirectly>();
                result.Reason = _reason;
                return result;
            }
            return null;
        }
    }
}
