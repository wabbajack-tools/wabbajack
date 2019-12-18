using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeVortexDeployment : ACompilationStep
    {
        public IncludeVortexDeployment(ACompiler compiler) : base(compiler)
        {
        }

        public override async ValueTask<Directive> Run(RawSourceFile source)
        {
            var l = new List<string> {"vortex.deployment.msgpack", "vortex.deployment.json"};
            if (!l.Any(a => source.Path.Contains(a))) return null;
            var inline = source.EvolveTo<InlineFile>();
            inline.SourceDataID = _compiler.IncludeFile(File.ReadAllBytes(source.AbsolutePath));
            if (!source.Path.Contains("vortex.deployment.json"))
                return inline;

            var path = source.Path;
            if (!path.StartsWith(Consts.GameFolderFilesDir))
                return inline;

            path = path.Substring(Consts.GameFolderFilesDir.Length + 1);
            path = $"{Consts.ManualGameFilesDir}\\{path}";
            inline.To = path;

            return inline;
        }

        public override IState GetState()
        {
            return new State();
        }

        public class State : IState
        {
            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new IncludeVortexDeployment(compiler);
            }
        }
    }
}
