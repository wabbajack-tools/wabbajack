using System;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.Paths.IO;

namespace Wabbajack.Compiler.CompilationSteps;

public class IncludeStubbedConfigFiles : ACompilationStep
{
    private readonly MO2Compiler _mo2Compiler;

    public IncludeStubbedConfigFiles(ACompiler compiler) : base(compiler)
    {
        _mo2Compiler = (MO2Compiler) compiler;
    }

    public override async ValueTask<Directive?> Run(RawSourceFile source)
    {
        return Consts.ConfigFileExtensions.Contains(source.Path.Extension) ? await RemapFile(source) : null;
    }

    private async Task<Directive?> RemapFile(RawSourceFile source)
    {
        var data = await source.AbsolutePath.ReadAllTextAsync();
        var originalData = data;

        data = RemapData(_mo2Compiler, data);

        if (data == originalData)
            return null;
        var result = source.EvolveTo<RemappedInlineFile>();
        result.SourceDataID = await _compiler.IncludeFile(Encoding.UTF8.GetBytes(data));
        return result;
    }

    public static string RemapData(ACompiler compiler, string data)
    {
        var gamePath = compiler._locator.GameLocation(compiler._settings.Game).ToString();
        data = data.Replace(gamePath, Consts.GAME_PATH_MAGIC_BACK, StringComparison.InvariantCultureIgnoreCase);
        data = data.Replace(gamePath.Replace("\\", "\\\\"), Consts.GAME_PATH_MAGIC_DOUBLE_BACK,
            StringComparison.InvariantCultureIgnoreCase);
        data = data.Replace(gamePath.Replace("\\", "/"), Consts.GAME_PATH_MAGIC_FORWARD,
            StringComparison.InvariantCultureIgnoreCase);

        var sourcePath = compiler._settings.Source.ToString();
        data = data.Replace(sourcePath, Consts.MO2_PATH_MAGIC_BACK, StringComparison.InvariantCultureIgnoreCase);
        data = data.Replace(sourcePath.Replace("\\", "\\\\"), Consts.MO2_PATH_MAGIC_DOUBLE_BACK,
            StringComparison.InvariantCultureIgnoreCase);
        data = data.Replace(sourcePath.Replace("\\", "/"), Consts.MO2_PATH_MAGIC_FORWARD,
            StringComparison.InvariantCultureIgnoreCase);

        var downloadsPath = compiler._settings.Source.ToString();
        data = data.Replace(downloadsPath, Consts.DOWNLOAD_PATH_MAGIC_BACK,
            StringComparison.InvariantCultureIgnoreCase);
        data = data.Replace(downloadsPath.Replace("\\", "\\\\"), Consts.DOWNLOAD_PATH_MAGIC_DOUBLE_BACK,
            StringComparison.InvariantCultureIgnoreCase);
        data = data.Replace(downloadsPath.Replace("\\", "/"), Consts.DOWNLOAD_PATH_MAGIC_FORWARD,
            StringComparison.InvariantCultureIgnoreCase);
        return data;
    }
}