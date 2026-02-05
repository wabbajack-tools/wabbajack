using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.Paths;

namespace Wabbajack.Compiler.CompilationSteps;

public class IncludeTaggedMods : ACompilationStep
{
    private readonly IEnumerable<AbsolutePath> _includeDirectly;
    private readonly MO2Compiler _mo2Compiler;
    private readonly string _tag;

    public IncludeTaggedMods(ACompiler compiler, string tag) : base(compiler)
    {
        _mo2Compiler = (MO2Compiler) compiler;
        _tag = tag;
        _includeDirectly = _mo2Compiler.ModInis.Where(kv =>
        {
            var general = kv.Value["General"];
            if (general["notes"] != null && general["notes"].Contains(_tag))
                return true;
            return general["comments"] != null && general["comments"].Contains(_tag);
        }).Select(kv => kv.Key);
    }

    public override async ValueTask<Directive?> Run(RawSourceFile source)
    {
        if (!source.Path.InFolder(Consts.MO2ModFolderName)) return null;
        foreach (var modpath in _includeDirectly)
        {
            if (!source.AbsolutePath.InFolder(modpath)) continue;
            var result = source.EvolveTo<InlineFile>();
            result.SourceDataID = await _compiler.IncludeFile(source.AbsolutePath, CancellationToken.None);
            return result;
        }

        return null;
    }
}