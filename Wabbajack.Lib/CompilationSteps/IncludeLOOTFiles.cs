using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeLootFiles : ACompilationStep
    {
        private readonly string _prefix;

        public IncludeLootFiles(ACompiler compiler) : base(compiler)
        {
            _prefix = Consts.LOOTFolderFilesDir + "\\";
        }

        public override Directive Run(RawSourceFile source)
        {
            if (!source.Path.StartsWith(_prefix)) return null;
            var result = source.EvolveTo<InlineFile>();
            result.SourceDataID = _compiler.IncludeFile(File.ReadAllBytes(source.AbsolutePath).ToBase64());
            return result;
        }

        public override IState GetState()
        {
            return new State();
        }

        [JsonObject("IncludeLootFiles")]
        public class State : IState
        {
            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new IncludeLootFiles(compiler);
            }
        }
    }
}