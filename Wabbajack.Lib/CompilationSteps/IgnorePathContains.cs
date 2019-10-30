using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IgnorePathContains : ACompilationStep
    {
        private readonly string _pattern;
        private readonly string _reason;

        public IgnorePathContains(Compiler compiler, string pattern) : base(compiler)
        {
            _pattern = $"\\{pattern.Trim('\\')}\\";
            _reason = $"Ignored because path contains {_pattern}";
        }

        public override Directive Run(RawSourceFile source)
        {
            if (!source.Path.Contains(_pattern)) return null;
            var result = source.EvolveTo<IgnoredDirectly>();
            result.Reason = _reason;
            return result;

        }
    }
}
