using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Compiler.CompilationSteps;

public class IgnoreDisabledMods : ACompilationStep
{
    private readonly IEnumerable<AbsolutePath> _allEnabledMods;
    private readonly MO2Compiler _mo2Compiler;

    public IgnoreDisabledMods(ACompiler compiler) : base(compiler)
    {
        _mo2Compiler = (MO2Compiler) compiler;

        if (!compiler.Settings.IsMO2Modlist)
        {
            Disabled = true;
            return;
        }

        _allEnabledMods = _mo2Compiler._settings.AllProfiles
            .SelectMany(p => _mo2Compiler._settings.Source.Combine("profiles", p, "modlist.txt").ReadAllLines())
            .Where(line => line.StartsWith("+") || line.EndsWith("_separator"))
            .Select(line => line[1..].ToRelativePath().RelativeTo(_mo2Compiler.MO2ModsFolder))
            .Concat(_mo2Compiler.Mo2Settings.AlwaysEnabled.Select(r => r.RelativeTo(_mo2Compiler.Settings.Source)))
            //.Except(alwaysDisabled)
            .ToList();
    }

    public override async ValueTask<Directive?> Run(RawSourceFile source)
    {
        if (!source.AbsolutePath.InFolder(_mo2Compiler.MO2ModsFolder)) return null;
        if (_allEnabledMods.Any(mod => source.AbsolutePath.InFolder(mod)))
            return null;
        var r = source.EvolveTo<IgnoredDirectly>();
        r.Reason = "Disabled Mod";
        return r;
    }

    public static bool HasFlagInNotes(dynamic data, string flag)
    {
        if (data == null)
            return false;
        if (data.General != null && data.General.notes != null &&
            data.General.notes.Contains(
                flag))
            return true;

        return data.General != null && data.General.comments != null &&
               data.General.comments.Contains(flag);
    }
}