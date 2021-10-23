using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.Paths.IO;

namespace Wabbajack.Compiler.CompilationSteps;

public class IncludeDummyESPs : ACompilationStep
{
    public IncludeDummyESPs(ACompiler compiler) : base(compiler)
    {
    }

    public override async ValueTask<Directive?> Run(RawSourceFile source)
    {
        if (source.AbsolutePath.Extension != Ext.Esp &&
            source.AbsolutePath.Extension != Ext.Esm) return null;

        var bsa = source.AbsolutePath.ReplaceExtension(Ext.Bsa);
        var bsaTextures = source.AbsolutePath.AppendToName(" - Textures").ReplaceExtension(Ext.Bsa);

        if (source.AbsolutePath.Size() > 250 || !bsa.FileExists() && !bsaTextures.FileExists()) return null;

        var inline = source.EvolveTo<InlineFile>();
        inline.SourceDataID = await _compiler.IncludeFile(await source.AbsolutePath.ReadAllBytesAsync());
        return inline;
    }
}