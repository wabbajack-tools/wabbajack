using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IgnoreOtherProfiles : ACompilationStep
    {
        private readonly IEnumerable<AbsolutePath> _profiles;
        private readonly MO2Compiler _mo2Compiler;
        private AbsolutePath _modProfilesFolder;

        public IgnoreOtherProfiles(ACompiler compiler) : base(compiler)
        {
            _mo2Compiler = (MO2Compiler) compiler;
            _modProfilesFolder = _mo2Compiler.SourcePath.Combine("profiles");

            _profiles = _mo2Compiler.SelectedProfiles
                .Select(p => _modProfilesFolder.Combine(p))
                .ToList();
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            if (!source.AbsolutePath.InFolder(_modProfilesFolder)) return null;
            if (_profiles.Any(profile => source.AbsolutePath.InFolder(profile))) return null;
            var c = source.EvolveTo<IgnoredDirectly>();
            c.Reason = "File not for selected profiles";
            return c;
        }
    }
}
