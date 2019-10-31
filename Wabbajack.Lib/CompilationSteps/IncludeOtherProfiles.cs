using System.Collections.Generic;
using System.Linq;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IgnoreOtherProfiles : ACompilationStep
    {
        private readonly IEnumerable<string> _profiles;

        public IgnoreOtherProfiles(Compiler compiler) : base(compiler)
        {
            _profiles = _compiler.SelectedProfiles
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
            public ICompilationStep CreateStep(Compiler compiler)
            {
                return new IgnoreOtherProfiles(compiler);
            }
        }
    }
}