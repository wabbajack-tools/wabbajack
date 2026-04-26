using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.Paths;

namespace Wabbajack.Compiler.CompilationSteps;

public class IgnoreOtherProfiles : ACompilationStep
{
    private readonly MO2Compiler _mo2Compiler;
    private readonly IEnumerable<AbsolutePath> _profiles;
    private readonly AbsolutePath _modProfilesFolder;

    public IgnoreOtherProfiles(ACompiler compiler) : base(compiler)
    {
        _mo2Compiler = (MO2Compiler) compiler;
        _modProfilesFolder = _mo2Compiler._settings.Source.Combine("profiles");

        _profiles = _mo2Compiler._settings.AllProfiles
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