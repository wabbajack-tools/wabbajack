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

        // Foo.esp beside Foo.BSA is the same pair to Windows, so it is the same pair here.
        var folder = source.AbsolutePath.Parent;
        var bsa = folder.FindIgnoringCase(source.AbsolutePath.ReplaceExtension(Ext.Bsa).FileName);
        var bsaTextures =
            folder.FindIgnoringCase(source.AbsolutePath.AppendToName(" - Textures").ReplaceExtension(Ext.Bsa).FileName);

        if (source.AbsolutePath.Size() > 250 || !bsa.FileExists() && !bsaTextures.FileExists()) return null;

        var inline = source.EvolveTo<InlineFile>();
        inline.SourceDataID = await _compiler.IncludeFile(await source.AbsolutePath.ReadAllBytesAsync());
        return inline;
    }
}