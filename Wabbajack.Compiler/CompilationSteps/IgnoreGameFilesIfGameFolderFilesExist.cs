using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Compiler.CompilationSteps;

public class IgnoreGameFilesIfGameFolderFilesExist : ACompilationStep
{
    private readonly AbsolutePath _gameFolder;
    private readonly bool _gameFolderFilesExists;

    public IgnoreGameFilesIfGameFolderFilesExist(ACompiler compiler) : base(compiler)
    {
        _gameFolderFilesExists = _compiler._settings.Source.Combine(Consts.GameFolderFilesDir).DirectoryExists();
        _gameFolder = _compiler._locator.GameLocation(_compiler._settings.Game);
    }

    public override async ValueTask<Directive?> Run(RawSourceFile source)
    {
        if (!_gameFolderFilesExists) return null;

        if (!source.AbsolutePath.InFolder(_gameFolder)) return null;

        var result = source.EvolveTo<IgnoredDirectly>();
        result.Reason = $"Ignoring game files because {Consts.GameFolderFilesDir} exists";
        return result;
    }
}