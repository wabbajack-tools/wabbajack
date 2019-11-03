using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeAllConfigs : ACompilationStep
    {
        public IncludeAllConfigs(ACompiler compiler) : base(compiler)
        {
        }

        public override Directive Run(RawSourceFile source)
        {
            if (!Consts.ConfigFileExtensions.Contains(Path.GetExtension(source.Path))) return null;
            var result = source.EvolveTo<InlineFile>();
            result.SourceDataID = _compiler.IncludeFile(File.ReadAllBytes(source.AbsolutePath));
            return result;
        }

        public override IState GetState()
        {
            return new State();
        }

        [JsonObject("IncludeAllConfigs")]
        public class State : IState
        {
            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new IncludeAllConfigs(compiler);
            }
        }
    }
}