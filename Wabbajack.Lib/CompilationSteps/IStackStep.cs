using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Lib.CompilationSteps
{
    public interface ICompilationStep
    {
        Directive Run(RawSourceFile source);
    }
}
