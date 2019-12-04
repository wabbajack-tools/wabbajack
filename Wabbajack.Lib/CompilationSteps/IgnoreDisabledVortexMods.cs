using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IgnoreDisabledVortexMods : ACompilationStep
    {
        private readonly VortexCompiler _vortexCompiler;

        public IgnoreDisabledVortexMods(ACompiler compiler) : base(compiler)
        {
            _vortexCompiler = (VortexCompiler) compiler;
        }

        public override async ValueTask<Directive> Run(RawSourceFile source)
        {
            var b = false;
            _vortexCompiler.ActiveArchives.Do(a =>
            {
                if (source.Path.Contains(a)) b = true;
            });
            if (b) return null;
            var r = source.EvolveTo<IgnoredDirectly>();
            r.Reason = "Disabled Archive";
            return r;
        }

        public override IState GetState()
        {
            return new State();
        }

        public class State : IState
        {
            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new IgnoreDisabledVortexMods(compiler);
            }
        }
    }
}
