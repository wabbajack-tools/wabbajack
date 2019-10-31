using Newtonsoft.Json;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IgnoreGameFiles : ACompilationStep
    {
        private readonly string _startDir;

        public IgnoreGameFiles(Compiler compiler) : base(compiler)
        {
            _startDir = Consts.GameFolderFilesDir + "\\";
        }

        public override Directive Run(RawSourceFile source)
        {
            if (!source.Path.StartsWith(_startDir)) return null;
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
            public ICompilationStep CreateStep(Compiler compiler)
            {
                return new IgnoreGameFiles(compiler);
            }
        }
    }
}