using System.Threading.Tasks;
using Newtonsoft.Json;
using Wabbajack.Common;
#nullable enable

namespace Wabbajack.Lib.CompilationSteps
{
    public class IgnoreGameFiles : ACompilationStep
    {
        private readonly string _startDir;

        public IgnoreGameFiles(ACompiler compiler) : base(compiler)
        {
            _startDir = Consts.GameFolderFilesDir + "\\";
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            if (!((string)source.Path).StartsWith(_startDir)) return null;
            var i = source.EvolveTo<IgnoredDirectly>();
            i.Reason = "Default game file";
            return i;
        }

        public override IState GetState()
        {
            return new State();
        }

        [JsonObject("IgnoreGameFiles")]
        public class State : IState
        {
            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new IgnoreGameFiles(compiler);
            }
        }
    }
}
