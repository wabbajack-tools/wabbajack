using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Compiler.CompilationSteps;

public class IncludeThisProfile : ACompilationStep
{
    private readonly IEnumerable<AbsolutePath> _correctProfiles;
    private readonly MO2Compiler _mo2Compiler;

    public IncludeThisProfile(ACompiler compiler) : base(compiler)
    {
        _mo2Compiler = (MO2Compiler) compiler;
        _correctProfiles = _mo2Compiler._settings.SelectedProfiles
            .Select(p => _mo2Compiler.MO2ProfileDir.Parent.Combine(p)).ToList();
    }

    public override async ValueTask<Directive?> Run(RawSourceFile source)
    {
        if (!_correctProfiles.Any(p => source.AbsolutePath.InFolder(p)))
            return null;

        var data = source.Path.FileName == Consts.ModListTxt
            ? await ReadAndCleanModlist(source.AbsolutePath)
            : await source.AbsolutePath.ReadAllBytesAsync();

        var e = source.EvolveTo<InlineFile>();
        e.SourceDataID = await _compiler.IncludeFile(data);
        return e;
    }

    private async Task<byte[]> ReadAndCleanModlist(AbsolutePath absolutePath)
    {
        var alwaysEnabled = _mo2Compiler.ModInis
            .Where(f => IgnoreDisabledMods.HasFlagInNotes(f.Value, Consts.WABBAJACK_ALWAYS_ENABLE))
            .Select(f => f.Key)
            .Select(f => f.FileName.ToString())
            .Distinct();
        var lines = await absolutePath.ReadAllLinesAsync()
            .Where(l =>
            {
                return l.StartsWith("+")
                       || alwaysEnabled.Any(x => x.Equals(l.Substring(1)))
                       || l.EndsWith("_separator");
            }).ToList();
        return Encoding.UTF8.GetBytes(string.Join(Consts.LineSeparator, lines));
    }
}