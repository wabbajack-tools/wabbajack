using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.Paths.IO;

namespace Wabbajack.Compiler.CompilationSteps;

public class IncludeModIniData : ACompilationStep
{
    public IncludeModIniData(ACompiler compiler) : base(compiler)
    {
    }

    public override async ValueTask<Directive?> Run(RawSourceFile source)
    {
        if (!source.Path.InFolder(Consts.MO2ModFolderName) || source.Path.FileName != Consts.MetaIni) return null;
        var e = source.EvolveTo<InlineFile>();
        e.SourceDataID = await _compiler.IncludeFile(await source.AbsolutePath.ReadAllBytesAsync());
        return e;
    }
}