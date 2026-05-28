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
        var gamePath = compiler._settings.GamePath != default
            ? compiler._settings.GamePath.ToString()
            : compiler._locator.GameLocation(compiler._settings.Game).ToString();
        return RemapData(
            data,
            gamePath,
            compiler._settings.Source.ToString(),
            compiler._settings.Downloads.ToString());
    }

    public static string RemapData(string data, string gamePath, string sourcePath, string downloadsPath)
    {
        data = ReplacePath(data, gamePath, Consts.GAME_PATH_MAGIC_BACK, Consts.GAME_PATH_MAGIC_DOUBLE_BACK, Consts.GAME_PATH_MAGIC_FORWARD);
        data = ReplacePath(data, sourcePath, Consts.MO2_PATH_MAGIC_BACK, Consts.MO2_PATH_MAGIC_DOUBLE_BACK, Consts.MO2_PATH_MAGIC_FORWARD);
        data = ReplacePath(data, downloadsPath, Consts.DOWNLOAD_PATH_MAGIC_BACK, Consts.DOWNLOAD_PATH_MAGIC_DOUBLE_BACK, Consts.DOWNLOAD_PATH_MAGIC_FORWARD);
        return data;
    }

    private static string ReplacePath(string data, string path, string magicBack, string magicDoubleBack, string magicForward)
    {
        // On Linux, MO2 runs under Wine/Proton which mounts the Linux filesystem as the Z: drive.
        // Replace Wine Z: path variants FIRST (they are longer and more specific) so that the
        // shorter native Linux path does not partially match and leave a dangling Z: prefix.
        if (path.StartsWith('/'))
        {
            var wineBack = "Z:" + path.Replace("/", "\\");
            data = data.Replace(wineBack, magicBack, StringComparison.InvariantCultureIgnoreCase);
            data = data.Replace(wineBack.Replace("\\", "\\\\"), magicDoubleBack, StringComparison.InvariantCultureIgnoreCase);
            data = data.Replace(wineBack.Replace("\\", "/"), magicForward, StringComparison.InvariantCultureIgnoreCase);
        }

        data = data.Replace(path, magicBack, StringComparison.InvariantCultureIgnoreCase);
        data = data.Replace(path.Replace("\\", "\\\\"), magicDoubleBack, StringComparison.InvariantCultureIgnoreCase);
        data = data.Replace(path.Replace("\\", "/"), magicForward, StringComparison.InvariantCultureIgnoreCase);

        return data;
    }
}