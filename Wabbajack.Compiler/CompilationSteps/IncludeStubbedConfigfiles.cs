using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.Paths;
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

    /// <summary>
    ///     Swaps the author's game, downloads and MO2 folders for markers the installer replaces with its own.
    ///     The downloads folder is usually inside the MO2 folder, so the longest folder goes first: done the
    ///     other way round, the MO2 marker would take the front of every downloads path and the install would
    ///     point it inside its own folder rather than at the downloads folder the user chose.
    /// </summary>
    public static string RemapData(ACompiler compiler, string data)
    {
        return RemapData(data, compiler._locator.GameLocation(compiler._settings.Game), compiler._settings.Source,
            compiler._settings.Downloads);
    }

    public static string RemapData(string data, AbsolutePath game, AbsolutePath mo2, AbsolutePath downloads)
    {
        var markers = new[]
        {
            (Folder: game,
                Back: Consts.GAME_PATH_MAGIC_BACK, DoubleBack: Consts.GAME_PATH_MAGIC_DOUBLE_BACK,
                Forward: Consts.GAME_PATH_MAGIC_FORWARD),
            (Folder: mo2,
                Back: Consts.MO2_PATH_MAGIC_BACK, DoubleBack: Consts.MO2_PATH_MAGIC_DOUBLE_BACK,
                Forward: Consts.MO2_PATH_MAGIC_FORWARD),
            (Folder: downloads,
                Back: Consts.DOWNLOAD_PATH_MAGIC_BACK, DoubleBack: Consts.DOWNLOAD_PATH_MAGIC_DOUBLE_BACK,
                Forward: Consts.DOWNLOAD_PATH_MAGIC_FORWARD)
        };

        // On Linux, MO2 and the game's tools run under Wine and write these folders as Z:\home\..., while a
        // native tool writes /home/...; both are the same folder.
        var folders = markers.SelectMany(m => new[] {m.Folder.ToString(), m.Folder.ToWindowsProgramPath()}
            .Distinct()
            .Select(path => (Path: path, m.Back, m.DoubleBack, m.Forward)));

        foreach (var folder in folders.Where(f => f.Path.Length > 0).OrderByDescending(f => f.Path.Length))
        {
            // A Unix path has no backslashes to tell the spellings apart, and it is written with forward ones.
            if (!folder.Path.Contains('\\'))
            {
                data = data.Replace(folder.Path, folder.Forward, StringComparison.InvariantCultureIgnoreCase);
                continue;
            }

            data = data.Replace(folder.Path, folder.Back, StringComparison.InvariantCultureIgnoreCase);
            data = data.Replace(folder.Path.Replace("\\", "\\\\"), folder.DoubleBack,
                StringComparison.InvariantCultureIgnoreCase);
            data = data.Replace(folder.Path.Replace("\\", "/"), folder.Forward,
                StringComparison.InvariantCultureIgnoreCase);
        }

        return data;
    }
}
