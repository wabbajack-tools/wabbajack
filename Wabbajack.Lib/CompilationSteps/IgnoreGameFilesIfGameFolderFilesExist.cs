using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IgnoreGameFilesIfGameFolderFilesExist : ACompilationStep
    {
        private readonly bool _gameFolderFilesExists;
        private readonly AbsolutePath _gameFolder;

        public IgnoreGameFilesIfGameFolderFilesExist(ACompiler compiler) : base(compiler)
        {
            _gameFolderFilesExists = ((MO2Compiler)compiler).MO2Folder.Combine(Consts.GameFolderFilesDir).IsDirectory;
            _gameFolder = compiler.GamePath;
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
}
