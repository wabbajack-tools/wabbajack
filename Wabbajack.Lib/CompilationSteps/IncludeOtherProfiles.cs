using System.Collections.Generic;
using System.Linq;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IgnoreOtherProfiles : ACompilationStep
    {
        private readonly IEnumerable<string> _profiles;
        private readonly Compiler _mo2Compiler;

        public IgnoreOtherProfiles(ACompiler compiler) : base(compiler)
        {
            _mo2Compiler = (Compiler) compiler;

                _profiles = _mo2Compiler.SelectedProfiles
                .Select(p => Path.Combine("profiles", p) + "\\")
                .ToList();
        }

        public override Directive Run(RawSourceFile source)
        {
            if (!source.Path.StartsWith("profiles\\")) return null;
            if (_profiles.Any(profile => source.Path.StartsWith(profile))) return null;
            var c = source.EvolveTo<IgnoredDirectly>();
            c.Reason = "File not for selected profiles";
            return c;
        }

        public override IState GetState()
        {
            return new State();
        }

        [JsonObject("IgnoreOtherProfiles")]
        public class State : IState
        {
            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new IgnoreOtherProfiles(compiler);
            }
        }
    }
}