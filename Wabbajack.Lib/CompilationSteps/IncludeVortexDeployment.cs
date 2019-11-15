using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeVortexDeployment : ACompilationStep
    {
        public IncludeVortexDeployment(ACompiler compiler) : base(compiler)
        {
        }

        public override Directive Run(RawSourceFile source)
        {
            if (!source.Path.EndsWith("vortex.deployment.msgpack") &&
                !source.Path.EndsWith("\\vortex.deployment.json")) return null;
            var inline = source.EvolveTo<InlineFile>();
            inline.SourceDataID = _compiler.IncludeFile(File.ReadAllBytes(source.AbsolutePath));
            return inline;
        }

        public override IState GetState()
        {
            return new State();
        }

        public class State : IState
        {
            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new IncludeVortexDeployment(compiler);
            }
        }
    }
}
