using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IgnoreGameFilesIfGameFolderFilesExist : ACompilationStep
    {
        private readonly bool _gameFolderFilesExists;
        private readonly string _gameFolder;

        public IgnoreGameFilesIfGameFolderFilesExist(ACompiler compiler) : base(compiler)
        {
            _gameFolderFilesExists = Directory.Exists(Path.Combine(((MO2Compiler)compiler).MO2Folder, Consts.GameFolderFilesDir));
            _gameFolder = compiler.GamePath;
        }

        public override async ValueTask<Directive> Run(RawSourceFile source)
        {
            if (_gameFolderFilesExists)
            {
                if (source.AbsolutePath.IsInPath(_gameFolder))
                {
                    var result = source.EvolveTo<IgnoredDirectly>();
                    result.Reason = $"Ignoring game files because {Consts.GameFolderFilesDir} exists";
                    return result;
                }
            }

            return null;
        }

        public override IState GetState()
        {
            return new State();
        }

        public class State : IState
        {
            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new IgnoreGameFilesIfGameFolderFilesExist(compiler);
            }
        }
    }
}
