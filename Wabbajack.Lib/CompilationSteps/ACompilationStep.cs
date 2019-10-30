using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Lib.CompilationSteps
{
    public abstract class ACompilationStep : ICompilationStep
    {
        protected Compiler _compiler;

        public ACompilationStep(Compiler compiler)
        {
            _compiler = compiler;
        }

        public abstract Directive Run(RawSourceFile source);
    }
}
